using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runs an independent GPU particle simulation driven by AudioCaptureCSCore FFT data and
/// AudioVisualizer beat analysis. Particle simulation and respawning stay entirely on the GPU.
/// </summary>
[DisallowMultipleComponent]
public sealed class GpuAudioParticleVisualizer : MonoBehaviour
{
    private const int ThreadGroupSize = 64;
    private const int ParticleStride = 16 * sizeof(float);
    private const int FallbackSampleRate = 48000;

    [Header("Audio Sources")]
    [Tooltip("FFT source. Automatically resolved when empty.")]
    public AudioCaptureCSCore audioCapture;

    [Tooltip("Beat, BPM, and band-energy source. Automatically resolved when empty.")]
    public AudioVisualizer audioVisualizer;

    [Header("GPU Assets")]
    public ComputeShader particleCompute;
    public Shader particleShader;

    [Header("Particle Layers")]
    [Min(64)] public int spectrumParticleCount = 3072;
    [Min(64)] public int beatParticleCount = 1024;
    [Min(64)] public int backgroundParticleCount = 3072;
    [Range(32, 256)] public int spectrumBinCount = 128;

    [Header("Composition")]
    [Min(0.05f)] public float spectrumRadius = 0.78f;
    [Min(0.01f)] public float spectrumHeight = 0.46f;
    [Min(0f)] public float spectrumDepth = 0.18f;
    [Min(0f)] public float spectrumDisplacement = 0.58f;
    [Min(0f)] public float beatExpansion = 0.32f;
    public Vector3 backgroundExtents = new Vector3(2.6f, 1.55f, 0.75f);
    [Min(0.01f)] public float beatSpeed = 1.8f;
    [Min(0.01f)] public float beatLifetime = 0.85f;
    [Min(0f)] public float swirlSpeed = 0.18f;
    [Min(0f)] public float backgroundSpeed = 0.08f;

    [Header("Audio Response")]
    [Min(0f)] public float spectrumGain = 70f;
    [Min(0f)] public float kickGain = 12f;
    [Min(0f)] public float bassGain = 10f;
    [Min(0f)] public float synthGain = 9f;
    [Min(0.01f)] public float spectrumAttack = 18f;
    [Min(0.01f)] public float spectrumRelease = 6f;
    [Min(0.01f)] public float beatDecay = 0.42f;
    [Range(0f, 0.25f)] public float silentBackgroundLevel = 0.035f;

    [Header("Color")]
    [ColorUsage(true, true)] public Color bassColor = new Color(0.02f, 0.72f, 1f, 1f);
    [ColorUsage(true, true)] public Color midColor = new Color(1f, 0.16f, 0.58f, 1f);
    [ColorUsage(true, true)] public Color trebleColor = new Color(1f, 0.72f, 0.12f, 1f);
    [ColorUsage(true, true)] public Color backgroundColor = new Color(0.14f, 0.3f, 0.62f, 1f);
    [ColorUsage(true, true)] public Color beatColor = new Color(0.65f, 0.95f, 1f, 1f);

    [Header("Rendering")]
    [Min(0.001f)] public float particleSize = 0.018f;
    [Min(0f)] public float backgroundSizeScale = 0.46f;
    [Min(0f)] public float beatSizeScale = 1.45f;
    [Min(0f)] public float emission = 1.8f;
    [Range(0f, 1f)] public float opacity = 0.82f;

    private GraphicsBuffer particleBuffer;
    private GraphicsBuffer spectrumBuffer;
    private ComputeShader runtimeCompute;
    private Material runtimeMaterial;
    private MaterialPropertyBlock materialProperties;
    private float[] spectrumValues;
    private int initializeKernel;
    private int updateKernel;
    private int totalParticleCount;
    private int beatSequence;
    private float beatEnvelope;
    private float lastObservedBeatTime;
    private bool lastBeatSignal;
    private bool resourcesReady;
    private bool warnedAboutSupport;

