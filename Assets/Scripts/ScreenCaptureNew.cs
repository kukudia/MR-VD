using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

/// <summary>
/// Streams the primary desktop display into a Unity UI texture through the native DesktopPlugin.
/// </summary>
public class ScreenCaptureNew : MonoBehaviour
{
    [DllImport("DesktopPlugin")]
    private static extern void InitCaptureResources(int width, int height);
    [DllImport("DesktopPlugin")]
    private static extern void ReleaseCaptureResources();
    [DllImport("DesktopPlugin")]
    private static extern bool PerformCapture(IntPtr buffer, int width, int height);

    public RawImage screenObject;

    private Texture2D screenTexture;
    private int screenWidth, screenHeight;
    private bool isInitialized = false;

    // Cache the native texture buffer on the main thread before passing it to the capture thread.
    private NativeArray<byte> textureNativeArray;
    private IntPtr textureDataPtr;

    private Thread captureThread;
    private volatile bool shouldStop = false;
    private volatile bool newDataReady = false;

    private void Start()
    {
        InitializeTexture();
        StartCaptureThread();
    }

    private void InitializeTexture()
    {
        screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
        screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

        screenTexture = new Texture2D(
            screenWidth,
            screenHeight,
            TextureFormat.BGRA32,
            mipChain: false,
            linear: false
        );

        if (!screenTexture.isReadable)
        {
            Debug.LogError("[ScreenCaptureNew] Texture is not readable. Cannot access raw texture data.");
            return;
        }

        screenTexture.filterMode = FilterMode.Bilinear;

        if (screenObject != null)
            screenObject.texture = screenTexture;

        textureNativeArray = screenTexture.GetRawTextureData<byte>();

        unsafe
        {
            textureDataPtr = (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(textureNativeArray);
        }

        InitCaptureResources(screenWidth, screenHeight);
        isInitialized = true;

        Debug.Log($"[ScreenCaptureNew] Capture initialized: {screenWidth}x{screenHeight}, ptr: {textureDataPtr}");
    }

    private void StartCaptureThread()
    {
        shouldStop = false;
        captureThread = new Thread(CaptureLoop)
        {
            Name = "ScreenCaptureThread",
            IsBackground = true
        };
        captureThread.Start();
    }

    /// <summary>
    /// Runs the native capture loop on a background thread. Do not call Unity APIs from this method.
    /// </summary>
    private void CaptureLoop()
    {
        while (!shouldStop)
        {
            if (!isInitialized || textureDataPtr == IntPtr.Zero)
            {
                Thread.Sleep(100);
                continue;
            }

            if (PerformCapture(textureDataPtr, screenWidth, screenHeight))
            {
                newDataReady = true;
            }
        }
    }

    private void Update()
    {
        if (!isInitialized) return;

        if (newDataReady)
        {
            screenTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            newDataReady = false;
        }

        if (Time.frameCount % 60 == 0)
        {
            CheckResolutionChange();
        }
    }

    private void CheckResolutionChange()
    {
        int currentWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
        int currentHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

        if (currentWidth != screenWidth || currentHeight != screenHeight)
        {
            Reinitialize();
        }
    }

    private void Reinitialize()
    {
        shouldStop = true;
        captureThread?.Join(2000);

        ReleaseCaptureResources();

        if (textureNativeArray.IsCreated)
        {
            textureNativeArray.Dispose();
        }

        Destroy(screenTexture);
        InitializeTexture();

        if (isInitialized)
        {
            shouldStop = false;
            StartCaptureThread();
        }
    }

    private void OnApplicationQuit()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        shouldStop = true;
        captureThread?.Join(2000);

        if (textureNativeArray.IsCreated)
        {
            textureNativeArray.Dispose();
        }

        ReleaseCaptureResources();
    }
}
