using System;
using System.Linq;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// 高级版：把 FFT 频谱数据转换为（1）滚动二维矩阵纹理（瀑布图），（2）一维 GraphicsBuffer（当前帧能量条），
/// 用于驱动 VFX Graph 的可视化（例如频谱墙、音频山脉、流光等）。
///
/// 使用方式：
/// 1) 将本脚本挂到一个 GameObject 上，分配 VisualEffect（VFX Graph 的实例组件）。
/// 2) 选择数据来源：
///    - ExternalFFT：从外部脚本/系统传入 spectrum（调用 FeedFFT(...)）。
///    - FromComponent：勾选 “bindToComponent”，并把你的 AudioVisualizerCSCore 拖进来（要求它能提供频谱数组）。
/// 3) 在 VFX Graph 中暴露以下属性并匹配：
///    - Texture2D  "FFTTexture" （二维滚动纹理，width=bars, height=history）
///    - GraphicsBuffer<float> "FFTBuffer" （当前帧的一维能量条，长度=bars）
///    - 可选：float "GlobalGain"（全局增益，脚本会设置）
/// 4) 你可以在 VFX 中用 Sample Texture2D/Graphics Buffer 采样驱动粒子位置/大小/颜色。
///
/// 关键特性：
/// - 频段映射：线性/对数频率映射，支持 logPower 调整低频密度。
/// - 动态范围：支持 Log 压缩 / 幂函数压缩 / 峰值归一化 / EMA 平滑。
/// - 瀑布图：支持历史帧滚动显示（history 行），可在 VFX 中做 3D 高度或条形动画。
/// - 高性能：使用 RFloat 纹理与 GraphicsBuffer；仅写入一行像素；可选 Burst 行为仍由 VFX Graph 完成。
///
/// 注意：
/// - sampleRate 需正确设置（外部传入或手动填写），以保证频率→FFT bin 映射准确。
/// - FFT 数组通常只需要使用 0..N/2（Nyquist）部分。
/// - 本脚本不直接做音频捕获，建议与你已有的 CSCore 可视化管线配合使用。
/// </summary>
public class FftToVfxMatrix : MonoBehaviour
{
    public enum DataSource
    {
        ExternalFFT,   // 由其它系统调用 FeedFFT 传入
        FromComponent  // 从指定组件读取（如你的 AudioVisualizerCSCore）
    }

    public enum CompressMode
    {
        None,
        Log10,      // y = log10(1 + gain * x)
        PowerGamma  // y = pow(x, gamma)
    }

    [Header("VFX Graph 绑定")]
    public VisualEffect vfx;
    public string textureProperty = "FFTTexture";     // VFX Graph 暴露的 Texture2D 属性名
    public string bufferProperty = "FFTBuffer";      // VFX Graph 暴露的 GraphicsBuffer<float> 属性名
    public string gainProperty = "GlobalGain";     // 可选：全局增益传入 VFX（没有也可）

    [Header("数据来源")]
    public DataSource source = DataSource.ExternalFFT;
    public bool bindToComponent = false;
    [Tooltip("当从组件读取时，拖入能提供频谱数组的脚本（例如你现有的 AudioVisualizerCSCore）。\n需要该组件提供 public float[] spectrum/fftData 字段或属性。")]
    public MonoBehaviour spectrumProvider; // 反射读取字段名
    public string spectrumFieldName = "smoothedFftData"; // 你的组件里的频谱数组字段名（默认匹配你给的脚本）

    [Header("FFT 参数")]
    [Tooltip("音频采样率（Hz）。如果外部没有提供，请手动填写与你的音频设备一致的值（如 44100/48000）。")]
    public int sampleRate = 48000;
    [Tooltip("FFT长度（仅用于频率→索引映射）。与外部 FFT 数组长度一致（如 2048）。")]
    public int fftSize = 2048;
    [Tooltip("是否只使用 Nyquist 之前（N/2）的数据。大多数实数FFT建议勾选。")]
    public bool useHalfSpectrum = true;

