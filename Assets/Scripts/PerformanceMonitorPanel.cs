using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using LibreHardwareMonitor.Hardware;
#endif

public sealed class PerformanceMonitorPanel : MonoBehaviour
{
    [SerializeField, Range(0.25f, 5f)] private float sampleIntervalSeconds = 1f;
    [SerializeField] private Text cpuValue;
    [SerializeField] private Text gpuValue;
    [SerializeField] private Text memoryValue;
    [SerializeField] private Text networkValue;
    [SerializeField] private PerformanceHistoryGraphic cpuGraph;
    [SerializeField] private PerformanceHistoryGraphic gpuGraph;
    [SerializeField] private PerformanceHistoryGraphic memoryGraph;
    [SerializeField] private PerformanceHistoryGraphic networkGraph;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private Computer computer;
#endif
    private float nextSampleTime;
    private long previousReceived;
    private long previousSent;
    private DateTime previousNetworkTime;
    private string hardwareError;

    private void OnEnable()
    {
        nextSampleTime = 0f;
        previousNetworkTime = default(DateTime);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
            computer.Open();
        }
        catch (Exception exception)
        {
            hardwareError = exception.GetType().Name;
            Debug.LogWarning("[PerformanceMonitorPanel] Hardware sensors unavailable: " + exception.Message);
            computer?.Close();
            computer = null;
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        computer?.Close();
        computer = null;
#endif
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSampleTime)
        {
            return;
        }

        nextSampleTime = Time.unscaledTime + sampleIntervalSeconds;
        float cpu = float.NaN;
        float gpu = float.NaN;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (computer != null)
        {
            try
            {
                foreach (IHardware hardware in computer.Hardware)
                {
                    if (hardware.HardwareType != HardwareType.Cpu && hardware.HardwareType != HardwareType.GpuNvidia
                        && hardware.HardwareType != HardwareType.GpuAmd && hardware.HardwareType != HardwareType.GpuIntel)
                    {
                        continue;
                    }

                    hardware.Update();
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType != SensorType.Load || !sensor.Value.HasValue)
                        {
                            continue;
                        }

                        if (hardware.HardwareType == HardwareType.Cpu && sensor.Name == "CPU Total")
                        {
                            cpu = sensor.Value.Value;
                        }
                        else if (hardware.HardwareType != HardwareType.Cpu && sensor.Name == "GPU Core")
                        {
                            gpu = float.IsNaN(gpu) ? sensor.Value.Value : Mathf.Max(gpu, sensor.Value.Value);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                hardwareError = exception.GetType().Name;
                Debug.LogWarning("[PerformanceMonitorPanel] Sensor update failed: " + exception.Message);
                computer.Close();
                computer = null;
            }
        }
#endif

        float memory = ReadMemoryPercent(out string memoryDetail);
        float network = ReadNetworkMbps(out string networkDetail);
        SetPercent(cpuValue, cpuGraph, cpu, hardwareError);
        SetPercent(gpuValue, gpuGraph, gpu, hardwareError);
        memoryValue.text = float.IsNaN(memory) ? "N/A" : memory.ToString("0") + "%  " + memoryDetail;
        memoryGraph.AddSample(memory);
        networkValue.text = networkDetail;
        networkGraph.AddSample(network);
    }

    private static void SetPercent(Text label, PerformanceHistoryGraphic graph, float value, string unavailable)
    {
        label.text = float.IsNaN(value) ? (string.IsNullOrEmpty(unavailable) ? "N/A" : unavailable) : value.ToString("0") + "%";
        graph.AddSample(value);
    }

    private static float ReadMemoryPercent(out string detail)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        MemoryStatus status = new MemoryStatus { length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (GlobalMemoryStatusEx(ref status) && status.totalPhysical > 0)
        {
            double used = (status.totalPhysical - status.availablePhysical) / 1073741824.0;
            double total = status.totalPhysical / 1073741824.0;
            detail = used.ToString("0.0") + "/" + total.ToString("0.0") + " GB";
            return (float)(100.0 * used / total);
        }
#endif
        detail = "N/A";
        return float.NaN;
    }

    private float ReadNetworkMbps(out string detail)
    {
        try
        {
            long received = 0;
            long sent = 0;
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                IPv4InterfaceStatistics stats = adapter.GetIPv4Statistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }

            DateTime now = DateTime.UtcNow;
            double seconds = (now - previousNetworkTime).TotalSeconds;
            float down = seconds > 0 && received >= previousReceived ? (float)((received - previousReceived) * 8 / seconds / 1000000.0) : 0f;
            float up = seconds > 0 && sent >= previousSent ? (float)((sent - previousSent) * 8 / seconds / 1000000.0) : 0f;
            previousReceived = received;
            previousSent = sent;
            previousNetworkTime = now;
            detail = "D " + down.ToString("0.0") + "  U " + up.ToString("0.0") + " Mb/s";
            return down + up;
        }
        catch (Exception)
        {
            detail = "N/A";
            return float.NaN;
        }
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint length;
        public uint memoryLoad;
        public ulong totalPhysical;
        public ulong availablePhysical;
        public ulong totalPageFile;
        public ulong availablePageFile;
        public ulong totalVirtual;
        public ulong availableVirtual;
        public ulong availableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
#endif
}
