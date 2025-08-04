using UnityEngine;
using System.Text;
using NativeWebSocket;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;
using System;

struct AudioData
{
    public float[] samples;
    public float time;
}

public class AudioManager : MonoBehaviour
{
    const int NumSpectrumSample = 1024;

    public LevelManager levelManager;
    public int bufferSize = 10;
    public int sampleRate = 48000;
    public int numChannels = 2;

    Dictionary<string, float> m_weightedPrompts = new Dictionary<string, float>();
    float m_temperature = 1.0f;
    int m_bpm = 90;

    AudioSource m_audioSource;
    Queue<AudioClip> m_audioClips = new Queue<AudioClip>();
    List<AudioData> m_audioDatas = new List<AudioData>();
    Queue<Note> m_chart = new Queue<Note>();
    string m_wsurl = "wss://generativelanguage.googleapis.com//ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateMusic?key=AIzaSyBE_WpYLV2beN9E52AUsGTjzjs82_DVT_I";
    WebSocket m_webSocket;
    float m_audioLength;
    bool m_isSetup = false;
    bool m_isGenerating = false;
    float m_maxSpectrum = 0;
    float[] m_spectrum = new float[NumSpectrumSample];

    async void Start()
    {
        m_audioSource = this.gameObject.AddComponent<AudioSource>();

        Dictionary<string, string> headers = new Dictionary<string, string>
        {
            { "Content-Type", "application/json" },
            { "x-goog-api-key", "AIzaSyBE_WpYLV2beN9E52AUsGTjzjs82_DVT_I" },
            { "user-agent", "google-genai-sdk/1.26.0 gl-python/3.12.0" },
            { "x-goog-api-client", "google-genai-sdk/1.26.0 gl-python/3.12.0" }
        };
        m_webSocket = new WebSocket(m_wsurl, headers);
        m_webSocket.OnOpen += () =>
        {
            Debug.Log("WebSocket Opened!");
        };
        m_webSocket.OnError += (e) => Debug.Log("WebSocket Error: " + e);
        m_webSocket.OnClose += (e) =>
        {
            Debug.Log("WebSocket Closed: " + e);
            levelManager.isPlaying = false;
        };
        m_webSocket.OnMessage += (bytes) =>
        {
            string jsonString = Encoding.UTF8.GetString(bytes);
            JObject jObject = JObject.Parse(jsonString);
            if (jObject["serverContent"] != null)
            {
                JToken dataToken = jObject["serverContent"]["audioChunks"][0]["data"];
                ReceiveAudio(Convert.FromBase64String(dataToken.ToString()));
            }
        };
        await m_webSocket.Connect();
    }

    public async void Setup()
    {
        await m_webSocket.SendText("{\"setup\": {\"model\": \"models/lyria-realtime-exp\"}}");
        // SetWeightedPrompts("synthwave", 1.0f);
        SetWeightedPrompts("chillwave", 1.0f);
        SetWeightedPrompts("Bossa Nova", 1.0f);
        // SetWeightedPrompts("Drum and Bass", 0.8f);
        // SetWeightedPrompts("Post Punk", 1.0f);
        // SetWeightedPrompts("Shoegaze", 0.5f);
        // SetWeightedPrompts("Funk", 1.0f);
        // SetWeightedPrompts("Chiptune", 1.0f);
        // SetWeightedPrompts("Lush Strings", 1.0f);
        SetMusicGenerationConfig(m_temperature, m_bpm);
        m_isSetup = true;
    }

    private async void SendWeightedPrompts()
    {
        string message = $"{{\"clientContent\": {{\"weightedPrompts\": [";
        foreach (string prompt in m_weightedPrompts.Keys)
        {
            message += $"{{\"text\": \"{prompt}\", \"weight\": {m_weightedPrompts[prompt]}}},";
        }
        message = message.Substring(0, message.Length - 1);
        message += "]}}";
        await m_webSocket.SendText(message);
    }

    public Queue<Note> GetChart()
    {
        return m_chart;
    }

    public float[] GetSpectrumData()
    {
        return m_spectrum;
    }

    public float GetMaxSpectrum()
    {
        return m_maxSpectrum;
    }

    public void SetWeightedPrompts(string prompt, float weight)
    {
        m_weightedPrompts[prompt] = weight;
        SendWeightedPrompts();
    }

    public void RemoveWeightedPrompts(string prompt)
    {
        m_weightedPrompts.Remove(prompt);
        SendWeightedPrompts();
    }

