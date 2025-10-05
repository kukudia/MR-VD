----------------------------------------
Realistic Fog FX
----------------------------------------

1. Introduction
2. Customizing ffects
3. Custom Shader
4. URP/HDRP Upgrade
5. Contact

----------------------------------------
1. INTRODUCTION
----------------------------------------

To use the effects, simply instantiate them, or drag & drop them into the scene. The effects will automatically start playing when the scene is running.

Effects use Soft Particles and Camera Fade by default, this means you need to have Soft Particles enabled in your project to have them render properly. Alternatively you can disable the Soft Particle and Camera Fade settings.

For URP projects, make sure that Depth Texture is enabled in your SRP Profile and your Camera. Otherwise you may end up with invisible particles.

----------------------------------------
2. CUSTOMIZING EFFECTS
----------------------------------------

-- Fog Color -- 

You can set the Fog Color directly in the Material, or in the Start Color of the particle system.

-- Scaling -- 

To scale an effect in the scene, simply use the default Scaling tool (Hotkey 'R'). You can also select the effect and type in the Scale in Transform manually. Alternatively change the Start Size for each individual particle at the top of the Particle System.

-- Effect Radius -- 

If you want to adjust the radius of the area of effect, find the Shape module in the parent Particle System and adjust the Radius.

-- Fog wind/direction --

The direction of the fog can be set in the 'Velocity over Lifetime' module, or if you have wind zones, you can enable 'External Forces' in the parent Particle System.

----------------------------------------
3. CUSTOM SHADERS
----------------------------------------

Included in this asset are two custom shaders with different lighting options.

'Archanor VFX/Real Fog/LitFog' and 'Archanor VFX/Real Fog/UnlitFog' 

The lit fog shader is affected by the light while the other one is not. For best performance, the Unlit fog is recommended.

Additionally there are two alternate Materials that you can swap to that uses Unity's default Standard Particle shaders.

Custom Shader Properties:
--------------------

* Texture - What texture/spritesheet the material uses.

* Fog Color - Set the Color of the fog.

* Fog Glow - Add an optional glow to the fog, this can look great with Bloom post processing effects.

* Soft Particles Factor - This adjusts how much the particles will blend with the scene depth.

* Camera Fade Distance - How far should the fog fade according to the camera?

* Camera Fade Sharpness - How gradually the fog fades away.

* Soft Particles Bool - If there's no environment for the particles to blend with, this can be turned off.

* Camera Fade - If the camera does not approach the fog particles, this can be turned off.

The shaders were created with Amplify Shader Editor.

----------------------------------------
4. URP UPGRADE
----------------------------------------

To upgrade to URP or HDRP find the 'Upgrade' folder, double-click the Upgrade package and import to your project.

Upgrade packages have been tested in Unity 2019.3.10f1 with URP v7.3.1 and HDRP v7.4.3.

----------------------------------------
5. CONTACT
----------------------------------------

Support  -  archanor.com/support.html
Email    -  archanor.work@gmail.com
Twitter  -  @archanor

Ratings & reviews are much appreciated!