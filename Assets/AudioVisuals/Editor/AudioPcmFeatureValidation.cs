using System;
using System.Globalization;
using System.IO;
using System.Linq;
using CSCore.DSP;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reads authored PCM fixtures without playback/capture and runs production CSCore FFT and log
/// projection, followed by the same attack/release and median descriptors. Saves measured ranges;
/// does not touch singleton capture/devices or claim listening accuracy on representative music.
/// </summary>
public static class AudioPcmFeatureValidation
{
    private const int SampleRate = 48000;
    private const int Hop = 2400;
    private const string Root = "Temp/AudioVisualResearch/ReferenceAudio";

    private struct Metrics
    {
        public float centroid, persistent, broadbandPeak, peakBand, maximum, peakPcmRms, peakSpectralNorm, peakAggregate;
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Validate Authored PCM Features")]
    public static void Run()
    {
        string[] names = { "low-tone", "high-tone", "harmonic-bed", "broadband-bursts", "mixed-bed-bursts", "silence" };
        Metrics[] metrics = new Metrics[names.Length];
        string report = DateTimeOffset.Now.ToString("o")
            + "\nControlled PCM through CSCore Fft4096 and production AudioLogSpectrum; 48kHz/20Hz hops.\n";
        for (int i = 0; i < names.Length; i++)
        {
            metrics[i] = Analyze(Path.Combine(Root, names[i] + ".wav"));
            Metrics m = metrics[i];
            report += string.Format(CultureInfo.InvariantCulture,
                "{0}: centroid={1:F4}, persistent={2:F4}, broadbandPeak={3:F4}, peakBand={4:F1}, maximum={5:F5}, pcmHopRmsPeak={6:F5}, fftNormPeak={7:F5}, aggregateGain6Peak={8:F5}\n",
                names[i], m.centroid, m.persistent, m.broadbandPeak, m.peakBand, m.maximum, m.peakPcmRms, m.peakSpectralNorm, m.peakAggregate);
        }
        Require(metrics[0].peakBand >= 6f && metrics[0].peakBand <= 12f, "110Hz PCM projected to the wrong display bands.");
        Require(metrics[1].peakBand >= 37f && metrics[1].peakBand <= 43f, "3520Hz PCM projected to the wrong display bands.");
        Require(metrics[1].centroid > metrics[0].centroid + 0.4f, "Actual high/low PCM centroid ordering failed.");
        Require(metrics[2].persistent > 0.65f, "Sustained harmonic PCM did not produce persistent display structure.");
        Require(metrics[3].broadbandPeak > metrics[2].broadbandPeak + 0.15f,
            "Broadband PCM pulses were not distinguished from sustained harmonic structure.");
        Require(metrics[5].maximum == 0f && metrics[5].centroid == 0f, "Silent PCM produced display energy.");
        Require(metrics[5].peakPcmRms == 0f && metrics[5].peakSpectralNorm == 0f, "Silent PCM produced aggregate energy.");
        Require(Mathf.Abs(metrics[0].peakPcmRms - metrics[1].peakPcmRms) < 0.005f,
            "Equal-amplitude tone fixtures do not have comparable PCM RMS.");
        float normRatio = metrics[1].peakSpectralNorm / Mathf.Max(0.000001f, metrics[0].peakSpectralNorm);
        Require(normRatio > 0.9f && normRatio < 1.1f, "Equal-amplitude FFT aggregate changed substantially with tone frequency.");
        Require(Mathf.Abs(metrics[0].peakAggregate - metrics[1].peakAggregate) < 0.01f,
            "Production aggregate mapping changed substantially with equal-amplitude tone frequency.");
        report += "PASS: low/high band projection and centroid order; persistence/broadband contrast; silence; all outputs finite.\n"
            + "Aggregate study: equal-amplitude low/high raw-FFT L2 norms agree within 10%; silence is zero.\n"
            + "FFT L2 is an uncalibrated aggregate, not LUFS or perceptual loudness. PCM RMS uses 50ms hops; FFT uses its own window.\n"
            + "Controlled PCM only; excludes WASAPI capture, real musical listening, BPM/key accuracy and XR.\n";
        File.WriteAllText(AudioVisualFieldTools.PreviewDirectory + "/pcm-features.txt", report);
        Debug.Log("[AudioPCM] " + report);
    }

