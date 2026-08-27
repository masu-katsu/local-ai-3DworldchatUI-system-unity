# Local AI 3D World Chat UI System

Unityで動作する、3Dキャラクター付きのローカルAIチャットUIです。チャット入力、AI応答の履歴表示、キャラクター頭上の吹き出し表示、キャラクターの自律行動を提供します。

## 目的と構成

このプロジェクトはUnity側のフロントエンドです。AIの推論、会話履歴、RAGやWeb検索などのバックエンド処理は、次の別プロジェクトが担当します。

- バックエンド: [masu-katsu/local-ai-project-backend](https://github.com/masu-katsu/local-ai-project-backend)
- Unity側: 本リポジトリ
- 通信方式: Unity `UnityWebRequest` とFastAPIのHTTP API
- 現在の既定接続先: `http://100.81.92.14:8000`

`100.81.92.14`はTailscaleネットワーク内のアドレスです。別の利用者が使う場合は、同じIPを使うのではなく、自分のバックエンドURLを設定してください。

## 必要環境

- Unity `2022.3.62f3`
- バックエンドプロジェクトと、その依存関係
- Unityからバックエンドへ到達できるネットワーク
- 日本語を表示する場合は日本語対応のTextMesh Proフォント

## セットアップ

### 1. バックエンドを起動する

バックエンドリポジトリを取得し、リポジトリの手順に従ってFastAPIサーバーを起動します。

```text
https://github.com/masu-katsu/local-ai-project-backend
```

Unityからアクセスできるアドレスとポートで待ち受けていることを確認してください。まず、次のヘルスチェックが成功することを確認します。

```text
http://<バックエンドのIPまたはホスト名>:8000/api/health
```

### 2. Unityプロジェクトを開く

1. Unity `2022.3.62f3`で本プロジェクトを開く
2. `Assets/Scenes/SampleScene.unity`を開く
3. 必要なUnityパッケージのインポート完了を待つ
4. Playモードを開始する

このリポジトリでは、チャットUIとキャラクターを配置済みのシーンを使用します。以前存在したシーン自動生成用のEditorツールは削除済みです。

### 3. 接続先を設定する

Playモード中に設定画面を開き、次の項目を入力して保存します。

| 項目 | 内容 |
| --- | --- |
| サーバーURL | バックエンドのベースURL。例: `http://192.168.1.20:8000` |
| APIキー | バックエンドが要求するAPIキー。不要な場合は空欄 |
| ユーザーID | 会話を識別するID |

保存した値はUnityの`PlayerPrefs`に端末ごとに保存され、次回起動時に読み込まれます。初期値を変更する場合は [Assets/Scripts/API/ApiClient.cs](Assets/Scripts/API/ApiClient.cs) の`DefaultServerUrl`を変更し、シーンに保存されたURLも合わせて更新してください。

## 使い方

### チャット

1. 入力欄にメッセージを入力する
2. 送信ボタン、またはEnterキーで送信する
3. ユーザーのメッセージとAIの応答が履歴に追加される
4. AIの応答はキャラクター頭上の吹き出しにも表示される

送信中は二重送信を防ぐため送信ボタンが無効になり、入力エリアに処理中の状態が表示されます。応答には使用モデル名、処理時間、コンテキスト使用状態が含まれる場合があります。

### 設定

設定画面では次の値を変更できます。

- バックエンドURL
- APIキー
- ユーザーID
- 接続テスト

接続テストは設定画面で入力したURLを一時的に使ってヘルスチェックを行います。保存するまで永続設定は変更されません。

## キャラクターの自律行動

キャラクターの行動は [Assets/Scripts/Character/CharacterBrain.cs](Assets/Scripts/Character/CharacterBrain.cs) が決定します。現在の行動は次の4種類です。

- `Wait`: その場で待機
- `Walk`: Waypointへ移動
- `Look`: 周囲を見渡すアニメーション
- `Sit`: 座るアニメーション

行動は前の行動に応じた重みからランダムに選ばれます。Inspectorでは次の値を調整できます。

- `waitDurationMin` / `waitDurationMax`: 待機時間
- `lookDurationMin` / `lookDurationMax`: 見渡し時間
- `sitDurationMin` / `sitDurationMax`: 着席時間
- `weightsAfterWait` / `weightsAfterWalk` / `weightsAfterLook` / `weightsAfterSit`: 次の行動の重み
- `repeatPenalty`: 同じ行動が連続する確率を下げる係数
- `extraPauseChance`: 追加待機の確率
- `extraPauseMin` / `extraPauseMax`: 追加待機時間

`MovementController`はNavMeshを優先し、NavMeshを利用できない場合は平面移動へフォールバックします。`AnimationController`は状態をAnimatorの`Wait`、`Walk`、`Look`、`Sit`パラメータへ反映します。

## API通信

### ベースURL

```text
http://100.81.92.14:8000
```

これは既定値です。Tailscaleを使わない利用者は、自分のバックエンドのURLへ変更してください。

### チャット送信

```text
POST /api/chat
```

404の場合、Unity側は互換用に次のエンドポイントを1回試します。

```text
POST /unity/predict
```

リクエストには`X-API-Key`ヘッダーと、次のJSONを送信します。

```json
{
   "message": "ユーザーメッセージ",
   "user_id": "ユーザーID",
   "web_search_confirmed": false,
   "web_search_action": null
}
```

### ヘルスチェック

```text
GET /api/health
```

404の場合は`GET /health`へ再試行します。現在のバックエンド応答例は次の形式です。

```json
{
   "status": "running",
   "services": {
      "fastapi": "ok",
      "qwen": "ok",
      "phi3": "disabled"
   }
}
```

## ディレクトリ構成

```text
Assets/
├── Scenes/
│   └── SampleScene.unity       # 実行対象シーン
├── Scripts/
│   ├── API/                    # FastAPI通信とJSONモデル
│   ├── Chat/                   # チャット状態、履歴、応答処理
│   ├── Character/              # 行動決定、移動、Animator制御
│   ├── Core/                   # 実行時ワールド、NavMesh、行動開始
│   ├── Navigation/             # Waypoint管理
│   └── UI/                     # チャット、設定、吹き出し、入力補助
├── Prefabs/                   # ユーザー/AIメッセージPrefab
├── Animations/                # Animatorとアニメーション
└── Fonts/                     # フォントアセット
```

## 主なスクリプト

| スクリプト | 役割 |
| --- | --- |
| `ApiClient.cs` | チャットとヘルスチェックのHTTP通信、URL/APIキー保存 |
| `ApiModels.cs` | リクエスト・レスポンスのデータモデル |
| `ChatUIManager.cs` | 入力、送信、履歴表示、メッセージPrefab生成 |
| `ChatUIManager.cs`（Chatフォルダ） | チャット履歴、API応答、吹き出し通知 |
| `SettingsUIManager.cs` | URL、APIキー、ユーザーIDの設定画面 |
| `CharacterBrain.cs` | キャラクターの次の行動を決定 |
| `CharacterStateMachine.cs` | `Idle`、`Walking`、`Looking`、`Sitting`を管理 |
| `MovementController.cs` | NavMesh移動とフォールバック移動 |
| `AnimationController.cs` | 状態に応じたアニメーション再生 |
| `WaypointManager.cs` | ランダムな移動先を提供 |

## トラブルシューティング

### 接続できない

1. バックエンドが起動しているか確認する
2. Unity端末から`http://<IP>:8000/api/health`へアクセスできるか確認する
3. 設定画面のURLに`http://`とポート番号が含まれているか確認する
4. Tailscaleを使う場合、Unityを実行する端末が同じTailnetに接続しているか確認する
5. APIキーが必要なバックエンドでは正しいキーを入力する

### URLを変更しても戻る

設定画面で「保存」を押してください。保存値は`PlayerPrefs`に保持されます。Unity Editorで別の実行環境を確認する場合は、その環境のPlayerPrefsが別管理になることがあります。

### AIの吹き出しが表示されない

キャラクターに`CharacterBrain`が存在すること、シーンに`CharacterBubbleDisplay`が存在すること、日本語フォントが利用可能であることを確認してください。

### キャラクターが移動しない

Waypointが存在することを確認してください。NavMeshが利用できない場合はフォールバック移動を試みますが、キャラクターに`MovementController`と`NavMeshAgent`が必要です。

## ライセンス

このプロジェクトと使用アセットのライセンスは、それぞれの配布元の条件を確認してください。
