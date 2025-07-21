using System.Collections.Generic;
using UnityEngine;

public class NoteManager : MonoBehaviour
{
    public Vector3 noteScale = new Vector3(1, 0.3f, 1);
    public Mesh noteMesh;
    public Material noteMaterial;
    public float hitThreshold = 1.5f;
    public float distance = 50.0f;
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
    int m_numTracks = 4;
    float m_startTime = 0;
    float m_speed = 10.0f;
    bool m_isPlaying = false;
    int m_noteIndex = 0;
    float m_trackThreshold = 0.2f;

    public void Play(List<Note> chart, float speed)
    {
        m_startTime = Time.time;
        m_speed = speed;
        m_chart = chart;
        m_isPlaying = true;
        m_noteIndex = 0;
    }

    public void Update()
    {
        if (m_isPlaying)
        {
            float currentTime = Time.time - m_startTime;
            float spacing = -transform.position.x * 2 / (m_numTracks - 1);

            // Dispose
            while (m_spawnTime.Count > 0 && m_spawnTime[0] < currentTime - 1)
            {
                m_spawnTime.RemoveAt(0);
                m_spawnList.RemoveAt(0);
            }

            // Spawn
            while (m_noteIndex < m_chart.Count && currentTime >= m_chart[m_noteIndex].time - distance / m_speed)
            {
                Vector3 offset = new Vector3(m_chart[m_noteIndex].track * spacing, 0, 0);
                Matrix4x4 spawnTransform = RoadGenerator.GetTransform(m_chart[m_noteIndex].time * m_speed);
                m_spawnList.Add(spawnTransform * Matrix4x4.Translate(offset) * transform.localToWorldMatrix * Matrix4x4.Scale(noteScale));
                m_spawnTime.Add(m_chart[m_noteIndex].time);
                m_noteIndex++;
            }

            // Hit
            for (int i = 0; i < m_spawnList.Count; i++)
            {
                Vector3 position = m_spawnList[i].GetPosition();
                Matrix4x4 targetTransform = RoadGenerator.GetTransform(currentTime * m_speed);
                Vector3 targetPosition = targetTransform.GetPosition();
                Vector3 targetDiretion = targetTransform.MultiplyVector(Vector3.forward);
                Vector3 offsetDirection = targetTransform.MultiplyVector(Vector3.right);
                float targetDistance = Vector3.Dot(targetPosition, targetDiretion);
                float nowDistance = Vector3.Dot(position, targetDiretion);
                if (nowDistance > (targetDistance + hitThreshold))
                    break;
                if ((targetDistance - hitThreshold) < nowDistance)
                {
                    for (int j = 0; j < m_numTracks; j++)
                    {
                        float offset = Vector3.Dot(position - targetPosition, offsetDirection);
                        if (Input.GetKeyDown(keyCodes[j]) && Mathf.Abs(offset - (transform.position.x + j * spacing)) < m_trackThreshold)
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