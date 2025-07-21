using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using NativeWebSocket;
using System.Collections.Generic;

class AudioGenerator : MonoBehaviour
{
    public AudioClip audioClip;

    private AudioSource m_audioSource;
    private string m_wsurl = "wss://generativelanguage.googleapis.com//ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateMusic?key=AIzaSyBE_WpYLV2beN9E52AUsGTjzjs82_DVT_I";
    private WebSocket m_webSocket;

    async void Start()
    {
        // m_audioSource = gameObject.AddComponent<AudioSource>();
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
        Debug.Log(headers.ToString());
        m_webSocket = new WebSocket(m_wsurl, headers);
        m_webSocket.OnOpen += () => Debug.Log("WebSocket Opened!");
        m_webSocket.OnError += (e) => Debug.Log("WebSocket Error: " + e);
        m_webSocket.OnClose += (e) => Debug.Log("WebSocket Closed: " + e);
        m_webSocket.OnMessage += (bytes) =>
        {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log("WebSocket Message: " + message);
        };
        await m_webSocket.Connect();
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
    }

    public void PlayAudio()
    {
        if (!m_audioSource.isPlaying)
        {
            m_audioSource.Play();
        }
    }

    public void StopAudio()
    {
        if (m_audioSource.isPlaying)
        {
            m_audioSource.Stop();
        }
    }

    public void PauseAudio()
    {
        if (m_audioSource.isPlaying)
        {
            m_audioSource.Pause();
        }
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