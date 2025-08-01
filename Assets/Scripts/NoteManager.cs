using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class NoteManager : MonoBehaviour
{
    public LevelManager levelManager;
    public AudioManager audioManager;
    public Transform cameraTransform;
    public GameObject hitPrefab;
    public Vector3 noteScale = new Vector3(1, 1, 1);
    public Vector3 noteRotation = new Vector3(0, 0, 0);
    public Mesh noteMesh;
    public Material noteMaterial;
    public float hitThreshold = 1.5f;
    public float hitShakeDuration = 0.1f;
    public float hitShakeAmplitude = 0.05f;
    public float hitShakeFreq = 10f;

    List<float> m_spawnTime = new List<float>();
    List<Matrix4x4> m_spawnList = new List<Matrix4x4>();
    float m_trackThreshold = 0.1f;
    bool m_isInit = false;
    List<GameObject> m_hitHints = new List<GameObject>();

    void Init()
    {
        float spacing = -transform.position.x * 2 / (levelManager.NumTracks - 1);
        for (int i = 0; i < levelManager.NumTracks; i++)
        {
            Vector3 offset = new Vector3(i * spacing, 0, 0);
            GameObject hitObject = Instantiate(hitPrefab, cameraTransform);
            hitObject.transform.position = transform.position + offset;
            hitObject.transform.rotation = transform.rotation;
            hitObject.GetComponentInChildren<TextMeshPro>().text = levelManager.keyCodes[i].ToString();
            m_hitHints.Add(hitObject);
        }
    }

    public void Update()
    {
        if (levelManager.isPlaying)
        {
            if (!m_isInit)
            {
                Init();
                m_isInit = true;
            }
            float currentTime = Time.time - levelManager.startTime;
            float spacing = -transform.position.x * 2 / (levelManager.NumTracks - 1);

            // Dispose
            while (m_spawnTime.Count > 0 && m_spawnTime[0] < currentTime - 1)
            {
                m_spawnTime.RemoveAt(0);
                m_spawnList.RemoveAt(0);
            }

            // Spawn
            Queue<Note> chart = audioManager.GetChart();
            while (chart.Count > 0)
            {
                Note note = chart.Dequeue();
                Vector3 offset = new Vector3(note.track * spacing, 0, 0);
                Matrix4x4 spawnTransform = CurveGenerator.GetTransform(note.time * levelManager.speed, 0) * CurveGenerator.GetRotation(note.time * levelManager.speed);
                m_spawnList.Add(spawnTransform * Matrix4x4.Translate(offset) * transform.localToWorldMatrix * Matrix4x4.Rotate(Quaternion.Euler(noteRotation)) * Matrix4x4.Scale(noteScale));
                m_spawnTime.Add(note.time);
            }

            // Hit
            for (int i = 0; i < m_spawnList.Count; i++)
            {
                Vector3 position = m_spawnList[i].GetPosition();
                Matrix4x4 targetTransform = CurveGenerator.GetTransform(currentTime * levelManager.speed, 0);
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
                            m_spawnTime.RemoveAt(i);
                            m_spawnList.RemoveAt(i);
                            i--;
                            StartCoroutine(ShakeRoutine(j));
                        }
                    }
                }
            }

            // Draw
            Graphics.DrawMeshInstanced(noteMesh, 0, noteMaterial, m_spawnList.ToArray(), m_spawnList.Count);
        }
        else
        {
            if (m_isInit)
            {
                m_isInit = false;
                for (int i = 0; i < m_hitHints.Count; i++)
                {
                    Destroy(m_hitHints[i]);
                }
                m_hitHints.Clear();
                m_spawnTime.Clear();
                m_spawnList.Clear();
            }
        }
    }

    IEnumerator ShakeRoutine(int trackID)
    {
        float elapsed = 0f;
        Vector3 initialPosition = m_hitHints[trackID].transform.localPosition;
        while (elapsed < hitShakeDuration)
        {
            elapsed += Time.deltaTime;

            float x = Mathf.Sin(elapsed * hitShakeFreq * Mathf.PI * 2) * hitShakeAmplitude;
            float y = Mathf.Sin(elapsed * hitShakeFreq * Mathf.PI * 2 + 1) * hitShakeAmplitude;
            float z = 0;

            m_hitHints[trackID].transform.localPosition = initialPosition + new Vector3(x, y, z);

            yield return null;
        }
        
        m_hitHints[trackID].transform.localPosition = initialPosition;
    }
}