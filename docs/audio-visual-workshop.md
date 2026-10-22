# 音频视觉工作台

使用入口与当前可编辑资产。实现和逐轮实测记录见 [visual-research.md](visual-research.md)，模型预研见 [audio-visual-frontier.md](audio-visual-frontier.md)。

## 先看效果

1. 使用当前工程匹配的 Unity 6000.3.11f1，打开 `Assets/Scenes/AudioVisualShowcase.unity`，进入 Play。
2. 打开 `Tools/MR-VD/Spatial Audio/Open Visual Workshop`，点击 **Find in Active Scene**。
3. 在 Authored look 选择 `Assets/AudioVisuals/Looks/` 的配置并点击 **Apply Authored Look**。切换有平滑过渡，稍等数秒再评估。

展示场使用显式合成输入、独立 HDR/Bloom 配置和桌面侧雾，适合看造型；它不是 Windows 捕获或真实音乐准确率证明。Play 中调整对象参数在退出后通常会复原；保存主题资产的编辑则会保留。

| Look | 主要造型 | 优先调整 |
| --- | --- | --- |
| Aurora Cathedral | 光幕、编织穹顶、星系粒子 | 主题 motion/flowSpeed/canopy；编织线数、半径和层距 |
| Solar Percussion | 冲击环、起伏波前、频谱地形 | 主题 rings/ringSpectralRelief；环寿命、厚度、终点半径 |
| Jade Orbit | 翡翠晶格、编织粒子环面 | 主题 lattice；晶格节点/层数、传播时间 |
| Violet Knot | 紫色闭合流结 | GPU knotLobes/knotDepth、过渡、短光痕 |
| Opal Iris | 螺旋折面花瓣、肋纹与视角色彩 | 虹膜 aperture/reach/spiral/foldDepth；主题 Iris Optical Style |
| Amber Flow | 冷暖交织的三维卷流 | GPU curlScale/curlSpeed/curlConfinement |

所有造型都是可编辑艺术映射。名称不表示已经识别出音乐类型、音源、物理流体或歌段。

## 细节的编辑位置

- `Assets/AudioVisuals/Prefabs/SpatialAudioField.prefab`：光带、环、历史地形、穹顶、晶格和虹膜的组件参数。改场景实例会形成 Prefab override；需要全局复用时再明确 Apply。
- `Assets/AudioVisuals/Looks/* Theme.asset`：颜色、透明度、表面纤维、流速、地形轮廓、虹膜光学和各层权重。运行时用私有副本渐变，不反向写共享主题。
- 场景 GPU 组件：形态/锚点、粒子数、细节、稳定频带响应和 motionStretch。`spectralBandResponse=0` 关闭局部频带调制；没有有效绑定场时自动关闭。
- `Assets/AudioVisuals/Textures/GraphTextureSettings.asset` 与 **Generate and Apply Detailed VFX Surfaces**：固定种子生成星点、分形雾、丝束和轮廓纹理。该命令会重新生成对应纹理，手动绘制前先复制资产。
- `Assets/AudioVisuals/Graphs/ShowcaseMist.vfx`：独立展示雾，未提供阅读射线遮罩，不应直接搬进正式 XR 阅读区域。

建议先定一个主造型，再调整少量局部响应。频带、瞬态、慢主题承担不同时间尺度；提高每层亮度容易遮住结构细节。

## 原生配置与预算

工作台 Complexity profile 选择 `Assets/AudioVisuals/Complexity/` 的资产，仅在 Edit Mode 显式应用。操作只改数量，支持 Undo；变换、配色和音频参数继续独立编辑。

| 配置 | 六层网格顶点上限 | GPU 粒子 | 粒子状态缓冲 |
| --- | ---: | ---: | ---: |
| Desktop Detail | 16753 | 4096 | 192KiB |
| Reduced Geometry | 6990 | 2048 | 96KiB |

上限包含可选虹膜；未启用虹膜的主题可能不生成它。数字不是测得的头显帧率，也不含 VFX、纹理、常量、材质过绘制和渲染目标。**Preview Mesh Themes at This Complexity** 输出四种基础主题的隔离网格联系图，不含 GPU 粒子和后处理。

## 视觉序列和保存快照

`Looks/Five Look Journey.asset` 是可编辑的五段示例，不自动包含后来新增的 Amber Flow。可改每段 Look、时长、按秒/显示拍数计时和循环。展示场序列默认不自动开始，工作台提供播放、暂停、下一段和停止。

静音/源禁用可暂停序列时钟，停止恢复起始 Look；恢复会重新应用该 Look，不能当作每个临时手调参数的完整快照。按拍计时是 BPM 积分，不是歌曲结构识别。

**Bake Current Meshes into New Editable Prefab** 会创建新的 `Assets/AudioVisuals/Snapshots/Snapshot-*`：网格、材质、Prefab 和源参数 JSON。保留程序化来源，不替换它。Bake 保存的是静态网格时刻，不包含 GPU/VFX 模拟；静态快照没有动态阅读屏绑定。

## 真实输入和实验开关

正式场景 `Assets/Scenes/v203.0.0.unity` 连接原 AudioVisualizer、StageManager 与 Screen，demonstration 关闭。不要把展示场的演示参数复制成正式音频源。

主题 `aggregateEnergyWeight` 默认 0，是可选整体视觉能量实验；提高后按原始 FFT 平方和响应，gain 可调。它不改变已接受瞬态、BPM/调性或 bass/detail，不是 LUFS 或自动增益控制。尚无代表性歌曲试听证据，现有预设保持关闭。

原阅读遮罩和近眼淡出对动态网格/GPU 粒子有效；设备上的双眼遮挡、闪烁、舒适度与帧率需要头显实测。

## 验证与截图

`Tools/MR-VD/Spatial Audio` 提供网格/GPU、表面像素、复杂度、Look 和运行中序列验证；使用前注意菜单要求的 Edit/Play 状态。**Start Twelve Minute Six Look Soak** 显式执行两轮六 Look，每套一分钟；第一轮暖机可选网格，第二轮比较数量，序列需先停止。结果为 Editor 观察，不是 Player GPU 基准。

纯相机截图使用 **Capture Desktop Camera without Debug Overlay**，要求桌面 Play、主相机且不是 stereo；包含 URP 后处理和 GPU，保存在 `Temp/AudioVisualResearch/`。截图、CSV 和机器日志未提交，不依赖它们作为生产资产。

当前 `dotnet build Assembly-CSharp.csproj --no-restore --nologo` 有既有 LibreHardwareMonitor 缺失：2 错误、13 个 MSB3277 警告。Unity 导入和实测结果需单独看研究记录；真实音乐听测、头显与 Player GPU 性能未认证。
