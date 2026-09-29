# XR audio effects

Unity 6000.3.11f1 / URP 17.3.0. Metres, world-space simulation, no camera shake or dynamic lights.

## Visual direction and assets

Design references: the music-synchronised particle accents of [Tetris Effect](https://www.tetriseffect.game/) and peripheral stage lighting in [Beat Saber](https://www.beatsaber.com/). These are composition references, not copied assets. The central desktop reading area remains open; short bilateral sparks introduce longer, tapered comet trails. Four palettes: AuroraCyan, NebulaViolet, SolarAmber, JadeEmerald.

All four prefabs and MeteorGlow.mat are native editable assets. The shader generates the soft core analytically; no third-party textures were needed. No content from the ignored UNI VFX/GabrielAguiarProductions folders is required by these prefabs. The production sources are XrMeteor.shader and AudioVisualEffectsInstaller.cs; deterministic seeds are stored on each prefab.

## Editing and preview

- Scene root `Audio Visual Effects`: AudioVisualEffectsController binds AudioVisualizer, camera, optional stage palette/cues and four prefab instances. It changes variant after four detected beats; minimum interval is 0.28 s. `Preview Beat (Play Mode)` is a context menu on the component.
- Each prefab: XrBeatMeteorEffect controls side distance, forward distance, passing offset, lifetime, count and seed. `Meteors` and `Burst Sparks` expose Particle System colors, curves, capacity and trails. Material emission and head-distance fade are editable.
- Default volley: at most 18 meteor heads + 36 short sparks. Heads live about 0.94–1.27 s; trails last 0.3 s. Each variant is capped at 128 heads / 96 sparks. These are conservative initial budgets, not measured headset performance guarantees.
- A volley captures horizontal camera orientation when fired and moves in world space. It heads toward the camera's peripheral region, not its centre. Shader fades geometry between 1.4 and 0.85 m from each rendered eye, including when the user moves after emission.
- GPU stardust keeps 3,072 particles and Screen occlusion. `Particle Gradient`, `Gradient Speed`, `Emission` and `Particle Size` are the main editing controls. Brightness is applied once instead of squaring the audio response. Gradient texture is rebuilt on Inspector changes and released on disable.
- `Tools/MR-VD/Audio Effects/Install or Repair Active Scene` creates missing assets/instances without overwriting existing prefab edits. Save the scene after installation. Project migration saves both stage scenes. To regenerate a prefab, deliberately remove that prefab asset and rerun installation; existing assets are preserved.
- `Render Variant Contact Sheet` renders all four editable prefabs to `Temp/AudioEffectsValidation/meteor-variants.png`. `Validate Trajectories` checks symmetric emission, tilted-head orientation, default trajectory clearance and cleanup.

## Architecture and migration

StageManager now owns lighting, VJ screens and stage composition. It exposes a read-only PresentationFrame; it no longer binds, creates or updates VFX Graphs. AudioVisualEffectsController owns the graph references/parameters and detected-beat emission. Meteors work with StageManager disabled. StageLightingPreset smoke control and StageBuilder installation were migrated too.

Both `v203.0.0.unity` and `v77.0(Abondoned).unity` were migrated via Unity serialization, retaining their graph references and authored VFX parameter values. The unreferenced `Assets/VFX/BeatBurstEffect.vfx` was retired; the four Particle System prefabs replace it. Restore branch: `codex/audio-vfx-before-20260929`.

## Validation (2026-09-29)

- Unity script import/compilation and meteor Shader validation passed. Four-prefab contact sheet rendered and inspected.
- Edit Mode trajectory check passed: 9 heads per side, no centre crossing, default path >1.1 m from the captured head position, clear removes all particles.
- Play Mode with injected AudioVisualizer beat timestamps passed: one emission per timestamp, silence suppression, independence from disabled StageManager, particle cleanup and GPU buffer release/recreation (3,072 particles).
- Standard `dotnet build Assembly-CSharp.csproj --no-restore --nologo` is blocked by existing LibreHardwareMonitor dependencies targeting .NET 4.7.2 while the generated csproj targets 4.7.1. Command-local override `-p:TargetFrameworkVersion=v4.8` passed with 0 errors; no project framework or dependency was changed. Existing reference/compiler warnings remain.
- Real music synchronisation, stereo headset comfort and headset frame time are not validated. The connected Editor reports Meta XR `ErrorFormFactorUnavailable` without a headset.
