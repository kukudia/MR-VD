using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime panel for pairing with an Android phone over Bluetooth and preparing notification access.
/// </summary>
public sealed class AndroidPhonePairingPanel : MonoBehaviour
{
    private const string PanelObjectName = "AndroidPhonePairingPanel";

    private static bool bootstrapped;

    [Header("Runtime Panel")]
    [Tooltip("Shows the Android phone pairing panel at runtime.")]
    public bool showPanel = true;

    [Tooltip("Position and size of the Android phone pairing panel.")]
    public Rect panelRect = new Rect(452f, 16f, 460f, 640f);

    [Tooltip("Height of the device list scroll view.")]
    [Range(120f, 420f)]
    public float deviceListHeight = 260f;

    [Tooltip("How often the panel refreshes Android bridge status while visible.")]
    [Range(0.5f, 10f)]
    public float statusRefreshInterval = 2f;

    private AndroidPhoneBridgeStatus status = AndroidPhoneBridgeStatus.CreateUnsupported();
    private AndroidPhoneDeviceList devices = new AndroidPhoneDeviceList();
    private AndroidPhoneNotificationEvent lastNotification;
    private Vector2 deviceScroll;
    private string selectedAddress = string.Empty;
    private string selectedName = string.Empty;
    private string scanState = "idle";
    private string lastMessage = string.Empty;
    private string lastPairingMessage = string.Empty;
    private float nextStatusRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (bootstrapped)
        {
            return;
        }

