using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public float speed = 10.0f;
    public float showDistance = 100.0f;

    public AudioManager audioManager;
    public GameObject startUI;

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
    }

    void Update()
    {
        if (m_isConnecting)
        {
            if (audioManager.IsReady)
            {
                m_isConnecting = false;
                Play();
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
        m_isPlaying = true;
        m_startTime = Time.time;
        startUI.SetActive(false);
    }

    public void PlayInfinite()
    {
        audioManager.Connect();
        m_isConnecting = true;
    }
}