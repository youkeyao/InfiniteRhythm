using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class NoteManager : MonoBehaviour
{
    public GameObject notePrefab;

    List<Note> m_chart;
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

            while (m_noteIndex < m_chart.Count && currentTime >= m_chart[m_noteIndex].time)
            {
                SpawnNote(m_chart[m_noteIndex]);
                m_noteIndex++;
            }
        }
    }

    void SpawnNote(Note note)
    {
        Vector3 spawnPos = new Vector3(note.track * 4, 0, note.time * m_speed);
        
        GameObject noteObj = Instantiate(
            notePrefab,
            spawnPos,
            Quaternion.identity,
            transform
        );
    }
}