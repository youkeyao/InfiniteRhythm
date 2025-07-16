using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.Networking;
using Unity.VisualScripting;

public class LevelManager : MonoBehaviour
{
    public List<Note> currentChart;
    public float speed = 10.0f;

    public GameObject prefab;
    public NoteManager noteManager;
    public CameraMover cameraMover;

    AudioSource m_audioSource;
    List<string> m_levels = new List<string>();
    int m_currentLevel = 0;

    GameObject[] gameObjects;

    void Start()
    {
        m_audioSource = this.AddComponent<AudioSource>();
        string[] dirs = Directory.GetDirectories(Application.persistentDataPath);
        foreach (string dir in dirs)
        {
            m_levels.Add(dir);
        }

        gameObjects = new GameObject[ChartGenerator.windowSize];
        for (int i = 0; i < gameObjects.Length; i++)
        {
            gameObjects[i] = Instantiate(prefab, new Vector3(i, 0, 0), Quaternion.identity, transform);
            gameObjects[i].name = "Note " + i;
        }
    }

    // public IEnumerator AddLevel()
    // {
    //     // string levelName = "Level " + (levels.Count + 1);
    //     // string levelPath = Application.persistentDataPath + "/" + levelName;
    //     // Directory.CreateDirectory(levelPath);
    //     // levels.Add(levelPath);

    //     string musicPath = Path.Combine(Application.persistentDataPath, levels[currentLevel], "music.wav");
    //     UnityWebRequest musicRequest = UnityWebRequestMultimedia.GetAudioClip(musicPath, AudioType.WAV);
    //     yield return musicRequest.SendWebRequest();

    //     if (musicRequest.result == UnityWebRequest.Result.Success)
    //     {
    //         AudioClip currentBGM = DownloadHandlerAudioClip.GetContent(musicRequest);
    //         List<Note> notes = NoteDetect.GetNotes(currentBGM);
    //     }
    // }

    void Update()
    {
        // if (m_audioSource.isPlaying)
        // {
        //     float[] samples = new float[ChartGenerator.windowSize];
        //     m_audioSource.GetSpectrumData(samples, 0, FFTWindow.BlackmanHarris);

        //     for (int i = 0; i < samples.Length; i++)
        //     {
        //         float note = samples[i];
        //         gameObjects[i].transform.localScale = new Vector3(1, note * 200, 1);
        //     }
        // }
    }

    public void Play()
    {
        StartCoroutine(LoadLevel());
    }

    private IEnumerator<UnityWebRequestAsyncOperation> LoadLevel()
    {
        string levelPath = m_levels[m_currentLevel];
        string musicPath = Path.Combine(levelPath, "music.wav");
        UnityWebRequest musicRequest = UnityWebRequestMultimedia.GetAudioClip(musicPath, AudioType.WAV);
        yield return musicRequest.SendWebRequest();

        if (musicRequest.result == UnityWebRequest.Result.Success)
        {
            AudioClip audioClip = DownloadHandlerAudioClip.GetContent(musicRequest);
            currentChart = ChartGenerator.GetChart(audioClip);

            noteManager.Play(currentChart, speed);
            cameraMover.Play(speed);
            m_audioSource.clip = audioClip;
            m_audioSource.Play();
        }
    }
}