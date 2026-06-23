using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Builds an audio-reactive bar visualizer and exposes beat, BPM, silence, and key-analysis data.
/// </summary>
public class AudioVisualizer : MonoBehaviour
{
    public bool movingBars;

    [Tooltip("Time-domain smoothing weight for FFT data. 0 disables smoothing; values near 1 are highly smoothed.")]
    public float smoothingWeight = 0.5f;

    public GameObject barPrefab;

    [Tooltip("Total number of visualizer bars.")]
    public int barCount = 64;

    [Tooltip("Parent transform used to position the visualizer bar group.")]
    public Transform barPosition;

    [Tooltip("Horizontal spacing between visualizer bars.")]
    public float horizontalScale = 0.01f;

    [Tooltip("Height multiplier applied to visualizer bars.")]
    public float verticalScale = 1f;

    [Tooltip("Semi-major axis of the bar placement ellipse. Controls width.")]
    public float a = 5;

    [Tooltip("Semi-minor axis of the bar placement ellipse. Controls depth.")]
    public float b = 1;

    [Header("Log Compression")]
    [Tooltip("Log compression strength. Higher values increase compression; 1.0 to 3.0 is recommended.")]
    [Range(0.1f, 100f)]
    public float logCompressionStrength = 1.5f;

    [Tooltip("Log compression offset. Prevents log(0); 0.01 to 1.0 is recommended.")]
    [Range(0.001f, 2f)]
    public float logCompressionOffset = 0.1f;

    [Tooltip("Maximum visualizer bar height.")]
    public float maxBarHeight = 10f;

    [Tooltip("Minimum visualizer bar height to keep bars visible.")]
    public float minBarHeight = 0.05f;

    [Tooltip("Enables automatic dynamic range adjustment.")]
    public bool enableDynamicRange = true;

    [Tooltip("Speed used for dynamic range adjustment.")]
    [Range(0.1f, 5f)]
    public float dynamicRangeSpeed = 1f;

    private const int fftSize = 2048;

    private float dynamicScaleFactor = 1f;
    private float maxRecentAmplitude = 1f;

    private GameObject[] bars;
    private Renderer[] barRenderers;
    private float[] barGlowLevels;
    private MaterialPropertyBlock barPropertyBlock;
    private Material runtimeBarGlowMaterial;

    [Header("Emissive Bars")]
    [Tooltip("Enables emissive bar materials and audio-reactive color changes.")]
    public bool enableBarGlow = true;

    [Tooltip("Optional emissive bar material. When empty, a URP/Lit material is created at runtime.")]
    public Material barGlowMaterial;

    [Tooltip("Base emission intensity during silence or low-energy playback.")]
    [Range(0f, 8f)]
    public float baseBarEmissionIntensity = 0.8f;

    [Tooltip("Multiplier that maps audio energy to bar emission intensity.")]
    [Range(0f, 30f)]
    public float audioBarEmissionIntensity = 8f;

    [Tooltip("Additional emission intensity applied when a beat is detected.")]
    [Range(0f, 20f)]
    public float beatBarEmissionBoost = 4f;

    [Tooltip("Speed of time-based hue cycling.")]
    [Range(0f, 2f)]
    public float barHueCycleSpeed = 0.16f;

    [Tooltip("Color and brightness response speed.")]
    [Range(1f, 30f)]
    public float barGlowSmoothingSpeed = 12f;

    [Tooltip("Frequency-band hue spread. Higher values create stronger color separation between bars.")]
    [Range(0f, 1f)]
    public float barFrequencyHueSpread = 0.42f;

    [Tooltip("Influence of kick energy on the overall hue.")]
    [Range(0f, 1f)]
    public float kickHueInfluence = 0.16f;

    [Tooltip("Influence of synth energy on the overall hue.")]
    [Range(0f, 1f)]
    public float synthHueInfluence = 0.22f;

    private Queue<float> kickEnergyHistory = new Queue<float>();
    private Queue<float> snareEnergyHistory = new Queue<float>();
    private Queue<float> bassEnergyHistory = new Queue<float>();

    [Tooltip("Energy history window size in frames for adaptive threshold calculation.")]
    public int energyHistorySize = 50;

    [Tooltip("Recent beat timestamps used for BPM calculation.")]
    public List<float> beatTimestamps = new List<float>();

    private List<float> beatConfidences = new List<float>();

    [Tooltip("Timestamp of the last detected beat, in Time.time seconds.")]
    public float lastBeatTime = 0f;

    [Tooltip("Timestamp of the last BPM update, in Time.time seconds.")]
    public float lastBpmUpdateTime = 0f;

    public bool useKalmanEstimate = true;

    public float detectedBPM = 0f;

    public float limitedBPM = 0f;

    private float bpmVariance = 0f;

    [Tooltip("Kick trigger threshold calculated from historical energy statistics.")]
    public float dynamicKickThreshold;

    [Tooltip("Snare trigger threshold calculated from historical energy statistics.")]
    public float dynamicSnareThreshold;

    private float predictedNextBeat = 0f;
    private float phaseError = 0f;

    [Tooltip("BPM update interval in seconds. Lower values respond faster but are less stable.")]
    public float bpmUpdateInterval = 0.5f;

    [Tooltip("Minimum output value for BPM estimation.")]
    public float minTrackedBPM = 60f;

    [Tooltip("Maximum output value for BPM estimation. Prevents short-interval false positives from drifting upward.")]
    public float maxTrackedBPM = 200f;

    [Tooltip("Maximum allowed BPM change ratio per update. Reduces short-interval feedback loops.")]
    [Range(0.05f, 0.5f)]
    public float maxBpmChangeRatio = 0.14f;

    [Tooltip("Minimum interval between valid beats in seconds. Corresponds to roughly 200 BPM.")]
    public float minBeatInterval = 0.25f;


    [Tooltip("Hard beat cooldown in seconds. Onsets inside this window are ignored to avoid duplicate hits.")]
    public float beatCooldown = 0.35f;
    [Tooltip("Maximum interval between valid beats in seconds. Corresponds to roughly 50 BPM.")]
    public float maxBeatInterval = 1.2f;

    [Tooltip("Current beat interval in seconds, derived from limitedBPM.")]
    public float beatInterval = 0.5f;

    [Tooltip("Controls whether the BEAT overlay text is currently visible.")]
    public bool showBeatText = false;

    [Tooltip("Beat overlay display duration in seconds. Automatically set to one quarter of the beat interval.")]
    public float beatDisplayTime = 0.2f;

    private float beatTimer = 0f;

    [Tooltip("Minimum confidence required to accept a beat. The system adjusts this based on BPM stability.")]
    [Range(0f, 1f)]
    public float minBeatConfidence = 0.4f;

    private float previousKickEnergy = 0f;
    private float previousSnareEnergy = 0f;
    private float previousBassEnergy = 0f;
    private float playStartTime = 0f;
    private float silenceStartTime = 0f;

    [Header("Debug Display")]
    [Tooltip("Shows a standalone AudioVisualizer status overlay when AudioCaptureCSCore is not displaying a combined debug panel.")]
    public bool showStandaloneStatusOverlay = true;

