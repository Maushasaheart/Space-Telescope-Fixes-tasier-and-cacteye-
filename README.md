[README.txt](https://github.com/user-attachments/files/32868544/README.txt)
CactEyeParallaxFix 0.2.1
Parallax Scaled stays enabled

INSTALL OR UPGRADE
1. Close KSP.
2. Remove the previous GameData/CactEyeParallaxFix folder, if installed.
3. Copy this ZIP's GameData folder into the KSP folder, merging with the existing GameData folder.
4. Start KSP, open a CactEye telescope and zoom onto a planet.



The .cs source files and Tests folder are optional and are not required for the DLL to work. Only copy GameData.

WHAT CHANGED
Parallax's ScaledOnDemandComponent calculates planet screen size using ScaledCamera.Instance.cam, the normal scaled camera. CactEye uses its own camera and field of view. A planet can be large in the telescope while too small in the normal view to keep Parallax textures loaded. Parallax's planet color map defaults to white when missing. This is a concrete compatibility mismatch in the inspected source, but visual confirmation of the cause of your particular white planets remains pending.

This version observes only explicit CactEye telescope rendering. Immediately before its scaled camera renders, it checks which Parallax planets intersect the telescope view and cover at least one pixel. It asks Parallax's own Load method to load those textures and prevents its main camera visibility check from unloading them while viewed.

Already running async loads are allowed to finish; the plugin does not start a duplicate load. A two second grace period prevents rapid unloads at the view edge. After looking away or closing the telescope, normal Parallax texture management resumes. Scene changes clear every hold immediately.

Parallax Scaled materials, shaders and shadow settings remain in use. This plugin does not swap materials, disable renderers, change camera positions, alter graphics settings, or install a ModuleManager patch. Terrain and scatters remain enabled. Only textures needed by the telescope view are retained.

An initial pause when a new planet comes into view is possible because the requested load is synchronous. Existing async loads may take several frames to finish. Large planet textures may use extra memory while viewed. Parallax itself performs texture loading and ownership management.

REQUIREMENTS
KSP 1.12.x, CactEye, Parallax Continued and the existing Harmony dependency normally found in GameData/000_Harmony. No game assemblies or dependency DLLs are redistributed.

1.0.0+c449da19891ae8a39d569d3de0176a8ca16142b3. The Parallax source reviewed is that exact commit. CactEye camera source was reviewed from tree f4d0d09e7b00b398dde5b664a768a0250336516a. Your installation may have changed since the log.

TESTING AND LIMITS
Compiled against real KSP and Unity references as a .NET Framework DLL. Twenty two simulated checks passed for visibility, texture retention and release, material preservation, avoiding duplicate async loads, failure cooldown, scene cleanup and exception cleanup. Test sources are included under Tests.

Not run inside KSP. Tests do not exercise Unity rendering, native texture IO or runtime Harmony patch installation. This build targets the verified loading mismatch. It does not claim to resolve separate Deferred, lighting, cloud, clipping or shader problems.

HOW TO CHECK
Open CactEye and zoom onto Moho or another affected planet. Wait for any initial texture load. If the image remains white, close KSP and send the resulting KSP.log plus a telescope screenshot.

Search KSP.log for [CactEyeParallaxFix v2.1]. Expected messages:
Ready; Scaled rendering stays enabled.
Telescope render detected
Holding telescope textures: Moho
Textures ready: Moho; _ColorMap=...

The texture dimensions help distinguish a loaded color map from a missing one. If textures load but the image remains white, investigate the camera or shader path next. Inactive or Paused messages indicate an operation failed. No Parallax Scaled bodies suggests a disabling CFG remains installed.

UNINSTALL
Close KSP and remove GameData/CactEyeParallaxFix. No save file changes are made. Restore the previous global disabling CFG only if you want that workaround again.

SOURCE AND REBUILD
Compile both Source/CactEyeParallaxFix.cs and Source/ScopeVisibility.cs into one library using .NET Framework references plus Assembly-CSharp.dll, UnityEngine.dll, UnityEngine.CoreModule.dll and 0Harmony.dll from a compatible installation. Parallax and CactEye runtime methods are accessed through reflection.

References:
https://github.com/Gameslinx/Parallax-Continued
https://github.com/linuxgurugamer/CactEye-2
https://docs.unity.cn/2019.4/Documentation/ScriptReference/Camera.Render.html
send to me mausanchan on discord for any issues or complaints
