using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// チャットのロジックを管理する
/// UI とは分離し、イベントで通知する
/// </summary>
public class ChatManager : MonoBehaviour
{
    public static ChatManager Instance { get; private set; }

    [Header("ユーザー設定")]
    [SerializeField] private string userId = "default_user";

    // チャットメッセージのリスト（表示用）
    private List<ChatMessage> messages = new List<ChatMessage>();

    // 送信中フラグ
    public bool IsSending { get; private set; }

    // Events
    public event Action<ChatMessage> OnMessageAdded;
    public event Action<bool> OnSendingStateChanged;
    public event Action<string> OnError;

    public string UserId
    {
        get => userId;
        set => userId = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 保存済み userId を読み込む
        if (PlayerPrefs.HasKey("UserId"))
            userId = PlayerPrefs.GetString("UserId");
    }
    
    private void Start()
    {
        // Initialize
        Debug.Log("[ChatManager] チャットマネージャー起動");

        // CharacterBubbleDisplay がなければ自動作成
        if (CharacterBubbleDisplay.Instance == null)
        {
            Debug.Log("[ChatManager] CharacterBubbleDisplay が見つかりません。自動作成します。");
            CreateCharacterBubbleDisplay();
        }
        else
        {
            Debug.Log("[ChatManager] CharacterBubbleDisplay が既に存在します。");
        }
    }

