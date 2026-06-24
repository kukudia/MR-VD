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

/// <summary>
/// Captures Windows audio through CSCore and exposes FFT buffers for visualization systems.
/// </summary>
public class AudioCaptureCSCore : MonoBehaviour
{
    public static AudioCaptureCSCore instance;

    /// <summary>
    /// Selects whether audio comes from an input device or system playback loopback.
    /// </summary>
    public enum CaptureMode
    {
        Input,
        Loopback
    }

    [Header("Capture Mode")]
    [Tooltip("Input captures microphones or audio input devices. Loopback captures system playback from output devices.")]
    public CaptureMode captureMode = CaptureMode.Loopback;

    private WasapiCapture capture;
    private SoundInSource soundInSource;
    public IWaveSource waveSource;
    private SingleBlockNotificationStream notificationStream;
    public FftProvider fftProvider;
    private FftProvider leftFftProvider;
    private FftProvider rightFftProvider;
    public float[] frequencyData;
    public float[] smoothedFftData;
    public float[] rawFftData;
    public float[] leftFrequencyData;
    public float[] rightFrequencyData;
    public float[] smoothedLeftFftData;
    public float[] smoothedRightFftData;
    public float[] rawLeftFftData;
    public float[] rawRightFftData;
    public bool linearFftData = true;

    [Header("FFT Data Output")]
    [Tooltip("Length of the exposed FFT arrays. Keep this aligned with the visualizer FFT size.")]
    public int fftDataSize = 2048;

    [Tooltip("Automatically updates frequencyData and smoothedFftData when no other script updates the FFT arrays manually.")]
    public bool updateFftDataAutomatically = true;

    [Tooltip("Output scale applied when automatically updating exposed FFT arrays.")]
    public float fftDataOutputScale = 1f;

    [Tooltip("Smoothing weight applied when automatically updating exposed FFT arrays.")]
    [Range(0f, 0.99f)]
    public float fftDataSmoothingWeight = 0.5f;

    public bool HasFftData { get; private set; }

    [Header("Audio Device Selection")]
    [Tooltip("Selected capture device index. In Loopback mode this points to an output device.")]
    public int selectedDeviceIndex = 0;

    [Tooltip("Runtime device name list for the current capture mode.")]
    public List<string> deviceNames = new List<string>();

    private MMDevice[] availableDevices;
    private bool devicesRefreshed = false;

    [Header("Device Selection - Extended")]

    [Tooltip("Restores the previously selected device from PlayerPrefs during Awake.")]
    public bool rememberLastDevice = true;

    [Tooltip("Enables periodic polling for audio device hotplug changes.")]
    public bool enableHotplugDetection = true;

    [Tooltip("Hotplug polling interval in seconds. Recommended range: 2 to 5 seconds.")]
    [Range(1f, 10f)]
    public float hotplugPollInterval = 3f;

    [Tooltip("Invoked when audio devices are added or removed.")]
    public UnityEvent onDeviceListChanged;

    [Tooltip("Invoked after a successful device switch. The event argument is the new device name.")]
    public UnityEvent<string> onDeviceSwitched;

    [Tooltip("Friendly name of the active audio device for runtime display.")]
    [HideInInspector]
    public string currentDeviceName = "None";

    private const string PREFS_KEY_DEVICE_NAME = "AudioCapture_LastDeviceName";

    private List<string> _lastKnownDeviceNames = new List<string>();

    private bool _isSwitching = false;
    private EventHandler<DataAvailableEventArgs> _soundInDataAvailableHandler;
    private EventHandler<SingleBlockReadEventArgs> _singleBlockReadHandler;
    private float[] _sampleReadBuffer = Array.Empty<float>();
    private int _lastManualFftUpdateFrame = -1;

    [Header("Manual Runtime Controls")]
    [Tooltip("Shows a runtime debug panel for manually switching capture modes and devices.")]
    public bool showManualControlPanel = true;

    [Tooltip("Position and size of the manual runtime control panel.")]
    public Rect manualControlPanelRect = new Rect(16f, 16f, 420f, 560f);

    [Tooltip("Optional visualizer used to merge BPM, key, and silence status into the capture panel.")]
    public AudioVisualizer audioVisualizer;

    [Tooltip("Height of the scrollable device list in the manual runtime panel.")]
    [Range(80f, 360f)]
    public float manualDeviceListHeight = 180f;

    private Vector2 _manualDeviceListScrollPosition;

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

