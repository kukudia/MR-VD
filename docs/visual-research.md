# 音乐空间视觉研究

本轮目标：在现有桌面、音频分析、舞台、星尘和流星之外，增加有统一配色、不同时间尺度和空间层次的可编辑图形内容。恢复点：`codex/visual-research-before-20261022`。保留起始 `.gitignore`、`ProjectSettings/ProjectSettings.asset` 和 `showcase/` 改动；只做本地提交。

## 资产规格

| 项目 | 本轮基线 |
| --- | --- |
| 引擎 | 当前工程 Unity 6000.3.11f1、URP 17.3；Windows PC XR，头显预算待实测 |
| 形状语言 | 极光光带、节拍光环、频谱历史地形；安静时留白，高潮时增层 |
| 颜色 | 青蓝、紫罗兰、琥珀、翡翠四种可编辑主题；一个主色、邻近色及少量高光 |
| 空间 | 米、Y-up；以 Screen 为局部坐标，主要位于两侧、下方和后方；中央阅读区屏蔽、近眼渐隐 |
| 几何 | 初始 6 条细分光带、最多 8 圈节拍环、48×48 频谱历史网格；不新增实时灯和碰撞 |
| 材质 | 原生 URP 非光照透明 Shader，程序化纹理，无外部位图；双眼实例化入口、深度测试及近眼渐隐 |
| 数据 | 复用 AudioVisualizer 的音频能量和真实瞬态；复用 StageManager 的只读输出；手动演示输入与真实音频明确区分 |
| 编辑 | ScriptableObject 主题、Prefab 参数、Editor 安装/修复、预览实验场和可重复渲染入口 |
| 源与许可 | 本项目 C# 网格生成器、Shader、参数资产即完整制作源；本轮原创，无外部下载素材 |
| 验收 | Unity 导入、Shader、资产重载、生命周期、瞬态去重、静音、几何边界、接管及后台真实帧；静态构建另记 |

## 工作单元

1. 原生可编辑光带、节拍环和频谱历史：独立实验场、主题资产、预览和主场景安装。
2. 现有 GPU 粒子的可选形态与过渡：保留原星尘默认，使用可调参数增加空间图案。
3. 视觉编排、主题切换与运行时控制：统一色彩、过渡与强度，避免效果同时抢占注意力。
4. 回归、真实音频回放、代表性画面和持续运行：只记录实际证据；头显缺失时明确留待设备验证。

每个单元完成后在下方记录入口、验证与本地提交。未通过导入、运行和效果检查的内容不作为完成交付。

## 2026-10-22 第一轮实现与实测

