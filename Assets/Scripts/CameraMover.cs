using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMover : MonoBehaviour
{
    Vector3 m_initPosition;
    bool m_isPlaying = false;
    float m_startTime = 0;
    float m_speed = 10.0f;

    public void Start()
    {
        m_initPosition = transform.position;
    }

    public void Play(float speed)
    {
        m_isPlaying = true;
        m_startTime = Time.time;
        m_speed = speed;
    }

    void Update()
    {
        if (m_isPlaying)
        {
            float currentTime = Time.time - m_startTime;
            Vector3 position = m_initPosition + new Vector3(0, 0, currentTime * m_speed);
            transform.position = position;
        }
    }
}
