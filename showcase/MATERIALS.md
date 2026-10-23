# MR-VD Showcase 素材与验证

当前 V3 的素材、实现依据及复现说明以 README.md 和 V3-NOTES.md 为准。下文是 V1/V2 历史记录；其中临时录音没有完成无人声或版权属性验证，V3 已改用程序化生成音轨。

## V2 升级点

- 删除上一版的源码卡片和实录背景层。
- 使用纯色 `#f3f5f1` 背景与墨绿/薄荷绿主色，暖色只用于节拍和灯光强调。
- 使用官方 `@remotion/three` 的 `ThreeCanvas`，动画由 `useCurrentFrame()` 驱动；3D 曲线使用 Three.js `CatmullRomCurve3` 与 `TubeGeometry`。
- 每个 Part 各自计算局部进度并显示独立进度线，不使用全局进度条。
- 文字固定在左侧安全区，3D 图解固定在右侧画布，避免文字与模型互相遮挡。

本轮参考的 GitHub/API 来源：

- [Remotion v4.0.506 `ThreeCanvas`](https://github.com/remotion-dev/remotion/blob/v4.0.506/packages/three/src/ThreeCanvas.tsx)
- [Remotion `<ThreeCanvas>` 文档](https://github.com/remotion-dev/remotion/blob/v4.0.506/packages/docs/docs/three-canvas.mdx)
- [Remotion `spring()` 文档](https://github.com/remotion-dev/remotion/blob/v4.0.506/packages/docs/docs/spring.mdx)
- [Three.js `CatmullRomCurve3`](https://github.com/mrdoob/three.js/blob/r178/src/extras/curves/CatmullRomCurve3.js)

## 代理素材来源

原始录屏保留在 `C:\Users\12046\Videos\Oculus`，Remotion 只使用 `.showcase-work` 中通过 FFmpeg 生成的低码率代理：

| 代理 | 来源 | 截取区间 | 用途 |
|---|---|---:|---|
| `oculus-20260926-visualizer.mp4` | `com.oculus.vrshell-20260926-103410-0.mp4` | 68–78s | 初始频谱与屏幕界面 |
| `oculus-20261001-visualizer.mp4` | `com.oculus.vrshell-20261001-153359-0.mp4` | 96–114s | 音频分析与实时可视化 |
| `oculus-20261003-stage.mp4` | `com.oculus.vrshell-20261003-223641-0.mp4` | 228–245s | MR-VD 舞台/运行界面 |
| `oculus-20261001-panel.mp4` | `com.oculus.vrshell-20261001-153359-0.mp4` | 286–300s | 面板与设备状态 |
| `oculus-20260926-wheel.mp4` | `com.oculus.vrshell-20260926-103410-0.mp4` | 218–226s | 调性/颜色轮与收尾实录 |

临时音轨 `mr-vd-system-audio.m4a` 取自 `com.oculus.vrshell-20261001-153359-0.mp4` 的 96–186s，经过淡入淡出和音量降低处理。它用于保持无人声的技术展示节奏，后续可以替换为正式无版权音乐。

## 已确认与未确认

- 已确认：Remotion composition 为 90 秒、1920×1080、30fps；输出含 H.264 视频和 AAC 音轨。
- 已确认：画面中的 FFT4096、BPM、Rhythm、StageManager、18 灯光阵列和 Screen/Canvas 等标签来自当前 MR-VD 代码/项目约定。
- 仅作技术示意：代码卡片、灯光阵列图和信号流程图不是 Unity Editor 截图。
- 尚未验证：本次没有启动 Unity Editor、Play Mode、Meta Quest 设备连接或现场音频准确性验证。
- 第一轮缺口：正式背景音乐、更多纯净的 MR-VD 运行镜头、可选的 Unity Inspector/Prefab 近景。
