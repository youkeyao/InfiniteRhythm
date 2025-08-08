using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class NoteManager : MonoBehaviour
{
    public LevelManager levelManager;
    public Transform controllerTransform;
    public Transform cameraTransform;
    public GameObject hitPrefab;

    // note
    public Vector3 noteScale = new Vector3(1, 1, 1);
    public Vector3 noteRotation = new Vector3(0, 0, 0);
    public Mesh noteMesh;
    public Material noteMaterial;
    public GameObject noteEffect;

    // road
    public Material roadMaterial;
    public Material lineMaterial;
    public int roadSegmentCount = 20;
    public float roadSpacing = 5;
    public float roadY = -0.2f;

    // hit
    public float hitThreshold = 1.5f;
    public float hitEffectDuration = 0.1f;
    public float shakeMagnitude = 0.5f;

    // statistics
    public int Combo => m_combo;
    public int Miss => m_miss;
    public int Hit => m_hit;

    List<float> m_noteSpawnTime = new List<float>();
    List<Matrix4x4> m_noteSpawnList = new List<Matrix4x4>();
    float m_trackThreshold = 0.1f;
    List<GameObject> m_hitHints = new List<GameObject>();

    float m_roadDistance = 0;
    float m_lineHalfWidth = 0.05f;
    Mesh m_roadMesh;
    List<Mesh> m_lineMesh = new List<Mesh>();
    Queue<Vector3> m_roadVertices = new Queue<Vector3>();
    List<Queue<Vector3>> m_lineVertices = new List<Queue<Vector3>>();
    List<MaterialPropertyBlock> m_linePropertyBlock = new List<MaterialPropertyBlock>();

    int m_comboCycle = 50;
    int m_combo = 0;
    int m_hit = 0;
    int m_miss = 0;

    MaterialPropertyBlock m_propertyBlock;

    public void Init()
    {
        float spacing = -transform.position.x * 2 / (levelManager.NumTracks - 1);
        for (int i = 0; i < levelManager.NumTracks; i++)
        {
            Vector3 offset = new Vector3(i * spacing, 0, 0);
            GameObject hitObject = Instantiate(hitPrefab, controllerTransform);
            hitObject.transform.localPosition = offset;
            hitObject.transform.localRotation = Quaternion.identity;
            hitObject.GetComponentInChildren<TextMeshPro>().text = levelManager.keyCodes[i].ToString();
            m_hitHints.Add(hitObject);
        }
        m_roadMesh = new Mesh();
        for (int i = 0; i < levelManager.NumTracks; i++)
        {
            m_lineMesh.Add(new Mesh());
            m_lineVertices.Add(new Queue<Vector3>());
            m_linePropertyBlock.Add(new MaterialPropertyBlock());
        }
        m_propertyBlock = new MaterialPropertyBlock();
    }

    public void Clear()
    {
        for (int i = 0; i < m_hitHints.Count; i++)
        {
            Destroy(m_hitHints[i]);
        }
        m_hitHints.Clear();
        m_noteSpawnTime.Clear();
        m_noteSpawnList.Clear();
        m_roadVertices.Clear();
        m_roadDistance = 0;
        m_lineMesh.Clear();
        m_lineVertices.Clear();
        m_linePropertyBlock.Clear();

        m_combo = 0;
        m_hit = 0;
        m_miss = 0;
    }

    public void Update()
    {
        if (levelManager.IsPlaying)
        {
            GenerateRoad();
            GenerateNote();
            HitNote();
        }

        // Draw
        Graphics.DrawMesh(m_roadMesh, Matrix4x4.identity, roadMaterial, 0);
        for (int i = 0; i < m_lineMesh.Count; i++)
        {
            Graphics.DrawMesh(m_lineMesh[i], Matrix4x4.identity, lineMaterial, 0, null, 0, m_linePropertyBlock[i]);
        }
        Graphics.DrawMeshInstanced(noteMesh, 0, noteMaterial, m_noteSpawnList.ToArray(), m_noteSpawnList.Count);
    }

    void GenerateRoad()
    {
        float padding = -transform.position.x / (levelManager.NumTracks - 1);
        float spacing = -transform.position.x * 2 / (levelManager.NumTracks - 1);

        // Dispose
        while (m_roadVertices.Count > 0 && Vector3.Dot(m_roadVertices.Peek() - cameraTransform.position, cameraTransform.forward) < 0)
        {
            m_roadVertices.Dequeue();
            m_roadVertices.Dequeue();
            for (int i = 0; i < m_lineVertices.Count; i++)
            {
                m_lineVertices[i].Dequeue();
                m_lineVertices[i].Dequeue();
            }
        }
        while (m_roadDistance < CurveGenerator.GetLength(0))
        {
            Matrix4x4 roadTransform = CurveGenerator.GetTransform(m_roadDistance, 0) * CurveGenerator.GetRotation(m_roadDistance) * transform.localToWorldMatrix;
            Vector3 leftVertex = roadTransform.MultiplyPoint3x4(new Vector3(-padding, roadY, 0));
            Vector3 rightVertex = roadTransform.MultiplyPoint3x4(new Vector3(-transform.position.x * 2 + padding, roadY, 0));
            m_roadVertices.Enqueue(leftVertex);
            m_roadVertices.Enqueue(rightVertex);
            for (int i = 0; i < m_lineVertices.Count; i++)
            {
                leftVertex = roadTransform.MultiplyPoint3x4(new Vector3(i * spacing - m_lineHalfWidth, 0, 0));
                rightVertex = roadTransform.MultiplyPoint3x4(new Vector3(i * spacing + m_lineHalfWidth, 0, 0));
                m_lineVertices[i].Enqueue(leftVertex);
                m_lineVertices[i].Enqueue(rightVertex);
            }
            m_roadDistance += roadSpacing;
        }

        BuildMesh(m_roadMesh, m_roadVertices);
        for (int i = 0; i < m_lineVertices.Count; i++)
        {
            BuildMesh(m_lineMesh[i], m_lineVertices[i]);
        }
    }

    static void BuildMesh(Mesh mesh, Queue<Vector3> vertices)
    {
        if (vertices.Count >= 4)
        {
            int[] indices = new int[(vertices.Count - 2) * 3];
            int count = 0;
            for (int i = 3; i < vertices.Count; i += 2)
            {
                indices[count++] = i - 2;
                indices[count++] = i - 3;
                indices[count++] = i - 1;
                indices[count++] = i - 1;
                indices[count++] = i - 0;
                indices[count++] = i - 2;
            }
            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = indices;
        }
    }

    void GenerateNote()
    {
        float currentTime = Time.time - levelManager.StartTime;
        float spacing = -transform.position.x * 2 / (levelManager.NumTracks - 1);

        // Dispose
        while (m_noteSpawnTime.Count > 0 && m_noteSpawnTime[0] < currentTime - 1)
        {
            m_noteSpawnTime.RemoveAt(0);
            m_noteSpawnList.RemoveAt(0);
            m_combo = 0;
            m_miss = 0;
        }

        // Spawn
        Queue<Note> chart = ChartGenerator.GetChart();
        while (chart.Count > 0 && chart.Peek().time * levelManager.speed < CurveGenerator.GetLength(0))
        {
            Note note = chart.Dequeue();
            Vector3 offset = new Vector3(note.track * spacing, 0, 0);
            Matrix4x4 spawnTransform = CurveGenerator.GetTransform(note.time * levelManager.speed, 0) * CurveGenerator.GetRotation(note.time * levelManager.speed);
            m_noteSpawnList.Add(spawnTransform * Matrix4x4.Translate(offset) * transform.localToWorldMatrix * Matrix4x4.Rotate(Quaternion.Euler(noteRotation)) * Matrix4x4.Scale(noteScale));
            m_noteSpawnTime.Add(note.time);
        }
    }

    void HitNote()
    {
        float currentTime = Time.time - levelManager.StartTime;
        float spacing = -transform.position.x * 2 / (levelManager.NumTracks - 1);

        for (int i = 0; i < m_noteSpawnList.Count; i++)
        {
            Vector3 position = m_noteSpawnList[i].GetPosition();
            Matrix4x4 targetTransform = CurveGenerator.GetTransform(currentTime * levelManager.speed, 0) * CurveGenerator.GetRotation(currentTime * levelManager.speed);
            Vector3 targetPosition = targetTransform.GetPosition();
            Vector3 targetDirection = targetTransform.MultiplyVector(Vector3.forward);
            Vector3 trackDirection = targetTransform.MultiplyVector(Vector3.right);
            float targetDistance = Vector3.Dot(position - targetPosition, targetDirection);
            if (targetDistance > hitThreshold)
                break;
            if (targetDistance > -hitThreshold)
            {
                for (int j = 0; j < levelManager.keyCodes.Length; j++)
                {
                    Vector3 trackPosition = targetPosition + targetTransform.MultiplyVector(Vector3.right * (transform.position.x + j * spacing));
                    if (Input.GetKeyDown(levelManager.keyCodes[j]) && Mathf.Abs(Vector3.Dot(trackPosition - position, trackDirection)) < m_trackThreshold)
                    {
                        m_hit++;
                        m_combo++;

                        StartCoroutine(HitRoutine(j));

                        GameObject effect = Instantiate(noteEffect, controllerTransform);
                        effect.transform.position = position;
                        effect.transform.rotation = m_noteSpawnList[i].rotation * Quaternion.LookRotation(new Vector3(0, 0, 1));
                        ParticleSystem ps = noteEffect.GetComponentInChildren<ParticleSystem>();
                        Destroy(effect, ps.main.duration + ps.main.startLifetime.constantMax);

                        m_noteSpawnTime.RemoveAt(i);
                        m_noteSpawnList.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
    }

    IEnumerator HitRoutine(int trackID)
    {
        float elapsed = 0f;
        bool isCombo = m_combo % m_comboCycle == 0;
        Vector3 initPosition = cameraTransform.localPosition;
        Renderer renderer = m_hitHints[trackID].GetComponentInChildren<Renderer>();
        while (elapsed < hitEffectDuration)
        {
            elapsed += Time.deltaTime;

            float hitStrength = Mathf.Sin(elapsed / hitEffectDuration * Mathf.PI);

            m_hitHints[trackID].transform.localScale = new Vector3(hitStrength + 1, 1, hitStrength + 1);
            if (isCombo)
            {
                cameraTransform.localPosition = initPosition + new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0) * shakeMagnitude;
            }

            // hit color
            renderer.GetPropertyBlock(m_propertyBlock);
            m_propertyBlock.SetFloat("_Hit", hitStrength);
            renderer.SetPropertyBlock(m_propertyBlock);
            m_linePropertyBlock[trackID].SetFloat("_Hit", hitStrength);

            yield return null;
        }
        renderer.GetPropertyBlock(m_propertyBlock);
        m_propertyBlock.SetFloat("_Hit", 0);
        renderer.SetPropertyBlock(m_propertyBlock);
        m_linePropertyBlock[trackID].SetFloat("_Hit", 0);

        m_hitHints[trackID].transform.localScale = new Vector3(1, 1, 1);
        cameraTransform.localPosition = initPosition;
    }
}