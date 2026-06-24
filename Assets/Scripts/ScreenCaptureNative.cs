using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Captures the foreground native window through the NativeScreenCapture plugin.
/// </summary>
public class ScreenCaptureNative : MonoBehaviour
{
    [DllImport("NativeScreenCapture")]
    private static extern bool StartCapture(IntPtr hwnd);

    [DllImport("NativeScreenCapture")]
    private static extern bool CopyFrameToBuffer(IntPtr destBuffer, int destWidth, int destHeight);

    [DllImport("NativeScreenCapture")]
    private static extern void GetCaptureSize(out int width, out int height);

    [DllImport("NativeScreenCapture")]
    private static extern void StopCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    public RawImage screenObject;

    private Texture2D unityTexture;
    private bool isCapturing = false;
    private Color32[] pixelBuffer;
    private GCHandle bufferHandle;

    private void Start()
    {
        IntPtr hWnd = GetForegroundWindow();

        var titleBuilder = new System.Text.StringBuilder(256);
        GetWindowText(hWnd, titleBuilder, 256);
        Debug.Log($"[ScreenCaptureNative] Capturing window: {titleBuilder}");

        if (!StartCapture(hWnd))
        {
            Debug.LogError("[ScreenCaptureNative] Failed to start capture.");
            return;
        }

        Debug.Log("[ScreenCaptureNative] Capture started.");

        GetCaptureSize(out int width, out int height);
        Debug.Log($"[ScreenCaptureNative] Capture size: {width}x{height}");

        if (width <= 0 || height <= 0)
        {
            Debug.LogError("[ScreenCaptureNative] Invalid capture size.");
            return;
        }

        unityTexture = new Texture2D(width, height, TextureFormat.BGRA32, true);
        ConfigureScreenTexture(unityTexture);
        pixelBuffer = new Color32[width * height];
        bufferHandle = GCHandle.Alloc(pixelBuffer, GCHandleType.Pinned);

        if (screenObject != null)
        {
            screenObject.texture = unityTexture;
        }

        isCapturing = true;
    }

    private void Update()
    {
        if (!isCapturing || unityTexture == null)
        {
            return;
        }

        if (CopyFrameToBuffer(bufferHandle.AddrOfPinnedObject(), unityTexture.width, unityTexture.height))
        {
            unityTexture.SetPixels32(pixelBuffer);
            unityTexture.Apply(updateMipmaps: true);
        }
    }

    private static void ConfigureScreenTexture(Texture2D texture)
    {
        texture.filterMode = FilterMode.Trilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.anisoLevel = 2;
    }

    private void OnDestroy()
    {
        isCapturing = false;

        if (bufferHandle.IsAllocated)
        {
            bufferHandle.Free();
        }

        StopCapture();

        if (unityTexture != null)
        {
            Destroy(unityTexture);
        }
    }
}
