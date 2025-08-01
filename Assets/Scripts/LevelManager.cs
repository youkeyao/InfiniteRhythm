using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public float speed = 10.0f;

    public GameObject playButton;

    public KeyCode[] keyCodes = new KeyCode[]
    {
        KeyCode.A,
        KeyCode.D,
        KeyCode.J,
        KeyCode.L,
    };
    public int NumTracks => keyCodes.Length;

    [HideInInspector] public bool isPlaying = false;
    [HideInInspector] public float startTime = 0;

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