using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe; // 需要这个命名空间

public class ScreenCaptureNew : MonoBehaviour
{
    [DllImport("DesktopPlugin")]
    private static extern void InitCaptureResources(int width, int height);
    [DllImport("DesktopPlugin")]
    private static extern void ReleaseCaptureResources();
    [DllImport("DesktopPlugin")]
    private static extern bool PerformCapture(IntPtr buffer, int width, int height);

    public RawImage screenObject;
    public int targetFPS = 30;

    private Texture2D screenTexture;
    private int screenWidth, screenHeight;
    private bool isInitialized = false;

    // 🔑 关键：在主线程缓存 NativeArray 和指针
    private NativeArray<byte> textureNativeArray;
    private IntPtr textureDataPtr;

    // 多线程控制
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

        // ⚠️ 关键：Texture2D 必须设置 isReadable = true（第三个参数）
        // 否则 GetRawTextureData 会返回空或报错
        screenTexture = new Texture2D(
            screenWidth,
            screenHeight,
            TextureFormat.BGRA32,
            mipChain: false,
            linear: false // 或 true，根据你的色彩空间需求
        );

        // 确保 texture 是可读的（虽然 BGRA32 默认就是可读的）
        if (!screenTexture.isReadable)
        {
            Debug.LogError("Texture is not readable! Cannot use GetRawTextureData");
            return;
        }

        screenTexture.filterMode = FilterMode.Bilinear;

        if (screenObject != null)
            screenObject.texture = screenTexture;

        // 🔥 核心修复：在主线程获取 NativeArray 和指针
        textureNativeArray = screenTexture.GetRawTextureData<byte>();

        // 获取原生指针（Unsafe 操作，但仍在主线程执行）
        unsafe
        {
            textureDataPtr = (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(textureNativeArray);
        }

        // 初始化 C++ 资源
        InitCaptureResources(screenWidth, screenHeight);
        isInitialized = true;

        Debug.Log($"Capture initialized: {screenWidth}x{screenHeight}, ptr: {textureDataPtr}");
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
    /// 后台捕获循环 - ⚠️ 绝对不要调用任何 Unity API
    /// </summary>
    private void CaptureLoop()
    {
        int frameIntervalMs = 1000 / targetFPS;

        while (!shouldStop)
        {
            if (!isInitialized || textureDataPtr == IntPtr.Zero)
            {
                Thread.Sleep(100);
                continue;
            }

            // ✅ 安全：只调用纯 C++ DLL，传入之前缓存的指针
            // 这里没有任何 Unity API 调用！
            if (PerformCapture(textureDataPtr, screenWidth, screenHeight))
            {
                // 标记数据就绪，通知主线程 Apply
                newDataReady = true;
            }

            Thread.Sleep(frameIntervalMs);
        }
    }

    private void Update()
    {
        if (!isInitialized) return;

        // 主线程只负责 Apply（轻量操作）
        if (newDataReady)
        {
            // Apply 会通知 GPU 更新纹理数据
            screenTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            newDataReady = false;
        }

        // 可选：低频分辨率检测
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
        // 1. 停止线程
        shouldStop = true;
        captureThread?.Join(2000);

        // 2. 清理资源（主线程）
        ReleaseCaptureResources();

        // 3. 释放 NativeArray（如果需要）
        if (textureNativeArray.IsCreated)
        {
            textureNativeArray.Dispose();
        }

        // 4. 重建 Texture
        Destroy(screenTexture);
        InitializeTexture();

        // 5. 重启线程
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