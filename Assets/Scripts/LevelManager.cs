using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;

public class LevelManager : MonoBehaviour
{
    public float speed = 10.0f;
    public float showDistance = 100.0f;

    public AudioManager audioManager;
    public MapManager mapManager;
    public NoteManager noteManager;
    public GameObject startUI;
    public GameObject loadingUI;

    public SceneData[] scenes;
    public TMP_Dropdown levelDropDown;

    public KeyCode[] keyCodes = new KeyCode[]
    {
        KeyCode.A,
        KeyCode.D,
        KeyCode.J,
        KeyCode.L,
    };
    public int NumTracks => keyCodes.Length;

    public bool IsPlaying => m_isPlaying;
    public float StartTime => m_startTime;

    bool m_isPlaying = false;
    bool m_isConnecting = false;
    float m_startTime = 0;

    void Start()
    {
        PopulateDropdown();
        loadingUI.SetActive(false);
    }

    void Update()
    {
        if (m_isConnecting)
        {
            if (audioManager.IsReady)
            {
                m_isConnecting = false;
                m_isPlaying = true;
                m_startTime = Time.time;
                loadingUI.SetActive(false);
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (Time.timeScale == 0)
            {
                Time.timeScale = 1;
                m_isPlaying = true;
            }
            else
            {
                Time.timeScale = 0;
                m_isPlaying = false;
            }
        }
    }

    public void Play()
    {
        startUI.SetActive(false);
        loadingUI.SetActive(true);
        StartCoroutine(LoadAudio());
        noteManager.Init();
    }

    public void PlayInfinite()
    {
        audioManager.Connect();
        m_isConnecting = true;
        startUI.SetActive(false);
        loadingUI.SetActive(true);
        noteManager.Init();
    }

    public void Stop()
    {
        m_isPlaying = false;
        startUI.SetActive(true);
        CurveGenerator.Clear();
        mapManager.CLear();
        noteManager.Clear();
    }

    void PopulateDropdown()
    {
        levelDropDown.ClearOptions();

        string streamingAssetsPath = Application.streamingAssetsPath;
        string[] subDirectories = Directory.GetDirectories(streamingAssetsPath);

        List<string> directoryNames = new List<string>();
        foreach (string dirPath in subDirectories)
        {
            string dirName = Path.GetFileName(dirPath);
            directoryNames.Add(dirName);
        }

        levelDropDown.AddOptions(directoryNames);

        levelDropDown.value = 0;
        levelDropDown.RefreshShownValue();
    }

    IEnumerator<UnityWebRequestAsyncOperation> LoadAudio()
    {
        string dirPath = Path.Combine(Application.streamingAssetsPath, levelDropDown.options[levelDropDown.value].text);
        string audioFile = Directory.GetFiles(dirPath)[0];
        int sceneIndex = int.Parse(Path.GetFileNameWithoutExtension(audioFile));
        mapManager.sceneData = scenes[sceneIndex];

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(audioFile, AudioType.UNKNOWN))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(www.error);
                yield break;
            }

            AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);

            if (audioClip == null)
            {
                Debug.LogError("Invalid audio!");
                yield break;
            }

            audioManager.AddAudioClip(audioClip);
        }

        m_isPlaying = true;
        m_startTime = Time.time;
        loadingUI.SetActive(false);
    }
}