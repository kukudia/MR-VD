# XR audio effects

Unity 6000.3.11f1 / URP 17.3.0. Metres, world-space simulation, no camera shake or dynamic lights.

## Visual direction and assets

Design references: the music-synchronised particle accents of [Tetris Effect](https://www.tetriseffect.game/) and peripheral stage lighting in [Beat Saber](https://www.beatsaber.com/). These are composition references, not copied assets. The central desktop reading area remains open; short bilateral sparks introduce longer, tapered comet trails. Four palettes: AuroraCyan, NebulaViolet, SolarAmber, JadeEmerald.

All four prefabs and MeteorGlow.mat are native editable assets. The shader generates the soft core analytically; no third-party textures were needed. No content from the ignored UNI VFX/GabrielAguiarProductions folders is required by these prefabs. The production sources are XrMeteor.shader and AudioVisualEffectsInstaller.cs; deterministic seeds are stored on each prefab.

## Editing and preview

- Scene root `Audio Visual Effects`: AudioVisualEffectsController binds AudioVisualizer, camera, optional stage palette/cues and four prefab instances. It rotates variant after four visual onsets; minimum burst interval is 0.18 s and the tempo detector keeps its own 0.45 s BPM cooldown. `Preview Beat (Play Mode)` is a context menu on the component.
- Each prefab: XrBeatMeteorEffect controls side distance, forward distance, passing offset, lifetime, count and seed. `Meteors` and `Burst Sparks` expose Particle System colors, curves, capacity and trails. Material emission and head-distance fade are editable.
- Default strong volley: 18 meteor heads + 36 short companion sparks, with no separate flash or scatter burst. Heads have high initial speed and brightness, then slow and dim through lifetime. Each particle system is capped at 128 particles. These are conservative initial budgets, not measured headset performance guarantees.
- A volley captures horizontal camera orientation when fired and moves in world space. It heads toward the camera's peripheral region, not its centre. Shader fades geometry between 1.4 and 0.85 m from each rendered eye, including when the user moves after emission.
- GPU stardust keeps 3,072 particles and Screen occlusion. `Particle Gradient`, `Gradient Speed`, `Emission` and `Particle Size` are the main editing controls. Brightness is applied once instead of squaring the audio response. Gradient texture is rebuilt on Inspector changes and released on disable.
- `Tools/MR-VD/Audio Effects/Install or Repair Active Scene` creates missing assets/instances without overwriting existing prefab edits. Save the scene after installation. Project migration saves both stage scenes. To regenerate a prefab, deliberately remove that prefab asset and rerun installation; existing assets are preserved.
- `Render Variant Contact Sheet` renders all four editable prefabs to `Temp/AudioEffectsValidation/meteor-variants.png`. `Validate Trajectories` checks symmetric emission, tilted-head orientation, default trajectory clearance and cleanup. `Validate Onset Response` checks fast accents, steady audio and silence. `Validate Play Mode Response` exercises onset consumption and lifecycle in Play Mode.
- `Configure Meteor Lifetime Decay` reapplies the exponential speed and alpha curves to the four prefabs while preserving authored palette colors and trajectory settings. Normal installation still leaves existing prefab edits intact.

## Architecture and migration

StageManager now owns lighting, VJ screens and stage composition. It exposes a read-only PresentationFrame; it no longer binds, creates or updates VFX Graphs. AudioVisualEffectsController owns graph references/parameters and visual-onset emission. AudioVisualizer publishes sequence/time/confidence/strength from unsmoothed kick/snare attacks before BPM cooldown; the controller consumes each new sequence once, with its own confidence gate and cooldown. Strength uses normalized attacks instead of smoothed kick loudness. Meteors work with StageManager disabled. StageLightingPreset smoke control and StageBuilder installation were migrated too.

Both `v203.0.0.unity` and `v77.0(Abondoned).unity` were migrated via Unity serialization, retaining their graph references and authored VFX parameter values. The unreferenced `Assets/VFX/BeatBurstEffect.vfx` was retired; the four Particle System prefabs replace it. Restore branch: `codex/audio-vfx-before-20260929`.

## Validation (2026-09-29)

- Unity script import/compilation and meteor Shader validation passed. Four-prefab contact sheet rendered and inspected.
- Edit Mode trajectory check passed: 9 heads per side, no centre crossing, default path >1.1 m from the captured head position, clear removes all particles.
- Play Mode with injected AudioVisualizer beat timestamps passed: one emission per timestamp, silence suppression, independence from disabled StageManager, particle cleanup and GPU buffer release/recreation (3,072 particles).
- Standard `dotnet build Assembly-CSharp.csproj --no-restore --nologo` is blocked by existing LibreHardwareMonitor dependencies targeting .NET 4.7.2 while the generated csproj targets 4.7.1. Command-local override `-p:TargetFrameworkVersion=v4.8` passed with 0 errors; no project framework or dependency was changed. Existing reference/compiler warnings remain.
- Real music synchronisation, stereo headset comfort and headset frame time are not validated. The connected Editor reports Meta XR `ErrorFormFactorUnavailable` without a headset.

## Impact refinement validation (2026-09-30)

- Unity 6000.3.11f1 imported/compiled the changes and saved the four prefabs, material and active scene through Editor APIs. Four-color preview inspected; 26-head trajectory check passed. Existing central clearance and per-eye near fade retained.
- Synthetic 0.24 s accents: 12/12 visual onsets detected while BPM cooldown remained separate. Sustained baseline and silence produced no extra onsets.
- Offline replay of the user's 95.21 s recording: 235 visual onsets versus 165 tempo beats; normalized strengths 0.32–1.0. Replay uses mono audio, a 4096-sample Hann FFT, 20 ms steps and the actual C# detector. This is an approximate comparison, not a measurement of live capture latency or musical detection accuracy.
- Play Mode injection passed: same-sequence deduplication, 0.18 s cooldown, a 0.20 s accent accepted without changing tempo state, silence/stale/weak rejection, disabled StageManager independence and disable/re-enable cleanup. No new effect exceptions; Meta XR reports the missing-headset error.
- `git diff --check` passed. Standard dotnet build still fails on existing LibreHardwareMonitor/PerformanceMonitorPanel framework references. The command-local `-p:TargetFrameworkVersion=v4.8` build passed with 0 errors and 22 existing warnings; no framework configuration changed.
- Live music listening, end-to-end capture latency and headset comfort/frame time remain unverified. Lower `Meteor Intensity` if the peripheral accents are too prominent in-headset.

## Lifetime decay revision (2026-09-30)

- Removed the separate launch flash, scatter burst and strong-beat count/size boosts. Four variants retain their color gradients, original nine heads per side, two companion sparks per head and XR near fade.
- Normalized lifetime `t` uses speed multiplier `0.12 + 0.88 exp(-3.4t)`. Initial velocity is divided by the integral mean of that curve, so the particle still covers its peripheral path. Alpha follows sampled `exp(-4t)` and reaches zero at death; material emission is 7.5 at spawn. Both curves are editable in the Prefabs.
- Unity particle simulation, trajectory clearance and four-color contact sheet were rerun after the change. Actual sampled movement slowed on each successive 0.1 s step. Headset comfort and frame time remain to be checked on device.
