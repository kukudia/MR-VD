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
    private WasapiLoopbackCapture capture; // 捕获系统音频输出 / Capture system audio output
    private IWaveSource waveSource; // 音频源，用于读取采样数据 / Audio source for reading sample data
    private SingleBlockNotificationStream notificationStream; // 通知流，每当音频数据准备好时触发事件 / Notification stream, triggers events when audio data is ready
    private FftProvider fftProvider; // FFT 提供者，用于频谱分析 / FFT provider for spectrum analysis

    private const int fftSize = 2048; // FFT 数据大小 / Size of FFT data
    public float[] frequencyData;
    private float[] averageSamples; // 平均采样数据 / Averaged samples
    public float[] smoothedFftData; // 平滑后的频谱数据 / Smoothed FFT data
    public bool linearFftData;
    public bool movingBars;
    public float smoothingWeight = 0.5f; // 频率数据平滑因子 / Smoothing factor for frequency data
    public float logPower = 1f;
    public int lowFrequencyRange = 256;
    public GameObject barPrefab; // 3D 物体的 prefab，例如 cube / Prefab for 3D object, e.g., cube
    public Light lowFrequencyLight;
    public Light beatLight;
    public float lowFrequencyIntensity;
    public float beatIntensity;
    public int barCount = 64; // 频谱柱体的数量 / Number of bars in the spectrum
    public Transform barPosition;
    public float brightness = 5;
    public float maxBrightness = 20;
    public float horizontalScale = 0.01f; // 水平方向的比例 / Horizontal scale
    public float verticalScale = 1f; // 垂直方向的比例 / Vertical scale adjustment
    public float a = 5; // 控制柱体排列的参数 / Parameter for bar arrangement
    public float b = 1; // 控制柱体排列的参数 / Parameter for bar arrangement
    private GameObject[] bars; // 用于存储柱状体 / Array for storing bar GameObjects

    // BPM 检测相关字段
    public int beat = 0;
    private Queue<float> recentLowFreqEnergies = new Queue<float>();
    public int energyHistorySize = 50; // 最近50帧
    //private float lowFreqSum = 0f;
    public List<float> beatTimestamps = new List<float>();
    public float lastBeatTime = 0f;
    public float lastBpmUpdateTime = 0f;
    public float lastDetectTime = 0f;
    public float lastAddTime = 0f;
    public float lastInitTime = 0f;
    public float bpmUpdateInterval = 1.5f; // 多久刷新一次 BPM
    public float detectedBPM = 0f;
    public float limitedBPM = 0f;
    public float dynamicThreshold;
    public float dynamicThresholdOffset = 1.5f;

    public float deltaTimeOffset = 0.1f;
    public float beatInterval = 0.5f;
    public bool showBeatText = false;
    public float beatDisplayTime = 0.2f;
    private float beatTimer = 0f;

    private string currentKey = "Unknown";
    private float lastKeyUpdateTime = 0f;
    private float keyUpdateInterval = 1.5f;

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
            // 初始化柱体 / Initialize bars
            bars = new GameObject[barCount];
            for (int i = 0; i < barCount; i++)
            {
                // 实例化立方体并将其放置在场景中 / Instantiate cube and place it in the scene
                GameObject bar = Instantiate(barPrefab, transform);
                float x = -barCount / 2 * horizontalScale + i * horizontalScale; // 横向位置 / Horizontal position
                float z = Mathf.Sqrt(1 - (x * x) / (a * a)) * b; // 纵向位置 / Vertical position
                bar.transform.position = new Vector3(x, transform.position.y, z);
                bars[i] = bar;
            }
        }

        // 调用初始化捕获的函数 / Call the capture initialization function
        InitializeCapture();
    }

    private void InitializeCapture()
    {
        try
        {
            capture?.Stop();
            capture?.Dispose();
            Debug.Log("[AudioVisualizerCSCore] Stopped and disposed previous capture."); // 停止并释放之前的捕获 / Stopped and disposed previous capture

            // 初始化 WasapiLoopbackCapture / Initialize WasapiLoopbackCapture
            capture = new WasapiLoopbackCapture();
            Debug.Log("[AudioVisualizerCSCore] Created WasapiLoopbackCapture.");
            capture.Initialize();
            Debug.Log("[AudioVisualizerCSCore] Capture initialized.");

            if (capture.Device == null)
            {
                Debug.LogError("[AudioVisualizerCSCore] No default audio output device found."); // 没有找到默认音频输出设备 / No default audio output device found
                return;
            }

            // 打印捕获设备信息 / Log information about the capture device
            Debug.Log($"[AudioVisualizerCSCore] Using device: {capture.Device.FriendlyName}, State: {capture.Device.DeviceState}");

            var sampleSource = new SoundInSource(capture) { FillWithZeros = false }.ToSampleSource(); // 创建采样源 / Create sample source
            waveSource = sampleSource.ToWaveSource();
            notificationStream = new SingleBlockNotificationStream(sampleSource);
            Debug.Log("[AudioVisualizerCSCore] Audio stream and notification stream initialized.");

            fftProvider = new FftProvider(waveSource.WaveFormat.Channels, FftSize.Fft2048); // 初始化 FFT 提供者 / Initialize FFT provider
            Debug.Log("[AudioVisualizerCSCore] FFT Provider created with channels: " + waveSource.WaveFormat.Channels);

            capture.DataAvailable += (s, args) =>
            {
                // 将字节数据转换为浮点数组（假设为 32 位浮点格式） / Convert byte data to float array (assuming 32-bit float format)
                float[] buffer = new float[args.ByteCount / 4]; // 根据实际格式调整 / Adjust based on the actual format
                Buffer.BlockCopy(args.Data, 0, buffer, 0, args.ByteCount);

                // 将采样数据添加到 FFT 提供者 / Add samples to FFT provider
                for (int i = 0; i < buffer.Length; i += waveSource.WaveFormat.Channels)
                {
                    // 限制频率范围（假设高频采样从某个阈值之后开始） / Limit frequency range (e.g., reduce high frequencies)
                    if (waveSource.WaveFormat.SampleRate > 2205) // 示例阈值为 22050 Hz / Example threshold of 22050 Hz
                    {
                        if (i / waveSource.WaveFormat.SampleRate < 0.6f) // 保留低于 70% 采样率的频率 / Keep frequencies below 70% of sample rate
                        {
                            if (waveSource.WaveFormat.Channels == 2) // 立体声 / Stereo
                            {
                                fftProvider.Add(buffer[i], buffer[i + 1]); // 传递左右声道 / Pass left and right channels
                            }
                            else // 单声道 / Mono
                            {
                                fftProvider.Add(buffer[i], buffer[i]);
                            }
                        }
                    }
                }
            };

            capture.Start(); // 开始捕获 / Start capture
            Debug.Log("[AudioVisualizerCSCore] Capture started.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AudioVisualizerCSCore] Error initializing audio capture: {ex.Message}"); // 捕获初始化错误 / Error initializing audio capture
        }
    }

    void FixedUpdate()
    {
        if (fftProvider != null)
        {
            float[] fftBuffer = new float[fftSize]; // FFT 缓冲区 / FFT buffer
            bool hasFftData = fftProvider.GetFftData(fftBuffer); // 获取 FFT 数据 / Get FFT data

            if (hasFftData)
            {
                ProcessFftData(fftBuffer); // 处理 FFT 数据 / Process FFT data
            }
            else
            {
                //Debug.LogWarning("[AudioVisualizerCSCore] No FFT data available."); // 没有可用的 FFT 数据 / No FFT data available
            }
        }
    }

    private void ProcessFftData(float[] fftBuffer)
    {
        int dataLength = fftBuffer.Length;
        frequencyData = new float[dataLength];

        for (int i = 0; i < dataLength; i++)
        {
            if (linearFftData)
            {
                frequencyData[i] = fftBuffer[i] * verticalScale; // 线性变换
            }
            else
            {
                frequencyData[i] = Mathf.Log10(fftBuffer[i] + 1) * verticalScale;
            }
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

                //Debug.Log("BEAT");
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

        // 控制显示时间
        if (showBeatText)
        {
            beatTimer -= Time.deltaTime;
            if (beatTimer <= 0f)
            {
                showBeatText = false;
            }
        }

        // 使用低频平均值更新灯光亮度 / Update light intensity based on low-frequency average
        if (lowFrequencyLight != null)
        {
            //light.intensity = Mathf.Clamp(lowFrequencyAverage * brightness, 0, maxBrightness); // 限制亮度范围在 0 到 5 之间 / Clamp intensity range [0, 5]
            lowFrequencyLight.intensity = lowFrequencyIntensity;

            // 随时间变化的色相值 / Time-based hue value
            float hue = Mathf.Repeat(Time.time * 0.01f, 1f); // 色相范围在 0 到 1 之间，速度为 0.01 倍时间 / Hue range [0,1], speed scaled by 0.01
            Color targetColor = Color.HSVToRGB(hue, 1f, 1f); // 将 HSV 转换为 RGB（饱和度和亮度为 1） / Convert HSV to RGB (saturation and value are 1)

            // 平滑颜色变化 / Smooth the color transition
            lowFrequencyLight.color = Color.Lerp(lowFrequencyLight.color, targetColor, Time.deltaTime * 2); // 时间插值控制平滑速度 / Time-based interpolation for smooth transition
        }

        if (beatLight != null)
        {
            beatLight.intensity = beatIntensity;
        }

        // 🎵 频段能量
        kickEnergy = GetBandEnergy(smoothedFftData, 40, 100);
        bassEnergy = GetBandEnergy(smoothedFftData, 60, 250);
        synthEnergy = GetBandEnergy(smoothedFftData, 400, 4000);

        // Kick → 触发 VFX 事件
        if (kickVfx != null && kickEnergy > kickThreshold)
        {
            kickVfx.SendEvent("OnKick");
            kickVfx.SetFloat("KickBurst", kickEnergy * 50f);
        }

        // Bass → 控制发射率
        if (bassVfx != null)
        {
            bassVfx.SetFloat("BassRate", Mathf.Clamp01(bassEnergy * bassSensitivity));
        }

        // Synth → 控制粒子大小 / 强度
        if (synthVfx != null)
        {
            synthVfx.SetFloat("SynthStrength", Mathf.Clamp01(synthEnergy * synthSensitivity));
        }

        UpdateBars(smoothedFftData);

        // ---- 🎵 BPM 检测区域 ----
        DetectBeat(frequencyData);
    }

    private void UpdateBars(float[] spectrumData)
    {
        //int stepSize = Mathf.FloorToInt(spectrumData.Length / barCount); // 计算步长 / Calculate step size
        //float lowFrequencySum = 0f;

        //for (int i = 0; i < lowFrequencyRange; i++)
        //{
        //    lowFrequencySum += spectrumData[i];
        //}

        // 计算平均低频幅度 / Calculate average low-frequency amplitude
        //float lowFrequencyAverage = lowFrequencySum / lowFrequencyRange;

        if (movingBars)
        {
            List<GameObject> barObjects = new List<GameObject>();
            if (Time.time - lastBeatTime >= beatInterval)
            {
                // 初始化柱体 / Initialize bars
                bars = new GameObject[barCount];
                for (int i = 0; i < barCount; i++)
                {
                    // 实例化立方体并将其放置在场景中 / Instantiate cube and place it in the scene
                    GameObject bar = Instantiate(barPrefab, transform);
                    float x = -barCount / 2 * horizontalScale + i * horizontalScale; // 横向位置 / Horizontal position
                    float z = Mathf.Sqrt(1 - (x * x) / (a * a)) * b; // 纵向位置 / Vertical position
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
            // 对数映射的索引（0 ~ 1 再映射到 fftIndex）
            float logIndex = Mathf.Pow((float)(i + 1) / barCount, logPower); // 指数控制分布密度（可以改为 log、sqrt、pow）
            int fftIndex = Mathf.Clamp((int)(logIndex * (spectrumData.Length - 1)), 0, spectrumData.Length - 1);

            float rawHeight = spectrumData[fftIndex];
            float scaledHeight = Mathf.Log10(rawHeight + 1);
            float height = Mathf.Clamp(scaledHeight, 0, 10);

            Vector3 newScale = bars[i].transform.localScale;
            newScale.y = height;
            bars[i].transform.localScale = newScale;
        }
    }

    private void DetectBeat(float[] fft)
    {
        //// 计算当前窗口的低频总能量（重置后再累加）
        //lowFreqSum = 0f; // 关键修复：每次检测前重置为0
        //for (int i = 0; i < lowFrequencyRange; i++)
        //{
        //    lowFreqSum += fft[i];
        //}

        float time = Time.time;

        // 维护滑动窗口（存储当前窗口的低频能量）
        recentLowFreqEnergies.Enqueue(kickEnergy);
        if (recentLowFreqEnergies.Count > energyHistorySize)
        {
            recentLowFreqEnergies.Dequeue();
        }

        if (time - lastBpmUpdateTime > bpmUpdateInterval * 2 && beatTimestamps.Count > 0 && beatTimestamps.Count < 4)
        {
            lastBpmUpdateTime = time;
            beatTimestamps.RemoveAt(beatTimestamps.Count - 1);
            Debug.Log($"清空缓存");
        }

        if (recentLowFreqEnergies.Count == 0)
        {
            return;
        }

        float avgBeatTime = 0;
        if (beatTimestamps.Count > 0)
        {
            avgBeatTime = beatTimestamps.Average();
        }

        float deltaTime = time - lastDetectTime;
        if (deltaTime > 1.2f)
        {
            while (deltaTime > 1.2f)
            {
                deltaTime /= 2;
            }
        }
        else if (deltaTime < 0.5f)
        {
            //deltaTime *= 2;
        }

        // 计算动态平均阈值（基于当前窗口的能量）
        float avgEnergy = recentLowFreqEnergies.Average();

        deltaTimeOffset = (time - lastAddTime) * 0.03f;
        dynamicThresholdOffset = Mathf.Lerp(2f, 1.2f, (time - lastAddTime) / 10);
        dynamicThreshold = avgEnergy * dynamicThresholdOffset; // 动态阈值


        //if (lowFreqSum > dynamicThreshold && Mathf.Abs(deltaTime - 60f / Mathf.Max(detectedBPM, 72f)) < 0.2f)
        if (kickEnergy > dynamicThreshold)
        {
            Debug.Log($"当前低频能量: {kickEnergy:F2}, 平均能量: {avgEnergy:F2}, 动态阈值: {dynamicThreshold:F2}, 时间差{deltaTime}, 数据队列{recentLowFreqEnergies.Count}");
            if (deltaTime > 0.3f)
            {
                if (beatTimestamps.Count >= 1)
                {
                    if (Mathf.Abs(deltaTime - avgBeatTime) < deltaTimeOffset)
                    {
                        beatTimestamps.Add(deltaTime);
                        lastAddTime = time;
                    }
                }
                else
                {
                    beatTimestamps.Add(deltaTime);
                }

                lowFrequencyIntensity = 3;

                lastDetectTime = time;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            beatTimestamps.Add(time - lastDetectTime);
            lastDetectTime = Time.time;
            lastBeatTime = Time.time;
        }

        if (kickEnergy < 0.001)
        {
            recentLowFreqEnergies.Dequeue();
            beatTimestamps.Clear();
            detectedBPM = 0f;
            limitedBPM = 0f;
        }


        // 定期更新BPM（每bpmUpdateInterval秒，且至少有4个时间戳）
        if (time - lastBpmUpdateTime > bpmUpdateInterval && beatTimestamps.Count >= 2)
        {
            lastBpmUpdateTime = time;

            detectedBPM = 60f / avgBeatTime;

            if (beatTimestamps.Count > 4)
            {
                var sorted = beatTimestamps.OrderBy(x => Math.Abs(x - avgBeatTime)).ToList();
                beatTimestamps.RemoveRange(0, beatTimestamps.Count - 4);
            }

            LimitBPM(); // 限制BPM范围
        }
    }

    private void LimitBPM()
    {
        if (detectedBPM > 0)
        {
            limitedBPM = detectedBPM;
            while (limitedBPM < 72)
            {
                limitedBPM *= 2;
            }

            while (limitedBPM > 180)
            {
                limitedBPM /= 2;
            }
            beatDisplayTime = 60 / limitedBPM / 2 / 2;
        }

        Debug.Log($"[BPM] Detected BPM: {Mathf.RoundToInt(limitedBPM)}");
    }

    private void DetectKeyFromFft(float[] fft)
    {
        try
        {
            int chromaBins = 12;
            double[] chroma = new double[chromaBins];

            // 简化版 chroma 提取（从 FFT 映射频率到 12 半音）
            for (int i = 0; i < fft.Length; i++)
            {
                double freq = i * waveSource.WaveFormat.SampleRate / fft.Length;
                int bin = (int)Math.Round(12 * Math.Log(freq / 440.0, 2)) % 12;
                if (bin >= 0 && bin < 12)
                {
                    chroma[bin] += fft[i];
                }
            }

            // Krumhansl-Schmuckler 模板（C大调）
            double[] majorTemplate = {6.35, 2.23, 3.48, 2.33, 4.38, 4.09,
                                  2.52, 5.19, 2.39, 3.66, 2.29, 2.88};

            // 计算与各调的相似度
            double maxCorr = double.MinValue;
            int bestKey = 0;

            for (int shift = 0; shift < 12; shift++)
            {
                double corr = 0.0;
                for (int i = 0; i < 12; i++)
                {
                    corr += chroma[i] * majorTemplate[(i + shift) % 12];
                }
                if (corr > maxCorr)
                {
                    maxCorr = corr;
                    bestKey = shift;
                }
            }

            string[] keyNames = { "C", "C#", "D", "D#", "E", "F",
                              "F#", "G", "G#", "A", "A#", "B" };
            currentKey = keyNames[bestKey];
            //Debug.Log($"[Key] Detected Key: {currentKey}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KeyDetection] Error detecting key: {ex.Message}");
        }
    }

    // 获取频段能量
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

        return sum / (imax - imin + 1); // 平均能量
    }


    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 32;
        style.normal.textColor = Color.green;
        GUI.Label(new Rect(20, 20, 300, 50), $"BPM: {Mathf.RoundToInt(limitedBPM)}", style);
        GUI.Label(new Rect(20, 60, 300, 50), $"Key: {currentKey}", style);
        if (showBeatText)
        {
            GUI.Label(new Rect(20, 90, 200, 50), $"🎵 BEAT 🎵 {beat} 🎵", style);
        }
        //GUI.Label(new Rect(20, 120, 300, 50), $"LowFreq: {lowFreqSum / lowFrequencyRange:F2}", style);
        GUI.Label(new Rect(20, 120, 300, 50), $"LowFreq: {kickEnergy:F2}", style);
        GUI.Label(new Rect(20, 150, 500, 50), $"RECENT: {frequencyData.Average():F2}", style);
    }

    void OnDisable()
    {
        Debug.Log("[AudioVisualizerCSCore] Disposing capture and waveSource."); // 释放资源 / Dispose of resources
        capture?.Stop();
        capture?.Dispose();
        waveSource?.Dispose();
    }
}