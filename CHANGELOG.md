# Changelog

## [1.0] - 2021-02-07

### Added

- リリース  

## [1.1] - 2021-02-08

### Fixed

- Quaternion補間が行われていない不具合を修正
- `OutputLogLoader` を v1.1 に更新  
- Interactで録画の停止・再開が行えるInteractRecorderを追加

## [1.1.1] - 2021-02-09

### Fixed

- TmpAniConへのパスの修正，clip名が空の時には適当な名前が付くように対応
- clip名が空の時には適当な名前が付くように対応
- `OutputLogLoader` を v1.1.1 に更新
- InteractRecorder.prefabのU#参照が外れていたのを修正,誤字を修正

## [1.2] - 2021-02-12

### Added

- インタラクトでレコードの停止・再開を行えるInteractRecorderを追加
- `Recorder` を v1.1 に更新  

### Fixed

- VRCを落とさずに複数回記録した際に正常に出力されない不具合を修正
- `OutputLogLoader` を v1.2 に更新  
- ログファイル内に複数のレコードログが有った場合に分けて出力するように対応

## [1.3] - 2021-04-27

### Fixed

- ArmatureのScaleが(1,1,1)でないときに座標が正しく出力されない不具合を修正
- `OutputLogLoader` を v1.3 に更新  

## [1.3.1] - 2021-09-12

### Fixed

- v1.3でunitypackageに反映が漏れていた修正を反映
- `OutputLogLoader` を v1.3.1 に更新  

## [1.3.2] - 2021-11-27

### Fixed

- DisplayNameにファイルパスに使用できない文字を使用していた場合に対応
- `OutputLogLoader` を v1.3.1 に更新
- InteractRecorder.prefabのU#参照が外れていたのを修正
- 誤字を修正
- `Recorder` を v1.1.1 に更新

## [VCC_1.0.0] - 2024-05-12

### Changed

- VCC向けに諸々を更新
- VCCに合わせて記載やフォルダ構造等を変更。
- VCC(U#1.0以上)向けに対応
- Unity2022.3.6f1で動作確認
- Q&Aを更新  

## [VCC_1.1.0] - 2025-02-12

### Changed

- VRChat 2025.1.2 (OPEN BETA)以降のログファイル形式に対応
- 旧バージョンの説明書きとファイルを削除

### Fixed

- `OutputLogLoader` を v1.1.0 に更新  

## [VCC_1.1.1] - 2026-06-09

### Changed

- 選択と出力に日時を含める変更

### Fixed

- 地域による小数点表記の違いに対応。選択と出力に日時を含める変更。
- `OutputLogLoader` を v1.1.1 に更新  

## [2.0.0-beta.1]