        bootstrapped = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsurePanelExists();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsurePanelExists();
    }

    private static void EnsurePanelExists()
    {
        if (Object.FindFirstObjectByType<AndroidPhonePairingPanel>() != null)
        {
            return;
        }

        GameObject panelObject = new GameObject(PanelObjectName);
        DontDestroyOnLoad(panelObject);
        panelObject.AddComponent<AndroidPhonePairingPanel>();
    }

    private void OnEnable()
    {
        AndroidPhoneBridge.StatusChanged += OnStatusChanged;
        AndroidPhoneBridge.DevicesChanged += OnDevicesChanged;
        AndroidPhoneBridge.ScanStateChanged += OnScanStateChanged;
        AndroidPhoneBridge.PairingResultReceived += OnPairingResultReceived;
        AndroidPhoneBridge.NotificationReceived += OnNotificationReceived;
        AndroidPhoneBridge.MessageReceived += OnMessageReceived;

        devices.Normalize();
        AndroidPhoneBridge.Initialize();
        RefreshAll();
    }

    private void OnDisable()
    {
        AndroidPhoneBridge.StatusChanged -= OnStatusChanged;
        AndroidPhoneBridge.DevicesChanged -= OnDevicesChanged;
        AndroidPhoneBridge.ScanStateChanged -= OnScanStateChanged;
        AndroidPhoneBridge.PairingResultReceived -= OnPairingResultReceived;
        AndroidPhoneBridge.NotificationReceived -= OnNotificationReceived;
        AndroidPhoneBridge.MessageReceived -= OnMessageReceived;
    }

    private void Update()
    {
        if (!showPanel || Time.unscaledTime < nextStatusRefreshTime)
        {
            return;
        }

        AndroidPhoneBridge.RefreshState();
        nextStatusRefreshTime = Time.unscaledTime + statusRefreshInterval;
    }

    private void OnGUI()
    {
        if (!showPanel)
        {
            return;
        }

        panelRect.width = Mathf.Max(panelRect.width, 460f);
        panelRect.height = Mathf.Max(panelRect.height, 560f);
        panelRect = GUILayout.Window(GetInstanceID(), panelRect, DrawPanel, "Android Phone");
    }

    private void DrawPanel(int windowId)
    {
        DrawStatusSection();
        GUILayout.Space(8f);
        DrawActionSection();
        GUILayout.Space(8f);
        DrawDeviceSection();
        GUILayout.Space(8f);
        DrawNotificationSection();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DrawStatusSection()
    {
        GUILayout.Label($"Runtime: {(status.isSupported ? "Android" : "Editor/Unsupported")}");
        GUILayout.Label($"Bluetooth: {FormatBool(status.bluetoothEnabled)} | Permission: {FormatBool(status.hasBluetoothPermissions)}");
        GUILayout.Label($"Adapter: {FormatValue(status.adapterName)}");
        GUILayout.Label($"Discovery: {scanState} | Notification access: {FormatBool(status.notificationListenerEnabled)}");

        if (!string.IsNullOrWhiteSpace(status.message))
        {
            GUILayout.Label($"Status: {status.message}");
        }

        if (!string.IsNullOrWhiteSpace(lastMessage))
        {
            GUILayout.Label($"Message: {lastMessage}");
        }

        if (!string.IsNullOrWhiteSpace(lastPairingMessage))
        {
            GUILayout.Label($"Pairing: {lastPairingMessage}");
        }
    }

    private void DrawActionSection()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh"))
        {
            RefreshAll();
        }

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && status.isSupported;
        if (GUILayout.Button("Permissions"))
        {
            AndroidPhoneBridge.RequestBluetoothPermissions();
        }

        if (GUILayout.Button("Enable BT"))
        {
            AndroidPhoneBridge.RequestEnableBluetooth();
        }
        GUI.enabled = previousEnabled;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUI.enabled = previousEnabled && status.isSupported;
        if (GUILayout.Button(status.isDiscovering ? "Stop Scan" : "Scan"))
        {
            if (status.isDiscovering)
            {
                AndroidPhoneBridge.CancelDiscovery();
            }
            else
            {
                AndroidPhoneBridge.StartDiscovery();
            }
        }

        if (GUILayout.Button("BT Settings"))
        {
            AndroidPhoneBridge.OpenBluetoothSettings();
        }

        if (GUILayout.Button("Notification Access"))
        {
            AndroidPhoneBridge.OpenNotificationListenerSettings();
        }
        GUI.enabled = previousEnabled;
        GUILayout.EndHorizontal();
    }

    private void DrawDeviceSection()
    {
        GUILayout.Label("Phone / Bluetooth Devices");
        GUILayout.Label($"Selected: {FormatSelectedDevice()}");

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && status.isSupported && !string.IsNullOrWhiteSpace(selectedAddress);
        if (GUILayout.Button("Pair Selected Device"))
        {
            AndroidPhoneBridge.PairDevice(selectedAddress);
        }
        GUI.enabled = previousEnabled;

        deviceScroll = GUILayout.BeginScrollView(deviceScroll, GUILayout.Height(deviceListHeight));
        DrawDeviceList("Bonded", devices.bonded, true);
        GUILayout.Space(6f);
        DrawDeviceList("Discovered", devices.discovered, false);
        GUILayout.EndScrollView();
    }

    private void DrawDeviceList(string title, AndroidPhoneDeviceInfo[] deviceList, bool alreadyBonded)
    {
        GUILayout.Label($"{title} ({deviceList?.Length ?? 0})");
        if (deviceList == null || deviceList.Length == 0)
        {
            GUILayout.Label(alreadyBonded ? "No paired devices." : "No discovered devices. Start scanning first.");
            return;
        }

        for (int i = 0; i < deviceList.Length; i++)
        {
            AndroidPhoneDeviceInfo device = deviceList[i];
            if (device == null)
            {
                continue;
            }

            string selectedPrefix = device.address == selectedAddress ? "* " : string.Empty;
            string bondLabel = device.isBonded ? "Bonded" : FormatValue(device.bondStateName);
            string label = $"{selectedPrefix}{device.DisplayName} [{bondLabel}]";
            if (!string.IsNullOrWhiteSpace(device.address))
            {
                label += $" {device.address}";
            }

            if (GUILayout.Button(label))
            {
                selectedAddress = device.address ?? string.Empty;
                selectedName = device.DisplayName;
            }
        }
    }

    private void DrawNotificationSection()
    {
        GUILayout.Label("NotificationListenerService");
        GUILayout.Label(status.notificationListenerEnabled
            ? "Listener access is enabled for this Android device."
            : "Open Notification Access and allow this app before testing notifications.");

        if (lastNotification == null)
        {
            GUILayout.Label("Last notification: none");
            return;
        }

        GUILayout.Label($"Last notification: {FormatValue(lastNotification.appLabel)} / {FormatValue(lastNotification.packageName)}");
        GUILayout.Label($"Title: {FormatValue(lastNotification.title)}");
        GUILayout.Label($"Text: {FormatValue(lastNotification.text)}");
    }

    private void RefreshAll()
    {
        status = AndroidPhoneBridge.RefreshState();
        devices = AndroidPhoneBridge.RefreshDevices();
        devices.Normalize();
        nextStatusRefreshTime = Time.unscaledTime + statusRefreshInterval;
    }

    private void OnStatusChanged(AndroidPhoneBridgeStatus newStatus)
    {
        status = newStatus ?? AndroidPhoneBridgeStatus.CreateUnsupported();
        scanState = status.isDiscovering ? "discovering" : scanState;
    }

    private void OnDevicesChanged(AndroidPhoneDeviceList newDevices)
    {
        devices = newDevices ?? new AndroidPhoneDeviceList();
        devices.Normalize();

        if (!string.IsNullOrWhiteSpace(devices.message))
        {
            lastMessage = devices.message;
        }
    }

    private void OnScanStateChanged(string newScanState)
    {
        scanState = string.IsNullOrWhiteSpace(newScanState) ? "idle" : newScanState;
    }

    private void OnPairingResultReceived(AndroidPhonePairingResult result)
    {
        if (result == null)
        {
            return;
        }

        string deviceName = string.IsNullOrWhiteSpace(result.name) ? result.address : result.name;
        lastPairingMessage = $"{deviceName}: {result.message}";
    }

    private void OnNotificationReceived(AndroidPhoneNotificationEvent notificationEvent)
    {
        lastNotification = notificationEvent;
    }

    private void OnMessageReceived(string message)
    {
        lastMessage = message;
    }

    private string FormatSelectedDevice()
    {
        if (string.IsNullOrWhiteSpace(selectedAddress))
        {
            return "None";
        }

        return string.IsNullOrWhiteSpace(selectedName)
            ? selectedAddress
            : $"{selectedName} ({selectedAddress})";
    }

    private static string FormatBool(bool value)
    {
        return value ? "Yes" : "No";
    }

    private static string FormatValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}
