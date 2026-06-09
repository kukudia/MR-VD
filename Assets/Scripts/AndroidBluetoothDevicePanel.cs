using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class AndroidBluetoothDevicePanel : MonoBehaviour
{
    private const float MinimumRefreshIntervalSeconds = 1f;

    private const string QueryBluetoothDevicesScript = @"
$ErrorActionPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$separator = [char]9
Get-PnpDevice -Class Bluetooth -ErrorAction SilentlyContinue | ForEach-Object {
    $name = [string]$_.FriendlyName
    if ([string]::IsNullOrWhiteSpace($name)) {
        $name = [string]$_.Name
    }

    if (-not [string]::IsNullOrWhiteSpace($name)) {
        $fields = @(
            $name,
            [string]$_.InstanceId,
            [string]$_.Manufacturer,
            [string]$_.Status,
            [string]$_.Present,
            [string]$_.Class,
            [string]$_.Description
        )

        ($fields | ForEach-Object {
            [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([string]$_))
        }) -join $separator
    }
}
";

    public bool showPanel = true;
    public bool autoRefresh = true;
    public float deviceListHeight = 220f;
    public float refreshIntervalSeconds = 5f;
    public float queryTimeoutSeconds = 8f;
    public Rect panelRect = new Rect(16f, 600f, 440f, 420f);

    private static AndroidBluetoothDevicePanel instance;

    private readonly object stateLock = new object();
    private readonly List<BluetoothDeviceSnapshot> devices = new List<BluetoothDeviceSnapshot>();
    private readonly Queue<AndroidPhoneNotification> recentNotifications = new Queue<AndroidPhoneNotification>();

    private CancellationTokenSource refreshCancellation;
    private IAndroidNotificationBridge notificationBridge = new PlaceholderAndroidNotificationBridge();
    private Vector2 deviceScrollPosition;
    private bool queryInProgress;
    private DateTime lastRefreshUtc;
    private string lastError;
    private string selectedDeviceInstanceId = string.Empty;
    private string selectedDeviceName = string.Empty;
    private GUIStyle wrapLabelStyle;
    private GUIStyle strongLabelStyle;

    public event Action<AndroidPhoneNotification> NotificationReceived;

    public BluetoothDeviceSnapshot[] CurrentDevices
    {
        get
        {
            lock (stateLock)
            {
                return devices.ToArray();
            }
        }
    }

    public BluetoothDeviceSnapshot SelectedDevice
    {
        get
        {
            lock (stateLock)
            {
                return FindSelectedDevice(devices.ToArray());
            }
        }
    }

    public bool IsSelectedDeviceConnected
    {
        get
        {
            BluetoothDeviceSnapshot selectedDevice = SelectedDevice;
            return selectedDevice != null && selectedDevice.IsConnected;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreatePanelOnWindows()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (FindFirstObjectByType<AndroidBluetoothDevicePanel>() != null)
        {
            return;
        }

        GameObject panelObject = new GameObject("Android Bluetooth Device Panel");
        DontDestroyOnLoad(panelObject);
        panelObject.AddComponent<AndroidBluetoothDevicePanel>();
#endif
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        refreshCancellation = new CancellationTokenSource();
        notificationBridge.NotificationReceived += OnBridgeNotificationReceived;
    }

    private void OnEnable()
    {
        RequestRefresh(true);
    }

    private void Update()
    {
        if (autoRefresh
            && (DateTime.UtcNow - lastRefreshUtc).TotalSeconds >= Mathf.Max(MinimumRefreshIntervalSeconds, refreshIntervalSeconds))
        {
            RequestRefresh(false);
        }

        notificationBridge.Tick();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (notificationBridge != null)
        {
            notificationBridge.NotificationReceived -= OnBridgeNotificationReceived;
            notificationBridge.StopListening();
        }

        if (refreshCancellation != null)
        {
            refreshCancellation.Cancel();
            refreshCancellation.Dispose();
            refreshCancellation = null;
        }
    }

    public void SetNotificationBridge(IAndroidNotificationBridge bridge)
    {
        if (bridge == null)
        {
            throw new ArgumentNullException(nameof(bridge));
        }

        if (notificationBridge != null)
        {
            notificationBridge.NotificationReceived -= OnBridgeNotificationReceived;
            notificationBridge.StopListening();
        }

        notificationBridge = bridge;
        notificationBridge.NotificationReceived += OnBridgeNotificationReceived;
    }

    public void RequestRefresh(bool force)
    {
        if (!IsWindowsRuntime())
        {
            lock (stateLock)
            {
                lastError = "Bluetooth device detection is available only on Windows.";
                lastRefreshUtc = DateTime.UtcNow;
            }

            return;
        }

        lock (stateLock)
        {
            if (queryInProgress)
            {
                return;
            }

            if (!force && (DateTime.UtcNow - lastRefreshUtc).TotalSeconds < Mathf.Max(MinimumRefreshIntervalSeconds, refreshIntervalSeconds))
            {
                return;
            }

            queryInProgress = true;
            lastError = null;
        }

        if (refreshCancellation == null || refreshCancellation.IsCancellationRequested)
        {
            refreshCancellation = new CancellationTokenSource();
        }

        int timeoutMilliseconds = Mathf.Max(1000, Mathf.RoundToInt(queryTimeoutSeconds * 1000f));
        CancellationToken token = refreshCancellation.Token;

        Task.Run(() => QueryBluetoothDevices(timeoutMilliseconds, token), token)
            .ContinueWith(task =>
            {
                BluetoothQueryResult result = null;
                string error = null;

                if (task.IsCanceled || token.IsCancellationRequested)
                {
                    error = "Bluetooth refresh was canceled.";
                }
                else if (task.IsFaulted)
                {
                    error = task.Exception != null ? task.Exception.GetBaseException().Message : "Bluetooth refresh failed.";
                }
                else
                {
                    result = task.Result;
                    error = result.ErrorMessage;
                }

                lock (stateLock)
                {
                    queryInProgress = false;

                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    devices.Clear();

                    if (result != null)
                    {
                        devices.AddRange(result.Devices);
                    }

                    lastError = error;
                    lastRefreshUtc = DateTime.UtcNow;
                }
            }, CancellationToken.None);
    }

    private void OnGUI()
    {
        if (!showPanel)
        {
            return;
        }

        EnsureStyles();
        panelRect.width = Mathf.Max(panelRect.width, 420f);
        panelRect.height = Mathf.Max(panelRect.height, 360f);
        panelRect = GUILayout.Window(GetInstanceID(), panelRect, DrawPanel, "Android Bluetooth");
    }

    private void DrawPanel(int windowId)
    {
        BluetoothDeviceSnapshot[] snapshot;
        string error;
        bool refreshing;
        DateTime refreshedUtc;

        lock (stateLock)
        {
            snapshot = devices.ToArray();
            error = lastError;
            refreshing = queryInProgress;
            refreshedUtc = lastRefreshUtc;
        }

        BluetoothDeviceSnapshot selectedDevice = FindSelectedDevice(snapshot);
        int connectedBluetoothCount = CountConnectedBluetoothDevices(snapshot);

        GUILayout.Label($"Selected device: {FormatSelectedDevice(selectedDevice)}", strongLabelStyle);
        GUILayout.Label($"Selected connected: {FormatBool(selectedDevice != null && selectedDevice.IsConnected)} | Bluetooth connected/present: {connectedBluetoothCount}");

        GUILayout.BeginHorizontal();
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && !refreshing;
        if (GUILayout.Button(refreshing ? "Refreshing..." : "Refresh"))
        {
            RequestRefresh(true);
        }

        GUI.enabled = wasEnabled;
        autoRefresh = GUILayout.Toggle(autoRefresh, "Auto");
        GUILayout.EndHorizontal();

        if (refreshedUtc != default)
        {
            GUILayout.Label($"Last refresh: {refreshedUtc.ToLocalTime():HH:mm:ss}");
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            GUILayout.Label(error, wrapLabelStyle);
        }

        GUILayout.Space(6f);
        DrawDeviceList(snapshot);
        GUILayout.Space(8f);
        DrawNotificationBridge(snapshot);

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DrawDeviceList(BluetoothDeviceSnapshot[] snapshot)
    {
        GUILayout.Label("Bluetooth Devices", strongLabelStyle);
        deviceScrollPosition = GUILayout.BeginScrollView(deviceScrollPosition, GUILayout.Height(deviceListHeight));

        for (int i = 0; i < snapshot.Length; i++)
        {
            BluetoothDeviceSnapshot device = snapshot[i];
            string prefix = IsSelectedDevice(device) ? "* " : string.Empty;
            string label = $"{prefix}{i}: {device.Name} [{device.ConnectionLabel}]";

            if (GUILayout.Button(label))
            {
                SelectDevice(device);
            }
        }

        if (snapshot.Length == 0)
        {
            GUILayout.Label("No Bluetooth devices. Pair or connect an Android phone, then refresh.", wrapLabelStyle);
        }

        GUILayout.EndScrollView();

        BluetoothDeviceSnapshot selectedDevice = FindSelectedDevice(snapshot);
        if (selectedDevice != null)
        {
            GUILayout.Space(4f);
            GUILayout.Label("Selected Device", strongLabelStyle);
            GUILayout.Label($"{selectedDevice.Name} | {selectedDevice.ConnectionLabel}", wrapLabelStyle);

            if (!string.IsNullOrWhiteSpace(selectedDevice.Manufacturer))
            {
                GUILayout.Label($"Manufacturer: {selectedDevice.Manufacturer}", wrapLabelStyle);
            }

            GUILayout.Label(selectedDevice.InstanceId, wrapLabelStyle);
        }
        else if (!string.IsNullOrWhiteSpace(selectedDeviceName))
        {
            GUILayout.Label($"Selected device not found: {selectedDeviceName}", wrapLabelStyle);
        }
    }

    private void DrawNotificationBridge(BluetoothDeviceSnapshot[] snapshot)
    {
        BluetoothDeviceSnapshot targetDevice = FindSelectedDevice(snapshot);

        GUILayout.Label("Phone Notifications", strongLabelStyle);
        GUILayout.Label(notificationBridge.StatusText, wrapLabelStyle);

        GUILayout.BeginHorizontal();
        bool wasEnabled = GUI.enabled;

        if (notificationBridge.IsListening)
        {
            if (GUILayout.Button("Stop Listener"))
            {
                notificationBridge.StopListening();
            }
        }
        else
        {
            GUI.enabled = wasEnabled && targetDevice != null && targetDevice.IsConnected;
            if (GUILayout.Button("Start Listener"))
            {
                notificationBridge.StartListening(targetDevice);
            }

            GUI.enabled = wasEnabled;
        }

        if (targetDevice != null)
        {
            GUILayout.Label($"Target: {targetDevice.Name}");
        }
        else
        {
            GUILayout.Label("Target: none");
        }

        GUILayout.EndHorizontal();
        GUI.enabled = wasEnabled;

        AndroidPhoneNotification latest = GetLatestNotification();
        if (latest != null)
        {
            GUILayout.Label($"Latest: {latest.Title} - {latest.Body}", wrapLabelStyle);
        }
    }

    private void EnsureStyles()
    {
        if (wrapLabelStyle != null)
        {
            return;
        }

        wrapLabelStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true
        };

        strongLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
    }

    private void OnBridgeNotificationReceived(AndroidPhoneNotification notification)
    {
        if (notification == null)
        {
            return;
        }

        lock (stateLock)
        {
            recentNotifications.Enqueue(notification);
            while (recentNotifications.Count > 5)
            {
                recentNotifications.Dequeue();
            }
        }

        NotificationReceived?.Invoke(notification);
    }

    private AndroidPhoneNotification GetLatestNotification()
    {
        lock (stateLock)
        {
            AndroidPhoneNotification latest = null;
            foreach (AndroidPhoneNotification notification in recentNotifications)
            {
                latest = notification;
            }

            return latest;
        }
    }

    private static BluetoothQueryResult QueryBluetoothDevices(int timeoutMilliseconds, CancellationToken token)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + EncodePowerShellCommand(QueryBluetoothDevicesScript),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using (Process process = new Process())
        {
            process.StartInfo = startInfo;

            try
            {
                if (!process.Start())
                {
                    return BluetoothQueryResult.Failed("Unable to start PowerShell.");
                }
            }
            catch (Exception ex)
            {
                return BluetoothQueryResult.Failed(ex.Message);
            }

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMilliseconds) || token.IsCancellationRequested)
            {
                TryKill(process);
                return BluetoothQueryResult.Failed("Timed out while querying Windows Bluetooth devices.");
            }

            string output = ReadCompletedTask(outputTask);
            string error = ReadCompletedTask(errorTask);
            List<BluetoothDeviceSnapshot> parsedDevices = ParsePowerShellOutput(output);

            if (process.ExitCode != 0)
            {
                return new BluetoothQueryResult(parsedDevices, string.IsNullOrWhiteSpace(error) ? "PowerShell Bluetooth query failed." : error.Trim());
            }

            return new BluetoothQueryResult(parsedDevices, null);
        }
    }

    private static string EncodePowerShellCommand(string command)
    {
        return Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
    }

    private static string ReadCompletedTask(Task<string> task)
    {
        try
        {
            return task.Wait(1000) ? task.Result : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch
        {
            // Process may already be gone.
        }
    }

    private static List<BluetoothDeviceSnapshot> ParsePowerShellOutput(string output)
    {
        List<BluetoothDeviceSnapshot> parsedDevices = new List<BluetoothDeviceSnapshot>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return parsedDevices;
        }

        string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        HashSet<string> seenDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split('\t');
            if (fields.Length < 7)
            {
                continue;
            }

            string name = DecodeField(fields[0]);
            string instanceId = DecodeField(fields[1]);
            string manufacturer = DecodeField(fields[2]);
            string status = DecodeField(fields[3]);
            bool present = string.Equals(DecodeField(fields[4]), "True", StringComparison.OrdinalIgnoreCase);
            string deviceClass = DecodeField(fields[5]);
            string description = DecodeField(fields[6]);

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string uniqueKey = string.IsNullOrWhiteSpace(instanceId) ? name : instanceId;
            if (!seenDevices.Add(uniqueKey))
            {
                continue;
            }

            parsedDevices.Add(new BluetoothDeviceSnapshot(
                name,
                instanceId,
                manufacturer,
                status,
                present,
                deviceClass,
                description));
        }

        parsedDevices.Sort(CompareDevices);
        return parsedDevices;
    }

    private static string DecodeField(string encodedValue)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(encodedValue);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int CompareDevices(BluetoothDeviceSnapshot left, BluetoothDeviceSnapshot right)
    {
        int connectionCompare = right.IsConnected.CompareTo(left.IsConnected);
        if (connectionCompare != 0)
        {
            return connectionCompare;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountConnectedBluetoothDevices(BluetoothDeviceSnapshot[] snapshot)
    {
        int count = 0;
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i].IsConnected)
            {
                count++;
            }
        }

        return count;
    }

    private void SelectDevice(BluetoothDeviceSnapshot device)
    {
        if (device == null)
        {
            selectedDeviceInstanceId = string.Empty;
            selectedDeviceName = string.Empty;
            return;
        }

        selectedDeviceInstanceId = device.InstanceId;
        selectedDeviceName = device.Name;

        if (notificationBridge.IsListening)
        {
            notificationBridge.StopListening();
        }
    }

    private bool IsSelectedDevice(BluetoothDeviceSnapshot device)
    {
        if (device == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selectedDeviceInstanceId)
            && string.Equals(device.InstanceId, selectedDeviceInstanceId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(selectedDeviceInstanceId)
            && !string.IsNullOrWhiteSpace(selectedDeviceName)
            && string.Equals(device.Name, selectedDeviceName, StringComparison.OrdinalIgnoreCase);
    }

    private BluetoothDeviceSnapshot FindSelectedDevice(BluetoothDeviceSnapshot[] snapshot)
    {
        if (snapshot == null || snapshot.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            if (IsSelectedDevice(snapshot[i]))
            {
                return snapshot[i];
            }
        }

        return null;
    }

    private static string FormatSelectedDevice(BluetoothDeviceSnapshot selectedDevice)
    {
        return selectedDevice == null ? "None" : selectedDevice.Name;
    }

    private static string FormatBool(bool value)
    {
        return value ? "Yes" : "No";
    }

    private static bool IsWindowsRuntime()
    {
        return Application.platform == RuntimePlatform.WindowsPlayer
            || Application.platform == RuntimePlatform.WindowsEditor;
    }

    private sealed class BluetoothQueryResult
    {
        public readonly List<BluetoothDeviceSnapshot> Devices;
        public readonly string ErrorMessage;

        public BluetoothQueryResult(List<BluetoothDeviceSnapshot> devices, string errorMessage)
        {
            Devices = devices ?? new List<BluetoothDeviceSnapshot>();
            ErrorMessage = errorMessage;
        }

        public static BluetoothQueryResult Failed(string errorMessage)
        {
            return new BluetoothQueryResult(new List<BluetoothDeviceSnapshot>(), errorMessage);
        }
    }

    private sealed class PlaceholderAndroidNotificationBridge : IAndroidNotificationBridge
    {
        private const string IdleStatus = "Ready for a future Android notification bridge. Windows Bluetooth alone does not expose phone notifications.";

        private BluetoothDeviceSnapshot currentDevice;

        public event Action<AndroidPhoneNotification> NotificationReceived
        {
            add { }
            remove { }
        }

        public bool IsListening { get; private set; }

        public string StatusText
        {
            get
            {
                if (!IsListening)
                {
                    return IdleStatus;
                }

                return currentDevice == null
                    ? "Waiting for Android notification bridge input."
                    : $"Waiting for Android notification bridge input from {currentDevice.Name}.";
            }
        }

        public void StartListening(BluetoothDeviceSnapshot device)
        {
            currentDevice = device;
            IsListening = true;
        }

        public void StopListening()
        {
            currentDevice = null;
            IsListening = false;
        }

        public void Tick()
        {
        }
    }
}

