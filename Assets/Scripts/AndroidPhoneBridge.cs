using System;
using UnityEngine;

/// <summary>
/// Thin Unity side wrapper around the Android Bluetooth and notification listener bridge.
/// </summary>
public static class AndroidPhoneBridge
{
    public const string ReceiverGameObjectName = "AndroidPhoneBridge";

    private const string JavaBridgeClassName = "com.mrvd.androidphone.PhoneBridge";

    private static AndroidPhoneBridgeStatus lastStatus = AndroidPhoneBridgeStatus.CreateUnsupported();
    private static AndroidPhoneDeviceList lastDevices = new AndroidPhoneDeviceList();
    private static bool initialized;

    public static event Action<AndroidPhoneBridgeStatus> StatusChanged;
    public static event Action<AndroidPhoneDeviceList> DevicesChanged;
    public static event Action<string> ScanStateChanged;
    public static event Action<AndroidPhonePairingResult> PairingResultReceived;
    public static event Action<AndroidPhoneNotificationEvent> NotificationReceived;
    public static event Action<string> MessageReceived;

    public static AndroidPhoneBridgeStatus LastStatus => lastStatus;
    public static AndroidPhoneDeviceList LastDevices => lastDevices;

    public static bool IsAndroidRuntime
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Application.platform == RuntimePlatform.Android;
#else
            return false;
#endif
        }
    }

    public static void Initialize()
    {
        EnsureReceiver();

        if (!IsAndroidRuntime)
        {
            lastStatus = AndroidPhoneBridgeStatus.CreateUnsupported();
            StatusChanged?.Invoke(lastStatus);
            return;
        }

        if (initialized)
        {
            RefreshState();
            RefreshDevices();
            return;
        }

        initialized = true;
        CallAndroid("initialize", GetUnityActivity(), ReceiverGameObjectName);
        RefreshState();
        RefreshDevices();
    }

    public static AndroidPhoneBridgeStatus RefreshState()
    {
        if (!IsAndroidRuntime)
        {
            lastStatus = AndroidPhoneBridgeStatus.CreateUnsupported();
            StatusChanged?.Invoke(lastStatus);
            return lastStatus;
        }

        HandleStatusJson(CallAndroidString("getStateJson", string.Empty));
        return lastStatus;
    }

    public static AndroidPhoneDeviceList RefreshDevices()
    {
        if (!IsAndroidRuntime)
        {
            lastDevices = new AndroidPhoneDeviceList();
            DevicesChanged?.Invoke(lastDevices);
            return lastDevices;
        }

        HandleDevicesJson(CallAndroidString("getDeviceListJson", string.Empty));
        return lastDevices;
    }

    public static void RequestBluetoothPermissions()
    {
        CallAndroid("requestBluetoothPermissions");
    }

    public static void RequestEnableBluetooth()
    {
        CallAndroid("requestEnableBluetooth");
    }

    public static void OpenBluetoothSettings()
    {
        CallAndroid("openBluetoothSettings");
    }

    public static void OpenNotificationListenerSettings()
    {
        CallAndroid("openNotificationListenerSettings");
    }

    public static void StartDiscovery()
    {
        CallAndroid("startDiscovery");
    }

    public static void CancelDiscovery()
    {
        CallAndroid("cancelDiscovery");
    }

    public static void PairDevice(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            MessageReceived?.Invoke("No Bluetooth device selected.");
            return;
        }

        CallAndroid("pairDevice", address);
    }

    internal static void HandleStatusJson(string json)
    {
        AndroidPhoneBridgeStatus parsed = FromJson<AndroidPhoneBridgeStatus>(json);
        if (parsed == null)
        {
            parsed = AndroidPhoneBridgeStatus.CreateUnsupported("Invalid Android bridge status.");
        }

        lastStatus = parsed;
        StatusChanged?.Invoke(lastStatus);
    }

    internal static void HandleDevicesJson(string json)
    {
        AndroidPhoneDeviceList parsed = FromJson<AndroidPhoneDeviceList>(json);
        lastDevices = parsed ?? new AndroidPhoneDeviceList();
        lastDevices.Normalize();
        DevicesChanged?.Invoke(lastDevices);
    }

    internal static void HandleScanState(string state)
    {
        ScanStateChanged?.Invoke(state);
        RefreshState();
    }

    internal static void HandlePairingResultJson(string json)
    {
        AndroidPhonePairingResult result = FromJson<AndroidPhonePairingResult>(json);
        if (result != null)
        {
            PairingResultReceived?.Invoke(result);
        }

        RefreshState();
        RefreshDevices();
    }

    internal static void HandleNotificationJson(string json)
    {
        AndroidPhoneNotificationEvent notificationEvent = FromJson<AndroidPhoneNotificationEvent>(json);
        if (notificationEvent != null)
        {
            NotificationReceived?.Invoke(notificationEvent);
        }

        RefreshState();
    }

    internal static void HandleMessage(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            MessageReceived?.Invoke(message);
        }
    }

    private static void EnsureReceiver()
    {
        GameObject receiverObject = GameObject.Find(ReceiverGameObjectName);
        if (receiverObject == null)
        {
            receiverObject = new GameObject(ReceiverGameObjectName);
            UnityEngine.Object.DontDestroyOnLoad(receiverObject);
        }

        if (receiverObject.GetComponent<AndroidPhoneBridgeReceiver>() == null)
        {
            receiverObject.AddComponent<AndroidPhoneBridgeReceiver>();
        }
    }

    private static T FromJson<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (ArgumentException ex)
        {
            Debug.LogWarning($"[AndroidPhoneBridge] Failed to parse JSON for {typeof(T).Name}: {ex.Message}");
            return null;
        }
    }

    private static void CallAndroid(string methodName, params object[] args)
    {
        if (!IsAndroidRuntime)
        {
            return;
        }

        try
        {
            using (AndroidJavaClass bridgeClass = new AndroidJavaClass(JavaBridgeClassName))
            {
                bridgeClass.CallStatic(methodName, args);
            }
        }
        catch (Exception ex)
        {
            string message = $"Android bridge call failed: {methodName} ({ex.Message})";
            Debug.LogWarning($"[AndroidPhoneBridge] {message}");
            MessageReceived?.Invoke(message);
        }
    }

    private static string CallAndroidString(string methodName, string fallback, params object[] args)
    {
        if (!IsAndroidRuntime)
        {
            return fallback;
        }

        try
        {
            using (AndroidJavaClass bridgeClass = new AndroidJavaClass(JavaBridgeClassName))
            {
                return bridgeClass.CallStatic<string>(methodName, args);
            }
        }
        catch (Exception ex)
        {
            string message = $"Android bridge call failed: {methodName} ({ex.Message})";
            Debug.LogWarning($"[AndroidPhoneBridge] {message}");
            MessageReceived?.Invoke(message);
            return fallback;
        }
    }

    private static AndroidJavaObject GetUnityActivity()
    {
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
    }
}