    /// <summary>
    /// キャラクター頭上の吹き出しシステムを自動作成
    /// </summary>
    private void CreateCharacterBubbleDisplay()
    {
        // キャラクターを探す
        CharacterBrain characterBrain = FindObjectOfType<CharacterBrain>();
        if (characterBrain == null)
        {
            Debug.LogError("[ChatManager] CharacterBrain が見つかりません。キャラクターをシーンに配置してください。");
            return;
        }

        // キャラクターのバウンディングボックスから高さを計算
        Bounds characterBounds = GetCharacterBounds(characterBrain.gameObject);
        float characterHeight = characterBounds.max.y - characterBounds.min.y;
        float headOffsetY = characterBounds.max.y + 0.3f; // 頭の上さらに 0.3 ユニット上

        Debug.Log($"[ChatManager] キャラクター位置: {characterBrain.transform.position}");
        Debug.Log($"[ChatManager] キャラクター高さ: {characterHeight}");
        Debug.Log($"[ChatManager] バウンディングボックス Max: {characterBounds.max}, Min: {characterBounds.min}");
        Debug.Log($"[ChatManager] 吹き出し オフセット Y: {headOffsetY}");

        // World Space Canvas を作成
        GameObject canvasObj = new GameObject("CharacterBubbleCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600, 200);
        // スケールを大きくして見やすくする（20倍）
        canvasRect.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        // GraphicRaycaster を追加
        canvasObj.AddComponent<GraphicRaycaster>();

        // BubblePanel (背景) を作成
        GameObject panelObj = new GameObject("BubblePanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.28f, 0.30f, 0.36f, 1f);
        
        // 初期状態では透明に
        Color panelColor = panelImage.color;
        panelColor.a = 0f;
        panelImage.color = panelColor;

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(600, 150);
        panelRect.anchoredPosition = Vector2.zero;

        LayoutElement panelLayout = panelObj.AddComponent<LayoutElement>();
        panelLayout.preferredWidth = 600;
        panelLayout.preferredHeight = 150;

        VerticalLayoutGroup verticalLayout = panelObj.AddComponent<VerticalLayoutGroup>();
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childControlWidth = true;
        verticalLayout.padding = new RectOffset(15, 15, 10, 10);
        verticalLayout.spacing = 5;

        // BubbleText を作成
        GameObject textObj = new GameObject("BubbleText");
        textObj.transform.SetParent(panelObj.transform, false);

        TextMeshProUGUI bubbleText = textObj.AddComponent<TextMeshProUGUI>();
        bubbleText.text = "吹き出しテスト";
        bubbleText.fontSize = 50;
        bubbleText.alignment = TextAlignmentOptions.TopLeft;
        bubbleText.color = Color.white;
        bubbleText.overflowMode = TextOverflowModes.Overflow;
        bubbleText.enableWordWrapping = true; // テキスト折り返し有効

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(600, 150);

        // テキストが自動的にサイズ調整されるよう LayoutElement を追加
        LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
        textLayout.preferredWidth = 600;
        textLayout.preferredHeight = -1; // -1 で自動サイズ
        textLayout.layoutPriority = 1;

        // 日本語フォントを設定
        TMP_FontAsset japaneseFont = FindJapaneseFont();
        if (japaneseFont != null)
        {
            bubbleText.font = japaneseFont;
            Debug.Log($"[ChatManager] 日本語フォント設定: {japaneseFont.name}");
            bubbleText.ForceMeshUpdate(true, true);
        }
        else
        {
            Debug.LogWarning("[ChatManager] 日本語フォントが見つかりません。デフォルトフォントを使用します。");
        }

        // CharacterBubbleDisplay を Canvas にアタッチ
        CharacterBubbleDisplay bubbleDisplay = canvasObj.AddComponent<CharacterBubbleDisplay>();
        bubbleDisplay.SetBubbleText(bubbleText);
        bubbleDisplay.SetBubbleBackground(panelImage);
        bubbleDisplay.SetLayoutElement(panelLayout);
        bubbleDisplay.SetCanvas(canvas);
        bubbleDisplay.SetCharacterHead(characterBrain.transform);
        bubbleDisplay.SetHeadOffsetY(headOffsetY);

        // キャラクター頭上に配置
        Vector3 headTopPos = characterBrain.transform.position + Vector3.up * headOffsetY;
        canvasObj.transform.position = headTopPos;
        
        Debug.Log($"[ChatManager] 吹き出し初期位置: {canvasObj.transform.position}");
        Debug.Log($"[ChatManager] CharacterBubbleDisplay を自動作成しました。");
    }

    /// <summary>
    /// キャラクターのバウンディングボックスを取得（子オブジェクトも含む）
    /// </summary>
    private Bounds GetCharacterBounds(GameObject character)
    {
        Bounds bounds = new Bounds(character.transform.position, Vector3.zero);
        bool hasBounds = false;

        // スキニングメッシュレンダラーをチェック
        foreach (var smr in character.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr.enabled)
            {
                if (!hasBounds)
                {
                    bounds = smr.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(smr.bounds);
                }
                Debug.Log($"[ChatManager] SkinnedMeshRenderer 見つかった: {smr.name}, Bounds: {smr.bounds}");
            }
        }

        // メッシュレンダラーをチェック
        foreach (var mr in character.GetComponentsInChildren<MeshRenderer>())
        {
            if (mr.enabled)
            {
                if (!hasBounds)
                {
                    bounds = mr.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(mr.bounds);
                }
                Debug.Log($"[ChatManager] MeshRenderer 見つかった: {mr.name}, Bounds: {mr.bounds}");
            }
        }

        // Collider をチェック
        foreach (var collider in character.GetComponentsInChildren<Collider>())
        {
            if (collider.enabled)
            {
                Bounds colliderBounds = collider.bounds;
                if (!hasBounds)
                {
                    bounds = colliderBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(colliderBounds);
                }
                Debug.Log($"[ChatManager] Collider 見つかった: {collider.name}, Bounds: {colliderBounds}");
            }
        }

        if (!hasBounds)
        {
            Debug.LogWarning("[ChatManager] キャラクターのバウンディングボックスが見つかりません。デフォルト値を使用します。");
            bounds = new Bounds(character.transform.position, new Vector3(1, 2, 1));
        }

        return bounds;
    }

    /// <summary>
    /// 日本語フォントを探す
    /// </summary>
    private TMP_FontAsset FindJapaneseFont()
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var f in fonts)
        {
            string name = f.name.ToLower();
            if (name.Contains("noto") || name.Contains("japanese") || name.Contains("jp"))
                return f;
        }
        return null;
    }

    /// <summary>
    /// Send message to AI
    /// </summary>
    public new void SendMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || IsSending) return;

        // ユーザーメッセージを追加
        var userMsg = new ChatMessage
        {
            role = "user",
            content = text,
            timestamp = DateTime.Now
        };
        messages.Add(userMsg);
        OnMessageAdded?.Invoke(userMsg);

        // 送信状態を変更
        IsSending = true;
        OnSendingStateChanged?.Invoke(true);

        // API に送信
        SendChatRequest(text);
    }
    
    private void SendChatRequest(string message)
    {
        // API に送信
        ApiClient.Instance.SendChat(
            message,
            userId,
            onSuccess: (response) =>
            {
                HandleChatResponse(response);
                IsSending = false;
                OnSendingStateChanged?.Invoke(false);
            },
            onError: (error) =>
            {
                OnError?.Invoke(error);
                IsSending = false;
                OnSendingStateChanged?.Invoke(false);
            }
        );
    }

    private void HandleChatResponse(ChatResponse response)
    {
        Debug.Log($"[ChatManager] HandleChatResponse - メッセージ: {response.response}");

        // AI の応答を追加
        var aiMsg = new ChatMessage
        {
            role = "assistant",
            content = response.response,
            timestamp = DateTime.Now,
            modelUsed = response.model_used,
            processingTime = response.processing_time,
            contextUsed = response.context_used
        };
        messages.Add(aiMsg);
        OnMessageAdded?.Invoke(aiMsg);

        // キャラクター頭上の吹き出しに表示
        if (CharacterBubbleDisplay.Instance != null)
        {
            Debug.Log("[ChatManager] CharacterBubbleDisplay.Instance が見つかりました。ShowBubble を実行します。");
            CharacterBubbleDisplay.Instance.ShowBubble(response.response);
        }
        else
        {
            Debug.LogError("[ChatManager] CharacterBubbleDisplay.Instance が null です");
        }
    }

    /// <summary>
    /// ユーザーID を保存する
    /// </summary>
    public void SaveUserId()
    {
        PlayerPrefs.SetString("UserId", userId);
        PlayerPrefs.Save();
    }
}

/// <summary>
/// チャットメッセージのデータ
/// </summary>
[Serializable]
public class ChatMessage
{
    public string role;       // "user" or "assistant"
    public string content;
    public DateTime timestamp;
    public string modelUsed;
    public float processingTime;
    public bool contextUsed;
}