    [Header("Silence Detection")]
    [Tooltip("Broadband energy must stay below this threshold before entering silence.")]
    public float silenceEnterThreshold = 0.001f;

    [Tooltip("Broadband energy must stay above this threshold before playback resumes. Keep above the enter threshold.")]
    public float silenceExitThreshold = 0.001f;

    [Tooltip("Continuous low-energy duration required before entering silence, in seconds.")]
    public float silenceEnterDelay = 0.25f;

    [Tooltip("Continuous high-energy duration required before exiting silence, in seconds.")]
    public float silenceExitDelay = 0.05f;

    [Tooltip("Smoothing speed for silence energy. Higher values respond faster; lower values reduce jitter.")]
    [Range(1f, 30f)]
    public float silenceEnergySmoothSpeed = 18f;

    [Tooltip("Multiplier used to reduce beat confidence during low-energy but non-silent playback.")]
    public float lowEnergyThresholdMultiplier = 4f;

    private float smoothedSilenceEnergy = 0f;
    private float pendingSilenceStartTime = -1f;
    private float pendingSoundStartTime = -1f;
    public bool wasSilent = true;
    private List<float> beatStrengths = new List<float>();

    [Tooltip("Onset sensitivity. Higher values require stronger energy spikes before triggering a beat.")]
    [Range(0.3f, 3.0f)]
    public float onsetSensitivity = 1.5f;

    [Tooltip("Kick threshold smoothing speed. Higher values adapt faster.")]
    [Range(1f, 100f)]
    public float dynamicKickThresholdSpeed = 2f;

    [Tooltip("Snare threshold smoothing speed. Higher values adapt faster.")]
    [Range(1f, 100f)]
    public float dynamicSnareThresholdSpeed = 2f;

    private float smoothedKickThreshold = 0.5f;
    private float smoothedSnareThreshold = 0.5f;

    private float kalmanEstimate = 0f;

    [Tooltip("Detected key name, such as C, C#, D, or B.")]
    public string currentKey = "Unknown";

    [Tooltip("Detected key mode, such as Major or Minor.")]
    public string currentMode = "Unknown";

    [Tooltip("Key detection update interval in seconds. Set to 0 to update every frame.")]
    public float keyUpdateInterval = 0.0f;

    [Tooltip("Chroma smoothing factor for key detection. Higher values are more stable; lower values respond faster.")]
    [Range(0f, 0.98f)]
    public float keyChromaSmoothing = 0.55f;

    [Tooltip("Minimum score margin required before switching to a new key.")]
    [Range(0f, 0.3f)]
    public float keySwitchMargin = 0.015f;

    [Tooltip("Number of consecutive confirmations required before switching key results.")]
    [Range(1, 12)]
    public int keyStableFrameThreshold = 2;

    [Tooltip("Current key detection confidence, measured as the score gap between the best and second-best templates.")]
    public float currentKeyConfidence = 0f;

    private float lastKeyUpdateTime = 0f;
    private readonly double[] smoothedChroma = new double[12];
    private bool hasSmoothedChroma = false;
    private string pendingKey = "Unknown";
    private string pendingMode = "Unknown";
    private int pendingKeyFrames = 0;

    // Standard Krumhansl-Schmuckler key profiles.
    private static readonly double[] majorProfile = new double[]
    {
        6.35, 2.23, 3.48, 2.33, 4.38, 4.09,
        2.52, 5.19, 2.39, 3.66, 2.29, 2.88
    };

    private static readonly double[] minorProfile = new double[]
    {
        6.33, 2.68, 3.52, 5.38, 2.60, 3.53,
        2.54, 4.75, 3.98, 2.69, 3.34, 3.17
    };

    private static readonly string[] keyNames = new string[]
    {
        "C", "C#", "D", "D#", "E", "F",
        "F#", "G", "G#", "A", "A#", "B"
    };

    [Tooltip("Mean energy of the kick frequency band for the current frame, roughly 40 to 100 Hz.")]
    public float kickEnergy;
    public float smoothedKickEnergy;

    [Tooltip("Mean energy of the bass frequency band for the current frame, roughly 60 to 250 Hz.")]
    public float bassEnergy;
    public float smoothedBassEnergy;

    [Tooltip("Mean energy of the synth frequency band for the current frame, roughly 400 to 4000 Hz.")]
    public float synthEnergy;
    public float smoothedSynthEnergy;

    [Tooltip("Minimum kickEnergy required to trigger kick VFX events.")]
    public float kickThreshold = 0.5f;

    [Tooltip("Sensitivity multiplier for mapping bass energy to the VFX BassRate parameter.")]
    public float bassSensitivity = 20f;

    [Tooltip("Sensitivity multiplier for mapping synth energy to the VFX SynthStrength parameter.")]
    public float synthSensitivity = 10f;

    void Start()
    {
        wasSilent = true;
        silenceStartTime = Time.time;
        playStartTime = 0f;
        pendingSilenceStartTime = -1f;
        pendingSoundStartTime = -1f;
        smoothedSilenceEnergy = 0f;

        if (!movingBars)
        {
            bars = new GameObject[barCount];
            barRenderers = new Renderer[barCount];
            barGlowLevels = new float[barCount];
            for (int i = 0; i < barCount; i++)
            {
                GameObject bar = Instantiate(barPrefab, transform);
                float x = -barCount / 2 * horizontalScale + i * horizontalScale;
                float z = Mathf.Sqrt(1 - (x * x) / (a * a)) * b;
                bar.transform.position = new Vector3(x, transform.position.y, z);
                bars[i] = bar;
                CacheBarRenderer(i, bar);
            }
        }
    }

    void FixedUpdate()
    {
        if (AudioCaptureCSCore.instance != null &&
            AudioCaptureCSCore.instance.TryUpdateFftData(fftSize, verticalScale, smoothingWeight))
        {
            ProcessFftData(AudioCaptureCSCore.instance.rawFftData);
        }
    }

    private void ProcessFftData(float[] fftBuffer)
    {
        float[] frequencyData = AudioCaptureCSCore.instance.frequencyData;
        float[] smoothedFftData = AudioCaptureCSCore.instance.smoothedFftData;

        if (frequencyData == null || smoothedFftData == null ||
            frequencyData.Length == 0 || smoothedFftData.Length == 0)
        {
            return;
        }

        if (limitedBPM <= 0)
        {
            if (Time.time - lastKeyUpdateTime >= keyUpdateInterval)
            {
                DetectKeyFromFft(fftBuffer);
            }
        }

        // Count down the transient beat overlay.
        if (showBeatText)
        {
            beatTimer -= Time.deltaTime;
            if (beatTimer <= 0f)
            {
                showBeatText = false;
            }
        }

        // Calculate band energy values.
        kickEnergy = GetBandEnergy(frequencyData, 40, 100);
        smoothedKickEnergy = GetBandEnergy(smoothedFftData, 40, 100);

        bassEnergy = GetBandEnergy(frequencyData, 60, 250);
        smoothedBassEnergy = GetBandEnergy(smoothedFftData, 60, 250);

        synthEnergy = GetBandEnergy(frequencyData, 400, 4000);
        smoothedSynthEnergy = GetBandEnergy(smoothedFftData, 400, 4000);

        // Use the unsmoothed spectrum for silence detection so visual smoothing does not delay state changes.
        CheckAndHandleSilence(frequencyData, Time.time);

        // Update visualizer bars.
        UpdateBars(smoothedFftData);

        // Run the unified beat detection pipeline.
        DetectBeatImproved(frequencyData);
    }

