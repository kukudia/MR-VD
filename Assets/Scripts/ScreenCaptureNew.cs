using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections.LowLevel.Unsafe;

/// <summary>Publishes complete, opaque desktop frames without sharing Unity texture memory with the capture thread.</summary>
public class ScreenCaptureNew : MonoBehaviour
{
    [DllImport("DesktopPlugin")] private static extern void InitCaptureResources(int width, int height);
    [DllImport("DesktopPlugin")] private static extern void ReleaseCaptureResources();
    [DllImport("DesktopPlugin")] private static extern bool PerformCapture(IntPtr buffer, int width, int height);

    public RawImage screenObject;
    [Header("Desktop Capture")]
    [SerializeField, Range(1, 60)] private int captureFrameRate = 60;
    [Tooltip("Enable only when the desktop is viewed at a distance; regenerating mipmaps increases upload cost.")]
    [SerializeField] private bool generateMipmaps;
    [SerializeField, Range(1, 8)] private int failedCaptureDelayMilliseconds = 8;

    // DesktopPlugin uses process-wide resources. A retiring session must release them before a new one starts.
    private static readonly object NativeSessionLock = new object();
    private CaptureSession session;
    private Texture2D screenTexture;
    private float nextResolutionCheck;
    private bool faultReported;

    public long CapturedFrames => session == null ? 0 : Interlocked.Read(ref session.Captured);
    public long FailedCaptures => session == null ? 0 : Interlocked.Read(ref session.Failed);
    public long UploadedFrames { get; private set; }
    public bool IsCapturing => session != null && session.Fault == null && session.Thread.IsAlive && !session.Stop;

    private sealed class CaptureSession
    {
        public readonly object FramesLock = new object();
        public readonly int Width, Height, Bytes, FrameRate, RetryDelay;
        public IntPtr Writing, Pending, Uploading;
        public bool Ready;
        public volatile bool Stop;
        public volatile string Fault;
        public long Captured, Failed;
        public Thread Thread;

        public CaptureSession(int width, int height, int frameRate, int retryDelay)
        {
            Width = width; Height = height; Bytes = checked(width * height * 4);
            FrameRate = frameRate; RetryDelay = retryDelay;
        }
    }

    private void OnEnable() => Initialize();

