# 読み込み

[English](Loading.md)

まず、アニメーションを録画する必要があります：[Recording.md](../Recording/Recording.ja.md)

録画データの読み込みに VRChat SDK は必要ありません。VRChat Package Manager を使わない場合は、[リリースページから `.unitypackage`](https://github.com/DrSakuu/HyakuashiUdonMotionRecorder/releases)をダウンロードし、Unity 2022.3.22f1 の任意のプロジェクトにインポートしてください。

VRChat アバターでアニメーションを読み込む場合は、Prefab ではなく元の `.fbx` ファイルを使用することをおすすめします。それでも Prefab を使用する場合は、読み込み前に「Tools - Modular Avatar - Manual bake avatar」を実行してください。

人型アバターを持つ Animator に HumrRecordingLoader コンポーネントを追加してください。録画済みの VRChat ログファイルを選択し、録画データを `.fbx` または `.anim` としてエクスポートできます。

![Unity でアニメーションを読み込み](HUMRLoading.gif)

> [!NOTE]
> `.anim` ファイルは、アバターが Humanoid であっても Generic アニメーションです。アバターで再生する場合は、Animator の Avatar を一時的に None に設定してください。この問題は今後のリリースで修正される予定です。
