package com.mrvd.androidphone;

import android.app.Notification;
import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.os.SystemClock;
import android.service.notification.NotificationListenerService;
import android.service.notification.StatusBarNotification;

import org.json.JSONException;
import org.json.JSONObject;

public class AndroidNotificationListenerService extends NotificationListenerService {
    @Override
    public void onListenerConnected() {
        PhoneBridge.dispatchNotification(this, buildLifecycleJson("listener-connected"));
    }

    @Override
    public void onListenerDisconnected() {
        PhoneBridge.dispatchNotification(this, buildLifecycleJson("listener-disconnected"));
    }

    @Override
    public void onNotificationPosted(StatusBarNotification sbn) {
        PhoneBridge.dispatchNotification(this, buildNotificationJson(this, "posted", sbn));
    }

    @Override
    public void onNotificationRemoved(StatusBarNotification sbn) {
        PhoneBridge.dispatchNotification(this, buildNotificationJson(this, "removed", sbn));
    }

    private static String buildNotificationJson(Context context, String eventType, StatusBarNotification sbn) {
        JSONObject json = new JSONObject();
        try {
            Notification notification = sbn == null ? null : sbn.getNotification();
            Bundle extras = notification == null ? null : notification.extras;
            String packageName = sbn == null ? "" : sbn.getPackageName();

            json.put("eventType", eventType);
            json.put("packageName", packageName);
            json.put("appLabel", getAppLabel(context, packageName));
            json.put("title", getExtraText(extras, Notification.EXTRA_TITLE));
            json.put("text", getExtraText(extras, Notification.EXTRA_TEXT));
            json.put("subText", getExtraText(extras, Notification.EXTRA_SUB_TEXT));
            json.put("key", sbn == null ? "" : sbn.getKey());
            json.put("postTime", sbn == null ? 0L : sbn.getPostTime());
            json.put("receivedAt", System.currentTimeMillis());
        } catch (JSONException ignored) {
        }

        return json.toString();
    }

    private static String buildLifecycleJson(String eventType) {
        JSONObject json = new JSONObject();
        try {
            json.put("eventType", eventType);
            json.put("packageName", "");
            json.put("appLabel", "");
            json.put("title", "");
            json.put("text", "");
            json.put("subText", "");
            json.put("key", "");
            json.put("postTime", 0L);
            json.put("receivedAt", System.currentTimeMillis());
        } catch (JSONException ignored) {
        }

        return json.toString();
    }

    private static String getExtraText(Bundle extras, String key) {
        if (extras == null || key == null) {
            return "";
        }

        Object value = extras.get(key);
        return value == null ? "" : value.toString();
    }

    private static String getAppLabel(Context context, String packageName) {
        if (context == null || packageName == null || packageName.length() == 0) {
            return "";
        }

        PackageManager packageManager = context.getPackageManager();
        try {
            ApplicationInfo info = packageManager.getApplicationInfo(packageName, 0);
            CharSequence label = packageManager.getApplicationLabel(info);
            return label == null ? "" : label.toString();
        } catch (PackageManager.NameNotFoundException ex) {
            return "";
        }
    }
}