    /// <summary>
    /// Refreshes the available audio devices for the current capture mode.
    /// </summary>
    [ContextMenu("Refresh Audio Devices")]
    public void RefreshDeviceList()
    {
        deviceNames.Clear();
        availableDevices = null;
        devicesRefreshed = false;

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
                Debug.LogWarning($"[AudioCaptureCSCore] No active {captureMode} devices found.");
            }
            else
            {
                Debug.Log($"[AudioCaptureCSCore] Found {availableDevices.Length} {captureMode} device(s).");
            }
            devicesRefreshed = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AudioCaptureCSCore] Error enumerating devices: {ex.Message}");
        }
    }

    /// <summary>
    /// Switches the active capture device by runtime device index.
    /// </summary>
    public void SwitchDevice(int index)
    {
        if (_isSwitching)
        {
            Debug.LogWarning("[AudioCaptureCSCore] Device switch already in progress, ignoring.");
            return;
        }

        if (!devicesRefreshed || availableDevices == null)
        {
            RefreshDeviceList();
        }

        if (availableDevices == null || availableDevices.Length == 0)
        {
            Debug.LogWarning($"[AudioCaptureCSCore] No active {captureMode} devices available.");
            return;
        }

        if (index < 0 || index >= availableDevices.Length)
        {
            Debug.LogError($"[AudioCaptureCSCore] Invalid device index: {index}");
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
            Debug.Log("[AudioCaptureCSCore] Stopped and disposed previous capture.");

            string initializedDeviceName;

            if (captureMode == CaptureMode.Loopback)
            {
                var loopback = new WasapiLoopbackCapture();

                if (devicesRefreshed && availableDevices != null && availableDevices.Length > 0)
                {
                    if (selectedDeviceIndex >= availableDevices.Length)
                    {
                        selectedDeviceIndex = 0;
                        Debug.LogWarning("[AudioCaptureCSCore] Selected device index out of range, resetting to 0.");
                    }
                    loopback.Device = availableDevices[selectedDeviceIndex];
                    Debug.Log($"[AudioCaptureCSCore] [Loopback] Using output device: {loopback.Device.FriendlyName}");
                    initializedDeviceName = loopback.Device.FriendlyName;
                }
                else
                {
                    Debug.LogWarning("[AudioCaptureCSCore] [Loopback] No output devices enumerated, using system default.");
                    initializedDeviceName = "System Default (Loopback)";
                }

                capture = loopback;
                Debug.Log("[AudioCaptureCSCore] Created WasapiLoopbackCapture.");
            }
            else
            {
                var inputCapture = new WasapiCapture();

                if (devicesRefreshed && availableDevices != null && availableDevices.Length > 0)
                {
                    if (selectedDeviceIndex >= availableDevices.Length)
                    {
                        selectedDeviceIndex = 0;
                        Debug.LogWarning("[AudioCaptureCSCore] Selected device index out of range, resetting to 0.");
                    }
                    inputCapture.Device = availableDevices[selectedDeviceIndex];
                    Debug.Log($"[AudioCaptureCSCore] [Input] Using device: {inputCapture.Device.FriendlyName}, State: {inputCapture.Device.DeviceState}");
                    initializedDeviceName = inputCapture.Device.FriendlyName;
                }
                else
                {
                    Debug.LogWarning("[AudioCaptureCSCore] No input devices enumerated, using system default.");
                    initializedDeviceName = "System Default (Input)";
                }

                capture = inputCapture;
                Debug.Log("[AudioCaptureCSCore] Created WasapiCapture.");
            }
            capture.Initialize();
            Debug.Log("[AudioCaptureCSCore] Capture initialized.");

            soundInSource = new SoundInSource(capture) { FillWithZeros = false };
            var sampleSource = soundInSource.ToSampleSource();
            notificationStream = new SingleBlockNotificationStream(sampleSource);
            waveSource = notificationStream.ToWaveSource();
            Debug.Log("[AudioCaptureCSCore] Audio stream and notification stream initialized.");

            fftProvider = new FftProvider(waveSource.WaveFormat.Channels, FftSize.Fft2048);
            leftFftProvider = new FftProvider(1, FftSize.Fft2048);
            rightFftProvider = new FftProvider(1, FftSize.Fft2048);
            Debug.Log("[AudioCaptureCSCore] FFT Provider created with channels: " + waveSource.WaveFormat.Channels);

            int channels = waveSource.WaveFormat.Channels;
            _sampleReadBuffer = new float[Mathf.Max(1024, waveSource.WaveFormat.SampleRate * channels / 10)];
            var activeNotificationStream = notificationStream;
            var activeFftProvider = fftProvider;
            var activeLeftFftProvider = leftFftProvider;
            var activeRightFftProvider = rightFftProvider;
            var activeSampleReadBuffer = _sampleReadBuffer;

            _singleBlockReadHandler = (s, args) =>
            {
                if (channels == 1)
                {
                    activeFftProvider.Add(args.Left, args.Left);
                    activeLeftFftProvider.Add(args.Left, args.Left);
                    activeRightFftProvider.Add(args.Left, args.Left);
                }
                else
                {
                    activeFftProvider.Add(args.Left, args.Right);
                    activeLeftFftProvider.Add(args.Left, args.Left);
                    activeRightFftProvider.Add(args.Right, args.Right);
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
            Debug.Log("[AudioCaptureCSCore] Capture started.");

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
            Debug.LogError($"[AudioCaptureCSCore] Error initializing audio capture: {ex.Message}");
        }
        finally
        {
            _isSwitching = false;
        }
    }

    void OnDisable()
    {
        Debug.Log("[AudioCaptureCSCore] Disposing capture and waveSource.");
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
            Debug.LogWarning($"[AudioCaptureCSCore] Error stopping capture: {ex.Message}");
        }

        waveSource?.Dispose();
        soundInSource?.Dispose();
        capture?.Dispose();

        capture = null;
        soundInSource = null;
        notificationStream = null;
        waveSource = null;
        fftProvider = null;
        leftFftProvider = null;
        rightFftProvider = null;
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

        UpdateFftDataArrayFromBuffer(rawFftData, frequencyData, smoothedFftData, outputScale, smoothingWeight);

        bool hasLeftFftData = leftFftProvider != null && leftFftProvider.GetFftData(rawLeftFftData);
        bool hasRightFftData = rightFftProvider != null && rightFftProvider.GetFftData(rawRightFftData);
        if (hasLeftFftData)
        {
            UpdateFftDataArrayFromBuffer(rawLeftFftData, leftFrequencyData, smoothedLeftFftData, outputScale, smoothingWeight);
        }

        if (hasRightFftData)
        {
            UpdateFftDataArrayFromBuffer(rawRightFftData, rightFrequencyData, smoothedRightFftData, outputScale, smoothingWeight);
        }

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

        UpdateFftDataArrayFromBuffer(fftBuffer, frequencyData, smoothedFftData, outputScale, smoothingWeight);
        UpdateFftDataArrayFromBuffer(fftBuffer, leftFrequencyData, smoothedLeftFftData, outputScale, smoothingWeight);
        UpdateFftDataArrayFromBuffer(fftBuffer, rightFrequencyData, smoothedRightFftData, outputScale, smoothingWeight);

        HasFftData = true;
    }

    private void UpdateFftDataArrayFromBuffer(
        float[] fftBuffer,
        float[] targetFrequencyData,
        float[] targetSmoothedFftData,
        float outputScale,
        float smoothingWeight)
    {
        if (fftBuffer == null || targetFrequencyData == null || targetSmoothedFftData == null)
        {
            return;
        }

        int count = Mathf.Min(fftBuffer.Length, targetFrequencyData.Length, targetSmoothedFftData.Length);
        for (int i = 0; i < count; i++)
        {
            float magnitude = Mathf.Max(fftBuffer[i], 1e-6f);
            targetFrequencyData[i] = linearFftData
                ? magnitude * outputScale
                : Mathf.Log10(magnitude) * outputScale * 20f;
        }

        for (int i = 0; i < count; i++)
        {
            targetSmoothedFftData[i] = (targetSmoothedFftData[i] * smoothingWeight) + (targetFrequencyData[i] * (1f - smoothingWeight));
        }
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

        if (rawLeftFftData == null || rawLeftFftData.Length != dataSize)
        {
            rawLeftFftData = new float[dataSize];
        }

        if (rawRightFftData == null || rawRightFftData.Length != dataSize)
        {
            rawRightFftData = new float[dataSize];
        }

        if (leftFrequencyData == null || leftFrequencyData.Length != dataSize)
        {
            leftFrequencyData = new float[dataSize];
        }

        if (rightFrequencyData == null || rightFrequencyData.Length != dataSize)
        {
            rightFrequencyData = new float[dataSize];
        }

        if (smoothedLeftFftData == null || smoothedLeftFftData.Length != dataSize)
        {
            smoothedLeftFftData = new float[dataSize];
            Array.Copy(leftFrequencyData, smoothedLeftFftData, Mathf.Min(leftFrequencyData.Length, smoothedLeftFftData.Length));
        }

        if (smoothedRightFftData == null || smoothedRightFftData.Length != dataSize)
        {
            smoothedRightFftData = new float[dataSize];
            Array.Copy(rightFrequencyData, smoothedRightFftData, Mathf.Min(rightFrequencyData.Length, smoothedRightFftData.Length));
        }
    }

    /// <summary>
    /// Switches capture mode at runtime, then refreshes devices and reinitializes capture.
    /// </summary>
    public void SwitchCaptureMode(CaptureMode mode)
    {
        if (captureMode == mode)
        {
            Debug.Log($"[AudioCaptureCSCore] Already in {mode} mode, skipping.");
            return;
        }

        captureMode = mode;
        selectedDeviceIndex = 0;
        RefreshDeviceList();
        InitializeCapture();

        Debug.Log($"[AudioCaptureCSCore] Capture mode switched to: {mode}");
    }

    /// <summary>
    /// Switches to microphone or input-device capture mode. Intended for Unity UI button bindings.
    /// </summary>
    public void SwitchToInputMode()
    {
        SwitchCaptureMode(CaptureMode.Input);
    }

    /// <summary>
    /// Switches to system playback loopback capture mode. Intended for Unity UI button bindings.
    /// </summary>
    public void SwitchToLoopbackMode()
    {
        SwitchCaptureMode(CaptureMode.Loopback);
    }

    /// <summary>
    /// Toggles between input and loopback capture modes. Intended for Unity UI button bindings.
    /// </summary>
    public void ToggleCaptureMode()
    {
        SwitchCaptureMode(captureMode == CaptureMode.Input ? CaptureMode.Loopback : CaptureMode.Input);
    }

    /// <summary>
    /// Switches capture devices by friendly name. Supports dropdown-style UI bindings.
    /// </summary>
    public void SwitchDeviceByName(string friendlyName)
    {
        if (!devicesRefreshed || availableDevices == null)
        {
            Debug.LogWarning("[AudioCaptureCSCore] Device list not ready. Call RefreshDeviceList() first.");
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
            Debug.LogError($"[AudioCaptureCSCore] Device not found: \"{friendlyName}\"");
            return;
        }

        SwitchDevice(index);
    }

    /// <summary>
    /// Switches to the next available device, wrapping at the end of the list.
    /// </summary>
    [ContextMenu("Switch To Next Device")]
    public void SwitchToNextDevice()
    {
        if (!devicesRefreshed || availableDevices == null || availableDevices.Length == 0) return;
        int next = (selectedDeviceIndex + 1) % availableDevices.Length;
        SwitchDevice(next);
    }

    /// <summary>
    /// Switches to the previous available device, wrapping at the start of the list.
    /// </summary>
    [ContextMenu("Switch To Previous Device")]
    public void SwitchToPreviousDevice()
    {
        if (!devicesRefreshed || availableDevices == null || availableDevices.Length == 0) return;
        int prev = (selectedDeviceIndex - 1 + availableDevices.Length) % availableDevices.Length;
        SwitchDevice(prev);
    }

    /// <summary>
    /// Gets the current device names for UI dropdown population.
    /// </summary>
    public string[] GetDeviceNames()
    {
        return deviceNames.ToArray();
    }

    /// <summary>
    /// Gets the friendly name of the active device.
    /// </summary>
    public string GetCurrentDeviceName()
    {
        return currentDeviceName;
    }

    /// <summary>
    /// Clears the persisted device preference.
    /// </summary>
    [ContextMenu("Clear Saved Device Preference")]
    public void ClearSavedDevicePreference()
    {
        PlayerPrefs.DeleteKey(PREFS_KEY_DEVICE_NAME);
        PlayerPrefs.Save();
        Debug.Log("[AudioCaptureCSCore] Cleared saved device preference.");
    }

    /// <summary>
    /// Attempts to restore the previously selected device from PlayerPrefs.
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
            Debug.Log($"[AudioCaptureCSCore] Restored last device: \"{savedName}\" (index {index})");
        }
        else
        {
            Debug.LogWarning($"[AudioCaptureCSCore] Saved device \"{savedName}\" not found, using default (index 0).");
            selectedDeviceIndex = 0;
        }
    }

    /// <summary>
    /// Polls the device list and raises onDeviceListChanged when devices are added or removed.
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
                Debug.LogWarning($"[AudioCaptureCSCore] Hotplug poll error: {ex.Message}");
                continue;
            }

            bool changed = !currentNames.SequenceEqual(_lastKnownDeviceNames);
            if (changed)
            {
                Debug.Log("[AudioCaptureCSCore] Audio device list changed, refreshing...");
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
                    Debug.LogWarning($"[AudioCaptureCSCore] Previous device \"{previousDevice}\" disconnected. Switching to index 0.");
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