[Serializable]
public sealed class AndroidPhoneBridgeStatus
{
    public bool isSupported;
    public bool isAndroid;
    public bool hasBluetooth;
    public bool bluetoothEnabled;
    public bool hasBluetoothPermissions;
    public bool isDiscovering;
    public bool notificationListenerEnabled;
    public string adapterName;
    public string adapterAddress;
    public string message;
    public int sdkInt;

    public static AndroidPhoneBridgeStatus CreateUnsupported(string reason = "Android bridge is available only in Android player builds.")
    {
        return new AndroidPhoneBridgeStatus
        {
            isSupported = false,
            isAndroid = Application.platform == RuntimePlatform.Android,
            hasBluetooth = false,
            bluetoothEnabled = false,
            hasBluetoothPermissions = false,
            isDiscovering = false,
            notificationListenerEnabled = false,
            adapterName = string.Empty,
            adapterAddress = string.Empty,
            message = reason,
            sdkInt = 0
        };
    }
}

[Serializable]
public sealed class AndroidPhoneDeviceInfo
{
    public string name;
    public string address;
    public string type;
    public string bondStateName;
    public int bondState;
    public bool isBonded;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return string.IsNullOrWhiteSpace(address) ? "Unknown device" : address;
        }
    }
}

[Serializable]
public sealed class AndroidPhoneDeviceList
{
    public AndroidPhoneDeviceInfo[] bonded;
    public AndroidPhoneDeviceInfo[] discovered;
    public string message;

    public void Normalize()
    {
        bonded ??= Array.Empty<AndroidPhoneDeviceInfo>();
        discovered ??= Array.Empty<AndroidPhoneDeviceInfo>();
        message ??= string.Empty;
    }
}

[Serializable]
public sealed class AndroidPhonePairingResult
{
    public string name;
    public string address;
    public string message;
    public string bondStateName;
    public int bondState;
    public bool success;
    public bool isBonded;
}

[Serializable]
public sealed class AndroidPhoneNotificationEvent
{
    public string eventType;
    public string packageName;
    public string appLabel;
    public string title;
    public string text;
    public string subText;
    public string key;
    public long postTime;
    public long receivedAt;
}

public sealed class AndroidPhoneBridgeReceiver : MonoBehaviour
{
    public void OnAndroidPhoneBridgeState(string json)
    {
        AndroidPhoneBridge.HandleStatusJson(json);
    }

    public void OnAndroidPhoneBridgeDevices(string json)
    {
        AndroidPhoneBridge.HandleDevicesJson(json);
    }

    public void OnAndroidPhoneBridgeScanState(string state)
    {
        AndroidPhoneBridge.HandleScanState(state);
    }

    public void OnAndroidPhoneBridgePairingResult(string json)
    {
        AndroidPhoneBridge.HandlePairingResultJson(json);
    }

    public void OnAndroidPhoneBridgeNotification(string json)
    {
        AndroidPhoneBridge.HandleNotificationJson(json);
    }

    public void OnAndroidPhoneBridgeMessage(string message)
    {
        AndroidPhoneBridge.HandleMessage(message);
    }
}
