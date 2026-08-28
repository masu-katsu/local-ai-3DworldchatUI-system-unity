using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// チャットUI管理 - 極限シンプル版
/// </summary>
public class ChatUIManager : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField] private Transform messageContainer;   // Content (VLG付き)
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private GameObject typingIndicator;
    [SerializeField] private TextMeshProUGUI statusText;
    [Header("メッセージPrefab")]
    [SerializeField] private GameObject userMessagePrefab;
    [SerializeField] private GameObject aiMessagePrefab;
    private MessageBubble activeAiBubble;




    [Header("Chat表示切り替え")]
    [SerializeField] private GameObject messageArea;      // Chat履歴エリア全体
    [SerializeField] private Toggle chatToggle;          // Chat表示/非表示ボタン
    [SerializeField] private Button chatButton;          // Chat表示ボタン（Toggleの代替）

    [Header("接続状態")]
    [SerializeField] private Image connectionIndicator;
    [SerializeField] private Color connectedColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color disconnectedColor = new Color(0.8f, 0.2f, 0.2f);

    // カラー定義
    private static readonly Color USER_BUBBLE_COLOR = new Color(0.22f, 0.35f, 0.55f);
    private static readonly Color AI_BUBBLE_COLOR = new Color(0.28f, 0.30f, 0.36f);
    private static readonly Color TEXT_COLOR = Color.white;

    // フォント
    private TMP_FontAsset japaneseFont;

    private void Awake()
    {
        // Inspector の参照が外れても、Build Chat Scene 既定の Hierarchy 名なら復旧する
        WireMissingReferencesFromHierarchy();
    }

    private void WireMissingReferencesFromHierarchy()
    {
        Transform root = transform;
        if (messageContainer == null)
        {
            var t = root.Find("MessageArea/ScrollView/Viewport/Content");
            if (t != null) messageContainer = t;
        }
        if (scrollRect == null)
        {
            var t = root.Find("MessageArea/ScrollView");
            if (t != null) scrollRect = t.GetComponent<ScrollRect>();
        }
        if (inputField == null)
        {
            var t = root.Find("InputArea/InputField");
            if (t != null) inputField = t.GetComponent<TMP_InputField>();
        }
        if (sendButton == null)
        {
            var t = root.Find("InputArea/SendButton");
            if (t != null) sendButton = t.GetComponent<Button>();
        }
        if (typingIndicator == null)
        {
            var t = root.Find("MessageArea/TypingIndicator");
            if (t != null) typingIndicator = t.gameObject;
        }
        if (statusText == null)
        {
            var t = root.Find("MessageArea/StatusText");
            if (t != null) statusText = t.GetComponent<TextMeshProUGUI>();
        }
        if (connectionIndicator == null)
        {
            var t = root.Find("Header/ConnectionIndicator");
            if (t != null) connectionIndicator = t.GetComponent<Image>();
        }
        if (messageArea == null)
        {
            var t = root.Find("MessageArea");
            if (t != null) messageArea = t.gameObject;
        }
        if (chatButton == null)
        {
            var t = root.Find("ChatButton");
            if (t != null) chatButton = t.GetComponent<Button>();
        }
    }

    private void Start()
    {
        japaneseFont = FindJapaneseFont();
        Debug.Log($"[ChatUI] フォント: {(japaneseFont != null ? japaneseFont.name : "なし")}");

        if (sendButton == null || inputField == null)
        {
            Debug.LogError("[ChatUI] SendButton または InputField が未設定です。Canvas がルートで、InputArea/InputField と InputArea/SendButton があるか確認するか、LocalAI → Auto Wire References を実行してください。");
            return;
        }

        if (ChatManager.Instance == null)
        {
            Debug.LogError("[ChatUI] ChatManager が見つかりません。シーンに [GameManager]（ChatManager 付き）を置いてください。");
            return;
        }

        sendButton.onClick.AddListener(OnSendClicked);
        inputField.onSubmit.AddListener((_) => OnSendClicked());

        ChatManager.Instance.OnMessageAdded += OnMessageAdded;
            ChatManager.Instance.OnAssistantTextAppended += OnAssistantTextAppended;
        ChatManager.Instance.OnSendingStateChanged += OnSendingStateChanged;
        ChatManager.Instance.OnError += OnError;

        if (typingIndicator != null)
            typingIndicator.SetActive(false);

        // Chatボタンが Inspector で設定されていれば表示切り替えを配線する
        // （Canvas のレイアウト・色を実行時に書き換える処理は削除済み）
        if (chatButton != null)
            chatButton.onClick.AddListener(ToggleChatArea);

        CheckConnection();
    }

    private void OnDestroy()
    {
        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.OnMessageAdded -= OnMessageAdded;
                        ChatManager.Instance.OnAssistantTextAppended -= OnAssistantTextAppended;
            ChatManager.Instance.OnSendingStateChanged -= OnSendingStateChanged;
            ChatManager.Instance.OnError -= OnError;
        }
    }

    // ========== 送信 ==========

    private void OnSendClicked()
    {
        if (inputField == null || ChatManager.Instance == null) return;

        string text = inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        inputField.text = "";
        inputField.ActivateInputField();

        ChatManager.Instance.SendMessage(text);
    }

    // ========== メッセージ表示 ==========

    private void OnMessageAdded(ChatMessage message)
    {
        Debug.Log(
            $"[ChatUI] OnMessageAdded: role={message.role}, content={message.content}"
        );

        if (messageContainer == null)
        {
            Debug.LogError("[ChatUI] messageContainer が null！");
            return;
        }

        AddBubble(message);
    }

    private void OnAssistantTextAppended(string text)
    {
        if (activeAiBubble == null) return;
        activeAiBubble.AppendMessageText(text);
        Canvas.ForceUpdateCanvases();
        if (messageContainer is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// バブルを追加
    /// 構造: Content(VLG) → Row(LayoutElement のみ・高さ＝吹き出し) → Bubble(アンカーで左右寄せ) → Text
    /// 行に HLG を付けない（VLG+CSF とネストすると行高が潰れてメッセージが縦に重なることがある）
    /// </summary>
    /// <summary>
    /// TMP が本文中の &lt; &gt; をリッチタグと誤解して描画が壊れないよう、本文だけ noparse で包む
    /// </summary>

    private void AddBubble(ChatMessage message)
    {
        if (message == null)
        {
            Debug.LogError("[ChatUI] ChatMessage が null です。");
            return;
        }

        bool isUser = message.role == "user";

        GameObject prefab =
            isUser ? userMessagePrefab : aiMessagePrefab;

        if (prefab == null)
        {
            Debug.LogError(
                isUser
                    ? "[ChatUI] UserMessagePrefab が未設定です。"
                    : "[ChatUI] AIMessagePrefab が未設定です。"
            );
            return;
        }

        // PrefabをContent直下に生成
        GameObject instance = Instantiate(
            prefab,
            messageContainer,
            false
        );

     instance.name =
            isUser ? "UserMessageBubble" : "AIMessageBubble";

        // Prefabに付いているMessageBubbleを取得
        MessageBubble bubble =
            instance.GetComponent<MessageBubble>();

        if (bubble == null)
        {
            Debug.LogError(
                $"[ChatUI] {instance.name} に MessageBubble.cs がありません。",
                instance
            );

            Destroy(instance);
            return;
        }

        // 本文・サイズを設定
        bubble.SetMessage(message);
        if (!isUser)
            activeAiBubble = bubble;

        // Contentのレイアウトを再計算
        Canvas.ForceUpdateCanvases();

        RectTransform contentRT =
            messageContainer as RectTransform;

        if (contentRT != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                contentRT
            );
        }

        // 一番下へスクロール
        StartCoroutine(ScrollToBottom());

        Debug.Log(
            $"[ChatUI] Prefabからバブル追加: " +
            $"role={message.role}, prefab={prefab.name}"
        );
    }

    private IEnumerator ScrollToBottom()
    {
        // ContentSizeFitter が 1 フレーム遅れることがあるので再レイアウトしてからスクロール
        yield return null;
        var contentRT = messageContainer as RectTransform;
        if (contentRT != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        yield return null;
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    // ========== 状態表示 ==========

    private void OnSendingStateChanged(bool isSending)
    {
        if (sendButton != null) sendButton.interactable = !isSending;
        if (typingIndicator != null) typingIndicator.SetActive(isSending);
        if (statusText != null)
            statusText.text = isSending ? "応答待ち..." : "";
    }

    private void OnError(string error)
    {
        Debug.LogError($"[ChatUI] エラー: {error}");
        if (statusText != null)
        {
            statusText.text = "エラー: " + error;
            statusText.color = new Color(1f, 0.4f, 0.4f);
        }
    }

    // ========== 接続チェック ==========

    private void CheckConnection()
    {
        StartCoroutine(CheckConnectionCoroutine());
    }

    private IEnumerator CheckConnectionCoroutine()
    {
        var apiClient = ApiClient.Instance;
        if (apiClient == null)
        {
            Debug.LogError("[ChatUI] ApiClient が見つかりません");
            UpdateConnectionIndicator(false);
            yield break;
        }

        bool done = false;
        bool connected = false;

        apiClient.CheckHealth(
            onSuccess: (res) => { connected = true; done = true; },
            onError: (err) => { connected = false; done = true; }
        );

        while (!done) yield return null;

        UpdateConnectionIndicator(connected);
    }

    private void UpdateConnectionIndicator(bool isConnected)
    {
        if (connectionIndicator != null)
            connectionIndicator.color = isConnected ? connectedColor : disconnectedColor;
    }

    // ========== Chat表示/非表示 ==========

    /// <summary>
    /// Chat履歴エリアの表示/非表示を切り替え
    /// </summary>
    public void ToggleChatArea()
    {
        if (messageArea == null) return;

        bool isActive = messageArea.activeSelf;
        messageArea.SetActive(!isActive);
        Debug.Log($"[ChatUI] Chat履歴エリア: {(isActive ? "非表示" : "表示")}");
    }

    /// <summary>
    /// Chat履歴エリアを表示
    /// </summary>
    public void ShowChatArea()
    {
        if (messageArea != null)
        {
            messageArea.SetActive(true);
            Debug.Log("[ChatUI] Chat履歴エリアを表示");
        }
    }

    /// <summary>
    /// Chat履歴エリアを非表示
    /// </summary>
    public void HideChatArea()
    {
        if (messageArea != null)
        {
            messageArea.SetActive(false);
            Debug.Log("[ChatUI] Chat履歴エリアを非表示");
        }
    }

    // ========== 日本語フォント検出 ==========

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
}