- 新增原生光带、瞬态环和频谱历史地形，四个主题、材质、Prefab、独立实验场均为真实 Unity 资产。光带包含细纤维、多尺度流动噪声、雾层和高光；环包含分段轨迹与扫描亮点；地形包含网格、等高线和节点。
- 原 GPU 星尘增加可选三臂星系、环面及连续过渡，默认仍为 Stardust；形态固定到场景锚点，保留相机移动补偿与近眼距离限制。空间主题可以同步粒子颜色。
- 主场景已接入现有 AudioVisualizer、StageManager 和 Screen；演示信号只在实验场启用。主题使用私有副本平滑过渡，不修改共享资产。
- 已执行 Unity C# 导入、Shader 检查、网格边界/复用、瞬态去重、重新启用、静音衰减、历史缓存和主题隔离验证；GPU 三种模式实际 dispatch/readback 的位置有限且位于配置范围内。
- 实验场桌面 Play Mode 运行约 142 秒，确认星系、环面及三层网格效果可见，运行后段无新增错误。修复 PreviewRenderUtility 中未启用对象不触发 OnDestroy 的资源泄漏；验证及主题预览前后网格数量为 80 → 80。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo` 基线因既有 PerformanceMonitorPanel 的 LibreHardwareMonitor/Computer 引用缺失失败：13 个 MSB3277 警告、2 个错误。不能将 Unity 导入成功等同于此命令通过。
- 无头显，XR 启动有 MetaXR ErrorFormFactorUnavailable。真实音乐听测、XR 双眼效果、设备性能、黑屏下后台帧推进尚未验证。NightDesk 当前处于用户接管状态，遵守接管，不自动恢复遮罩。

证据保存于项目 `Temp/AudioVisualResearch/`，不提交生成截图与机器日志。

## 使用与运行链路

入口为 `Tools/MR-VD/Spatial Audio`：生成缺少资产、安装/修复当前场景、升级实验场、生成主题联系图，以及执行网格与 GPU 验证。安装操作应在 Edit Mode 执行；现有主题和 GPU 默认模式会保留。实验场使用显式合成输入，不能用它证明真实音频分析准确。

```mermaid
flowchart LR
    A[AudioVisualizer energies and accepted onset] --> F[AudioVisualField.LateUpdate]
    S[StageManager.Presentation] --> F
    T[AudioVisualTheme asset] --> B[Private blended theme]
    F --> B
    F --> R[AudioAuroraRibbons.RenderFrame]
    F --> P[AudioBeatRings.RenderFrame]
    F --> H[AudioSpectrumTerrain.RenderFrame]
    F --> K[AudioHarmonicCanopy.RenderFrame]
    F --> L[AudioSpectralLattice.RenderFrame]
    B --> R
    B --> P
    B --> H
    B --> K
    B --> G[GpuAudioParticleVisualizer]
    F --> V[AudioVisualEffectsController.LateUpdate]
    S --> V
    V --> Q[Existing VFX exposed properties]
    G --> C[GpuAudioParticles.compute]
    R --> M[AudioVisualSurface shader]
    P --> M
    H --> M
    K --> M
    L --> M
