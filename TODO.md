# TODO

- [ ] English and Japanese localization
- [ ] Update scene

## BaseRecorder.cs

- [ ] Set hierarchy path as TargetName
- [ ] Add take number and framerate to start tags
- [ ] Add take number and frame count to end tags
- [ ] Analyze frame times on recording end
- [ ] Option for world relative or start position relative recording

## PlayerRecorder.cs

- [x] Do not start recording before avatar loads
- [ ] Record all players
- [ ] Restart recording on avatar change or eye height change

## PlayerRecorder.prefab

- [x] Add user interface with record button
- [ ] T-pose on recording start

## RecordLogLoader.cs

- [ ] Make compatible with previous Log syntax
- [x] Export Generic animation
- [ ] Load separate takes
- [ ] Fix toes rotation
- [ ] Include displayname and take number in exported FBX
- [ ] Load Object recordings

## RecordLogLoaderEditor.cs

- [x] Only show log files with HUMR data
- [x] Choose DisplayName from dropdown
- [ ] Export Mode (Humanoid/Generic) dropdown
- [x] Explore log file path in advanced foldout
- [ ] Take selector

## HumrSampleScene.unity

- [ ] Add canvas with instructions
