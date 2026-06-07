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
