using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class Note
{
    public float time;
    public int track;
}

public static class ChartGenerator
{
    public static float step = 0.2f;
    public static int windowSize = 2048;
    public static int moveLength = 2048;
    public static int firstBandWidth = 10;
    public static int allBandWidth = 64;
    public static int numSubBands = 4;
    public static int historySize = 43;
    public static float sensitivity = 3.0f;

    public static List<Note> GetChart(AudioClip audioClip)
    {
        if (audioClip == null)
            return null;

        List<Note> notes = new List<Note>();

        float[] subBandWidths = GenerateSubBandWidth(numSubBands, allBandWidth);
        float[][] energyHistory = new float[numSubBands][];
        for (int i = 0; i < numSubBands; i++)
        {
            energyHistory[i] = new float[historySize];
        }
        int sampleRate = audioClip.frequency;
        float[] samples = new float[audioClip.samples * audioClip.channels];
        audioClip.GetData(samples, 0);
        float[] window = GenerateHanningWindow(windowSize);
        int historyIndex = 0;
        float[] lastTime = new float[numSubBands];

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
                    segment[i] = samples[sampleIndex + i * 2] * window[i];
                }
                fft.Transform(segment);
                NativeArray<float> spectrum = fft.Spectrum;
                float[] subBandEnergies = GenerateSubBandEnergy(spectrum, subBandWidths);
                for (int i = 0; i < numSubBands; i++)
                {
                    if (sampleIndex / moveLength >= historySize)
                    {
                        float average = 0;
                        for (int j = 0; j < historySize; j++)
                        {
                            average += energyHistory[i][j];
                        }
                        average /= historySize;

                        float variance = 0;
                        for (int j = 0; j < historySize; j++)
                        {
                            variance += Mathf.Pow(energyHistory[i][j] - average, 2);
                        }
                        variance /= historySize;

                        float C = 1.5142857f - variance / average / average * 0.0025714f;
                        float V0 = 0.2f * average * average;
                        float currentTime = (float)(sampleIndex + windowSize) / sampleRate / 2;
                        if (subBandEnergies[i] > sensitivity * C * average && variance > sensitivity * V0 && currentTime - lastTime[i] > step)
                        {
                            notes.Add(new Note
                            {
                                time = currentTime,
                                track = i,
                            });
                            lastTime[i] = currentTime;
                        }
                    }

                    energyHistory[i][historyIndex] = subBandEnergies[i];
                }
                historyIndex = (historyIndex + 1) % historySize;
            }
            segment.Dispose();
        }

        return notes;
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

// public static class ChartGenerator
// {
//     public static float step = 0.2f;
//     public static int windowSize = 1024;
//     public static int moveLength = 2048;
//     public static int historySize = 43;
//     public static float sensitivity = 1.0f;

//     public static List<Note> GetChart(AudioClip audioClip)
//     {
//         if (audioClip == null)
//             return null;

//         List<Note> notes = new List<Note>();

//         float[] energyHistory = new float[historySize];
//         int sampleRate = audioClip.frequency;
//         float[] samples = new float[audioClip.samples * audioClip.channels];
//         audioClip.GetData(samples, 0);
//         int historyIndex = 0;
//         float lastTime = 0;

//         NativeArray<float> segment = new NativeArray<float>(windowSize, Allocator.TempJob);
//         for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex += moveLength)
//         {
//             float energy = 0;
//             for (int i = 0; i < windowSize; i++)
//             {
//                 if (sampleIndex + i * 2 >= samples.Length)
//                     break;
//                 float sample = samples[sampleIndex + i * 2];
//                 energy += sample * sample;
//             }
//             energy /= windowSize;
//             if (sampleIndex / moveLength >= historySize)
//             {
//                 float average = 0;
//                 for (int j = 0; j < historySize; j++)
//                 {
//                     average += energyHistory[j];
//                 }
//                 average /= historySize;

//                 float variance = 0;
//                 for (int j = 0; j < historySize; j++)
//                 {
//                     variance += Mathf.Pow(energyHistory[j] - average, 2);
//                 }
//                 variance /= historySize;

//                 float C = 1.5142857f - variance / average / average * 0.0025714f;
//                 float currentTime = (float)(sampleIndex) / sampleRate / 2;
//                 if (energy > sensitivity * C * average && currentTime - lastTime > step)
//                 {
//                     notes.Add(new Note
//                     {
//                         time = currentTime,
//                         track = (int)Mathf.Clamp(energy / average, 1, 4) - 1,
//                     });
//                     lastTime = currentTime;
//                 }
//             }

//             energyHistory[historyIndex] = energy;
//             historyIndex = (historyIndex + 1) % historySize;
//         }
//         segment.Dispose();

//         return notes;
//     }
// }