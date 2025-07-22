using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using NativeWebSocket;
using System.Collections.Generic;
using System.Threading.Tasks;

class AudioManager : MonoBehaviour
{
    private AudioSource m_audioSource;
    private List<AudioClip> m_audioClips = new List<AudioClip>();
    private string m_wsurl = "wss://generativelanguage.googleapis.com//ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateMusic?key=AIzaSyBE_WpYLV2beN9E52AUsGTjzjs82_DVT_I";
    private WebSocket m_webSocket;

    async void Start()
    {
        m_audioSource = gameObject.AddComponent<AudioSource>();
        // m_audioSource.clip = audioClip;
        // m_audioSource.loop = true;
        // m_audioSource.Play();

        // StartCoroutine(SendGeminiMusicRequest("prompt"));

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
            Setup();
            SetWeightedPrompts("minimal techno", 1.0f);
            SetMusicGenerationConfig(1.0f, 90);
            Play();
        };
        m_webSocket.OnError += (e) => Debug.Log("WebSocket Error: " + e);
        m_webSocket.OnClose += (e) => Debug.Log("WebSocket Closed: " + e);
        m_webSocket.OnMessage += (bytes) =>
        {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log("WebSocket Message: " + message);
        };
        await m_webSocket.Connect();
    }

    async void Setup()
    {
        await m_webSocket.SendText("{\"setup\": {\"model\": \"models/lyria-realtime-exp\"}}");
    }

    async void SetWeightedPrompts(string prompt, float weight)
    {
        string message = $"{{\"clientContent\": {{\"weightedPrompts\": [{{\"text\": \"{prompt}\", \"weight\": {weight}}}]}}}}";
        await m_webSocket.SendText(message);
    }

    async void SetMusicGenerationConfig(float temperature, int bpm)
    {
        string message = $"{{\"musicGenerationConfig\": {{\"temperature\": {temperature}, \"bpm\": {bpm}}}}}";
        await m_webSocket.SendText(message);
    }

    async void OnApplicationQuit()
    {
        await m_webSocket.Close();
    }

    void Update()
    {
        if (m_webSocket.State == WebSocketState.Connecting)
        {
            Debug.Log("WebSocket is connecting...");
        }
        else if (m_webSocket.State == WebSocketState.Open)
        {
            m_webSocket.DispatchMessageQueue();
        }
    }

    public async void Play()
    {
        await m_webSocket.SendText("{\"playbackControl\": \"PLAY\"}");
    }

    public async void Stop()
    {
        await m_webSocket.SendText("{\"playbackControl\": \"STOP\"}");
    }

    public async void Pause()
    {
        await m_webSocket.SendText("{\"playbackControl\": \"PAUSE\"}");
    }

    void GetAudio()
    {

    }

    // IEnumerator SendGeminiMusicRequest(string prompt)
    // {
    //     string url = "https://generativelanguage.googleapis.com/v1alpha/models/lyria-realtime-exp:generateContent";

    //     string json = JsonUtility.ToJson(new GeminiRequest
    //     {
    //         contents = new Content[]
    //         {
    //             new Content { role = "user", parts = new Part[] { new Part { text = prompt } } }
    //         }
    //     });

    //     byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

    //     UnityWebRequest request = new UnityWebRequest(url, "POST");
    //     request.uploadHandler = new UploadHandlerRaw(bodyRaw);
    //     request.downloadHandler = new DownloadHandlerBuffer();
    //     request.SetRequestHeader("x-goog-api-key", m_apiKey);
    //     request.SetRequestHeader("Content-Type", "application/json");

    //     Debug.Log("Sending request to Gemini API: " + json);
    //     yield return request.SendWebRequest();

    //     if (request.result == UnityWebRequest.Result.Success)
    //     {
    //         Debug.Log("Music generated: " + request.downloadHandler.text);

    //         // Optional: Parse and decode music here
    //         // TODO: Parse base64 audio and play it
    //     }
    //     else
    //     {
    //         Debug.LogError("Error: " + request.error + "\n" + request.downloadHandler.text);
    //     }
    // }
}

[System.Serializable]
public class GeminiRequest
{
    public Content[] contents;
}

[System.Serializable]
public class Content
{
    public string role;
    public Part[] parts;
}

[System.Serializable]
public class Part
{
    public string text;
}