    private static readonly int ParticlesId = Shader.PropertyToID("_Particles");
    private static readonly int SpectrumId = Shader.PropertyToID("_Spectrum");
    private static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
    private static readonly int SpectrumParticleCountId = Shader.PropertyToID("_SpectrumParticleCount");
    private static readonly int BeatParticleCountId = Shader.PropertyToID("_BeatParticleCount");
    private static readonly int SpectrumBinCountId = Shader.PropertyToID("_SpectrumBinCount");
    private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
    private static readonly int SimulationTimeId = Shader.PropertyToID("_SimulationTime");
    private static readonly int BeatId = Shader.PropertyToID("_BeatId");
    private static readonly int BeatTriggerId = Shader.PropertyToID("_BeatTrigger");
    private static readonly int BeatEnvelopeId = Shader.PropertyToID("_BeatEnvelope");
    private static readonly int KickId = Shader.PropertyToID("_Kick");
    private static readonly int BassId = Shader.PropertyToID("_Bass");
    private static readonly int SynthId = Shader.PropertyToID("_Synth");
    private static readonly int EnergyId = Shader.PropertyToID("_Energy");
    private static readonly int SpectrumRadiusId = Shader.PropertyToID("_SpectrumRadius");
    private static readonly int SpectrumHeightId = Shader.PropertyToID("_SpectrumHeight");
    private static readonly int SpectrumDepthId = Shader.PropertyToID("_SpectrumDepth");
    private static readonly int SpectrumDisplacementId = Shader.PropertyToID("_SpectrumDisplacement");
    private static readonly int BeatExpansionId = Shader.PropertyToID("_BeatExpansion");
    private static readonly int BackgroundExtentsId = Shader.PropertyToID("_BackgroundExtents");
    private static readonly int BeatSpeedId = Shader.PropertyToID("_BeatSpeed");
    private static readonly int BeatLifetimeId = Shader.PropertyToID("_BeatLifetime");
    private static readonly int SwirlSpeedId = Shader.PropertyToID("_SwirlSpeed");
    private static readonly int BackgroundSpeedId = Shader.PropertyToID("_BackgroundSpeed");
    private static readonly int BassColorId = Shader.PropertyToID("_BassColor");
    private static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    private static readonly int TrebleColorId = Shader.PropertyToID("_TrebleColor");
    private static readonly int BackgroundColorId = Shader.PropertyToID("_BackgroundColor");
    private static readonly int BeatColorId = Shader.PropertyToID("_BeatColor");
    private static readonly int LocalToWorldId = Shader.PropertyToID("_LocalToWorld");
    private static readonly int ParticleSizeId = Shader.PropertyToID("_ParticleSize");
    private static readonly int BackgroundSizeScaleId = Shader.PropertyToID("_BackgroundSizeScale");
    private static readonly int BeatSizeScaleId = Shader.PropertyToID("_BeatSizeScale");
    private static readonly int EmissionId = Shader.PropertyToID("_Emission");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    public int TotalParticleCount => totalParticleCount;
    public bool IsReady => resourcesReady;

    private void OnEnable()
    {
        ResolveAudioSources();
        TryCreateResources();
    }

    private void OnDisable()
    {
        ReleaseResources();
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void OnValidate()
    {
        spectrumParticleCount = Mathf.Max(ThreadGroupSize, spectrumParticleCount);
        beatParticleCount = Mathf.Max(ThreadGroupSize, beatParticleCount);
        backgroundParticleCount = Mathf.Max(ThreadGroupSize, backgroundParticleCount);
        spectrumBinCount = Mathf.Clamp(spectrumBinCount, 32, 256);
        backgroundExtents.x = Mathf.Max(0.1f, backgroundExtents.x);
        backgroundExtents.y = Mathf.Max(0.1f, backgroundExtents.y);
        backgroundExtents.z = Mathf.Max(0.05f, backgroundExtents.z);
    }

    private void Update()
    {
        if (!resourcesReady)
        {
            TryCreateResources();
            return;
        }

        if (audioCapture == null || audioVisualizer == null)
        {
            ResolveAudioSources();
        }

        float deltaTime = Mathf.Min(Time.deltaTime, 0.05f);
        UpdateSpectrum(deltaTime);
        UpdateSimulation(deltaTime);
        RenderParticles();
    }

    private void ResolveAudioSources()
    {
        if (audioCapture == null)
        {
            audioCapture = AudioCaptureCSCore.instance != null
                ? AudioCaptureCSCore.instance
                : FindFirstObjectByType<AudioCaptureCSCore>();
        }

        if (audioVisualizer == null)
        {
            audioVisualizer = FindFirstObjectByType<AudioVisualizer>();
        }
    }

    private void TryCreateResources()
    {
        if (resourcesReady || !isActiveAndEnabled)
        {
            return;
        }

        if (!SystemInfo.supportsComputeShaders)
        {
            if (!warnedAboutSupport)
            {
                Debug.LogWarning("[GpuAudioParticleVisualizer] Compute shaders are not supported on this graphics device.", this);
                warnedAboutSupport = true;
            }
            return;
        }

        if (particleCompute == null || particleShader == null)
        {
            return;
        }

        ReleaseResources();

        totalParticleCount = spectrumParticleCount + beatParticleCount + backgroundParticleCount;
        spectrumValues = new float[spectrumBinCount];
        particleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalParticleCount, ParticleStride);
        spectrumBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, spectrumBinCount, sizeof(float));
        spectrumBuffer.SetData(spectrumValues);

