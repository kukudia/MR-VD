# MR-VD Showcase

当前交付为 `MRVDShowcaseV3`：90 秒、1920×1080、30 fps。浅色纯背景、逐章进度线、三维 MR 房间与可追踪的数据动画。没有灯光环绕或调性检测章节。前两版保留为历史 composition，原视频与备份留在本地。详细依据和分镜见 [V3-NOTES.md](V3-NOTES.md)。

## 使用

```powershell
npm ci
python -m pip install numpy
npm run prepare:v3
npm run studio
npm run preview
npm run render
```

`npm run preview` 和 `npm run render` 默认指向 V3。V3 不依赖用户录屏，配乐与 FFT 数据均通过准备脚本重建；需要 Python + NumPy 和 FFmpeg。V1/V2 依赖本地 `public/media` 代理文件，不属于 V3 的依赖。

本机渲染使用系统 Chrome 和单并发：

```powershell
.\node_modules\.bin\tsc.cmd --noEmit
node node_modules\@remotion\cli\remotion-cli.js render MRVDShowcaseV3 ../.showcase-work/MR-VD-Showcase-v3.mp4 --browser-executable='C:\Program Files\Google\Chrome\Application\chrome.exe' --concurrency=1
```