    [Header("频段映射（频谱条）")]
    [Range(8, 1024)] public int bars = 128;  // X 方向：频段条数
    [Range(8, 2048)] public int history = 256; // Y 方向：历史帧数（瀑布高度）
    [Tooltip("频率下限（Hz），建议 20~40Hz 之间")]
    public float fMin = 20f;
    [Tooltip("频率上限（Hz），通常 <= Nyquist = sampleRate/2")]
    public float fMax = 18000f;
    [Tooltip("使用对数频率映射（更重低频）")]
    public bool logFrequency = true;
    [Tooltip("对数分布力度，>1 增加低频密度，=1 均匀（log 线性），<1 减少低频密度")]
    [Range(0.25f, 4f)] public float logPower = 1.3f;

    [Header("能量处理")]
    public CompressMode compressMode = CompressMode.Log10;
    [Tooltip("用于 Log10 压缩：y = log10(1 + gain * x)")]
    [Range(0.1f, 100f)] public float gain = 10f;
    [Tooltip("用于 Gamma 压缩：y = pow(x, gamma)")]
    [Range(0.1f, 2.5f)] public float gamma = 0.6f;
    [Tooltip("单条能量的 EMA 平滑系数（越大越平滑）")]
    [Range(0f, 0.99f)] public float ema = 0.5f;
    [Tooltip("去噪门限（进入处理前先减去floor并截断<0 的值）")]
    [Range(0f, 0.1f)] public float noiseFloor = 0.005f;
    [Tooltip("滚动最大值归一化的衰减速度（越大衰减越快）")]
    [Range(0.9f, 0.9999f)] public float rollingMaxDecay = 0.98f;

    [Header("纹理 / 缓冲")]
    public TextureFormat textureFormat = TextureFormat.RFloat; // 单通道高精度，PC/VR 建议
    public FilterMode textureFilter = FilterMode.Bilinear;
    public bool generateTexture = true;
    public bool generateBuffer = true;

    [Header("调试显示")]
    public bool drawGizmos = false;
    public float gizmoHeight = 1f;

    // 运行期
    private Texture2D tex;
    private Color[] rowColors;        // 仅一行
    private GraphicsBuffer buffer;    // bars x float
    private float[] barsNow;          // 当前帧的 bars 能量
    private float[] barsEma;          // EMA 平滑
    private float[] barsMax;          // 滚动最大值（归一化）

    private int writeRow = 0;         // 纹理当前写入的行（从下往上或环形）
    private int[] binStart, binEnd;   // 每个 bar 对应的 FFT bin 范围（预计算）

    // 外部数据注入缓存（ExternalFFT 模式）
    private float[] lastExternalFft;

    // 反射缓存
    private System.Reflection.FieldInfo spectrumField;

    void Awake()
    {
        barsNow = new float[bars];
        barsEma = new float[bars];
        barsMax = new float[bars];
        for (int i = 0; i < bars; i++) barsMax[i] = 1e-6f; // 避免除0

        PrecomputeBarBinRanges();
        AllocateOutputs();
        CacheSpectrumField();
        PushStaticsToVfx();
    }

    void OnValidate()
    {
        fMax = Mathf.Min(fMax, sampleRate * 0.5f);
    }

    void OnDestroy()
    {
        if (buffer != null)
        {
            buffer.Release();
            buffer = null;
        }
    }

    /// <summary>
    /// 外部调用：传入一帧 FFT 幅度数组（建议已是幅度或功率谱，长度=fftSize 或 N/2）。
    /// </summary>
    public void FeedFFT(float[] fftMagnitudes, int providedSampleRate = -1, int providedFftSize = -1)
    {
        if (fftMagnitudes == null || fftMagnitudes.Length == 0) return;
        lastExternalFft = fftMagnitudes;
        if (providedSampleRate > 0) sampleRate = providedSampleRate;
        if (providedFftSize > 0) fftSize = providedFftSize;
    }

