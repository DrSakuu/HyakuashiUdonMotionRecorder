# Hyakuashi Udon Motion Recorder

[日本語](Documentation/README.jp.md)

HUMR is a motion capture tool that records player's movements into the VRChat log files and then reads them in a Unity project. This is version 2, which uses a new log format.

## Installation

> [!WARNING]
> Remove the old `HUMR OutputLogLoader` package and `Prefabs`, `ReadMe`, `Scenes` and `Scripts` in `Assets/HUMR` before importing. This is done automatically if installed from VPM.

### Requirements

- Unity 2022.3.22f1
- FBX Exporter =>4.2.1 (Installed automatically on import)
- VRChat World SDK =>3.10.0 (For recording)

### VRChat Package Manager

Install from Sakuu's VPM Listing: <https://drsakuu.github.io/vpm-listing/> (with [ALCOM](https://vrc-get.anatawa12.com/alcom/)).

### Other

For loading animations, the VRChat SDK is not needed. If you don't use VPM, download the `.unitypackage` from [releases](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/releases) and import it into any Unity project.

## Usage

### Recording

> [!IMPORTANT]
> You need to set Logging to Full in the VRChat Debug settings for HUMR to work.

Either use [the public world](https://vrchat.com/home/launch?worldId=wrld_1fbb2fea-788e-43a8-a588-8ee7edf8e680) or add the HumrPlayerRecorder prefab to your VRChat World project. The public world is included as HUMR Sample World in the Samples tab of the Package Manager.

Use the button on the mirror to start and stop recording. Multiple recordings will be split into takes in the same exported file.

The bones of the avatar that you record and load your motion have to match exactly, so if you don't have access to the .fbx file of your VRChat avatar, you can use the sample robot from the pedestal on the left, because that is included in the VRChat SDK. Unity can retarget the animation to another avatar after you import it, or you can use a tool like Rokoko plugin for Blender for manual retargeting.

VRChat logs are deleted after about a week, so make sure to load the saved data or copy the log files elsewhere.

### Loading

Import the drsakuu.humr Unitypackage into a Unity 2022.3.22f1 project and attach the HumrRecordingLoader component to an animator with a human avatar. Select the VRChat log file with your recording takes from the list and export it as either .fbx or .anim.

## Changelog

[CHANGELOG.md](CHANGELOG.md)

## Contributing

[Issues](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/issues) and [Pull requests](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/pulls) are welcome! Check [TODO.md](TODO.md) for inspiration what to do next.

## License

[MIT License](LICENSE.md)
