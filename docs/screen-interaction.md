# Screen 手柄交互

场景：`Assets/Scenes/v203.0.0.unity`。使用现有 Meta Interaction SDK 的左右手柄射线，Canvas 从 1×1 扩大到 1.6×1，覆盖 AudioPanel、InfoPanel 以及中央桌面。面板原来的位置、缩放、设备与舞台引用保持不变。

## 操作

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

`ScreenRayManipulator`：距离默认 0.25–4 米，且不会超过原射线长度；缩放默认相对场景初始大小的 0.35–3 倍，摇杆死区 0.2。速度、范围、射线、CanvasGroup 和提示文字均可在 Inspector 调整。运行时位置/大小不写入磁盘，下次启动按平台参数重新初始化。

## 编辑器工具

- `Tools/MR-VD/Setup Controller Ray Interaction`：保存当前场景的射线和操控引用，并按 UI 实际边界扩大 Canvas；保留各面板的尺寸与位置。反复执行不会重复添加组件/提示，已有操控参数和平台选项不会被重置。编辑面板范围后可重新执行。
- `Tools/MR-VD/Verify Screen Interaction`：Edit Mode 自动检查左右面板边角的 Meta 射线命中、按钮的 GraphicRaycaster 命中，以及模拟输入下的移动、缩放、单手所有权、UI 拖动互斥、死区、追踪丢失和释放。测试结束恢复场景参数。模拟输入只存在于 Editor 验证工具中。

## 本次验证（2026-09-22）

- 当前本地 Unity 6000.3.11f1 导入/编译通过，Editor 自动化探针通过；重复配置的场景文件相同，重新加载后再次通过探针。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo`：0 错误，5 个 MSB3277 依赖版本警告。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo`：0 错误，6 个 MSB3277 依赖版本警告（包含运行时工程）。当前本地包/Unity 升级尚未提交，警告数与旧版记录不同。
- 完整场景 Play Mode 尝试遇到 OpenXR `XR_ERROR_FORM_FACTOR_UNAVAILABLE`，随后 Meta SDK 初始化组件出现异常；未将此尝试判定为通过。
- 尚需连接头显验证左右手真实射线、A 回中、扳机拖拽、移动/缩放手感，以及 Quest / PC VR 的启动定位。未执行 Android 构建；现有 Windows 音频/桌面捕获功能的平台限制未在本次改动中调整。