    void Update()
    {
        float[] src = TryGetSpectrumFromSource();
        if (src == null) return;

        // 只取有效一半（实数 FFT），可选
        int usableLen = useHalfSpectrum ? Mathf.Min(src.Length, fftSize / 2) : Mathf.Min(src.Length, fftSize);

        // 计算每个 bar 的能量
        for (int i = 0; i < bars; i++)
        {
            int s = Mathf.Clamp(binStart[i], 0, usableLen - 1);
            int e = Mathf.Clamp(binEnd[i], 0, usableLen - 1);
            if (e < s) e = s;

            double sum = 0.0;
            for (int k = s; k <= e; k++)
            {
                float v = src[k];
                v = Mathf.Max(0f, v - noiseFloor); // 去噪门限
                sum += v;
            }
            float avg = (float)(sum / Math.Max(1, e - s + 1));

            // 压缩
            float comp = ApplyCompression(avg);

            // EMA 平滑
            float smoothed = Mathf.Lerp(comp, barsEma[i], ema);
            barsEma[i] = smoothed;

            // 滚动最大值（用于归一化，避免不同音量下显示过暗/过亮）
            barsMax[i] = Mathf.Max(barsMax[i] * rollingMaxDecay, smoothed);
            barsMax[i] = Mathf.Max(barsMax[i], 1e-6f);

            // 归一化到 0..1
            barsNow[i] = Mathf.Clamp01(smoothed / barsMax[i]);
        }

        // 写入纹理与缓冲
        if (generateTexture) WriteRowToTexture(barsNow);
        if (generateBuffer) WriteToBuffer(barsNow);

        // 将一些全局参数传到 VFX（可选）
        if (vfx != null && !string.IsNullOrEmpty(gainProperty))
            vfx.SetFloat(gainProperty, gain);
    }

    #region 核心：映射、压缩、输出

    private void PrecomputeBarBinRanges()
    {
        binStart = new int[bars];
        binEnd = new int[bars];

        float nyquist = sampleRate * 0.5f;
        float fLo = Mathf.Clamp(fMin, 1f, nyquist);
        float fHi = Mathf.Clamp(fMax, fLo + 1f, nyquist);

        for (int i = 0; i < bars; i++)
        {
            // 0..1 的采样位置
            float t = (i + 0.5f) / bars;

            float fCenter;
            if (logFrequency)
            {
                // 对数空间线性插值 + 可选幂次调整
                float logLo = Mathf.Log(fLo);
                float logHi = Mathf.Log(fHi);
                float tl = Mathf.Pow(t, logPower);
                float lf = Mathf.Lerp(logLo, logHi, tl);
                fCenter = Mathf.Exp(lf);
            }
            else
            {
                fCenter = Mathf.Lerp(fLo, fHi, t);
            }

            // 给每个条分配一个带宽（等间距/相对带宽）。这里用邻域差近似带宽。
            float t0 = (i + 0f) / bars;
            float t1 = (i + 1f) / bars;

            float f0 = logFrequency ? Mathf.Exp(Mathf.Lerp(Mathf.Log(fLo), Mathf.Log(fHi), Mathf.Pow(t0, logPower)))
                                    : Mathf.Lerp(fLo, fHi, t0);
            float f1 = logFrequency ? Mathf.Exp(Mathf.Lerp(Mathf.Log(fLo), Mathf.Log(fHi), Mathf.Pow(t1, logPower)))
                                    : Mathf.Lerp(fLo, fHi, t1);

            float bandLo = Mathf.Min(f0, f1);
            float bandHi = Mathf.Max(f0, f1);

            // 频率→FFT bin（0..fftSize-1）
            int iLo = Mathf.RoundToInt(bandLo * fftSize / sampleRate);
            int iHi = Mathf.RoundToInt(bandHi * fftSize / sampleRate);

            binStart[i] = Mathf.Clamp(iLo, 0, fftSize - 1);
            binEnd[i] = Mathf.Clamp(iHi, 0, fftSize - 1);
        }
    }

    private float ApplyCompression(float x)
    {
        switch (compressMode)
        {
            case CompressMode.Log10:
                return Mathf.Log10(1f + gain * Mathf.Max(0f, x));
            case CompressMode.PowerGamma:
                return Mathf.Pow(Mathf.Max(0f, x), Mathf.Clamp(gamma, 0.1f, 3f));
            default:
                return Mathf.Max(0f, x);
        }
    }

