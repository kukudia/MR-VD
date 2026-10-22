using UnityEngine;

/// <summary>
/// Allocation-free appearance descriptors over equal-width log-frequency bands. Analyze returns
/// centroid, spread, flatness and positive change in [0,1]. These are visual controls, not
/// pitch/voice classification or a replacement for accepted audio onsets.
/// </summary>
public sealed class AudioSpectralShape
{
    private readonly float[] previous;
    private bool primed;

    public AudioSpectralShape(int bands)
    {
        previous = new float[Mathf.Max(2, bands)];
    }

    public Vector4 Analyze(float[] bands, bool silent)
    {
        if (bands == null || bands.Length != previous.Length || silent)
        {
            Reset();
            return Vector4.zero;
        }
        float total = 0f;
        float weighted = 0f;
        float logTotal = 0f;
        float rise = 0f;
        for (int i = 0; i < bands.Length; i++)
        {
            float magnitude = float.IsFinite(bands[i]) ? Mathf.Clamp01(bands[i]) : 0f;
            total += magnitude;
            weighted += magnitude * i / (bands.Length - 1f);
            logTotal += Mathf.Log(Mathf.Max(0.000001f, magnitude));
            rise += Mathf.Max(0f, magnitude - previous[i]);
            previous[i] = magnitude;
        }
        if (total < 0.0001f)
        {
            Reset();
            return Vector4.zero;
        }
        float centroid = weighted / total;
        float variance = 0f;
        for (int i = 0; i < bands.Length; i++)
        {
            float distance = i / (bands.Length - 1f) - centroid;
            variance += previous[i] * distance * distance;
        }
        float spread = Mathf.Clamp01(Mathf.Sqrt(variance / total) * 2f);
        float flatness = Mathf.Clamp01(Mathf.Exp(logTotal / bands.Length) / (total / bands.Length));
        float change = primed ? Mathf.Clamp01(rise / total) : 0f;
        primed = true;
        return new Vector4(centroid, spread, flatness, change);
    }

    public void Reset()
    {
        System.Array.Clear(previous, 0, previous.Length);
        primed = false;
    }
}
