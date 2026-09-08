# Hyakuashi Udon Motion Recorder

[English](../../README.md)

HUMR は、ユーザーの動きを VRChat のログファイルに記録し、Unity プロジェクト内で FBX としてエクスポートするモーションキャプチャツールです。これは新しいログ形式を使用する v2 です。

## 導入

> [!WARNING]
> インポート前に、v2.0.0 より前の `Packages/HUMR OutputLogLoader` と、`Assets/HUMR` 配下の `Prefabs`、`ReadMe`、`Scenes`、`Scripts` を削除してください。VPM から導入した場合は自動的に削除されます。

### 必須環境

- [PC 版 VRChat](https://store.steampowered.com/app/438100/VRChat/)
- [Unity 2022.3.22f1](https://unity.com/releases/editor/whats-new/2022.3.22f1)（6000.0.67f1 では FBX エクスポートが動作しません）
- FBX Exporter `>= 4.2.1`（Unity Registry から自動的に導入されます）

#### オプションパッケージ

- [VRChat SDK](https://creators.vrchat.com/sdk/) `>= 3.10.0`（カスタムワールドでの録画に必要です）

### Unity パッケージで導入

録画データの読み込みと `.fbx` へのエクスポートに VRChat SDK は必要ありません。VPM を使わない場合は、[リリースページから `.unitypackage`](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/releases/latest)をダウンロードし、Unity 2022.3.22f1 のプロジェクトにインポートしてください。

### VRChat Package Manager

[Sakuu 氏の VPM リスト](https://drsakuu.github.io/vpm-listing/)<a href="https://drsakuu.github.io/vpm-listing/"><img src="AddToVCC.png" alt="VCC に追加" height="24"></a>から導入してください。 （[ALCOM](https://vrc-get.anatawa12.com/alcom/) を使用）

## 使い方

### 録画

*詳しい手順: [Recording.md](../Recording/Recording.ja.md)*

> [!IMPORTANT]
> HUMR で録画するには、VRChat のデバッグ設定でログの出力を元全に設定する必要があります。

[公開ワールド](https://vrchat.com/home/launch?worldId=wrld_1fbb2fea-788e-43a8-a588-8ee7edf8e680)を利用するか、[VRChat ワールドプロジェクト](https://creators.vrchat.com/worlds/)に HumrPlayerRecorder prefab を追加してください。

![VRChat で HUMR を使ってアニメーションを録画](../Recording/HumrRecordingStart.gif)

### 読み込み

*詳しい手順: [Loading.md](../Loading/Loading.ja.md)*

人型アバターを持つ Animator に HumrRecordingLoader コンポーネントを追加してください。録画済みの VRChat ログファイルを選択し、録画データを `.fbx` または `.anim` としてエクスポートできます。

![Unity でアニメーションを読み込み](../Loading/HUMRLoading.gif)

### 詳細ガイド

アバターにアニメーションを適用する: [Avatar.md](../Avatar/Avatar.md)

カメラの動きを録画する: [Camera.md](../Camera/Camera.md)

## 更新履歴

[CHANGELOG.md](../CHANGELOG.md)

## コントリビューション

[Issues](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/issues) と [Pull requests](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/pulls) を歓迎します。v2.1 の予定は[こちら](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/issues/2)、v2.2 の予定は[こちら](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/issues/3)で確認できます。

## License

[MIT License](../../LICENSE.md)
