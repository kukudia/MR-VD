# Troubleshooting

## Unity Does Not Compile

- Confirm Unity `6000.3.0f1` is installed.
- Let Unity finish package restore and script compilation.
- Check that `Assets/Plugins/CSCore.dll` and `Assets/Plugins/System.Windows.Forms.dll` are present.
- Check that unsafe code remains enabled for desktop capture.

## No Audio Devices Appear

- Confirm the project is running on Windows.
- Use the runtime `Audio Capture` panel and press `Refresh`.
- Switch between `Input` and `Loopback`.
- Confirm the selected Windows audio device is active.
- If hotplug behavior looks stale, restart Play Mode.

## Stage Does Not React to Audio

- Confirm `AudioCaptureCSCore` exists in `Assets/Main.unity`.
- Confirm `AudioVisualizer` is present and receiving FFT data.
- Check the runtime audio panel for BPM, energy, and silence status.
- Confirm `StageManager.audioVisualizer` references the scene `AudioVisualizer`.
- Confirm `StageManager.runtime.enableSystem` is enabled.

## VFX Does Not Respond

- Confirm Visual Effect Graph assets are assigned on the stage VFX objects.
- Use `Tools > Stage > Complete Stage VFX Graphs` in the editor.
- Check `StageManager.vfx.enableVFX`.
- Confirm exposed VFX property names have not been renamed.

## Desktop Capture Is Blank

- Confirm the scene is running on Windows.
- Confirm `ScreenCaptureNew` is enabled and `screenObject` is assigned.
- Confirm `Assets/Plugins/DesktopPlugin.dll` is present.
- Check the Console for `[ScreenCaptureNew]` logs.
- Try disabling `ScreenCaptureNew` and enabling one of the legacy capture components only for comparison.

## Quest Build Problems

- Re-run Meta XR project setup checks.
- Confirm Android Build Support is installed.
- Confirm the Android manifest keeps the Quest VR category and supported device metadata.
- Test on hardware for hand tracking, controller tracking, and passthrough.

## Missing References

- Do not move or recreate scripts without preserving `.meta` files.
- If a MonoBehaviour loses its script reference, recover the original script GUID from version control.
- Avoid renaming serialized fields unless `[FormerlySerializedAs]` is used.

## XInput Cannot Instantiate the Dpad Layout (2026-10-01)

OpenXR 1.16.1 registers its XR `DPad` device under a name that collides with Input System's built-in `Dpad` control (layout names are case-insensitive). XInput then tries to create an XR device as a child control and reports `devices must be added at root`. Disabling the OpenXR D-Pad feature alone does not prevent Editor layout registration.

This project pins the official OpenXR 1.17.1 package, which registers the XR device as `XRDPad`. Meta XR and Input System versions are unchanged. Do not patch `Library/PackageCache` or replace built-in input controls with device layouts. Any custom XR action path `<DPad>` must migrate to `<XRDPad>`; no such project binding was found. Existing `Dpad` action composites should remain unchanged.

Regression checks should confirm that `InputSystem.LoadLayout("Dpad").type` is `DpadControl`, that `XRDPad` is the OpenXR device, and that XInput device creation and all four direction buttons survive OpenXR profile re-registration. Physical controller discovery and Quest behavior require separate runtime validation.

## dotnet Cannot Create a Stack Guard Page (2026-10-01)

During package recompilation, Windows reported virtual-memory exhaustion (System event 2004). The 7,657 MB paging file reached a 7,656 MB peak; available commit headroom fell below 1 GB despite several GB of free physical RAM. The affected dotnet process was Unity's Roslyn compiler, rather than an unrelated standalone build. This is a separate resource failure from the D-Pad layout collision.

Dismiss the system error dialog so Unity can report or finish the failed compilation. Avoid simultaneous external builds and Editor imports; save work before closing unused applications. If commit pressure persists, configure a system-managed or larger paging file with sufficient disk space, then retry Unity compilation. Changing paging-file settings may require administrator rights and a restart. Do not terminate the Editor or its compiler while it holds unsaved work.

## Desktop Capture Stability and Cost (2026-10-01)

`ScreenCaptureNew` publishes frames through three independent unmanaged buffers. The native worker owns capture/resources; the main thread copies only a completed frame into Unity-owned texture memory. GDI Alpha is forced to 255 before publication. Failed captures keep the last completed image, and retries sleep briefly. Disable/re-enable and resolution changes retire the old worker safely; a timed-out native call keeps its buffers until it actually exits.

Inspector: `Capture Frame Rate` defaults to 60 Hz (cap, not a guaranteed rate), `Generate Mipmaps` defaults off, and failure retry delay defaults to 8 ms. Mipmaps may help distant text but add regeneration cost. Restart the component after changing these startup settings. `CapturedFrames`, `UploadedFrames`, `FailedCaptures` and `IsCapturing` expose session diagnostics. Triple buffering adds two CPU frame buffers: about 51.2 MB at 3200×2000 versus a single shared CPU frame.

For game playback, compare capped game FPS and 1080p desktop resolution one change at a time. This implementation still uses the existing GDI plugin and CPU-to-GPU texture upload; it does not implement GPU texture sharing or guarantee that all fullscreen capture artifacts disappear.

Unity import and native desktop capture were exercised in Editor Play Mode at 3200×2000; an uploaded frame had zero non-opaque pixels and capture resumed after disable/re-enable. Worker termination and release of all three buffers were confirmed; the optional 12-level mipmap path uploaded 557 frames with zero capture failures in the sampled session and passed the same Alpha check. These counters are not frame-rate measurements. The standard generated-project build is blocked by the existing v4.7.1 target versus v4.7.2 hardware-monitor libraries. `dotnet build Assembly-CSharp.csproj --no-restore --nologo -p:TargetFrameworkVersion=v4.7.2` passed with 22 warnings. XR reported `ErrorFormFactorUnavailable`; real Quest input, game-time frame pacing and disappearance of the recorded artifact still require hardware validation.
