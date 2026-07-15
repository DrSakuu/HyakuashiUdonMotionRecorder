# Hyakuashi Udon Motion Recorder

[日本語](README.jp.md)

A complete rewrite of the codebase so new features can be implemented more easily.

- Combine Recorder and OutputLogLoader `.unitypackage`s
- New Recording log format: semicolons to separate different types
- Select player DisplayName from a dropdown
- Split up code into different methods

> [!WARNING]
> This is v2.0.0-alpha.0, subject to breaking changes.

## Installation

> [!WARNING]
> Remove `Packages/HUMR OutputLogLoader` and `Prefabs`, `ReadMe`, `Scenes` and `Scripts` in `Assets/HUMR` before importing.

### Requirements

- Unity 2022.3.22f1
- VRChat SDK - Worlds 3.10.3
- FBX Exporter Version 4.2.1
- VRChat 2026.2.2

Download the `.unitypackage` from releases and import it into your VRChat World project.

## Usage

Duplicate `Humr Sample Scene.unity` from `Packages/Hyakuashi Udon Motion Recorder/Scenes/` into your `Assets`. Build & Test the world to do a recording, or use [the public world](https://vrchat.com/home/launch?worldId=wrld_5962f8a1-bc92-481e-b05a-7cb90eadce34). Use the `PlayerRecordingLoader` component in `Avatar_Utility Loader` to Load recording and export fbx.

## Changelog

[CHANGELOG.md](CHANGELOG.md#200---unreleased)