    public async void SetMusicGenerationConfig(float temperature, int bpm)
    {
        string message = $"{{\"musicGenerationConfig\": {{\"temperature\": {temperature}, \"bpm\": {bpm}}}}}";
        await m_webSocket.SendText(message);
    }

    async void OnDestroy()
    {
        await m_webSocket.Close();
    }

    // pcm
    void ReceiveAudio(byte[] bytes)
    {
        int bytesPerSample = 2;
        int sampleCount = bytes.Length / (numChannels * bytesPerSample);
        AudioClip audioClip = AudioClip.Create("GeneratedAudioClip", sampleCount, numChannels, sampleRate, false);
        float[] samples = new float[sampleCount * numChannels];
        for (int i = 0; i < sampleCount; i++)
        {
            for (int channel = 0; channel < numChannels; channel++)
            {
                int index = (i * numChannels + channel) * bytesPerSample;

                short sample = System.BitConverter.ToInt16(bytes, index);
                samples[i * numChannels + channel] = sample / 32768f; // normalize
            }
        }
        audioClip.SetData(samples, 0);
        // RoadGenerator.GenerateNextControlPoint(samples, samples.Length / (numChannels * sampleRate) * levelManager.speed);

        // generate chart
        List<Note> chart = ChartGenerator.GetChart(samples, sampleRate, m_audioLength, levelManager.NumTracks);
        foreach (Note note in chart)
        {
            m_chart.Enqueue(note);
        }
        m_audioLength += audioClip.length;
        m_audioClips.Enqueue(audioClip);
        m_audioDatas.Add(new AudioData { samples = samples, time = m_audioLength });

        // generate curve
        while (CurveGenerator.GetLength(0) < m_audioLength * levelManager.speed)
        {
            CurveGenerator.GenerateNextControlPoint(samples);
        }
    }

    void Update()
    {
        if (m_webSocket.State == WebSocketState.Open && m_isSetup)
        {
            m_webSocket.DispatchMessageQueue();

            // control generation speed
            if (m_audioClips.Count > bufferSize)
            {
                if (!levelManager.isPlaying)
                {
                    levelManager.isPlaying = true;
                    levelManager.startTime = Time.time;
                }
                Pause();
            }
            else if (m_audioClips.Count < bufferSize)
            {
                Play();
            }
        }

        if (levelManager.isPlaying && !m_audioSource.isPlaying)
        {
            // unpause
            if (!m_audioSource.isPlaying)
                m_audioSource.UnPause();
            // finished, play next clip
            if ((m_audioSource.clip == null || !m_audioSource.isPlaying) && m_audioClips.Count > 0)
            {
                m_audioSource.clip = m_audioClips.Dequeue();
                m_audioSource.Play();
            }
            else
            {
                levelManager.isPlaying = false;
            }
        }
        else if (!levelManager.isPlaying && m_audioSource.isPlaying)
        {
            // pause
            m_audioSource.Pause();
        }

        // update spectrum
        m_audioSource.GetSpectrumData(m_spectrum, 0, FFTWindow.BlackmanHarris);
        Shader.SetGlobalFloatArray("_Spectrum", m_spectrum);
        m_maxSpectrum = m_spectrum.Max();
        Shader.SetGlobalFloat("_MaxSpectrum", m_maxSpectrum);
    }

    async void Play()
    {
        if (!m_isGenerating && m_webSocket.State == WebSocketState.Open)
        {
            m_isGenerating = true;
            await m_webSocket.SendText("{\"playbackControl\": \"PLAY\"}");
        }
    }

    async void Stop()
    {
        if (m_isGenerating && m_webSocket.State == WebSocketState.Open)
        {
            m_isGenerating = false;
            await m_webSocket.SendText("{\"playbackControl\": \"STOP\"}");
        }
    }

    async void Pause()
    {
        if (m_isGenerating && m_webSocket.State == WebSocketState.Open)
        {
            m_isGenerating = false;
            await m_webSocket.SendText("{\"playbackControl\": \"PAUSE\"}");
        }
    }
    
    public float GetSample(float time)
    {
        if (time < m_audioLength)
        {
            float lastTime = 0;
            for (int i = 0; i < m_audioDatas.Count; i++)
            {
                if (time < m_audioDatas[i].time)
                {
                    return m_audioDatas[i].samples[(int)((time - lastTime) / (m_audioDatas[i].time - lastTime) * m_audioDatas[i].samples.Length)];
                }
                lastTime = m_audioDatas[i].time;
            }
        }
        return 0;
    }
}