```

源码入口：[AudioVisualField](../Assets/AudioVisuals/Runtime/AudioVisualField.cs)、[主题](../Assets/AudioVisuals/Runtime/AudioVisualTheme.cs)、[编辑器工具](../Assets/AudioVisuals/Editor/AudioVisualFieldTools.cs)、[验证](../Assets/AudioVisuals/Editor/AudioVisualValidation.cs)、[GPU](../Assets/AudioReactiveParticles/Runtime/GpuAudioParticleVisualizer.cs)。后续研究在此基础上逐个完整单元推进，不把视觉短语调制称为自动歌曲段落识别，也不把 synth 能量称为人声或语义检测。

## 第二轮：频谱形状与粒子微结构

入口：[AudioSpectralShape.Analyze](../Assets/AudioVisuals/Runtime/AudioSpectralShape.cs)。48 个对数频率区间先积分 FFT、压缩和平滑，再输出归一化的对数频率重心、分散度、平坦度和正向变化量。它们是外观描述器，重心不是以 Hz 表示的标准线性谱质心；正向变化使用平滑后的展示频谱，受更新率影响，不能用作精准起音估计。BPM、调性和已接受节拍事件保持原分析链。

重心影响光流，平坦度影响细丝扰动，正向变化影响微高光；主题 `spectralInfluence` 可独立调节。GPU `formationDetail` 增加稳定身份的五层细丝、稀疏外晕、编织环面、细尘与少量高亮节点，`spectralResponse` 调节描述器影响。默认星尘形态不使用这些形态细节。

验证：窄带低/高频、静态频谱、均匀频谱、静音/恢复和 NaN/Infinity 输入测试通过；三种 GPU 形态实际读回通过。桌面合成 Play 65 秒、3188 帧时粒子 ready，星系和环面截图已检查。启动仍有缺少头显的 MetaXR 错误；一次 CLI 序列化整个 Frame 时访问默认矩阵的 rotation 触发工具断言，后续只输出必要标量，不能将它算成视觉运行故障或忽略未验证项。本地提交第一轮为 `312d425`。

## 预研依据与下一阶段

2026-10-22 实际读取的公开资料：

- [librosa spectral.py](https://github.com/librosa/librosa/blob/main/librosa/feature/spectral.py)：以频谱分布统计描述声音形状。本轮采用更适合展示的对数区间变体；后续真实音频实验需独立量测其稳定性、频率分辨率与延迟。
- [Unity VFX Graph 17.3 Custom HLSL](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.3/manual/CustomHLSL-Common.html)：支持自定义 block/operator、纹理与缓冲输入，可将可复用场函数用于 GPU 粒子；文档明确梯度使用专门打包与采样函数，不能当成普通纹理契约猜测。

研究路线按证据推进：多速率特征（展示连续量与精确瞬态分开）、频段驱动的空间纤维、谐波/打击分解、确定性场与多尺度粒子、可编辑演出编排。神经音频嵌入和歌曲段落识别暂不引入运行依赖，先比较离线精度、许可证、推理延迟和 PC XR 预算；以上属于预研方向，未声称已实现。已有 VFX Graph 属性由 `AudioVisualEffectsController` 驱动，后续接入需先检查图的真实属性，避免虚设参数或重复写入。

## 第三轮：编织频谱穹顶与编辑工作台

[AudioHarmonicCanopy.RenderFrame](../Assets/AudioVisuals/Runtime/AudioHarmonicCanopy.cs) 复用 4830 顶点网格，15 条有独立相位与深度的频谱纤维围绕中央开口；各位置由展示频带驱动，细线中有移动亮点。名称仅描述几何形态，不声称检测出音乐谐波。线数、细分、半径、线宽、层距、编织圈数/幅度、频带位移、中心与种子均为 Inspector 参数，主题可调 canopy 权重。

`Tools/MR-VD/Spatial Audio/Open Visual Workshop` 打开[工作台](../Assets/AudioVisuals/Editor/AudioVisualWorkshop.cs)，可选主题、构图、强度、过渡、GPU 形态，查看描述器、生成合成预览、截图到项目 Temp，以及 Bake 当前网格为新 Prefab。Bake 保留网格、独立材质、冻结视觉时间和 SourceParameters.json，不覆盖源资产。静态快照没有活动 Screen 绑定，因此明确禁用其阅读屏幕遮罩；正式程序化效果继续保护阅读区。示例快照保存在 `Assets/AudioVisuals/Snapshots/`。

已用 Editor API 升级并重载原 Prefab、实验场和主场景，重复升级仍为一个 canopy。网格/Shader/资源清理测试通过，原有层数量保持完整；示例快照重载验证四层网格与 Style 0/1/2/3、冻结时间均持久化。桌面合成 Play 在 109.55 秒、5453 帧时穹顶正常显示；运行期间只有已知无头显 MetaXR 启动错误。工作台截图入口已实际写入 Temp 且查看画面，当前项目的 AndroidBluetooth 全局调试 IMGUI 也会出现在 backbuffer 截图中；纯相机主题预览不包含该界面。最终 15 条配置另经过几何边界与 Shader 检查。

## 第四轮：现有 VFX Graph 编排与隔离实验场

[AudioVisualEffectsController](../Assets/Scripts/AudioVisualEffectsController.cs) 在 StageManager 与 AudioVisualField 之后执行，读取频谱形状与私有混合主题。重心提高背景细尘发射，平坦度调制烟雾密度，正向变化与接受的瞬态短包络调制激光、地环。调制强度与调色混合可在 Inspector 独立调整。失去有效音频源、禁用效果或禁用控制器时将四图发射/强度归零；黑场同时归零。停止生成不保证立刻删除已有粒子。

实际图契约：BackgroundParticles 为 SpawnRate/ParticleColor，SmokeEffect 为 Density/SmokeColor/TransformPosition，LaserBeamEffect 为 BeamIntensity/BeamColor，GroundRingEffect 为 RingExpansion/RingColor。原主场景四图均不活动，保持该状态，只连接控制器数据源；不会把不可见图强行启用。通用参数仍采用 HasFloat/HasVector 检查。

`Tools/MR-VD/Spatial Audio/Build Missing VFX Graph Laboratory` 保存独立 `Assets/Scenes/AudioGraphLaboratory.unity`，使用原四图和显式合成输入；不会发射合成流星。`Validate Existing VFX Property Writes` 使用隔离组件验证四图属性存在、调制增量、主题一致与黑场归零。已通过 Unity 导入和该属性测试，实际 Play 观察到背景约 98 个、激光 12 个、地环 10 个活动粒子；烟雾计数 -1，不能据此确认其可见。实际停用控制器后四图强度读回为零。无头显启动错误仍存在。

视觉检查发现原激光/地环图呈现方形粒子，当前只验证编排通路，不能称其外观完成；下一单元针对 VFX 输出/材质和几何层次继续改进，并再次渲染。场景变更与原图源资产保持可分离。

## 第五轮：可复现 VFX 细节纹理与烟雾发射率

`Generate and Apply Detailed VFX Surfaces` 根据 `Textures/GraphTextureSettings.asset` 生成四张 256×256 RGBA 遮罩：带微光芒的柔星、四层扭曲噪声薄雾、脉冲细丝、分层轮廓轨迹。默认种子 8457；分辨率、晕光、雾尺度和纤维频率均可编辑。原始制作源为 [AudioGraphSurfaceTools](../Assets/AudioVisuals/Editor/AudioGraphSurfaceTools.cs) 和参数资产，未使用外部素材或引入第三方许可证；纹理为项目程序化原创资源。线性色彩、Clamp、Trilinear、mipmap、RGBA32，默认四张含 mip 的显存预算约 1.33 MiB，不含粒子和输出缓冲。

生成器通过 VFX Graph 17.3 自身的图模型和序列化 API 绑定输出 mainTexture：普通粒子用柔星/薄雾，Strip 用纤维/轮廓。包的图模型 API 为内部接口，因此反射仅限 Editor 工具，升级 VFX 包后必须重新验证；运行时不依赖该反射。保持现有节点、公开属性、GUID 与主场景激活状态。

检查真实图连接发现 SmokeEffect 的 `Density` 直接进入每秒发射数，而不是归一化密度。控制器新增 Inspector `smokeParticlesPerSecond`（默认 120），按归一化强度换算发射率；原 SmokeDensity 备用属性仍保持归一化契约。默认实验场运行 17.09 秒时烟雾实际存活 21 个、发射率约 3.246/秒，截图可见淡青色烟丝；不会将旧的 -1 计数记为成功。实际截图 `Temp/AudioVisualResearch/play-20261022-174835-446.png` 已查看，柔光点替代此前硬方块，整体层次仍需继续打磨。

重复生成与 Unity 导入通过，四图属性/频谱/调色/黑场验证通过，新增每秒发射率检查通过。重新读取四张 importer 确认线性、mip、Clamp/Trilinear 配置持久化。验证窗口控制台没有新增 error，Editor 无编译失败；不等同于设备、真实音频或 Player 性能验证。

## 第六轮：带传播时差的频谱星座

[AudioSpectralLattice.RenderFrame](../Assets/AudioVisuals/Runtime/AudioSpectralLattice.cs) 用三维三角截面细丝构成开口星座，默认 3 层、每层 20 节点、97 连接，每连接 8 段，共 2619 顶点。静态种子保持形态稳定，48 个展示频带改变节点局部径向位移；连接带有端点结、细节刻度与流动高光。最多保留 8 个接受的瞬态包络，沿开口弧形传播（默认 0.75 秒），不使用全屏同步闪烁，也不声称网络代表音乐相似性或检测到的和声。

Inspector 可调节点数、层数、连接细分、线半径、整体半径、高宽比、深度间距、频带位移、传播时长、中心和种子；主题 `lattice` 控制独立权重。组件使用既有近眼淡出和阅读遮罩；禁用清空传播记录，显式释放网格供编辑预览清理。工作台可升级缺少的 Prefab 组件，重复运行不增加重复层；Bake 的通用效果收集会保留该组件几何、Style 4 和源参数。已有四层快照保持原样。

实际验证：传播起点/远端时差、Clear 不重放、2619 顶点预算、有限边界和网格复用测试通过；全部既有网格/Shader/GPU 检查通过。四主题联系图已渲染查看。桌面合成 Play 49.28 秒、2441 帧，截图 `Temp/AudioVisualResearch/play-20261022-180331-375.png` 已查看；主场景重载检查 demo=false、星座数量为 1、绑定有效，AudioVisualizer/VirtualStage 引用保留。`dotnet build` 仍为基线 LibreHardwareMonitor/Computer 缺失的 2 错误、13 个 MSB3277 警告，不能记录为构建通过；Unity 编译与视觉运行验证独立成立。真实音频听感、头显和 Player 性能仍待验证。
## 第七轮：阅读空间的实际 GPU 像素检查

[AudioVisualPixelValidation](../Assets/AudioVisuals/Editor/AudioVisualPixelValidation.cs) 通过隔离预览相机真实渲染并读取像素。菜单 `Validate Reading Clearance Pixels` 覆盖全部 5 种生产表面样式、前后景、25 度旋转阅读平面、屏幕位于相机后方、0.5 米近眼淡出及 ±0.032 米独立单目视点。中央必须透明，外侧保留亮度；测试不修改用户场景，显式释放网格、材质、纹理和 RenderTexture。

`Validate GPU Particle Reading Pixels` 使用生产粒子 Shader 和单粒子 ComputeBuffer，通过实际 RenderPrimitives 提交与像素读回复现旧问题：屏幕前方的粒子仍出现在中央阅读射线上。修复取消交点必须落在视点与粒子之间的限制，使 GPU 与空间网格一样清除完整阅读射线；Shader 改用当前相机/眼睛位置，完整 Rect 边界替代居中 HalfSize 假设，保留非居中 Pivot。旧的独立相机中心 uniform 已删除，运行时仅更新新的内部 Shader 契约，不改场景序列化字段。

修复后 GPU 前后景、非居中矩形、旋转平面、左右独立视点像素检查通过；五种网格表面像素检查通过。报告写入 `Temp/AudioVisualResearch/reading-pixels.txt` 与 `gpu-reading-pixels.txt`。这包含真实 GPU 渲染证据，仍不是单通道立体头显、设备体验或 Player 性能认证；粒子按中心裁剪，跨越遮罩边缘的大粒子可能发生整体显隐，当前小粒子方案保持原行为。
## 第八轮：因果频谱层次预研

实际阅读 [librosa.decompose.hpss 源码](https://github.com/librosa/librosa/blob/main/librosa/decompose.py)（2026-10-22）：时间/频率中值和幂次软遮罩思想来源于其说明的 Fitzgerald 2010 方法。本项目独立实现 [AudioSpectralLayers.Analyze](../Assets/AudioVisuals/Runtime/AudioSpectralLayers.cs)，没有复制代码或引入 librosa 依赖；只处理 48 个已压缩、平滑的对数展示频带，不重建音频、不等同完整 HPSS、更不识别人声/乐器。因果 9 帧时间中值、7 频带局部中值、20 Hz 更新，不使用前视；启动覆盖约 0.45 秒，中值对变化存在历史滞后。帧卡顿不复制同一快照填满历史。

Frame 的 spectralLayers 为持续比例、宽带比例、对比分离权重、预热覆盖。两比例互补；持续宽带噪声保持 0.5/0.5 且对比为 0，避免强行分类。分离权重乘预热覆盖与主题 `layerResponse`，仅轻量调制极光表面高光/细流和穹顶载波速度，不改变原 BPM、调性、节拍接受链或整体亮度。工作台显示原始描述符，Bake 保存新 Shader 向量；设 layerResponse=0 可关闭调制。

最新程序集已确认包含新增方法与 Frame 字段后，稳定窄带、首次宽带、持续宽带模糊、静音复位、NaN/Infinity 清理、30/60/120 FPS 采样覆盖与 200 次稳定路径零托管分配测试通过。既有网格/GPU 与五样式阅读像素检查通过。算法初版 2000 次窄带更新微基准约 12.8 微秒/次（Editor 单次测量，不是 Player 保证）。真实音频分层可靠性尚未验证，不称为音频分析质量提升。

本轮出现 Bee 构建后端访问冲突及进程创建失败（系统提交余量约 0.3 GB，物理空闲约 6 GB）。保存 DAG 哈希/副本至 Temp 后请求 CleanBuildCache，清理重建仍受资源限制；最终通过官方 AssetDatabase API 将本项目 DesiredWorkerCount 从 3 临时收缩为 1，提交余量恢复约 3 GB，正常编译成功。未结束用户其他程序，未更改分页或全局系统设置。早期旧程序集测试不作为新增回归证据，恢复后已明确验证方法存在并重新运行。
恢复后实际合成 Play 至少 47.55 秒、2348 帧，分层向量持续更新，截图 Temp/AudioVisualResearch/play-20261022-182252-742.png 已检查。五层 Bake 重载验证 Style 4 与全部材质的 _SpectralLayers 覆盖值持久化通过；临时验证快照已由 AssetDatabase 删除，原示例快照保留。运行窗口仅记录已知无头显 MetaXR 启动错误。

## 第九轮：独立 HDR 展示场与可编辑 Look

`Build Missing Cinematic Showcase` 保存 `Assets/Scenes/AudioVisualShowcase.unity`、三个 Look/独立主题与原生 VolumeProfile。Aurora Cathedral 强调持续光流与穹顶，Solar Percussion 降低上层持续亮度、强调地环，Jade Orbit 强调环面与星座。Look 仅组合主题、强度、构图和 GPU 形态；在工作台选择并 Apply，或修改场景 `AudioVisualLookPlayer` 的显式引用，通过 Inspector 菜单应用。生成工具只补缺失资产，保留手工编辑。

检查发现正式 URP 管线 supportsHDR=false，相机 allowHDR 本身不足以启用 HDR。展示专用管线复制原 URP 资源并启用 HDR，不改原资源；场景 `AudioShowcasePipelineScope.OnEnable` 临时切换质量管线，OnDisable 在仍为自己所设管线时恢复之前值。实测 Play 中 HDR=true、退出后 HDR=false 且原 Quality override 为空。VolumeProfile 使用 Bloom（阈值 .25、强度 .65、散射 .6）、ACES 和曝光 +1.1；后期与原渲染器共享资源的设备性能尚未验证，不自动迁入正式场景。

`Capture Desktop Camera without Debug Overlay` 用 URP SingleCameraRequest 导出 1600×1000 PNG 到 Temp，包含相机后期与当前 GPU 粒子提交，不包含既有 AndroidBluetooth OnGUI。仅支持正在运行的单目桌面主相机；头显捕获独立。已实际查看 `camera-20261022-183228-651.png`、`camera-20261022-183305-002.png`、`camera-20261022-183350-895.png`。第一次启动即时请求有一次 CLI 5 秒超时，稍后正常完成，不能当成首个请求已生成图片。

实际合成 Play 至少 78.4 秒、3890 帧，三配置运行切换与明暗细节已查看。`Validate Authored Showcase Looks` 确认三独立主题/形态、渲染不改变主题 JSON、HDR/后期资产完整。编辑器编译通过。展示场为显式合成输入，画面不是实际歌曲或头显设备效果证明。
## 第十轮：GPU 可见度与舞台生命周期

AudioVisualField.Frame 增加显式 visibility，真实输入跟随 StageManager 的黑场值，合成实验场为 1。GPU 在执行顺序 130（空间场 120 之后）更新提交，绑定场的 opacity 乘可见度；绑定场禁用或缺少主题时为 0，未绑定场保持独立使用。黑场继续模拟，不销毁/重建缓冲，使恢复显现延续运动；不修改用户既有 opacity/渐变。

`Validate Running GPU Visibility` 在显式合成 Play 中检查真实运行组件的渲染提交 PropertyBlock：全黑、0.5 渐隐、恢复、场禁用/重启与无绑定状态，结束恢复引用与帧。检查通过。禁用后实际纯相机图 `camera-20261022-184030-295.png` 只剩背景，恢复后 `camera-20261022-184051-467.png` 重新显现，均已查看。启动即时 CLI 检查有一次 5 秒超时，之后正常执行；不把超时计为通过。Unity 编译与 diff 检查通过；没有真实 Stage/头显设备黑场体验认证。