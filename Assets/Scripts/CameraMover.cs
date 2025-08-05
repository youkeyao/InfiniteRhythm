using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMover : MonoBehaviour
{
    public LevelManager levelManager;

    Vector3 m_initPosition;
    Quaternion m_initRotation;

    public void Start()
    {
        m_initPosition = transform.position;
        m_initRotation = transform.rotation;
    }

    void Update()
    {
        if (levelManager.IsPlaying)
        {
            float currentTime = Time.time - levelManager.StartTime;
            Matrix4x4 targetTransform = CurveGenerator.GetTransform(currentTime * levelManager.speed, 0) * CurveGenerator.GetRotation(currentTime * levelManager.speed);
            transform.position = targetTransform.rotation * m_initPosition + targetTransform.GetPosition();
            transform.rotation = targetTransform.rotation * m_initRotation;
        }
    }
}
