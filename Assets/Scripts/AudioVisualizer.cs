using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class AudioVisualizer : MonoBehaviour
{
    public bool movingBars;

    [Tooltip("FFT 数据的时域平滑权重（0=无平滑，趋近1=极度平滑）")]
    public float smoothingWeight = 0.5f;

    public GameObject barPrefab;

    [Tooltip("柱状条的总数量")]
    public int barCount = 64;

    [Tooltip("柱状条组的父级 Transform，用于定位")]
    public Transform barPosition;

    [Tooltip("柱状条之间的水平间距")]
    public float horizontalScale = 0.01f;

    [Tooltip("柱状条高度的缩放系数")]
    public float verticalScale = 1f;

    [Tooltip("柱状条排列椭圆的半长轴 a（控制宽度）")]
    public float a = 5;

    [Tooltip("柱状条排列椭圆的半短轴 b（控制深度）")]
    public float b = 1;

    // ==================== 新增：对数压缩参数 ====================
    [Header("对数压缩设置")]
    [Tooltip("对数压缩强度（值越大压缩越明显，建议 1.0~3.0）")]
    [Range(0.1f, 100f)]
    public float logCompressionStrength = 1.5f;

    [Tooltip("对数压缩偏移量（避免 log(0)，建议 0.01~1.0）")]
    [Range(0.001f, 2f)]
    public float logCompressionOffset = 0.1f;

    [Tooltip("柱状条高度最大值限制")]
    public float maxBarHeight = 10f;

    [Tooltip("柱状条高度最小值（确保可见性）")]
    public float minBarHeight = 0.05f;

    [Tooltip("启用动态范围自动调整")]
    public bool enableDynamicRange = true;

    [Tooltip("动态范围调整速度")]
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

    [Header("发光柱状条设置")]
    [Tooltip("启用柱状条发光材质和随音频变化的颜色")]
    public bool enableBarGlow = true;

    [Tooltip("可选：指定柱状条发光材质。为空时运行时自动创建 URP/Lit 发光材质")]
    public Material barGlowMaterial;

    [Tooltip("静音或低能量时的基础发光强度")]
    [Range(0f, 8f)]
    public float baseBarEmissionIntensity = 0.8f;

    [Tooltip("音频能量映射到发光强度的倍率")]
    [Range(0f, 30f)]
    public float audioBarEmissionIntensity = 8f;

    [Tooltip("检测到节拍时额外增加的发光强度")]
    [Range(0f, 20f)]
    public float beatBarEmissionBoost = 4f;

    [Tooltip("颜色随时间循环的速度")]
    [Range(0f, 2f)]
    public float barHueCycleSpeed = 0.16f;

    [Tooltip("颜色/亮度响应速度")]
    [Range(1f, 30f)]
    public float barGlowSmoothingSpeed = 12f;

    [Tooltip("频段颜色梯度强度，值越大不同柱状条色差越明显")]
    [Range(0f, 1f)]
    public float barFrequencyHueSpread = 0.42f;

    [Tooltip("Kick 能量对整体色相的影响")]
    [Range(0f, 1f)]
    public float kickHueInfluence = 0.16f;

    [Tooltip("Synth 能量对整体色相的影响")]
    [Range(0f, 1f)]
    public float synthHueInfluence = 0.22f;

    // ==================== 改进的BPM检测字段 ====================

    private Queue<float> kickEnergyHistory = new Queue<float>();
    private Queue<float> snareEnergyHistory = new Queue<float>();
    private Queue<float> bassEnergyHistory = new Queue<float>();

    [Tooltip("用于计算自适应阈值的能量历史窗口大小（帧数）")]
    public int energyHistorySize = 50;

    [Tooltip("记录最近若干节拍的间隔时间（秒），用于BPM计算")]
    public List<float> beatTimestamps = new List<float>();

    private List<float> beatConfidences = new List<float>();

    [Tooltip("上一次检测到节拍的时间戳（Time.time）")]
    public float lastBeatTime = 0f;

    [Tooltip("上一次更新BPM的时间戳（Time.time）")]
    public float lastBpmUpdateTime = 0f;

    public bool useKalmanEstimate = true;

    public float detectedBPM = 0f;

    public float limitedBPM = 0f;

    private float bpmVariance = 0f;

    [Tooltip("基于历史能量均值和标准差动态计算的 Kick 鼓触发阈值")]
    public float dynamicKickThreshold;

    [Tooltip("基于历史能量均值和标准差动态计算的 Snare 鼓触发阈值")]
    public float dynamicSnareThreshold;

    private float energyStdDev = 0f;
    private float predictedNextBeat = 0f;
    private float phaseError = 0f;

    [Tooltip("BPM 更新的时间间隔（秒），数值越小响应越快但越不稳定")]
    public float bpmUpdateInterval = 0.5f;

    [Tooltip("两次有效节拍之间的最小间隔（秒），对应最大 BPM 约 200")]
    public float minBeatInterval = 0.25f;


    [Tooltip("节拍硬冷却时间（秒）。触发后此窗口内的 onset 全部忽略，防止鼓击瞬态衰减被重复记录。建议为 minBeatInterval * 1.5")]
    public float beatCooldown = 0.35f;
    [Tooltip("两次有效节拍之间的最大间隔（秒），对应最小 BPM 约 50")]
    public float maxBeatInterval = 1.2f;

    [Tooltip("当前节拍间隔（秒），由 limitedBPM 自动推算")]
    public float beatInterval = 0.5f;

    [Tooltip("是否正在显示节拍提示文字（在 OnGUI 中控制 BEAT 字样的闪烁）")]
    public bool showBeatText = false;

    [Tooltip("节拍文字的显示持续时间（秒），自动设为节拍间隔的 1/4")]
    public float beatDisplayTime = 0.2f;

    private float beatTimer = 0f;

    [Tooltip("节拍被接受所需的最低置信度（0~1），系统会根据BPM稳定性动态调整")]
    [Range(0f, 1f)]
    public float minBeatConfidence = 0.4f;

    private float previousKickEnergy = 0f;
    private float previousSnareEnergy = 0f;
    private float previousBassEnergy = 0f;
    private float playStartTime = 0f;
    private float silenceStartTime = 0f;

    [Header("调试显示设置")]
    [Tooltip("当 AudioCaptureCSCore 没有显示合并调试面板时，单独显示 AudioVisualizer 状态")]
    public bool showStandaloneStatusOverlay = true;

    [Header("静音检测设置")]
    [Tooltip("低于该宽频能量并持续一段时间后，才判定进入静音")]
    public float silenceEnterThreshold = 0.001f;

    [Tooltip("高于该宽频能量并持续一段时间后，才判定恢复播放。应大于进入阈值，避免状态抖动")]
    public float silenceExitThreshold = 0.003f;

    [Tooltip("进入静音前需要连续低能量的时间（秒）")]
    public float silenceEnterDelay = 0.25f;

    [Tooltip("退出静音前需要连续高能量的时间（秒）")]
    public float silenceExitDelay = 0.05f;

    [Tooltip("静音能量平滑速度。值越大响应越快，值越小越抗抖动")]
    [Range(1f, 30f)]
    public float silenceEnergySmoothSpeed = 18f;

    [Tooltip("低能量但非静音时的判定倍率，用于临时降低节拍置信度")]
    public float lowEnergyThresholdMultiplier = 4f;

    private float smoothedSilenceEnergy = 0f;
    private float pendingSilenceStartTime = -1f;
    private float pendingSoundStartTime = -1f;
    public bool wasSilent = true;
    private List<float> beatStrengths = new List<float>();

    [Tooltip("Onset 灵敏度系数，值越大需要更强的能量突变才能触发节拍（1.0=宽松，3.0=严格）")]
    [Range(0.3f, 3.0f)]
    public float onsetSensitivity = 1.5f;

    [Tooltip("Kick 阈值平滑速度（值越大变化越快，1=慢速平滑，10=快速响应）")]
    [Range(1f, 100f)]
    public float dynamicKickThresholdSpeed = 2f;

    [Tooltip("Snare 阈值平滑速度（值越大变化越快，1=慢速平滑，10=快速响应）")]
    [Range(1f, 100f)]
    public float dynamicSnareThresholdSpeed = 2f;

    // 私有变量：存储平滑后的阈值（内部使用）
    private float smoothedKickThreshold = 0.5f;
    private float smoothedSnareThreshold = 0.5f;

    private float kalmanEstimate = 0f;
    private float kalmanErrorCovariance = 1f;
    private float kalmanProcessNoise = 0.01f;
    private float kalmanMeasurementNoise = 0.1f;

    // ==================== 调性检测字段 ====================
    [Tooltip("当前检测到的调名（C / C# / D ... B）")]
    public string currentKey = "Unknown";

    [Tooltip("当前检测到的调式（Major / Minor）")]
    public string currentMode = "Unknown";

    [Tooltip("调性检测更新间隔（秒）。每隔此时间重新计算一次，0 = 每帧更新")]
    public float keyUpdateInterval = 0.0f;

    [Tooltip("调性色度平滑系数。值越大越稳定，值越小响应越快")]
    [Range(0f, 0.98f)]
    public float keyChromaSmoothing = 0.82f;

    [Tooltip("新调性需要比当前调性高出的相关分数差，避免频繁跳调")]
    [Range(0f, 0.3f)]
    public float keySwitchMargin = 0.05f;

    [Tooltip("调性结果切换前需要连续确认的次数")]
    [Range(1, 12)]
    public int keyStableFrameThreshold = 3;

    [Tooltip("当前调性检测置信度（最佳模板与次佳模板的相关分差）")]
    public float currentKeyConfidence = 0f;

    private float lastKeyUpdateTime = 0f;
    private readonly double[] smoothedChroma = new double[12];
    private bool hasSmoothedChroma = false;
    private string pendingKey = "Unknown";
    private string pendingMode = "Unknown";
    private int pendingKeyFrames = 0;

    // Krumhansl-Schmuckler 调性模板（标准值）
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

    [Tooltip("当前帧的 Kick 频段（40~100Hz）能量均值")]
    public float kickEnergy;
    public float smoothedKickEnergy;

    [Tooltip("当前帧的 Bass 频段（60~250Hz）能量均值")]
    public float bassEnergy;
    public float smoothedBassEnergy;

    [Tooltip("当前帧的 Synth 频段（400~4000Hz）能量均值")]
    public float synthEnergy;
    public float smoothedSynthEnergy;

    [Tooltip("触发 Kick VFX 事件所需的最低 kickEnergy 值")]
    public float kickThreshold = 0.5f;

    [Tooltip("Bass 能量映射到 VFX 参数 BassRate 的灵敏度倍率")]
    public float bassSensitivity = 20f;

    [Tooltip("Synth 能量映射到 VFX 参数 SynthStrength 的灵敏度倍率")]
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

        // ====== 移除旧的自动触发逻辑 ======
        if (limitedBPM > 0)
        {
            
        }
        else
        {
            if (Time.time - lastKeyUpdateTime >= keyUpdateInterval)
            {
                DetectKeyFromFft(fftBuffer);
            }
        }

        // ====== 节拍文字显示的倒计时 ======
        if (showBeatText)
        {
            beatTimer -= Time.deltaTime;
            if (beatTimer <= 0f)
            {
                showBeatText = false;
            }
        }

        // 频段能量计算
        kickEnergy = GetBandEnergy(frequencyData, 40, 100);
        smoothedKickEnergy = GetBandEnergy(smoothedFftData, 40, 100);

        bassEnergy = GetBandEnergy(frequencyData, 60, 250);
        smoothedBassEnergy = GetBandEnergy(smoothedFftData, 60, 250);

        synthEnergy = GetBandEnergy(frequencyData, 400, 4000);
        smoothedSynthEnergy = GetBandEnergy(smoothedFftData, 400, 4000);

        // 先用未平滑频谱更新静音状态，避免可视化平滑拖慢静音检测。
        CheckAndHandleSilence(frequencyData, Time.time);

        // 更新柱状条
        UpdateBars(smoothedFftData);

        // ====== 统一的节拍检测入口 ======
        DetectBeatImproved(frequencyData);
    }

    /// <summary>
    /// 更新柱状条可视化（改进版 - 加入对数压缩）
    /// 
    /// 改进点：
    /// 1. 对数压缩：使用 log(x + offset) 压缩动态范围，让低幅度信号更明显
    /// 2. 动态范围调整：根据最近的最大幅度自动调整缩放，避免爆表或过小
    /// 3. 高度限制：设置最小/最大高度，确保可见性和美观
    /// 4. 平滑过渡：保持原有的平滑效果
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

        // ====== 动态范围调整 ======
        if (enableDynamicRange)
        {
            // 找到当前帧的最大幅度
            float currentMaxAmplitude = 0f;
            for (int i = 0; i < barCount; i++)
            {
                float logIndex = Mathf.Pow((float)(i + 1) / barCount, 1);
                int fftIndex = Mathf.Clamp((int)(logIndex * (spectrumData.Length - 1)), 0, spectrumData.Length - 1);
                currentMaxAmplitude = Mathf.Max(currentMaxAmplitude, spectrumData[fftIndex]);
            }

            // 平滑更新最大幅度记录
            maxRecentAmplitude = Mathf.Lerp(maxRecentAmplitude, currentMaxAmplitude, Time.deltaTime * dynamicRangeSpeed);

            // 根据最大幅度调整缩放因子（避免除以0）
            if (maxRecentAmplitude > 0.001f)
            {
                dynamicScaleFactor = maxBarHeight / Mathf.Log(maxRecentAmplitude + logCompressionOffset, 10f + logCompressionStrength);
            }
        }
        else
        {
            dynamicScaleFactor = 1f;
        }

        // ====== 更新每根柱状条 ======
        EnsureBarVisualArrays();

        for (int i = 0; i < barCount; i++)
        {
            float finalHeight = minBarHeight;
            float rawAmplitude = 0f;

            if (!wasSilent)
            {
                // 对数频率映射（低频区域更宽）
                float logIndex = Mathf.Pow((float)(i + 1) / barCount, 1);
                int fftIndex = Mathf.Clamp((int)(logIndex * (spectrumData.Length - 1)), 0, spectrumData.Length - 1);

                rawAmplitude = spectrumData[fftIndex];

                // ====== 对数压缩 ======
                // 使用 log10(x + offset) 进行压缩
                // logCompressionStrength 控制压缩程度
                // logCompressionOffset 避免 log(0) 并控制低幅度信号的提升
                float compressedHeight = Mathf.Log(rawAmplitude + logCompressionOffset, 10f + logCompressionStrength);

                // 应用动态缩放
                float scaledHeight = compressedHeight * dynamicScaleFactor;

                // ====== 高度限制 ======
                // 确保柱状条在可见范围内，不会太小或太大
                finalHeight = Mathf.Clamp(scaledHeight, minBarHeight, maxBarHeight);
            }

            // ====== 更新柱状条缩放 ======
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

    // ==================== 节拍检测算法 ====================

    /// <summary>
    /// 节拍检测算法
    ///
    /// 修复了原版"间隔不断漂移至 minBeatInterval"的根本原因：
    ///
    /// 【问题根源】
    ///   原版将 beatTimestamps 存为 deltaTime（距上次节拍的秒数），并在每次触发后
    ///   立刻重置 lastBeatTime。鼓击的瞬态衰减会在 minBeatInterval 内连续触发多次，
    ///   每次间隔都约等于 minBeatInterval，大量污染数据；中位数也跟着漂移，
    ///   过滤逻辑失效，形成正反馈死锁。
    ///
    /// 【修复方案】
    ///   1. beatTimestamps 改为存储"绝对触发时间戳"（Time.time），
    ///      BPM 在 UpdateBPM 时统一用相邻时间戳差值计算，不再实时写入 deltaTime。
    ///   2. 引入 beatCooldown（默认 = minBeatInterval * 1.5）硬冷却：
    ///      冷却期内的 onset 全部忽略，从根本上防止同一鼓击被重复记录。
    ///   3. UpdateBPM 中的合法性检查改为对"相邻时间戳差"做范围约束
    ///      （必须在 minBeatInterval ~ maxBeatInterval 之间），
    ///      而不是对早已污染的中位数做偏差过滤。
    ///   4. 移除 OnKeyChanged 对 beatTimestamps 的写入，
    ///      调性变化不再污染节拍数据。
    /// </summary>
    private void DetectBeatImproved(float[] fft)
    {
        float time = Time.time;

        // ====== 步骤1：计算多频段能量 ======
        float snareEnergy = GetBandEnergy(fft, 150, 300);

        if (wasSilent)
        {
            previousKickEnergy = kickEnergy;
            previousSnareEnergy = snareEnergy;
            previousBassEnergy = bassEnergy;
            return;
        }

        // ====== 步骤2：维护能量历史 ======
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

        // ====== 步骤3：计算自适应阈值 ======
        float kickMean = kickEnergyHistory.Average();
        float snareMean = snareEnergyHistory.Average();
        float bassMean = bassEnergyHistory.Count > 0 ? bassEnergyHistory.Average() : bassEnergy;
        float kickStdDev = CalculateStdDev(kickEnergyHistory.ToArray(), kickMean);
        float snareStdDev = CalculateStdDev(snareEnergyHistory.ToArray(), snareMean);
        float bassStdDev = bassEnergyHistory.Count > 0 ? CalculateStdDev(bassEnergyHistory.ToArray(), bassMean) : 0f;

        float rawKickThreshold = kickMean + onsetSensitivity * kickStdDev;
        float rawSnareThreshold = snareMean + onsetSensitivity * snareStdDev;

        // ====== 新增：阈值平滑过渡 ======
        // 使用 Lerp 实现指数平滑，speed 越大响应越快
        smoothedKickThreshold = Mathf.Lerp(smoothedKickThreshold, rawKickThreshold,
            Time.deltaTime * dynamicKickThresholdSpeed);
        smoothedSnareThreshold = Mathf.Lerp(smoothedSnareThreshold, rawSnareThreshold,
            Time.deltaTime * dynamicSnareThresholdSpeed);

        // 将平滑后的值赋给公开字段（供 UI 显示和其他逻辑使用）
        dynamicKickThreshold = smoothedKickThreshold;
        dynamicSnareThreshold = smoothedSnareThreshold;

        // ====== 步骤4：Onset 检测（正向能量突变）======
        float kickOnset = Mathf.Max(0, kickEnergy - previousKickEnergy);
        float snareOnset = Mathf.Max(0, snareEnergy - previousSnareEnergy);
        float bassOnset = Mathf.Max(0, bassEnergy - previousBassEnergy);

        // ====== 步骤5：判断是否为有效 onset ======
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

        // ====== 步骤6：相位窗口加权（锁相后提升置信度）======
        float timeSinceLast = time - lastBeatTime;

        if (predictedNextBeat > 0f)
        {
            float timeToPredicted = Mathf.Abs(time - predictedNextBeat);
            if (timeToPredicted < beatInterval * 0.15f)
                totalConfidence = Mathf.Clamp01(totalConfidence * 1.3f);
        }

        // ====== 步骤7：硬冷却 + 置信度门控 ======
        // beatCooldown 防止同一鼓击的瞬态衰减被重复记录，
        // 这是解决"漂移至 minBeatInterval"的核心修复。
        bool cooldownPassed = timeSinceLast >= beatCooldown;
        bool isBeat = (isKickBeat || isSnareBeat)
                      && totalConfidence > minBeatConfidence
                      && cooldownPassed;

        // ====== 步骤8：节拍丢失检测（超过最大间隔则重置）======
        //if (predictedNextBeat > 0f && timeSinceLast > maxBeatInterval)
        //{
        //    Debug.Log("[Beat] 节拍丢失，重置检测");
        //    ResetBeatDetection();
        //}

        // ====== 步骤9：记录绝对时间戳并触发节拍显示 ======
        if (isBeat)
        {
            float beatStrength = kickEnergy + snareEnergy;

            // 存储绝对时间戳
            beatTimestamps.Add(time);
            beatConfidences.Add(totalConfidence);
            beatStrengths.Add(beatStrength);

            // 限制历史窗口大小
            int maxSize = 16;
            if (beatTimestamps.Count > maxSize)
            {
                beatTimestamps.RemoveAt(0);
                beatConfidences.RemoveAt(0);
                beatStrengths.RemoveAt(0);
            }

            lastBeatTime = time;

            // ====== 触发节拍显示 ======
            showBeatText = true;
            beatTimer = beatDisplayTime;

            // 在节拍发生时更新调性（可选）
            if (limitedBPM > 0)
            {
                DetectKeyFromFft(AudioCaptureCSCore.instance.frequencyData);
            }

            Debug.Log($"[Beat] 触发节拍 - 置信度: {totalConfidence:F2}, Kick: {kickEnergy:F3}, Snare: {snareEnergy:F3}");
        }

            // ====== 步骤10：定期更新 BPM ======
        if (time - lastBpmUpdateTime > bpmUpdateInterval && beatTimestamps.Count >= 4)
        {
            UpdateBPM();
            lastBpmUpdateTime = time;
        }

        // ====== 步骤11：预测下一个节拍 ======
        if (limitedBPM > 0f)
        {
            predictedNextBeat = lastBeatTime + beatInterval;
            if (isBeat) phaseError = time - predictedNextBeat;
        }

        // 更新前一帧能量
        previousKickEnergy = kickEnergy;
        previousSnareEnergy = snareEnergy;
        previousBassEnergy = bassEnergy;

        // 手动点击辅助（调试用）
        if (Input.GetMouseButtonDown(0) && timeSinceLast > minBeatInterval)
        {
            beatTimestamps.Add(time);
            beatConfidences.Add(1.0f);
            beatStrengths.Add(kickEnergy + snareEnergy);
            lastBeatTime = time;
            Debug.Log($"[Beat] 手动节拍 @ {time:F2}s");
        }

    }

    /// <summary>
    /// 更新 BPM（鲁棒候选评分版）
    /// 
    /// 改进点：
    /// 1. 从平均/中位间隔、单个间隔和成对时间戳生成 BPM 候选。
    /// 2. 按候选与历史间隔的匹配度评分，允许半拍、倍拍和漏拍。
    /// 3. 常见 BPM 区间只做软偏置，不强行把真实 tempo 拉回 90-150。
    /// 4. 根据候选稳定度动态调整平滑速度。
    /// </summary>
    private void UpdateBPM()
    {
        if (beatTimestamps.Count < 4)
            return;

        // ====== 步骤1：将绝对时间戳转换为相邻差值 ======
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
            Debug.Log("[BPM] 合法间隔不足3个，等待更多节拍");
            return;
        }

        float[] intervalArray = intervals.ToArray();

        // ====== 步骤2：多来源 BPM 候选 ======
        float avgInterval = intervals.Average();
        float medianInterval = GetMedian(intervalArray);
        List<float> bpmCandidates = BuildBpmCandidates(intervals, intConfidences, avgInterval, medianInterval);

        // ====== 步骤3：选择最佳 BPM（按历史间隔一致性评分）======
        float bestBPM = SelectBestBPM(bpmCandidates, intervals, intConfidences);

        // ====== 步骤4：指数移动平均平滑 ======
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

        detectedBPM = kalmanEstimate;

        // ====== 步骤5：倍频修正 ======
        LimitBPM();

        // ====== 步骤6：根据稳定性动态调整置信度阈值 ======
        bpmVariance = CalculateStdDev(intervalArray, avgInterval);
        minBeatConfidence = bpmVariance < 0.08f
            ? Mathf.Max(0.25f, minBeatConfidence - 0.02f)
            : 0.4f;

        Debug.Log($"[BPM] 更新：{Mathf.RoundToInt(limitedBPM)} BPM " +
                  $"（原始：{bestBPM:F1}, 平滑：{kalmanEstimate:F1}, 方差：{bpmVariance:F3}）");
    }

    /// <summary>
    /// 使用自相关分析找到最佳周期
    /// </summary>
    private float FindBestPeriod(float[] intervals)
    {
        if (intervals.Length < 4) return 0f;

        float mean = intervals.Average();
        float bestCorrelation = 0f;
        float bestPeriod = mean;

        // 测试不同滞后量的自相关
        for (int lag = 1; lag < Mathf.Min(5, intervals.Length - 1); lag++)
        {
            float correlation = 0f;
            int count = 0;

            for (int i = 0; i < intervals.Length - lag; i++)
            {
                // 累加滞后 lag 的间隔和
                float sumLag = 0f;
                for (int j = 0; j < lag; j++)
                {
                    sumLag += intervals[i + j];
                }

                // 计算与平均周期的偏差
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
            AddTempoCandidate(candidates, 120f / intervals[i]);
            AddTempoCandidate(candidates, 30f / intervals[i]);
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
                AddTempoCandidate(candidates, 30f * beatSteps / span);
                AddTempoCandidate(candidates, 120f * beatSteps / span);
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
        while (bpm < 60f && bpm * 2f <= 220f)
            bpm *= 2f;

        while (bpm > 200f && bpm * 0.5f >= 50f)
            bpm *= 0.5f;

        return Mathf.Clamp(bpm, 50f, 220f);
    }

    /// <summary>
    /// 从多个 BPM 候选中选择最佳值（以间隔一致性为主，常见范围为软偏置）
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

            // 常见舞曲区间只作为软偏置，避免把 70/160 这类真实 tempo 强行拉回中段。
            score += GetTempoPlausibilityScore(bpm);

            // 奖励稳定性（与历史 BPM 接近）
            if (kalmanEstimate > 0)
            {
                float diff = Mathf.Abs(bpm - kalmanEstimate);
                score += 1.5f / (1f + diff * 0.08f);
            }

            // 奖励与间隔数据的一致性
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
        float[] multiples = { 0.5f, 1f, 1.5f, 2f, 3f, 4f };

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
        if (bpm >= 60f && bpm <= 200f) return 0.35f;
        return -1f;
    }

    /// <summary>
    /// 限制 BPM 范围到合理值（改进版）
    /// </summary>
    private void LimitBPM()
    {
        if (detectedBPM <= 0) return;

        limitedBPM = detectedBPM;

        // ====== 智能倍频修正 ======
        // 策略：优先将 BPM 调整到 90-150 范围，这是最常见的音乐 BPM 区间

        // 如果太低，尝试倍频
        while (limitedBPM < 90 && limitedBPM * 2 <= 200)
        {
            limitedBPM *= 2;
        }

        // 如果太高，尝试半频
        while (limitedBPM > 150 && limitedBPM / 2 >= 60)
        {
            limitedBPM /= 2;
        }

        // 最终硬限制
        limitedBPM = Mathf.Clamp(limitedBPM, 60, 200);

        // 更新节拍间隔。这里必须用倍频修正后的 limitedBPM，避免 70/140 或 170/85
        // 这类半速/倍速修正后，预测窗口仍按未修正值运行。
        beatInterval = 60f / Mathf.Max(limitedBPM, 1f);
        beatDisplayTime = beatInterval / 4f;

        Debug.Log($"[BPM] 倍频修正后：{Mathf.RoundToInt(limitedBPM)} BPM");
    }

    /// <summary>
    /// 重置节拍检测
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
        kalmanErrorCovariance = 1f;
        predictedNextBeat = 0f;
        previousBassEnergy = 0f;
        hasSmoothedChroma = false;
        pendingKey = "Unknown";
        pendingMode = "Unknown";
        pendingKeyFrames = 0;
        currentKeyConfidence = 0f;

        // 重置静音状态
        wasSilent = false;
        silenceStartTime = -1f;
        minBeatConfidence = 0.3f; // 恢复默认阈值

        Debug.Log("[Beat] 重置节拍检测");
    }

    /// <summary>
    /// 计算标准差
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
    /// 计算中位数
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
    /// 检测静音并进行渐进式清理
    /// 改进点：
    /// 1. 需要持续静音一定时间才清空（避免短暂停顿误触发）
    /// 2. 静音时保留部分历史数据（便于快速恢复）
    /// 3. 区分完全静音和低能量
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

                    Debug.Log($"[Silence] 音频恢复，静音持续了 {silenceDurationTotal:F2}s，能量: {smoothedSilenceEnergy:F5}");
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

                Debug.Log($"[Silence] 检测到静音开始，时间: {currentTime:F2}，能量: {smoothedSilenceEnergy:F5}");
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

    // ==================== 调性检测算法 ====================

    /// <summary>
    /// 调性检测（稳定版）
    ///
    /// 设计原则：保留实时响应，但不再让单帧 FFT 直接改写调性。
    ///
    /// 流程：
    ///   1. ExtractChromaFeatures：将 FFT 频谱映射到 12 个色度 bin，
    ///      权重 = sqrt(amplitude)，并对低频根音稍加权。
    ///   2. 色度做指数平滑后再 L2 归一化，降低瞬时泛音和噪声的影响。
    ///   3. 遍历 24 个调（12 大调 + 12 小调），用皮尔逊相关系数与 K-S 模板匹配，
    ///      同时记录最佳与次佳分数差作为置信度。
    ///   4. 新调性需要连续确认且分数超过当前调性一定边距，避免显示频繁跳动。
    /// </summary>
    private void DetectKeyFromFft(float[] fft)
    {
        try
        {
            double[] chroma = ExtractChromaFeatures(fft);
            chroma = SmoothChroma(chroma);
            chroma = NormalizeChroma(chroma);
            lastKeyUpdateTime = Time.time;

            // 遍历全部 24 个调，取皮尔逊相关系数最大者
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
    /// 将 FFT 频谱映射到 12 色度 bin。
    /// 权重 = sqrt(amplitude)，使高幅度音符突出，噪声趋零，无需硬阈值。
    /// 相邻半音之间按音符内小数部分做高斯扩散，减少量化误差。
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

            // 高斯扩散到相邻半音，减少频率量化误差
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

    /// <summary>L2 归一化，消除响度差异。</summary>
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

    /// <summary>皮尔逊相关系数：chroma 与 template 在偏移 shift 处的相关度（-1 ~ 1）。</summary>
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
        // 调性变化不写入 beatTimestamps，避免污染节拍间隔数据。
        // 原版此处会把 deltaTime（往往很短）塞入列表并重置 lastBeatTime，
        // 是导致间隔漂移至 minBeatInterval 的原因之一。
        Debug.Log($"[Key] 调性变化回调（不写入节拍数据）");
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

        // 显示静音状态
        style.normal.textColor = Color.yellow;
        GUILayout.Label(GetPlaybackStatusText(), style);
        GUILayout.Label($"Silence Energy: {smoothedSilenceEnergy:F5}", style);
        style.normal.textColor = Color.green;

        // 显示对数压缩参数（调试用）
        if (enableDynamicRange)
        {
            GUILayout.Label($"动态缩放: {dynamicScaleFactor:F2} | 最大幅度: {maxRecentAmplitude:F3}", style);
        }

        GUILayout.Label($"原始 BPM: {detectedBPM:F1} | 方差: {bpmVariance:F3}", style);
    }

    private string GetPlaybackStatusText()
    {
        if (wasSilent)
        {
            float startTime = silenceStartTime >= 0f ? silenceStartTime : Time.time;
            return $"静音 {FormatDuration(Time.time - startTime)}";
        }

        float playTime = playStartTime > 0f ? playStartTime : Time.time;
        return $"播放 {FormatDuration(Time.time - playTime)}";
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
