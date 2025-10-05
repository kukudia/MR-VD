using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Windows.Forms;
using UnityEngine.UI; // C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Windows.Forms.dll

public class ScreenCapture : MonoBehaviour
{
    // 引入User32.dll中的方法 / Import methods from User32.dll
    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    public static extern bool GetCursorInfo(out CURSORINFO pci);

    [DllImport("user32.dll")]
    public static extern bool DrawIcon(IntPtr hDC, int x, int y, IntPtr hIcon);

    // 引入GDI32.dll中的方法 / Import methods from GDI32.dll
    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
                                     IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hDC);

    // 定义CURSORINFO结构体 / Define the CURSORINFO struct
    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    // 定义POINT结构体 / Define the POINT struct
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    // 定义CURSOR_SHOWING常量，用于判断光标是否可见 / Define CURSOR_SHOWING constant to check if the cursor is visible
    private const uint CURSOR_SHOWING = 0x00000001;


    // 定义必要的结构体 / Define necessary structs
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    public RawImage screenObject; // 需要将显示视频的3D对象拖到这里 / Drag the 3D object that displays the video here
    public float brightnessFactor = 0.2f; // 亮度调节因子 / Brightness adjustment factor

    private Texture2D screenTexture;
    //private Texture2D lastCapturedTexture; // 缓存上一次的纹理 / Cache the last captured texture
    private IntPtr buffer; // 缓存的像素数据缓冲区 / Cached pixel data buffer
    private int bufferSize; // 缓存的像素数据大小 / Cached pixel data size

    private void Start()
    {
        GetMonitors();

        // 创建一次纹理，后续直接复用 / Create the texture once and reuse it
        int width = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
        int height = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
        screenTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        screenObject.texture = screenTexture;
    }

    private void Update()
    {
        // 捕获屏幕并应用为材质纹理 / Capture the screen and apply it as texture
        Texture2D capturedTexture = CaptureScreen();
    }

    void GetMonitors()
    {
        // 获取所有屏幕的信息 / Retrieve information for all monitors
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            Debug.Log($"[ScreenCapture] Display: {screen.DeviceName} ");
            Debug.Log($"[ScreenCapture] Bounds: {screen.Bounds}, Primary: {screen.Primary}, WorkingArea: {screen.WorkingArea}");
        }
    }

    public Texture2D CaptureScreen()
    {
        // Debug.Log("[ScreenCapture] Starting screen capture.");

        // 获取桌面窗口句柄和DC / Get the desktop window handle and device context
        IntPtr desktopHandle = GetDesktopWindow();
        IntPtr desktopDC = GetWindowDC(desktopHandle);
        IntPtr memoryDC = CreateCompatibleDC(desktopDC);

        // 使用 System.Windows.Forms.Screen 获取屏幕宽度和高度 / Get screen width and height using System.Windows.Forms.Screen
        int width = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
        int height = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

        // 创建位图对象 / Create a bitmap object
        IntPtr hBitmap = CreateCompatibleBitmap(desktopDC, width, height);
        IntPtr oldBitmap = SelectObject(memoryDC, hBitmap);

        // 将桌面屏幕的内容复制到内存DC中 / Copy the desktop screen content into the memory DC
        bool blitResult = BitBlt(memoryDC, 0, 0, width, height, desktopDC, 0, 0, 0x00CC0020); // SRCCOPY

        // 获取并绘制鼠标光标 / Retrieve and draw the mouse cursor
        CURSORINFO cursorInfo = new CURSORINFO();
        cursorInfo.cbSize = (uint)Marshal.SizeOf(typeof(CURSORINFO));

        if (GetCursorInfo(out cursorInfo) && (cursorInfo.flags & CURSOR_SHOWING) != 0)
        {
            // 绘制鼠标光标在内存DC中 / Draw the mouse cursor in the memory DC
            DrawIcon(memoryDC, cursorInfo.ptScreenPos.x, cursorInfo.ptScreenPos.y, cursorInfo.hCursor);
        }

        // 准备BITMAPINFO结构体 / Prepare the BITMAPINFO structure
        BITMAPINFO bmi = new BITMAPINFO();
        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
        bmi.bmiHeader.biWidth = width;
        bmi.bmiHeader.biHeight = -height; // 翻转Y轴 / Flip the Y-axis
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = 0; // BI_RGB
        bmi.bmiHeader.biSizeImage = (uint)(width * height * 4);

        // 计算需要的字节大小 / Calculate the required byte size
        int bytes = width * height * 4;

        // 如果缓冲区未分配或大小发生变化，则重新分配 / Reallocate buffer if not assigned or size changes
        if (buffer == IntPtr.Zero || bufferSize != bytes)
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer); // 释放旧的缓冲区 / Free the old buffer
            }
            buffer = Marshal.AllocHGlobal(bytes);
            bufferSize = bytes;
        }

        // 获取像素数据 / Retrieve the pixel data
        int result = GetDIBits(memoryDC, hBitmap, 0, (uint)height, buffer, ref bmi, 0);

        // 检查是否成功（GetDIBits 返回值大于 0 表示成功） / Check for success (GetDIBits returns a value greater than 0 if successful)
        if (result <= 0)
        {
            Debug.LogError("[ScreenCapture] GetDIBits failed.");
            return null;
        }

        // 释放GDI资源 / Release GDI resources
        SelectObject(memoryDC, oldBitmap);
        DeleteObject(hBitmap);
        DeleteDC(memoryDC);
        ReleaseDC(desktopHandle, desktopDC);

        // 复制像素数据到字节数组 / Copy pixel data to byte array
        byte[] pixelData = new byte[bytes];
        Marshal.Copy(buffer, pixelData, 0, bytes);

        // 反转颜色通道，从 BGRA 转换为 RGBA / Reverse color channels from BGRA to RGBA
        for (int i = 0; i < pixelData.Length; i += 4)
        {
            // 交换 B 和 R 通道 / Swap B and R channels
            byte temp = pixelData[i];     // B
            pixelData[i] = pixelData[i + 2]; // R
            pixelData[i + 2] = temp;       // B
        }

        // 创建或更新纹理 / Update the texture
        screenTexture.LoadRawTextureData(pixelData);
        screenTexture.Apply();
        return screenTexture;
    }

    private void OnDestroy()
    {
        // 销毁缓冲区 / Destroy buffer
        if (buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(buffer);
            buffer = IntPtr.Zero;
        }
    }

    // 引入GetDIBits方法
    [DllImport("gdi32.dll")]
    public static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, IntPtr lpvBits, ref BITMAPINFO lpbmi, uint uUsage);
}