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
    private float playStartTime = 0f;
    private float silenceStartTime = 0f;
    private float silenceThreshold = 0.001f;
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

    private float lastKeyUpdateTime = 0f;

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
        if (!movingBars)
        {
            bars = new GameObject[barCount];
            for (int i = 0; i < barCount; i++)
            {
                GameObject bar = Instantiate(barPrefab, transform);
                float x = -barCount / 2 * horizontalScale + i * horizontalScale;
                float z = Mathf.Sqrt(1 - (x * x) / (a * a)) * b;
                bar.transform.position = new Vector3(x, transform.position.y, z);
                bars[i] = bar;
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
                for (int i = 0; i < barCount; i++)
                {
                    GameObject bar = Instantiate(barPrefab, transform);
                    float x = -barCount / 2 * horizontalScale + i * horizontalScale;
                    float z = Mathf.Sqrt(1 - (x * x) / (a * a)) * b;
                    bar.transform.position = new Vector3(x, transform.position.y, z);
                    bars[i] = bar;
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
        for (int i = 0; i < barCount; i++)
        {
            float finalHeight = minBarHeight;

            if (!wasSilent)
            {
                // 对数频率映射（低频区域更宽）
                float logIndex = Mathf.Pow((float)(i + 1) / barCount, 1);
                int fftIndex = Mathf.Clamp((int)(logIndex * (spectrumData.Length - 1)), 0, spectrumData.Length - 1);

                float rawAmplitude = spectrumData[fftIndex];

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
            }
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

        // ====== 步骤2：维护能量历史 ======
        kickEnergyHistory.Enqueue(kickEnergy);
        snareEnergyHistory.Enqueue(snareEnergy);

        if (kickEnergyHistory.Count > energyHistorySize) kickEnergyHistory.Dequeue();
        if (snareEnergyHistory.Count > energyHistorySize) snareEnergyHistory.Dequeue();

        if (kickEnergyHistory.Count < 10)
        {
            previousKickEnergy = kickEnergy;
            previousSnareEnergy = snareEnergy;
            return;
        }

        // ====== 步骤3：计算自适应阈值 ======
        float kickMean = kickEnergyHistory.Average();
        float snareMean = snareEnergyHistory.Average();
        float kickStdDev = CalculateStdDev(kickEnergyHistory.ToArray(), kickMean);
        float snareStdDev = CalculateStdDev(snareEnergyHistory.ToArray(), snareMean);

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

        // ====== 步骤5：判断是否为有效 onset ======
        bool isKickBeat = kickEnergy > dynamicKickThreshold && kickOnset > kickStdDev * 0.5f;
        bool isSnareBeat = snareEnergy > dynamicSnareThreshold && snareOnset > snareStdDev * 0.5f;

        float kickConfidence = isKickBeat ? Mathf.Clamp01((kickEnergy - dynamicKickThreshold) / Mathf.Max(kickMean * 0.5f, 1e-6f)) : 0f;
        float snareConfidence = isSnareBeat ? Mathf.Clamp01((snareEnergy - dynamicSnareThreshold) / Mathf.Max(snareMean * 0.5f, 1e-6f)) : 0f;
        float totalConfidence = Mathf.Max(kickConfidence, snareConfidence);

        // ====== 步骤6：相位窗口加权（锁相后提升置信度）======
        float timeSinceLast = time - lastBeatTime;

        if (predictedNextBeat > 0f)
        {
            float timeToPredicted = Mathf.Abs(time - predictedNextBeat);
            if (timeToPredicted < beatInterval * 0.15f)
                totalConfidence *= 1.3f;
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

        // 手动点击辅助（调试用）
        if (Input.GetMouseButtonDown(0) && timeSinceLast > minBeatInterval)
        {
            beatTimestamps.Add(time);
            beatConfidences.Add(1.0f);
            beatStrengths.Add(kickEnergy + snareEnergy);
            lastBeatTime = time;
            Debug.Log($"[Beat] 手动节拍 @ {time:F2}s");
        }

        // ====== 静音检测与渐进式清理 ======
        CheckAndHandleSilence(kickEnergy, snareEnergy, time);
    }

    /// <summary>
    /// 更新 BPM（改进版 - 解决检测值偏低问题）
    /// 
    /// 改进点：
    /// 1. 使用自相关分析检测周期性，不依赖单一节拍检测
    /// 2. 多假设跟踪：同时跟踪多个 BPM 候选，选择最稳定的
    /// 3. 智能倍频修正：优先选择 90-150 BPM 常见范围
    /// 4. 降低漏检影响：允许部分节拍缺失仍能正确计算
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

        // ====== 步骤2：自相关分析检测周期性（核心改进）======
        float[] intervalArray = intervals.ToArray();
        float bestPeriod = FindBestPeriod(intervalArray);

        // ====== 步骤3：多假设 BPM 候选 ======
        List<float> bpmCandidates = new List<float>();

        // 候选1：基于平均间隔
        float avgInterval = intervals.Average();
        bpmCandidates.Add(60f / avgInterval);

        // 候选2：基于自相关最佳周期
        if (bestPeriod > 0)
            bpmCandidates.Add(60f / bestPeriod);

        // 候选3：基于中位数间隔
        float medianInterval = GetMedian(intervalArray);
        bpmCandidates.Add(60f / medianInterval);

        // 候选4：2倍速假设（可能漏检了弱拍）
        bpmCandidates.Add(60f / (avgInterval * 0.5f));

        // 候选5：1.5倍速假设（三连音情况）
        bpmCandidates.Add(60f / (avgInterval * 0.67f));

        // ====== 步骤4：选择最佳 BPM（优先 90-150 范围）======
        float bestBPM = SelectBestBPM(bpmCandidates, intervals);

        // ====== 步骤5：指数移动平均平滑（替代卡尔曼）======
        float emaAlpha = 0.3f; // 响应速度系数
        if (kalmanEstimate == 0f)
        {
            kalmanEstimate = bestBPM;
        }
        else
        {
            kalmanEstimate = kalmanEstimate * (1 - emaAlpha) + bestBPM * emaAlpha;
        }

        detectedBPM = kalmanEstimate;

        // ====== 步骤6：智能倍频修正 ======
        LimitBPM();

        // ====== 步骤7：根据稳定性动态调整置信度阈值 ======
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

    /// <summary>
    /// 从多个 BPM 候选中选择最佳值（优先常见范围）
    /// </summary>
    private float SelectBestBPM(List<float> candidates, List<float> intervals)
    {
        if (candidates.Count == 0) return 60f / intervals.Average();

        float bestScore = float.MinValue;
        float bestBPM = candidates[0];

        foreach (float bpm in candidates)
        {
            if (bpm <= 0) continue;

            float score = 0f;

            // 优先奖励 90-150 BPM 范围（最常见）
            if (bpm >= 90 && bpm <= 150)
                score += 3f;
            else if (bpm >= 72 && bpm <= 180)
                score += 1f;

            // 奖励稳定性（与历史 BPM 接近）
            if (kalmanEstimate > 0)
            {
                float diff = Mathf.Abs(bpm - kalmanEstimate);
                score += 2f / (1f + diff);
            }

            // 奖励与间隔数据的一致性
            float expectedInterval = 60f / bpm;
            float matchCount = 0;
            foreach (float interval in intervals)
            {
                if (Mathf.Abs(interval - expectedInterval) < expectedInterval * 0.2f)
                    matchCount++;
            }
            score += matchCount / intervals.Count;

            if (score > bestScore)
            {
                bestScore = score;
                bestBPM = bpm;
            }
        }

        return bestBPM;
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

        // 更新节拍间隔
        beatInterval = 60f / Mathf.Max(detectedBPM, limitedBPM);
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
        beatTimestamps.Clear();
        beatConfidences.Clear();
        beatStrengths.Clear();
        detectedBPM = 0f;
        limitedBPM = 0f;
        kalmanEstimate = 0f;
        kalmanErrorCovariance = 1f;
        predictedNextBeat = 0f;

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
    private void CheckAndHandleSilence(float kickEnergy, float snareEnergy, float currentTime)
    {
        float totalEnergy = kickEnergy + snareEnergy;
        bool isSilent = totalEnergy < silenceThreshold;

        // ====== 情况1: 当前是静音 ======
        if (isSilent)
        {
            // 首次进入静音状态
            if (!wasSilent)
            {
                ResetBeatDetection();
                silenceStartTime = currentTime;
                wasSilent = true;
                Debug.Log($"[Silence] 检测到静音开始，时间: {currentTime:F2}");
            }
        }
        // ====== 情况2: 从静音恢复 ======
        else
        {
            if (wasSilent)
            {
                float silenceDurationTotal = currentTime - silenceStartTime;
                Debug.Log($"[Silence] 音频恢复，静音持续了 {silenceDurationTotal:F2}s");

                // 如果静音时间很短(<1秒)，恢复正常置信度阈值
                if (silenceDurationTotal < 1f)
                {
                    minBeatConfidence = 0.3f;
                }
                // 如果静音时间较长，给予一个较低的初始阈值，便于快速重新检测
                else if (beatTimestamps.Count > 0)
                {
                    minBeatConfidence = 0.2f; // 临时降低阈值
                    Debug.Log($"[Silence] 降低初始阈值以便快速重新锁定节拍");
                }

                playStartTime = currentTime;
                wasSilent = false;
            }

            // ====== 情况3: 低能量但非静音（例如安静的间奏）======
            // 如果能量很低但还不到静音阈值，逐渐降低置信度要求
            else if (totalEnergy < silenceThreshold * 5) // 能量低于5倍静音阈值
            {
                // 逐渐降低置信度，帮助在低能量段落继续跟踪
                minBeatConfidence = Mathf.Max(0.2f, minBeatConfidence - 0.01f * Time.deltaTime);
            }
            // 能量正常，恢复标准置信度
            else
            {
                minBeatConfidence = Mathf.Lerp(minBeatConfidence, 0.3f, Time.deltaTime * 2f);
            }
        }
    }

    // ==================== 调性检测算法 ====================

    /// <summary>
    /// 调性检测（即时版）
    ///
    /// 设计原则：去掉所有缓冲、投票、置信度门控，每次调用直接输出最佳匹配调性。
    /// 唯一的节流是 keyUpdateInterval（默认 0，即每帧更新）。
    ///
    /// 流程：
    ///   1. ExtractChromaFeatures：将 FFT 频谱映射到 12 个色度 bin，
    ///      权重 = sqrt(amplitude)，噪声频率自然趋零，无需阈值过滤。
    ///   2. NormalizeChroma：L2 归一化，消除响度差异。
    ///   3. 遍历 24 个调（12 大调 + 12 小调），用皮尔逊相关系数与 K-S 模板匹配，
    ///      取相关系数最大的那个调直接作为结果，无平局处理、无粘滞偏置。
    ///   4. 结果与上一帧不同时立即更新 currentKey / currentMode 并触发回调。
    /// </summary>
    private void DetectKeyFromFft(float[] fft)
    {
        try
        {
            double[] chroma = ExtractChromaFeatures(fft);
            chroma = NormalizeChroma(chroma);

            // 遍历全部 24 个调，取皮尔逊相关系数最大者
            double bestScore = double.MinValue;
            string bestKey = "C";
            string bestMode = "Major";

            for (int shift = 0; shift < 12; shift++)
            {
                double scoreMajor = PearsonCorr(chroma, majorProfile, shift);
                if (scoreMajor > bestScore)
                {
                    bestScore = scoreMajor;
                    bestKey = keyNames[shift];
                    bestMode = "Major";
                }

                double scoreMinor = PearsonCorr(chroma, minorProfile, shift);
                if (scoreMinor > bestScore)
                {
                    bestScore = scoreMinor;
                    bestKey = keyNames[shift];
                    bestMode = "Minor";
                }
            }

            // 结果变化时立即更新，无任何延迟
            if (bestKey != currentKey || bestMode != currentMode)
            {
                currentKey = bestKey;
                currentMode = bestMode;
                lastKeyUpdateTime = Time.time;
                OnKeyChanged();
                Debug.Log($"[Key] 🎵 {currentKey} {currentMode}");
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
            int noteClass = ((int)Math.Round(midiNote) % 12 + 12) % 12;
            double weight = Math.Sqrt(fft[i]);

            // 高斯扩散到相邻半音，减少频率量化误差
            double frac = midiNote - Math.Floor(midiNote);
            chroma[noteClass] += weight * Math.Exp(-0.5 * frac * frac);
            chroma[(noteClass + 1) % 12] += weight * Math.Exp(-0.5 * (1.0 - frac) * (1.0 - frac));
        }

        return chroma;
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

    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 32;
        style.normal.textColor = Color.green;

        GUI.Label(new Rect(Screen.width - 420, 20, 300, 50), $"BPM: {limitedBPM:F1}", style);
        GUI.Label(new Rect(Screen.width - 420, 60, 300, 50), $"Key: {currentKey} {currentMode}", style);

        if (showBeatText)
        {
            GUI.Label(new Rect(Screen.width - 420, 90, 200, 50), $"** BEAT **", style);
        }

        GUI.Label(new Rect(Screen.width - 420, 120, 400, 50), $"Kick: {kickEnergy:F3} (T: {dynamicKickThreshold:F3})", style);
        GUI.Label(new Rect(Screen.width - 420, 150, 500, 50), $"Confidence: {(beatConfidences.Count > 0 ? beatConfidences.Last() : 0):F2}", style);
        GUI.Label(new Rect(Screen.width - 420, 180, 500, 50), $"Variance: {bpmVariance:F3}", style);

        // 显示静音状态
        style.normal.textColor = Color.yellow;
        if (wasSilent && silenceStartTime > 0)
        {
            float silenceDur = Time.time - silenceStartTime;
            
            GUI.Label(new Rect(Screen.width - 420, 210, 500, 50), "⚠️ 静音 " + (silenceDur <= 10 ? $"{silenceDur:F1}s" : ""), style);
        }
        else if (!wasSilent && playStartTime > 0)
        {
            int playDur = (int) (Time.time - playStartTime);

            int min = playDur / 60;
            int sec = playDur % 60;
            string minStr = min < 10 ? "0" + min : min.ToString();
            string secStr = sec < 10 ? "0" + sec : sec.ToString();
            GUI.Label(new Rect(Screen.width - 420, 210, 500, 50), $"🎶 播放 {minStr}:{secStr}", style);
        }
        style.normal.textColor = Color.green;

        // 显示对数压缩参数（调试用）
        if (enableDynamicRange)
        {
            GUI.Label(new Rect(Screen.width - 420, 240, 600, 50), $"动态缩放: {dynamicScaleFactor:F2} | 最大幅度: {maxRecentAmplitude:F3}", style);
        }

        GUI.Label(new Rect(Screen.width - 420, 270, 600, 50), $"原始 BPM: {detectedBPM:F1} | 方差：{bpmVariance:F3}", style);
    }
}
