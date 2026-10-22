using System;
using UnityEngine;

/// <summary>
/// Optional aggregate visual energy from full raw FFT magnitudes over 40Hz–8kHz. Unlike display
/// bands it does not average by band width. The compressed L2 norm is not calibrated loudness;
/// it never publishes onsets or changes bass/detail analysis. No state or per-call allocations.
/// </summary>
public static class AudioSpectralEnergy
{
    public static float Blend(float legacyEnergy, float[] fft, float sampleRate, float gain, float weight)
    {
        if (fft == null || fft.Length < 4 || !float.IsFinite(sampleRate) || sampleRate <= 80f
            || !float.IsFinite(weight) || weight <= 0f || !float.IsFinite(gain))
        {
            return legacyEnergy;
        }
        int first = Mathf.Clamp(Mathf.CeilToInt(40f * fft.Length / sampleRate), 1, fft.Length / 2 - 1);
        int last = Mathf.Clamp(Mathf.FloorToInt(8000f * fft.Length / sampleRate), first, fft.Length / 2 - 1);
        double power = 0d;
        for (int bin = first; bin <= last; bin++)
        {
            double magnitude = float.IsFinite(fft[bin]) ? Math.Max(0d, fft[bin]) : 0d;
            power += magnitude * magnitude;
        }
        float aggregate = (float)(1d - Math.Exp(-Math.Sqrt(power) * Math.Max(0d, gain)));
        return Mathf.Lerp(legacyEnergy, aggregate, Mathf.Clamp01(weight));
    }
}
