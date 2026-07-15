# TODO

- [ ] English and Japanese localization

## BaseRecorder.cs

- [ ] Set TargetName default to hierarchy path
- [ ] Option for world relative or start position relative recording
- [ ] Add take number and framerate to start tags
- [ ] Add take number, frame count and duration to end
- [ ] Analyze frametimes, did we drop frames?

## PlayerRecorder.cs

- [ ] Restart take on avatar change or eye height change
- [ ] T-pose on avatar change to calibrate hip height, save as first frame
- [ ] Record hand and feet positions for IK
- [ ] Record all players

## PlayerRecorder.prefab

- [ ] Add recording overlay with status and framerate

## HumrRecordingLoader.cs

- [ ] Include displayname and take number in exported animations
- [ ] Remove prefix from exported FBX name
- [ ] Make target selector actually select only those takes
- [ ] Detect Avatar height mismatch, scale from calibrated first frame
- [ ] Use `HumanPoseHandler` to write muscle values and hand and feet IK instead of raw rotations
- [ ] Fix toes rotation

## HumrRecordingLoaderEditor.cs

- [ ] Update RecordingFiles if last write time is different
- [ ] Export Mode (Humanoid/Generic) dropdown
- [ ] List takes with durations and checkmarks to include them

## Samples

- [ ] Add canvas with instructions
- [ ] Make `Humr Sample Scene.unity` into a package sample
