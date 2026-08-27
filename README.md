# UI-Builder

操作ブロックをステージ上に配置し、配置したブロックを使ってプレイヤーをゴールまで導く2Dパズルアクションゲームです。

Unity WebGL向けに開発しています。

## 開発環境

- Unity 6000.1.0f1
- TextMesh Pro
- DOTween
- UniTask
- Git / Git LFS

Unity Hubから、このリポジトリのフォルダーを開いてください。

## ゲームの流れ

1. 画面上部にある操作ブロックをステージへ配置する
2. 必要なブロックをすべて配置する
3. スタートボタンを押す
4. 配置したブロックをクリックまたは長押ししてプレイヤーを操作する
5. トゲなどの障害物を避けてゴールを目指す

## 操作ブロック

| ブロック | 動作 |
| --- | --- |
| MoveR | 押している間、プレイヤーを右へ移動させる |
| MoveL | 押している間、プレイヤーを左へ移動させる |
| Jump | 接地中のプレイヤーをジャンプさせる |
| BGMScrollBar | BGM音量を変更する。足場としても使用できる |

## シーン構成

| シーン | 用途 |
| --- | --- |
| Title | タイトル画面 |
| StageSelect | ステージ選択画面 |
| Stage1〜Stage5 | プレイ可能なステージ |
| SandBox | 新しいブロックの開発・動作確認用 |

通常のゲーム用シーンは、以下の順番でBuild Settingsに登録されています。

1. Title
2. StageSelect
3. Stage1
4. Stage2
5. Stage3
6. Stage4
7. Stage5

`SandBox`は開発専用のため、Build Settingsには登録していません。

## ステージごとのブロック

| ステージ | 使用可能なブロック |
| --- | --- |
| Stage1 | MoveR、MoveL |
| Stage2 | MoveR、MoveL、Jump |
| Stage3 | MoveR、MoveL、Jump |
| Stage4 | MoveR、MoveL、BGMScrollBar |
| Stage5 | MoveR、MoveL、Jump、BGMScrollBar |

## 新しいブロックを追加するとき

新しいブロックをいきなり本番ステージへ追加せず、最初に`Assets/Scenes/SandBox.unity`へ追加して動作を確認してください。

確認項目：

- 上部パネルからドラッグできる
- グリッドに沿って配置できる
- ほかのブロックやステージと重ならない
- ゲーム開始後に正しく操作できる
- ポーズ、リトライ後も正常に動く
- PCとWebGLの両方で入力できる
- Consoleにエラーや警告が出ない

動作確認が終わってから、必要なステージへ反映してください。

### 関連ファイル

- ブロックの動作：`Assets/Scripts/block/`
- ブロックの配置・管理：`Assets/Scripts/BlockManager.cs`
- Inspector上のブロック設定：`Assets/Scripts/BlockManagerEditor.cs`
- 開発・テスト用シーン：`Assets/Scenes/SandBox.unity`

新しい種類を追加する場合は、スクリプトや画像だけでなく、`BlockManager`と`BlockManagerEditor`側の設定も確認してください。

## WebGLビルド

unityroom向けのビルドでは、次の設定を使用します。

- Development Build：オフ
- Compression Format：Gzip
- Decompression Fallback：オフ

## Git運用上の注意

UnityのシーンやPrefabを編集すると、意図していない差分が発生することがあります。コミット前に必ず変更内容を確認してください。

```powershell
git status
git diff --stat
```

新しいアセットを追加するときは、対応する`.meta`ファイルも一緒にコミットします。

フォントなど一部の大きなファイルにはGit LFSを使用しています。

## 現在の状態

- Stage1〜Stage5まで実装済み
- タイトル画面とステージ選択画面を実装済み
- unityroom上での動作確認実績あり
- SandBoxシーンによるブロック開発フローを導入済み
