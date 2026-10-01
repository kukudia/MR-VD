# Screen 手柄交互

场景：`Assets/Scenes/v203.0.0.unity`。使用现有 Meta Interaction SDK 的左右手柄射线，Canvas 从 1×1 扩大到 1.6×1，覆盖 AudioPanel、InfoPanel 以及中央桌面。面板原来的位置、缩放、设备与舞台引用保持不变。

## 操作

- PC 鼠标：直接点击 AudioPanel、InfoPanel 的按钮、滑块等 Canvas UI。`InputSystemUIInputModule` 与 Meta 手柄射线共用 EventSystem；手柄处理后恢复 Canvas 的桌面事件相机。
- 扳机：原有 UI 悬停、按钮点击、滑块和滚动区域拖动。
- 指向 Screen / 左右面板，再按住侧握键：移动整个 Screen；屏幕保持当前朝向，避免手腕转动使桌面倾斜。
- 保持侧握，摇杆上 / 下：推远 / 拉近；左 / 右：缩小 / 放大。以抓取点缩放，保持宽高比。
- 松开侧握：固定当前位置，关闭相机跟随，避免松手后弹回。
- 右手 A 或键盘 R：按当前平台回中，保留用户调整的大小。

操作期间只允许一只手柄拥有 Screen；正在进行 UI 扳机拖动时不会开始移动 Screen。移动时暂停 Canvas 的 UI 命中和左右手柄的玩家移动事件转发，避免误点或摇杆同时移动视角。追踪丢失、应用失焦/暂停、禁用组件或回中都会释放控制并恢复 UI 及之前的移动启用状态；重新抓取需要先松开侧握。

## Inspector 参数

`ScreenPositionController`：

- Platform：Automatic 自动识别；Desktop / Headset 可用于强制预览。Android 及已激活的 PC VR / Quest Link 使用 Headset，其余使用 Desktop。
- Apply Platform Position On Start：本场景 Screen 已启用；不会更改复用此脚本的 Cube 的启动选项。
- Desktop Distance / Height Offset / Follow Offset：桌面默认前方 1.5 米，高度偏移 0。
- 原有 Recenter Distance / Height Offset / Position Offset：继续作为头显参数，保留场景中的 0.5 米回中距离及现有跟随偏移。
- Startup Tracking Wait：默认等待最多 3 秒以获得有效头部追踪；桌面先显示，若初始化窗口内 PC VR 激活则重新按头显参数放置。手动移动或回中会取消启动重定位。

`ScreenRayManipulator`：距离默认 0.25–4 米，且不会超过原射线长度；缩放默认相对场景初始大小的 0.35–3 倍，摇杆死区 0.2。速度、范围、射线、CanvasGroup 和指针平滑时间均可在 Inspector 调整。握住侧键时亮起由四条 UI Image 组成的屏幕轮廓；操作提示文字已移除。运行时位置/大小不写入磁盘，下次启动按平台参数重新初始化。`RayCursorSmoother` 平滑光标与射线的显示，UI 命中仍使用 Meta 原始射线。

## 编辑器工具

- `Tools/MR-VD/Setup Controller Ray Interaction`：保存当前场景的射线和操控引用，并按 UI 实际边界扩大 Canvas；保留各面板的尺寸与位置。反复执行不会重复添加组件或恢复已删除的提示文字，已有操控参数和平台选项不会被重置。编辑面板范围后可重新执行。
- `Tools/MR-VD/Apply Quest Visual and UI Tuneup`：重新生成手部、Quest 3 手柄和操控轮廓材质，连接平滑光标，调整设置按钮命中范围与音频面板刷新参数；可重复执行。
- `Tools/MR-VD/Verify Screen Interaction`：Edit Mode 自动检查左右面板边角的 Meta 射线命中、按钮的 GraphicRaycaster 命中，以及模拟输入下的移动、缩放、单手所有权、UI 拖动互斥、死区、追踪丢失和释放。测试结束恢复场景参数。模拟输入只存在于 Editor 验证工具中。

## 本次验证（2026-09-22）

