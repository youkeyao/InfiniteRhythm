using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.Networking;
using Unity.VisualScripting;

public class LevelManager : MonoBehaviour
{
    public List<Note> currentChart;
    public float speed = 10.0f;

    public NoteManager noteManager;
    public CameraMover cameraMover;

    AudioSource audioSource;
    List<string> m_levels = new List<string>();
    int m_currentLevel = 0;

    void Start()
    {
        audioSource = this.AddComponent<AudioSource>();
        string[] dirs = Directory.GetDirectories(Application.persistentDataPath);
        foreach (string dir in dirs)
        {
            m_levels.Add(dir);
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
            audioSource.clip = audioClip;
            audioSource.Play();
        }
    }
}