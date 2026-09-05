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
using UnityEngine.UI;

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
    public int fftDataSize = 4096;

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

    [Tooltip("Places the Windows default endpoint first and selects it after startup, refresh, or capture-mode changes.")]
    public bool preferSystemDefaultDevice = true;

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

    [Header("Runtime Controls")]
    [Tooltip("Shows the Screen/Canvas audio routing module.")]
    public bool showManualControlPanel = true;

    [Tooltip("Optional visualizer used to merge BPM, key, and silence status into the capture panel.")]
    public AudioVisualizer audioVisualizer;


    [Header("Screen Canvas Panel")]
    [Tooltip("Renders the manual audio panel inside Screen/Canvas/AudioPanel instead of the legacy IMGUI overlay.")]
    public bool useScreenCanvasPanel = true;

    [Tooltip("Optional target panel under Screen/Canvas. When empty, Screen/Canvas/AudioPanel is used.")]
    public RectTransform screenCanvasPanelRoot;

    [Tooltip("How often the Screen/Canvas panel text is refreshed.")]
    [Range(0.05f, 1f)]
    public float screenCanvasRefreshInterval = 0.2f;

    private RectTransform _screenCanvasContent;
    private Text _screenModeText;
    private Text _screenDeviceText;
    private Text _screenDeviceHeaderText;
    private Text _screenVisualizerText;
    private Button _screenHideButton;
    private string _screenDeviceListSignature = string.Empty;
    private float _nextScreenCanvasRefreshTime;
    private Font _screenCanvasFont;
    private const float ScreenCanvasContentWidth = 270f;
    private const float ScreenCanvasContentHeight = 560f;
    private const float ScreenCanvasChildWidth = 250f;

    private void Awake()
    {
        instance = this;
        if (audioVisualizer == null)
        {
            audioVisualizer = FindFirstObjectByType<AudioVisualizer>();
        }

        EnsureFftDataArrays(fftDataSize);
        RefreshDeviceList();

        if (rememberLastDevice && !preferSystemDefaultDevice)
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
            UpdateScreenCanvasPanel(false);
            return;
        }

        TryUpdateFftData(fftDataSize, fftDataOutputScale, fftDataSmoothingWeight);
        UpdateScreenCanvasPanel(false);
    }

    /// <summary>
    /// Refreshes the available audio devices for the current capture mode.
    /// </summary>
    [ContextMenu("Refresh Audio Devices")]
    public void RefreshDeviceList()
    {
        RefreshDeviceList(preferSystemDefaultDevice);
    }

    public void RefreshDeviceListAndRestartCapture()
    {
        RefreshDeviceList();

        if (availableDevices != null && availableDevices.Length > 0)
        {
            InitializeCapture();
        }
    }

    private void RefreshDeviceList(bool selectDefaultDevice)
    {
        deviceNames.Clear();
        string previousDeviceId = GetSelectedDeviceId();
        availableDevices = null;
        devicesRefreshed = false;

        DataFlow dataFlow = (captureMode == CaptureMode.Loopback) ? DataFlow.Render : DataFlow.Capture;

        try
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                var deviceCollection = enumerator.EnumAudioEndpoints(dataFlow, DeviceState.Active);
                string defaultDeviceId = GetDefaultDeviceId(enumerator, dataFlow);

                var deviceList = new List<MMDevice>();
                for (int i = 0; i < deviceCollection.Count; i++)
                {
                    deviceList.Add(deviceCollection[i]);
                }

                MoveDefaultDeviceToTop(deviceList, defaultDeviceId);
                availableDevices = deviceList.ToArray();

                foreach (var device in availableDevices)
                {
                    deviceNames.Add(device.FriendlyName);
                }
            }

            if (availableDevices.Length == 0)
            {
                Debug.LogWarning($"[AudioCaptureCSCore] No active {captureMode} devices found.");
                selectedDeviceIndex = 0;
            }
            else
            {
                Debug.Log($"[AudioCaptureCSCore] Found {availableDevices.Length} {captureMode} device(s).");
                if (selectDefaultDevice)
                {
                    selectedDeviceIndex = 0;
                }
                else if (!string.IsNullOrEmpty(previousDeviceId))
                {
                    int restoredIndex = Array.FindIndex(availableDevices, d =>
                        string.Equals(d.DeviceID, previousDeviceId, StringComparison.OrdinalIgnoreCase));
                    selectedDeviceIndex = restoredIndex >= 0 ? restoredIndex : Mathf.Clamp(selectedDeviceIndex, 0, availableDevices.Length - 1);
                }
                else
                {
                    selectedDeviceIndex = Mathf.Clamp(selectedDeviceIndex, 0, availableDevices.Length - 1);
                }
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
            RefreshDeviceList(false);
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

    private string GetSelectedDeviceId()
    {
        if (availableDevices == null || selectedDeviceIndex < 0 || selectedDeviceIndex >= availableDevices.Length)
        {
            return null;
        }

        return availableDevices[selectedDeviceIndex].DeviceID;
    }

    private static string GetDefaultDeviceId(MMDeviceEnumerator enumerator, DataFlow dataFlow)
    {
        try
        {
            using (MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(dataFlow, Role.Multimedia))
            {
                return defaultDevice?.DeviceID;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AudioCaptureCSCore] Could not resolve default {dataFlow} endpoint: {ex.Message}");
            return null;
        }
    }

    private static void MoveDefaultDeviceToTop(List<MMDevice> deviceList, string defaultDeviceId)
    {
        if (deviceList == null || deviceList.Count <= 1 || string.IsNullOrEmpty(defaultDeviceId))
        {
            return;
        }

        int defaultIndex = deviceList.FindIndex(device =>
            string.Equals(device.DeviceID, defaultDeviceId, StringComparison.OrdinalIgnoreCase));
        if (defaultIndex <= 0)
        {
            return;
        }

        MMDevice defaultDevice = deviceList[defaultIndex];
        deviceList.RemoveAt(defaultIndex);
        deviceList.Insert(0, defaultDevice);
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

            fftProvider = new FftProvider(waveSource.WaveFormat.Channels, FftSize.Fft4096);
            leftFftProvider = new FftProvider(1, FftSize.Fft4096);
            rightFftProvider = new FftProvider(1, FftSize.Fft4096);
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
            dataSize = 4096;
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
        RefreshDeviceList(preferSystemDefaultDevice);
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
                    string defaultDeviceId = GetDefaultDeviceId(enumerator, dataFlow);
                    var currentDevices = new List<MMDevice>();
                    for (int i = 0; i < col.Count; i++)
                    {
                        currentDevices.Add(col[i]);
                    }

                    MoveDefaultDeviceToTop(currentDevices, defaultDeviceId);
                    currentNames.AddRange(currentDevices.Select(device => device.FriendlyName));
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
                RefreshDeviceList(preferSystemDefaultDevice);

                int restoredIndex = Array.FindIndex(availableDevices ?? Array.Empty<MMDevice>(),
                    d => d.FriendlyName == previousDevice);

                if (!preferSystemDefaultDevice && restoredIndex >= 0)
                {
                    selectedDeviceIndex = restoredIndex;
                }
                else if (availableDevices != null && availableDevices.Length > 0)
                {
                    selectedDeviceIndex = 0;
                    if (!preferSystemDefaultDevice)
                    {
                        Debug.LogWarning($"[AudioCaptureCSCore] Previous device \"{previousDevice}\" disconnected. Switching to index 0.");
                    }
                    InitializeCapture();
                }

                onDeviceListChanged?.Invoke();
            }
        }
    }

    private void UpdateScreenCanvasPanel(bool force)
    {
        if (!useScreenCanvasPanel || !EnsureScreenCanvasPanel(false))
        {
            return;
        }

        _screenCanvasContent.gameObject.SetActive(showManualControlPanel);
        if (!showManualControlPanel)
        {
            return;
        }

        if (!force && Time.unscaledTime < _nextScreenCanvasRefreshTime)
        {
            return;
        }

        _nextScreenCanvasRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, screenCanvasRefreshInterval);
        _screenModeText.text = $"Mode: {captureMode}";
        _screenDeviceText.text = $"Device: {currentDeviceName}";
        _screenDeviceHeaderText.text = $"{captureMode} Devices";

        string signature = captureMode + "|" + selectedDeviceIndex + "|" + string.Join("|", deviceNames);
        if (force || signature != _screenDeviceListSignature)
        {
            _screenDeviceListSignature = signature;
            RebuildScreenDeviceButtons();
        }

        UpdateScreenVisualizerStatus();
    }

    private bool EnsureScreenCanvasPanel(bool forceRebuild)
    {
        if (!useScreenCanvasPanel)
        {
            return false;
        }

        if (screenCanvasPanelRoot == null)
        {
            GameObject panelObject = GameObject.Find("Screen/Canvas/AudioPanel");
            if (panelObject != null)
            {
                screenCanvasPanelRoot = panelObject.GetComponent<RectTransform>();
            }
        }

        if (screenCanvasPanelRoot == null)
        {
            return false;
        }

        if (_screenCanvasContent != null && _screenModeText != null && _screenDeviceText != null && _screenDeviceHeaderText != null && _screenVisualizerText != null && !forceRebuild)
        {
            return true;
        }

        _screenCanvasContent = screenCanvasPanelRoot.Find("AudioCaptureCanvasContent") as RectTransform;
        if (_screenCanvasContent == null)
        {
            Debug.LogError("[AudioCaptureCSCore] AudioCaptureCanvasContent is missing under Screen/Canvas/AudioPanel.");
            return false;
        }

        _screenCanvasFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _screenModeText = FindScreenComponent<Text>("AudioRoutingModule/ModeText");
        _screenDeviceText = FindScreenComponent<Text>("AudioRoutingModule/DeviceText");
        _screenDeviceHeaderText = FindScreenComponent<Text>("AudioRoutingModule/DeviceModule/DeviceHeaderRow/DeviceHeaderText");
        _screenVisualizerText = FindScreenComponent<Text>("AudioStatusModule/AudioStatusText");

        Button inputButton = FindScreenComponent<Button>("AudioRoutingModule/ModeButtons/InputButton");
        Button loopbackButton = FindScreenComponent<Button>("AudioRoutingModule/ModeButtons/LoopbackButton");
        Button refreshButton = FindScreenComponent<Button>("AudioRoutingModule/DeviceModule/DeviceHeaderRow/RefreshButton");
        Button previousButton = FindScreenComponent<Button>("AudioRoutingModule/DeviceModule/NavigationButtons/PreviousButton");
        Button nextButton = FindScreenComponent<Button>("AudioRoutingModule/DeviceModule/NavigationButtons/NextButton");
        _screenHideButton = screenCanvasPanelRoot.Find("AudioPanelControls/HideButton") != null
            ? screenCanvasPanelRoot.Find("AudioPanelControls/HideButton").GetComponent<Button>()
            : null;

        if (_screenCanvasFont == null
            || _screenModeText == null
            || _screenDeviceText == null
            || _screenDeviceHeaderText == null
            || _screenVisualizerText == null
            || inputButton == null
            || loopbackButton == null
            || refreshButton == null
            || previousButton == null
            || nextButton == null
            || _screenHideButton == null
            || EnsureScreenDeviceList() == null)
        {
            Debug.LogError("[AudioCaptureCSCore] Audio panel modules are incomplete. Check the serialized hierarchy under Screen/Canvas/AudioPanel.");
            return false;
        }

        inputButton.onClick.RemoveAllListeners();
        inputButton.onClick.AddListener(() => SwitchCaptureMode(CaptureMode.Input));
        loopbackButton.onClick.RemoveAllListeners();
        loopbackButton.onClick.AddListener(() => SwitchCaptureMode(CaptureMode.Loopback));
        refreshButton.onClick.RemoveAllListeners();
        refreshButton.onClick.AddListener(RefreshDeviceListAndRestartCapture);
        previousButton.onClick.RemoveAllListeners();
        previousButton.onClick.AddListener(SwitchToPreviousDevice);
        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(SwitchToNextDevice);
        _screenHideButton.onClick.RemoveAllListeners();
        _screenHideButton.onClick.AddListener(() => SetAudioPanelVisible(!_screenCanvasContent.gameObject.activeSelf));

        UpdateScreenCanvasPanel(true);
        return true;
    }

    private void SetAudioPanelVisible(bool visible)
    {
        showManualControlPanel = visible;
        _screenCanvasContent.gameObject.SetActive(visible);
        Text label = _screenHideButton != null ? _screenHideButton.GetComponentInChildren<Text>(true) : null;
        if (label != null)
        {
            label.text = visible ? "HIDE" : "SHOW";
        }
    }

    private T FindScreenComponent<T>(string path) where T : Component
    {
        Transform target = _screenCanvasContent != null ? _screenCanvasContent.Find(path) : null;
        return target != null ? target.GetComponent<T>() : null;
    }

    private RectTransform EnsureScreenDeviceList()
    {
        return _screenCanvasContent != null
            ? _screenCanvasContent.Find("AudioRoutingModule/DeviceModule/DeviceList") as RectTransform
            : null;
    }

    private void RebuildScreenDeviceButtons()
    {
        if (_screenCanvasContent == null)
        {
            return;
        }

        RectTransform deviceContent = EnsureScreenDeviceList();
        if (deviceContent == null)
        {
            return;
        }

        if (deviceNames.Count == 0)
        {
            SetScreenDeviceButtonsActive(deviceContent, 0);
            Text noDevicesLabel = FindOrCreateText("NoDevicesText", deviceContent, "No active devices", 10, FontStyle.Italic, TextAnchor.MiddleLeft, 24f);
            noDevicesLabel.gameObject.SetActive(true);
            return;
        }

        Transform noDevicesText = deviceContent.Find("NoDevicesText");
        if (noDevicesText != null)
        {
            noDevicesText.gameObject.SetActive(false);
        }

        for (int i = 0; i < deviceNames.Count; i++)
        {
            int deviceIndex = i;
            string prefix = deviceIndex == selectedDeviceIndex ? "* " : string.Empty;
            Button deviceButton = FindOrCreateButton("DeviceButton" + i, deviceContent, $"{prefix}{deviceIndex}: {deviceNames[i]}", 24f, 9);
            deviceButton.gameObject.SetActive(true);
            deviceButton.transform.SetSiblingIndex(ScreenCanvasArtTheme.GetContentStartSiblingIndex(deviceContent) + i);
            deviceButton.onClick.RemoveAllListeners();
            deviceButton.onClick.AddListener(() => SwitchDevice(deviceIndex));
        }

        SetScreenDeviceButtonsActive(deviceContent, deviceNames.Count);
    }

    private void SetScreenDeviceButtonsActive(RectTransform deviceContent, int activeCount)
    {
        for (int i = 0; i < deviceContent.childCount; i++)
        {
            Transform child = deviceContent.GetChild(i);
            if (!child.name.StartsWith("DeviceButton", StringComparison.Ordinal))
            {
                continue;
            }

            string suffix = child.name.Substring("DeviceButton".Length);
            if (int.TryParse(suffix, out int deviceButtonIndex) && deviceButtonIndex >= activeCount)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateScreenVisualizerStatus()
    {
        if (_screenVisualizerText == null)
        {
            return;
        }

        if (audioVisualizer == null)
        {
            audioVisualizer = FindFirstObjectByType<AudioVisualizer>();
        }

        if (audioVisualizer == null)
        {
            _screenVisualizerText.text = "Audio Visualizer\nAudioVisualizer not found";
            return;
        }

        List<string> lines = new List<string> { "AUDIO ANALYSIS" };
        audioVisualizer.BuildCompactStatusLines(lines);
        _screenVisualizerText.text = string.Join("\n", lines);
    }

    private RectTransform FindOrCreateRow(string name, RectTransform parent, float height)
    {
        RectTransform row = FindOrCreateRect(name, parent, new Vector2(ScreenCanvasChildWidth, height));
        EnsureLayoutElement(row.gameObject, height, height, 0f, ScreenCanvasChildWidth, ScreenCanvasChildWidth);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return row;
    }

    private RectTransform FindOrCreateRect(string name, RectTransform parent, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        RectTransform rectTransform = obj.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = obj.AddComponent<RectTransform>();
        }

        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;
        return rectTransform;
    }

    private Text FindOrCreateText(string name, RectTransform parent, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, float height)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        obj.layer = parent.gameObject.layer;
        RectTransform rectTransform = obj.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = obj.AddComponent<RectTransform>();
        }

        rectTransform.SetParent(parent, false);
        rectTransform.sizeDelta = new Vector2(ScreenCanvasChildWidth, height);
        EnsureLayoutElement(obj, height, height, 0f, ScreenCanvasChildWidth, ScreenCanvasChildWidth);

        if (obj.GetComponent<CanvasRenderer>() == null)
        {
            obj.AddComponent<CanvasRenderer>();
        }

        Text label = obj.GetComponent<Text>();
        if (label == null)
        {
            label = obj.AddComponent<Text>();
        }

        label.font = _screenCanvasFont;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.color = Color.white;
        label.text = text;
        ScreenCanvasArtTheme.StyleText(label, name, fontSize, fontStyle);
        return label;
    }

    private Button FindOrCreateButton(string name, RectTransform parent, string label, float height = 28f, int fontSize = 10)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        obj.layer = parent.gameObject.layer;
        RectTransform rectTransform = obj.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = obj.AddComponent<RectTransform>();
        }

        rectTransform.SetParent(parent, false);
        rectTransform.sizeDelta = new Vector2(80f, height);
        EnsureLayoutElement(obj, height, height, 0f, 80f, 80f);

        if (obj.GetComponent<CanvasRenderer>() == null)
        {
            obj.AddComponent<CanvasRenderer>();
        }

        Image image = obj.GetComponent<Image>();
        if (image == null)
        {
            image = obj.AddComponent<Image>();
        }
        image.color = new Color(0.18f, 0.24f, 0.28f, 0.88f);
        image.raycastTarget = true;

        Button button = obj.GetComponent<Button>();
        if (button == null)
        {
            button = obj.AddComponent<Button>();
        }
        button.targetGraphic = image;
        button.interactable = true;
        ScreenCanvasArtTheme.StyleSelectable(button, image, true);

        Text text = FindOrCreateText("Label", rectTransform, label, fontSize, FontStyle.Normal, TextAnchor.MiddleCenter, height);
        text.color = Color.white;
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 2f);
        textRect.offsetMax = new Vector2(-4f, -2f);
        return button;
    }

    private static LayoutElement EnsureLayoutElement(GameObject obj, float minHeight, float preferredHeight, float flexibleHeight, float minWidth = -1f, float preferredWidth = -1f)
    {
        LayoutElement layoutElement = obj.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = obj.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = minHeight;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleHeight = flexibleHeight;
        layoutElement.minWidth = minWidth;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.flexibleWidth = minWidth > 0f ? 0f : -1f;
        return layoutElement;
    }
}
