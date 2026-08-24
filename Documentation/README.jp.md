# Hyakuashi Udon Motion Recorder

[English](../README.md)

HUMR は、VRChat 上のプレイヤーの動きを VRChat のログファイルに記録し、Unity プロジェクト内で読み込むためのモーションキャプチャツールです。これは新しいログ形式を使う v2 です。

## 導入

> [!WARNING]
> インポート前に、古い `HUMR OutputLogLoader` パッケージと `Assets/HUMR` 配下の `Prefabs`、`ReadMe`、`Scenes`、`Scripts` を削除してください。VPM から導入した場合は自動で削除されます。

### 必須環境

- Unity 2022.3.22f1
- FBX Exporter =>4.2.1（インポート時に自動導入）
- VRChat World SDK =>3.10.0（録画用）

### VRChat Package Manager

Sakuu 氏の VPM リストから導入してください: <https://drsakuu.github.io/vpm-listing/>（[ALCOM](https://vrc-get.anatawa12.com/alcom/) を使用）

### その他

アニメーションの読み込みには VRChat SDK は不要です。VPM を使わない場合は、[releases](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/releases) から `.unitypackage` をダウンロードして、任意の Unity プロジェクトにインポートしてください。

## 使い方

### 録画

> [!IMPORTANT]
> HUMR を動作させるには、VRChat のデバッグ設定でログの出力を完全に設定する必要があります。

[公開ワールド](https://vrchat.com/home/launch?worldId=wrld_1fbb2fea-788e-43a8-a588-8ee7edf8e680) を利用するか、VRChat ワールドプロジェクトに HumrPlayerRecorder prefab を追加してください。公開ワールドはパッケージマネージャーのサンプルタブにある HUMR Sample World に含まれています。

ミラーのボタンを使って録画の開始と停止を行います。複数の録画は同じ出力ファイル内の take として分割されます。

記録するアバターのボーン構造と、モーションを読み込むアバターのボーン構造は完全に一致している必要があります。VRChat アバターの .fbx が手元にない場合は、VRChat SDK に含まれているサンプルロボットを使うとよいでしょう。Unity でアニメーションを別アバターへリターゲットできますし、Blender で Rokoko plugin のようなツールを使って手動でリターゲットすることもできます。

VRChat のログは約1週間後に削除されるため、保存したデータを読み込むか、別の場所にログファイルをコピーしておくことをおすすめします。

### 読み込み

Unity 2022.3.22f1 のプロジェクトに `drsakuu.humr` の UnityPackage をインポートし、Humanoid Avatar を持つ Animator に `HumrRecordingLoader` コンポーネントを追加してください。録画済みの VRChat ログファイルをリストから選択し、`.fbx` または `.anim` としてエクスポートしてください。

## 更新履歴

[CHANGELOG.md](../CHANGELOG.md)

## コントリビューション

[Issues](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/issues) と [Pull requests](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/pulls) を歓迎します。次にやることの参考として [TODO.md](TODO.md) もご確認ください。

## License

[MIT License](../LICENSE.md)
