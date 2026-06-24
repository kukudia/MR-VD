using System;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Legacy GDI desktop capture implementation that writes the primary display into a RawImage texture.
/// </summary>
public class ScreenCapture : MonoBehaviour
{
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

    [DllImport("gdi32.dll")]
    public static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
                                       IntPtr lpvBits, ref BITMAPINFO lpbmi, uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    private const uint CURSOR_SHOWING = 0x00000001;
    private const int SRCCOPY = 0x00CC0020;

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

    [Tooltip("RawImage that displays the captured desktop texture.")]
    public RawImage screenObject;

    public bool expand;

    private Texture2D screenTexture;
    private IntPtr buffer;
    private int bufferSize;

    private void Start()
    {
        GetMonitors();

        int width = Forms.Screen.PrimaryScreen.Bounds.Width;
        int height = Forms.Screen.PrimaryScreen.Bounds.Height;
        screenTexture = new Texture2D(width, height, TextureFormat.RGBA32, true);
        ConfigureScreenTexture(screenTexture);

        if (screenObject != null)
        {
            screenObject.texture = screenTexture;
        }
    }

    private void Update()
    {
        CaptureScreen();
    }

    private void GetMonitors()
    {
        foreach (Forms.Screen screen in Forms.Screen.AllScreens)
        {
            Debug.Log($"[ScreenCapture] Display: {screen.DeviceName}");
            Debug.Log($"[ScreenCapture] Bounds: {screen.Bounds}, Primary: {screen.Primary}, WorkingArea: {screen.WorkingArea}");
        }
    }

    public Texture2D CaptureScreen()
    {
        IntPtr desktopHandle = GetDesktopWindow();
        IntPtr desktopDC = GetWindowDC(desktopHandle);
        IntPtr memoryDC = CreateCompatibleDC(desktopDC);
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;

        try
        {
            int width = Forms.Screen.PrimaryScreen.Bounds.Width;
            int height = Forms.Screen.PrimaryScreen.Bounds.Height;

            if (expand)
            {
                width = 1920;
                height = height * width / 1920;
            }

            hBitmap = CreateCompatibleBitmap(desktopDC, width, height);
            oldBitmap = SelectObject(memoryDC, hBitmap);

            if (!BitBlt(memoryDC, 0, 0, width, height, desktopDC, 0, 0, SRCCOPY))
            {
                Debug.LogWarning("[ScreenCapture] BitBlt failed.");
            }

            CURSORINFO cursorInfo = new CURSORINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(CURSORINFO))
            };

            if (GetCursorInfo(out cursorInfo) && (cursorInfo.flags & CURSOR_SHOWING) != 0)
            {
                DrawIcon(memoryDC, cursorInfo.ptScreenPos.x, cursorInfo.ptScreenPos.y, cursorInfo.hCursor);
            }

            BITMAPINFO bitmapInfo = new BITMAPINFO();
            bitmapInfo.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bitmapInfo.bmiHeader.biWidth = width;
            bitmapInfo.bmiHeader.biHeight = -height;
            bitmapInfo.bmiHeader.biPlanes = 1;
            bitmapInfo.bmiHeader.biBitCount = 32;
            bitmapInfo.bmiHeader.biCompression = 0;
            bitmapInfo.bmiHeader.biSizeImage = (uint)(width * height * 4);

            int bytes = width * height * 4;
            EnsureBuffer(bytes);

            int result = GetDIBits(memoryDC, hBitmap, 0, (uint)height, buffer, ref bitmapInfo, 0);
            if (result <= 0)
            {
                Debug.LogError("[ScreenCapture] GetDIBits failed.");
                return null;
            }

            byte[] pixelData = new byte[bytes];
            Marshal.Copy(buffer, pixelData, 0, bytes);

            for (int i = 0; i < pixelData.Length; i += 4)
            {
                byte blue = pixelData[i];
                pixelData[i] = pixelData[i + 2];
                pixelData[i + 2] = blue;
            }

            screenTexture.SetPixelData(pixelData, 0);
            screenTexture.Apply(updateMipmaps: true);
            return screenTexture;
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
            {
                SelectObject(memoryDC, oldBitmap);
            }

            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }

            if (memoryDC != IntPtr.Zero)
            {
                DeleteDC(memoryDC);
            }

            if (desktopDC != IntPtr.Zero)
            {
                ReleaseDC(desktopHandle, desktopDC);
            }
        }
    }

    private void EnsureBuffer(int bytes)
    {
        if (buffer != IntPtr.Zero && bufferSize == bytes)
        {
            return;
        }

        if (buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(buffer);
        }

        buffer = Marshal.AllocHGlobal(bytes);
        bufferSize = bytes;
    }

    private static void ConfigureScreenTexture(Texture2D texture)
    {
        texture.filterMode = FilterMode.Trilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.anisoLevel = 2;
    }

    private void OnDestroy()
    {
        if (buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(buffer);
            buffer = IntPtr.Zero;
        }
    }
}
