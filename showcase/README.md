# MR-VD Showcase

当前交付为 `MRVDShowcaseV4`：120 秒、1920×1080、30 fps。移除角落标签和页码，将章节主题并入主标题；重新安排讲解节奏，细化坐标、频带和曲线动画。保留纯色背景、逐章进度线和三维 MR 场景。旧版源文件和视频保留。分镜见 [V4-NOTES.md](V4-NOTES.md)，技术依据见 [V3-NOTES.md](V3-NOTES.md)。

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

`npm run preview` 和 `npm run render` 默认指向 V4。V3/V4 不依赖用户录屏，配乐与 FFT 数据均通过准备脚本重建；需要 Python + NumPy 和 FFmpeg。执行两版 prepare 命令是为了让统一 Root 中的历史 composition 也能加载。V1/V2 依赖本地 `public/media` 代理文件，不属于 V4 的依赖。

本机渲染使用系统 Chrome 和单并发：

```powershell
.\node_modules\.bin\tsc.cmd --noEmit
node node_modules\@remotion\cli\remotion-cli.js render MRVDShowcaseV4 ../.showcase-work/MR-VD-Showcase-v4.mp4 --browser-executable='C:\Program Files\Google\Chrome\Application\chrome.exe' --concurrency=1
python scripts/validate-v4.py
```
