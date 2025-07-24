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
    public static float step = 0.2f;
    public static int windowSize = 1024;
    public static int moveLength = 2048;
    public static int historySize = 43;
    public static float sensitivity = 0.9f;

    static List<Note> s_notes = new List<Note>();

    public static List<Note> GetChart(float[] samples, int sampleRate, float timeOffset, int numTracks)
    {
        s_notes.Clear();

        float[] energyHistory = new float[historySize];
        int historyIndex = 0;
        float lastTime = 0;
        float lastEnergy = -1;
        int lastTrack = 0;

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
            if (sampleIndex / moveLength >= historySize)
            {
                float average = 0;
                for (int j = 0; j < historySize; j++)
                {
                    average += energyHistory[j];
                }
                average /= historySize;

                float variance = 0;
                for (int j = 0; j < historySize; j++)
                {
                    variance += Mathf.Pow(energyHistory[j] - average, 2);
                }
                variance /= historySize;

                float C = 1.5142857f - variance / average / average * 0.025714f;
                float currentTime = (float)sampleIndex / sampleRate / 2;
                if (energy > sensitivity * C * average && currentTime - lastTime > step)
                {
                    s_notes.Add(new Note
                    {
                        time = timeOffset + currentTime,
                        track = Mathf.Abs(energy - lastEnergy) > variance ? Random.Range(0, numTracks) : lastTrack
                    });
                    lastTrack = s_notes[s_notes.Count - 1].track;
                    lastEnergy = energy;
                    lastTime = currentTime;
                }
            }

            energyHistory[historyIndex] = energy;
            historyIndex = (historyIndex + 1) % historySize;
        }
        segment.Dispose();

        return s_notes;
    }
}