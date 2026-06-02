using CSCore;
using CSCore.DSP;
using CSCore.SoundIn;
using CSCore.Streams;
using CSCore.CoreAudioAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class AudioCaptureCSCore : MonoBehaviour
{
    public static AudioCaptureCSCore instance;

    // ================= 捕获模式枚举 =================
    public enum CaptureMode
    {
        Input,    // 麦克风 / 音频输入设备（WasapiCapture）
        Loopback  // 系统播放回环，即扬声器输出（WasapiLoopbackCapture）
    }

    [Header("Capture Mode")]
    [Tooltip("Input = 麦克风输入；Loopback = 系统播放回环（捕获扬声器输出）")]
    public CaptureMode captureMode = CaptureMode.Loopback;
    // =================================================

    // 原有变量保留（Loopback 模式时 capture 实际类型为 WasapiLoopbackCapture，基类均为 WasapiCapture）
    private WasapiCapture capture;
    private SoundInSource soundInSource;
    public IWaveSource waveSource;
    private SingleBlockNotificationStream notificationStream;
    public FftProvider fftProvider;
    public float[] frequencyData;
    public float[] smoothedFftData;
    public float[] rawFftData;
    public bool linearFftData = true;

    [Header("FFT Data Output")]
    [Tooltip("公开 FFT 数组的长度，需要与可视化端使用的 FFT size 保持一致")]
    public int fftDataSize = 2048;

    [Tooltip("当没有其他脚本主动更新 FFT 数组时，由 AudioCaptureCSCore 自动更新 frequencyData / smoothedFftData")]
    public bool updateFftDataAutomatically = true;

    [Tooltip("自动更新公开 FFT 数组时使用的输出缩放")]
    public float fftDataOutputScale = 1f;

    [Tooltip("自动更新公开 FFT 数组时使用的平滑权重")]
    [Range(0f, 0.99f)]
    public float fftDataSmoothingWeight = 0.5f;

    public bool HasFftData { get; private set; }

    // ================= 设备选择相关变量 =================
    [Header("Audio Device Selection")]
    [Tooltip("当前选择的输入设备索引（0为默认；Loopback 模式下为输出设备索引）")]
    public int selectedDeviceIndex = 0;

    [Tooltip("运行时动态获取的设备名称列表（随 captureMode 变化）")]
    public List<string> deviceNames = new List<string>();

    private MMDevice[] availableDevices;
    private bool devicesRefreshed = false;
    // =====================================================

    // ================= 扩展设备选择功能变量 =================
    [Header("Device Selection - Extended")]

    [Tooltip("是否在 Awake 时自动选择上次使用的设备（通过 PlayerPrefs 持久化）")]
    public bool rememberLastDevice = true;

    [Tooltip("是否启用设备热插拔检测（定时轮询）")]
    public bool enableHotplugDetection = true;

    [Tooltip("热插拔轮询间隔（秒），建议 2~5 秒")]
    [Range(1f, 10f)]
    public float hotplugPollInterval = 3f;

    [Tooltip("设备列表发生变化时触发（新增/移除设备）")]
    public UnityEvent onDeviceListChanged;

    [Tooltip("成功切换设备后触发，参数为新设备名称")]
    public UnityEvent<string> onDeviceSwitched;

    [Tooltip("当前正在使用的设备友好名称（只读显示用）")]
    [HideInInspector]
    public string currentDeviceName = "None";

    // PlayerPrefs 持久化 Key
    private const string PREFS_KEY_DEVICE_NAME = "AudioCapture_LastDeviceName";

    // 热插拔检测用：上一次的设备列表快照
    private List<string> _lastKnownDeviceNames = new List<string>();

    // 防止 SwitchDevice / InitializeCapture 重入
    private bool _isSwitching = false;
    private EventHandler<DataAvailableEventArgs> _soundInDataAvailableHandler;
    private EventHandler<SingleBlockReadEventArgs> _singleBlockReadHandler;
    private float[] _sampleReadBuffer = Array.Empty<float>();
    private int _lastManualFftUpdateFrame = -1;
    // =========================================================

    // ================= 手动切换按钮面板 =================
    [Header("Manual Runtime Controls")]
    [Tooltip("运行时显示一个调试按钮面板，用于手动切换捕获模式和设备")]
    public bool showManualControlPanel = true;

    [Tooltip("手动按钮面板的位置与大小")]
    public Rect manualControlPanelRect = new Rect(16f, 16f, 420f, 560f);

    [Tooltip("合并显示 AudioVisualizer 的 BPM / 调性 / 静音状态")]
    public AudioVisualizer audioVisualizer;

    [Tooltip("设备列表滚动区域高度")]
    [Range(80f, 360f)]
    public float manualDeviceListHeight = 180f;

    private Vector2 _manualDeviceListScrollPosition;
    // =========================================================

    private void Awake()
    {
        instance = this;
        if (audioVisualizer == null)
        {
            audioVisualizer = FindFirstObjectByType<AudioVisualizer>();
        }

        EnsureFftDataArrays(fftDataSize);
        RefreshDeviceList();

        if (rememberLastDevice)
        {
            TryRestoreLastDevice();
        }

        InitializeCapture();

        if (enableHotplugDetection)
        {
            StartCoroutine(HotplugDetectionCoroutine());
        }
    }

    private void Update()
    {
        if (!updateFftDataAutomatically || Time.frameCount == _lastManualFftUpdateFrame)
        {
            return;
        }

        TryUpdateFftData(fftDataSize, fftDataOutputScale, fftDataSmoothingWeight);
    }

    // ======================== 原有方法（保留并适配 CaptureMode） ========================

    /// <summary>
    /// 右键组件菜单或代码调用：刷新可用音频设备。
    /// Input 模式枚举输入设备（DataFlow.Capture），Loopback 模式枚举输出设备（DataFlow.Render）。
    /// </summary>
    [ContextMenu("Refresh Audio Devices")]
    public void RefreshDeviceList()
    {
        deviceNames.Clear();
        availableDevices = null;
        devicesRefreshed = false;

        // Loopback 模式枚举输出（Render）设备；Input 模式枚举输入（Capture）设备
        DataFlow dataFlow = (captureMode == CaptureMode.Loopback) ? DataFlow.Render : DataFlow.Capture;

        try
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                var deviceCollection = enumerator.EnumAudioEndpoints(dataFlow, DeviceState.Active);

                var deviceList = new List<MMDevice>();
                for (int i = 0; i < deviceCollection.Count; i++)
                {
                    deviceList.Add(deviceCollection[i]);
                }
                availableDevices = deviceList.ToArray();

                foreach (var device in availableDevices)
                {
                    deviceNames.Add(device.FriendlyName);
                }
            }

            if (availableDevices.Length == 0)
            {
                Debug.LogWarning($"[AudioVisualizerCSCore] No active {captureMode} devices found.");
            }
            else
            {
                Debug.Log($"[AudioVisualizerCSCore] Found {availableDevices.Length} {captureMode} device(s).");
            }
            devicesRefreshed = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AudioVisualizerCSCore] Error enumerating devices: {ex.Message}");
        }
    }

    /// <summary>
    /// 运行时切换设备（通过索引）
    /// </summary>
    public void SwitchDevice(int index)
    {
        if (_isSwitching)
        {
            Debug.LogWarning("[AudioVisualizerCSCore] Device switch already in progress, ignoring.");
            return;
        }

        if (!devicesRefreshed || availableDevices == null)
        {
            RefreshDeviceList();
        }

        if (availableDevices == null || availableDevices.Length == 0)
        {
            Debug.LogWarning($"[AudioVisualizerCSCore] No active {captureMode} devices available.");
            return;
        }

        if (index < 0 || index >= availableDevices.Length)
        {
            Debug.LogError($"[AudioVisualizerCSCore] Invalid device index: {index}");
            return;
        }

        selectedDeviceIndex = index;
        InitializeCapture();
    }

    private void InitializeCapture()
    {
        _isSwitching = true;
        try
        {
            DisposeCaptureChain();
            Debug.Log("[AudioVisualizerCSCore] Stopped and disposed previous capture.");

            string initializedDeviceName;

            // ---- 根据 captureMode 创建对应的捕获实例 ----
            if (captureMode == CaptureMode.Loopback)
            {
                // Loopback：捕获系统播放的声音（扬声器输出回环）
                var loopback = new WasapiLoopbackCapture();

                if (devicesRefreshed && availableDevices != null && availableDevices.Length > 0)
                {
                    if (selectedDeviceIndex >= availableDevices.Length)
                    {
                        selectedDeviceIndex = 0;
                        Debug.LogWarning("[AudioVisualizerCSCore] Selected device index out of range, resetting to 0.");
                    }
                    // WasapiLoopbackCapture 通过 Device 属性指定输出设备
                    loopback.Device = availableDevices[selectedDeviceIndex];
                    Debug.Log($"[AudioVisualizerCSCore] [Loopback] Using output device: {loopback.Device.FriendlyName}");
                    initializedDeviceName = loopback.Device.FriendlyName;
                }
                else
                {
                    Debug.LogWarning("[AudioVisualizerCSCore] [Loopback] No output devices enumerated, using system default.");
                    initializedDeviceName = "System Default (Loopback)";
                }

                capture = loopback;
                Debug.Log("[AudioVisualizerCSCore] Created WasapiLoopbackCapture.");
            }
            else
            {
                // Input：捕获麦克风等输入设备
                var inputCapture = new WasapiCapture();

                if (devicesRefreshed && availableDevices != null && availableDevices.Length > 0)
                {
                    if (selectedDeviceIndex >= availableDevices.Length)
                    {
                        selectedDeviceIndex = 0;
                        Debug.LogWarning("[AudioVisualizerCSCore] Selected device index out of range, resetting to 0.");
                    }
                    inputCapture.Device = availableDevices[selectedDeviceIndex];
                    Debug.Log($"[AudioVisualizerCSCore] [Input] Using device: {inputCapture.Device.FriendlyName}, State: {inputCapture.Device.DeviceState}");
                    initializedDeviceName = inputCapture.Device.FriendlyName;
                }
                else
                {
                    Debug.LogWarning("[AudioVisualizerCSCore] No input devices enumerated, using system default.");
                    initializedDeviceName = "System Default (Input)";
                }

                capture = inputCapture;
                Debug.Log("[AudioVisualizerCSCore] Created WasapiCapture.");
            }
            // -----------------------------------------------

            capture.Initialize();
            Debug.Log("[AudioVisualizerCSCore] Capture initialized.");

            soundInSource = new SoundInSource(capture) { FillWithZeros = false };
            var sampleSource = soundInSource.ToSampleSource();
            notificationStream = new SingleBlockNotificationStream(sampleSource);
            waveSource = notificationStream.ToWaveSource();
            Debug.Log("[AudioVisualizerCSCore] Audio stream and notification stream initialized.");

            fftProvider = new FftProvider(waveSource.WaveFormat.Channels, FftSize.Fft2048);
            Debug.Log("[AudioVisualizerCSCore] FFT Provider created with channels: " + waveSource.WaveFormat.Channels);

            int channels = waveSource.WaveFormat.Channels;
            _sampleReadBuffer = new float[Mathf.Max(1024, waveSource.WaveFormat.SampleRate * channels / 10)];
            var activeNotificationStream = notificationStream;
            var activeFftProvider = fftProvider;
            var activeSampleReadBuffer = _sampleReadBuffer;

            _singleBlockReadHandler = (s, args) =>
            {
                if (channels == 1)
                {
                    activeFftProvider.Add(args.Left, args.Left);
                }
                else
                {
                    activeFftProvider.Add(args.Left, args.Right);
                }
            };
            activeNotificationStream.SingleBlockRead += _singleBlockReadHandler;

            _soundInDataAvailableHandler = (s, args) =>
            {
                while (activeNotificationStream.Read(activeSampleReadBuffer, 0, activeSampleReadBuffer.Length) > 0)
                {
                }
            };
            soundInSource.DataAvailable += _soundInDataAvailableHandler;

            capture.Start();
            Debug.Log("[AudioVisualizerCSCore] Capture started.");

            currentDeviceName = initializedDeviceName;
            if (rememberLastDevice)
            {
                PlayerPrefs.SetString(PREFS_KEY_DEVICE_NAME, currentDeviceName);
                PlayerPrefs.Save();
            }

            onDeviceSwitched?.Invoke(currentDeviceName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AudioVisualizerCSCore] Error initializing audio capture: {ex.Message}");
        }
        finally
        {
            _isSwitching = false;
        }
    }

    void OnDisable()
    {
        Debug.Log("[AudioVisualizerCSCore] Disposing capture and waveSource.");
        DisposeCaptureChain();
    }

    private void DisposeCaptureChain()
    {
        if (soundInSource != null && _soundInDataAvailableHandler != null)
        {
            soundInSource.DataAvailable -= _soundInDataAvailableHandler;
        }

        if (notificationStream != null && _singleBlockReadHandler != null)
        {
            notificationStream.SingleBlockRead -= _singleBlockReadHandler;
        }

        _soundInDataAvailableHandler = null;
        _singleBlockReadHandler = null;

        try
        {
            capture?.Stop();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AudioVisualizerCSCore] Error stopping capture: {ex.Message}");
        }

        waveSource?.Dispose();
        soundInSource?.Dispose();
        capture?.Dispose();

        capture = null;
        soundInSource = null;
        notificationStream = null;
        waveSource = null;
        fftProvider = null;
        HasFftData = false;
        _sampleReadBuffer = Array.Empty<float>();
    }

    public bool TryUpdateFftData(int dataSize, float outputScale, float smoothingWeight)
    {
        if (fftProvider == null || dataSize <= 0)
        {
            HasFftData = false;
            return false;
        }

        EnsureFftDataArrays(dataSize);

        bool hasFftData = fftProvider.GetFftData(rawFftData);
        if (!hasFftData)
        {
            HasFftData = false;
            return false;
        }

        UpdateFftDataFromBuffer(rawFftData, outputScale, smoothingWeight);
        _lastManualFftUpdateFrame = Time.frameCount;
        HasFftData = true;
        return true;
    }

    public void UpdateFftDataFromBuffer(float[] fftBuffer, float outputScale, float smoothingWeight)
    {
        if (fftBuffer == null || fftBuffer.Length == 0)
        {
            HasFftData = false;
            return;
        }

        EnsureFftDataArrays(fftBuffer.Length);
        smoothingWeight = Mathf.Clamp01(smoothingWeight);

        for (int i = 0; i < fftBuffer.Length; i++)
        {
            float magnitude = Mathf.Max(fftBuffer[i], 1e-6f);
            frequencyData[i] = linearFftData
                ? magnitude * outputScale
                : Mathf.Log10(magnitude) * outputScale * 20f;
        }

        for (int i = 0; i < frequencyData.Length; i++)
        {
            smoothedFftData[i] = (smoothedFftData[i] * smoothingWeight) + (frequencyData[i] * (1f - smoothingWeight));
        }

        HasFftData = true;
    }

    private void EnsureFftDataArrays(int dataSize)
    {
        if (dataSize <= 0)
        {
            dataSize = 2048;
        }

        if (rawFftData == null || rawFftData.Length != dataSize)
        {
            rawFftData = new float[dataSize];
        }

        if (frequencyData == null || frequencyData.Length != dataSize)
        {
            frequencyData = new float[dataSize];
        }

        if (smoothedFftData == null || smoothedFftData.Length != dataSize)
        {
            smoothedFftData = new float[dataSize];
            Array.Copy(frequencyData, smoothedFftData, Mathf.Min(frequencyData.Length, smoothedFftData.Length));
        }
    }

    // ======================== 新增方法 ========================

    /// <summary>
    /// 运行时切换捕获模式，并重新刷新设备列表 & 重新初始化捕获。
    /// 示例：SwitchCaptureMode(AudioCaptureCSCore.CaptureMode.Loopback)
    /// </summary>
    public void SwitchCaptureMode(CaptureMode mode)
    {
        if (captureMode == mode)
        {
            Debug.Log($"[AudioVisualizerCSCore] Already in {mode} mode, skipping.");
            return;
        }

        captureMode = mode;
        selectedDeviceIndex = 0; // 切换模式时重置设备索引，避免越界
        RefreshDeviceList();
        InitializeCapture();

        Debug.Log($"[AudioVisualizerCSCore] Capture mode switched to: {mode}");
    }

    /// <summary>
    /// 供 Unity UI Button 直接绑定：切换到麦克风/输入设备模式。
    /// </summary>
    public void SwitchToInputMode()
    {
        SwitchCaptureMode(CaptureMode.Input);
    }

    /// <summary>
    /// 供 Unity UI Button 直接绑定：切换到系统声音回环模式。
    /// </summary>
    public void SwitchToLoopbackMode()
    {
        SwitchCaptureMode(CaptureMode.Loopback);
    }

    /// <summary>
    /// 供 Unity UI Button 直接绑定：在输入和回环模式之间切换。
    /// </summary>
    public void ToggleCaptureMode()
    {
        SwitchCaptureMode(captureMode == CaptureMode.Input ? CaptureMode.Loopback : CaptureMode.Input);
    }

    /// <summary>
    /// 通过设备友好名称切换设备（适合下拉框 UI 绑定）
    /// 示例：SwitchDeviceByName("麦克风 (USB Audio Device)")
    /// </summary>
    public void SwitchDeviceByName(string friendlyName)
    {
        if (!devicesRefreshed || availableDevices == null)
        {
            Debug.LogWarning("[AudioVisualizerCSCore] Device list not ready. Call RefreshDeviceList() first.");
            return;
        }

        int index = Array.FindIndex(availableDevices, d => d.FriendlyName == friendlyName);
        if (index < 0)
        {
            index = Array.FindIndex(availableDevices, d =>
                d.FriendlyName.IndexOf(friendlyName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (index < 0)
        {
            Debug.LogError($"[AudioVisualizerCSCore] Device not found: \"{friendlyName}\"");
            return;
        }

        SwitchDevice(index);
    }

    /// <summary>
    /// 切换到下一个设备（循环）
    /// </summary>
    [ContextMenu("Switch To Next Device")]
    public void SwitchToNextDevice()
    {
        if (!devicesRefreshed || availableDevices == null || availableDevices.Length == 0) return;
        int next = (selectedDeviceIndex + 1) % availableDevices.Length;
        SwitchDevice(next);
    }

    /// <summary>
    /// 切换到上一个设备（循环）
    /// </summary>
    [ContextMenu("Switch To Previous Device")]
    public void SwitchToPreviousDevice()
    {
        if (!devicesRefreshed || availableDevices == null || availableDevices.Length == 0) return;
        int prev = (selectedDeviceIndex - 1 + availableDevices.Length) % availableDevices.Length;
        SwitchDevice(prev);
    }

    /// <summary>
    /// 获取当前所有设备名称数组（供 UI Dropdown 填充）
    /// </summary>
    public string[] GetDeviceNames()
    {
        return deviceNames.ToArray();
    }

    /// <summary>
    /// 获取当前选中设备名称
    /// </summary>
    public string GetCurrentDeviceName()
    {
        return currentDeviceName;
    }

    /// <summary>
    /// 清除持久化的设备记忆
    /// </summary>
    [ContextMenu("Clear Saved Device Preference")]
    public void ClearSavedDevicePreference()
    {
        PlayerPrefs.DeleteKey(PREFS_KEY_DEVICE_NAME);
        PlayerPrefs.Save();
        Debug.Log("[AudioVisualizerCSCore] Cleared saved device preference.");
    }

    /// <summary>
    /// 尝试从 PlayerPrefs 恢复上次使用的设备
    /// </summary>
    private void TryRestoreLastDevice()
    {
        if (!PlayerPrefs.HasKey(PREFS_KEY_DEVICE_NAME)) return;

        string savedName = PlayerPrefs.GetString(PREFS_KEY_DEVICE_NAME);
        int index = Array.FindIndex(availableDevices ?? Array.Empty<MMDevice>(),
            d => d.FriendlyName == savedName);

        if (index >= 0)
        {
            selectedDeviceIndex = index;
            Debug.Log($"[AudioVisualizerCSCore] Restored last device: \"{savedName}\" (index {index})");
        }
        else
        {
            Debug.LogWarning($"[AudioVisualizerCSCore] Saved device \"{savedName}\" not found, using default (index 0).");
            selectedDeviceIndex = 0;
        }
    }

    /// <summary>
    /// 热插拔检测协程：定时对比设备列表，有变化时触发 onDeviceListChanged 事件。
    /// 自动根据当前 captureMode 枚举对应类型的设备。
    /// </summary>
    private IEnumerator HotplugDetectionCoroutine()
    {
        _lastKnownDeviceNames = new List<string>(deviceNames);

        while (true)
        {
            yield return new WaitForSeconds(hotplugPollInterval);

            DataFlow dataFlow = (captureMode == CaptureMode.Loopback) ? DataFlow.Render : DataFlow.Capture;
            List<string> currentNames = new List<string>();

            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    var col = enumerator.EnumAudioEndpoints(dataFlow, DeviceState.Active);
                    for (int i = 0; i < col.Count; i++)
                        currentNames.Add(col[i].FriendlyName);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AudioVisualizerCSCore] Hotplug poll error: {ex.Message}");
                continue;
            }

            bool changed = !currentNames.SequenceEqual(_lastKnownDeviceNames);
            if (changed)
            {
                Debug.Log("[AudioVisualizerCSCore] Audio device list changed, refreshing...");
                _lastKnownDeviceNames = currentNames;

                string previousDevice = currentDeviceName;
                RefreshDeviceList();

                int restoredIndex = Array.FindIndex(availableDevices ?? Array.Empty<MMDevice>(),
                    d => d.FriendlyName == previousDevice);

                if (restoredIndex >= 0)
                {
                    selectedDeviceIndex = restoredIndex;
                }
                else if (availableDevices != null && availableDevices.Length > 0)
                {
                    selectedDeviceIndex = 0;
                    Debug.LogWarning($"[AudioVisualizerCSCore] Previous device \"{previousDevice}\" disconnected. Switching to index 0.");
                    InitializeCapture();
                }

                onDeviceListChanged?.Invoke();
            }
        }
    }

    private void OnGUI()
    {
        if (!showManualControlPanel)
        {
            return;
        }

        manualControlPanelRect.width = Mathf.Max(manualControlPanelRect.width, 420f);
        manualControlPanelRect.height = Mathf.Max(manualControlPanelRect.height, 560f);
        manualControlPanelRect = GUILayout.Window(
            GetInstanceID(),
            manualControlPanelRect,
            DrawManualControlPanel,
            "Audio Capture");
    }

    private void DrawManualControlPanel(int windowId)
    {
        GUILayout.Label($"Mode: {captureMode}");
        GUILayout.Label($"Device: {currentDeviceName}");

        GUILayout.BeginHorizontal();
        DrawModeButton("Input", CaptureMode.Input);
        DrawModeButton("Loopback", CaptureMode.Loopback);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Toggle Mode"))
        {
            ToggleCaptureMode();
        }

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh"))
        {
            RefreshDeviceList();
        }

        if (GUILayout.Button("Previous"))
        {
            SwitchToPreviousDevice();
        }

        if (GUILayout.Button("Next"))
        {
            SwitchToNextDevice();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label($"{captureMode} Devices");

        if (deviceNames.Count == 0)
        {
            GUILayout.Label("No active devices");
        }
        else
        {
            _manualDeviceListScrollPosition = GUILayout.BeginScrollView(
                _manualDeviceListScrollPosition,
                GUILayout.Height(manualDeviceListHeight));

            for (int i = 0; i < deviceNames.Count; i++)
            {
                string prefix = (i == selectedDeviceIndex) ? "* " : string.Empty;
                if (GUILayout.Button($"{prefix}{i}: {deviceNames[i]}"))
                {
                    SwitchDevice(i);
                }
            }

            GUILayout.EndScrollView();
        }

        DrawVisualizerStatusSection();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DrawVisualizerStatusSection()
    {
        if (audioVisualizer == null)
        {
            audioVisualizer = FindFirstObjectByType<AudioVisualizer>();
        }

        GUILayout.Space(8f);
        GUILayout.Label("Audio Visualizer");

        if (audioVisualizer == null)
        {
            GUILayout.Label("AudioVisualizer not found");
            return;
        }

        audioVisualizer.DrawStatusGui();
    }

    private void DrawModeButton(string label, CaptureMode mode)
    {
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && captureMode != mode;

        if (GUILayout.Button(label))
        {
            SwitchCaptureMode(mode);
        }

        GUI.enabled = wasEnabled;
    }
}
