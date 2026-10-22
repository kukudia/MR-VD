using System;
using UnityEngine;

/// <summary>
/// Causal display-spectrum median masks: a trailing temporal median estimates persistence,
/// a local frequency median estimates broadband structure. Analyze samples at 20 Hz without
/// lookahead or allocations. This is a visualization heuristic, not audio source separation.
/// </summary>
public sealed class AudioSpectralLayers
{
    private const int TemporalWindow = 9;
    private const int FrequencyWindow = 7;
    private const float Interval = 0.05f;
    private readonly int bandCount;
    private readonly float[] history;
    private readonly float[] current;
    private readonly float[] temporal = new float[TemporalWindow];
    private readonly float[] frequency = new float[FrequencyWindow];
    private int cursor, samples;
    private float elapsed;
    public Vector4 Current { get; private set; }

    public AudioSpectralLayers(int bands)
    {
        bandCount = Mathf.Max(1, bands);
        history = new float[bandCount * TemporalWindow];
        current = new float[bandCount];
    }

    public void Reset()
    {
        Array.Clear(history, 0, history.Length);
        Array.Clear(current, 0, current.Length);
        cursor = samples = 0;
        elapsed = 0f;
        Current = Vector4.zero;
    }

    // x/y: complementary persistent/broadband energy ratios. z: contrast times warm-up coverage.
    // w: warm-up coverage. Broad sustained noise is ambiguous (x=y=.5, z=0), not a drum label.
    public Vector4 Analyze(float[] spectrum, float deltaTime, bool silent)
    {
        if (silent || spectrum == null || spectrum.Length < bandCount)
        {
            Reset();
            return Current;
        }
        elapsed += float.IsFinite(deltaTime) ? Mathf.Clamp(deltaTime, 0f, 0.1f) : 0f;
        if (elapsed + 0.000001f < Interval)
        {
            return Current;
        }
        // Do not insert repeated copies of one spectrum when recovering from a stalled frame.
        elapsed = Mathf.Max(0f, elapsed - Interval);
        if (elapsed >= Interval)
        {
            elapsed %= Interval;
        }
        samples = Mathf.Min(samples + 1, TemporalWindow);
        float total = 0f;
        for (int band = 0; band < bandCount; band++)
        {
            float value = float.IsFinite(spectrum[band]) ? Mathf.Clamp01(spectrum[band]) : 0f;
            current[band] = value;
            history[band * TemporalWindow + cursor] = value;
            total += value;
        }
        cursor = (cursor + 1) % TemporalWindow;
        if (total < 0.00001f)
        {
            Current = Vector4.zero;
            return Current;
        }
        float persistent = 0f;
        float contrast = 0f;
        for (int band = 0; band < bandCount; band++)
        {
            Array.Copy(history, band * TemporalWindow, temporal, 0, TemporalWindow);
            Array.Sort(temporal);
            for (int offset = 0; offset < FrequencyWindow; offset++)
            {
                frequency[offset] = current[Mathf.Clamp(band + offset - FrequencyWindow / 2, 0, bandCount - 1)];
            }
            Array.Sort(frequency);
            float h = temporal[TemporalWindow / 2];
            float p = frequency[FrequencyWindow / 2];
            float sum = h * h + p * p;
            float mask = sum > 0.00000001f ? h * h / sum : 0.5f;
            persistent += current[band] * mask;
            contrast += current[band] * Mathf.Abs(mask * 2f - 1f);
        }
        float coverage = samples / (float)TemporalWindow;
        float ratio = Mathf.Clamp01(persistent / total);
        Current = new Vector4(ratio, 1f - ratio, Mathf.Clamp01(contrast / total) * coverage, coverage);
        return Current;
    }
}
