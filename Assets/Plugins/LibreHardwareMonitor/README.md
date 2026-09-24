# Performance sensor binaries

Source: [LibreHardwareMonitor v0.9.6](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/tag/v0.9.6), `LibreHardwareMonitor.zip`.

The five DLLs in this folder are the Windows .NET Framework 4.7.2 binaries from that release. They are used only for CPU and GPU load sensors. Physical memory and network throughput are sampled through Windows and .NET APIs in `PerformanceMonitorPanel.cs`.

LibreHardwareMonitor is licensed under MPL-2.0. See `LICENSE` and `THIRD-PARTY-NOTICES.txt` for the upstream notices. Hardware access may require elevated permissions on some machines; unavailable sensors are shown as N/A.
