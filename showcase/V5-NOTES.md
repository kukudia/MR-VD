# V5 · 文案与版式修订

120 秒，1920×1080，30 fps。时间线沿用 V4，116–120 秒替换为独立实机演示标题卡。

## 修改

- 项目名称统一为“混合现实虚拟桌面”。精简 MR、FFT、节拍与信息采集章节的标题和字幕。
- 节拍图表缩窄，右侧预留注释区域；自适应阈值与冷却期说明不会被曲线和扫描线覆盖。
- 音频说明为“4096 个浮点采样”，另列“32 位浮点”和“示例采样率 48 kHz”。`AudioCaptureCSCore.cs` 使用 `ToSampleSource()`、`float[]` 与 `FftSize.Fft4096`。4096 是 FFT 窗口中的采样数量，不是位深或采样率。视频中的 48 kHz 为配乐分析参数，不表示所有设备都使用这一采样率。
- 删除片头、章节边界、步骤切换、MR 合成图、节拍标注和缓冲区释放的透明度渐变。直接切换状态，保留运动、曲线绘制与章节进度动画。固定透明色仍用于图解高亮。
- V5.1 恢复章节整体淡入淡出：章节内容用 14 帧淡入、11 帧淡出；片头与片尾用约 15 帧淡入淡出；底色保持连续，不产生黑场。图表内部动画不受这层过渡影响。
- MR 分层图直接替换房间画面，消除叠影；视点标记沿同一条二次贝塞尔曲线移动。
- 片尾新增“混合现实虚拟桌面 / 实机演示DEMO片段”。复用参考 HY-Sandbox 标题卡的微软雅黑、主标题 74 px / 600、副标题 30 px、间距 32 px、双轴居中布局；背景颜色取所给 JPEG 的主体颜色 `#f1f2ec`。不同文字的实际字形宽度自然不同。

## 文件与复现

- `src/v5/Reel.tsx`：120 秒主片。
- `src/v5/DemoTitleCard.tsx`：独立 4 秒标题卡，可导出 PNG 或 MP4。
- `src/v5/Beat.tsx`：曲线和旁侧注释。
- `src/v5/AudioSpectrum.tsx`：音频技术说明。
- V5 复用 V4 配乐与 FFT 数据，先运行 `npm run prepare:v3`、`npm run prepare:v4`；无需复制新一份音频。V4 源码与导出保留。

```powershell
npm run render
npm run demo:still
npm run demo:render
python scripts/validate-v5.py
```

实际交付文件位于 `C:\Users\12046\Videos`：`MR-VD-Showcase-v5.mp4`、`MR-VD-DEMO-Title-v5.png`、同名 JPG 与 4 秒 MP4。

## 验证范围

TypeScript 检查、代表帧检查、完整视频/音频解码及章节交界帧检查。媒体结果写入 `.showcase-work/v5-review/validation-v5.json`；逐帧检查额外检测意外空白画面。标题卡属于剪辑素材，不包含新的实机录屏。本次没有修改 Unity 源码或执行 Quest 设备测试。

原无淡化版本检查：3600 帧完整解码，H.264 / 1920×1080 / 30 fps，未检出空白帧；AAC / 48 kHz / 双声道，峰值 0.455。独立标题卡为 120 帧；PNG 副标题的深色字形边界与参考 JPEG 相同，均为 `(825,592)–(1095,620)`。已检查代表画面与章节交界抽帧，音频验证为解码与峰值检查，未进行完整人工听审。

## V5.1 淡入淡出交付

导出为 `C:\Users\12046\Videos\MR-VD-Showcase-v5-fade.mp4`，独立标题短片为 `MR-VD-DEMO-Title-v5-fade.mp4`。原无淡化视频与静态标题图片保留。静态标题导出命令固定取第 30 帧，避免取到淡入起始空白。

实际验证：TypeScript 通过；全片 3600 帧、音轨完整解码通过；8 段的固定标题区域亮度均呈单调淡入/淡出，过渡区外未检出空白帧。标题卡全显帧与 V5 PNG 像素完全一致，独立短片完整解码 120 帧。报告位于 `.showcase-work/v5-fade-review/validation-v5.json`。
