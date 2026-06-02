using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Advanced VJ and dynamic stage manager.
/// Keeps the original inspector fields, then adds an audio-reactive director,
/// moving fixture rig, generated LED visuals, VFX parameter bus, and dynamic
/// stage environment controls.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(80)]
public partial class StageManager : MonoBehaviour
{
    [Header("Stage Core References")]
    public AudioVisualizer audioVisualizer;

    [Header("Legacy Lighting Arrays")]
    public Light[] spotlights;
    public Light[] rimLights;
    public Light[] chaseLights;
    public Light[] laserLights;
    public Light[] strobeLights;

    [Header("Legacy VFX Graphs")]
    public VisualEffect backgroundParticles;
    public VisualEffect smokeEffect;
    public VisualEffect beatBurstEffect;
    public VisualEffect laserBeamEffect;
    public VisualEffect groundRingEffect;

    [Header("Legacy Stage Decor")]
    public Renderer[] ledScreens;
    public GameObject discoBall;
    public Renderer stageFloor;

    [Header("Legacy Light Parameters")]
    [Range(0f, 10f)] public float baseLightIntensity = 2f;
    [Range(0f, 20f)] public float beatLightIntensity = 8f;
    [Range(0f, 5f)] public float colorChangeSpeed = 1f;
    [Range(0f, 2f)] public float chaseLightSpeed = 0.5f;

    [Header("Legacy VFX Parameters")]
    [Range(0f, 1000f)] public float particleSpawnRate = 100f;
    [Range(0f, 1f)] public float smokeDensity = 0.3f;
    [Range(0f, 10f)] public float laserIntensity = 5f;

    [Header("Legacy Mood Gradients")]
    public Gradient majorMoodGradient;
    public Gradient minorMoodGradient;

    [Header("Legacy Energy Thresholds")]
    [Range(0f, 1f)] public float strobeThreshold = 0.7f;
    [Range(0f, 1f)] public float laserThreshold = 0.5f;

    [Header("Advanced Runtime")]
    public StageRuntimeSettings runtime = new StageRuntimeSettings();
    public StageAudioSettings audio = new StageAudioSettings();
    public StageLightingSettings lighting = new StageLightingSettings();
    public StageVJSettings vj = new StageVJSettings();
    public StageVFXSettings vfx = new StageVFXSettings();
    public StageDirectorSettings director = new StageDirectorSettings();
    public StageEnvironmentSettings environment = new StageEnvironmentSettings();
    public StageAutomationSlot[] manualSlots = new StageAutomationSlot[0];
    public StageDebugSettings debug = new StageDebugSettings();

    private const string LogPrefix = "[StageManager]";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    private readonly List<FixtureState> fixtures = new List<FixtureState>(128);
    private readonly List<FixtureState> spotFixtures = new List<FixtureState>(32);
    private readonly List<FixtureState> rimFixtures = new List<FixtureState>(32);
    private readonly List<FixtureState> chaseFixtures = new List<FixtureState>(32);
    private readonly List<FixtureState> laserFixtures = new List<FixtureState>(32);
    private readonly List<FixtureState> strobeFixtures = new List<FixtureState>(32);
    private readonly List<ScreenState> screens = new List<ScreenState>(16);
    private readonly List<VisualEffect> allVfx = new List<VisualEffect>(16);
    private readonly List<StageCue> cueLibrary = new List<StageCue>(256);
    private readonly List<StagePalette> paletteLibrary = new List<StagePalette>(96);
    private readonly Dictionary<Light, FixtureState> fixtureLookup = new Dictionary<Light, FixtureState>();
    private MaterialPropertyBlock rendererBlock;
    private readonly StageAudioFrame frame = new StageAudioFrame();
    private readonly StageAudioFrame previousFrame = new StageAudioFrame();
    private readonly BeatClock clock = new BeatClock();
    private readonly Envelope beatEnvelope = new Envelope();
    private readonly Envelope kickEnvelope = new Envelope();
    private readonly Envelope bassEnvelope = new Envelope();
    private readonly Envelope synthEnvelope = new Envelope();
    private readonly Envelope strobeEnvelope = new Envelope();
    private readonly Envelope laserEnvelope = new Envelope();
    private readonly RandomDeck randomDeck = new RandomDeck(9917);

    private StageCue currentCue;
    private StageCue previousCue;
    private StageCue requestedCue;
    private StagePalette currentPalette;
    private StagePalette previousPalette;
    private StagePalette targetPalette;
    private RenderSettingsSnapshot renderSnapshot;
    private Bounds stageBounds;
    private Vector3 stageCenter;
    private Color moodColor = Color.white;
    private Color primaryColor = Color.white;
    private Color secondaryColor = Color.cyan;
    private Color accentColor = Color.magenta;
    private Color backgroundColor = Color.black;
    private Color flashColor = Color.white;
    private bool initialized;
    private bool renderSnapshotCaptured;
    private bool lastBeatSignal;
    private bool blackout;
    private bool manualAudio;
    private bool pauseDirector;
    private bool firstFrame = true;
    private float hue;
    private float chaseAngle;
    private float scannerAngle;
    private float strobeTime;
    private float cueElapsedSeconds;
    private float cueElapsedBeats;
    private float cueBlend = 1f;
    private float cueBlendTime;
    private float paletteBlend = 1f;
    private float screenTimer;
    private float blackoutLevel;
    private float flashLevel;
    private float flashTimer;
    private float flashDuration = 0.18f;
    private float manualKick;
    private float manualBass;
    private float manualSynth;
    private float manualBpm = 120f;
    private float masterLevel = 1f;
    private int currentCueIndex = -1;
    private int lastBeatIndex = -1;
    private int lastPhraseIndex = -1;

    public int FixtureCount { get { return fixtures.Count; } }
    public int ScreenCount { get { return screens.Count; } }
    public int CueCount { get { return cueLibrary.Count; } }
    public string CurrentCueName { get { return currentCue != null ? currentCue.name : "None"; } }
    public StageEnergyMode CurrentEnergyMode { get { return frame.energyMode; } }
    public Color CurrentMoodColor { get { return moodColor; } }

    private void Reset()
    {
        runtime.autoDiscoverScene = true;
        runtime.autoFindAudioVisualizer = true;
        runtime.masterIntensity = 1f;
        director.enableAutoDirector = true;
        lighting.enableLighting = true;
        vj.enableVJ = true;
        environment.enableDynamicEnvironment = true;
        EnsureDefaultGradients();
    }

    private void Awake()
    {
        InitializeRuntime();
    }

    private void OnEnable()
    {
        InitializeRuntime();
    }

    private void Start()
    {
        InitializeRuntime();
        if (debug.logLifecycle)
        {
            Debug.Log(string.Format("{0} ready: fixtures={1}, screens={2}, cues={3}", LogPrefix, fixtures.Count, screens.Count, cueLibrary.Count));
        }
    }

    private void OnValidate()
    {
        SanitizeSettings();
        EnsureDefaultGradients();
    }

    private void OnDisable()
    {
        if (runtime.restoreRenderSettingsOnDisable)
        {
            RestoreRenderSettings();
        }
    }

    private void OnDestroy()
    {
        DisposeScreenTextures();
        if (runtime.restoreRenderSettingsOnDisable)
        {
            RestoreRenderSettings();
        }
    }

    private void Update()
    {
        if (!runtime.enableSystem)
        {
            return;
        }

        if (!initialized || runtime.rebuildCacheEveryFrame)
        {
            InitializeRuntime();
        }

        float dt = runtime.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f && !runtime.updateOnZeroDeltaTime)
        {
            return;
        }

