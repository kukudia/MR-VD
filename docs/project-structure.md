# Project Structure

## Repository Root

- `Assets` - Unity project assets.
- `Packages` - Unity package manifest and lock file.
- `ProjectSettings` - Unity editor, player, XR, graphics, physics, and build settings.
- `NativeScreenCapture` - Visual Studio project and artifacts for the native capture plugin.
- `Working Prototype` - Built-player output from an earlier prototype.
- `docs` - Maintainer documentation.

## Assets

- `Assets/Scripts` - First-party runtime scripts and the `LightingPreset` ScriptableObject.
- `Assets/Editor` - First-party editor-only scripts.
- `Assets/VFX` - First-party Visual Effect Graph assets used by the stage system.
- `Assets/Prefab` - First-party prefabs and material assets.
- `Assets/Plugins` - DLL dependencies, native plugins, and the Android manifest.
- `Assets/XR` - XR loader and OpenXR/Oculus configuration.
- `Assets/Oculus`, `Assets/MetaXR`, `Assets/Resources` - Meta XR and Oculus runtime settings.
- `Assets/Imports` - Imported art/VFX packages.
- `Assets/Digital Rain FX` - Imported visual effect package.
- `Assets/Samples` - Unity package sample content.
- `Assets/TextMesh Pro` - TextMesh Pro resources and shaders.

## Scripts

- `AudioCaptureCSCore.cs` - Audio capture and runtime device UI.
- `AudioVisualizer.cs` - FFT visualization and audio feature detection.
- `StageManager.cs` - Runtime stage director and fixture/VFX/screen controller.
- `StageManager.GeneratedLibrary.cs` - Generated cue and palette data.
- `StageBuilder.cs` - Editor stage creation utility.
- `StageLightingPreset.cs` - Lighting preset ScriptableObject definitions.
- `ScreenCaptureNew.cs` - Current desktop capture path.
- `ScreenCapture.cs` - Legacy GDI capture path.
- `ScreenCaptureNative.cs` - Alternative native window capture path.
- `FogLightSync.cs` - Particle color synchronization helper.
- `ScreenPositionController.cs` - Camera-follow toggle for a screen object.
- `Init.cs` - Main camera tag setup helper.

## Organization Recommendations

Do not move folders aggressively while the project is under active cleanup. A safe future structure would be:

```text
Assets/
  Scripts/
    Audio/
    Stage/
    Capture/
    Utilities/
    Editor/
  VFX/
  Prefabs/
  Materials/
  Resources/
  XR/
```

Move scripts only when `.meta` files are preserved and Unity scene references are verified.
