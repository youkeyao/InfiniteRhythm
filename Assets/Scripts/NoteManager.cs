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
    List<Matrix4x4> m_spawnList = new List<Matrix4x4>();
    int m_numTracks = 4;
    float m_startTime = 0;
    float m_speed = 10.0f;
    bool m_isPlaying = false;
    int m_noteIndex = 0;

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

            while (m_spawnList.Count > 0 && m_spawnList[0].GetPosition().z < (currentTime - 1) * m_speed)
            {
                m_spawnList.RemoveAt(0);
            }
            while (m_noteIndex < m_chart.Count && currentTime >= m_chart[m_noteIndex].time - distance / m_speed)
            {
                Vector3 spawnPos = new Vector3(m_chart[m_noteIndex].track * spacing, 0, m_chart[m_noteIndex].time * m_speed) + transform.position;
                m_spawnList.Add(Matrix4x4.Translate(spawnPos) * Matrix4x4.Scale(noteScale));
                m_noteIndex++;
            }
            for (int i = 0; i < m_spawnList.Count; i++)
            {
                Vector3 position = m_spawnList[i].GetPosition();
                if (position.z > (currentTime * m_speed + hitThreshold))
                    break;
                if ((currentTime * m_speed - hitThreshold) < position.z)
                {
                    for (int j = 0; j < m_numTracks; j++)
                    {
                        if (Input.GetKeyDown(keyCodes[j]) && Mathf.Approximately(position.x, transform.position.x + spacing * j))
                        {
                            m_spawnList.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
            Graphics.DrawMeshInstanced(noteMesh, 0, noteMaterial, m_spawnList.ToArray(), m_spawnList.Count);
        }
    }
}