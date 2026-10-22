using UnityEngine;

/// <summary>
/// Main entry for the spatial audio composition. LateUpdate reads the existing audio analysis once,
/// normalizes the spectrum and directly drives the three mesh effects. Demo input is opt-in and
/// clearly serialized; OnEnable snapshots onset state so re-enabling never replays an old hit.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(120)]
public sealed class AudioVisualField : MonoBehaviour
{
    public enum Composition { Balanced, Aurora, Spectrum, Pulse }

    [Header("Sources and Theme")]
    [SerializeField] private AudioVisualizer audioVisualizer;
    [SerializeField] private RectTransform readingScreen;
    [SerializeField] private AudioVisualTheme theme;
    [SerializeField] private AudioAuroraRibbons ribbons;
    [SerializeField] private AudioBeatRings rings;
    [SerializeField] private AudioSpectrumTerrain terrain;
    [SerializeField] private AudioHarmonicCanopy canopy;
    [SerializeField] private StageManager stage;

    [Tooltip("Follow stage blackout and beat phase without changing the stage or its palette.")]
    [SerializeField] private bool followStage = true;

    [Header("Composition")]
    [SerializeField] private Composition composition;
    [SerializeField, Range(0f, 1f)] private float intensity = 0.75f;
    [SerializeField] private bool evolveOverPhrase = true;
    [SerializeField, Min(0.1f)] private float transitionSeconds = 2f;
    [SerializeField, Min(0.1f)] private float minimumOnsetInterval = 0.2f;
    [SerializeField, Range(0f, 1f)] private float minimumOnsetConfidence = 0.25f;

    [Header("Explicit Demonstration Input")]
    [Tooltip("Only enable in the visual laboratory. It does not analyze or represent real audio.")]
    [SerializeField] private bool demonstration;
    [SerializeField, Range(40f, 200f)] private float demonstrationBpm = 112f;
    [SerializeField, Range(0f, 1f)] private float demonstrationEnergy = 0.65f;

    private readonly float[] spectrum = new float[48];
    private readonly AudioSpectralShape spectralShape = new AudioSpectralShape(48);
    private Vector4 smoothedShape;
    private uint lastObservedOnset;
    private float lastAcceptedOnset = float.NegativeInfinity;
    private float bass;
    private float detail;
    private float energy;
    private float visualTime;
    private float previousDemoBeat = -1f;
    private AudioVisualTheme displayedTheme;
    public Frame CurrentFrame { get; private set; }
    public bool UsesDemonstration => demonstration;
    public AudioVisualTheme Theme => theme;
    public AudioVisualTheme DisplayedTheme => displayedTheme != null ? displayedTheme : theme;
    public AudioBeatRings Rings => rings;
    public AudioSpectrumTerrain Terrain => terrain;

    public struct Frame
    {
        public float time, deltaTime, bass, detail, energy, beatPhase, onsetStrength;
        public float ribbonWeight, ringWeight, terrainWeight;
        public bool onset, hasScreen;
        public Matrix4x4 screenWorldToLocal;
        public Vector4 screenRect;
        public Vector4 spectralShape;
    }

    public float GetSpectrumBand(int index)
    {
        return spectrum[Mathf.Clamp(index, 0, spectrum.Length - 1)];
    }

    public void Configure(AudioVisualizer source, RectTransform screen, AudioVisualTheme visualTheme,
        AudioAuroraRibbons aurora, AudioBeatRings beatRings, AudioSpectrumTerrain spectrumTerrain, bool demo)
    {
        audioVisualizer = source;
        readingScreen = screen;
        theme = visualTheme;
        ribbons = aurora;
        rings = beatRings;
        terrain = spectrumTerrain;
        demonstration = demo;
        SnapshotOnset();
    }

    public void SetTheme(AudioVisualTheme value)
    {
        if (value != null)
        {
            theme = value;
        }
    }

    public void SetComposition(Composition value)
    {
        composition = value;
    }

    public void SetIntensity(float value)
    {
        intensity = Mathf.Clamp01(value);
    }

