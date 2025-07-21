using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMover : MonoBehaviour
{
    Vector3 m_initPosition;
    Quaternion m_initRotation;
    bool m_isPlaying = false;
    float m_startTime = 0;
    float m_speed = 10.0f;

    public void Start()
    {
        m_initPosition = transform.position;
        m_initRotation = transform.rotation;
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
            Matrix4x4 targetTransform = RoadGenerator.GetTransform(currentTime * m_speed);
            transform.position = targetTransform.rotation * m_initPosition + targetTransform.GetPosition();
            transform.rotation = targetTransform.rotation * m_initRotation;
        }
    }
}