        dt = Mathf.Clamp(dt, 0f, runtime.maxDeltaTime);
        UpdateAudio(dt);
        UpdateManualSlots();
        UpdateDirector(dt);
        UpdatePalette(dt);
        UpdateEnvelopes(dt);
        UpdateLighting(dt);
        UpdateVfx(dt);
        UpdateScreens(dt);
        UpdateEnvironment(dt);
        firstFrame = false;
    }

    private void LateUpdate()
    {
        if (!runtime.enableSystem || !runtime.forceEnableActiveSpotlights)
        {
            return;
        }

        for (int i = 0; i < fixtures.Count; i++)
        {
            FixtureState fixture = fixtures[i];
            if (fixture.light != null && fixture.light.type == LightType.Spot && fixture.light.intensity > 0.02f)
            {
                fixture.light.enabled = true;
            }
        }
    }

    private void OnGUI()
    {
        if (!debug.showRuntimeOverlay)
        {
            return;
        }

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = debug.overlayFontSize;
        style.normal.textColor = Color.white;
        GUILayout.BeginArea(new Rect(20, 20, 460, 290), GUI.skin.box);
        GUILayout.Label("Cue: " + CurrentCueName, style);
        GUILayout.Label(string.Format("Energy: {0} {1:F2} | BPM {2:F1}", frame.energyMode, frame.energy, frame.bpm), style);
        GUILayout.Label(string.Format("Kick {0:F2} Bass {1:F2} Synth {2:F2}", frame.kick, frame.bass, frame.synth), style);
        GUILayout.Label(string.Format("Key {0} {1} | Fixtures {2} Screens {3}", frame.key, frame.mode, fixtures.Count, screens.Count), style);
        GUILayout.EndArea();
    }

    private void OnDrawGizmos()
    {
        if (debug == null || !debug.drawGizmos)
        {
            return;
        }

        Gizmos.color = new Color(moodColor.r, moodColor.g, moodColor.b, 0.35f);
        Gizmos.DrawWireCube(stageBounds.center == Vector3.zero ? transform.position : stageBounds.center, stageBounds.size == Vector3.zero ? new Vector3(8f, 4f, 8f) : stageBounds.size);

        if (!debug.drawFixtureRays || fixtures == null)
        {
            return;
        }

        for (int i = 0; i < fixtures.Count; i++)
        {
            FixtureState fixture = fixtures[i];
            if (fixture == null || fixture.light == null)
            {
                continue;
            }

            Gizmos.color = RoleColor(fixture.role);
            Gizmos.DrawWireSphere(fixture.transform.position, 0.12f * debug.gizmoScale);
            Gizmos.DrawLine(fixture.transform.position, fixture.transform.position + fixture.transform.forward * debug.gizmoScale * 1.25f);
        }
    }

    [ContextMenu("Rebuild Stage Cache")]
    public void RebuildStageCache()
    {
        initialized = false;
        InitializeRuntime();
    }

    [ContextMenu("Trigger Next Cue")]
    public void TriggerNextCue()
    {
        if (cueLibrary.Count == 0)
        {
            return;
        }

        int next = (currentCueIndex + 1) % cueLibrary.Count;
        TriggerCue(next);
    }

    [ContextMenu("Flash White")]
    public void FlashWhite()
    {
        Flash(Color.white, 1f, 0.18f);
    }

    public void TriggerCue(string cueName)
    {
        if (string.IsNullOrEmpty(cueName))
        {
            return;
        }

        EnsureLibraries();
        for (int i = 0; i < cueLibrary.Count; i++)
        {
            if (string.Equals(cueLibrary[i].name, cueName, StringComparison.OrdinalIgnoreCase))
            {
                requestedCue = cueLibrary[i];
                currentCueIndex = i;
                return;
            }
        }
    }

    public void TriggerCue(int index)
    {
        EnsureLibraries();
        if (index < 0 || index >= cueLibrary.Count)
        {
            return;
        }

        requestedCue = cueLibrary[index];
        currentCueIndex = index;
    }

    public void SetMasterIntensity(float intensity)
    {
        runtime.masterIntensity = Mathf.Clamp(intensity, 0f, 4f);
    }

    public void Blackout(bool enabled)
    {
        blackout = enabled;
    }

    public void ToggleBlackout()
    {
        blackout = !blackout;
    }

    public void Flash(Color color, float strength, float duration)
    {
        flashColor = color;
        flashLevel = Mathf.Max(flashLevel, Mathf.Clamp01(strength));
        flashDuration = Mathf.Max(0.03f, duration);
        flashTimer = flashDuration;
    }

    public void SetManualAudio(float kick, float bass, float synth, float bpm, bool beat)
    {
        manualAudio = true;
        audio.useManualInput = true;
        manualKick = Mathf.Clamp01(kick);
        manualBass = Mathf.Clamp01(bass);
        manualSynth = Mathf.Clamp01(synth);
        manualBpm = Mathf.Clamp(bpm, 40f, 240f);
        if (beat)
        {
            clock.ForceBeat(director.phraseLengthBeats);
        }
    }

    public void ClearManualAudio()
    {
        manualAudio = false;
        audio.useManualInput = false;
    }

    public void SetVJPattern(VJPattern pattern)
    {
        vj.defaultPattern = pattern;
        if (currentCue != null)
        {
            currentCue.pattern = pattern;
        }
    }

    public void PauseDirector(bool paused)
    {
        pauseDirector = paused;
    }

    public void ApplyLightingPreset(StageLightingPreset preset)
    {
        if (preset == null)
        {
            return;
        }

        preset.ApplyToStage(this);
        RebuildStageCache();
    }

    private void InitializeRuntime()
    {
        SanitizeSettings();
        EnsureDefaultGradients();
        CaptureRenderSettings();
        EnsureRuntimeObjects();

        if (runtime.autoFindAudioVisualizer && audioVisualizer == null)
        {
#if UNITY_2023_1_OR_NEWER
            audioVisualizer = FindFirstObjectByType<AudioVisualizer>();
#else
            audioVisualizer = FindObjectOfType<AudioVisualizer>();
#endif
        }

        if (runtime.autoDiscoverScene)
        {
            DiscoverReferences();
        }

        CompleteVfxGraphBindings();
        CacheStageBounds();
        CacheFixtures();
        CacheScreens();
        CacheVfx();
        EnsureLibraries();

        if (currentCue == null && cueLibrary.Count > 0)
        {
            currentCueIndex = 0;
            currentCue = cueLibrary[0];
            previousCue = currentCue;
            SelectPalette(currentCue, true);
        }

        initialized = true;
    }

    private void EnsureRuntimeObjects()
    {
        if (rendererBlock == null)
        {
            rendererBlock = new MaterialPropertyBlock();
        }
    }

    [ContextMenu("Complete Stage VFX Graphs")]
    public void CompleteStageVfxGraphs()
    {
        CompleteVfxGraphBindings();
        CacheVfx();
    }

    private void SanitizeSettings()
    {
        if (runtime == null) runtime = new StageRuntimeSettings();
        if (audio == null) audio = new StageAudioSettings();
        if (lighting == null) lighting = new StageLightingSettings();
        if (vj == null) vj = new StageVJSettings();
        if (vfx == null) vfx = new StageVFXSettings();
        if (director == null) director = new StageDirectorSettings();
        if (environment == null) environment = new StageEnvironmentSettings();
        if (debug == null) debug = new StageDebugSettings();
        baseLightIntensity = Mathf.Max(0f, baseLightIntensity);
        beatLightIntensity = Mathf.Max(baseLightIntensity, beatLightIntensity);
        particleSpawnRate = Mathf.Max(0f, particleSpawnRate);
        smokeDensity = Mathf.Clamp01(smokeDensity);
        strobeThreshold = Mathf.Clamp01(strobeThreshold);
        laserThreshold = Mathf.Clamp01(laserThreshold);
        director.phraseLengthBeats = Mathf.Max(1, director.phraseLengthBeats);
        director.minimumCueBeats = Mathf.Max(1f, director.minimumCueBeats);
        director.maximumCueBeats = Mathf.Max(director.minimumCueBeats, director.maximumCueBeats);
        vj.textureWidth = Mathf.Clamp(vj.textureWidth, 16, 256);
        vj.textureHeight = Mathf.Clamp(vj.textureHeight, 8, 144);
    }

    private void EnsureDefaultGradients()
    {
        if (majorMoodGradient == null || majorMoodGradient.colorKeys == null || majorMoodGradient.colorKeys.Length == 0)
        {
            majorMoodGradient = new Gradient();
            majorMoodGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.68f, 0.16f), 0f),
                    new GradientColorKey(new Color(0.1f, 0.96f, 1f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.22f, 0.45f), 1f)
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        }

        if (minorMoodGradient == null || minorMoodGradient.colorKeys == null || minorMoodGradient.colorKeys.Length == 0)
        {
            minorMoodGradient = new Gradient();
            minorMoodGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.15f, 0.22f, 1f), 0f),
                    new GradientColorKey(new Color(0.72f, 0.12f, 0.95f), 0.5f),
                    new GradientColorKey(new Color(0.02f, 0.9f, 0.72f), 1f)
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        }
    }

    private void CaptureRenderSettings()
    {
        if (renderSnapshotCaptured)
        {
            return;
        }

        renderSnapshot = new RenderSettingsSnapshot();
        renderSnapshot.fog = RenderSettings.fog;
        renderSnapshot.fogColor = RenderSettings.fogColor;
        renderSnapshot.fogDensity = RenderSettings.fogDensity;
        renderSnapshot.ambientLight = RenderSettings.ambientLight;
        renderSnapshot.ambientIntensity = RenderSettings.ambientIntensity;
        renderSnapshotCaptured = true;
    }

    private void RestoreRenderSettings()
    {
        if (!renderSnapshotCaptured || renderSnapshot == null)
        {
            return;
        }

        RenderSettings.fog = renderSnapshot.fog;
        RenderSettings.fogColor = renderSnapshot.fogColor;
        RenderSettings.fogDensity = renderSnapshot.fogDensity;
        RenderSettings.ambientLight = renderSnapshot.ambientLight;
        RenderSettings.ambientIntensity = renderSnapshot.ambientIntensity;
    }

    private void DiscoverReferences()
    {
        Light[] lights = GetComponentsInChildren<Light>(true);
        if (IsEmpty(spotlights)) spotlights = FilterLights(lights, FixtureRole.Spotlight);
        if (IsEmpty(rimLights)) rimLights = FilterLights(lights, FixtureRole.Rim);
        if (IsEmpty(chaseLights)) chaseLights = FilterLights(lights, FixtureRole.Chase);
        if (IsEmpty(laserLights)) laserLights = FilterLights(lights, FixtureRole.Laser);
        if (IsEmpty(strobeLights)) strobeLights = FilterLights(lights, FixtureRole.Strobe);
        if (stageFloor == null) stageFloor = FindRenderer("floor");
        if (discoBall == null)
        {
            Transform t = FindTransform("disco");
            if (t != null) discoBall = t.gameObject;
        }
        if (IsEmpty(ledScreens)) ledScreens = FilterRenderers(GetComponentsInChildren<Renderer>(true), "screen", "led", "panel", "wall");
        VisualEffect[] effects = GetComponentsInChildren<VisualEffect>(true);
        if (backgroundParticles == null) backgroundParticles = FindVfx(effects, "background", "particle", "rain");
        if (smokeEffect == null) smokeEffect = FindVfx(effects, "smoke", "fog", "haze");
        if (beatBurstEffect == null) beatBurstEffect = FindVfx(effects, "beat", "burst", "impact");
        if (laserBeamEffect == null) laserBeamEffect = FindVfx(effects, "laser", "beam");
        if (groundRingEffect == null) groundRingEffect = FindVfx(effects, "ground", "ring", "floor");
    }

    private void CompleteVfxGraphBindings()
    {
#if UNITY_EDITOR
        if (!vfx.autoCompleteGraphBindings)
        {
            return;
        }

        backgroundParticles = EnsureStageVfx("BackgroundParticles", backgroundParticles, "Assets/VFX/BackgroundParticles.vfx", null);
        smokeEffect = EnsureStageVfx("SmokeEffect", smokeEffect, "Assets/VFX/SmokeEffect.vfx", null);
        beatBurstEffect = EnsureStageVfx("BeatBurstEffect", beatBurstEffect, "Assets/VFX/BeatBurstEffect.vfx", null);
        laserBeamEffect = EnsureStageVfx("LaserBeamEffect", laserBeamEffect, "Assets/VFX/LaserBeamEffect.vfx", null);
        groundRingEffect = EnsureStageVfx("GroundRingEffect", groundRingEffect, "Assets/VFX/GroundRingEffect.vfx", "Assets/VFX/LaserBeamEffect.vfx");
#endif
    }

#if UNITY_EDITOR
    private VisualEffect EnsureStageVfx(string objectName, VisualEffect current, string primaryAssetPath, string fallbackAssetPath)
    {
        VisualEffectAsset asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(primaryAssetPath);
        if (asset == null && !string.IsNullOrEmpty(fallbackAssetPath))
        {
            asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(fallbackAssetPath);
        }

        if (current == null)
        {
            Transform target = FindTransform(objectName);
            if (target == null && vfx.createMissingGraphObjects)
            {
                GameObject created = new GameObject(objectName);
                created.transform.SetParent(transform, false);
                target = created.transform;
            }

            if (target != null)
            {
                current = target.GetComponent<VisualEffect>();
                if (current == null)
                {
                    current = target.gameObject.AddComponent<VisualEffect>();
                }
            }
        }

        if (current != null && asset != null && current.visualEffectAsset != asset)
        {
            current.visualEffectAsset = asset;
            EditorUtility.SetDirty(current);
            if (!Application.isPlaying && vfx.markSceneDirtyWhenAutoCompleted)
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }

        return current;
    }
