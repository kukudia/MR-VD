# Setup

## Unity Version

Use Unity `6000.3.0f1`. Opening the project with a different major version can trigger package, shader, and serialization changes.

## Required Modules

- Windows Build Support for desktop playback and capture testing.
- Android Build Support for Quest builds.
- OpenJDK, Android SDK, and Android NDK from Unity Hub if building to Quest.

## Package Restore

Unity restores package dependencies from `Packages/manifest.json`. Important packages include:

- Meta XR SDK All `77.0.0`
- Universal Render Pipeline `17.3.0`
- Visual Effect Graph `17.3.0`
- Unity Input System `1.16.0`
- Unity OpenXR `1.16.0`
- XR Management `4.5.3`

## Windows Audio Setup

1. Open `Assets/Main.unity`.
2. Enter Play Mode on Windows.
3. Use the `Audio Capture` panel.
4. Select `Loopback` to capture system playback or `Input` to capture a microphone/input device.
5. Choose an active device from the list.
6. Play audio and verify the visualizer, BPM display, and stage response.

## Screen Capture Setup

The main scene currently enables `ScreenCaptureNew`, which depends on `Assets/Plugins/DesktopPlugin.dll`.

For Windows desktop capture:

1. Keep `DesktopPlugin.dll` in `Assets/Plugins`.
2. Keep unsafe code enabled in Project Settings.
3. Assign the target `RawImage` to `screenObject`.
4. Run the scene on Windows.

The legacy `ScreenCapture` and `ScreenCaptureNative` components are present but disabled in the main scene.

## Quest / XR Setup

1. Confirm Meta XR project setup status in Unity.
2. Use Android as the build target for Quest.
3. Confirm the Android manifest includes Quest VR intent categories.
4. Verify OpenXR/Oculus features in `Assets/XR/Settings`.
5. Build and test on hardware because desktop validation cannot cover hand tracking, passthrough, and Quest runtime behavior.

## Build

The current build scene list contains only:

- `Assets/Main.unity`

Before release, run a fresh Unity compile, open the main scene, and perform a platform build for the intended target.
