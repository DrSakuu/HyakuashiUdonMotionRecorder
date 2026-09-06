# Loading

[日本語](Loading.jp.md)

For loading recordings, the VRChat SDK is not needed. If you want to do it without the VRChat Package Manager, download the `.unitypackage` from [releases](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/releases) and import it into any Unity 2022.3.22f1 project.

For loading the animation with your VRChat avatar, it is better to use the base .fbx file and not the prefab. If you still want to do it, use "Tools - Modular Avatar - Manual bake avatar" before loading.

Add the HumrRecordingLoader Component to an animator with a human avatar. Select the VRChat log file you recorded earlier and export the takes as either .fbx or .anim. 

![Loading an animation in Unity](HUMRLoading.gif)

> [!NOTE]
> The .anim files are Generic and not Humanoid animations, even if the Avatar is Humanoid. If you want to play them on your avatar, temporarily set the Avatar to None in the Animator. This will be fixed in a future release.