    private void OnEnable()
    {
        SnapshotOnset();
        previousDemoBeat = -1f;
        SetRenderersVisible(true);
    }

    private void SnapshotOnset()
    {
        lastObservedOnset = audioVisualizer != null ? audioVisualizer.VisualOnsetSequence : 0;
        lastAcceptedOnset = float.NegativeInfinity;
    }

    private void LateUpdate()
    {
        if (theme == null)
        {
            return;
        }
        float delta = Mathf.Clamp(Time.deltaTime, 0f, 0.1f);
        visualTime += delta;
        Frame frame = ReadAudio(delta);
        RenderFrame(frame);
    }

    private Frame ReadAudio(float delta)
    {
        float targetBass = 0f;
        float targetDetail = 0f;
        float bpm = 120f;
        bool onset = false;
        float strength = 0f;
        bool silent = true;
        if (demonstration)
        {
            bpm = demonstrationBpm;
            float beat = Mathf.Floor(visualTime * bpm / 60f);
            onset = beat != previousDemoBeat;
            previousDemoBeat = beat;
            float pulse = Mathf.Exp(-Mathf.Repeat(visualTime * bpm / 60f, 1f) * 6f);
            targetBass = demonstrationEnergy * (0.45f + pulse * 0.55f);
            targetDetail = demonstrationEnergy * (0.65f + Mathf.Sin(visualTime * 0.7f) * 0.25f);
            strength = 0.7f + pulse * 0.3f;
            silent = false;
        }
        else if (audioVisualizer != null && audioVisualizer.isActiveAndEnabled)
        {
            silent = audioVisualizer.wasSilent;
            bpm = audioVisualizer.limitedBPM > 0f ? audioVisualizer.limitedBPM : 120f;
            if (!silent)
            {
                targetBass = Compress(audioVisualizer.smoothedBassEnergy, theme.bassGain);
                targetDetail = Compress(audioVisualizer.smoothedSynthEnergy, theme.detailGain);
                uint sequence = audioVisualizer.VisualOnsetSequence;
                bool fresh = sequence != lastObservedOnset && Time.time - audioVisualizer.VisualOnsetTime < 0.12f;
                onset = fresh && audioVisualizer.VisualOnsetConfidence >= minimumOnsetConfidence
                    && visualTime - lastAcceptedOnset >= minimumOnsetInterval;
                strength = audioVisualizer.VisualOnsetStrength;
            }
            lastObservedOnset = audioVisualizer.VisualOnsetSequence;
        }
        if (onset)
        {
            lastAcceptedOnset = visualTime;
        }
        bass = Smooth(bass, targetBass, delta);
        detail = Smooth(detail, targetDetail, delta);
        energy = Smooth(energy, (targetBass + targetDetail) * 0.5f, delta);
        UpdateSpectrum(delta, silent);
        Vector4 shape = spectralShape.Analyze(spectrum, silent);
        smoothedShape = Vector4.Lerp(smoothedShape, shape, 1f - Mathf.Exp(-delta * 8f));
        float phase = visualTime * bpm / 60f;
        float visibility = 1f;
        if (!demonstration && followStage && stage != null && stage.isActiveAndEnabled)
        {
            StageManager.PresentationFrame presentation = stage.Presentation;
            phase = Mathf.Floor(phase) + presentation.beatPhase;
            visibility = Mathf.Clamp01(presentation.visibility);
        }
        float phrase = Mathf.Repeat(phase / Mathf.Max(4, theme.phraseBeats), 1f);
        float evolution = evolveOverPhrase ? 0.8f + Mathf.Sin(phrase * Mathf.PI * 2f) * 0.2f : 1f;
        float level = intensity * visibility * Mathf.Lerp(theme.idleLevel, 1f, Mathf.Clamp01(energy * 2f));
        Frame frame = new Frame
        {
            time = visualTime, deltaTime = delta, bass = bass, detail = detail,
            energy = energy, beatPhase = Mathf.Repeat(phase, 1f), onset = onset,
            onsetStrength = Mathf.Clamp01(strength),
            spectralShape = smoothedShape,
            ribbonWeight = theme.ribbons * level * evolution * (composition == Composition.Spectrum ? 0.2f : 1f),
            ringWeight = theme.rings * intensity * visibility * (composition == Composition.Aurora ? 0.35f : 1f),
            terrainWeight = theme.terrain * level * (composition == Composition.Aurora ? 0.15f : 1f)
        };
        if (composition == Composition.Pulse)
        {
            frame.ribbonWeight *= 0.25f;
            frame.terrainWeight *= 0.25f;
        }
        return frame;
    }

