using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.DSP;
using CSCore.SoundIn;
using CSCore.Streams;
using UnityEngine.VFX;

public class AudioVisualizerCSCore : MonoBehaviour
{
    private WasapiLoopbackCapture capture;
    private IWaveSource waveSource;
    private SingleBlockNotificationStream notificationStream;
    private FftProvider fftProvider;

    private const int fftSize = 2048;
    public float[] frequencyData;
    private float[] averageSamples;
    public float[] smoothedFftData;
    public bool linearFftData;
    public bool movingBars;
    public float smoothingWeight = 0.5f;
    public float logPower = 1f;
    public int lowFrequencyRange = 256;
    public GameObject barPrefab;
    public Light lowFrequencyLight;
    public Light beatLight;
    public float lowFrequencyIntensity;
    public float beatIntensity;
    public int barCount = 64;
    public Transform barPosition;
    public float brightness = 5;
    public float maxBrightness = 20;
    public float horizontalScale = 0.01f;
    public float verticalScale = 1f;
    public float a = 5;
    public float b = 1;
    private GameObject[] bars;

    // ==================== 改进的BPM检测字段 ====================
    public int beat = 0;

    // 多频段能量历史
    private Queue<float> kickEnergyHistory = new Queue<float>();
    private Queue<float> snareEnergyHistory = new Queue<float>();
    private Queue<float> bassEnergyHistory = new Queue<float>();
    public int energyHistorySize = 50;

    // 节拍时间戳
    public List<float> beatTimestamps = new List<float>();
    private List<float> beatConfidences = new List<float>(); // 每个节拍的置信度
    public float lastBeatTime = 0f;
    public float lastBpmUpdateTime = 0f;

    // BPM相关
    public float detectedBPM = 0f;
    public float limitedBPM = 0f;
    public float smoothedBPM = 0f; // 平滑后的BPM
    private float bpmVariance = 0f; // BPM方差，用于评估稳定性

    // 自适应阈值
    public float dynamicKickThreshold;
    public float dynamicSnareThreshold;
    private float energyStdDev = 0f; // 能量标准差

    // 相位跟踪
    private float predictedNextBeat = 0f; // 预测的下一个节拍时间
    private float phaseError = 0f; // 相位误差

    // 配置参数
    public float bpmUpdateInterval = 1f;
    public float minBeatInterval = 0.3f; // 最小节拍间隔（对应200 BPM）
    public float maxBeatInterval = 1.2f; // 最大节拍间隔（对应50 BPM）
    public float beatInterval = 0.5f;

    // UI相关
    public bool showBeatText = false;
    public float beatDisplayTime = 0.2f;
    private float beatTimer = 0f;

    // 置信度阈值
    [Range(0f, 1f)]
    public float minBeatConfidence = 0.3f; // 最小置信度

    // 能量变化率检测
    private float previousKickEnergy = 0f;
    private float previousSnareEnergy = 0f;

    // 静音检测
    private float silenceStartTime = -1f; // 静音开始时间
    private float silenceThreshold = 0.001f; // 静音能量阈值
    private float silenceDuration = 0.1f; // 持续静音多久后才清空（秒）
    private bool wasSilent = false; // 上一帧是否静音

    // 节拍强度分级（用于区分强拍和弱拍）
    private List<float> beatStrengths = new List<float>();

    // Onset detection 改进
    [Range(1.0f, 3.0f)]
    public float onsetSensitivity = 1.5f; // Onset灵敏度系数

    // 卡尔曼滤波参数（用于平滑BPM）
    private float kalmanEstimate = 0f;
    private float kalmanErrorCovariance = 1f;
    private float kalmanProcessNoise = 0.01f;
    private float kalmanMeasurementNoise = 0.1f;

    // ==================== 调性检测字段 ====================
    private string currentKey = "Unknown";
    private string currentMode = "Major";
    private float lastKeyUpdateTime = 0f;
    private float keyUpdateInterval = 0.8f;

    private Queue<string> recentKeys = new Queue<string>();
    private int keyHistorySize = 5;
    private float keyConfidenceThreshold = 0.08f;
    private double[] chromaAccumulator = new double[12];
    private int chromaFrameCount = 0;
    private int chromaAverageFrames = 3;

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

    // VFX
    public VisualEffect kickVfx;
    public VisualEffect bassVfx;
    public VisualEffect synthVfx;

    public float kickEnergy;
    public float bassEnergy;
    public float synthEnergy;

