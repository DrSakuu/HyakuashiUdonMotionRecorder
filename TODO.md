# TODO

- [ ] English and Japanese localization

## BaseRecorder.cs

- [ ] Set hierarchy path as TargetName
- [ ] Add take number and framerate to start tags
- [ ] Add take number, frame count and duration to end tags
- [ ] Option for world relative or start position relative recording

## PlayerRecorder.cs

- [ ] T-pose on avatar change to calibrate hip height, save as first frame
- [ ] Restart recording on avatar change or eye height change
- [ ] Record hand and feet positions for IK
- [ ] Record all players

## PlayerRecorder.prefab

- [ ] Add recording overlay

## PlayerRecordingLoader.cs

- [ ] Use `HumanPoseHandler` to write muscle values and hand and feet IK instead of raw rotations
- [ ] Detect Avatar height mismatch, scale from calibrated first frame
- [ ] Fix toes rotation
- [ ] Include displayname and take number in exported animations

## PlayerRecordingLoaderEditor.cs

- [ ] List all log files, but show if they have None, Standard or Legacy recordings
- [ ] Rename button "LoadLogToExportAnim" -> "Load recording and export `.fbx`"
- [ ] Export Mode (Humanoid/Generic) dropdown
- [ ] List takes with durations and checkmarks to include them

## Samples

- [ ] Make `Humr Sample Scene.unity` into a package sample
- [ ] Add canvas with instructions