    public void RenderFrame(Frame frame)
    {
        if (theme == null)
        {
            return;
        }
        UpdateDisplayedTheme(frame.deltaTime);
        float blend = 1f - Mathf.Exp(-Mathf.Max(0f, frame.deltaTime) * 3f / Mathf.Max(0.1f, transitionSeconds));
        frame.ribbonWeight = Mathf.Lerp(CurrentFrame.ribbonWeight, frame.ribbonWeight, blend);
        frame.ringWeight = Mathf.Lerp(CurrentFrame.ringWeight, frame.ringWeight, blend);
        frame.terrainWeight = Mathf.Lerp(CurrentFrame.terrainWeight, frame.terrainWeight, blend);
        if (readingScreen != null)
        {
            Rect rect = readingScreen.rect;
            frame.hasScreen = true;
            frame.screenWorldToLocal = readingScreen.worldToLocalMatrix;
            frame.screenRect = new Vector4(rect.xMin - theme.screenPadding, rect.yMin - theme.screenPadding,
                rect.xMax + theme.screenPadding, rect.yMax + theme.screenPadding);
        }
        CurrentFrame = frame;
        if (ribbons != null && ribbons.isActiveAndEnabled)
        {
            ribbons.RenderFrame(frame, displayedTheme);
        }
        if (rings != null && rings.isActiveAndEnabled)
        {
            rings.RenderFrame(frame, displayedTheme);
        }
        if (terrain != null && terrain.isActiveAndEnabled)
        {
            terrain.RenderFrame(frame, displayedTheme, spectrum);
        }
        if (canopy != null && canopy.isActiveAndEnabled)
        {
            canopy.RenderFrame(frame, displayedTheme, this);
        }
    }

