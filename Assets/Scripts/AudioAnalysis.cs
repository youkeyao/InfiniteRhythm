using UnityEngine;
using System.Text;
using NativeWebSocket;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System;

public class AudioAnalysis : MonoBehaviour
{
    public AudioManager audioManager;
    public float swingTransitionThreshold = 0.2f;
    public float spectrumThreshold = 0.2f;
    public float spectrumScale = 1.0f;
    public Vector2 swingPI = new Vector2(1f, 1f);

    public float beatThreshold = 1e-3f;
    public float beatSpeed = 1.0f;

    float m_swingTarget;
    float m_lastSwingE;
    float m_swingValue;

    float m_beatValue;

    void Update()
    {
        // swing
        float maxSpectrum = audioManager.GetMaxSpectrum();
        if (maxSpectrum > spectrumThreshold && (Mathf.Abs(m_swingValue) > (1 - swingTransitionThreshold) || Mathf.Abs(m_swingValue) < swingTransitionThreshold))
        {
            m_swingTarget = -Mathf.Sign(m_swingTarget) * spectrumScale * maxSpectrum;
        }
        float nowSwingE = m_swingTarget - m_swingValue;
        m_swingValue += (swingPI[0] * (nowSwingE - m_lastSwingE) + swingPI[1] * nowSwingE) * Time.deltaTime;
        if (Mathf.Abs(m_swingValue - m_swingTarget) < swingTransitionThreshold)
        {
            m_swingTarget = Mathf.Sign(m_swingTarget) * Mathf.Epsilon;
        }
        m_lastSwingE = nowSwingE;
        Shader.SetGlobalFloat("_Swing", m_swingValue);

        // beat
        float[] spectrumData = audioManager.GetSpectrumData();
        if (spectrumData[6] > beatThreshold)
        {
            m_beatValue = 1;
        }
        if (m_beatValue > 0)
        {
            m_beatValue -= beatSpeed * Time.deltaTime;
        }
        else
        {
            m_beatValue = 0;
        }
        Shader.SetGlobalFloat("_Beat", m_beatValue);
    }
}