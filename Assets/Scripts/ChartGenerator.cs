using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public struct Note
{
    public float time;
    public int track;
}

public static class ChartGenerator
{
    const int HistorySize = 43;

    public static float interval = 0.2f;
    public static int windowSize = 1024;
    public static int moveLength = 1024;
    public static float sensitivity = 1.0f;
    public static float restTime = 1;
    public static float energyThreshold = 1e-2f;

    static float[] s_energyHistory = new float[HistorySize];
    static int s_historyIndex = -1;
    static int s_lastTrack = 0;
    static float s_lastEnergy = 0;
    static float s_lastTime = 0;
    static Queue<Note> s_notes = new Queue<Note>();

    public static void Clear()
    {
        s_historyIndex = -1;
        s_lastTrack = 0;
        s_lastEnergy = 0;
        s_lastTime = 0;
        s_notes.Clear();
    }

    public static Queue<Note> GetChart()
    {
        return s_notes;
    }

    public static void Generate(float[] samples, int sampleRate, float timeOffset, int numTracks)
    {
        Debug.Log(DetectBPM(samples, sampleRate, 40, 200));
        NativeArray<float> segment = new NativeArray<float>(windowSize, Allocator.TempJob);
        for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex += moveLength)
        {
            float energy = 0;
            for (int i = 0; i < windowSize; i++)
            {
                if (sampleIndex + i * 2 >= samples.Length)
                    break;
                float sample = samples[sampleIndex + i * 2];
                energy += sample * sample;
            }
            energy /= windowSize;

            float currentTime = (float)(sampleIndex + windowSize) / sampleRate / 2 + timeOffset;
            if (currentTime > restTime)
            {
                float average = 0;
                for (int j = 0; j < HistorySize; j++)
                {
                    average += s_energyHistory[j];
                }
                average /= HistorySize;

                float variance = 0;
                for (int j = 0; j < HistorySize; j++)
                {
                    variance += Mathf.Pow(s_energyHistory[j] - average, 2);
                }
                variance /= HistorySize;

                float C = 1.5142857f - variance / average / average * 0.025714f;
                if (energy > energyThreshold && energy > sensitivity * C * average && currentTime - s_lastTime > interval)
                {
                    int track = Mathf.Abs(energy - s_lastEnergy) > 10 * variance ? Random.Range(0, numTracks) : s_lastTrack;
                    s_notes.Enqueue(new Note
                    {
                        time = currentTime,
                        track = track
                    });
                    s_lastTrack = track;
                    s_lastEnergy = energy;
                    s_lastTime = currentTime;
                }
            }

            s_historyIndex = (s_historyIndex + 1) % HistorySize;
            s_energyHistory[s_historyIndex] = energy;
        }
        segment.Dispose();
    }

    static float DetectBPM(float[] samples, float sr, float bpmMin, float bpmMax)
    {
        // 能量包络
        int hop = 512;
        List<float> energyEnv = new List<float>();
        for (int i = 0; i < samples.Length - hop; i += hop)
        {
            float sum = 0f;
            for (int j = 0; j < hop; j++)
                sum += samples[i + j] * samples[i + j];
            energyEnv.Add(sum);
        }

        // 中心化
        float mean = 0f;
        foreach (var e in energyEnv) mean += e;
        mean /= energyEnv.Count;
        for (int i = 0; i < energyEnv.Count; i++)
            energyEnv[i] -= mean;

        // 自相关
        int minLag = Mathf.RoundToInt((60f / bpmMax) * sr / hop);
        int maxLag = Mathf.RoundToInt((60f / bpmMin) * sr / hop);

        float bestCorr = 0f;
        int bestLag = minLag;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            float corr = 0f;
            for (int i = 0; i < energyEnv.Count - lag; i++)
                corr += energyEnv[i] * energyEnv[i + lag];
            if (corr > bestCorr)
            {
                bestCorr = corr;
                bestLag = lag;
            }
        }

        float secondsPerBeat = (bestLag * hop) / sr;
        return 60f / secondsPerBeat;
    }
}