    /// <summary>
    /// Updates the bar visualization using log compression, dynamic range adjustment, height limits, and smoothing.
    /// </summary>
    private void UpdateBars(float[] spectrumData)
    {
        if (movingBars)
        {
            List<GameObject> barObjects = new List<GameObject>();
            if (Time.time - lastBeatTime >= beatInterval)
            {
                bars = new GameObject[barCount];
                barRenderers = new Renderer[barCount];
                barGlowLevels = new float[barCount];
                for (int i = 0; i < barCount; i++)
                {
                    GameObject bar = Instantiate(barPrefab, transform);
                    float x = -barCount / 2 * horizontalScale + i * horizontalScale;
                    float z = Mathf.Sqrt(1 - (x * x) / (a * a)) * b;
                    bar.transform.position = new Vector3(x, transform.position.y, z);
                    bars[i] = bar;
                    CacheBarRenderer(i, bar);
                    barObjects.Add(bar);
                    Destroy(bar, 5);
                }
            }

            if (barObjects.Count > 0)
            {
                foreach (GameObject bar in barObjects)
                {
                    if (bar != null)
                    {
                        bar.transform.position += bar.transform.forward.normalized;
                    }
                    else
                    {
                        barObjects.Remove(bar);
                    }
                }
            }
        }

        if (bars == null || bars.Length != barCount)
        {
            return;
        }

        // Adjust the dynamic range.
        if (enableDynamicRange)
        {
            // Find the maximum amplitude in the current frame.
            float currentMaxAmplitude = 0f;
            for (int i = 0; i < barCount; i++)
            {
                float logIndex = Mathf.Pow((float)(i + 1) / barCount, 1);
                int fftIndex = Mathf.Clamp((int)(logIndex * (spectrumData.Length - 1)), 0, spectrumData.Length - 1);
                currentMaxAmplitude = Mathf.Max(currentMaxAmplitude, spectrumData[fftIndex]);
            }

            // Smoothly update the recent maximum amplitude.
            maxRecentAmplitude = Mathf.Lerp(maxRecentAmplitude, currentMaxAmplitude, Time.deltaTime * dynamicRangeSpeed);

            // Adjust the scale factor from the maximum amplitude while avoiding division by zero.
            if (maxRecentAmplitude > 0.001f)
            {
                dynamicScaleFactor = maxBarHeight / Mathf.Log(maxRecentAmplitude + logCompressionOffset, 10f + logCompressionStrength);
            }
        }
        else
        {
            dynamicScaleFactor = 1f;
        }

        // Update each bar.
        EnsureBarVisualArrays();

        for (int i = 0; i < barCount; i++)
        {
            float finalHeight = minBarHeight;
            float rawAmplitude = 0f;

            if (!wasSilent)
            {
                // Logarithmic frequency mapping gives the low-frequency range more space.
                float logIndex = Mathf.Pow((float)(i + 1) / barCount, 1);
                int fftIndex = Mathf.Clamp((int)(logIndex * (spectrumData.Length - 1)), 0, spectrumData.Length - 1);

                rawAmplitude = spectrumData[fftIndex];

                // Compress dynamic range with log(rawAmplitude + offset).
                float compressedHeight = Mathf.Log(rawAmplitude + logCompressionOffset, 10f + logCompressionStrength);

                // Apply dynamic scaling.
                float scaledHeight = compressedHeight * dynamicScaleFactor;

                // Keep bars within the configured visible height range.
                finalHeight = Mathf.Clamp(scaledHeight, minBarHeight, maxBarHeight);
            }

            // Update bar scale and emission.
            if (bars[i] != null)
            {
                Vector3 newScale = bars[i].transform.localScale;
                newScale.y = finalHeight;
                bars[i].transform.localScale = newScale;

                UpdateBarGlow(i, rawAmplitude, finalHeight);
            }
        }
    }

