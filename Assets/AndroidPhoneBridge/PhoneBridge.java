package com.mrvd.androidphone;

import android.Manifest;
import android.app.Activity;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothManager;
import android.content.BroadcastReceiver;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;

import com.unity3d.player.UnityPlayer;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

public final class PhoneBridge {
    private static final int REQUEST_BLUETOOTH_PERMISSIONS = 5417;
    private static final String DEFAULT_UNITY_RECEIVER = "AndroidPhoneBridge";
    private static final Handler MAIN_HANDLER = new Handler(Looper.getMainLooper());
    private static final Map<String, BluetoothDevice> DISCOVERED_DEVICES = new LinkedHashMap<String, BluetoothDevice>();

    private static Activity activity;
    private static Context appContext;
    private static String unityReceiver = DEFAULT_UNITY_RECEIVER;
    private static BroadcastReceiver bluetoothReceiver;
    private static boolean receiverRegistered;

    private PhoneBridge() {
    }

    public static void initialize(Activity unityActivity, String receiverName) {
        activity = unityActivity;
        if (unityActivity != null) {
            appContext = unityActivity.getApplicationContext();
        }

        if (receiverName != null && receiverName.length() > 0) {
            unityReceiver = receiverName;
        }

        ensureBluetoothReceiver();
        sendState();
        sendDevices();
    }

    public static String getStateJson() {
        JSONObject json = new JSONObject();
        BluetoothAdapter adapter = getAdapter();

        try {
            boolean hasBluetooth = adapter != null;
            boolean hasPermissions = hasBluetoothPermissions();
            json.put("isSupported", appContext != null && hasBluetooth);
            json.put("isAndroid", true);
            json.put("hasBluetooth", hasBluetooth);
            json.put("hasBluetoothPermissions", hasPermissions);
            json.put("bluetoothEnabled", hasBluetooth && safeIsEnabled(adapter));
            json.put("isDiscovering", hasBluetooth && hasPermissions && safeIsDiscovering(adapter));
            json.put("notificationListenerEnabled", isNotificationListenerEnabled());
            json.put("adapterName", hasBluetooth && hasPermissions ? safeAdapterName(adapter) : "");
            json.put("adapterAddress", hasBluetooth && hasPermissions ? safeAdapterAddress(adapter) : "");
            json.put("sdkInt", Build.VERSION.SDK_INT);
            json.put("message", buildStatusMessage(hasBluetooth, hasPermissions, adapter));
        } catch (JSONException ignored) {
        }

        return json.toString();
    }

    public static String getDeviceListJson() {
        JSONObject json = new JSONObject();
        try {
            json.put("bonded", getBondedDeviceArray());
            json.put("discovered", getDiscoveredDeviceArray());
            json.put("message", "");
        } catch (JSONException ignored) {
        }

        return json.toString();
    }

