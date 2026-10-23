# V3 分镜与实现依据

`src/v3/Reel.tsx` 管理时间线；每章一个源文件。`Design.tsx` 管理安全区、配色、字级和独立进度线；`Room.tsx` 是由帧号驱动的 ThreeCanvas 场景。

| 时间 | 源文件 | 内容 |
|---|---|---|
| 0–4s / 86–90s | Bookends.tsx | 片头与收尾 |
| 4–19s | MixedReality.tsx | 现实、虚拟、分层合成、视点 |
| 19–34s | Capture.tsx | GDI、BGRA、Alpha、纹理、鼠标对应位置 |
| 34–48s | Buffers.tsx | 捕获线程、三缓冲、主线程、资源释放 |
| 48–64s | AudioSpectrum.tsx | 4096 FFT、对数频带、左右声道柱条 |
| 64–74s | Beat.tsx | 能量、阈值、冷却、BPM |
| 74–86s | Information.tsx | 时间、IP 地点、天气、性能 |

## 技术依据与示意边界

- `Assets/Scenes/v203.0.0.unity` 使用 Passthrough Underlay。房间是解释现实环境的模型，不是房间扫描结果；合成图不声称启用真实物体深度遮挡。视点变化用模型相对视图的变化表示。
- 场景启用 `ScreenCaptureNew.cs`。`DesktopPlugin.dll` 可见 GDI 捕获及 `GetCursorInfo` / `DrawIcon` API 名称；插件源码未找到，内部具体顺序不作额外推断。仓库旧 `ScreenCapture.cs` 提供可读 GDI 示例。鼠标动画表示光标随帧纹理的相对位置映射，不表示头显射线回写 Windows 鼠标。
- `ScreenCaptureNew.cs` 使用三块非托管缓冲区。捕获线程写 Writing，FramesLock 保护指针交换，主线程 MemCpy 至 Unity 纹理并 Apply。纹理指针不暴露给生产线程。退出和分辨率变化有释放/重建流程。动画展示核心交接，不声称零拷贝。
- `AudioCaptureCSCore.cs` 使用混合及左右声道 Fft4096。当前场景 `AudioVisualizer` 为 64 根柱、40–4000 Hz、左右各 32 频带。视频使用程序化配乐的真实 4096 点 FFT，固定 48 kHz 和 Hann 窗，并以简化幅值缩放突出频带变化；不是 Unity 运行时性能或检测准确率测试。关键映射、平滑、对数压缩、动态范围和限高对应源码。
- `DetectBeatImproved` 使用频段历史均值/标准差、正向突增、置信度与冷却；BPM 另有候选评分/平滑。曲线为慢放判定示意，120 BPM 是示例，不是对所有音频的识别结果。
- `RuntimeInformationPanel.cs`：DateTime.Now、ipwho.is 经纬度或手动位置、Open-Meteo JSON 天气快照。IP 地点非精确 GPS。
- `PerformanceMonitorPanel.cs`：LibreHardwareMonitor CPU/GPU Load、GlobalMemoryStatusEx、网卡字节差与时间差。画面数值和曲线标注为演示，缺少传感器时显示 N/A。

## 素材与声音

V3 配乐由 `scripts/prepare-v3-audio.py` 通过确定性振荡器、包络和种子噪声生成，120 BPM，无录制歌曲、语音或外部采样。FFT 数据和音轨由同一脚本生成。画面所有模型、图形与数据示意为本项目程序化源文件，参考案例仅用于布局节奏。

V1/V2 历史 composition 依赖 `public/media` 的本地代理素材；它们不是 V3 的依赖。用户原录屏位于 `C:\Users\12046\Videos\Oculus`。

## 验证

V3 执行 TypeScript 检查、代表帧视觉检查和完整渲染。最终媒体解码结果记录在 `.showcase-work/v3-review/validation.json`。本次未修改 Unity 实现，也未运行 Unity Play Mode 或 Quest 设备测试。

最终媒体检查（2026-10-06）：2700 个视频帧完整解码；H.264 1920×1080 / 30 fps；AAC 48 kHz stereo，音频峰值 0.455。34 个时间点用于成片抽帧检查，修正后再次检查缓冲块、曲线及步骤切换。未进行完整人工听审。