    private void EnsureBarVisualArrays()
    {
        if (bars == null || bars.Length != barCount)
        {
            return;
        }

        if (barRenderers == null || barRenderers.Length != barCount)
        {
            barRenderers = new Renderer[barCount];
        }

        if (barGlowLevels == null || barGlowLevels.Length != barCount)
        {
            barGlowLevels = new float[barCount];
        }

        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] != null && barRenderers[i] == null)
            {
                CacheBarRenderer(i, bars[i]);
            }
        }
    }

    private void CacheBarRenderer(int index, GameObject bar)
    {
        if (bar == null)
        {
            return;
        }

        EnsureBarGlowMaterial();

        Renderer renderer = bar.GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            return;
        }

        if (barRenderers != null && index >= 0 && index < barRenderers.Length)
        {
            barRenderers[index] = renderer;
        }

        if (enableBarGlow && barGlowMaterial != null)
        {
            renderer.sharedMaterial = barGlowMaterial;
        }
    }

    private void EnsureBarGlowMaterial()
    {
        if (barGlowMaterial != null)
        {
            return;
        }

        if (runtimeBarGlowMaterial != null)
        {
            barGlowMaterial = runtimeBarGlowMaterial;
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return;
        }

        runtimeBarGlowMaterial = new Material(shader);
        runtimeBarGlowMaterial.name = "Runtime Audio Bar Glow";
        runtimeBarGlowMaterial.EnableKeyword("_EMISSION");
        runtimeBarGlowMaterial.SetColor("_BaseColor", Color.cyan);
        runtimeBarGlowMaterial.SetColor("_EmissionColor", Color.cyan * baseBarEmissionIntensity);
        barGlowMaterial = runtimeBarGlowMaterial;
    }

    private void UpdateBarGlow(int index, float rawAmplitude, float finalHeight)
    {
        if (!enableBarGlow || barRenderers == null || index < 0 || index >= barRenderers.Length)
        {
            return;
        }

        Renderer renderer = barRenderers[index];
        if (renderer == null)
        {
            return;
        }

        if (barPropertyBlock == null)
        {
            barPropertyBlock = new MaterialPropertyBlock();
        }

        float normalizedHeight = Mathf.InverseLerp(minBarHeight, maxBarHeight, finalHeight);
        float bandEnergy = Mathf.Clamp01(rawAmplitude * audioBarEmissionIntensity);
        float targetGlow = wasSilent ? 0f : Mathf.Clamp01(Mathf.Max(normalizedHeight, bandEnergy));
        float smoothFactor = 1f - Mathf.Exp(-barGlowSmoothingSpeed * Time.deltaTime);
        barGlowLevels[index] = Mathf.Lerp(barGlowLevels[index], targetGlow, smoothFactor);

        float beatBoost = showBeatText ? beatBarEmissionBoost : 0f;
        float energyHueOffset = Mathf.Clamp01(kickEnergy * 30f) * kickHueInfluence
                              + Mathf.Clamp01(synthEnergy * 12f) * synthHueInfluence;
        float barOffset = barCount > 1 ? (float)index / (barCount - 1) : 0f;
        float hue = Mathf.Repeat(Time.time * barHueCycleSpeed + barOffset * barFrequencyHueSpread + energyHueOffset, 1f);
        float saturation = Mathf.Lerp(0.65f, 1f, barGlowLevels[index]);
        float value = Mathf.Lerp(0.35f, 1f, barGlowLevels[index]);
        Color color = Color.HSVToRGB(hue, saturation, value);

        float emissionIntensity = baseBarEmissionIntensity
                                + barGlowLevels[index] * audioBarEmissionIntensity
                                + beatBoost * barGlowLevels[index];

        renderer.GetPropertyBlock(barPropertyBlock);
        SetRendererColorProperty(barPropertyBlock, renderer, "_BaseColor", color);
        SetRendererColorProperty(barPropertyBlock, renderer, "_Color", color);
        SetRendererColorProperty(barPropertyBlock, renderer, "_EmissionColor", color * emissionIntensity);
        renderer.SetPropertyBlock(barPropertyBlock);
    }

    private void SetRendererColorProperty(MaterialPropertyBlock propertyBlock, Renderer renderer, string propertyName, Color color)
    {
        Material material = renderer.sharedMaterial;
        if (material == null || material.HasProperty(propertyName))
        {
            propertyBlock.SetColor(propertyName, color);
        }
    }

    /// <summary>
    /// Detects beats from multi-band onset energy.
    ///
    /// Fixes the root cause of the previous drift toward minBeatInterval.
    ///
    /// Root cause:
    ///   The earlier implementation stored deltaTime values in beatTimestamps and reset lastBeatTime
    ///   immediately after every trigger. A drum transient could retrigger repeatedly inside
    ///   minBeatInterval, filling the history with intervals near minBeatInterval and breaking filtering.
    ///
    /// Fix:
    ///   1. beatTimestamps stores absolute Time.time values.
    ///   2. UpdateBPM calculates adjacent timestamp deltas in one place.
    ///   3. beatCooldown ignores onsets inside the hard cooldown window.
    ///   4. OnKeyChanged no longer writes into beatTimestamps.
    /// </summary>
    private void DetectBeatImproved(float[] fft)
    {
        float time = Time.time;

        // Step 1: calculate multi-band energy.
        float snareEnergy = GetBandEnergy(fft, 150, 300);

        if (wasSilent)
        {
            previousKickEnergy = kickEnergy;
            previousSnareEnergy = snareEnergy;
            previousBassEnergy = bassEnergy;
            return;
        }

        // Step 2: maintain energy history.
        kickEnergyHistory.Enqueue(kickEnergy);
        snareEnergyHistory.Enqueue(snareEnergy);
        bassEnergyHistory.Enqueue(bassEnergy);

        if (kickEnergyHistory.Count > energyHistorySize) kickEnergyHistory.Dequeue();
        if (snareEnergyHistory.Count > energyHistorySize) snareEnergyHistory.Dequeue();
        if (bassEnergyHistory.Count > energyHistorySize) bassEnergyHistory.Dequeue();

        if (kickEnergyHistory.Count < 10)
        {
            previousKickEnergy = kickEnergy;
            previousSnareEnergy = snareEnergy;
            previousBassEnergy = bassEnergy;
            return;
        }

        // Step 3: calculate adaptive thresholds.
        float kickMean = kickEnergyHistory.Average();
        float snareMean = snareEnergyHistory.Average();
        float bassMean = bassEnergyHistory.Count > 0 ? bassEnergyHistory.Average() : bassEnergy;
        float kickStdDev = CalculateStdDev(kickEnergyHistory.ToArray(), kickMean);
        float snareStdDev = CalculateStdDev(snareEnergyHistory.ToArray(), snareMean);
        float bassStdDev = bassEnergyHistory.Count > 0 ? CalculateStdDev(bassEnergyHistory.ToArray(), bassMean) : 0f;

        float rawKickThreshold = kickMean + onsetSensitivity * kickStdDev;
        float rawSnareThreshold = snareMean + onsetSensitivity * snareStdDev;

        // Smooth thresholds with Lerp; higher speeds respond faster.
        smoothedKickThreshold = Mathf.Lerp(smoothedKickThreshold, rawKickThreshold,
            Time.deltaTime * dynamicKickThresholdSpeed);
        smoothedSnareThreshold = Mathf.Lerp(smoothedSnareThreshold, rawSnareThreshold,
            Time.deltaTime * dynamicSnareThresholdSpeed);

        // Expose smoothed thresholds for UI and dependent systems.
        dynamicKickThreshold = smoothedKickThreshold;
        dynamicSnareThreshold = smoothedSnareThreshold;

        // Step 4: detect positive energy onsets.
        float kickOnset = Mathf.Max(0, kickEnergy - previousKickEnergy);
        float snareOnset = Mathf.Max(0, snareEnergy - previousSnareEnergy);
        float bassOnset = Mathf.Max(0, bassEnergy - previousBassEnergy);

        // Step 5: validate onset candidates.
        float kickOnsetFloor = Mathf.Max(kickStdDev * 0.45f, kickMean * 0.08f, 1e-6f);
        float snareOnsetFloor = Mathf.Max(snareStdDev * 0.45f, snareMean * 0.08f, 1e-6f);
        float bassOnsetFloor = Mathf.Max(bassStdDev * 0.35f, bassMean * 0.06f, 1e-6f);

        bool isKickBeat = kickEnergy > dynamicKickThreshold && kickOnset > kickOnsetFloor;
        bool isSnareBeat = snareEnergy > dynamicSnareThreshold && snareOnset > snareOnsetFloor;

        float kickConfidence = isKickBeat ? Mathf.Clamp01((kickEnergy - dynamicKickThreshold) / Mathf.Max(kickMean * 0.5f, 1e-6f)) : 0f;
        float snareConfidence = isSnareBeat ? Mathf.Clamp01((snareEnergy - dynamicSnareThreshold) / Mathf.Max(snareMean * 0.5f, 1e-6f)) : 0f;
        float bassConfidence = bassOnset > bassOnsetFloor
            ? Mathf.Clamp01(bassOnset / Mathf.Max(bassMean * 0.35f, 1e-6f)) * 0.45f
            : 0f;
        float totalConfidence = Mathf.Clamp01(Mathf.Max(kickConfidence, snareConfidence) + bassConfidence);

        // Step 6: boost confidence inside the predicted phase window.
        float timeSinceLast = time - lastBeatTime;

        if (predictedNextBeat > 0f)
        {
            float timeToPredicted = Mathf.Abs(time - predictedNextBeat);
            if (timeToPredicted < beatInterval * 0.15f)
                totalConfidence = Mathf.Clamp01(totalConfidence * 1.3f);
        }

        // Step 7: apply hard cooldown and confidence gating.
        bool cooldownPassed = timeSinceLast >= beatCooldown;
        bool isBeat = (isKickBeat || isSnareBeat)
                      && totalConfidence > minBeatConfidence
                      && cooldownPassed;

        // Step 8: optional lost-beat reset when the maximum interval is exceeded.
        //if (predictedNextBeat > 0f && timeSinceLast > maxBeatInterval)
        //{
        //    Debug.Log("[Beat] Lost beat; resetting detection.");
        //    ResetBeatDetection();
        //}

        // Step 9: record the absolute timestamp and trigger beat display.
        if (isBeat)
        {
            float beatStrength = kickEnergy + snareEnergy;

            // Store the absolute timestamp.
            beatTimestamps.Add(time);
            beatConfidences.Add(totalConfidence);
            beatStrengths.Add(beatStrength);

            // Limit the history window size.
            int maxSize = 16;
            if (beatTimestamps.Count > maxSize)
            {
                beatTimestamps.RemoveAt(0);
                beatConfidences.RemoveAt(0);
                beatStrengths.RemoveAt(0);
            }

            lastBeatTime = time;

            // Trigger the transient beat display.
            showBeatText = true;
            beatTimer = beatDisplayTime;

            // Optionally update key detection on the beat.
            if (limitedBPM > 0)
            {
                DetectKeyFromFft(AudioCaptureCSCore.instance.frequencyData);
            }

            Debug.Log($"[Beat] Triggered beat - confidence: {totalConfidence:F2}, Kick: {kickEnergy:F3}, Snare: {snareEnergy:F3}");
        }

        // Step 10: periodically update BPM.
        if (time - lastBpmUpdateTime > bpmUpdateInterval && beatTimestamps.Count >= 4)
        {
            UpdateBPM();
            lastBpmUpdateTime = time;
        }

        // Step 11: predict the next beat.
        if (limitedBPM > 0f)
        {
            predictedNextBeat = lastBeatTime + beatInterval;
            if (isBeat) phaseError = time - predictedNextBeat;
        }

        // Update previous-frame energy values.
        previousKickEnergy = kickEnergy;
        previousSnareEnergy = snareEnergy;
        previousBassEnergy = bassEnergy;

        // Manual tap helper for debugging.
        if (Input.GetMouseButtonDown(0) && timeSinceLast > minBeatInterval)
        {
            beatTimestamps.Add(time);
            beatConfidences.Add(1.0f);
            beatStrengths.Add(kickEnergy + snareEnergy);
            lastBeatTime = time;
            Debug.Log($"[Beat] Manual tap @ {time:F2}s");
        }

    }

    /// <summary>
    /// Updates BPM using robust candidate scoring.
    /// 
    /// Candidate scoring uses average, median, individual intervals, and paired timestamps.
    /// It allows half-time, double-time, and missed beats without forcing every tempo into 90-150 BPM.
    /// </summary>
    private void UpdateBPM()
    {
        if (beatTimestamps.Count < 4)
            return;

        // Step 1: convert absolute timestamps into adjacent deltas.
        List<float> intervals = new List<float>();
        List<float> intConfidences = new List<float>();

        for (int i = 1; i < beatTimestamps.Count; i++)
        {
            float gap = beatTimestamps[i] - beatTimestamps[i - 1];
            if (gap >= minBeatInterval && gap <= maxBeatInterval)
            {
                intervals.Add(gap);
                intConfidences.Add((beatConfidences[i - 1] + beatConfidences[i]) * 0.5f);
            }
        }

        if (intervals.Count < 3)
        {
            Debug.Log("[BPM] Fewer than three valid intervals; waiting for more beats.");
            return;
        }

        float[] intervalArray = intervals.ToArray();

        // Step 2: build BPM candidates from multiple sources.
        float avgInterval = intervals.Average();
        float medianInterval = GetMedian(intervalArray);
        List<float> bpmCandidates = BuildBpmCandidates(intervals, intConfidences, avgInterval, medianInterval);

        // Step 3: select the best BPM by historical interval consistency.
        float bestBPM = SelectBestBPM(bpmCandidates, intervals, intConfidences);
        bestBPM = ConstrainBpmEstimate(bestBPM);

        // Step 4: smooth with an exponential moving average.
        float stability = 1f - Mathf.Clamp01(CalculateRelativeTempoError(bestBPM, intervals));
        float emaAlpha = Mathf.Lerp(0.18f, 0.42f, stability);
        if (kalmanEstimate == 0f)
        {
            kalmanEstimate = bestBPM;
        }
        else
        {
            kalmanEstimate = kalmanEstimate * (1 - emaAlpha) + bestBPM * emaAlpha;
        }
        kalmanEstimate = Mathf.Clamp(kalmanEstimate, GetMinTrackedBpm(), GetMaxTrackedBpm());

        detectedBPM = kalmanEstimate;

        // Step 5: apply half-time or double-time correction.
        LimitBPM();

        // Step 6: adjust confidence threshold from stability.
        bpmVariance = CalculateStdDev(intervalArray, avgInterval);
        minBeatConfidence = bpmVariance < 0.08f
            ? Mathf.Max(0.25f, minBeatConfidence - 0.02f)
            : 0.4f;

        Debug.Log($"[BPM] Updated: {Mathf.RoundToInt(limitedBPM)} BPM " +
                  $"(raw: {bestBPM:F1}, smoothed: {kalmanEstimate:F1}, variance: {bpmVariance:F3})");
    }

    /// <summary>
    /// Finds the best period using autocorrelation analysis.
    /// </summary>
    private float FindBestPeriod(float[] intervals)
    {
        if (intervals.Length < 4) return 0f;

        float mean = intervals.Average();
        float bestCorrelation = 0f;
        float bestPeriod = mean;

        // Test autocorrelation at different lags.
        for (int lag = 1; lag < Mathf.Min(5, intervals.Length - 1); lag++)
        {
            float correlation = 0f;
            int count = 0;

            for (int i = 0; i < intervals.Length - lag; i++)
            {
                // Accumulate interval sums for the current lag.
                float sumLag = 0f;
                for (int j = 0; j < lag; j++)
                {
                    sumLag += intervals[i + j];
                }

                // Calculate deviation from the mean period.
                float deviation = Mathf.Abs(sumLag - mean * lag);
                float score = 1f / (1f + deviation);

                correlation += score;
                count++;
            }

            if (count > 0)
            {
                correlation /= count;
                if (correlation > bestCorrelation)
                {
                    bestCorrelation = correlation;
                    bestPeriod = mean * lag;
                }
            }
        }

        return bestCorrelation > 0.7f ? bestPeriod : 0f;
    }

    private List<float> BuildBpmCandidates(List<float> intervals, List<float> confidences, float avgInterval, float medianInterval)
    {
        List<float> candidates = new List<float>();

        AddTempoCandidate(candidates, 60f / avgInterval);
        AddTempoCandidate(candidates, 60f / medianInterval);

        for (int i = 0; i < intervals.Count; i++)
        {
            float confidence = i < confidences.Count ? confidences[i] : 1f;
            if (confidence < 0.2f) continue;

            AddTempoCandidate(candidates, 60f / intervals[i]);
        }

        // Pairwise timestamps let the tracker recover when weak beats are missed.
        for (int i = 0; i < beatTimestamps.Count - 1; i++)
        {
            for (int j = i + 1; j < beatTimestamps.Count; j++)
            {
                float span = beatTimestamps[j] - beatTimestamps[i];
                int beatSteps = j - i;
                if (span <= 0f || beatSteps <= 0) continue;

                AddTempoCandidate(candidates, 60f * beatSteps / span);

                // Only add a doubled candidate for long spans, where it likely represents a missed weak beat.
                if (span > medianInterval * 1.45f)
                {
                    AddTempoCandidate(candidates, 120f * beatSteps / span);
                }
            }
        }

        if (kalmanEstimate > 0f) AddTempoCandidate(candidates, kalmanEstimate);
        if (limitedBPM > 0f) AddTempoCandidate(candidates, limitedBPM);

        return candidates;
    }

    private void AddTempoCandidate(List<float> candidates, float bpm)
    {
        if (float.IsNaN(bpm) || float.IsInfinity(bpm) || bpm <= 0f)
            return;

        bpm = NormalizeBpmToTrackingRange(bpm);

        for (int i = 0; i < candidates.Count; i++)
        {
            if (Mathf.Abs(candidates[i] - bpm) < 0.75f)
                return;
        }

        candidates.Add(bpm);
    }

    private float NormalizeBpmToTrackingRange(float bpm)
    {
        float minBpm = GetMinTrackedBpm();
        float maxBpm = GetMaxTrackedBpm();

        while (bpm < minBpm && bpm * 2f <= maxBpm)
            bpm *= 2f;

        while (bpm > maxBpm && bpm * 0.5f >= minBpm)
            bpm *= 0.5f;

        return Mathf.Clamp(bpm, minBpm, maxBpm);
    }

    private float ConstrainBpmEstimate(float bpm)
    {
        bpm = Mathf.Clamp(bpm, GetMinTrackedBpm(), GetMaxTrackedBpm());

        float referenceBpm = limitedBPM > 0f ? limitedBPM : kalmanEstimate;
        if (referenceBpm <= 0f)
            return bpm;

        float maxStep = Mathf.Max(4f, referenceBpm * maxBpmChangeRatio);
        return Mathf.Clamp(bpm, referenceBpm - maxStep, referenceBpm + maxStep);
    }

    private float GetMinTrackedBpm()
    {
        return Mathf.Clamp(minTrackedBPM, 40f, 240f);
    }

    private float GetMaxTrackedBpm()
    {
        return Mathf.Clamp(Mathf.Max(maxTrackedBPM, GetMinTrackedBpm() + 1f), 60f, 260f);
    }

    /// <summary>
    /// Selects the best BPM candidate using interval consistency with a soft bias toward common tempo ranges.
    /// </summary>
    private float SelectBestBPM(List<float> candidates, List<float> intervals, List<float> confidences)
    {
        if (candidates.Count == 0) return 60f / intervals.Average();

        float bestScore = float.MinValue;
        float bestBPM = candidates[0];

        foreach (float bpm in candidates)
        {
            if (bpm <= 0) continue;

            float score = 0f;

            // Common dance tempo ranges are only a soft bias; do not force 70/160 BPM material into the middle range.
            score += GetTempoPlausibilityScore(bpm);

            // Reward stability relative to historical BPM.
            if (kalmanEstimate > 0)
            {
                float diff = Mathf.Abs(bpm - kalmanEstimate);
                score += 1.5f / (1f + diff * 0.08f);
            }

            // Reward consistency with interval data.
            float weightedMatch = 0f;
            float totalWeight = 0f;
            for (int i = 0; i < intervals.Count; i++)
            {
                float confidence = i < confidences.Count ? Mathf.Clamp01(confidences[i]) : 1f;
                float intervalScore = ScoreIntervalAgainstTempo(intervals[i], bpm);
                weightedMatch += intervalScore * Mathf.Lerp(0.5f, 1.5f, confidence);
                totalWeight += Mathf.Lerp(0.5f, 1.5f, confidence);
            }
            score += totalWeight > 0f ? weightedMatch / totalWeight * 4f : 0f;

            float relativeError = CalculateRelativeTempoError(bpm, intervals);
            score += 2f / (1f + relativeError * 10f);

            if (score > bestScore)
            {
                bestScore = score;
                bestBPM = bpm;
            }
        }

        return bestBPM;
    }

    private float ScoreIntervalAgainstTempo(float interval, float bpm)
    {
        if (interval <= 0f || bpm <= 0f) return 0f;

        float beatPeriod = 60f / bpm;
        float bestScore = 0f;
        float[] multiples = { 1f, 1.5f, 2f, 3f, 4f };

        for (int i = 0; i < multiples.Length; i++)
        {
            float expected = beatPeriod * multiples[i];
            float tolerance = Mathf.Max(0.035f, expected * 0.16f);
            float error = Mathf.Abs(interval - expected) / tolerance;
            bestScore = Mathf.Max(bestScore, 1f / (1f + error * error));
        }

        return bestScore;
    }

    private float CalculateRelativeTempoError(float bpm, List<float> intervals)
    {
        if (bpm <= 0f || intervals.Count == 0) return 1f;

        float error = 0f;
        for (int i = 0; i < intervals.Count; i++)
        {
            error += 1f - ScoreIntervalAgainstTempo(intervals[i], bpm);
        }

        return error / intervals.Count;
    }

    private float GetTempoPlausibilityScore(float bpm)
    {
        if (bpm >= 90f && bpm <= 150f) return 1.2f;
        if (bpm >= 72f && bpm <= 180f) return 0.8f;
        if (bpm >= GetMinTrackedBpm() && bpm <= GetMaxTrackedBpm()) return 0.35f;
        return -1f;
    }

    /// <summary>
    /// Constrains BPM to a musically useful range.
    /// </summary>
    private void LimitBPM()
    {
        if (detectedBPM <= 0) return;

        limitedBPM = detectedBPM;

        // Prefer the common 90-150 BPM range when half-time or double-time correction is plausible.

        // If the estimate is too low, try double-time correction.
        while (limitedBPM < 90 && limitedBPM * 2 <= 200)
        {
            limitedBPM *= 2;
        }

        // If the estimate is too high, try half-time correction.
        while (limitedBPM > 150 && limitedBPM / 2 >= 60)
        {
            limitedBPM /= 2;
        }

        // Apply final hard limits.
        limitedBPM = Mathf.Clamp(limitedBPM, GetMinTrackedBpm(), GetMaxTrackedBpm());

        // Use the corrected limitedBPM for the beat interval so prediction stays aligned after tempo correction.
        beatInterval = 60f / Mathf.Max(limitedBPM, 1f);
        beatDisplayTime = beatInterval / 4f;

        Debug.Log($"[BPM] After tempo correction: {Mathf.RoundToInt(limitedBPM)} BPM");
    }

    /// <summary>
    /// Resets beat detection state.
    /// </summary>
    private void ResetBeatDetection()
    {
        kickEnergyHistory.Clear();
        snareEnergyHistory.Clear();
        bassEnergyHistory.Clear();
        beatTimestamps.Clear();
        beatConfidences.Clear();
        beatStrengths.Clear();
        detectedBPM = 0f;
        limitedBPM = 0f;
        kalmanEstimate = 0f;
        predictedNextBeat = 0f;
        previousBassEnergy = 0f;
        hasSmoothedChroma = false;
        pendingKey = "Unknown";
        pendingMode = "Unknown";
        pendingKeyFrames = 0;
        currentKeyConfidence = 0f;

        // Reset silence state.
        wasSilent = false;
        silenceStartTime = -1f;
        minBeatConfidence = 0.3f;

        Debug.Log("[Beat] Reset beat detection.");
    }

    /// <summary>
    /// Calculates standard deviation.
    /// </summary>
    private float CalculateStdDev(float[] values, float mean)
    {
        if (values.Length == 0)
            return 0f;

        float sumSquaredDiff = 0f;
        foreach (float value in values)
        {
            float diff = value - mean;
            sumSquaredDiff += diff * diff;
        }

        return Mathf.Sqrt(sumSquaredDiff / values.Length);
    }

    /// <summary>
    /// Calculates the median value.
    /// </summary>
    private float GetMedian(float[] values)
    {
        if (values.Length == 0)
            return 0f;

        float[] sorted = values.OrderBy(x => x).ToArray();
        int mid = sorted.Length / 2;

        if (sorted.Length % 2 == 0)
        {
            return (sorted[mid - 1] + sorted[mid]) / 2f;
        }
        else
        {
            return sorted[mid];
        }
    }

    /// <summary>
    /// Detects silence and progressively clears transient beat state.
    /// Requires sustained low energy before entering silence, preserves useful history during silence,
    /// and distinguishes full silence from low-energy playback.
    /// </summary>
    private void CheckAndHandleSilence(float[] spectrumData, float currentTime)
    {
        float broadbandEnergy = GetBandEnergy(spectrumData, 40, 8000);
        float weightedEnergy = Mathf.Max(
            broadbandEnergy,
            kickEnergy * 0.6f + bassEnergy * 0.4f + synthEnergy * 0.4f);

        smoothedSilenceEnergy = Mathf.Lerp(
            smoothedSilenceEnergy,
            weightedEnergy,
            1f - Mathf.Exp(-silenceEnergySmoothSpeed * Time.deltaTime));

        bool hasSoundNow = weightedEnergy >= silenceExitThreshold;
        bool isSilentNow = weightedEnergy <= silenceEnterThreshold;

        if (wasSilent)
        {
            pendingSilenceStartTime = -1f;

            if (hasSoundNow)
            {
                if (pendingSoundStartTime < 0f)
                    pendingSoundStartTime = currentTime;

                if (currentTime - pendingSoundStartTime >= silenceExitDelay)
                {
                    float silenceDurationTotal = silenceStartTime > 0f
                        ? currentTime - silenceStartTime
                        : 0f;

                    minBeatConfidence = silenceDurationTotal > 1f ? 0.2f : 0.3f;
                    playStartTime = currentTime;
                    wasSilent = false;
                    pendingSoundStartTime = -1f;

                    Debug.Log($"[Silence] Audio resumed after {silenceDurationTotal:F2}s of silence. Energy: {smoothedSilenceEnergy:F5}");
                }
            }
            else
            {
                pendingSoundStartTime = -1f;
            }

            return;
        }

        pendingSoundStartTime = -1f;

        if (isSilentNow)
        {
            if (pendingSilenceStartTime < 0f)
                pendingSilenceStartTime = currentTime;

            if (currentTime - pendingSilenceStartTime >= silenceEnterDelay)
            {
                ResetBeatDetection();
                silenceStartTime = currentTime;
                wasSilent = true;
                pendingSilenceStartTime = -1f;
                showBeatText = false;

                Debug.Log($"[Silence] Silence detected at {currentTime:F2}. Energy: {smoothedSilenceEnergy:F5}");
            }
        }
        else
        {
            pendingSilenceStartTime = -1f;

            if (smoothedSilenceEnergy < silenceExitThreshold * lowEnergyThresholdMultiplier)
            {
                minBeatConfidence = Mathf.Max(0.2f, minBeatConfidence - 0.01f * Time.deltaTime);
            }
            else
            {
                minBeatConfidence = Mathf.Lerp(minBeatConfidence, 0.3f, Time.deltaTime * 2f);
            }
        }
    }

    /// <summary>
    /// Detects musical key using a stabilized chroma analysis pipeline.
    ///
    /// Maintains real-time response without allowing a single FFT frame to overwrite the displayed key.
    ///
    /// Pipeline:
    ///   1. Map the FFT spectrum to 12 chroma bins.
    ///   2. Smooth and L2-normalize chroma to reduce transient overtone and noise influence.
    ///   3. Compare all 24 major/minor keys against Krumhansl-Schmuckler templates.
    ///   4. Require repeated confirmation and a score margin before switching keys.
    /// </summary>
    private void DetectKeyFromFft(float[] fft)
    {
        try
        {
            double[] chroma = ExtractChromaFeatures(fft);
            chroma = SmoothChroma(chroma);
            chroma = NormalizeChroma(chroma);
            lastKeyUpdateTime = Time.time;

            // Evaluate all 24 major and minor keys and choose the highest Pearson correlation.
            double bestScore = double.MinValue;
            double secondBestScore = double.MinValue;
            string bestKey = "C";
            string bestMode = "Major";

            for (int shift = 0; shift < 12; shift++)
            {
                double scoreMajor = PearsonCorr(chroma, majorProfile, shift);
                TrackKeyCandidate(scoreMajor, keyNames[shift], "Major",
                    ref bestScore, ref secondBestScore, ref bestKey, ref bestMode);

                double scoreMinor = PearsonCorr(chroma, minorProfile, shift);
                TrackKeyCandidate(scoreMinor, keyNames[shift], "Minor",
                    ref bestScore, ref secondBestScore, ref bestKey, ref bestMode);
            }

            currentKeyConfidence = Mathf.Clamp01((float)(bestScore - secondBestScore));

            if (ShouldAcceptKeyCandidate(bestKey, bestMode, bestScore, chroma))
            {
                currentKey = bestKey;
                currentMode = bestMode;
                pendingKey = "Unknown";
                pendingMode = "Unknown";
                pendingKeyFrames = 0;
                OnKeyChanged();
                Debug.Log($"[Key] {currentKey} {currentMode} (confidence {currentKeyConfidence:F2})");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KeyDetection] {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Maps the FFT spectrum into 12 chroma bins using sqrt(amplitude) weighting and Gaussian semitone spreading.
    /// </summary>
    private double[] ExtractChromaFeatures(float[] fft)
    {
        double[] chroma = new double[12];
        int sampleRate = AudioCaptureCSCore.instance.waveSource.WaveFormat.SampleRate;
        double freqRes = (double)sampleRate / fft.Length;

        int minBin = Mathf.Max(1, (int)(80.0 / freqRes));
        int maxBin = Mathf.Min(fft.Length - 1, (int)(4000.0 / freqRes));

        for (int i = minBin; i <= maxBin; i++)
        {
            if (fft[i] <= 0f) continue;

            double freq = i * freqRes;
            double midiNote = 12.0 * Math.Log(freq / 440.0, 2.0) + 69.0;
            int lowerNote = (int)Math.Floor(midiNote);
            int noteClass = (lowerNote % 12 + 12) % 12;
            double weight = Math.Sqrt(fft[i]) * GetChromaFrequencyWeight(freq);

            // Spread energy into neighboring semitones to reduce frequency quantization error.
            double frac = midiNote - lowerNote;
            chroma[noteClass] += weight * Math.Exp(-0.5 * frac * frac);
            chroma[(noteClass + 1) % 12] += weight * Math.Exp(-0.5 * (1.0 - frac) * (1.0 - frac));
        }

        return chroma;
    }

    private double GetChromaFrequencyWeight(double freq)
    {
        if (freq < 120.0) return 0.65;
        if (freq < 1000.0) return 1.25;
        if (freq < 2500.0) return 1.0;
        return 0.75;
    }

    private double[] SmoothChroma(double[] chroma)
    {
        double smoothing = Mathf.Clamp01(keyChromaSmoothing);

        if (!hasSmoothedChroma)
        {
            for (int i = 0; i < 12; i++)
                smoothedChroma[i] = chroma[i];
            hasSmoothedChroma = true;
        }
        else
        {
            for (int i = 0; i < 12; i++)
                smoothedChroma[i] = smoothedChroma[i] * smoothing + chroma[i] * (1.0 - smoothing);
        }

        double[] result = new double[12];
        for (int i = 0; i < 12; i++)
            result[i] = smoothedChroma[i];
        return result;
    }

    private void TrackKeyCandidate(
        double score,
        string key,
        string mode,
        ref double bestScore,
        ref double secondBestScore,
        ref string bestKey,
        ref string bestMode)
    {
        if (score > bestScore)
        {
            secondBestScore = bestScore;
            bestScore = score;
            bestKey = key;
            bestMode = mode;
        }
        else if (score > secondBestScore)
        {
            secondBestScore = score;
        }
    }

    private bool ShouldAcceptKeyCandidate(string bestKey, string bestMode, double bestScore, double[] chroma)
    {
        if (currentKey == "Unknown" || currentMode == "Unknown")
            return true;

        if (bestKey == currentKey && bestMode == currentMode)
        {
            pendingKey = "Unknown";
            pendingMode = "Unknown";
            pendingKeyFrames = 0;
            return false;
        }

        double currentScore = GetKeyScore(chroma, currentKey, currentMode);
        if (bestScore < currentScore + keySwitchMargin)
            return false;

        if (bestKey == pendingKey && bestMode == pendingMode)
        {
            pendingKeyFrames++;
        }
        else
        {
            pendingKey = bestKey;
            pendingMode = bestMode;
            pendingKeyFrames = 1;
        }

        return pendingKeyFrames >= Mathf.Max(1, keyStableFrameThreshold);
    }

    private double GetKeyScore(double[] chroma, string key, string mode)
    {
        int keyIndex = Array.IndexOf(keyNames, key);
        if (keyIndex < 0) return double.MinValue;

        return mode == "Minor"
            ? PearsonCorr(chroma, minorProfile, keyIndex)
            : PearsonCorr(chroma, majorProfile, keyIndex);
    }

    /// <summary>L2-normalizes chroma to remove loudness differences.</summary>
    private double[] NormalizeChroma(double[] chroma)
    {
        double sq = 0;
        for (int i = 0; i < 12; i++) sq += chroma[i] * chroma[i];
        double norm = Math.Sqrt(sq);
        if (norm < 1e-10) return chroma;
        double[] n = new double[12];
        for (int i = 0; i < 12; i++) n[i] = chroma[i] / norm;
        return n;
    }

    /// <summary>Calculates Pearson correlation between chroma and a shifted key template.</summary>
    private double PearsonCorr(double[] chroma, double[] template, int shift)
    {
        double cMean = 0, tMean = 0;
        for (int i = 0; i < 12; i++)
        {
            cMean += chroma[i];
            tMean += template[(i + shift) % 12];
        }
        cMean /= 12.0;
        tMean /= 12.0;

        double num = 0, cVar = 0, tVar = 0;
        for (int i = 0; i < 12; i++)
        {
            double cd = chroma[i] - cMean;
            double td = template[(i + shift) % 12] - tMean;
            num += cd * td;
            cVar += cd * cd;
            tVar += td * td;
        }

        double denom = Math.Sqrt(cVar * tVar);
        return denom < 1e-10 ? 0.0 : num / denom;
    }

    private void OnKeyChanged()
    {
        // Key changes do not write into beatTimestamps, preventing key updates from polluting beat intervals.
        Debug.Log("[Key] Key changed without modifying beat timing data.");
    }

    private float GetBandEnergy(float[] spectrum, float fMin, float fMax)
    {
        int sampleRate = AudioCaptureCSCore.instance.waveSource.WaveFormat.SampleRate;
        int imin = Mathf.FloorToInt(fMin * fftSize / sampleRate);
        int imax = Mathf.FloorToInt(fMax * fftSize / sampleRate);

        imin = Mathf.Clamp(imin, 0, spectrum.Length - 1);
        imax = Mathf.Clamp(imax, 0, spectrum.Length - 1);

        float sum = 0f;
        for (int i = imin; i <= imax; i++)
            sum += spectrum[i];

        return sum / (imax - imin + 1);
    }

    public void DrawStatusGui(int fontSize = 18)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        style.wordWrap = false;
        style.normal.textColor = Color.green;

        GUILayout.Label($"BPM: {limitedBPM:F1}", style);
        GUILayout.Label($"Key: {currentKey} {currentMode}", style);

        if (showBeatText)
        {
            GUILayout.Label("** BEAT **", style);
        }

        GUILayout.Label($"Kick: {kickEnergy:F3} (T: {dynamicKickThreshold:F3})", style);
        GUILayout.Label($"Confidence: {(beatConfidences.Count > 0 ? beatConfidences.Last() : 0):F2}", style);
        GUILayout.Label($"Variance: {bpmVariance:F3}", style);

        // Display silence state.
        style.normal.textColor = Color.yellow;
        GUILayout.Label(GetPlaybackStatusText(), style);
        GUILayout.Label($"Silence Energy: {smoothedSilenceEnergy:F5}", style);
        style.normal.textColor = Color.green;

        // Display log compression debug values.
        if (enableDynamicRange)
        {
            GUILayout.Label($"Dynamic Scale: {dynamicScaleFactor:F2} | Max Amplitude: {maxRecentAmplitude:F3}", style);
        }

        GUILayout.Label($"Raw BPM: {detectedBPM:F1} | Variance: {bpmVariance:F3}", style);
    }

    private string GetPlaybackStatusText()
    {
        if (wasSilent)
        {
            float startTime = silenceStartTime >= 0f ? silenceStartTime : Time.time;
            return $"Silent {FormatDuration(Time.time - startTime)}";
        }

        float playTime = playStartTime > 0f ? playStartTime : Time.time;
        return $"Playing {FormatDuration(Time.time - playTime)}";
    }

    private string FormatDuration(float duration)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(duration));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    void OnGUI()
    {
        AudioCaptureCSCore capture = AudioCaptureCSCore.instance;
        bool mergedPanelVisible = capture != null && capture.showManualControlPanel;
        if (!showStandaloneStatusOverlay || mergedPanelVisible)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(Screen.width - 420, 20, 400, 360));
        DrawStatusGui(24);
        GUILayout.EndArea();
    }
}