    private void UpdateDisplayedTheme(float deltaTime)
    {
        if (displayedTheme == null)
        {
            displayedTheme = Instantiate(theme);
            displayedTheme.name = "Spatial Audio Theme (Runtime Blend)";
            displayedTheme.hideFlags = HideFlags.HideAndDontSave;
        }
        float blend = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) * 3f / Mathf.Max(0.1f, transitionSeconds));
        // Blend a private copy; editing a theme or switching palettes never mutates an asset.
        displayedTheme.primary = Color.Lerp(displayedTheme.primary, theme.primary, blend);
        displayedTheme.secondary = Color.Lerp(displayedTheme.secondary, theme.secondary, blend);
        displayedTheme.accent = Color.Lerp(displayedTheme.accent, theme.accent, blend);
        displayedTheme.emission = Mathf.Lerp(displayedTheme.emission, theme.emission, blend);
        displayedTheme.opacity = Mathf.Lerp(displayedTheme.opacity, theme.opacity, blend);
        displayedTheme.motion = Mathf.Lerp(displayedTheme.motion, theme.motion, blend);
        displayedTheme.fiberDensity = Mathf.Lerp(displayedTheme.fiberDensity, theme.fiberDensity, blend);
        displayedTheme.turbulence = Mathf.Lerp(displayedTheme.turbulence, theme.turbulence, blend);
        displayedTheme.flowSpeed = Mathf.Lerp(displayedTheme.flowSpeed, theme.flowSpeed, blend);
        displayedTheme.sparkle = Mathf.Lerp(displayedTheme.sparkle, theme.sparkle, blend);
        displayedTheme.spectralInfluence = Mathf.Lerp(displayedTheme.spectralInfluence, theme.spectralInfluence, blend);
        displayedTheme.canopy = Mathf.Lerp(displayedTheme.canopy, theme.canopy, blend);
        displayedTheme.fadeStartDistance = theme.fadeStartDistance;
        displayedTheme.fullVisibilityDistance = theme.fullVisibilityDistance;
    }

    public void PreviewDemonstration(float time, float delta)
    {
        if (!demonstration || theme == null)
        {
            return;
        }
        visualTime = Mathf.Max(0f, time);
        RenderFrame(ReadAudio(Mathf.Clamp(delta, 0f, 0.1f)));
    }

    private void UpdateSpectrum(float delta, bool silent)
    {
        AudioCaptureCSCore capture = AudioCaptureCSCore.instance;
        float[] fft = !demonstration && capture != null ? capture.smoothedFftData : null;
        float sampleRate = capture != null && capture.waveSource != null
            ? capture.waveSource.WaveFormat.SampleRate : 48000f;
        for (int band = 0; band < spectrum.Length; band++)
        {
            float t = band / (float)(spectrum.Length - 1);
            float target = 0f;
            if (demonstration)
            {
                target = (0.2f + 0.5f * Mathf.Pow(Mathf.Sin(t * 11f - visualTime * 1.2f), 2f))
                    * demonstrationEnergy * (0.5f + 0.5f * (1f - t));
            }
            else if (!silent && fft != null && fft.Length >= 4 && sampleRate > 0f)
            {
                float frequency = 40f * Mathf.Pow(200f, t);
                // Integrate log intervals so peaks between display bands still contribute.
                float halfStep = Mathf.Pow(200f, 0.5f / (spectrum.Length - 1));
                int first = Mathf.Clamp(Mathf.FloorToInt(frequency / halfStep * fft.Length / sampleRate), 1, fft.Length / 2 - 1);
                int last = Mathf.Clamp(Mathf.CeilToInt(frequency * halfStep * fft.Length / sampleRate), first + 1, fft.Length / 2);
                float sum = 0f;
                for (int bin = first; bin < last; bin++)
                {
                    sum += float.IsFinite(fft[bin]) ? Mathf.Max(0f, fft[bin]) : 0f;
                }
                target = Compress(sum / (last - first), 18f);
            }
            spectrum[band] = Smooth(spectrum[band], target, delta);
        }
    }

    private float Smooth(float current, float target, float delta)
    {
        float response = target > current ? theme.attack : theme.release;
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0.1f, response) * delta));
    }

    private static float Compress(float value, float gain)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0f, value) * Mathf.Max(0f, gain));
    }

    private void OnDisable()
    {
        SetRenderersVisible(false);
        CurrentFrame = default;
        bass = detail = energy = 0f;
        smoothedShape = Vector4.zero;
        spectralShape.Reset();
        System.Array.Clear(spectrum, 0, spectrum.Length);
        if (rings != null)
        {
            rings.Clear();
        }
        if (terrain != null)
        {
            terrain.Clear();
        }
    }

    private void OnDestroy()
    {
        ReleasePreviewResources();
    }

    // Editor preview objects may never receive OnDestroy; the preview owner calls this explicitly.
    public void ReleasePreviewResources()
    {
        if (ribbons != null)
        {
            ribbons.ReleaseMesh();
        }
        if (rings != null)
        {
            rings.ReleaseMesh();
        }
        if (terrain != null)
        {
            terrain.ReleaseMesh();
        }
        if (canopy != null)
        {
            canopy.ReleaseMesh();
        }
        if (displayedTheme == null)
        {
            return;
        }
        if (Application.isPlaying)
        {
            Destroy(displayedTheme);
        }
        else
        {
            DestroyImmediate(displayedTheme);
        }
        displayedTheme = null;
    }

    private void SetRenderersVisible(bool visible)
    {
        AudioVisualMesh[] effects = { ribbons, rings, terrain, canopy };
        foreach (AudioVisualMesh effect in effects)
        {
            if (effect != null && effect.TryGetComponent(out MeshRenderer renderer))
            {
                renderer.enabled = visible;
            }
        }
    }
}
