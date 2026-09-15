# MR-VD Unity 工作约定

## 范围与版本

- 本仓库使用 Unity `6000.3.0f1`；先读取 `ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json`、`.editorconfig` 和 `.gitattributes`。
- 通用 Unity、资产和 Git 规则来自用户级 `AGENTS.md` 与 `~/.codex/skills/unity-editable-workflow/SKILL.md`；本文件只记录 MR-VD 的版本、目录和验证例外。
- 修改前后运行 `git status --short`，保留用户未提交改动。不要手工替换复杂 Scene/Prefab/`.asset` YAML；优先连接匹配 Editor 或执行可审查的 Editor 工具。

## 项目约束

- 保持现有 `Assets/` 目录、序列化字段、设备/Stage wiring、视觉化参数和 Meta XR 依赖；不因审计升级包或替换现有 Coplay Unity MCP 包。
- 可调频段、颜色、灯光、设备列表和舞台参数优先 `[SerializeField] private`、Prefab、材质实例或 ScriptableObject，让 Inspector 保持可读可改。
- 仅编辑已有 `.cs` 时不修改 `.meta`；新增/移动 Assets 时同步处理 Meta。遵循仓库 `.editorconfig`/`.gitattributes` 的 UTF-8 与 CRLF。

## 验证

- 静态：`dotnet build Assembly-CSharp.csproj --no-restore --nologo`、`git diff --check`；现有 `MSB3277` 警告单独记录。
- Unity 导入、Edit Mode、Play Mode、视觉/音频和设备连接属于独立验证层次。没有目标 Editor/设备连接时不能宣称运行或效果验证通过。
- Unity CLI Pipeline 仅在确认当前项目和包兼容后安装；不能把 Unity 6 Pipeline 规则套用于旧版项目。