    public float kickThreshold = 0.5f;
    public float bassSensitivity = 20f;
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

        InitializeCapture();
    }

    private void InitializeCapture()
    {
        try
        {
            capture?.Stop();
            capture?.Dispose();
            Debug.Log("[AudioVisualizerCSCore] Stopped and disposed previous capture.");

            capture = new WasapiLoopbackCapture();
            Debug.Log("[AudioVisualizerCSCore] Created WasapiLoopbackCapture.");
            capture.Initialize();
            Debug.Log("[AudioVisualizerCSCore] Capture initialized.");

            if (capture.Device == null)
            {
                Debug.LogError("[AudioVisualizerCSCore] No default audio output device found.");
                return;
            }

            Debug.Log($"[AudioVisualizerCSCore] Using device: {capture.Device.FriendlyName}, State: {capture.Device.DeviceState}");

            var sampleSource = new SoundInSource(capture) { FillWithZeros = false }.ToSampleSource();
            waveSource = sampleSource.ToWaveSource();
            notificationStream = new SingleBlockNotificationStream(sampleSource);
            Debug.Log("[AudioVisualizerCSCore] Audio stream and notification stream initialized.");

            fftProvider = new FftProvider(waveSource.WaveFormat.Channels, FftSize.Fft2048);
            Debug.Log("[AudioVisualizerCSCore] FFT Provider created with channels: " + waveSource.WaveFormat.Channels);

            capture.DataAvailable += (s, args) =>
            {
                float[] buffer = new float[args.ByteCount / 4];
                Buffer.BlockCopy(args.Data, 0, buffer, 0, args.ByteCount);

                for (int i = 0; i < buffer.Length; i += waveSource.WaveFormat.Channels)
                {
                    if (waveSource.WaveFormat.SampleRate > 2205)
                    {
                        if (i / waveSource.WaveFormat.SampleRate < 0.6f)
                        {
                            if (waveSource.WaveFormat.Channels == 2)
                            {
                                fftProvider.Add(buffer[i], buffer[i + 1]);
                            }
                            else
                            {
                                fftProvider.Add(buffer[i], buffer[i]);
                            }
                        }
                    }
                }
            };

            capture.Start();
            Debug.Log("[AudioVisualizerCSCore] Capture started.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AudioVisualizerCSCore] Error initializing audio capture: {ex.Message}");
        }
    }

    void FixedUpdate()
    {
        if (fftProvider != null)
        {
            float[] fftBuffer = new float[fftSize];
            bool hasFftData = fftProvider.GetFftData(fftBuffer);

            if (hasFftData)
            {
                ProcessFftData(fftBuffer);
            }
        }
    }

    private void ProcessFftData(float[] fftBuffer)
    {
        int dataLength = fftBuffer.Length;
        frequencyData = new float[dataLength];

        for (int i = 0; i < dataLength; i++)
        {
            float magnitude = Mathf.Max(fftBuffer[i], 1e-6f);
            frequencyData[i] = linearFftData
                ? magnitude * verticalScale
                : Mathf.Log10(magnitude) * verticalScale * 20f;
        }

        if (smoothedFftData.Length == 0)
        {
            smoothedFftData = new float[dataLength];
            Array.Copy(frequencyData, smoothedFftData, dataLength);
        }
        else
        {
            for (int i = 0; i < dataLength; i++)
            {
                smoothedFftData[i] = (smoothedFftData[i] * smoothingWeight) + (frequencyData[i] * (1 - smoothingWeight));
            }
        }

        // 计算节拍间隔
        if (limitedBPM > 0)
        {
            beatInterval = 60f / limitedBPM;

            if (Time.time - lastBeatTime >= beatInterval)
            {
                lastBeatTime = Time.time;
                showBeatText = true;

                if (beat < 4)
                {
                    beat++;
                }
                else
                {
                    beat = 1;
                }

                DetectKeyFromFft(fftBuffer);

                beatTimer = beatDisplayTime;
                beatIntensity = 0.25f;
            }

            lowFrequencyIntensity = Mathf.Lerp(lowFrequencyIntensity, 0, limitedBPM / 10 * Time.deltaTime);
            beatIntensity = Mathf.Lerp(beatIntensity, 0, limitedBPM * Time.deltaTime);
        }
        else
        {
            if (Time.time - lastKeyUpdateTime >= keyUpdateInterval)
            {
                DetectKeyFromFft(fftBuffer);
            }
        }

        if (showBeatText)
        {
            beatTimer -= Time.deltaTime;
            if (beatTimer <= 0f)
            {
                showBeatText = false;
            }
        }

        if (lowFrequencyLight != null)
        {
            lowFrequencyLight.intensity = lowFrequencyIntensity;
            float hue = Mathf.Repeat(Time.time * 0.01f, 1f);
            Color targetColor = Color.HSVToRGB(hue, 1f, 1f);
            lowFrequencyLight.color = Color.Lerp(lowFrequencyLight.color, targetColor, Time.deltaTime * 2);
        }

        if (beatLight != null)
        {
            beatLight.intensity = beatIntensity;
        }

        // 频段能量
        kickEnergy = GetBandEnergy(smoothedFftData, 40, 100);
        bassEnergy = GetBandEnergy(smoothedFftData, 60, 250);
        synthEnergy = GetBandEnergy(smoothedFftData, 400, 4000);

        if (kickVfx != null && kickEnergy > kickThreshold)
        {
            kickVfx.SendEvent("OnKick");
            kickVfx.SetFloat("KickBurst", kickEnergy * 50f);
        }

        if (bassVfx != null)
        {
            bassVfx.SetFloat("BassRate", Mathf.Clamp01(bassEnergy * bassSensitivity));
        }

        if (synthVfx != null)
        {
            synthVfx.SetFloat("SynthStrength", Mathf.Clamp01(synthEnergy * synthSensitivity));
        }

        UpdateBars(smoothedFftData);

        // 改进的BPM检测
        DetectBeatImproved(smoothedFftData);
    }

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

        for (int i = 0; i < barCount; i++)
        {
            float logIndex = Mathf.Pow((float)(i + 1) / barCount, logPower);
            int fftIndex = Mathf.Clamp((int)(logIndex * (spectrumData.Length - 1)), 0, spectrumData.Length - 1);

            float rawHeight = spectrumData[fftIndex];
            float scaledHeight = Mathf.Log10(rawHeight + 1);
            float height = Mathf.Clamp(scaledHeight, 0, 10);

            Vector3 newScale = bars[i].transform.localScale;
            newScale.y = height;
            bars[i].transform.localScale = newScale;
        }
    }

    // ==================== 改进的节拍检测算法 ====================

    /// <summary>
    /// 改进的节拍检测算法
    /// 主要改进：
    /// 1. 多频段综合检测（Kick + Snare）
    /// 2. 自适应阈值（基于标准差）
    /// 3. Onset检测（能量变化率）
    /// 4. 相位预测和校正
    /// 5. 置信度评分
    /// 6. 卡尔曼滤波平滑BPM
    /// </summary>
    private void DetectBeatImproved(float[] fft)
    {
        float time = Time.time;

        // ====== 步骤1：计算多频段能量 ======
        float snareEnergy = GetBandEnergy(fft, 150, 300); // Snare通常在150-300Hz

        // ====== 步骤2：维护能量历史 ======
        kickEnergyHistory.Enqueue(kickEnergy);
        snareEnergyHistory.Enqueue(snareEnergy);

        if (kickEnergyHistory.Count > energyHistorySize)
        {
            kickEnergyHistory.Dequeue();
            snareEnergyHistory.Dequeue();
        }

        if (kickEnergyHistory.Count < 10) // 需要足够的历史数据
        {
            previousKickEnergy = kickEnergy;
            previousSnareEnergy = snareEnergy;
            return;
        }

        // ====== 步骤3：计算自适应阈值 ======
        float kickMean = kickEnergyHistory.Average();
        float snareMean = snareEnergyHistory.Average();

        // 计算标准差
        float kickStdDev = CalculateStdDev(kickEnergyHistory.ToArray(), kickMean);
        float snareStdDev = CalculateStdDev(snareEnergyHistory.ToArray(), snareMean);

        // 自适应阈值 = 平均值 + N * 标准差
        dynamicKickThreshold = kickMean + onsetSensitivity * kickStdDev;
        dynamicSnareThreshold = snareMean + onsetSensitivity * snareStdDev;

        // ====== 步骤4：Onset检测（能量突变检测）======
        float kickOnset = kickEnergy - previousKickEnergy;
        float snareOnset = snareEnergy - previousSnareEnergy;

        // 确保是正向突变（能量增加）
        kickOnset = Mathf.Max(0, kickOnset);
        snareOnset = Mathf.Max(0, snareOnset);

        // ====== 步骤5：综合判断是否为节拍 ======
        bool isKickBeat = kickEnergy > dynamicKickThreshold && kickOnset > kickStdDev * 0.5f;
        bool isSnareBeat = snareEnergy > dynamicSnareThreshold && snareOnset > snareStdDev * 0.5f;

        // 计算当前节拍的置信度
        float kickConfidence = isKickBeat ? Mathf.Clamp01((kickEnergy - dynamicKickThreshold) / (kickMean * 0.5f)) : 0f;
        float snareConfidence = isSnareBeat ? Mathf.Clamp01((snareEnergy - dynamicSnareThreshold) / (snareMean * 0.5f)) : 0f;
        float totalConfidence = Mathf.Max(kickConfidence, snareConfidence);

        // ====== 步骤6：相位校正 - 检查是否在预测的节拍窗口内 ======
        float deltaTime = time - lastBeatTime;
        bool inPredictedWindow = false;

        if (predictedNextBeat > 0)
        {
            float timeToPredicted = Mathf.Abs(time - predictedNextBeat);
            float windowSize = beatInterval * 0.15f; // 允许±15%的误差窗口
            inPredictedWindow = timeToPredicted < windowSize;

            // 如果在窗口内，提升置信度
            if (inPredictedWindow)
            {
                totalConfidence *= 1.3f; // 提升30%置信度
            }
        }

        // ====== 步骤7：决定是否接受此节拍 ======
        bool isBeat = (isKickBeat || isSnareBeat) &&
                      totalConfidence > minBeatConfidence &&
                      deltaTime >= minBeatInterval;

        // 如果有预测，还要检查是否超过最大间隔
        if (predictedNextBeat > 0 && deltaTime > maxBeatInterval)
        {
            // 太久没检测到节拍，强制重置
            Debug.Log($"[Beat] 节拍丢失，重置检测");
            ResetBeatDetection();
        }

        // ====== 步骤8：记录节拍 ======
        if (isBeat)
        {
            // 计算节拍强度（用于区分强拍弱拍）
            float beatStrength = kickEnergy + snareEnergy;

            beatTimestamps.Add(deltaTime);
            beatConfidences.Add(totalConfidence);
            beatStrengths.Add(beatStrength);

            // 实时清理：如果新节拍与历史中位数偏差过大，立即检查
            if (beatTimestamps.Count > 4)
            {
                float currentMedian = GetMedian(beatTimestamps.ToArray());
                float deviation = Mathf.Abs(deltaTime - currentMedian) / currentMedian;

                // 如果偏差>50%，这可能是噪音节拍，移除它
                if (deviation > 0.5f)
                {
                    Debug.Log($"[Beat] 检测到异常节拍并移除: {deltaTime:F2}s (中位数: {currentMedian:F2}s, 偏差: {deviation * 100:F1}%)");
                    beatTimestamps.RemoveAt(beatTimestamps.Count - 1);
                    beatConfidences.RemoveAt(beatConfidences.Count - 1);
                    beatStrengths.RemoveAt(beatStrengths.Count - 1);
                }
            }

            // 限制历史记录大小（保持较小的窗口）
            int maxSize = 12;
            if (beatTimestamps.Count > maxSize)
            {
                beatTimestamps.RemoveAt(0);
                beatConfidences.RemoveAt(0);
                beatStrengths.RemoveAt(0);
            }

            lastBeatTime = time;
            lowFrequencyIntensity = 3;

            Debug.Log($"[Beat] 检测到节拍 - 置信度: {totalConfidence:F2}, Kick: {kickEnergy:F2}, Snare: {snareEnergy:F2}, 间隔: {deltaTime:F2}s");
        }

        // ====== 步骤9：定期更新BPM ======
        if (time - lastBpmUpdateTime > bpmUpdateInterval && beatTimestamps.Count >= 4)
        {
            UpdateBPM();
            lastBpmUpdateTime = time;
        }

        // ====== 步骤10：预测下一个节拍 ======
        if (limitedBPM > 0)
        {
            predictedNextBeat = lastBeatTime + beatInterval;

            // 计算相位误差（用于调试）
            if (isBeat && predictedNextBeat > 0)
            {
                phaseError = time - predictedNextBeat;
            }
        }

        // 更新前一帧能量
        previousKickEnergy = kickEnergy;
        previousSnareEnergy = snareEnergy;

        // 手动点击辅助（调试用）
        if (Input.GetMouseButtonDown(0))
        {
            float manualDelta = time - lastBeatTime;
            if (manualDelta > minBeatInterval)
            {
                beatTimestamps.Add(manualDelta);
                beatConfidences.Add(1.0f);
                beatStrengths.Add(kickEnergy + snareEnergy);
                lastBeatTime = time;
                Debug.Log($"[Beat] 手动节拍 - 间隔: {manualDelta:F2}s");
            }
        }

        // ====== 改进的静音检测与渐进式清理 ======
        CheckAndHandleSilence(kickEnergy, snareEnergy, time);
    }

    /// <summary>
    /// 更新BPM（使用加权平均和卡尔曼滤波）
    /// </summary>
    private void UpdateBPM()
    {
        if (beatTimestamps.Count < 4)
            return;

        // ====== 步骤1：计算中位数作为参考 ======
        float median = GetMedian(beatTimestamps.ToArray());

        // ====== 步骤2：异常值过滤并清理原始数据 ======
        List<float> filteredIntervals = new List<float>();
        List<float> filteredConfidences = new List<float>();
        List<float> filteredStrengths = new List<float>();

        for (int i = 0; i < beatTimestamps.Count; i++)
        {
            float interval = beatTimestamps[i];
            float deviation = Mathf.Abs(interval - median) / median;

            // 保留偏差<30%的数据
            if (deviation < 0.3f)
            {
                filteredIntervals.Add(interval);
                filteredConfidences.Add(beatConfidences[i]);
                filteredStrengths.Add(beatStrengths[i]);
            }
            else
            {
                Debug.Log($"[BPM] 移除异常数据: {interval:F2}s (中位数: {median:F2}s, 偏差: {deviation * 100:F1}%)");
            }
        }

        // 如果过滤后数据太少，保留最近的几个
        if (filteredIntervals.Count < 3 && beatTimestamps.Count >= 3)
        {
            Debug.Log($"[BPM] 过滤后数据不足，保留最近3个");
            int startIdx = Mathf.Max(0, beatTimestamps.Count - 3);
            filteredIntervals.Clear();
            filteredConfidences.Clear();
            filteredStrengths.Clear();

            for (int i = startIdx; i < beatTimestamps.Count; i++)
            {
                filteredIntervals.Add(beatTimestamps[i]);
                filteredConfidences.Add(beatConfidences[i]);
                filteredStrengths.Add(beatStrengths[i]);
            }
        }

        // ====== 关键修复：用过滤后的数据替换原始数据 ======
        beatTimestamps = filteredIntervals;
        beatConfidences = filteredConfidences;
        beatStrengths = filteredStrengths;

        // ====== 步骤3：限制历史数据大小（保留最近8-12个） ======
        int maxHistorySize = 10;
        if (beatTimestamps.Count > maxHistorySize)
        {
            int removeCount = beatTimestamps.Count - maxHistorySize;
            beatTimestamps.RemoveRange(0, removeCount);
            beatConfidences.RemoveRange(0, removeCount);
            beatStrengths.RemoveRange(0, removeCount);
            Debug.Log($"[BPM] 清理旧数据，保留最近{maxHistorySize}个");
        }

        if (beatTimestamps.Count < 3)
        {
            Debug.Log($"[BPM] 数据不足，等待更多节拍");
            return;
        }

        // ====== 步骤4：使用置信度加权平均 ======
        float weightedSum = 0f;
        float totalWeight = 0f;

        for (int i = 0; i < beatTimestamps.Count; i++)
        {
            float weight = beatConfidences[i];
            weightedSum += beatTimestamps[i] * weight;
            totalWeight += weight;
        }

        float avgInterval = totalWeight > 0 ? weightedSum / totalWeight : beatTimestamps.Average();

        // ====== 步骤5：计算BPM ======
        float rawBPM = 60f / avgInterval;

        // ====== 步骤6：使用卡尔曼滤波平滑BPM ======
        if (kalmanEstimate == 0)
        {
            kalmanEstimate = rawBPM;
        }
        else
        {
            // 预测步骤
            float predictedEstimate = kalmanEstimate;
            float predictedCovariance = kalmanErrorCovariance + kalmanProcessNoise;

            // 更新步骤
            float kalmanGain = predictedCovariance / (predictedCovariance + kalmanMeasurementNoise);
            kalmanEstimate = predictedEstimate + kalmanGain * (rawBPM - predictedEstimate);
            kalmanErrorCovariance = (1 - kalmanGain) * predictedCovariance;
        }

        detectedBPM = kalmanEstimate;

        // ====== 步骤7：限制BPM范围 ======
        LimitBPM();

        // ====== 步骤8：计算BPM稳定性（方差）======
        bpmVariance = CalculateStdDev(beatTimestamps.ToArray(), avgInterval);

        // 如果BPM很稳定，可以提高置信度阈值
        if (bpmVariance < 0.05f)
        {
            minBeatConfidence = Mathf.Max(0.2f, minBeatConfidence - 0.05f);
        }
        else
        {
            minBeatConfidence = 0.3f;
        }

        Debug.Log($"[BPM] 更新BPM: {Mathf.RoundToInt(limitedBPM)} (原始: {rawBPM:F1}, 平滑: {kalmanEstimate:F1}, 方差: {bpmVariance:F3}, 数据点: {beatTimestamps.Count})");
    }

    /// <summary>
    /// 限制BPM范围到合理值
    /// </summary>
    private void LimitBPM()
    {
        if (detectedBPM > 0)
        {
            float previousLimitedBPM = limitedBPM;
            limitedBPM = detectedBPM;

            // 倍频修正
            while (limitedBPM < 72)
            {
                limitedBPM *= 2;
            }

            while (limitedBPM > 180)
            {
                limitedBPM /= 2;
            }

            // ====== 新增：检测BPM突变 ======
            if (previousLimitedBPM > 0)
            {
                float bpmChange = Mathf.Abs(limitedBPM - previousLimitedBPM) / previousLimitedBPM;

                // 如果BPM变化超过20%，可能是音乐段落变化
                if (bpmChange > 0.2f)
                {
                    Debug.Log($"[BPM] 检测到BPM突变: {previousLimitedBPM:F0} -> {limitedBPM:F0} (变化: {bpmChange * 100:F1}%)");

                    // 如果变化超过40%，清空历史重新检测
                    if (bpmChange > 0.4f)
                    {
                        Debug.Log($"[BPM] BPM变化过大，清空历史数据");
                        beatTimestamps.Clear();
                        beatConfidences.Clear();
                        beatStrengths.Clear();
                        kalmanEstimate = limitedBPM; // 重置卡尔曼滤波
                        kalmanErrorCovariance = 1f;
                    }
                }
            }

            // 更新节拍间隔
            beatInterval = 60f / limitedBPM;
            beatDisplayTime = beatInterval / 4f; // 显示时间为节拍间隔的1/4
        }
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
                silenceStartTime = currentTime;
                wasSilent = true;
                Debug.Log($"[Silence] 检测到静音开始，时间: {currentTime:F2}");
            }

            // 计算静音持续时长
            float silenceDurationSoFar = currentTime - silenceStartTime;

            // ====== 渐进式清理策略 ======

            // 阶段1: 0-1秒 - 不做任何处理（可能只是短暂停顿）
            if (silenceDurationSoFar < 1f)
            {
                // 保持所有数据
                return;
            }

            // 阶段2: 1-2秒 - 减少置信度阈值（降低敏感度，避免误触发）
            else if (silenceDurationSoFar < 2f)
            {
                minBeatConfidence = Mathf.Min(0.5f, minBeatConfidence + 0.1f);
                // Debug.Log($"[Silence] 静音 {silenceDurationSoFar:F1}s，降低敏感度");
            }

            // 阶段3: 2-3秒 - 清理旧数据，保留最近的
            else if (silenceDurationSoFar < silenceDuration)
            {
                // 只保留最近4个节拍时间戳
                if (beatTimestamps.Count > 4)
                {
                    int removeCount = beatTimestamps.Count - 4;
                    beatTimestamps.RemoveRange(0, removeCount);
                    beatConfidences.RemoveRange(0, removeCount);
                    beatStrengths.RemoveRange(0, removeCount);
                }

                // 清理一半的能量历史
                int historyHalf = kickEnergyHistory.Count / 2;
                for (int i = 0; i < historyHalf; i++)
                {
                    if (kickEnergyHistory.Count > 10) // 保留至少10个样本
                    {
                        kickEnergyHistory.Dequeue();
                        snareEnergyHistory.Dequeue();
                    }
                }

                // Debug.Log($"[Silence] 静音 {silenceDurationSoFar:F1}s，清理部分历史数据");
            }

            // 阶段4: 超过设定时长 - 完全重置
            else
            {
                Debug.Log($"[Silence] 静音超过 {silenceDuration}s，完全重置检测");
                ResetBeatDetection();
                silenceStartTime = currentTime; // 重置静音计时，避免重复触发
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

                wasSilent = false;
                silenceStartTime = -1f;
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

    // ==================== 调性检测算法（保持不变）====================

    private void DetectKeyFromFft(float[] fft)
    {
        try
        {
            double[] chroma = ExtractChromaFeatures(fft);

            for (int i = 0; i < 12; i++)
            {
                chromaAccumulator[i] += chroma[i];
            }
            chromaFrameCount++;

            if (chromaFrameCount < chromaAverageFrames)
            {
                return;
            }

            double[] avgChroma = new double[12];
            for (int i = 0; i < 12; i++)
            {
                avgChroma[i] = chromaAccumulator[i] / chromaFrameCount;
            }

            avgChroma = NormalizeChroma(avgChroma);

            Array.Clear(chromaAccumulator, 0, 12);
            chromaFrameCount = 0;

            string detectedKey;
            string detectedMode;
            double bestCorr;

            DetectKeyAndMode(avgChroma, out detectedKey, out detectedMode, out bestCorr);

            if (bestCorr < 0.2)
            {
                return;
            }

            string fullKey = $"{detectedKey} {detectedMode}";
            recentKeys.Enqueue(fullKey);

            if (recentKeys.Count > keyHistorySize)
            {
                recentKeys.Dequeue();
            }

            var keyVotes = recentKeys.GroupBy(k => k)
                                     .OrderByDescending(g => g.Count())
                                     .First();

            string mostVotedKey = keyVotes.Key;

            string currentFullKey = $"{currentKey} {currentMode}";
            if (mostVotedKey != currentFullKey && keyVotes.Count() >= keyHistorySize / 3)
            {
                string[] parts = mostVotedKey.Split(' ');
                currentKey = parts[0];
                currentMode = parts[1];

                lastKeyUpdateTime = Time.time;
                OnKeyChanged();

                Debug.Log($"[Key] 🎵 Key changed to: {currentKey} {currentMode} (confidence: {bestCorr:F3})");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KeyDetection] Error detecting key: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private double[] ExtractChromaFeatures(float[] fft)
    {
        double[] chroma = new double[12];
        int sampleRate = waveSource.WaveFormat.SampleRate;
        double freqResolution = (double)sampleRate / fft.Length;

        int minBin = Mathf.Max(1, (int)(80.0 / freqResolution));
        int maxBin = Mathf.Min(fft.Length - 1, (int)(4000.0 / freqResolution));

        for (int i = minBin; i <= maxBin; i++)
        {
            double freq = i * freqResolution;

            if (fft[i] < 0.0005f)
                continue;

            double midiNote = 12.0 * Math.Log(freq / 440.0, 2.0) + 69.0;
            int noteClass = ((int)Math.Round(midiNote) % 12 + 12) % 12;

            double weight = Math.Log10(fft[i] + 1e-10) + 20;

            double fractionalPart = midiNote - Math.Floor(midiNote);
            double currentWeight = weight * Math.Exp(-0.5 * fractionalPart * fractionalPart);
            double nextWeight = weight * Math.Exp(-0.5 * (1 - fractionalPart) * (1 - fractionalPart));

            chroma[noteClass] += Math.Max(0, currentWeight);
            chroma[(noteClass + 1) % 12] += Math.Max(0, nextWeight);
        }

        return chroma;
    }

    private double[] NormalizeChroma(double[] chroma)
    {
        double sum = 0;
        for (int i = 0; i < chroma.Length; i++)
        {
            sum += chroma[i] * chroma[i];
        }

        double norm = Math.Sqrt(sum);
        if (norm < 1e-10)
        {
            return chroma;
        }

        double[] normalized = new double[chroma.Length];
        for (int i = 0; i < chroma.Length; i++)
        {
            normalized[i] = chroma[i] / norm;
        }

        return normalized;
    }

    private void DetectKeyAndMode(double[] chroma, out string key, out string mode, out double confidence)
    {
        double maxCorrMajor = double.MinValue;
        double maxCorrMinor = double.MinValue;
        int bestKeyMajor = 0;
        int bestKeyMinor = 0;

        for (int shift = 0; shift < 12; shift++)
        {
            double corrMajor = CalculatePearsonCorrelation(chroma, majorProfile, shift);
            if (corrMajor > maxCorrMajor)
            {
                maxCorrMajor = corrMajor;
                bestKeyMajor = shift;
            }

            double corrMinor = CalculatePearsonCorrelation(chroma, minorProfile, shift);
            if (corrMinor > maxCorrMinor)
            {
                maxCorrMinor = corrMinor;
                bestKeyMinor = shift;
            }
        }

        if (maxCorrMajor > maxCorrMinor + keyConfidenceThreshold)
        {
            key = keyNames[bestKeyMajor];
            mode = "Major";
            confidence = maxCorrMajor;
        }
        else if (maxCorrMinor > maxCorrMajor + keyConfidenceThreshold)
        {
            key = keyNames[bestKeyMinor];
            mode = "Minor";
            confidence = maxCorrMinor;
        }
        else
        {
            if (currentMode == "Major")
            {
                key = keyNames[bestKeyMajor];
                mode = "Major";
                confidence = maxCorrMajor;
            }
            else
            {
                key = keyNames[bestKeyMinor];
                mode = "Minor";
                confidence = maxCorrMinor;
            }
        }
    }

    private double CalculatePearsonCorrelation(double[] chroma, double[] template, int shift)
    {
        double chromaMean = 0;
        double templateMean = 0;
        for (int i = 0; i < 12; i++)
        {
            chromaMean += chroma[i];
            templateMean += template[(i + shift) % 12];
        }
        chromaMean /= 12.0;
        templateMean /= 12.0;

        double numerator = 0;
        double chromaVar = 0;
        double templateVar = 0;

        for (int i = 0; i < 12; i++)
        {
            double chromaDiff = chroma[i] - chromaMean;
            double templateDiff = template[(i + shift) % 12] - templateMean;

            numerator += chromaDiff * templateDiff;
            chromaVar += chromaDiff * chromaDiff;
            templateVar += templateDiff * templateDiff;
        }

        double denominator = Math.Sqrt(chromaVar * templateVar);
        if (denominator < 1e-10)
        {
            return 0;
        }

        return numerator / denominator;
    }

    private void OnKeyChanged()
    {
        float deltaTime = Time.time - lastBeatTime;
        if (deltaTime > minBeatInterval)
        {
            beatTimestamps.Add(deltaTime);
            beatConfidences.Add(0.5f); // 调性变化时给予中等置信度
            beatStrengths.Add(kickEnergy + GetBandEnergy(smoothedFftData, 150, 300));
        }
        lastBeatTime = Time.time;
    }

    private float GetBandEnergy(float[] spectrum, float fMin, float fMax)
    {
        int sampleRate = waveSource.WaveFormat.SampleRate;
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

        GUI.Label(new Rect(20, 20, 300, 50), $"BPM: {Mathf.RoundToInt(limitedBPM)}", style);
        GUI.Label(new Rect(20, 60, 300, 50), $"Key: {currentKey} {currentMode}", style);

        if (showBeatText)
        {
            GUI.Label(new Rect(20, 90, 200, 50), $"🎵 BEAT 🎵 {beat} 🎵", style);
        }

        GUI.Label(new Rect(20, 120, 400, 50), $"Kick: {kickEnergy:F3} (T: {dynamicKickThreshold:F3})", style);
        GUI.Label(new Rect(20, 150, 500, 50), $"Confidence: {(beatConfidences.Count > 0 ? beatConfidences.Last() : 0):F2}", style);
        GUI.Label(new Rect(20, 180, 500, 50), $"Variance: {bpmVariance:F3}", style);

        // 显示静音状态
        if (wasSilent && silenceStartTime > 0)
        {
            float silenceDur = Time.time - silenceStartTime;
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(20, 210, 500, 50), $"⚠️ 静音: {silenceDur:F1}s / {silenceDuration:F1}s", style);
            style.normal.textColor = Color.green;
        }

        // 显示节拍历史
        if (beatTimestamps.Count > 0)
        {
            string intervals = string.Join(", ", beatTimestamps.Select(t => $"{t:F2}"));
            GUI.Label(new Rect(20, 240, 800, 50), $"Intervals: {intervals}", style);
        }
    }

    void OnDisable()
    {
        Debug.Log("[AudioVisualizerCSCore] Disposing capture and waveSource.");
        capture?.Stop();
        capture?.Dispose();
        waveSource?.Dispose();
    }
}