using UnityEngine;
using System.Text;
using NativeWebSocket;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System;

public class Swing : MonoBehaviour
{
    public AudioManager audioManager;
    public float transitionThreshold = 0.2f;
    public float spectrumThreshold = 0.2f;
    public float spectrumScale = 1.0f;
    public Vector2 swingPI = new Vector2(1f, 1f);

    float m_swingTarget;
    float m_lastE;
    float m_swingValue;

    void Update()
    {
        float spectrum = audioManager.GetSpectrumData()[0];
        if (spectrum > spectrumThreshold)
        {
            m_swingTarget = -Mathf.Sign(m_swingTarget) * spectrumScale * spectrum;
        }
        float nowE = m_swingTarget - m_swingValue;
        m_swingValue += (swingPI[0] * (nowE - m_lastE) + swingPI[1] * nowE) * Time.deltaTime;
        if (Mathf.Abs(m_swingValue - m_swingTarget) < transitionThreshold)
        {
            m_swingTarget = Mathf.Sign(m_swingTarget) * Mathf.Epsilon;
        }
        m_lastE = nowE;
        Shader.SetGlobalFloat("_Swing", m_swingValue);
    }
}