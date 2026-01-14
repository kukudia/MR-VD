using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

public class ScreenCaptureNew : MonoBehaviour
{
    // 引入 C++ DLL 函数
    [DllImport("DesktopPlugin")]
    private static extern void InitCaptureResources(int width, int height);

    [DllImport("DesktopPlugin")]
    private static extern void ReleaseCaptureResources();

    [DllImport("DesktopPlugin")]
    private static extern bool PerformCapture(IntPtr buffer, int width, int height);

    public RawImage screenObject;

    private Texture2D screenTexture;
    private int screenWidth;
    private int screenHeight;
    private bool isInitialized = false;

    private void Start()
    {
        InitializeTexture();
    }

    private void InitializeTexture()
    {
        // 获取主屏幕分辨率
        screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
        screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

        // 关键点：使用 BGRA32 格式
        // Windows GDI 原生输出就是 BGRA，这样我们就不用消耗 CPU 去交换 R 和 B 通道了
        // Key: Use BGRA32 format to match GDI native output, avoiding CPU channel swapping
        screenTexture = new Texture2D(screenWidth, screenHeight, TextureFormat.BGRA32, false);

        // 这里的 FilterMode 设为 Point 可能稍微快一点，但在 3D 空间显示建议 Bilinear
        screenTexture.filterMode = FilterMode.Bilinear;

        if (screenObject != null)
        {
            screenObject.texture = screenTexture;
        }

        // 初始化 C++ 侧的 GDI 资源
        InitCaptureResources(screenWidth, screenHeight);
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized) return;

        // 检查分辨率是否发生变化（可选）
        if (screenWidth != System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width ||
            screenHeight != System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height)
        {
            // 如果分辨率变了，重新初始化
            ReleaseCaptureResources();
            InitializeTexture();
        }

        CaptureAndApply();
    }

    private void CaptureAndApply()
    {
        // 获取纹理的原始原生数据指针
        // Get the native pointer to the texture data
        // 这比 C# 数组拷贝快得多，直接写入 Unity 引擎的内存
        var textureData = screenTexture.GetRawTextureData<byte>();

        // 我们需要 Unsafe 指针传递给 DLL
        // 注意：GetRawTextureData 返回的是 NativeArray，我们需要它的指针
        unsafe
        {
            // NativeArray<T> 在 Unity 2018+ 可用
            // 获取 NativeArray 的指针
            void* ptr = Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafePtr(textureData);

            // 调用 C++ 填充数据
            if (PerformCapture((IntPtr)ptr, screenWidth, screenHeight))
            {
                // 上传数据到 GPU
                screenTexture.Apply();
            }
        }
    }

    private void OnDestroy()
    {
        // 清理 C++ 资源
        ReleaseCaptureResources();
    }
}