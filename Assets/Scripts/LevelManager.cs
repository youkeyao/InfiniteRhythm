using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.Networking;
using Unity.VisualScripting;

public class LevelManager : MonoBehaviour
{
    public float speed = 10.0f;

    public GameObject playButton;

    public bool isPlaying  = false;
    public float startTime = 0;

    void Start()
    {
    }

    public void Play()
    {
        isPlaying = true;
        startTime = Time.time;
        playButton.SetActive(false);
    }
}