        runtimeCompute = Instantiate(particleCompute);
        runtimeMaterial = new Material(particleShader)
        {
            name = "GPU Audio Particles (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        materialProperties = new MaterialPropertyBlock();

        initializeKernel = runtimeCompute.FindKernel("InitializeParticles");
        updateKernel = runtimeCompute.FindKernel("UpdateParticles");
        BindSimulationBuffers(initializeKernel);
        BindSimulationBuffers(updateKernel);
        SetStaticSimulationParameters();

        runtimeCompute.Dispatch(initializeKernel, GetThreadGroupCount(totalParticleCount), 1, 1);
        lastObservedBeatTime = audioVisualizer != null ? audioVisualizer.lastBeatTime : 0f;
        lastBeatSignal = audioVisualizer != null && audioVisualizer.showBeatText;
        resourcesReady = true;
    }

    private void BindSimulationBuffers(int kernel)
    {
        runtimeCompute.SetBuffer(kernel, ParticlesId, particleBuffer);
        runtimeCompute.SetBuffer(kernel, SpectrumId, spectrumBuffer);
    }

    private void SetStaticSimulationParameters()
    {
        runtimeCompute.SetInt(ParticleCountId, totalParticleCount);
        runtimeCompute.SetInt(SpectrumParticleCountId, spectrumParticleCount);
        runtimeCompute.SetInt(BeatParticleCountId, beatParticleCount);
        runtimeCompute.SetInt(SpectrumBinCountId, spectrumBinCount);
        runtimeCompute.SetFloat(SpectrumRadiusId, spectrumRadius);
        runtimeCompute.SetFloat(SpectrumHeightId, spectrumHeight);
        runtimeCompute.SetFloat(SpectrumDepthId, spectrumDepth);
        runtimeCompute.SetFloat(SpectrumDisplacementId, spectrumDisplacement);
        runtimeCompute.SetFloat(BeatExpansionId, beatExpansion);
        runtimeCompute.SetVector(BackgroundExtentsId, backgroundExtents);
        runtimeCompute.SetFloat(BeatSpeedId, beatSpeed);
        runtimeCompute.SetFloat(BeatLifetimeId, beatLifetime);
        runtimeCompute.SetFloat(SwirlSpeedId, swirlSpeed);
        runtimeCompute.SetFloat(BackgroundSpeedId, backgroundSpeed);
        runtimeCompute.SetVector(BassColorId, bassColor);
        runtimeCompute.SetVector(MidColorId, midColor);
        runtimeCompute.SetVector(TrebleColorId, trebleColor);
        runtimeCompute.SetVector(BackgroundColorId, backgroundColor);
        runtimeCompute.SetVector(BeatColorId, beatColor);
    }

    private void UpdateSpectrum(float deltaTime)
    {
        float[] source = audioCapture != null && audioCapture.smoothedFftData != null
            ? audioCapture.smoothedFftData
            : null;
        bool hasSpectrum = source != null && source.Length > 1 && audioCapture.HasFftData;
        int sampleRate = GetSampleRate();

        for (int i = 0; i < spectrumValues.Length; i++)
        {
            float target = hasSpectrum ? SampleSpectrumLogarithmically(source, sampleRate, i) : 0f;
            float speed = target > spectrumValues[i] ? spectrumAttack : spectrumRelease;
            float blend = 1f - Mathf.Exp(-speed * deltaTime);
            spectrumValues[i] = Mathf.Lerp(spectrumValues[i], target, blend);
        }

        spectrumBuffer.SetData(spectrumValues);
    }

    private float SampleSpectrumLogarithmically(float[] source, int sampleRate, int spectrumIndex)
    {
        const float minimumFrequency = 35f;
        const float maximumFrequency = 16000f;
        float t = (spectrumIndex + 0.5f) / spectrumValues.Length;
        float frequency = minimumFrequency * Mathf.Pow(maximumFrequency / minimumFrequency, t);
        int center = Mathf.Clamp(Mathf.RoundToInt(frequency * source.Length / sampleRate), 1, source.Length - 1);
        int radius = center < 24 ? 2 : 1;
        float magnitude = 0f;
        int samples = 0;

        for (int offset = -radius; offset <= radius; offset++)
        {
            int index = Mathf.Clamp(center + offset, 0, source.Length - 1);
            magnitude += Mathf.Max(0f, source[index]);
            samples++;
        }

        magnitude /= Mathf.Max(1, samples);
        return Mathf.Clamp01(1f - Mathf.Exp(-magnitude * spectrumGain));
    }

