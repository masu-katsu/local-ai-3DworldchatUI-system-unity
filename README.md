# Local AI 3D World Chat UI System

Unityベースの3DチャットUIシステムです。AIと対話できる自律的な3Dキャラクターを備えています。

## 概要

このプロジェクトは、Unity 2022.3.62f3で構築された3Dチャットインターフェースシステムです。ユーザーは3D空間内で自律的に行動するキャラクターと対話することができます。

## 主な機能

### キャラクター機能
- **自律的な行動**: キャラクターは待機（wait）、移動（walk）、見渡し（look）、座る（sit）の4つの行動を自動的に切り替えます
- **自然な動き**: 行動の重み付けとランダム性により、自然な動きを実現しています
- **ナビゲーション**: NavMeshを使用してシーン内を自律的に移動します
- **アニメーション**: 各行動に対応したアニメーションを再生します

### チャット機能
- **AI対話**: バックエンドAPIを通じてAIと対話できます
- **吹き出し表示**: キャラクターの頭上に吹き出しを表示してメッセージを伝えます
- **チャット履歴**: チャットの履歴を管理・表示します
- **ユーザーID管理**: ユーザーごとの対話履歴を保存できます

### UI機能
- **チャットUI**: テキスト入力とメッセージ表示のUI
- **設定UI**: サーバーURL、APIキーなどの設定画面
- **モバイル対応**: モバイルデバイスでの入力をサポート

## システムアーキテクチャ

### バックエンド
バックエンドシステムには以下のリポジトリを使用しています：
- **[masu-katsu/local-ai-unity-system](https://github.com/masu-katsu/local-ai-unity-system)**

バックエンドの詳細な説明、セットアップ手順、API仕様については上記URLを参照してください。

### フロントエンド構成

```
Assets/
├── Scripts/
│   ├── API/              # バックエンドとの通信
│   │   ├── ApiClient.cs  # FastAPIサーバーとの通信クラス
│   │   └── ApiModels.cs  # リクエスト/レスポンスモデル
│   ├── Character/        # キャラクター制御
│   │   ├── CharacterBrain.cs      # 行動決定ロジック
│   │   ├── AnimationController.cs # アニメーション制御
│   │   ├── MovementController.cs  # 移動制御
│   │   └── CharacterStateMachine.cs # 状態管理
│   ├── Chat/             # チャット機能
│   │   └── ChatManager.cs         # チャットロジック管理
│   ├── Core/             # コアシステム
│   │   ├── AutonomousPlayBootstrap.cs # シーンセットアップ
│   │   └── UIManager.cs           # UI管理
│   ├── Navigation/       # ナビゲーション
│   │   ├── Waypoint.cs           # ウェイポイント
│   │   └── WaypointManager.cs     # ウェイポイント管理
│   ├── UI/               # UIコンポーネント
│   │   ├── ChatUIManager.cs       # チャットUI
│   │   ├── CharacterBubbleDisplay.cs # 吹き出し表示
│   │   └── SettingsUIManager.cs  # 設定UI
│   └── Network/          # ネットワーク監視
│       └── NetworkMonitor.cs
└── Editor/               # エディタ拡張
    ├── SceneBuilder.cs
    └── AutonomousCharacterSetup.cs
```

## セットアップ手順

### 前提条件
- Unity 2022.3.62f3
- バックエンドサーバー（[local-ai-unity-system](https://github.com/masu-katsu/local-ai-unity-system)）

### 手順

1. **バックエンドのセットアップ**
   - [masu-katsu/local-ai-unity-system](https://github.com/masu-katsu/local-ai-unity-system) をクローン
   - READMEに従ってバックエンドサーバーを起動
   - デフォルトでは `http://localhost:8000` で起動

2. **Unityプロジェクトのセットアップ**
   - このリポジトリをクローン
   - Unityでプロジェクトを開く
   - 必要なパッケージが自動的にインストールされます

3. **キャラクターモデルの設定**
   - エディタメニューから `3D model/Build` を選択
   - または `AutonomousPlayBootstrap` コンポーネントの `ManualSetupWorld()` を実行
   - キャラクター、床、ウェイポイントが自動的にセットアップされます

4. **サーバー接続設定**
   - Unityエディタで実行
   - 設定UIからサーバーURLとAPIキーを設定
   - デフォルト: `http://localhost:8000`

## 使用方法

### キャラクターのセットアップ
1. Unityエディタでシーンを開く
2. `3D model/Build` メニューを選択してシーンをセットアップ
3. Playモードを開始するとキャラクターが自動的に行動を開始します

### チャットの使用
1. Playモードで実行
2. チャットUIにメッセージを入力
3. 送信するとAIの応答がキャラクターの吹き出しに表示されます

### 設定の変更
- 設定UIから以下を変更できます：
  - サーバーURL
  - APIキー
  - ユーザーID

## キャラクターの行動パラメータ

`CharacterBrain` コンポーネントで以下のパラメータを調整できます：

- **待機時間**: waitDurationMin, waitDurationMax
- **見渡し時間**: lookDurationMin, lookDurationMax
- **座る時間**: sitDurationMin, sitDurationMax
- **行動の重み**: 各行動後の次の行動の選択確率
- **自然さ**: repeatPenalty（同じ行動の繰り返しを避ける）、extraPauseChance（追加のポーズ）

## API通信

### エンドポイント
- `POST /api/chat`: チャットメッセージを送信
- `GET /api/health`: サーバーのヘルスチェック

### リクエスト形式
```json
{
  "message": "ユーザーメッセージ",
  "user_id": "ユーザーID",
  "web_search_confirmed": false,
  "web_search_action": null
}
```

### レスポンス形式
```json
{
  "response": "AIの応答",
  "model_used": "使用モデル",
  "processing_time": 1.23,
  "context_used": true,
  "web_search_used": false,
  "requires_confirmation": false,
  "pending_web_search": "",
  "search_in_progress": false
}
```

## 技術スタック

- **Unity**: 2022.3.62f3
- **TextMeshPro**: テキストレンダリング
- **NavMesh**: ナビゲーションシステム
- **UnityWebRequest**: HTTP通信
- **FastAPI**: バックエンドAPI（別リポジトリ）

## 注意事項

- バックエンドサーバーが起動している必要があります
- キャラクターモデルは `Assets/untitled.fbx` に配置されています
- ウェイポイントは自動的に10箇所生成されます
- 日本語フォントが必要です（TextMeshProの日本語フォント）

## ライセンス

このプロジェクトのライセンスについては、別途確認してください。

## 貢献

バグ報告や機能リクエストはIssueにてお願いします。

## 連絡先

詳細についてはバックエンドリポジトリ [masu-katsu/local-ai-unity-system](https://github.com/masu-katsu/local-ai-unity-system) を参照してください。
