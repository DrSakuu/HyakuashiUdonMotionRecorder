# TODO

- [ ] English and Japanese localization
- [ ] Make VPM package site
- [ ] Clean up Editor/Runtime classes

## BaseRecorder.cs

- [ ] Set TargetName default to hierarchy path
- [ ] Option for world relative or start position relative recording
- [ ] Add take timestamp and framerate to start tags
- [ ] Add take timestamp, frame count and duration to end
- [ ] Analyze frametimes, did we drop frames?

## PlayerRecorder.cs

- [ ] Restart take on avatar change or eye height change
- [ ] T-pose on avatar change to calibrate hip height, save to start tags
- [ ] Record hand and feet positions for IK
- [ ] Record all players

## PlayerRecorder.prefab

- [ ] Add recording origin marker
- [ ] Add countdown
- [ ] Add recording overlay with status and framerate
- [ ] Advanced options to change framerate
- [ ] Hold right stick up to show stop button

## HumrRecordingLoaderEditor.cs

- [ ] Select newest log file with HUMR data
- [ ] List takes with durations and checkmarks to include them
- [ ] Test collect Legacy and Humr targets in same file
- [ ] Include displayname and take number in exported animations
- [ ] Make target selector actually select only takes belonging to target
- [ ] Detect Avatar height mismatch, scale from calibrated start tags
- [ ] Use `HumanPoseHandler` to write muscle values and hand and feet IK instead of raw rotations
- [ ] Fix toes rotation
- [ ] Update RecordingFiles if last write time is different

## Samples

- [ ] Update public world
