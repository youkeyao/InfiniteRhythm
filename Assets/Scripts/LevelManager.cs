using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using Unity.VisualScripting;

public class LevelManager : MonoBehaviour
{
    public float speed = 10.0f;
    public float showDistance = 100.0f;

    public AudioManager audioManager;
    public NoteManager noteManager;
    public GameObject startUI;
    public GameObject loadingUI;
    public GameObject pauseUI;

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
    float m_startTime = -1;
    int m_sceneIndex = 0;
    MapManager m_mapManager;

    bool isTmp = false;

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
                Ready();
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (Time.timeScale == 0)
            {
                Time.timeScale = 1;
                m_isPlaying = true;
                pauseUI.SetActive(false);
            }
            else
            {
                Time.timeScale = 0;
                m_isPlaying = false;
                pauseUI.SetActive(true);
            }
        }
    }

    void Init()
    {
        startUI.SetActive(false);
        pauseUI.SetActive(false);
        loadingUI.SetActive(true);
        noteManager.Init();
        ChangeScene();
    }

    void Ready()
    {
        Time.timeScale = 1;
        m_isPlaying = true;
        m_startTime = Time.time;
        loadingUI.SetActive(false);
    }

    public void Play()
    {
        StartCoroutine(LoadAudio());
        Init();
    }

    public void PlayInfinite()
    {
        audioManager.Connect();
        m_isConnecting = true;
        Init();
    }

    public void Stop()
    {
        m_isPlaying = false;
        startUI.SetActive(true);
        pauseUI.SetActive(false);
        CurveGenerator.Clear();
        ChartGenerator.Clear();
        Destroy(m_mapManager);
        m_mapManager = null;
        noteManager.Clear();
        audioManager.Clear();
    }

    public void ChangeScene()
    {
        int newSceneIndex = UnityEngine.Random.Range(0, scenes.Length);
        while (scenes.Length > 1 && newSceneIndex == m_sceneIndex)
        {
            newSceneIndex = UnityEngine.Random.Range(0, scenes.Length);
        }

        m_sceneIndex = newSceneIndex;
        if (m_mapManager != null)
        {
            m_mapManager.EraseMap();
            Destroy(m_mapManager, 1);
        }
        m_mapManager = this.AddComponent<MapManager>();
        m_mapManager.SetSceneData(scenes[m_sceneIndex]);
        m_mapManager.levelManager = this;
        m_mapManager.cameraTransform = noteManager.cameraTransform;
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

        Ready();
    }
}