    private void AllocateOutputs()
    {
        if (generateTexture)
        {
            tex = new Texture2D(bars, history, textureFormat, false, true);
            tex.wrapMode = TextureWrapMode.Repeat;    // 便于做环形滚动
            tex.filterMode = textureFilter;
            rowColors = new Color[bars];

            if (vfx != null && !string.IsNullOrEmpty(textureProperty))
                vfx.SetTexture(textureProperty, tex);
        }

        if (generateBuffer)
        {
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bars, sizeof(float));
            if (vfx != null && !string.IsNullOrEmpty(bufferProperty))
                vfx.SetGraphicsBuffer(bufferProperty, buffer);
        }
    }

    private void WriteRowToTexture(float[] values01)
    {
        if (tex == null) return;

        for (int x = 0; x < bars; x++)
        {
            // RFloat：只用 R 通道即可；为兼容性，这里仍写 RGB 同值
            float v = Mathf.Clamp01(values01[x]);
            rowColors[x] = new Color(v, v, v, 1f);
        }

        // 把当前帧写到 writeRow 行（环形缓冲）
        tex.SetPixels(0, writeRow, bars, 1, rowColors);
        tex.Apply(false, false);

        // 下一行（环形）
        writeRow = (writeRow + 1) % history;

        // 提示：在 VFX Graph 里，可用一个“时间索引” uniform 告诉取样的偏移（无需真的搬移纹理数据）
        if (vfx != null)
            vfx.SetInt("FFT_RowHead", writeRow); // 可选：VFX 里用来做取样偏移
    }

    private void WriteToBuffer(float[] values01)
    {
        if (buffer == null) return;
        buffer.SetData(values01);
    }

    private void PushStaticsToVfx()
    {
        if (vfx == null) return;
        vfx.SetInt("FFT_Bars", bars);
        vfx.SetInt("FFT_History", history);
        vfx.SetFloat("FFT_Fmin", fMin);
        vfx.SetFloat("FFT_Fmax", fMax);
        vfx.SetInt("FFT_SampleRate", sampleRate);
        vfx.SetInt("FFT_FftSize", fftSize);
    }

    private float[] TryGetSpectrumFromSource()
    {
        switch (source)
        {
            case DataSource.ExternalFFT:
                return lastExternalFft;
            case DataSource.FromComponent:
                if (!bindToComponent || spectrumProvider == null) return null;
                if (spectrumField == null) CacheSpectrumField();
                if (spectrumField == null) return null;
                var obj = spectrumField.GetValue(spectrumProvider) as float[];
                return obj;
            default:
                return null;
        }
    }

    private void CacheSpectrumField()
    {
        if (spectrumProvider == null || string.IsNullOrEmpty(spectrumFieldName)) return;
        var t = spectrumProvider.GetType();
        spectrumField = t.GetField(spectrumFieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (spectrumField == null)
        {
            Debug.LogWarning($"[FftToVfxMatrix] 未找到字段 {spectrumFieldName} 于 {t.Name}，请检查字段名是否正确（例如 public float[] smoothedFftData）。");
        }
    }

    #endregion

    #region 调参与可视化

    public void Reallocate()
    {
        // 重新分配纹理/缓冲（当 bars/history 或格式改变时调用）
        if (tex != null) Destroy(tex);
        if (buffer != null) { buffer.Release(); buffer = null; }

        barsNow = new float[bars];
        barsEma = new float[bars];
        barsMax = new float[bars];
        for (int i = 0; i < bars; i++) barsMax[i] = 1e-6f;

        PrecomputeBarBinRanges();
        AllocateOutputs();
        PushStaticsToVfx();
        writeRow = 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || barsNow == null) return;
        Gizmos.color = Color.cyan;
        float w = 1f / Mathf.Max(1, bars);
        for (int i = 0; i < bars; i++)
        {
            float h = barsNow[i] * gizmoHeight;
            Vector3 p = transform.position + new Vector3(i * w, 0f, 0f);
            Gizmos.DrawCube(p + Vector3.up * (h * 0.5f), new Vector3(w * 0.9f, h, 0.02f));
        }
    }

    #endregion
}
