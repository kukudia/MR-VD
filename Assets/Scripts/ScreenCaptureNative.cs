using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

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

    void Start()
    {
        // 获取当前最前端的窗口
        IntPtr hWnd = GetForegroundWindow();

        // 显示窗口标题
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(hWnd, sb, 256);
        Debug.Log($"Capturing window: {sb}");

        if (StartCapture(hWnd))
        {
            Debug.Log("Capture Started Successfully");

            // 获取捕获尺寸
            GetCaptureSize(out int width, out int height);
            Debug.Log($"Capture size: {width}x{height}");

            if (width > 0 && height > 0)
            {
                // 创建纹理和缓冲区
                unityTexture = new Texture2D(width, height, TextureFormat.BGRA32, false);
                pixelBuffer = new Color32[width * height];
                bufferHandle = GCHandle.Alloc(pixelBuffer, GCHandleType.Pinned);

                if (screenObject != null)
                {
                    screenObject.texture = unityTexture;
                }

                isCapturing = true;
            }
            else
            {
                Debug.LogError("Invalid capture size");
            }
        }
        else
        {
            Debug.LogError("Failed to start capture");
        }
    }

    void Update()
    {
        if (!isCapturing || unityTexture == null) return;

        // 从原生代码复制帧数据
        if (CopyFrameToBuffer(bufferHandle.AddrOfPinnedObject(), unityTexture.width, unityTexture.height))
        {
            // 更新 Unity 纹理
            unityTexture.SetPixels32(pixelBuffer);
            unityTexture.Apply();
        }
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