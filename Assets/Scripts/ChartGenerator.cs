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
    public static int fftSize = 1024;
    public static int moveLength => fftSize / 2;
    public static int numSubBands = 4;
    public static int historySize = 43;
    public static float sensitivity = 4.0f;
    public static float[] pitchRanges = { 40, 50, 60, 70 };

    public static List<Note> GetChart(AudioClip audioClip)
    {
        if (audioClip == null)
            return null;

        List<Note> notes = new List<Note>();

        float[] subBandWidths = GenerateSubBandWidth(numSubBands);
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

        using (BurstFFT fft = new BurstFFT(fftSize))
        {
            NativeArray<float> segment = new NativeArray<float>(fftSize, Allocator.TempJob);
            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex += moveLength * 2)
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
                    // Update history buffer
                    // energyHistory[i][historyIndex] = subBandEnergy[i];

                    // Calculate moving average
                    float average = 0;
                    for (int j = 0; j < historySize; j++)
                    {
                        average += energyHistory[i][j];
                    }
                    average /= historySize;

                    if (subBandEnergies[i] > sensitivity * average)
                    {
                        notes.Add(new Note
                        {
                            time = (float)sampleIndex / sampleRate / 2,
                            track = i,
                        });
                    }

                    energyHistory[i][historyIndex] = subBandEnergies[i];
                }
                historyIndex = (historyIndex + 1) % historySize;
            }
            segment.Dispose();
        }

        return notes;
    }

    public static int GetTrackFromPitch(float pitch)
    {
        for (int i = 0; i < pitchRanges.Length - 1; i++)
        {
            if (pitch >= pitchRanges[i] && pitch < pitchRanges[i + 1])
            {
                return i;
            }
        }
        return -1;
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

    private static float[] GenerateSubBandWidth(int num)
    {
        float[] subBandWidths = new float[num];

        for (int i = 0; i < num; i++)
        {
            subBandWidths[i] = fftSize / num;
        }
        return subBandWidths;
    }

    private static float[] GenerateSubBandEnergy(NativeArray<float> spectrum, float[] subBandWidths)
    {
        float[] subBandEnergies = new float[subBandWidths.Length];
        int band = 0;
        float bandWidth = 0;
        for (int i = 0; i < fftSize; i++)
        {
            if (i >= bandWidth + subBandWidths[band])
            {
                bandWidth += subBandWidths[band];
                band++;
                if (band >= subBandWidths.Length)
                    break;
            }
            float magnitude = spectrum[i];
            subBandEnergies[band] += magnitude * magnitude;
        }
        return subBandEnergies;
    }

    public static float DetectPitch(NativeArray<float> spectrum, float sampleRate)
    {
        float maxMagnitude = 0;
        int maxBin = 0;

        // 找到频谱中能量最大的频率 bin
        for (int i = 0; i < spectrum.Length / 2; i++) // 只取正频率部分
        {
            float magnitude = spectrum[i];
            if (magnitude > maxMagnitude)
            {
                maxMagnitude = magnitude;
                maxBin = i;
            }
        }

        // 频率 = bin索引 × 采样率 / FFT大小
        float frequency = maxBin * sampleRate / (spectrum.Length * 2);
        return FrequencyToPitch(frequency); // 频率转音高（MIDI音符编号）
    }

    // 频率转MIDI音符编号（A4=440Hz对应MIDI 69）
    private static float FrequencyToPitch(float frequency)
    {
        if (frequency <= 0) return -1;
        return 12 * Mathf.Log10(frequency / 440) + 69;
    }
}
