using System.Collections;
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
    public static float step = 0.3f;
    public static int fftSize = 1024;
    public static int moveLength = 1024;
    public static int numSubBands = 4;
    public static int historySize = 43;
    public static float sensitivity = 2.5f;

    public static List<Note> GetChart(AudioClip audioClip)
    {
        if (audioClip == null)
            return null;

        List<Note> notes = new List<Note>();

        float[] subBandWidths = GenerateSubBandWidth(numSubBands, 32);
        float[][] energyHistory = new float[numSubBands][];
        for (int i = 0; i < numSubBands; i++)
        {
            energyHistory[i] = new float[historySize];
        }
        int sampleRate = audioClip.frequency;
        float[] samples = new float[audioClip.samples * audioClip.channels];
        audioClip.GetData(samples, 0);
        float[] window = GenerateHanningWindow(fftSize);
        int historyIndex = 0;
        float[] lastTime = new float[numSubBands];

        using (BurstFFT fft = new BurstFFT(fftSize))
        {
            NativeArray<float> segment = new NativeArray<float>(fftSize, Allocator.TempJob);
            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex += moveLength)
            {
                // Get Spectrum
                for (int i = 0; i < fftSize; i++)
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

                        float C = 1.5142857f - variance / average / average * 0.25714f;
                        float V0 = 1e-4f;
                        float currentTime = (float)(sampleIndex + moveLength / 2) / sampleRate / 2;
                        if (subBandEnergies[i] > sensitivity * C * average && variance > sensitivity * V0 * average && currentTime - lastTime[i] > step)
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
            window[i] = 0.5f * (1 - Mathf.Cos(2 * Mathf.PI * i / (size - 1)));
        }
        return window;
    }

    private static float[] GenerateSubBandWidth(int num, int totalWidth)
    {
        float[] subBandWidths = new float[num];

        int b = 5;
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

        for (int i = 0; i < fftSize / 2; i++)
        {
            if (i >= bandWidth + subBandWidths[band])
            {
                bandWidth += subBandWidths[band];
                band++;
                if (band >= numBands)
                    break;
            }
            float magnitude = spectrum[i];
            subBandEnergies[band] += magnitude * magnitude / subBandWidths[band];
        }
        return subBandEnergies;
    }
}
