using UnityEngine;
using System.Text;
using NativeWebSocket;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System;

public class AudioAnalysis : MonoBehaviour
{
    public LevelManager levelManager;
    public AudioManager audioManager;

    public float swingTransitionThreshold = 0.2f;
    public float swingThreshold = 0.2f;
    public float swingScale = 1.0f;
    public Vector2 swingPI = new Vector2(1f, 1f);

    public float beatThreshold = 1e-3f;
    public float beatTransitionThreshold = 0.01f;
    public float beatScale = 1.0f;
    public Vector2 beatPI = new Vector2(1f, 1f);

    float m_swingTarget = 0;
    float m_lastSwingE = 0;
    float m_swingValue = 0;

    float m_beatTarget = 0;
    float m_lastBeatE = 0;
    float m_beatValue = 0;

    void Update()
    {
        if (levelManager.IsPlaying)
        {
            // swing
            float maxSpectrum = audioManager.GetMaxSpectrum();
            if (maxSpectrum > swingThreshold && (Mathf.Abs(m_swingValue) > (1 - swingTransitionThreshold) || Mathf.Abs(m_swingValue) < swingTransitionThreshold))
            {
                m_swingTarget = -Mathf.Sign(m_swingTarget) * swingScale * maxSpectrum;
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
                m_beatTarget = beatScale;
            }
            float nowBeatE = m_beatTarget - m_beatValue;
            m_beatValue += (beatPI[0] * (nowBeatE - m_lastBeatE) + beatPI[1] * nowBeatE) * Time.deltaTime;
            if (Mathf.Abs(m_beatValue - m_beatTarget) < beatTransitionThreshold)
            {
                m_beatTarget = 0;
            }
            m_lastBeatE = nowBeatE;
            Shader.SetGlobalFloat("_Beat", m_beatValue);
        }
    }
}