    private static Metrics Analyze(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("Generate PCM fixtures first: Tools/AudioVisualResearch/Generate-ReferenceAudio.ps1");
        }
        float[] pcm;
        using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
        {
            // These are controlled fixed-layout authoring fixtures, not a general-purpose WAV parser.
            Require(new string(reader.ReadChars(4)) == "RIFF", "Invalid fixture RIFF header.");
            reader.BaseStream.Position = 20;
            Require(reader.ReadInt16() == 1 && reader.ReadInt16() == 1 && reader.ReadInt32() == SampleRate,
                "Fixture requires 48kHz mono PCM.");
            reader.BaseStream.Position = 34;
            Require(reader.ReadInt16() == 16, "Fixture requires signed 16-bit samples.");
            reader.BaseStream.Position = 40;
            int bytes = reader.ReadInt32();
            Require(bytes > 0 && bytes <= SampleRate * 30 * 2 && bytes == reader.BaseStream.Length - 44,
                "Unexpected PCM fixture length.");
            pcm = new float[bytes / 2];
            for (int i = 0; i < pcm.Length; i++)
            {
                pcm[i] = reader.ReadInt16() / 32768f;
            }
        }
        FftProvider provider = new FftProvider(1, FftSize.Fft4096);
        AudioSpectralShape shape = new AudioSpectralShape(48);
        AudioSpectralLayers layers = new AudioSpectralLayers(48);
        float[] fft = new float[4096];
        float[] smoothFft = new float[4096];
        float[] bands = new float[48];
        Metrics metrics = new Metrics();
        int frames = 0;
        int peaks = 0;
        for (int offset = 0; offset + Hop <= pcm.Length; offset += Hop)
        {
            double pcmPower = 0d;
            for (int sample = offset; sample < offset + Hop; sample++)
            {
                provider.Add(pcm[sample], pcm[sample]);
                pcmPower += pcm[sample] * (double)pcm[sample];
            }
            if (!provider.GetFftData(fft))
            {
                continue;
            }
            for (int bin = 0; bin < fft.Length; bin++)
            {
                smoothFft[bin] = Mathf.Lerp(fft[bin], smoothFft[bin], 0.5f);
            }
            for (int band = 0; band < bands.Length; band++)
            {
                float target = AudioLogSpectrum.SampleBand(smoothFft, SampleRate, band, bands.Length);
                bands[band] = Mathf.Lerp(bands[band], target, 1f - Mathf.Exp(-(target > bands[band] ? 12f : 3f) * 0.05f));
            }
            Vector4 descriptor = shape.Analyze(bands, false);
            Vector4 split = layers.Analyze(bands, 0.05f, false);
            Require(float.IsFinite(descriptor.x) && float.IsFinite(split.x), "PCM produced non-finite features.");
            metrics.maximum = Mathf.Max(metrics.maximum, bands.Max());
            if (offset / (float)SampleRate >= 1f)
            {
                double spectralPower = 0d;
                int firstBin = Mathf.CeilToInt(40f * fft.Length / SampleRate);
                int lastBin = Mathf.FloorToInt(8000f * fft.Length / SampleRate);
                for (int bin = firstBin; bin <= lastBin; bin++)
                {
                    Require(float.IsFinite(fft[bin]), "Non-finite raw FFT in energy study.");
                    spectralPower += fft[bin] * (double)fft[bin];
                }
                metrics.peakPcmRms = Mathf.Max(metrics.peakPcmRms, (float)Math.Sqrt(pcmPower / Hop));
                metrics.peakSpectralNorm = Mathf.Max(metrics.peakSpectralNorm, (float)Math.Sqrt(spectralPower));
                metrics.peakAggregate = Mathf.Max(metrics.peakAggregate,
                    AudioSpectralEnergy.Blend(0f, fft, SampleRate, 6f, 1f));
                metrics.centroid += descriptor.x;
                metrics.persistent += split.x;
                metrics.broadbandPeak = Mathf.Max(metrics.broadbandPeak, split.y);
                int peak = 0;
                for (int band = 1; band < bands.Length; band++)
                {
                    if (bands[band] > bands[peak])
                    {
                        peak = band;
                    }
                }
                peaks += peak;
                frames++;
            }
        }
        Require(frames > 50, "Insufficient CSCore FFT frames.");
        metrics.centroid /= frames;
        metrics.persistent /= frames;
        metrics.peakBand = peaks / (float)frames;
        return metrics;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