    private void Initialize()
    {
        if (session != null) return;
        try
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var next = new CaptureSession(bounds.Width, bounds.Height,
                Mathf.Clamp(captureFrameRate, 1, 60), Mathf.Clamp(failedCaptureDelayMilliseconds, 1, 8));
            screenTexture = new Texture2D(next.Width, next.Height, TextureFormat.BGRA32, generateMipmaps, false)
            {
                name = "Captured Desktop", wrapMode = TextureWrapMode.Clamp,
                filterMode = generateMipmaps ? FilterMode.Trilinear : FilterMode.Bilinear, anisoLevel = 1
            };
            if (screenObject != null) screenObject.texture = screenTexture;
            next.Thread = new Thread(() => CaptureLoop(next)) { Name = "ScreenCaptureThread", IsBackground = true };
            session = next;
            faultReported = false;
            UploadedFrames = 0;
            nextResolutionCheck = Time.unscaledTime + 1f;
            next.Thread.Start();
            UnityEngine.Debug.Log($"[ScreenCaptureNew] Capture initialized: {next.Width}x{next.Height}, {next.FrameRate} Hz, independent frame buffers.");
        }
        catch (Exception exception)
        {
            StopCapture();
            UnityEngine.Debug.LogError("[ScreenCaptureNew] Initialization failed: " + exception.Message);
        }
    }

    private static unsafe void CaptureLoop(CaptureSession capture)
    {
        try
        {
            lock (NativeSessionLock)
            {
                if (capture.Stop) return;
                bool nativeInitializationAttempted = false;
                try
                {
                    capture.Writing = Marshal.AllocHGlobal(capture.Bytes);
                    capture.Pending = Marshal.AllocHGlobal(capture.Bytes);
                    capture.Uploading = Marshal.AllocHGlobal(capture.Bytes);
                    nativeInitializationAttempted = true;
                    InitCaptureResources(capture.Width, capture.Height);
                    var clock = Stopwatch.StartNew();
                    double nextFrame = 0;
                    double interval = 1000.0 / capture.FrameRate;
                    while (!capture.Stop)
                    {
                        double remaining = nextFrame - clock.Elapsed.TotalMilliseconds;
                        if (remaining > 0)
                        {
                            Thread.Sleep(Math.Max(1, (int)Math.Ceiling(remaining)));
                            continue;
                        }
                        if (PerformCapture(capture.Writing, capture.Width, capture.Height))
                        {
                            // GDI's fourth byte is not a reliable Alpha channel. Desktop pixels are always opaque.
                            byte* pixels = (byte*)capture.Writing;
                            for (int i = 3; i < capture.Bytes; i += 4) pixels[i] = 255;
                            lock (capture.FramesLock)
                            {
                                IntPtr old = capture.Pending;
                                capture.Pending = capture.Writing;
                                capture.Writing = old;
                                capture.Ready = true;
                            }
                            Interlocked.Increment(ref capture.Captured);
                            nextFrame = clock.Elapsed.TotalMilliseconds + interval;
                        }
                        else
                        {
                            Interlocked.Increment(ref capture.Failed);
                            Thread.Sleep(capture.RetryDelay);
                        }
                    }
                }
                finally
                {
                    if (nativeInitializationAttempted) ReleaseCaptureResources();
                }
            }
        }
        catch (Exception exception) { capture.Fault = exception.ToString(); }
        finally
        {
            // The lock also protects a main-thread copy if the native plugin faults during capture.
            lock (capture.FramesLock)
            {
                FreeBuffer(ref capture.Writing);
                FreeBuffer(ref capture.Pending);
                FreeBuffer(ref capture.Uploading);
                capture.Ready = false;
            }
        }
    }

    private static void FreeBuffer(ref IntPtr buffer)
    {
        if (buffer == IntPtr.Zero) return;
        Marshal.FreeHGlobal(buffer);
        buffer = IntPtr.Zero;
    }

    private void Update()
    {
        var capture = session;
        if (capture == null) return;
        if (capture.Fault != null && !faultReported)
        {
            faultReported = true;
            UnityEngine.Debug.LogError("[ScreenCaptureNew] Capture stopped: " + capture.Fault);
        }
        bool copied = false;
        lock (capture.FramesLock)
        {
            if (capture.Ready && !capture.Stop)
            {
                IntPtr old = capture.Uploading;
                capture.Uploading = capture.Pending;
                capture.Pending = old;
                capture.Ready = false;
                // Copy only mip 0 into Unity-owned memory; Apply regenerates optional mipmaps.
                // The pointer is reacquired on the main thread and never exposed to the producer.
                unsafe
                {
                    var textureData = screenTexture.GetRawTextureData<byte>();
                    UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafePtr(textureData),
                        (void*)capture.Uploading, capture.Bytes);
                }
                copied = true;
            }
        }
        if (copied)
        {
            screenTexture.Apply(generateMipmaps, false);
            UploadedFrames++;
        }
        if (Time.unscaledTime < nextResolutionCheck) return;
        nextResolutionCheck = Time.unscaledTime + 1f;
        var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
        if (bounds.Width != capture.Width || bounds.Height != capture.Height)
        {
            StopCapture();
            Initialize();
        }
    }

    private void StopCapture()
    {
        var retiring = session;
        session = null;
        if (retiring != null)
        {
            retiring.Stop = true;
            if (retiring.Thread != null && retiring.Thread.IsAlive && !retiring.Thread.Join(2000))
                UnityEngine.Debug.LogWarning("[ScreenCaptureNew] Native capture is still returning; its worker retains and releases its own buffers safely.");
        }
        if (screenObject != null && screenObject.texture == screenTexture) screenObject.texture = null;
        if (screenTexture != null) Destroy(screenTexture);
        screenTexture = null;
    }

    private void OnDisable() => StopCapture();
    private void OnDestroy() => StopCapture();
    private void OnApplicationQuit() => StopCapture();
}
