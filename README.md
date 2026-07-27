# Hyakuashi Udon Motion Recorder

[日本語](README.jp.md)

HUMR is a motion capture tool that records player's movements into the VRChat log files and then reads them in a VRChat world project. This is version 2.0.0, which is complete rewrite of the codebase so new features can be implemented more easily.

- Combine Recorder and OutputLogLoader `.unitypackage`s
- New Recording log format: semicolons to separate different types
- Select player DisplayName from a dropdown
- Add HUMR Sample World

## Installation

> [!WARNING]
> Remove the old `HUMR OutputLogLoader` package and `Prefabs`, `ReadMe`, `Scenes` and `Scripts` in `Assets/HUMR` before importing.

### Requirements

- Unity 2022.3.22f1
- FBX Exporter Version 4.2.1
- VRChat 2026.2.2

Download the `.unitypackage` from releases and import it into your VRChat World project for recording, or into your VRC avatar project for loading.

## Usage

### Recording

Either use [the public world](https://vrchat.com/home/launch?worldId=wrld_1fbb2fea-788e-43a8-a588-8ee7edf8e680) or import and build the HUMR Sample World from the Package Manager.

Use the button on the mirror to start and stop recording. Multiple recordings will be split into takes in the same exported file.

The bones of the avatar that you record and load your motion has to match exactly, so if you don't have access to the .fbx file of your current avatar, you can use the sample robot from the pedestal on the left, because that is included in the package. Unity can retarget the animation to another avatar when you import it. If you want to apply the animation to another model outside of Unity, you can use a tool like Rokoko plugin for Blender to retarget it.

VRChat logs are deleted after 48 hours, so make sure to load the saved data or copy the log files elsewhere.

### Loading

Use the Avatar_Utility Recording Loader in the HUMR Sample World to load your animation, or attach the HumrRecordingLoader component to your custom avatar in your avatar project.

## Changelog

[CHANGELOG.md](CHANGELOG.md#200-beta2---unreleased)

## License

[LICENSE.md](LICENSE.md)