#endif

    private Light[] FilterLights(Light[] lights, FixtureRole role)
    {
        List<Light> result = new List<Light>();
        if (lights == null) return result.ToArray();
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null) continue;
            string n = light.name.ToLowerInvariant();
            bool match = false;
            if (role == FixtureRole.Spotlight) match = n.Contains("spot") || n.Contains("main") || light.type == LightType.Spot;
            if (role == FixtureRole.Rim) match = n.Contains("rim") || n.Contains("edge") || n.Contains("outline");
            if (role == FixtureRole.Chase) match = n.Contains("chase") || n.Contains("moving");
            if (role == FixtureRole.Laser) match = n.Contains("laser") || n.Contains("beam");
            if (role == FixtureRole.Strobe) match = n.Contains("strobe") || n.Contains("flash");
            if (match) result.Add(light);
        }
        return result.ToArray();
    }

    private Renderer[] FilterRenderers(Renderer[] renderers, params string[] tokens)
    {
        List<Renderer> result = new List<Renderer>();
        if (renderers == null) return result.ToArray();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            string n = renderer.name.ToLowerInvariant();
            for (int t = 0; t < tokens.Length; t++)
            {
                if (n.Contains(tokens[t]))
                {
                    result.Add(renderer);
                    break;
                }
            }
        }
        return result.ToArray();
    }

    private Renderer FindRenderer(string token)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        string lower = token.ToLowerInvariant();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].name.ToLowerInvariant().Contains(lower)) return renderers[i];
        }
        return null;
    }

    private Transform FindTransform(string token)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        string lower = token.ToLowerInvariant();
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name.ToLowerInvariant().Contains(lower)) return transforms[i];
        }
        return null;
    }

    private VisualEffect FindVfx(VisualEffect[] effects, params string[] tokens)
    {
        if (effects == null) return null;
        for (int i = 0; i < effects.Length; i++)
        {
            VisualEffect effect = effects[i];
            if (effect == null) continue;
            string n = effect.name.ToLowerInvariant();
            for (int t = 0; t < tokens.Length; t++)
            {
                if (n.Contains(tokens[t])) return effect;
            }
        }
        return null;
    }

    private void CacheStageBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool has = false;
        Bounds bounds = new Bounds(transform.position, new Vector3(20f, 8f, 15f));
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            if (!has)
            {
                bounds = renderer.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        stageBounds = bounds;
        stageCenter = environment.useStageBoundsForTarget ? bounds.center : transform.position;
    }

    private void CacheFixtures()
    {
        fixtures.Clear(); spotFixtures.Clear(); rimFixtures.Clear(); chaseFixtures.Clear(); laserFixtures.Clear(); strobeFixtures.Clear(); fixtureLookup.Clear();
        RegisterFixtures(spotlights, FixtureRole.Spotlight, spotFixtures);
        RegisterFixtures(rimLights, FixtureRole.Rim, rimFixtures);
        RegisterFixtures(chaseLights, FixtureRole.Chase, chaseFixtures);
        RegisterFixtures(laserLights, FixtureRole.Laser, laserFixtures);
        RegisterFixtures(strobeLights, FixtureRole.Strobe, strobeFixtures);
    }

    private void RegisterFixtures(Light[] lights, FixtureRole role, List<FixtureState> cache)
    {
        if (lights == null) return;
        for (int i = 0; i < lights.Length; i++) RegisterFixture(lights[i], role, cache);
    }

    private void RegisterFixture(Light light, FixtureRole role, List<FixtureState> cache)
    {
        if (light == null || fixtureLookup.ContainsKey(light)) return;
        FixtureState f = new FixtureState();
        f.light = light;
        f.transform = light.transform;
        f.role = role;
        f.index = fixtures.Count;
        f.roleIndex = cache.Count;
        f.phase = fixtures.Count * 0.173f + cache.Count * 0.071f;
        f.seed = Mathf.Repeat((fixtures.Count + 1) * 12.9898f + (int)role * 78.233f, 1000f);
        f.homeLocalPosition = light.transform.localPosition;
        f.homeWorldPosition = light.transform.position;
        f.homeLocalRotation = light.transform.localRotation;
        f.homeWorldRotation = light.transform.rotation;
        f.homeIntensity = Mathf.Max(0.01f, light.intensity);
        f.homeRange = Mathf.Max(0.01f, light.range);
        f.homeSpotAngle = Mathf.Max(1f, light.spotAngle);
        f.homeColor = light.color;
        f.currentColor = light.color;
        f.currentIntensity = light.intensity;
        fixtures.Add(f);
        cache.Add(f);
        fixtureLookup.Add(light, f);
    }

    private void CacheScreens()
    {
        DisposeScreenTextures();
        screens.Clear();
        if (ledScreens == null) return;
        for (int i = 0; i < ledScreens.Length; i++)
        {
            Renderer renderer = ledScreens[i];
            if (renderer == null) continue;
            ScreenState screen = new ScreenState();
            screen.renderer = renderer;
            screen.index = screens.Count;
            screen.phase = i * 0.137f;
            screen.seed = Mathf.Repeat(41.17f * (i + 1), 999f);
            screen.width = vj.textureWidth;
            screen.height = vj.textureHeight;
            screen.pixels = new Color32[screen.width * screen.height];
            screen.block = new MaterialPropertyBlock();
            if (runtime.createRuntimeTextures)
            {
                screen.texture = new Texture2D(screen.width, screen.height, TextureFormat.RGBA32, false, true);
                screen.texture.name = "StageManager_VJ_" + screens.Count;
                screen.texture.wrapMode = TextureWrapMode.Repeat;
                screen.texture.filterMode = FilterMode.Point;
            }
            screens.Add(screen);
        }
    }

    private void DisposeScreenTextures()
    {
        for (int i = 0; i < screens.Count; i++)
        {
            Texture2D texture = screens[i].texture;
            if (texture == null) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(texture);
            else Destroy(texture);
#else
            Destroy(texture);
#endif
        }
    }

    private void CacheVfx()
    {
        allVfx.Clear();
        AddVfx(backgroundParticles); AddVfx(smokeEffect); AddVfx(beatBurstEffect); AddVfx(laserBeamEffect); AddVfx(groundRingEffect);
        VisualEffect[] children = GetComponentsInChildren<VisualEffect>(true);
        for (int i = 0; i < children.Length; i++) AddVfx(children[i]);
    }

    private void AddVfx(VisualEffect effect)
    {
        if (effect == null || allVfx.Contains(effect)) return;
        allVfx.Add(effect);
    }

    private void EnsureLibraries()
    {
        if (paletteLibrary.Count == 0) BuildGeneratedPalettes(paletteLibrary);
        if (director.useGeneratedCueLibrary && cueLibrary.Count == 0) BuildGeneratedCues(cueLibrary);
    }

    private void UpdateAudio(float dt)
    {
        previousFrame.CopyFrom(frame);
        bool hasAudio = audioVisualizer != null;
        bool useManual = manualAudio || audio.useManualInput;
        bool silent = !useManual && (!hasAudio || audioVisualizer.wasSilent);
        float rawKick = useManual ? manualKick : (hasAudio ? audioVisualizer.smoothedKickEnergy : 0f);
        float rawBass = useManual ? manualBass : (hasAudio ? audioVisualizer.smoothedBassEnergy : 0f);
        float rawSynth = useManual ? manualSynth : (hasAudio ? audioVisualizer.smoothedSynthEnergy : 0f);
        float bpm = useManual ? manualBpm : (hasAudio && audioVisualizer.limitedBPM > 0f ? audioVisualizer.limitedBPM : audio.fallbackBpm);
        bool beatSignal = useManual ? clock.beatPulse : (hasAudio && audioVisualizer.showBeatText);
        bool beatEdge = beatSignal && !lastBeatSignal;
        lastBeatSignal = beatSignal;
        float kick = NormalizeEnergy(rawKick, audio.kickGain);
        float bass = NormalizeEnergy(rawBass, audio.bassGain);
        float synth = NormalizeEnergy(rawSynth, audio.synthGain);
        if (silent && runtime.keepAnimatingWhenSilent)
        {
            float idle = runtime.silenceFloor;
            float t = Time.time * runtime.idleAnimationSpeed;
            kick = Mathf.Max(kick, idle * (0.35f + 0.65f * Mathf.PerlinNoise(t, 1.3f)));
            bass = Mathf.Max(bass, idle * (0.45f + 0.55f * Mathf.PerlinNoise(2.1f, t)));
            synth = Mathf.Max(synth, idle * (0.25f + 0.75f * Mathf.PerlinNoise(t, t * 0.37f)));
        }
        frame.kick = Follow(frame.kick, kick, dt);
        frame.bass = Follow(frame.bass, bass, dt);
        frame.synth = Follow(frame.synth, synth, dt);
        frame.previousEnergy = previousFrame.energy;
        frame.energy = Mathf.Clamp01(frame.kick * audio.kickWeight + frame.bass * audio.bassWeight + frame.synth * audio.synthWeight);
        frame.impact = Mathf.Clamp01(Mathf.Max(0f, frame.energy - frame.previousEnergy) + (beatEdge ? audio.beatEnergyBoost : 0f));
        frame.brightness = Mathf.Clamp01(frame.synth * 0.75f + frame.kick * 0.25f);
        frame.warmth = Mathf.Clamp01(frame.bass * 0.7f + frame.kick * 0.3f);
        frame.bpm = bpm;
        frame.isSilent = silent && frame.energy < audio.silenceExitEnergy;
        frame.hasAudio = hasAudio && !silent;
        frame.key = hasAudio ? audioVisualizer.currentKey : "Unknown";
        frame.mode = hasAudio ? audioVisualizer.currentMode : "Unknown";
        if (string.IsNullOrEmpty(frame.mode) || frame.mode == "Unknown") frame.mode = previousFrame.mode == "Unknown" ? "Major" : previousFrame.mode;
        clock.Update(dt, bpm, beatEdge, director.phraseLengthBeats);
        frame.isBeat = clock.beatPulse;
        frame.beatPhase = clock.phase;
        frame.barPhase = clock.barPhase;
        frame.secondsSinceBeat = clock.secondsSinceBeat;
        frame.beatStrength = beatEnvelope.value;
        frame.isBuild = frame.energy - previousFrame.energy > audio.dropRiseThreshold * 0.35f;
        frame.isDrop = frame.energy - previousFrame.energy > audio.dropRiseThreshold || (frame.isBeat && frame.energy > audio.driveEnergy);
        frame.energyMode = ClassifyEnergy(frame.energy, frame.isSilent);
        if (frame.isBeat)
        {
            beatEnvelope.Punch(Mathf.Clamp01(frame.energy + frame.impact));
            kickEnvelope.Punch(Mathf.Clamp01(frame.kick + 0.2f));
        }
    }

    private float NormalizeEnergy(float raw, float gain)
    {
        float v = Mathf.Max(0f, raw);
        v = audio.compressIncomingEnergy ? 1f - Mathf.Exp(-v * gain) : v * gain;
        return Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(v), Mathf.Max(0.1f, audio.energyGamma)));
    }

    private float Follow(float current, float target, float dt)
    {
        float speed = target > current ? audio.attackSpeed : audio.releaseSpeed;
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * dt));
    }

    private StageEnergyMode ClassifyEnergy(float energy, bool silent)
    {
        if (silent) return StageEnergyMode.Silence;
        if (energy >= audio.peakEnergy) return StageEnergyMode.Peak;
        if (energy >= audio.driveEnergy) return StageEnergyMode.Drive;
        if (energy >= audio.grooveEnergy) return StageEnergyMode.Groove;
        return StageEnergyMode.Calm;
    }

    private void UpdateManualSlots()
    {
        if (manualSlots == null) return;
        for (int i = 0; i < manualSlots.Length; i++)
        {
            StageAutomationSlot slot = manualSlots[i];
            if (slot == null || slot.key == KeyCode.None || !Input.GetKeyDown(slot.key)) continue;
            if (!string.IsNullOrEmpty(slot.cueName)) TriggerCue(slot.cueName);
            if (slot.pattern != VJPattern.Off) SetVJPattern(slot.pattern);
            if (slot.triggerFlash) Flash(slot.flashColor, slot.flashStrength, slot.flashDuration);
            if (slot.toggleBlackout) ToggleBlackout();
        }
    }

    private void UpdateDirector(float dt)
    {
        if (requestedCue != null)
        {
            SwitchCue(requestedCue, true);
            requestedCue = null;
        }
        if (currentCue == null)
        {
            if (cueLibrary.Count > 0) SwitchCue(cueLibrary[0], false);
            else return;
        }
        cueElapsedSeconds += dt;
        cueElapsedBeats += dt / Mathf.Max(0.01f, clock.beatDuration);
        cueBlendTime += dt;
        cueBlend = director.cueCrossfadeSeconds <= 0f ? 1f : Mathf.Clamp01(cueBlendTime / director.cueCrossfadeSeconds);
        bool newBeat = clock.beatPulse && clock.beatIndex != lastBeatIndex;
        bool newPhrase = clock.phraseIndex != lastPhraseIndex;
        if (newBeat) lastBeatIndex = clock.beatIndex;
        if (newPhrase) lastPhraseIndex = clock.phraseIndex;
        if (!director.enableAutoDirector || pauseDirector) return;
        bool durationExpired = cueElapsedBeats >= Mathf.Clamp(currentCue.durationBeats, director.minimumCueBeats, director.maximumCueBeats);
        bool phraseChange = director.changeCueOnPhrase && newPhrase && cueElapsedBeats >= director.minimumCueBeats;
        bool dropChange = director.changeCueOnDrop && frame.isDrop && cueElapsedBeats >= Mathf.Max(4f, director.minimumCueBeats * 0.5f);
        if (durationExpired || phraseChange || dropChange)
        {
            StageCue next = ChooseCue();
            if (next != null) SwitchCue(next, false);
        }
    }

    private StageCue ChooseCue()
    {
        float bestScore = -1f;
        int bestIndex = -1;
        for (int i = 0; i < cueLibrary.Count; i++)
        {
            if (!director.allowCueRepeats && i == currentCueIndex) continue;
            StageCue cue = cueLibrary[i];
            float score = cue.Score(frame, director, randomDeck.Next01());
            if (previousCue != null && cue.family != previousCue.family) score += director.noveltyWeight;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }
        if (bestIndex < 0) bestIndex = randomDeck.Range(0, cueLibrary.Count);
        currentCueIndex = bestIndex;
        return cueLibrary[bestIndex];
    }

    private void SwitchCue(StageCue cue, bool manual)
    {
        if (cue == null || cue == currentCue) return;
        previousCue = currentCue;
        currentCue = cue;
        cueElapsedSeconds = 0f;
        cueElapsedBeats = 0f;
        cueBlendTime = 0f;
        cueBlend = 0f;
        SelectPalette(cue, false);
        if (cue.useRandomAccent) hue = randomDeck.Next01();
        if (debug.logCueChanges) Debug.Log(string.Format("{0} Cue -> {1}{2}", LogPrefix, cue.name, manual ? " (manual)" : ""));
    }

    private void SelectPalette(StageCue cue, bool immediate)
    {
        if (paletteLibrary.Count == 0) BuildGeneratedPalettes(paletteLibrary);
        if (paletteLibrary.Count == 0) return;
        int index = cue != null ? Mathf.Abs(cue.paletteIndex) % paletteLibrary.Count : 0;
        previousPalette = currentPalette;
        targetPalette = paletteLibrary[index];
        if (immediate || currentPalette == null)
        {
            currentPalette = targetPalette;
            previousPalette = targetPalette;
            paletteBlend = 1f;
        }
        else
        {
            paletteBlend = 0f;
        }
    }

    private void UpdatePalette(float dt)
    {
        if (targetPalette == null) SelectPalette(currentCue, true);
        if (targetPalette == null) return;
        paletteBlend = Mathf.Clamp01(paletteBlend + dt / Mathf.Max(0.01f, director.paletteCrossfadeSeconds));
        StagePalette from = previousPalette != null ? previousPalette : targetPalette;
        StagePalette to = targetPalette;
        currentPalette = to;
        primaryColor = Color.Lerp(from.primary, to.primary, paletteBlend);
        secondaryColor = Color.Lerp(from.secondary, to.secondary, paletteBlend);
        accentColor = Color.Lerp(from.accent, to.accent, paletteBlend);
        backgroundColor = Color.Lerp(from.background, to.background, paletteBlend);
        float cueColorSpeed = currentCue != null ? currentCue.colorSpeed : 1f;
        hue = Mathf.Repeat(hue + dt * colorChangeSpeed * cueColorSpeed * 0.08f, 1f);
        Gradient gradient = string.Equals(frame.mode, "Minor", StringComparison.OrdinalIgnoreCase) ? minorMoodGradient : majorMoodGradient;
        Color keyColor = gradient != null ? gradient.Evaluate(Mathf.Repeat(hue + frame.barPhase * 0.25f, 1f)) : primaryColor;
        moodColor = Color.Lerp(to.Evaluate(hue, frame.energy, beatEnvelope.value), keyColor, to.moodWeight);
        moodColor = Color.Lerp(moodColor, flashColor, flashLevel * 0.8f);
    }

    private void UpdateEnvelopes(float dt)
    {
        beatEnvelope.Update(frame.isBeat ? Mathf.Clamp01(frame.energy + frame.impact) : 0f, 30f, 4f, dt);
        kickEnvelope.Update(frame.kick, 20f, 7f, dt);
        bassEnvelope.Update(frame.bass, 12f, 5f, dt);
        synthEnvelope.Update(frame.synth, 14f, 6f, dt);
        float strobeTarget = currentCue != null && lighting.allowStrobe && (frame.kick > strobeThreshold || frame.isBeat) ? currentCue.strobe : 0f;
        float laserTarget = currentCue != null && frame.synth > laserThreshold ? currentCue.laser : 0f;
        strobeEnvelope.Update(strobeTarget, 40f, 1f / Mathf.Max(0.01f, lighting.strobeHold), dt);
        laserEnvelope.Update(laserTarget, 24f, 1f / Mathf.Max(0.01f, lighting.laserHold), dt);
        if (flashTimer > 0f)
        {
            flashTimer -= dt;
            flashLevel = Mathf.Clamp01(flashTimer / Mathf.Max(0.01f, flashDuration));
        }
        else
        {
            flashLevel = Mathf.Lerp(flashLevel, 0f, 1f - Mathf.Exp(-8f * dt));
        }
        blackoutLevel = Mathf.MoveTowards(blackoutLevel, blackout || (currentCue != null && currentCue.blackout > 0.5f) ? 1f : 0f, dt / Mathf.Max(0.01f, runtime.blackoutFadeSpeed));
        masterLevel = Mathf.Lerp(masterLevel, runtime.masterIntensity, 1f - Mathf.Exp(-runtime.masterSmoothing * dt));
    }

    private void UpdateLighting(float dt)
    {
        if (!lighting.enableLighting) return;
        chaseAngle = Mathf.Repeat(chaseAngle + dt * BeatRate() * 360f * Mathf.Max(0.02f, chaseLightSpeed) * runtime.motionMaster, 360f);
        scannerAngle = Mathf.Repeat(scannerAngle + dt * (180f + frame.synth * 540f), 360f);
        strobeTime += dt;
        for (int i = 0; i < spotFixtures.Count; i++) UpdateSpot(spotFixtures[i], i, dt);
        for (int i = 0; i < rimFixtures.Count; i++) UpdateRim(rimFixtures[i], i, dt);
        for (int i = 0; i < chaseFixtures.Count; i++) UpdateChase(chaseFixtures[i], i, dt);
        for (int i = 0; i < laserFixtures.Count; i++) UpdateLaser(laserFixtures[i], i, dt);
        for (int i = 0; i < strobeFixtures.Count; i++) UpdateStrobe(strobeFixtures[i], i, dt);
    }

    private void UpdateSpot(FixtureState f, int index, float dt)
    {
        if (!Valid(f)) return;
        float cueGain = currentCue != null ? currentCue.spotlight : 1f;
        float pulse = Mathf.Clamp01(beatEnvelope.value * lighting.beatPunch + frame.kick * 0.35f);
        float target = Mathf.Lerp(baseLightIntensity, beatLightIntensity, pulse) * cueGain * lighting.spotGain * lighting.outputGain * masterLevel * (1f - blackoutLevel);
        ApplyLight(f, target, FixtureColor(f.phase + index * 0.07f, frame.kick, primaryColor, accentColor), dt);
        if (lighting.animateSpotAngles && f.light.type == LightType.Spot)
        {
            float angle = Mathf.Lerp(lighting.maxSpotAngle, lighting.minSpotAngle, Mathf.Clamp01(frame.kick + beatEnvelope.value * 0.5f));
            f.light.spotAngle = Mathf.Lerp(f.light.spotAngle, angle, 1f - Mathf.Exp(-lighting.intensitySmoothing * dt));
        }
        AimMotion(f, currentCue != null ? currentCue.spotMotion : MotionMode.SlowSweep, index, spotFixtures.Count, dt);
    }

    private void UpdateRim(FixtureState f, int index, float dt)
    {
        if (!Valid(f)) return;
        float cueGain = currentCue != null ? currentCue.rim : 1f;
        float chase = Mathf.PingPong(frame.barPhase * 2f + index / Mathf.Max(1f, rimFixtures.Count), 1f);
        float target = baseLightIntensity * (0.3f + frame.bass * lighting.bassGlow + chase * 0.3f) * cueGain * lighting.rimGain * lighting.outputGain * masterLevel * (1f - blackoutLevel);
        ApplyLight(f, target, FixtureColor(index * 0.09f, frame.bass, secondaryColor, moodColor), dt);
    }

    private void UpdateChase(FixtureState f, int index, float dt)
    {
        if (!Valid(f)) return;
        float cueGain = currentCue != null ? currentCue.chase : 1f;
        float target = baseLightIntensity * (0.5f + frame.energy * 0.8f + beatEnvelope.value * 0.4f) * cueGain * lighting.chaseGain * lighting.outputGain * masterLevel * (1f - blackoutLevel);
        ApplyLight(f, target, FixtureColor(index * 0.13f + frame.barPhase, frame.synth, accentColor, primaryColor), dt);
        ChaseMotion(f, currentCue != null ? currentCue.chaseMotion : MotionMode.Orbit, index, chaseFixtures.Count, dt);
    }

    private void UpdateLaser(FixtureState f, int index, float dt)
    {
        if (!Valid(f)) return;
        float cueGain = currentCue != null ? currentCue.laser : 0.4f;
        float active = Mathf.Clamp01(laserEnvelope.value * (frame.synth + frame.impact * 0.5f));
        if (frame.isSilent && lighting.laserBlackoutWhenSilent) active = 0f;
        float target = laserIntensity * 2f * active * cueGain * lighting.laserGain * masterLevel * (1f - blackoutLevel);
        ApplyLight(f, target, FixtureColor(index * 0.21f + scannerAngle / 360f, frame.synth, currentPalette != null ? currentPalette.laser : Color.red, accentColor), dt);
        f.light.enabled = target > 0.02f;
        AimMotion(f, currentCue != null ? currentCue.laserMotion : MotionMode.FastSweep, index, laserFixtures.Count, dt);
    }

    private void UpdateStrobe(FixtureState f, int index, float dt)
    {
        if (!Valid(f)) return;
        float cueGain = currentCue != null ? currentCue.strobe : 0f;
        float freq = Mathf.Max(0.01f, lighting.strobeFrequency * (0.5f + frame.energy));
        bool on = strobeEnvelope.value > 0.05f && Mathf.Repeat(strobeTime * freq + index * 0.17f, 1f) < lighting.strobeDutyCycle;
        float target = on ? beatLightIntensity * 2f * cueGain * lighting.strobeGain * masterLevel * (1f - blackoutLevel) : 0f;
        Color c = lighting.forceStrobeWhite || currentPalette == null ? Color.white : Color.Lerp(currentPalette.strobe, flashColor, flashLevel);
        ApplyLight(f, target, c, dt * 4f);
        f.light.enabled = target > 0.05f;
    }

    private void ApplyLight(FixtureState f, float targetIntensity, Color targetColor, float dt)
    {
        float smoothI = 1f - Mathf.Exp(-lighting.intensitySmoothing * Mathf.Max(0f, dt));
        float smoothC = 1f - Mathf.Exp(-lighting.colorSmoothing * Mathf.Max(0f, dt));
        f.currentIntensity = Mathf.Lerp(f.currentIntensity, Mathf.Max(0f, targetIntensity), smoothI);
        f.currentColor = Color.Lerp(f.currentColor, targetColor, smoothC);
        if (flashLevel > 0.001f)
        {
            f.currentColor = Color.Lerp(f.currentColor, flashColor, flashLevel);
            f.currentIntensity += beatLightIntensity * flashLevel;
        }
        f.light.intensity = f.currentIntensity;
        f.light.color = f.currentColor;
        if (lighting.animateRanges) f.light.range = Mathf.Lerp(f.light.range, f.homeRange * (0.8f + frame.energy * 0.6f), smoothI);
        if (lighting.softShadowsOnPeak && frame.energyMode == StageEnergyMode.Peak) f.light.shadows = LightShadows.Soft;
    }

    private Color FixtureColor(float phase, float energy, Color a, Color b)
    {
        float h = Mathf.Repeat(hue + phase * lighting.hueSpread + energy * 0.08f, 1f);
        Color rainbow = Color.HSVToRGB(h, 0.85f, 1f);
        Color c = Color.Lerp(a, b, Mathf.PingPong(phase + frame.barPhase, 1f));
        c = Color.Lerp(c, rainbow, 0.35f);
        return Color.Lerp(c, moodColor, lighting.moodBlend);
    }

    private void AimMotion(FixtureState f, MotionMode mode, int index, int count, float dt)
    {
        Vector3 target = stageCenter;
        float normalized = count <= 1 ? 0f : (float)index / (count - 1);
        float t = Time.time * runtime.motionMaster;
        float ampX = Mathf.Max(1f, stageBounds.size.x * 0.35f);
        float ampZ = Mathf.Max(1f, stageBounds.size.z * 0.35f);
        switch (mode)
        {
            case MotionMode.LockCenter: target += Vector3.up * Mathf.Sin(t + f.phase) * 0.5f; break;
            case MotionMode.SlowSweep: target += new Vector3(Mathf.Sin(t * 0.7f + normalized * Mathf.PI * 2f) * ampX, 0f, Mathf.Cos(t * 0.4f + f.phase) * ampZ * 0.35f); break;
            case MotionMode.FastSweep: target += new Vector3(Mathf.Sin(t * 2.7f + normalized * Mathf.PI * 2f) * ampX, 0f, Mathf.Cos(t * 1.9f + f.phase) * ampZ); break;
            case MotionMode.FigureEight: target += new Vector3(Mathf.Sin(t + f.phase) * ampX, 0f, Mathf.Sin((t + f.phase) * 2f) * ampZ * 0.5f); break;
            case MotionMode.Fan: target += new Vector3(Mathf.Lerp(-ampX, ampX, normalized), 0f, Mathf.Sin(t + f.phase) * ampZ * 0.5f); break;
            case MotionMode.Spiral:
                float r = Mathf.PingPong(t * 0.2f + normalized, 1f);
                target += new Vector3(Mathf.Cos(t * 2f + f.phase) * ampX * r, 0f, Mathf.Sin(t * 2f + f.phase) * ampZ * r);
                break;
            case MotionMode.RandomWalk: target += new Vector3((Mathf.PerlinNoise(t * 0.4f, f.seed) - 0.5f) * ampX * 2f, 0f, (Mathf.PerlinNoise(f.seed, t * 0.4f) - 0.5f) * ampZ * 2f); break;
            case MotionMode.CrossFire: target += new Vector3((index % 2 == 0 ? -ampX : ampX) * Mathf.Sin(t * 0.9f), 0f, Mathf.Cos(t + normalized) * ampZ * 0.4f); break;
            case MotionMode.WaveTilt: target += new Vector3(Mathf.Sin(t * 1.3f + normalized * Mathf.PI * 2f) * ampX, Mathf.Sin(t * 2f + f.phase) * 1.5f, 0f); break;
        }
        AimAt(f, target, dt);
    }

    private void ChaseMotion(FixtureState f, MotionMode mode, int index, int count, float dt)
    {
        if (!lighting.useHomePositions)
        {
            AimMotion(f, mode, index, count, dt);
            return;
        }
        float normalized = index / Mathf.Max(1f, count);
        float angle = chaseAngle + normalized * 360f;
        if (mode == MotionMode.CounterOrbit) angle = -chaseAngle + normalized * 360f;
        float radius = lighting.chaseOrbitRadius > 0f ? lighting.chaseOrbitRadius : Mathf.Max(new Vector2(f.homeLocalPosition.x, f.homeLocalPosition.z).magnitude, 4f);
        float height = lighting.chaseOrbitHeight > 0f ? lighting.chaseOrbitHeight : f.homeLocalPosition.y;
        Vector3 localTarget = f.homeLocalPosition;
        if (mode == MotionMode.Orbit || mode == MotionMode.CounterOrbit) localTarget = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, height + Mathf.Sin(Time.time + f.phase) * frame.energy, Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
        else if (mode == MotionMode.FigureEight) localTarget = new Vector3(Mathf.Sin(Time.time * BeatRate() + f.phase) * radius, height, Mathf.Sin(Time.time * BeatRate() * 2f + f.phase) * radius * 0.5f);
        else if (mode == MotionMode.CrossFire) localTarget = new Vector3(Mathf.Lerp(-radius, radius, normalized), height, Mathf.Sin(Time.time * 2f + f.phase) * radius);
        else if (mode == MotionMode.RandomWalk) localTarget = f.homeLocalPosition + new Vector3((Mathf.PerlinNoise(Time.time, f.seed) - 0.5f) * radius, 0f, (Mathf.PerlinNoise(f.seed, Time.time) - 0.5f) * radius);
        else { AimMotion(f, mode, index, count, dt); return; }
        float smooth = 1f - Mathf.Exp(-lighting.transformSmoothing * dt);
        f.transform.localPosition = Vector3.Lerp(f.transform.localPosition, localTarget, smooth);
        AimAt(f, stageCenter, dt);
    }

    private void AimAt(FixtureState f, Vector3 target, float dt)
    {
        Vector3 direction = target - f.transform.position;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
        f.transform.rotation = Quaternion.Slerp(f.transform.rotation, look, 1f - Mathf.Exp(-lighting.transformSmoothing * dt));
    }

    private void UpdateVfx(float dt)
    {
        if (!vfx.enableVFX) return;
        Color c = moodColor;
        Vector4 color = new Vector4(c.r, c.g, c.b, c.a);
        float master = runtime.vfxMaster * (1f - blackoutLevel);
        float spawn = particleSpawnRate * (0.15f + frame.energy * vfx.maxSpawnMultiplier) * vfx.backgroundGain * master;
        float smoke = smokeDensity * (0.3f + frame.bass * 0.7f) * (currentCue != null ? currentCue.smoke : 1f) * vfx.smokeGain * master;
        float burst = Mathf.Clamp(frame.kick + frame.impact, 0f, vfx.maxBurstStrength) * vfx.burstGain * master;
        float laser = laserEnvelope.value * laserIntensity * vfx.laserBeamGain * master;
        float ring = Mathf.Clamp(frame.bass * vfx.maxRingExpansion, 0f, vfx.maxRingExpansion) * vfx.groundRingGain * master;
        SetVfxFloat(backgroundParticles, "SpawnRate", spawn); SetVfxVector(backgroundParticles, "ParticleColor", color); SetVfxFloat(backgroundParticles, "Energy", frame.energy);
        SetVfxFloat(smokeEffect, "Density", smoke); SetVfxFloat(smokeEffect, "SmokeDensity", smoke); SetVfxVector(smokeEffect, "SmokeColor", color); if (smokeEffect != null) SetVfxVector(smokeEffect, "TransformPosition", smokeEffect.transform.position);
        SetVfxFloat(beatBurstEffect, "BurstStrength", burst); SetVfxVector(beatBurstEffect, "BurstColor", color);
        if (vfx.triggerBeatEvents && frame.isBeat && frame.kick > vfx.beatBurstThreshold) { SendVfxEvent(beatBurstEffect, "OnBeatBurst"); SendVfxEvent(beatBurstEffect, "OnBeat"); }
        SetVfxFloat(laserBeamEffect, "BeamIntensity", laser); SetVfxVector(laserBeamEffect, "BeamColor", color);
        SetVfxFloat(groundRingEffect, "RingExpansion", ring); SetVfxVector(groundRingEffect, "RingColor", color);
        SetVfxFloat(groundRingEffect, "BeamIntensity", ring); SetVfxVector(groundRingEffect, "BeamColor", color);
        if (!vfx.sendCommonParameters) return;
        for (int i = 0; i < allVfx.Count; i++)
        {
            VisualEffect effect = allVfx[i];
            SetVfxFloat(effect, "Kick", frame.kick); SetVfxFloat(effect, "Bass", frame.bass); SetVfxFloat(effect, "Synth", frame.synth); SetVfxFloat(effect, "Energy", frame.energy); SetVfxFloat(effect, "BPM", frame.bpm); SetVfxFloat(effect, "BeatPhase", frame.beatPhase); SetVfxFloat(effect, "BarPhase", frame.barPhase); SetVfxVector(effect, "StageColor", color);
        }
    }

    private void SetVfxFloat(VisualEffect effect, string property, float value)
    {
        if (effect == null) return;
        try
        {
            if (!effect.HasFloat(property))
            {
                return;
            }

            effect.SetFloat(property, value);
        }
        catch (Exception ex) { if (debug.logMissingVfxProperties) Debug.LogWarning(string.Format("{0} VFX float {1} on {2}: {3}", LogPrefix, property, effect.name, ex.Message)); }
    }

    private void SetVfxVector(VisualEffect effect, string property, Vector4 value)
    {
        if (effect == null) return;
        try
        {
            if (!effect.HasVector4(property))
            {
                return;
            }

            effect.SetVector4(property, value);
        }
        catch (Exception ex) { if (debug.logMissingVfxProperties) Debug.LogWarning(string.Format("{0} VFX vector {1} on {2}: {3}", LogPrefix, property, effect.name, ex.Message)); }
    }

    private void SetVfxVector(VisualEffect effect, string property, Vector3 value)
    {
        if (effect == null) return;
        try
        {
            if (!effect.HasVector3(property))
            {
                return;
            }

            effect.SetVector3(property, value);
        }
        catch (Exception ex) { if (debug.logMissingVfxProperties) Debug.LogWarning(string.Format("{0} VFX vector3 {1} on {2}: {3}", LogPrefix, property, effect.name, ex.Message)); }
    }

    private void SendVfxEvent(VisualEffect effect, string eventName)
    {
        if (effect == null) return;
        try { effect.SendEvent(eventName); }
        catch (Exception ex) { if (debug.logMissingVfxProperties) Debug.LogWarning(string.Format("{0} VFX event {1} on {2}: {3}", LogPrefix, eventName, effect.name, ex.Message)); }
    }

    private void UpdateScreens(float dt)
    {
        if (!vj.enableVJ || screens.Count == 0) return;
        if (frame.isSilent && !vj.updateWhenSilent) return;
        screenTimer += dt;
        float interval = 1f / Mathf.Max(1f, vj.updateHz);
        if (screenTimer < interval && !firstFrame) return;
        screenTimer = 0f;
        VJPattern pattern = currentCue != null ? currentCue.pattern : vj.defaultPattern;
        for (int i = 0; i < screens.Count; i++)
        {
            ScreenState screen = screens[i];
            if (screen.renderer == null) continue;
            RenderPattern(screen, pattern, Time.time * vj.patternSpeed + screen.phase);
            ApplyScreen(screen);
        }
    }

    private void RenderPattern(ScreenState screen, VJPattern pattern, float time)
    {
        if (screen.pixels == null || screen.pixels.Length != screen.width * screen.height) screen.pixels = new Color32[screen.width * screen.height];
        switch (pattern)
        {
            case VJPattern.Off: RenderSolid(screen, backgroundColor, 0f); break;
            case VJPattern.SolidPulse: RenderSolidPulse(screen, time); break;
            case VJPattern.SpectrumBars: RenderSpectrumBars(screen, time); break;
            case VJPattern.RadialBloom: RenderRadialBloom(screen, time); break;
            case VJPattern.Tunnel: RenderTunnel(screen, time); break;
            case VJPattern.GridPulse: RenderGridPulse(screen, time); break;
            case VJPattern.MatrixRain: RenderMatrixRain(screen, time); break;
            case VJPattern.GlitchBlocks: RenderGlitchBlocks(screen, time); break;
            case VJPattern.Waveform: RenderWaveform(screen, time); break;
            case VJPattern.Kaleidoscope: RenderKaleidoscope(screen, time); break;
            case VJPattern.HeatMap: RenderHeatMap(screen, time); break;
            case VJPattern.Iris: RenderIris(screen, time); break;
            case VJPattern.Scanner: RenderScanner(screen, time); break;
            case VJPattern.Concentric: RenderConcentric(screen, time); break;
            case VJPattern.PixelStorm: RenderPixelStorm(screen, time); break;
            case VJPattern.ChromaFlow: RenderChromaFlow(screen, time); break;
            case VJPattern.SplitMirror: RenderSplitMirror(screen, time); break;
            case VJPattern.DiagonalCuts: RenderDiagonalCuts(screen, time); break;
            case VJPattern.OrbitingCells: RenderOrbitingCells(screen, time); break;
            case VJPattern.HorizonLines: RenderHorizonLines(screen, time); break;
            default: RenderSpectrumBars(screen, time); break;
        }
    }

    private void ApplyScreen(ScreenState screen)
    {
        if (screen.texture != null && screen.pixels != null)
        {
            screen.texture.SetPixels32(screen.pixels);
            screen.texture.Apply(false, false);
        }
        screen.renderer.GetPropertyBlock(screen.block);
        if (screen.texture != null)
        {
            if (vj.assignMainTexture) screen.block.SetTexture(MainTexId, screen.texture);
            if (vj.assignBaseTexture) screen.block.SetTexture(BaseMapId, screen.texture);
        }
        Color emission = moodColor * vj.emissionGain * runtime.screenMaster * (currentCue != null ? currentCue.screen : 1f) * (1f - blackoutLevel);
        if (flashLevel > 0f) emission = Color.Lerp(emission, flashColor * vj.emissionGain * 2f, flashLevel);
        screen.block.SetColor(EmissionColorId, emission);
        screen.block.SetColor(BaseColorId, Color.white);
        screen.renderer.SetPropertyBlock(screen.block);
    }

    private void RenderSolid(ScreenState s, Color color, float amount)
    {
        Fill(s, ToScreenColor(Color.Lerp(backgroundColor, color, amount)));
    }

    private void RenderSolidPulse(ScreenState s, float time)
    {
        float pulse = Mathf.Clamp01(frame.energy + beatEnvelope.value * vj.beatFlash);
        Color c = Color.Lerp(primaryColor, accentColor, Mathf.PingPong(time * 0.5f + pulse, 1f));
        Fill(s, ToScreenColor(Color.Lerp(backgroundColor, c, 0.25f + pulse * 0.75f)));
    }

    private void RenderSpectrumBars(ScreenState s, float time)
    {
        Fade(s, vj.trailBlend);
        int bars = Mathf.Clamp(16 + Mathf.RoundToInt(frame.synth * 48f), 8, s.width);
        for (int x = 0; x < s.width; x++)
        {
            float u = x / Mathf.Max(1f, s.width - 1f);
            float sample = PseudoSpectrum(u, time, s.seed);
            int height = Mathf.RoundToInt(sample * s.height);
            Color color = PaletteColor(u + time * 0.03f, sample);
            for (int y = 0; y < s.height; y++)
            {
                float v = y / Mathf.Max(1f, s.height - 1f);
                float gate = y < height ? 1f : 0f;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.Abs(v - sample));
                SetPixel(s, x, y, Color.Lerp(backgroundColor, color, gate * (1f - edge * 0.25f)));
            }
        }
    }

    private void RenderRadialBloom(ScreenState s, float time)
    {
        Vector2 center = new Vector2(0.5f + Mathf.Sin(time * 0.37f) * 0.12f, 0.5f + Mathf.Cos(time * 0.29f) * 0.1f);
        float pulse = 0.2f + frame.kick * 0.7f + beatEnvelope.value * 0.4f;
        for (int y = 0; y < s.height; y++)
        {
            float v0 = y / Mathf.Max(1f, s.height - 1f);
            for (int x = 0; x < s.width; x++)
            {
                float u = x / Mathf.Max(1f, s.width - 1f);
                float d = Vector2.Distance(new Vector2(u, v0), center);
                float ring = Mathf.Sin((d * 18f - time * 3f) + pulse * 4f) * 0.5f + 0.5f;
                float glow = Mathf.Clamp01((1f - d * 1.8f) + ring * pulse);
                SetPixel(s, x, y, Color.Lerp(backgroundColor, PaletteColor(d + time * 0.05f, glow), glow));
            }
        }
    }

    private void RenderTunnel(ScreenState s, float time)
    {
        for (int y = 0; y < s.height; y++)
        {
            float v0 = (y / Mathf.Max(1f, s.height - 1f) - 0.5f) * 2f;
            for (int x = 0; x < s.width; x++)
            {
                float u = (x / Mathf.Max(1f, s.width - 1f) - 0.5f) * 2f;
                float d = Mathf.Sqrt(u * u + v0 * v0) + 0.001f;
                float angle = Mathf.Atan2(v0, u) / (Mathf.PI * 2f);
                float tunnel = Mathf.Sin(12f / d + angle * 18f + time * (2f + frame.energy * 4f));
                float value = Mathf.Clamp01(tunnel * 0.5f + 0.5f) * Mathf.Clamp01(1f - d * 0.25f);
                SetPixel(s, x, y, Color.Lerp(backgroundColor, PaletteColor(angle + time * 0.04f, value), value));
            }
        }
    }

    private void RenderGridPulse(ScreenState s, float time)
    {
        float gridScale = Mathf.Lerp(8f, 26f, frame.synth);
        for (int y = 0; y < s.height; y++)
        {
            float v0 = y / Mathf.Max(1f, s.height - 1f);
            for (int x = 0; x < s.width; x++)
            {
                float u = x / Mathf.Max(1f, s.width - 1f);
                float gx = Mathf.Abs(Mathf.Repeat(u * gridScale + time * 0.2f, 1f) - 0.5f);
                float gy = Mathf.Abs(Mathf.Repeat(v0 * gridScale - time * 0.16f, 1f) - 0.5f);
                float line = 1f - Mathf.SmoothStep(0.02f, 0.12f, Mathf.Min(gx, gy));
                float pulse = Mathf.Clamp01(line * (0.4f + frame.bass + beatEnvelope.value));
                SetPixel(s, x, y, Color.Lerp(backgroundColor, PaletteColor(u + v0 + time * 0.03f, pulse), pulse));
            }
        }
    }

    private void RenderMatrixRain(ScreenState s, float time)
    {
        Fade(s, 0.2f + frame.bass * 0.2f);
        for (int x = 0; x < s.width; x++)
        {
            float column = x * 0.071f + s.seed;
            float speed = 0.4f + Mathf.PerlinNoise(column, 3.1f) * 2f + frame.synth * 2f;
            float head = Mathf.Repeat(time * speed + Mathf.PerlinNoise(column, 9.2f), 1f);
            for (int y = 0; y < s.height; y++)
            {
                float v0 = y / Mathf.Max(1f, s.height - 1f);
                float trail = Mathf.Repeat(head - v0 + 1f, 1f);
                float glow = Mathf.SmoothStep(0.12f, 0f, trail) * (0.3f + frame.energy);
                if (glow > 0.02f) BlendPixel(s, x, y, Color.Lerp(secondaryColor, accentColor, glow), glow);
            }
        }
    }

    private void RenderGlitchBlocks(ScreenState s, float time)
    {
        RenderSolid(s, backgroundColor, 0.2f);
        int blockCount = Mathf.Clamp(Mathf.RoundToInt(8 + frame.synth * 80f + beatEnvelope.value * 48f), 4, 120);
        for (int i = 0; i < blockCount; i++)
        {
            float n = Hash01(i * 19.13f + Mathf.Floor(time * (8f + frame.energy * 18f)) + s.seed);
            int x0 = Mathf.FloorToInt(Hash01(n * 17.7f) * s.width);
            int y0 = Mathf.FloorToInt(Hash01(n * 31.1f) * s.height);
            int w = Mathf.Clamp(Mathf.FloorToInt(Hash01(n * 43.3f) * s.width * 0.22f), 1, s.width);
            int h = Mathf.Clamp(Mathf.FloorToInt(Hash01(n * 57.9f) * s.height * 0.18f), 1, s.height);
            Color c = PaletteColor(n + hue, frame.energy + 0.2f);
            for (int y = y0; y < Mathf.Min(s.height, y0 + h); y++) for (int x = x0; x < Mathf.Min(s.width, x0 + w); x++) SetPixel(s, x, y, c);
        }
    }

    private void RenderWaveform(ScreenState s, float time)
    {
        RenderSolid(s, backgroundColor, 0.1f);
        float center = s.height * 0.5f;
        float amp = s.height * (0.1f + frame.energy * 0.35f);
        for (int x = 0; x < s.width; x++)
        {
            float u = x / Mathf.Max(1f, s.width - 1f);
            float wave = Mathf.Sin((u * 12f + time * 4f) * Mathf.PI) * frame.bass;
            wave += Mathf.Sin((u * 29f - time * 7f) * Mathf.PI) * frame.synth * 0.45f;
            int yMid = Mathf.RoundToInt(center + wave * amp);
            for (int thickness = -2; thickness <= 2; thickness++)
            {
                int y = yMid + thickness;
                if (y >= 0 && y < s.height) BlendPixel(s, x, y, PaletteColor(u + time * 0.04f, 1f), 1f - Mathf.Abs(thickness) / 3f);
            }
        }
    }

    private void RenderKaleidoscope(ScreenState s, float time)
    {
        float segments = Mathf.Lerp(4f, 12f, frame.synth);
        for (int y = 0; y < s.height; y++)
        {
            float v0 = (y / Mathf.Max(1f, s.height - 1f) - 0.5f) * 2f;
            for (int x = 0; x < s.width; x++)
            {
                float u = (x / Mathf.Max(1f, s.width - 1f) - 0.5f) * 2f;
                float angle = Mathf.Atan2(v0, u) / (Mathf.PI * 2f);
                float d = Mathf.Sqrt(u * u + v0 * v0);
                float folded = Mathf.Abs(Mathf.Repeat(angle * segments + time * 0.2f, 1f) - 0.5f) * 2f;
                float value = Mathf.Clamp01((1f - d * 0.7f) * (0.4f + folded * 0.6f) + beatEnvelope.value * 0.25f);
                SetPixel(s, x, y, Color.Lerp(backgroundColor, PaletteColor(folded + d + time * 0.03f, value), value));
            }
        }
    }

    private void RenderHeatMap(ScreenState s, float time)
    {
        for (int y = 0; y < s.height; y++)
        {
            float v0 = y / Mathf.Max(1f, s.height - 1f);
            for (int x = 0; x < s.width; x++)
            {
                float u = x / Mathf.Max(1f, s.width - 1f);
                float n = FractalNoise(u * 4f + time * 0.25f, v0 * 4f - time * 0.18f, 4);
                float heat = Mathf.Clamp01(n * 0.6f + frame.bass * 0.5f + frame.kick * 0.25f);
                SetPixel(s, x, y, Color.Lerp(backgroundColor, Color.Lerp(primaryColor, accentColor, heat), heat));
            }
        }
    }

    private void RenderIris(ScreenState s, float time)
    {
        float open = Mathf.Clamp01(0.2f + frame.energy * 0.7f + beatEnvelope.value * 0.3f);
        for (int y = 0; y < s.height; y++)
        {
            float v0 = y / Mathf.Max(1f, s.height - 1f) - 0.5f;
            for (int x = 0; x < s.width; x++)
            {
                float u = x / Mathf.Max(1f, s.width - 1f) - 0.5f;
                float d = Mathf.Sqrt(u * u + v0 * v0) * 2f;
                float angle = Mathf.Atan2(v0, u);
                float blade = Mathf.Sin(angle * 12f + time * 2f) * 0.08f;
                float iris = 1f - Mathf.SmoothStep(open + blade, open + 0.15f + blade, d);
                SetPixel(s, x, y, Color.Lerp(backgroundColor, PaletteColor(angle / (Mathf.PI * 2f) + time * 0.04f, iris), iris));
            }
        }
    }

    private void RenderScanner(ScreenState s, float time)
    {
        Fade(s, 0.25f);
        float pos = Mathf.PingPong(time * (0.4f + frame.synth * 2f), 1f);
        int xLine = Mathf.RoundToInt(pos * (s.width - 1));
        for (int x = 0; x < s.width; x++)
        {
            float dx = Mathf.Abs(x - xLine) / Mathf.Max(1f, s.width);
            float glow = Mathf.SmoothStep(0.08f, 0f, dx) * (0.3f + frame.synth + beatEnvelope.value);
            if (glow <= 0f) continue;
            for (int y = 0; y < s.height; y++) BlendPixel(s, x, y, PaletteColor(y / Mathf.Max(1f, s.height - 1f) + time * 0.05f, glow), glow);
        }
    }

    private void RenderConcentric(ScreenState s, float time)
    {
        for (int y = 0; y < s.height; y++)
        {
            float v0 = y / Mathf.Max(1f, s.height - 1f) - 0.5f;
            for (int x = 0; x < s.width; x++)
            {
                float u = x / Mathf.Max(1f, s.width - 1f) - 0.5f;
                float d = Mathf.Sqrt(u * u + v0 * v0);
                float rings = Mathf.Sin((d * 28f - time * 4f * BeatRate()) + beatEnvelope.value * 4f) * 0.5f + 0.5f;
                float value = Mathf.Clamp01(rings * (1f - d * 1.2f) + frame.bass * 0.25f);
                SetPixel(s, x, y, Color.Lerp(backgroundColor, PaletteColor(d + hue, value), value));
            }
        }
    }

    private void RenderPixelStorm(ScreenState s, float time)
    {
        Fade(s, 0.35f);
        int count = Mathf.RoundToInt(s.width * s.height * Mathf.Lerp(0.015f, 0.16f, frame.energy));
        for (int i = 0; i < count; i++)
        {
            float h = Hash01(i * 37.23f + Mathf.Floor(time * 30f) + s.seed);
            int x = Mathf.FloorToInt(Hash01(h * 91.7f) * s.width);
            int y = Mathf.FloorToInt(Hash01(h * 113.1f) * s.height);
            BlendPixel(s, x, y, PaletteColor(h + hue, Mathf.Clamp01(frame.energy + 0.25f)), 0.5f + frame.energy * 0.5f);
        }
    }

    private void RenderChromaFlow(ScreenState s, float time)
    {
        for (int y = 0; y < s.height; y++)
        {
            float v0 = y / Mathf.Max(1f, s.height - 1f);
            for (int x = 0; x < s.width; x++)
            {
                float u = x / Mathf.Max(1f, s.width - 1f);
                float wave = Mathf.Sin((u * 4f + time) * Mathf.PI * 2f) + Mathf.Sin((v0 * 5f - time * 0.7f) * Mathf.PI * 2f);
                float value = Mathf.Clamp01(wave * 0.25f + 0.5f + frame.energy * 0.3f);
                SetPixel(s, x, y, PaletteColor(u * 0.5f + v0 * 0.5f + time * 0.04f, value));
            }
        }
    }

    private void RenderSplitMirror(ScreenState s, float time)
    {
        int half = s.width / 2;
        for (int y = 0; y < s.height; y++)
        {
            float v0 = y / Mathf.Max(1f, s.height - 1f);
            for (int x = 0; x < half; x++)
            {
                float u = x / Mathf.Max(1f, half - 1f);
                float value = Mathf.Clamp01(FractalNoise(u * 3f + time * 0.4f, v0 * 3f, 3) + beatEnvelope.value * 0.3f);
                Color c = PaletteColor(u + v0 + time * 0.03f, value);
                SetPixel(s, x, y, c);
                SetPixel(s, s.width - 1 - x, y, c);
            }
        }
    }

    private void RenderDiagonalCuts(ScreenState s, float time)
    {
        for (int y = 0; y < s.height; y++)
        {
            float v0 = y / Mathf.Max(1f, s.height - 1f);
            for (int x = 0; x < s.width; x++)
            {
                float u = x / Mathf.Max(1f, s.width - 1f);
                float bands = Mathf.Repeat((u + v0) * (6f + frame.synth * 12f) - time * 1.2f, 1f);
                float cut = 1f - Mathf.SmoothStep(0.1f, 0.38f, bands);
                float value = Mathf.Clamp01(cut * (0.3f + frame.energy));
                SetPixel(s, x, y, Color.Lerp(backgroundColor, PaletteColor(u - v0 + hue, value), value));
            }
        }
    }

    private void RenderOrbitingCells(ScreenState s, float time)
    {
        RenderSolid(s, backgroundColor, 0.12f);
        int cells = 5 + Mathf.RoundToInt(frame.energy * 10f);
        for (int i = 0; i < cells; i++)
        {
            float p = (float)i / Mathf.Max(1, cells);
            float r = 0.12f + 0.35f * Hash01(i * 5.13f + s.seed);
            Vector2 center = new Vector2(0.5f + Mathf.Cos(time * (0.4f + p) + p * Mathf.PI * 2f) * r, 0.5f + Mathf.Sin(time * (0.6f + p) + p * Mathf.PI * 2f) * r);
            float radius = 0.04f + frame.kick * 0.08f + Hash01(p * 17f) * 0.04f;
            DrawSoftCircle(s, center, radius, PaletteColor(p + hue, 1f), 0.8f);
        }
    }

    private void RenderHorizonLines(ScreenState s, float time)
    {
        for (int y = 0; y < s.height; y++)
        {
            float v0 = y / Mathf.Max(1f, s.height - 1f);
            float horizon = Mathf.Abs(v0 - 0.5f);
            float perspective = 1f / Mathf.Max(0.05f, horizon * 4f);
            for (int x = 0; x < s.width; x++)
            {
                float u = x / Mathf.Max(1f, s.width - 1f) - 0.5f;
                float line = Mathf.Repeat((horizon * 18f * perspective) - time * (0.5f + frame.bass), 1f);
                float side = Mathf.Repeat((Mathf.Abs(u) * 16f * perspective) + time * 0.2f, 1f);
                float value = Mathf.Max(1f - Mathf.SmoothStep(0.02f, 0.12f, line), 1f - Mathf.SmoothStep(0.02f, 0.08f, side));
                value *= Mathf.Clamp01(1f - horizon * 1.3f + frame.energy * 0.5f);
                SetPixel(s, x, y, Color.Lerp(backgroundColor, PaletteColor(u + v0 + time * 0.02f, value), value));
            }
        }
    }

    private void Fill(ScreenState s, Color32 color)
    {
        for (int i = 0; i < s.pixels.Length; i++) s.pixels[i] = color;
    }

    private void Fade(ScreenState s, float amount)
    {
        amount = Mathf.Clamp01(amount);
        Color32 bg = ToScreenColor(backgroundColor);
        for (int i = 0; i < s.pixels.Length; i++) s.pixels[i] = ToScreenColor(Color.Lerp(s.pixels[i], bg, amount));
    }

    private void SetPixel(ScreenState s, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= s.width || y >= s.height) return;
        s.pixels[y * s.width + x] = ToScreenColor(color);
    }

    private void BlendPixel(ScreenState s, int x, int y, Color color, float amount)
    {
        if (x < 0 || y < 0 || x >= s.width || y >= s.height) return;
        int index = y * s.width + x;
        s.pixels[index] = ToScreenColor(Color.Lerp(s.pixels[index], color, Mathf.Clamp01(amount)));
    }

    private void DrawSoftCircle(ScreenState s, Vector2 center, float radius, Color color, float intensity)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt((center.x - radius) * s.width));
        int maxX = Mathf.Min(s.width - 1, Mathf.CeilToInt((center.x + radius) * s.width));
        int minY = Mathf.Max(0, Mathf.FloorToInt((center.y - radius) * s.height));
        int maxY = Mathf.Min(s.height - 1, Mathf.CeilToInt((center.y + radius) * s.height));
        for (int y = minY; y <= maxY; y++)
        {
            float v0 = y / Mathf.Max(1f, s.height - 1f);
            for (int x = minX; x <= maxX; x++)
            {
                float u = x / Mathf.Max(1f, s.width - 1f);
                BlendPixel(s, x, y, color, Mathf.SmoothStep(radius, 0f, Vector2.Distance(new Vector2(u, v0), center)) * intensity);
            }
        }
    }

    private float PseudoSpectrum(float u, float time, float seed)
    {
        float low = Mathf.Exp(-u * 4f) * frame.bass;
        float mid = Mathf.Exp(-Mathf.Abs(u - 0.35f) * 5f) * frame.kick;
        float high = Mathf.SmoothStep(0.25f, 1f, u) * frame.synth;
        float noise = FractalNoise(u * 12f + seed, time * 0.8f, 3) * 0.25f;
        float beat = beatEnvelope.value * Mathf.Exp(-Mathf.Abs(u - 0.12f) * 8f);
        return Mathf.Clamp01(low + mid + high + noise + beat);
    }

    private Color PaletteColor(float t, float amount)
    {
        if (currentPalette != null) return currentPalette.Evaluate(t + hue, Mathf.Clamp01(amount), beatEnvelope.value);
        return Color.Lerp(primaryColor, accentColor, Mathf.PingPong(t, 1f));
    }

    private Color32 ToScreenColor(Color color)
    {
        color = Color.Lerp(backgroundColor, color, Mathf.Clamp01(1f - blackoutLevel));
        color = ApplyContrast(color, vj.screenContrast);
        color = StagePalette.ApplySaturationValue(color, vj.screenSaturation, 1f);
        color.r = Mathf.Clamp01(color.r + vj.blackLevel);
        color.g = Mathf.Clamp01(color.g + vj.blackLevel);
        color.b = Mathf.Clamp01(color.b + vj.blackLevel);
        color.a = 1f;
        return color;
    }

    private Color ApplyContrast(Color color, float contrast)
    {
        contrast = Mathf.Max(0f, contrast);
        color.r = Mathf.Clamp01((color.r - 0.5f) * contrast + 0.5f);
        color.g = Mathf.Clamp01((color.g - 0.5f) * contrast + 0.5f);
        color.b = Mathf.Clamp01((color.b - 0.5f) * contrast + 0.5f);
        return color;
    }

    private float FractalNoise(float x, float y, int octaves)
    {
        float value = 0f;
        float amp = 0.5f;
        float freq = 1f;
        for (int i = 0; i < octaves; i++)
        {
            value += Mathf.PerlinNoise(x * freq, y * freq) * amp;
            freq *= 2f;
            amp *= 0.5f;
        }
        return Mathf.Clamp01(value);
    }

    private float Hash01(float n)
    {
        return Mathf.Repeat(Mathf.Sin(n * 12.9898f) * 43758.5453f, 1f);
    }

    private void UpdateEnvironment(float dt)
    {
        if (!environment.enableDynamicEnvironment) return;
        if (environment.pulseStageFloor && stageFloor != null)
        {
            Color floorColor = Color.Lerp(backgroundColor, moodColor, environment.floorBaseColorBlend);
            float emission = environment.floorEmissionGain * runtime.environmentMaster * (0.25f + frame.bass + beatEnvelope.value * 0.5f) * (1f - blackoutLevel);
            stageFloor.GetPropertyBlock(rendererBlock);
            rendererBlock.SetColor(BaseColorId, floorColor);
            rendererBlock.SetColor(ColorId, floorColor);
            rendererBlock.SetColor(EmissionColorId, floorColor * emission);
            rendererBlock.SetFloat(MetallicId, environment.floorMetallic);
            rendererBlock.SetFloat(SmoothnessId, environment.floorSmoothness);
            stageFloor.SetPropertyBlock(rendererBlock);
        }
        if (discoBall != null)
        {
            if (environment.rotateDiscoBall)
            {
                float degrees = environment.discoRotationDegreesPerBeat * BeatRate() * (0.3f + frame.energy) * dt;
                discoBall.transform.Rotate(Vector3.up, degrees, Space.World);
                discoBall.transform.Rotate(Vector3.right, degrees * 0.13f, Space.Self);
            }
            if (environment.pulseDiscoBallScale)
            {
                float scale = 1f + beatEnvelope.value * environment.discoScalePulse;
                discoBall.transform.localScale = Vector3.Lerp(discoBall.transform.localScale, Vector3.one * scale, 1f - Mathf.Exp(-8f * dt));
            }
        }
        if (environment.controlRenderSettings)
        {
            RenderSettings.fog = true;
            Color targetFog = Color.Lerp(backgroundColor, moodColor, environment.renderSettingsBlend);
            float fogDensity = environment.fogBaseDensity + frame.bass * environment.fogEnergyDensity;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, 1f - Mathf.Exp(-2f * dt));
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, fogDensity, 1f - Mathf.Exp(-2f * dt));
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, moodColor * environment.ambientIntensity, 1f - Mathf.Exp(-2f * dt));
            RenderSettings.ambientIntensity = Mathf.Lerp(RenderSettings.ambientIntensity, environment.ambientIntensity, 1f - Mathf.Exp(-2f * dt));
        }
    }

    private bool Valid(FixtureState f)
    {
        return f != null && f.light != null && f.transform != null;
    }

    private bool IsEmpty(Array array)
    {
        return array == null || array.Length == 0;
    }

    private float BeatRate()
    {
        return Mathf.Max(0.1f, frame.bpm / 60f);
    }

    private Color RoleColor(FixtureRole role)
    {
        if (role == FixtureRole.Spotlight) return Color.yellow;
        if (role == FixtureRole.Rim) return Color.magenta;
        if (role == FixtureRole.Chase) return Color.cyan;
        if (role == FixtureRole.Laser) return Color.red;
        if (role == FixtureRole.Strobe) return Color.white;
        return Color.gray;
    }

    public enum StageEnergyMode { Silence, Calm, Groove, Drive, Peak }
    public enum FixtureRole { Unknown, Spotlight, Rim, Chase, Laser, Strobe, Wash, Practical }
    public enum VJPattern { Off, SolidPulse, SpectrumBars, RadialBloom, Tunnel, GridPulse, MatrixRain, GlitchBlocks, Waveform, Kaleidoscope, HeatMap, Iris, Scanner, Concentric, PixelStorm, ChromaFlow, SplitMirror, DiagonalCuts, OrbitingCells, HorizonLines }
    public enum MotionMode { Static, LockCenter, SlowSweep, FastSweep, Orbit, CounterOrbit, FigureEight, Fan, Spiral, RandomWalk, CrossFire, WaveTilt }
    public enum StageCueFamily { Intro, Groove, Build, Drop, Breakdown, Peak, Ambient, Manual }

    [Serializable]
    public class StageRuntimeSettings
    {
        public bool enableSystem = true;
        public bool autoFindAudioVisualizer = true;
        public bool autoDiscoverScene = true;
        public bool rebuildCacheEveryFrame = false;
        public bool createRuntimeTextures = true;
        public bool restoreRenderSettingsOnDisable = true;
        public bool keepAnimatingWhenSilent = true;
        public bool forceEnableActiveSpotlights = true;
        public bool useUnscaledTime = false;
        public bool updateOnZeroDeltaTime = false;
        [Range(0f, 4f)] public float masterIntensity = 1f;
        [Range(0f, 4f)] public float screenMaster = 1f;
        [Range(0f, 4f)] public float vfxMaster = 1f;
        [Range(0f, 4f)] public float motionMaster = 1f;
        [Range(0f, 4f)] public float environmentMaster = 1f;
        [Range(0.01f, 0.5f)] public float maxDeltaTime = 0.08f;
        [Range(0.1f, 20f)] public float masterSmoothing = 8f;
        [Range(0f, 2f)] public float silenceFloor = 0.08f;
        [Range(0f, 5f)] public float idleAnimationSpeed = 0.35f;
        [Range(0.01f, 1f)] public float blackoutFadeSpeed = 0.2f;
    }

    [Serializable]
    public class StageAudioSettings
    {
        public bool useManualInput = false;
        public bool compressIncomingEnergy = true;
        [Range(40f, 240f)] public float fallbackBpm = 120f;
        [Range(0f, 30f)] public float kickGain = 12f;
        [Range(0f, 30f)] public float bassGain = 10f;
        [Range(0f, 30f)] public float synthGain = 9f;
        [Range(0.1f, 4f)] public float energyGamma = 0.75f;
        [Range(0f, 1f)] public float kickWeight = 0.38f;
        [Range(0f, 1f)] public float bassWeight = 0.34f;
        [Range(0f, 1f)] public float synthWeight = 0.28f;
        [Range(0.1f, 40f)] public float attackSpeed = 18f;
        [Range(0.1f, 40f)] public float releaseSpeed = 7f;
        [Range(0f, 1f)] public float beatEnergyBoost = 0.35f;
        [Range(0f, 1f)] public float silenceExitEnergy = 0.025f;
        [Range(0f, 1f)] public float peakEnergy = 0.82f;
        [Range(0f, 1f)] public float driveEnergy = 0.55f;
        [Range(0f, 1f)] public float grooveEnergy = 0.25f;
        [Range(0f, 1f)] public float dropRiseThreshold = 0.28f;
    }

    [Serializable]
    public class StageLightingSettings
    {
        public bool enableLighting = true;
        public bool useHomePositions = true;
        public bool animateSpotAngles = true;
        public bool animateRanges = true;
        public bool laserBlackoutWhenSilent = true;
        public bool allowStrobe = true;
        public bool forceStrobeWhite = false;
        public bool softShadowsOnPeak = true;
        [Range(0f, 4f)] public float outputGain = 1f;
        [Range(0f, 2f)] public float spotGain = 1f;
        [Range(0f, 2f)] public float rimGain = 1f;
        [Range(0f, 2f)] public float chaseGain = 1f;
        [Range(0f, 3f)] public float laserGain = 1f;
        [Range(0f, 3f)] public float strobeGain = 1f;
        [Range(0f, 2f)] public float beatPunch = 0.8f;
        [Range(0f, 2f)] public float bassGlow = 0.6f;
        [Range(0f, 2f)] public float moodBlend = 0.45f;
        [Range(0f, 2f)] public float hueSpread = 0.35f;
        [Range(0.1f, 40f)] public float intensitySmoothing = 12f;
        [Range(0.1f, 40f)] public float colorSmoothing = 8f;
        [Range(0.1f, 40f)] public float transformSmoothing = 6f;
        [Range(0f, 20f)] public float chaseOrbitRadius = 6f;
        [Range(0f, 20f)] public float chaseOrbitHeight = 6f;
        [Range(0f, 30f)] public float strobeFrequency = 12f;
        [Range(0f, 1f)] public float strobeDutyCycle = 0.42f;
        [Range(0f, 1f)] public float laserHold = 0.22f;
        [Range(0f, 1f)] public float strobeHold = 0.18f;
        [Range(1f, 120f)] public float minSpotAngle = 18f;
        [Range(1f, 120f)] public float maxSpotAngle = 62f;
    }

    [Serializable]
    public class StageVJSettings
    {
        public bool enableVJ = true;
        public bool updateWhenSilent = true;
        public bool assignMainTexture = true;
        public bool assignBaseTexture = true;
        public VJPattern defaultPattern = VJPattern.SpectrumBars;
        [Range(16, 256)] public int textureWidth = 96;
        [Range(8, 144)] public int textureHeight = 54;
        [Range(1f, 60f)] public float updateHz = 24f;
        [Range(0f, 10f)] public float emissionGain = 3f;
        [Range(0f, 4f)] public float screenContrast = 1.2f;
        [Range(0f, 2f)] public float screenSaturation = 1f;
        [Range(0f, 2f)] public float patternSpeed = 1f;
        [Range(0f, 2f)] public float beatFlash = 0.35f;
        [Range(0f, 1f)] public float blackLevel = 0.02f;
        [Range(0f, 1f)] public float trailBlend = 0.08f;
    }

    [Serializable]
    public class StageVFXSettings
    {
        public bool enableVFX = true;
        public bool autoCompleteGraphBindings = true;
        public bool createMissingGraphObjects = true;
        public bool markSceneDirtyWhenAutoCompleted = true;
        public bool sendCommonParameters = true;
        public bool triggerBeatEvents = true;
        public bool logMissingProperties = false;
        [Range(0f, 4f)] public float backgroundGain = 1f;
        [Range(0f, 4f)] public float smokeGain = 1f;
        [Range(0f, 4f)] public float burstGain = 1f;
        [Range(0f, 4f)] public float laserBeamGain = 1f;
        [Range(0f, 4f)] public float groundRingGain = 1f;
        [Range(0f, 1f)] public float beatBurstThreshold = 0.25f;
        [Range(0f, 10f)] public float maxSpawnMultiplier = 4f;
        [Range(0f, 10f)] public float maxBurstStrength = 5f;
        [Range(0f, 10f)] public float maxRingExpansion = 8f;
    }

    [Serializable]
    public class StageDirectorSettings
    {
        public bool enableAutoDirector = true;
        public bool useGeneratedCueLibrary = true;
        public bool allowCueRepeats = false;
        public bool changeCueOnPhrase = true;
        public bool changeCueOnDrop = true;
        [Range(1, 64)] public int phraseLengthBeats = 16;
        [Range(1f, 128f)] public float minimumCueBeats = 8f;
        [Range(1f, 256f)] public float maximumCueBeats = 48f;
        [Range(0f, 1f)] public float randomCueWeight = 0.18f;
        [Range(0f, 1f)] public float noveltyWeight = 0.2f;
        [Range(0f, 1f)] public float modeMatchWeight = 0.12f;
        [Range(0f, 1f)] public float dropCueBias = 0.25f;
        [Range(0.01f, 8f)] public float cueCrossfadeSeconds = 1.2f;
        [Range(0.01f, 8f)] public float paletteCrossfadeSeconds = 1.6f;
    }

    [Serializable]
    public class StageEnvironmentSettings
    {
        public bool enableDynamicEnvironment = true;
        public bool controlRenderSettings = false;
        public bool pulseStageFloor = true;
        public bool rotateDiscoBall = true;
        public bool pulseDiscoBallScale = true;
        public bool useStageBoundsForTarget = true;
        [Range(0f, 5f)] public float floorEmissionGain = 1.8f;
        [Range(0f, 1f)] public float floorBaseColorBlend = 0.4f;
        [Range(0f, 1f)] public float floorMetallic = 0.7f;
        [Range(0f, 1f)] public float floorSmoothness = 0.85f;
        [Range(0f, 720f)] public float discoRotationDegreesPerBeat = 90f;
        [Range(0f, 2f)] public float discoScalePulse = 0.08f;
        [Range(0f, 1f)] public float fogBaseDensity = 0.01f;
        [Range(0f, 1f)] public float fogEnergyDensity = 0.03f;
        [Range(0f, 8f)] public float ambientIntensity = 0.7f;
        [Range(0f, 1f)] public float renderSettingsBlend = 0.35f;
    }

    [Serializable]
    public class StageAutomationSlot
    {
        public string label = "Cue";
        public KeyCode key = KeyCode.None;
        public string cueName = "";
        public VJPattern pattern = VJPattern.Off;
        public bool triggerFlash = false;
        public bool toggleBlackout = false;
        public Color flashColor = Color.white;
        [Range(0f, 2f)] public float flashStrength = 1f;
        [Range(0.03f, 2f)] public float flashDuration = 0.18f;
    }

    [Serializable]
    public class StageDebugSettings
    {
        public bool logLifecycle = true;
        public bool logCueChanges = false;
        public bool logMissingVfxProperties = false;
        public bool showRuntimeOverlay = false;
        public bool drawGizmos = true;
        public bool drawFixtureRays = true;
        [Range(0.1f, 10f)] public float gizmoScale = 1f;
        [Range(10, 32)] public int overlayFontSize = 16;
    }

    [Serializable]
    public class StageAudioFrame
    {
        public float kick;
        public float bass;
        public float synth;
        public float energy;
        public float previousEnergy;
        public float impact;
        public float brightness;
        public float warmth;
        public float bpm;
        public float beatPhase;
        public float barPhase;
        public float beatStrength;
        public float secondsSinceBeat;
        public bool isBeat;
        public bool isSilent = true;
        public bool hasAudio;
        public bool isDrop;
        public bool isBuild;
        public string key = "Unknown";
        public string mode = "Unknown";
        public StageEnergyMode energyMode = StageEnergyMode.Silence;
        public void CopyFrom(StageAudioFrame other)
        {
            kick = other.kick; bass = other.bass; synth = other.synth; energy = other.energy; previousEnergy = other.previousEnergy; impact = other.impact; brightness = other.brightness; warmth = other.warmth; bpm = other.bpm; beatPhase = other.beatPhase; barPhase = other.barPhase; beatStrength = other.beatStrength; secondsSinceBeat = other.secondsSinceBeat; isBeat = other.isBeat; isSilent = other.isSilent; hasAudio = other.hasAudio; isDrop = other.isDrop; isBuild = other.isBuild; key = other.key; mode = other.mode; energyMode = other.energyMode;
        }
    }

    public class FixtureState
    {
        public Light light;
        public Transform transform;
        public FixtureRole role;
        public int index;
        public int roleIndex;
        public float phase;
        public float seed;
        public float homeIntensity;
        public float homeRange;
        public float homeSpotAngle;
        public float currentIntensity;
        public Color homeColor;
        public Color currentColor;
        public Vector3 homeLocalPosition;
        public Vector3 homeWorldPosition;
        public Quaternion homeLocalRotation;
        public Quaternion homeWorldRotation;
    }

    public class ScreenState
    {
        public Renderer renderer;
        public MaterialPropertyBlock block;
        public Texture2D texture;
        public Color32[] pixels;
        public int width;
        public int height;
        public int index;
        public float phase;
        public float seed;
    }

    [Serializable]
    public class StagePalette
    {
        public string name = "Palette";
        public Color primary = Color.white;
        public Color secondary = Color.cyan;
        public Color accent = Color.magenta;
        public Color background = Color.black;
        public Color laser = Color.red;
        public Color strobe = Color.white;
        [Range(0f, 2f)] public float saturation = 1f;
        [Range(0f, 2f)] public float value = 1f;
        [Range(0f, 2f)] public float contrast = 1f;
        [Range(0f, 1f)] public float moodWeight = 0.4f;
        public Color Evaluate(float t, float energy, float beat)
        {
            t = Mathf.Repeat(t, 1f);
            Color a = Color.Lerp(primary, secondary, Mathf.SmoothStep(0f, 1f, Mathf.PingPong(t * 2f, 1f)));
            Color b = Color.Lerp(background, accent, Mathf.Clamp01(energy * contrast + beat * 0.35f));
            return ApplySaturationValue(Color.Lerp(a, b, 0.35f + energy * 0.25f), saturation, value);
        }
        public static Color ApplySaturationValue(Color color, float saturationScale, float valueScale)
        {
            float h; float s; float v;
            Color.RGBToHSV(color, out h, out s, out v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * saturationScale), Mathf.Clamp01(v * valueScale));
        }
    }

    [Serializable]
    public class StageCue
    {
        public string name = "Cue";
        public StageCueFamily family = StageCueFamily.Groove;
        public StageEnergyMode preferredEnergy = StageEnergyMode.Groove;
        public VJPattern pattern = VJPattern.SpectrumBars;
        public MotionMode spotMotion = MotionMode.SlowSweep;
        public MotionMode chaseMotion = MotionMode.Orbit;
        public MotionMode laserMotion = MotionMode.FastSweep;
        public int paletteIndex = 0;
        public float durationBeats = 16f;
        public float minEnergy = 0f;
        public float maxEnergy = 1f;
        public float probability = 1f;
        public float spotlight = 1f;
        public float rim = 1f;
        public float chase = 1f;
        public float laser = 0.4f;
        public float strobe = 0.2f;
        public float smoke = 0.4f;
        public float particles = 0.5f;
        public float screen = 1f;
        public float floor = 0.7f;
        public float motion = 1f;
        public float colorSpeed = 1f;
        public float beatDensity = 1f;
        public float blackout = 0f;
        public bool preferMajor = false;
        public bool preferMinor = false;
        public bool allowDuringSilence = false;
        public bool useRandomAccent = false;
        public float Score(StageAudioFrame frame, StageDirectorSettings settings, float randomValue)
        {
            if (frame.isSilent && !allowDuringSilence) return 0f;
            float energy = frame.energy;
            if (energy < minEnergy || energy > maxEnergy)
            {
                float distance = energy < minEnergy ? minEnergy - energy : energy - maxEnergy;
                if (distance > 0.25f) return 0f;
            }
            float score = Mathf.Max(0.01f, probability);
            score += 1f - Mathf.Clamp01(Mathf.Abs(energy - EnergyCenter(preferredEnergy)) * 2.2f);
            if (frame.isDrop && (family == StageCueFamily.Drop || family == StageCueFamily.Peak)) score += settings.dropCueBias;
            if (preferMajor && string.Equals(frame.mode, "Major", StringComparison.OrdinalIgnoreCase)) score += settings.modeMatchWeight;
            if (preferMinor && string.Equals(frame.mode, "Minor", StringComparison.OrdinalIgnoreCase)) score += settings.modeMatchWeight;
            score += randomValue * settings.randomCueWeight;
            return Mathf.Max(0f, score);
        }
        private static float EnergyCenter(StageEnergyMode mode)
        {
            if (mode == StageEnergyMode.Silence) return 0.02f;
            if (mode == StageEnergyMode.Calm) return 0.16f;
            if (mode == StageEnergyMode.Groove) return 0.38f;
            if (mode == StageEnergyMode.Drive) return 0.65f;
            return 0.9f;
        }
    }

    public class BeatClock
    {
        public float bpm = 120f;
        public float beatDuration = 0.5f;
        public float phase;
        public float barPhase;
        public float secondsSinceBeat;
        public int beatIndex;
        public int phraseIndex;
        public bool beatPulse;
        public void Update(float dt, float targetBpm, bool externalBeat, int phraseLength)
        {
            bpm = Mathf.Clamp(targetBpm > 0f ? targetBpm : bpm, 40f, 240f);
            beatDuration = 60f / Mathf.Max(1f, bpm);
            secondsSinceBeat += dt;
            beatPulse = false;
            if (externalBeat || secondsSinceBeat >= beatDuration)
            {
                int skipped = Mathf.Max(1, Mathf.FloorToInt(secondsSinceBeat / Mathf.Max(0.001f, beatDuration)));
                beatIndex += skipped;
                phraseIndex = phraseLength > 0 ? beatIndex / phraseLength : 0;
                secondsSinceBeat = externalBeat ? 0f : Mathf.Repeat(secondsSinceBeat, beatDuration);
                beatPulse = true;
            }
            phase = beatDuration > 0f ? Mathf.Clamp01(secondsSinceBeat / beatDuration) : 0f;
            int beatInPhrase = phraseLength > 0 ? beatIndex % phraseLength : 0;
            barPhase = phraseLength > 0 ? (beatInPhrase + phase) / phraseLength : phase;
        }
        public void ForceBeat(int phraseLength)
        {
            secondsSinceBeat = 0f;
            beatIndex++;
            phraseIndex = phraseLength > 0 ? beatIndex / phraseLength : 0;
            beatPulse = true;
        }
    }

    public class Envelope
    {
        public float value;
        public void Update(float target, float attack, float release, float dt)
        {
            float speed = target > value ? attack : release;
            value = Mathf.Lerp(value, target, 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * dt));
        }
        public void Punch(float amount)
        {
            value = Mathf.Clamp01(Mathf.Max(value, amount));
        }
    }

    public class RandomDeck
    {
        private int state;
        public RandomDeck(int seed) { state = seed == 0 ? 1 : seed; }
        public float Next01()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            uint unsigned = unchecked((uint)state);
            return (unsigned & 0x00FFFFFF) / 16777215f;
        }
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + Mathf.FloorToInt(Next01() * (maxExclusive - minInclusive));
        }
    }

    public class RenderSettingsSnapshot
    {
        public bool fog;
        public Color fogColor;
        public float fogDensity;
        public Color ambientLight;
        public float ambientIntensity;
    }
}