    public static void requestBluetoothPermissions() {
        if (activity == null) {
            sendMessage("Cannot request Bluetooth permissions without an active Unity activity.");
            return;
        }

        final String[] permissions = getMissingBluetoothPermissions();
        if (permissions.length == 0) {
            sendMessage("Bluetooth permissions already granted.");
            sendState();
            return;
        }

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                    activity.requestPermissions(permissions, REQUEST_BLUETOOTH_PERMISSIONS);
                }
            }
        });
    }

    public static void requestEnableBluetooth() {
        final BluetoothAdapter adapter = getAdapter();
        if (adapter == null) {
            sendMessage("Bluetooth adapter is not available on this Android device.");
            return;
        }

        if (!hasBluetoothPermissions()) {
            requestBluetoothPermissions();
            return;
        }

        if (safeIsEnabled(adapter)) {
            sendMessage("Bluetooth is already enabled.");
            sendState();
            return;
        }

        if (activity == null) {
            openBluetoothSettings();
            return;
        }

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    Intent intent = new Intent(BluetoothAdapter.ACTION_REQUEST_ENABLE);
                    activity.startActivity(intent);
                } catch (Exception ex) {
                    sendMessage("Failed to open Bluetooth enable prompt: " + ex.getMessage());
                    openBluetoothSettings();
                }
            }
        });
    }

    public static void openBluetoothSettings() {
        openSettings(Settings.ACTION_BLUETOOTH_SETTINGS);
    }

    public static void openNotificationListenerSettings() {
        openSettings(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS);
    }

    public static void startDiscovery() {
        BluetoothAdapter adapter = getAdapter();
        if (adapter == null) {
            sendScanState("unsupported");
            sendMessage("Bluetooth adapter is not available.");
            return;
        }

        if (!hasBluetoothPermissions()) {
            sendScanState("missing-permission");
            requestBluetoothPermissions();
            return;
        }

        if (!safeIsEnabled(adapter)) {
            sendScanState("disabled");
            requestEnableBluetooth();
            return;
        }

        ensureBluetoothReceiver();
        DISCOVERED_DEVICES.clear();

        try {
            if (adapter.isDiscovering()) {
                adapter.cancelDiscovery();
            }

            boolean started = adapter.startDiscovery();
            sendScanState(started ? "started" : "failed");
            sendState();
            sendDevices();
        } catch (SecurityException ex) {
            sendScanState("missing-permission");
            sendMessage("Bluetooth scan permission denied: " + ex.getMessage());
            requestBluetoothPermissions();
        } catch (Exception ex) {
            sendScanState("failed");
            sendMessage("Failed to start Bluetooth scan: " + ex.getMessage());
        }
    }

    public static void cancelDiscovery() {
        BluetoothAdapter adapter = getAdapter();
        if (adapter == null || !hasBluetoothPermissions()) {
            sendScanState("idle");
            return;
        }

        try {
            if (adapter.isDiscovering()) {
                adapter.cancelDiscovery();
            }
            sendScanState("cancelled");
            sendState();
        } catch (SecurityException ex) {
            sendMessage("Bluetooth cancel discovery permission denied: " + ex.getMessage());
        }
    }

    public static void pairDevice(String address) {
        BluetoothAdapter adapter = getAdapter();
        if (adapter == null) {
            sendPairResult(null, false, "Bluetooth adapter is not available.");
            return;
        }

        if (!hasBluetoothPermissions()) {
            requestBluetoothPermissions();
            sendPairResult(null, false, "Bluetooth permissions are required before pairing.");
            return;
        }

        if (address == null || address.length() == 0) {
            sendPairResult(null, false, "No Bluetooth device address was provided.");
            return;
        }

        try {
            BluetoothDevice device = adapter.getRemoteDevice(address);
            int bondState = device.getBondState();
            if (bondState == BluetoothDevice.BOND_BONDED) {
                sendPairResult(device, true, "Device is already paired.");
                sendDevices();
                return;
            }

            if (bondState == BluetoothDevice.BOND_BONDING) {
                sendPairResult(device, true, "Pairing is already in progress.");
                return;
            }

            boolean started = device.createBond();
            sendPairResult(device, started, started ? "Pairing request started." : "Pairing request failed to start.");
        } catch (IllegalArgumentException ex) {
            sendPairResult(null, false, "Invalid Bluetooth address: " + address);
        } catch (SecurityException ex) {
            sendPairResult(null, false, "Bluetooth pairing permission denied: " + ex.getMessage());
            requestBluetoothPermissions();
        } catch (Exception ex) {
            sendPairResult(null, false, "Failed to pair Bluetooth device: " + ex.getMessage());
        }
    }

    public static void dispatchNotification(Context context, String json) {
        if (context != null && appContext == null) {
            appContext = context.getApplicationContext();
        }

        sendUnity("OnAndroidPhoneBridgeNotification", json == null ? "{}" : json);
        sendState();
    }

    public static boolean isNotificationListenerEnabled() {
        if (appContext == null) {
            return false;
        }

        String enabledListeners = Settings.Secure.getString(
            appContext.getContentResolver(),
            "enabled_notification_listeners");

        if (enabledListeners == null || enabledListeners.length() == 0) {
            return false;
        }

        ComponentName expected = new ComponentName(appContext, AndroidNotificationListenerService.class);
        String[] components = enabledListeners.split(":");
        for (String component : components) {
            ComponentName enabled = ComponentName.unflattenFromString(component);
            if (expected.equals(enabled)) {
                return true;
            }
        }

        return false;
    }

    private static void ensureBluetoothReceiver() {
        if (appContext == null || receiverRegistered) {
            return;
        }

        if (bluetoothReceiver == null) {
            bluetoothReceiver = new BroadcastReceiver() {
                @Override
                public void onReceive(Context context, Intent intent) {
                    handleBluetoothBroadcast(intent);
                }
            };
        }

        IntentFilter filter = new IntentFilter();
        filter.addAction(BluetoothAdapter.ACTION_DISCOVERY_STARTED);
        filter.addAction(BluetoothAdapter.ACTION_DISCOVERY_FINISHED);
        filter.addAction(BluetoothDevice.ACTION_FOUND);
        filter.addAction(BluetoothDevice.ACTION_BOND_STATE_CHANGED);

        try {
            appContext.registerReceiver(bluetoothReceiver, filter);
            receiverRegistered = true;
        } catch (Exception ex) {
            sendMessage("Failed to register Bluetooth receiver: " + ex.getMessage());
        }
    }

    private static void handleBluetoothBroadcast(Intent intent) {
        if (intent == null) {
            return;
        }

        String action = intent.getAction();
        if (BluetoothAdapter.ACTION_DISCOVERY_STARTED.equals(action)) {
            sendScanState("started");
            sendState();
            return;
        }

        if (BluetoothAdapter.ACTION_DISCOVERY_FINISHED.equals(action)) {
            sendScanState("finished");
            sendState();
            sendDevices();
            return;
        }

        if (BluetoothDevice.ACTION_FOUND.equals(action)) {
            BluetoothDevice device = getBluetoothDeviceExtra(intent);
            if (device != null) {
                String address = safeDeviceAddress(device);
                if (address.length() > 0) {
                    DISCOVERED_DEVICES.put(address, device);
                }
            }
            sendDevices();
            return;
        }

        if (BluetoothDevice.ACTION_BOND_STATE_CHANGED.equals(action)) {
            BluetoothDevice device = getBluetoothDeviceExtra(intent);
            int bondState = intent.getIntExtra(BluetoothDevice.EXTRA_BOND_STATE, BluetoothDevice.ERROR);
            boolean bonded = bondState == BluetoothDevice.BOND_BONDED;
            sendPairResult(device, bonded, bonded ? "Device paired." : bondStateName(bondState));
            sendState();
            sendDevices();
        }
    }

    private static BluetoothDevice getBluetoothDeviceExtra(Intent intent) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            return intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE, BluetoothDevice.class);
        }

        return intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE);
    }

    private static JSONArray getBondedDeviceArray() throws JSONException {
        JSONArray array = new JSONArray();
        BluetoothAdapter adapter = getAdapter();
        if (adapter == null || !hasBluetoothPermissions()) {
            return array;
        }

        try {
            Set<BluetoothDevice> bondedDevices = adapter.getBondedDevices();
            if (bondedDevices == null) {
                return array;
            }

            for (BluetoothDevice device : bondedDevices) {
                array.put(deviceToJson(device));
            }
        } catch (SecurityException ex) {
            sendMessage("Bluetooth bonded device permission denied: " + ex.getMessage());
        }

        return array;
    }

    private static JSONArray getDiscoveredDeviceArray() throws JSONException {
        JSONArray array = new JSONArray();
        for (BluetoothDevice device : DISCOVERED_DEVICES.values()) {
            array.put(deviceToJson(device));
        }

        return array;
    }

    private static JSONObject deviceToJson(BluetoothDevice device) throws JSONException {
        JSONObject json = new JSONObject();
        int bondState = safeBondState(device);
        json.put("name", safeDeviceName(device));
        json.put("address", safeDeviceAddress(device));
        json.put("type", deviceTypeName(device));
        json.put("bondState", bondState);
        json.put("bondStateName", bondStateName(bondState));
        json.put("isBonded", bondState == BluetoothDevice.BOND_BONDED);
        return json;
    }

    private static void sendPairResult(BluetoothDevice device, boolean success, String message) {
        JSONObject json = new JSONObject();
        try {
            int bondState = device == null ? BluetoothDevice.ERROR : safeBondState(device);
            json.put("name", device == null ? "" : safeDeviceName(device));
            json.put("address", device == null ? "" : safeDeviceAddress(device));
            json.put("success", success);
            json.put("message", message == null ? "" : message);
            json.put("bondState", bondState);
            json.put("bondStateName", bondStateName(bondState));
            json.put("isBonded", bondState == BluetoothDevice.BOND_BONDED);
        } catch (JSONException ignored) {
        }

        sendUnity("OnAndroidPhoneBridgePairingResult", json.toString());
    }

    private static BluetoothAdapter getAdapter() {
        if (appContext == null) {
            return null;
        }

        BluetoothManager manager = (BluetoothManager) appContext.getSystemService(Context.BLUETOOTH_SERVICE);
        return manager == null ? null : manager.getAdapter();
    }

    private static boolean hasBluetoothPermissions() {
        String[] missingPermissions = getMissingBluetoothPermissions();
        return missingPermissions.length == 0;
    }

    private static String[] getMissingBluetoothPermissions() {
        List<String> missing = new ArrayList<String>();
        if (appContext == null || Build.VERSION.SDK_INT < Build.VERSION_CODES.M) {
            return new String[0];
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            addIfMissing(missing, Manifest.permission.BLUETOOTH_SCAN);
            addIfMissing(missing, Manifest.permission.BLUETOOTH_CONNECT);
        } else {
            addIfMissing(missing, Manifest.permission.ACCESS_FINE_LOCATION);
        }

        return missing.toArray(new String[missing.size()]);
    }

    private static void addIfMissing(List<String> missing, String permission) {
        if (appContext.checkSelfPermission(permission) != PackageManager.PERMISSION_GRANTED) {
            missing.add(permission);
        }
    }

    private static void openSettings(final String action) {
        if (activity == null) {
            sendMessage("Cannot open Android settings without an active Unity activity.");
            return;
        }

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    activity.startActivity(new Intent(action));
                } catch (Exception ex) {
                    sendMessage("Failed to open Android settings: " + ex.getMessage());
                }
            }
        });
    }

    private static boolean safeIsEnabled(BluetoothAdapter adapter) {
        try {
            return adapter != null && adapter.isEnabled();
        } catch (SecurityException ex) {
            return false;
        }
    }

    private static boolean safeIsDiscovering(BluetoothAdapter adapter) {
        try {
            return adapter != null && adapter.isDiscovering();
        } catch (SecurityException ex) {
            return false;
        }
    }

    private static String safeAdapterName(BluetoothAdapter adapter) {
        try {
            String name = adapter.getName();
            return name == null ? "" : name;
        } catch (SecurityException ex) {
            return "";
        }
    }

    private static String safeAdapterAddress(BluetoothAdapter adapter) {
        try {
            String address = adapter.getAddress();
            return address == null ? "" : address;
        } catch (SecurityException ex) {
            return "";
        }
    }

    private static String safeDeviceName(BluetoothDevice device) {
        try {
            String name = device == null ? "" : device.getName();
            return name == null ? "" : name;
        } catch (SecurityException ex) {
            return "";
        }
    }

    private static String safeDeviceAddress(BluetoothDevice device) {
        try {
            String address = device == null ? "" : device.getAddress();
            return address == null ? "" : address;
        } catch (SecurityException ex) {
            return "";
        }
    }

    private static int safeBondState(BluetoothDevice device) {
        try {
            return device == null ? BluetoothDevice.ERROR : device.getBondState();
        } catch (SecurityException ex) {
            return BluetoothDevice.ERROR;
        }
    }

    private static String deviceTypeName(BluetoothDevice device) {
        if (device == null) {
            return "Unknown";
        }

        try {
            switch (device.getType()) {
                case BluetoothDevice.DEVICE_TYPE_CLASSIC:
                    return "Classic";
                case BluetoothDevice.DEVICE_TYPE_LE:
                    return "LE";
                case BluetoothDevice.DEVICE_TYPE_DUAL:
                    return "Dual";
                default:
                    return "Unknown";
            }
        } catch (SecurityException ex) {
            return "Unknown";
        }
    }

    private static String bondStateName(int bondState) {
        switch (bondState) {
            case BluetoothDevice.BOND_NONE:
                return "Not bonded";
            case BluetoothDevice.BOND_BONDING:
                return "Bonding";
            case BluetoothDevice.BOND_BONDED:
                return "Bonded";
            default:
                return "Unknown";
        }
    }

    private static String buildStatusMessage(boolean hasBluetooth, boolean hasPermissions, BluetoothAdapter adapter) {
        if (appContext == null) {
            return "Android bridge is not initialized.";
        }

        if (!hasBluetooth) {
            return "Bluetooth adapter is not available.";
        }

        if (!hasPermissions) {
            return "Bluetooth runtime permissions are missing.";
        }

        if (!safeIsEnabled(adapter)) {
            return "Bluetooth is disabled.";
        }

        if (!isNotificationListenerEnabled()) {
            return "Notification listener access is not enabled yet.";
        }

        return "Ready.";
    }

    private static void sendState() {
        sendUnity("OnAndroidPhoneBridgeState", getStateJson());
    }

    private static void sendDevices() {
        sendUnity("OnAndroidPhoneBridgeDevices", getDeviceListJson());
    }

    private static void sendScanState(String state) {
        sendUnity("OnAndroidPhoneBridgeScanState", state == null ? "" : state);
    }

    private static void sendMessage(String message) {
        sendUnity("OnAndroidPhoneBridgeMessage", message == null ? "" : message);
    }

    private static void sendUnity(final String method, final String payload) {
        MAIN_HANDLER.post(new Runnable() {
            @Override
            public void run() {
                try {
                    UnityPlayer.UnitySendMessage(unityReceiver, method, payload == null ? "" : payload);
                } catch (Exception ignored) {
                }
            }
        });
    }
}