- 当前本地 Unity 6000.3.11f1 导入/编译通过，Editor 自动化探针通过；重复配置的场景文件相同，重新加载后再次通过探针。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo`：0 错误，5 个 MSB3277 依赖版本警告。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo`：0 错误，6 个 MSB3277 依赖版本警告（包含运行时工程）。当前本地包/Unity 升级尚未提交，警告数与旧版记录不同。
- 完整场景 Play Mode 尝试遇到 OpenXR `XR_ERROR_FORM_FACTOR_UNAVAILABLE`，随后 Meta SDK 初始化组件出现异常；未将此尝试判定为通过。
- 尚需连接头显验证左右手真实射线、A 回中、扳机拖拽、移动/缩放手感，以及 Quest / PC VR 的启动定位。未执行 Android 构建；现有 Windows 音频/桌面捕获功能的平台限制未在本次改动中调整。

## 2026-09-26 调整与验证

- 视频对应的虚拟手颗粒轮廓替换为不透明 URP Lit 材质，覆盖 BuildingBlock 手网格及 Interaction SDK 的真实手显示网格；系统手势材质同步替换。Quest 3 手柄模型使用保留原纹理并带有轻微自发光的 URP Lit 材质，避免场景照明不足时整片发黑。
- Audio Analysis 与 Performance 各自切换；关闭 Audio Analysis 仅关闭分析页，AudioVisualizer 保持启用。Play Mode 已验证两个选项可同时开启、关闭分析页不关闭可视化。
- 设置面板四行按钮改为 28 单位高，面板展开高度 172；展开设置时收起 Weather 与 System 为选项让出空间。Play Mode 几何检查确认第 4 行位于设置面板内，页脚仍在 Dashboard 范围内。
- Unity 6000.3.11f1 导入与编译通过；标准 dotnet 命令受生成的 v4.7.1 目标与监控库 v4.7.2 不匹配影响，使用 `-p:TargetFrameworkVersion=v4.7.2` 后编译通过（21 条依赖警告）。Quest 3 已通过 ADB 识别，但本次 Editor Play Mode 的 OpenXR 报 `ErrorFormFactorUnavailable`，没有完成头显内视觉与手柄操作验证。

### 2026-09-26 截图后修正

- 用户截图显示手和轮廓呈洋红色、轮廓悬在桌面上方。手和 Quest 3 手柄材质改用场景已经使用的 `Universal Render Pipeline/Lit`。轮廓最终改为 World Space Canvas 内的四条 UI Image，按实际桌面及两侧信息面板的外接边界排布，取代旧 LineRenderer 和其专用材质。
- Edit Mode 检查：两侧 BuildingBlock 手、12 个 Interaction SDK 手显示网格及系统手势引用均指向 `VirtualHandOpaque`。Play Mode 中手与 Quest 3 手柄渲染器所用 URP/Lit Shader 均报告 supported，手柄纹理引用存在；手部在无头显的 Editor 里无法确认真实外观。Play Mode 强制开启轮廓后的画面确认青色边框贴合三块可见面板，实际握柄切换和头显效果仍待设备验证。

## 2026-10-01 手部指针显示

两侧 HandRayInteractor 增加 `HandRayPointerVisibility`：普通手追踪保持运行，但光束、光标和捏合箭头仅在食指捏合强度达到 0.65 或正在 Select 时可见；松开后保留 0.18 秒反馈，追踪无效或失焦时隐藏。控制器射线不受此策略影响。阈值、松开宽限和目标渲染器都在 Inspector 可编辑。该策略只控制视觉，不禁用 SDK 命中检测和捏合操作；手部真实交互需要连接 Quest 验证。

`Tools/MR-VD/Apply Desktop Capture and Reading Comfort` 可重新配置当前 v203.0.0 场景的指针与柔和粒子预设，并通过 Unity API 保存。它不会改写手追踪 SDK Prefab 或包文件。

保存并重新加载场景后，两侧指针组件和配置引用仍在。Editor 无头显时手部 Rig 不活跃，通过直接调用其生命周期/视觉更新验证了追踪无效时目标渲染器隐藏；未验证真实捏合阈值或 SDK 活跃时序。
