using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.IO;
using TMPro;
using UnityEngine.Networking;
using Unity.VisualScripting;
using UnityEngine.UI;
using SFB;

public class LevelManager : MonoBehaviour
{
    public float speed = 10.0f;
    public float showDistance = 100.0f;

    public AudioManager audioManager;
    public NoteManager noteManager;

    public GameObject startUI;
    public GameObject loadingUI;
    public GameObject pauseUI;
    public GameObject chooseSongUI;
    public Transform songContent;
    public GameObject songPrefab;

    public Mesh scanMesh;
    public Material scanMat;

    public SceneData[] scenes;
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

    void Start()
    {
        foreach (string filePath in Directory.GetFiles(Application.streamingAssetsPath))
        {
            string name = Path.GetFileName(filePath);
            if (name.EndsWith(".wav") || name.EndsWith(".mp3"))
            {
                if (!File.Exists(Path.Combine(Application.persistentDataPath, name)))
                {
                    File.Copy(filePath, Path.Combine(Application.persistentDataPath, name));
                }
            }
        }

        PopulateSongChoose();
        startUI.SetActive(true);
        loadingUI.SetActive(false);
        chooseSongUI.SetActive(false);
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
        chooseSongUI.SetActive(false);
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

    public void ChooseSong()
    {
        chooseSongUI.SetActive(!chooseSongUI.activeSelf);
    }

    void Play(string name)
    {
        StartCoroutine(LoadAudio(name));
        Init();
    }

    public void PlayInfinite()
    {
        audioManager.Connect();
        m_isConnecting = true;
        Init();
    }

    public void Quit()
    {
        Application.Quit();
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

    public void UploadMusic()
    {
        StartCoroutine(SelectAndSaveAudio());
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
        StartCoroutine(ScanScene());
    }

    void PopulateSongChoose()
    {
        for (int i = songContent.childCount - 1; i >= 0; i--)
        {
            Destroy(songContent.GetChild(i).gameObject);
        }

        int count = 0;
        foreach (string filePath in Directory.GetFiles(Application.persistentDataPath))
        {
            string name = Path.GetFileName(filePath);
            if (name.EndsWith(".wav") || name.EndsWith(".mp3"))
            {
                GameObject song = Instantiate(songPrefab, songContent);
                song.transform.GetChild(0).GetComponentInChildren<TextMeshProUGUI>().text = name;
                song.transform.GetChild(0).GetComponent<Button>().onClick.AddListener(() => Play(name));
                song.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(() =>
                {
                    File.Delete(filePath);
                    PopulateSongChoose();
                });
                count++;
            }
        }
        songContent.GetComponent<RectTransform>().sizeDelta = new Vector2(0, count * 80 + 40);
    }

    IEnumerator<UnityWebRequestAsyncOperation> LoadAudio(string name)
    {
        string audioFile = Path.Combine(Application.persistentDataPath, name);

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

    IEnumerator ScanScene()
    {
        float elapsed = 0f;
        while (elapsed < 1)
        {
            elapsed += Time.deltaTime;
            float size = elapsed * showDistance;
            Graphics.DrawMesh(scanMesh, noteManager.cameraTransform.localToWorldMatrix * Matrix4x4.Scale(new Vector3(size, size, size)), scanMat, 0);

            yield return null;
        }
    }

    IEnumerator SelectAndSaveAudio()
    {
        ExtensionFilter[] extensions = new[] {
            new ExtensionFilter("Sound Files", "mp3", "wav" )
        };
        string[] filePaths = StandaloneFileBrowser.OpenFilePanel(
            "Choose Music",
            "",
            extensions,
            false
        );

        if (filePaths.Length == 0 || string.IsNullOrEmpty(filePaths[0]))
        {
            yield break;
        }

        string sourcePath = filePaths[0];
        string targetPath = Path.Combine(Application.persistentDataPath, Path.GetFileName(sourcePath));

        File.Copy(sourcePath, targetPath);
        
        PopulateSongChoose();
    }
}