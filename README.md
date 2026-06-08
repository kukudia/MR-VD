# MR-VD

## Overview

MR-VD is a Unity mixed-reality screen capture and audio visualization prototype for Meta Quest devices and Windows desktop playback. The project combines XR interaction, real-time audio capture, FFT analysis, beat detection, dynamic lighting, Visual Effect Graph assets, LED-style screen visuals, and optional desktop screen capture.

The main experience is an audio-reactive virtual stage. Music captured from a Windows input or loopback device drives stage lighting, generated screen textures, VFX parameters, and cue changes through a central stage director.

## Features

- Meta Quest / OpenXR mixed-reality scene built around Meta XR SDK building blocks.
- Real-time Windows audio capture through CSCore WASAPI input or loopback mode.
- FFT-driven bar visualization with BPM, beat, silence, and key estimation.
- Audio-reactive stage director for spotlights, rim lights, chase lights, lasers, strobes, LED screens, and stage environment effects.
- Generated cue and palette library for automated VJ-style lighting changes.
- Visual Effect Graph assets for background particles, smoke, beat bursts, laser beams, and ground rings.
- Desktop capture support through native Windows plugins and UI texture streaming.
- Editor utilities for stage generation and VFX binding maintenance.

## Tech Stack

- Unity `6000.3.0f1`
- Universal Render Pipeline `17.3.0`
- Visual Effect Graph `17.3.0`
- Meta XR SDK All `77.0.0`
- Unity OpenXR `1.16.0`
- XR Management `4.5.3`
- Unity Input System `1.16.0`
- CSCore `1.2.1.2`
- NWaves `0.9.6`
- Native Windows plugins: `DesktopPlugin.dll`, `NativeScreenCapture.dll`

## Installation

1. Install Unity `6000.3.0f1` with Windows Build Support and Android Build Support if targeting Quest.
2. Clone the repository.
3. Open the project folder in Unity Hub.
4. Allow Unity to restore packages from `Packages/manifest.json`.
5. Open `Assets/Main.unity`.
6. For Windows audio capture, run the scene on Windows with an active input or playback device.
7. For Quest builds, confirm Meta XR project setup and Android build settings before building.

## Project Structure

- `Assets/Scripts` - Project runtime scripts for audio capture, visualization, stage control, screen capture, and editor-assisted stage generation.
- `Assets/Editor` - Editor build hooks and stage utility menu commands.
- `Assets/VFX` - Project Visual Effect Graph assets used by the stage system.
- `Assets/Prefab` - Small project prefabs and stage materials.
- `Assets/Plugins` - Managed and native plugin DLLs for audio and screen capture.
- `Assets/XR`, `Assets/Oculus`, `Assets/MetaXR`, `Assets/Resources` - XR, Meta, Oculus, and runtime configuration assets.
- `Assets/Imports`, `Assets/Digital Rain FX`, `Assets/Samples` - Imported third-party and Unity sample content.
- `NativeScreenCapture` - Native Windows capture plugin source and build artifacts.
- `docs` - Project architecture, setup, structure, and troubleshooting notes.

## Architecture Overview

The runtime flow is:

`AudioCaptureCSCore -> AudioVisualizer -> StageManager -> Lights / VFX / LED Screens / Environment`

`AudioCaptureCSCore` captures audio through CSCore and exposes FFT buffers. `AudioVisualizer` analyzes the buffers for energy, beat, BPM, silence, and key information. `StageManager` consumes that data to drive fixtures, VFX Graph parameters, generated screen textures, cue selection, and render settings.

Screen capture is a separate path:

`DesktopPlugin.dll / NativeScreenCapture.dll / GDI -> ScreenCapture component -> RawImage`

See [Architecture](docs/architecture.md) for a deeper system map.

## Controls

- Keyboard: Space toggles screen-follow behavior in `ScreenPositionController`.
- Runtime audio panel: `AudioCaptureCSCore` can show an IMGUI panel for input/loopback mode switching and device selection.
- VR controls: Meta XR building blocks provide hand tracking, controller tracking, grab, passthrough, and interaction behavior in the main scene.
- Stage controls: `StageManager` exposes context menu commands for cache rebuilds, cue changes, white flash, and VFX graph completion.

## Setup Requirements

- Windows is required for CSCore WASAPI audio capture and the native desktop capture plugins.
- Meta Quest builds require Android support, Meta XR configuration, and a compatible Quest device.
- The project uses both the legacy input manager and the Unity Input System.
- Unsafe code is enabled for native texture buffer access.

## Usage

1. Open `Assets/Main.unity`.
2. Press Play in the Unity Editor.
3. Use the audio capture panel to select Input or Loopback mode.
4. Play audio through the selected device.
5. Watch the stage lighting, VFX, LED screens, and audio bars react to the signal.

## Screenshots / Demo

![MR-VD demo placeholder](docs/images/demo-placeholder.svg)

Replace `docs/images/demo-placeholder.svg` with a project screenshot or demo thumbnail when available.

- Main mixed-reality stage view
- Audio capture panel
- Audio-reactive lighting moment
- Quest passthrough interaction view

## Known Issues

- Several large runtime systems are implemented as monolithic scripts and should be refactored only with strong regression coverage.
- Windows screen capture paths depend on native DLL availability and platform-specific APIs.
- Some imported sample folders are not part of the main build and may contain unused assets.
- Runtime validation should be performed in Unity before release because CLI-only validation cannot detect missing scene references.

## Roadmap

- Add project screenshots and a short demo video.
- Add assembly definition files for project runtime and editor code.
- Split audio analysis and stage direction into smaller tested services.
- Add Play Mode tests for beat detection and stage cue selection.
- Add a documented Quest build checklist.
- Review imported samples and build artifacts for repository size reduction.

## Contributing

Keep changes conservative and Unity-safe. Avoid renaming serialized fields unless using `[FormerlySerializedAs]`, and verify scene and prefab references after any script changes. Use `rg` for repository searches and keep unrelated generated files out of commits.

## License

No license file is currently included. Add a license before distributing this project as open source.
