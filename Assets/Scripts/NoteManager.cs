using System.Collections.Generic;
using UnityEngine;

public class NoteManager : MonoBehaviour
{
    public LevelManager levelManager;
    public Vector3 noteScale = new Vector3(1, 0.3f, 1);
    public Mesh noteMesh;
    public Material noteMaterial;
    public float hitThreshold = 1.5f;
    public float showDistance = 50.0f;
    public KeyCode[] keyCodes = new KeyCode[]
    {
        KeyCode.A,
        KeyCode.D,
        KeyCode.J,
        KeyCode.L,
    };

    List<Note> m_chart;
    List<float> m_spawnTime = new List<float>();
    List<Matrix4x4> m_spawnList = new List<Matrix4x4>();
    int m_noteIndex = 0;
    float m_trackThreshold = 0.1f;

    public void Play(List<Note> chart)
    {
        m_chart = chart;
        m_noteIndex = 0;
    }

    public void Update()
    {
        if (levelManager.isPlaying)
        {
            float currentTime = Time.time - levelManager.startTime;
            float spacing = -transform.position.x * 2 / (keyCodes.Length - 1);

            // Dispose
            while (m_spawnTime.Count > 0 && m_spawnTime[0] < currentTime - 1)
            {
                m_spawnTime.RemoveAt(0);
                m_spawnList.RemoveAt(0);
            }

            // Spawn
            while (m_noteIndex < m_chart.Count && currentTime >= m_chart[m_noteIndex].time - showDistance / levelManager.speed)
            {
                Vector3 offset = new Vector3(m_chart[m_noteIndex].track * spacing, 0, 0);
                Matrix4x4 spawnTransform = RoadGenerator.GetTransform(m_chart[m_noteIndex].time * levelManager.speed);
                m_spawnList.Add(spawnTransform * Matrix4x4.Translate(offset) * transform.localToWorldMatrix * Matrix4x4.Scale(noteScale));
                m_spawnTime.Add(m_chart[m_noteIndex].time);
                m_noteIndex++;
            }

            // Hit
            for (int i = 0; i < m_spawnList.Count; i++)
            {
                Vector3 position = m_spawnList[i].GetPosition();
                Matrix4x4 targetTransform = RoadGenerator.GetTransform(currentTime * levelManager.speed);
                Vector3 targetPosition = targetTransform.GetPosition();
                Vector3 targetDirection = targetTransform.MultiplyVector(Vector3.forward);
                Vector3 trackDirection = targetTransform.MultiplyVector(Vector3.right);
                float targetDistance = Vector3.Dot(position - targetPosition, targetDirection);
                if (targetDistance > hitThreshold)
                    break;
                if (targetDistance > -hitThreshold)
                {
                    for (int j = 0; j < keyCodes.Length; j++)
                    {
                        Vector3 trackPosition = targetPosition + targetTransform.MultiplyVector(Vector3.right * (transform.position.x + j * spacing));
                        if (Input.GetKeyDown(keyCodes[j]) && Mathf.Abs(Vector3.Dot(trackPosition - position, trackDirection)) < m_trackThreshold)
                        {
                            m_spawnTime.RemoveAt(i);
                            m_spawnList.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }

            // Draw
            Graphics.DrawMeshInstanced(noteMesh, 0, noteMaterial, m_spawnList.ToArray(), m_spawnList.Count);
        }
    }
}