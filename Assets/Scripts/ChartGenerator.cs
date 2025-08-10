using System;
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
    const int SubBandsNum = 4;
    const int HistorySize = 43;

    public static float minInterval = 0.2f;
    public static int windowSize = 2048;
    public static int moveLength = 2048;
    public static int firstBandWidth = 10;
    public static int allBandWidth = 64;
    public static float restTime = 2;
    public static float sensitivity = 3.0f;
    public static float energyThreshold = 1.5f;

    static Queue<Note> s_notes = new Queue<Note>();
    static float[] s_subBandWidths = GenerateSubBandWidth(SubBandsNum, allBandWidth);
    static float[] s_window = GenerateHanningWindow(windowSize);
    static float[,] s_energyHistory = new float[SubBandsNum, HistorySize];
    static int s_historyIndex = 0;
    static float s_lastTime = -1;
    static int s_lastBand = -1;

    public static void Clear()
    {
        s_notes.Clear();
        s_historyIndex = 0;
        s_lastTime = -1;
        s_lastBand = -1;
        Array.Clear(s_energyHistory, 0, s_energyHistory.Length);
    }

    public static Queue<Note> GetChart() => s_notes;

    public static void Generate(float[] samples, int sampleRate, float timeOffset, int numTracks)
    {
        using (BurstFFT fft = new BurstFFT(windowSize))
        {
            NativeArray<float> segment = new NativeArray<float>(windowSize, Allocator.TempJob);
            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex += moveLength)
            {
                // Get Spectrum
                for (int i = 0; i < windowSize; i++)
                {
                    if (sampleIndex + i * 2 >= samples.Length)
                        break;
                    segment[i] = (samples[sampleIndex + i * 2] + samples[sampleIndex + i * 2 + 1]) / 2 * s_window[i];
                }
                fft.Transform(segment);
                NativeArray<float> spectrum = fft.Spectrum;
                float[] subBandEnergies = GenerateSubBandEnergy(spectrum, s_subBandWidths);

                float currentTime = (float)(sampleIndex) / sampleRate / 2 + timeOffset;
                for (int i = 0; i < SubBandsNum; i++)
                {
                    if (currentTime > restTime)
                    {
                        float average = 0;
                        for (int j = 0; j < HistorySize; j++)
                        {
                            average += s_energyHistory[i, j];
                        }
                        average /= HistorySize;

                        float variance = 0;
                        for (int j = 0; j < HistorySize; j++)
                        {
                            variance += Mathf.Pow(s_energyHistory[i, j] - average, 2);
                        }
                        variance /= HistorySize;

                        float C = Mathf.Max(1.5142857f - variance / average / average * 0.0025714f, energyThreshold);
                        float V0 = 0.2f * average * average;
                        if (subBandEnergies[i] > sensitivity * C * average && variance > sensitivity * V0 && currentTime - s_lastTime > minInterval)
                        {
                            int lastTrack = s_notes.Count > 0 ? s_notes.Peek().track : -1;
                            int track = lastTrack;
                            if (s_lastBand != i)
                            {
                                while (track == lastTrack)
                                {
                                    track = UnityEngine.Random.Range(0, numTracks);
                                }
                            }
                            s_notes.Enqueue(new Note
                            {
                                time = currentTime,
                                track = track,
                            });
                            s_lastTime = currentTime;
                            break;
                        }
                    }

                    s_energyHistory[i, s_historyIndex] = subBandEnergies[i];
                }
                s_historyIndex = (s_historyIndex + 1) % HistorySize;
            }
            segment.Dispose();
        }
    }

    private static float[] GenerateHanningWindow(int size)
    {
        float[] window = new float[size];
        for (int i = 0; i < size; i++)
        {
            window[i] = 0.54f - 0.46f * Mathf.Cos(2 * Mathf.PI * i / (size - 1));
        }
        return window;
    }

    private static float[] GenerateSubBandWidth(int num, int totalWidth)
    {
        float[] subBandWidths = new float[num];

        int b = firstBandWidth;
        int a = (totalWidth - b * num) * 2 / (num * (num + 1));
        for (int i = 0; i < num; i++)
        {
            subBandWidths[i] = a * i + b;
        }
        return subBandWidths;
    }

    private static float[] GenerateSubBandEnergy(NativeArray<float> spectrum, float[] subBandWidths)
    {
        int numBands = subBandWidths.Length;
        float[] subBandEnergies = new float[numBands];
        int band = 0;
        float bandWidth = 0;

        for (int i = 0; i < spectrum.Length / 2; i++)
        {
            if (i >= bandWidth + subBandWidths[band])
            {
                bandWidth += subBandWidths[band];
                band++;
                if (band >= numBands)
                    break;
            }
            subBandEnergies[band] += spectrum[i] * spectrum[i] / subBandWidths[band];
        }
        return subBandEnergies;
    }
}