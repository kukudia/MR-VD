using UnityEngine;

/// <summary>
/// Shared allocation-free projection from full-length FFT magnitudes to 40Hz–8kHz display bands.
/// The same integration/compression is used by AudioVisualField and controlled PCM regression.
/// This display projection is not a calibrated loudness or source-separation measurement.
/// </summary>
public static class AudioLogSpectrum
{
    public static float SampleBand(float[] fft, float sampleRate, int band, int bandCount)
    {
        if (fft == null || fft.Length < 4 || !float.IsFinite(sampleRate) || sampleRate <= 0f || bandCount < 2)
        {
            return 0f;
        }
        float t = Mathf.Clamp(band, 0, bandCount - 1) / (float)(bandCount - 1);
        float frequency = 40f * Mathf.Pow(200f, t);
        float halfStep = Mathf.Pow(200f, 0.5f / (bandCount - 1));
        int first = Mathf.Clamp(Mathf.FloorToInt(frequency / halfStep * fft.Length / sampleRate), 1, fft.Length / 2 - 1);
        int last = Mathf.Clamp(Mathf.CeilToInt(frequency * halfStep * fft.Length / sampleRate), first + 1, fft.Length / 2);
        float sum = 0f;
        for (int bin = first; bin < last; bin++)
        {
            sum += float.IsFinite(fft[bin]) ? Mathf.Max(0f, fft[bin]) : 0f;
        }
        return 1f - Mathf.Exp(-sum / (last - first) * 18f);
    }
}
