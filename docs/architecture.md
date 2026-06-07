# Architecture

## Runtime Summary

MR-VD is organized around a real-time audio-reactive stage pipeline. The main scene is `Assets/Main.unity`, and the build settings currently include only that scene.

The core runtime chain is:

```text
AudioCaptureCSCore
  -> FFT buffers
  -> AudioVisualizer
  -> StageManager
  -> Lights, VFX Graphs, LED screens, render settings, and stage decor
```

## Core Systems

### Audio Capture

`AudioCaptureCSCore` captures audio through CSCore WASAPI. It supports two capture modes:

- `Input` for microphones and audio input devices.
- `Loopback` for system playback capture from output devices.

It exposes raw and smoothed FFT arrays, keeps a runtime device list, supports hotplug polling, and can show an IMGUI control panel for mode and device switching.

### Audio Visualization and Analysis

`AudioVisualizer` consumes FFT data and derives:

- bar visualization heights and emission colors
- kick, bass, and synth energy
- beat confidence and BPM
- silence state
- musical key and mode estimates

This class currently has a broad responsibility surface. Refactoring should be staged carefully because `StageManager` reads several public values directly.

### Stage Direction

`StageManager` is the main stage director. It caches scene fixtures, LED screens, VFX Graph components, generated cues, and generated palettes. On each update it:

1. Reads audio state.
2. Updates the beat clock and envelopes.
3. Selects or blends stage cues.
4. Updates palette colors.
5. Drives lights, VFX parameters, LED screen textures, and environment state.

`StageManager.GeneratedLibrary` provides generated palette and cue data. Treat it as data, not hand-authored gameplay logic.

### Screen Capture

The screen capture object in the main scene contains three implementations:

- `ScreenCaptureNew` is enabled and uses `DesktopPlugin.dll`.
- `ScreenCapture` is a disabled legacy GDI path.
- `ScreenCaptureNative` is a disabled native foreground-window path.

All screen capture paths are Windows-specific and write captured pixels into a `RawImage` texture.

### XR and Interaction

The main scene uses Meta XR building blocks for controller tracking, hand tracking, grab interaction, passthrough, and XR input. These systems are primarily package-driven and should not be reorganized unless the package setup is being rebuilt.

## Data and Asset Dependencies

- Visual Effect Graph assets live in `Assets/VFX`.
- Stage presets live in `Assets/Scripts/LightingPreset.asset`.
- XR configuration lives under `Assets/XR`, `Assets/Oculus`, `Assets/MetaXR`, and `Assets/Resources`.
- Third-party and sample assets are present under `Assets/Imports`, `Assets/Digital Rain FX`, and `Assets/Samples`.

## High-Risk Areas

- Renaming serialized fields in any script referenced by `Assets/Main.unity`.
- Moving scripts without preserving `.meta` GUIDs.
- Changing VFX exposed property names.
- Editing native plugin imports without testing on Windows.
- Reworking audio analysis algorithms without Play Mode validation.
