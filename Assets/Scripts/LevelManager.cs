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

    // GUI
    public GameObject startUI;
    public GameObject loadingUI;
    public GameObject pauseUI;
    public GameObject playUI;
    public GameObject resultUI;
    public GameObject chooseSongUI;
    public GameObject chooseInfiniteUI;
    public GameObject settingsUI;
    public Transform songContent;
    public Transform parameterContent;
    public Transform trackKeyContent;
    public GameObject songPrefab;
    public GameObject parameterPrefab;
    public GameObject trackKeyPrefab;
    // Settings
    public TMP_InputField speedInput;
    public Slider speedSlider;
    public TMP_InputField noteIntervalInput;
    public Slider noteIntervalSlider;
    public TMP_InputField noteThresholdInput;
    public Slider noteThresholdSlider;
    public TMP_InputField noteSensitivityInput;
    public Slider noteSensitivitySlider;

    public Mesh scanMesh;
    public Material scanMat;

    public SceneData[] scenes;
    public List<KeyCode> keyCodes = new List<KeyCode>();
    public int NumTracks => keyCodes.Count;

    public bool IsPlaying => m_isPlaying;
    public float StartTime => m_startTime;

    bool m_isPlaying = false;
    int m_recordKey = -1;
    float m_startTime = -1;
    int m_sceneIndex = -1;
    float m_lastScanTime = -100;
    float m_scanInterval = 30.0f;
    MapManager m_mapManager;

    string[][] m_parameterPrompts = {
        new string[] { "rhythm", "synthwave" },
        new string[] { "chillwave", "Bossa Nova" },
    };

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

        AddTrack(KeyCode.A);
        AddTrack(KeyCode.D);
        AddTrack(KeyCode.J);
        AddTrack(KeyCode.L);

        PopulateSongChoose();
        ChangeStyle();
        speedSlider.value = speed;
        SetSpeed(false);
        noteIntervalSlider.value = ChartGenerator.minInterval;
        SetNoteInterval(false);
        noteThresholdSlider.value = ChartGenerator.energyThreshold;
        SetNoteThreshold(false);
        noteSensitivitySlider.value = ChartGenerator.sensitivity;
        SetNoteSensitivity(false);

        startUI.SetActive(true);
        pauseUI.SetActive(false);
        loadingUI.SetActive(false);
        playUI.SetActive(false);
        resultUI.SetActive(false);
        chooseSongUI.SetActive(false);
        chooseInfiniteUI.SetActive(false);
        settingsUI.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Pause();
        }

        if (m_recordKey >= 0)
        {
            if (Input.anyKeyDown)
            {
                foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(key))
                    {
                        if (!keyCodes.Contains(key))
                        {
                            keyCodes[m_recordKey] = key;
                            trackKeyContent.GetChild(m_recordKey).GetComponentInChildren<TMP_Text>().text = key.ToString();
                        }
                    }
                }
                m_recordKey = -1;
            }
        }

        playUI.transform.GetChild(0).GetComponent<TMP_Text>().text = noteManager.Combo.ToString();
    }

    void Init()
    {
        m_lastScanTime = -100;
        startUI.SetActive(false);
        pauseUI.SetActive(false);
        loadingUI.SetActive(true);
        playUI.SetActive(true);
        chooseSongUI.SetActive(false);
        chooseInfiniteUI.SetActive(false);
        settingsUI.SetActive(false);
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

    public void Pause()
    {
        if (!m_isPlaying)
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

    void Play(string name)
    {
        StartCoroutine(LoadAudio(name));
        Init();
    }

    public void PlayInfinite()
    {
        audioManager.ClearWeightedPrompts();
        foreach (Transform child in parameterContent)
        {
            string prompt = child.GetChild(0).GetComponentInChildren<TMP_InputField>().text;
            if (!string.IsNullOrEmpty(prompt))
            {
                audioManager.SetWeightedPrompts(prompt, 0.5f);
            }
        }
        audioManager.Connect();
        StartCoroutine(WaitForAudioManager());
        Init();
    }

    public void Stop()
    {
        m_isPlaying = false;
        startUI.SetActive(false);
        pauseUI.SetActive(false);
        playUI.SetActive(false);
        loadingUI.SetActive(false);
        resultUI.SetActive(true);

        resultUI.transform.GetChild(0).GetChild(1).GetComponent<TMP_Text>().text = noteManager.Hit.ToString();
        resultUI.transform.GetChild(1).GetChild(1).GetComponent<TMP_Text>().text = noteManager.Miss.ToString();
        resultUI.transform.GetChild(2).GetChild(1).GetComponent<TMP_Text>().text = (noteManager.Hit + noteManager.Miss).ToString();

        CurveGenerator.Clear();
        ChartGenerator.Clear();
        Destroy(m_mapManager);
        m_mapManager = null;
        noteManager.Clear();
        audioManager.Clear();
    }

    public void Restart()
    {
        resultUI.SetActive(false);
        startUI.SetActive(true);
    }

    // ---------------------UI-----------------------------------

    public void NewParameter(string prompt = "")
    {
        GameObject newParam = Instantiate(parameterPrefab, parameterContent);
        newParam.transform.GetChild(0).GetComponentInChildren<TMP_InputField>().text = prompt;
        newParam.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(() =>
        {
            Destroy(newParam);
        });
    }

    public void UploadMusic()
    {
        StartCoroutine(SelectAndSaveAudio());
    }

    public void OnChooseSong()
    {
        chooseInfiniteUI.SetActive(false);
        chooseSongUI.SetActive(!chooseSongUI.activeSelf);
    }

    public void OnChooseInfinite()
    {
        chooseSongUI.SetActive(false);
        chooseInfiniteUI.SetActive(!chooseInfiniteUI.activeSelf);
    }

    public void OnSettings()
    {
        settingsUI.SetActive(!settingsUI.activeSelf);
    }

    public void OnQuit()
    {
        Application.Quit();
    }

    // -------------------Settings--------------------------------

    public void SetSpeed(bool isInput)
    {
        if (isInput)
        {
            speed = float.Parse(speedInput.text);
            speedSlider.value = speed;
        }
        else
        {
            speed = speedSlider.value;
            speedInput.text = speed.ToString("F1");
        }
    }

    public void SetNoteInterval(bool isInput)
    {
        if (isInput)
        {
            ChartGenerator.minInterval = float.Parse(noteIntervalInput.text);
            noteIntervalSlider.value = ChartGenerator.minInterval;
        }
        else
        {
            ChartGenerator.minInterval = noteIntervalSlider.value;
            noteIntervalInput.text = ChartGenerator.minInterval.ToString("F2");
        }
    }

    public void SetNoteThreshold(bool isInput)
    {
        if (isInput)
        {
            ChartGenerator.energyThreshold = float.Parse(noteThresholdInput.text);
            noteThresholdSlider.value = ChartGenerator.energyThreshold;
        }
        else
        {
            ChartGenerator.energyThreshold = noteThresholdSlider.value;
            noteThresholdInput.text = ChartGenerator.energyThreshold.ToString("F2");
        }
    }

    public void SetNoteSensitivity(bool isInput)
    {
        if (isInput)
        {
            ChartGenerator.sensitivity = float.Parse(noteSensitivityInput.text);
            noteSensitivitySlider.value = ChartGenerator.sensitivity;
        }
        else
        {
            ChartGenerator.sensitivity = noteSensitivitySlider.value;
            noteSensitivityInput.text = ChartGenerator.sensitivity.ToString("F2");
        }
    }

    public void AddTrack()
    {
        AddTrack(KeyCode.None);
    }

    void AddTrack(KeyCode key)
    {
        int trackIndex = keyCodes.Count;
        keyCodes.Add(key);
        GameObject newTrack = Instantiate(trackKeyPrefab, trackKeyContent);
        newTrack.transform.GetComponentInChildren<TMP_Text>().text = key.ToString();
        newTrack.transform.GetComponent<Button>().onClick.AddListener(() =>
        {
            m_recordKey = trackIndex;
        });
    }

    public void RemoveTrack()
    {
        if (keyCodes.Count > 0)
        {
            keyCodes.RemoveAt(keyCodes.Count - 1);
            Destroy(trackKeyContent.GetChild(trackKeyContent.childCount - 1).gameObject);
        }
    }

    // -----------------------------------------------------------

    public void ChangeScene()
    {
        if (m_lastScanTime + m_scanInterval > Time.time)
        {
            return;
        }
        m_lastScanTime = Time.time;
        int newSceneIndex = Random.Range(0, scenes.Length);
        while (scenes.Length > 1 && newSceneIndex == m_sceneIndex)
        {
            newSceneIndex = Random.Range(0, scenes.Length);
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

    public void ChangeStyle()
    {
        for (int i = parameterContent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(parameterContent.GetChild(i).gameObject);
        }
        foreach (string prompt in m_parameterPrompts[Random.Range(0, m_parameterPrompts.Length)])
        {
            NewParameter(prompt);
        }
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
    }

    IEnumerator WaitForAudioManager()
    {
        yield return new WaitUntil(() => audioManager.IsReady);
        Ready();
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

            audioManager.SetAudioClip(audioClip);
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

        if (!File.Exists(targetPath))
        {
            File.Copy(sourcePath, targetPath);
            PopulateSongChoose();
        }
    }
}