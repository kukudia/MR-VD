# MR-VD Showcase

当前交付为 `MRVDShowcaseV5`：120 秒、1920×1080、30 fps。名称统一为“混合现实虚拟桌面”，阈值说明移至图表旁，澄清浮点采样数量、格式与采样率，移除画面淡入淡出。片尾与独立 composition 提供参考样式的 DEMO 标题卡。修改见 [V5-NOTES.md](V5-NOTES.md)，时间线见 [V4-NOTES.md](V4-NOTES.md)，技术依据见 [V3-NOTES.md](V3-NOTES.md)。

## 使用

```powershell
npm ci
python -m pip install numpy
npm run prepare:v3
npm run prepare:v4
npm run studio
npm run preview
npm run render
```

`npm run preview` 和 `npm run render` 默认指向 V5；`npm run demo:still` / `npm run demo:render` 导出独立标题卡。V3/V4/V5 不依赖用户录屏，配乐与 FFT 数据均通过准备脚本重建；需要 Python + NumPy 和 FFmpeg。V5 复用 V4 音频与分析数据。执行两版 prepare 命令是为了让统一 Root 中的历史 composition 也能加载。V1/V2 依赖本地 `public/media` 代理文件，不属于 V5 的依赖。

本机渲染使用系统 Chrome 和单并发：

```powershell
.\node_modules\.bin\tsc.cmd --noEmit
node node_modules\@remotion\cli\remotion-cli.js render MRVDShowcaseV5 ../.showcase-work/MR-VD-Showcase-v5.mp4 --browser-executable='C:\Program Files\Google\Chrome\Application\chrome.exe' --concurrency=1
python scripts/validate-v5.py
```