[Serializable]
public sealed class BluetoothDeviceSnapshot
{
    public readonly string Name;
    public readonly string InstanceId;
    public readonly string Manufacturer;
    public readonly string Status;
    public readonly bool Present;
    public readonly string DeviceClass;
    public readonly string Description;

    public BluetoothDeviceSnapshot(
        string name,
        string instanceId,
        string manufacturer,
        string status,
        bool present,
        string deviceClass,
        string description)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Unknown Bluetooth Device" : name;
        InstanceId = instanceId ?? string.Empty;
        Manufacturer = manufacturer ?? string.Empty;
        Status = status ?? string.Empty;
        Present = present;
        DeviceClass = deviceClass ?? string.Empty;
        Description = description ?? string.Empty;
    }

    public bool IsConnected
    {
        get
        {
            return Present && (string.IsNullOrWhiteSpace(Status) || string.Equals(Status, "OK", StringComparison.OrdinalIgnoreCase));
        }
    }

    public string ConnectionLabel
    {
        get
        {
            if (IsConnected)
            {
                return "Connected/Present";
            }

            return string.IsNullOrWhiteSpace(Status) ? "Not connected" : Status;
        }
    }
}

[Serializable]
public sealed class AndroidPhoneNotification
{
    public readonly string PackageName;
    public readonly string AppName;
    public readonly string Title;
    public readonly string Body;
    public readonly DateTime ReceivedAt;

    public AndroidPhoneNotification(string packageName, string appName, string title, string body, DateTime receivedAt)
    {
        PackageName = packageName ?? string.Empty;
        AppName = appName ?? string.Empty;
        Title = title ?? string.Empty;
        Body = body ?? string.Empty;
        ReceivedAt = receivedAt;
    }
}

public interface IAndroidNotificationBridge
{
    event Action<AndroidPhoneNotification> NotificationReceived;

    bool IsListening { get; }

    string StatusText { get; }

    void StartListening(BluetoothDeviceSnapshot device);

    void StopListening();

    void Tick();
}