    private int GetSampleRate()
    {
        if (audioCapture != null && audioCapture.waveSource != null)
        {
            return Mathf.Max(1, audioCapture.waveSource.WaveFormat.SampleRate);
        }

        return FallbackSampleRate;
    }

    private void UpdateSimulation(float deltaTime)
    {
        bool beatTriggered = DetectBeatEdge();
        if (beatTriggered)
        {
            beatSequence++;
            beatEnvelope = 1f;
        }
        else
        {
            beatEnvelope *= Mathf.Exp(-deltaTime / Mathf.Max(0.01f, beatDecay));
        }

        float kick = 0f;
        float bass = 0f;
        float synth = 0f;
        bool silent = true;
        if (audioVisualizer != null)
        {
            kick = CompressEnergy(audioVisualizer.smoothedKickEnergy, kickGain);
            bass = CompressEnergy(audioVisualizer.smoothedBassEnergy, bassGain);
            synth = CompressEnergy(audioVisualizer.smoothedSynthEnergy, synthGain);
            silent = audioVisualizer.wasSilent;
        }

        float idle = silent ? silentBackgroundLevel : 0f;
        synth = Mathf.Max(synth, idle);
        float energy = Mathf.Clamp01(kick * 0.38f + bass * 0.34f + synth * 0.28f);

        SetStaticSimulationParameters();
        runtimeCompute.SetFloat(DeltaTimeId, deltaTime);
        runtimeCompute.SetFloat(SimulationTimeId, Time.time);
        runtimeCompute.SetInt(BeatId, beatSequence);
        runtimeCompute.SetFloat(BeatTriggerId, beatTriggered ? 1f : 0f);
        runtimeCompute.SetFloat(BeatEnvelopeId, beatEnvelope);
        runtimeCompute.SetFloat(KickId, kick);
        runtimeCompute.SetFloat(BassId, bass);
        runtimeCompute.SetFloat(SynthId, synth);
        runtimeCompute.SetFloat(EnergyId, energy);
        runtimeCompute.Dispatch(updateKernel, GetThreadGroupCount(totalParticleCount), 1, 1);
    }

    private bool DetectBeatEdge()
    {
        if (audioVisualizer == null)
        {
            lastBeatSignal = false;
            return false;
        }

        float currentBeatTime = audioVisualizer.lastBeatTime;
        bool timeAdvanced = currentBeatTime > 0f && currentBeatTime > lastObservedBeatTime + 0.0001f;
        bool signal = audioVisualizer.showBeatText;
        bool signalRose = signal && !lastBeatSignal;
        lastObservedBeatTime = Mathf.Max(lastObservedBeatTime, currentBeatTime);
        lastBeatSignal = signal;
        return timeAdvanced || signalRose;
    }

    private static float CompressEnergy(float value, float gain)
    {
        return Mathf.Clamp01(1f - Mathf.Exp(-Mathf.Max(0f, value) * gain));
    }

    private void RenderParticles()
    {
        if (runtimeMaterial == null || particleBuffer == null)
        {
            return;
        }

        materialProperties.Clear();
        materialProperties.SetBuffer(ParticlesId, particleBuffer);
        materialProperties.SetMatrix(LocalToWorldId, transform.localToWorldMatrix);
        materialProperties.SetFloat(ParticleSizeId, particleSize);
        materialProperties.SetFloat(BackgroundSizeScaleId, backgroundSizeScale);
        materialProperties.SetFloat(BeatSizeScaleId, beatSizeScale);
        materialProperties.SetFloat(EmissionId, emission);
        materialProperties.SetFloat(OpacityId, opacity);

        Vector3 lossyScale = transform.lossyScale;
        float largestScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
        Vector3 localSize = backgroundExtents * 2f + Vector3.one * (spectrumDisplacement + beatSpeed * beatLifetime + 0.5f);
        Bounds worldBounds = new Bounds(transform.position, localSize * Mathf.Max(0.001f, largestScale));

        RenderParams renderParams = new RenderParams(runtimeMaterial)
        {
            worldBounds = worldBounds,
            matProps = materialProperties,
            layer = gameObject.layer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false
        };

        Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, totalParticleCount);
    }

    private static int GetThreadGroupCount(int count)
    {
        return Mathf.CeilToInt(count / (float)ThreadGroupSize);
    }

    private void ReleaseResources()
    {
        resourcesReady = false;

        particleBuffer?.Release();
        particleBuffer = null;
        spectrumBuffer?.Release();
        spectrumBuffer = null;

        DestroyRuntimeObject(runtimeMaterial);
        runtimeMaterial = null;
        DestroyRuntimeObject(runtimeCompute);
        runtimeCompute = null;
        materialProperties = null;
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
