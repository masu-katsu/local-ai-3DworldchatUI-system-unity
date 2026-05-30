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
        ChatManager.Instance.OnSendingStateChanged += OnSendingStateChanged;
        ChatManager.Instance.OnError += OnError;

        if (typingIndicator != null)
            typingIndicator.SetActive(false);

        // UI レイアウトを自動配置
        AutoLayoutUI();

        CheckConnection();
    }

    /// <summary>
    /// UI要素を3D空間メインの配置に自動変更
    /// </summary>
    private void AutoLayoutUI()
    {
        Debug.Log("[ChatUI] UI自動レイアウト開始");

        // ===== MessageArea（Chat履歴）を右上に移動・縮小 =====
        if (messageArea != null)
        {
            RectTransform messageAreaRect = messageArea.GetComponent<RectTransform>();
            if (messageAreaRect != null)
            {
                messageAreaRect.anchorMin = new Vector2(1, 1); // 右上
                messageAreaRect.anchorMax = new Vector2(1, 1); // 右上
                messageAreaRect.pivot = new Vector2(1, 1); // 右上
                messageAreaRect.anchoredPosition = new Vector2(-20, -20); // 右から20px、上から20px
                messageAreaRect.sizeDelta = new Vector2(400, 600); // 幅400、高さ600
                messageAreaRect.localScale = Vector3.one;
                
                Debug.Log("[ChatUI] MessageArea を右上に配置しました");
            }

            // Content（メッセージコンテナ）に ContentSizeFitter を追加
            if (messageContainer != null)
            {
                ContentSizeFitter csf = messageContainer.GetComponent<ContentSizeFitter>();
                if (csf == null)
                    csf = messageContainer.gameObject.AddComponent<ContentSizeFitter>();
                
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                Debug.Log("[ChatUI] ContentSizeFitter を Content に設定");
            }

            // 初期状態では非表示
            messageArea.SetActive(false);
            Debug.Log("[ChatUI] Chat履歴エリアを初期非表示に設定");
        }

        // ===== InputArea（入力）を画面下部に配置 =====
        Transform inputArea = transform.Find("InputArea");
        if (inputArea != null)
        {
            RectTransform inputAreaRect = inputArea.GetComponent<RectTransform>();
            if (inputAreaRect != null)
            {
                inputAreaRect.anchorMin = new Vector2(0.5f, 0); // 下中央
                inputAreaRect.anchorMax = new Vector2(0.5f, 0); // 下中央
                inputAreaRect.pivot = new Vector2(0.5f, 0); // 下中央
                inputAreaRect.anchoredPosition = new Vector2(0, 20); // 下から20px
                inputAreaRect.sizeDelta = new Vector2(600, 100); // 幅600、高さ100
                inputAreaRect.localScale = Vector3.one;
                
                Debug.Log("[ChatUI] InputArea を下部に配置しました");
            }
        }

        // ===== Chatボタンを右上に作成 =====
        if (chatButton == null)
        {
            CreateChatButton();
        }
        else
        {
            RectTransform chatButtonRect = chatButton.GetComponent<RectTransform>();
            if (chatButtonRect != null)
            {
                chatButtonRect.anchorMin = new Vector2(1, 1); // 右上
                chatButtonRect.anchorMax = new Vector2(1, 1); // 右上
                chatButtonRect.pivot = new Vector2(1, 1); // 右上
                chatButtonRect.anchoredPosition = new Vector2(-20, -450); // 右から20px、上から450px（MessageAreaの下）
                chatButtonRect.sizeDelta = new Vector2(80, 80); // 幅80、高さ80
                
                Debug.Log("[ChatUI] Chatボタンを右上に配置しました");
            }
        }

        // ===== Canvas背景を透明化 =====
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            Image canvasImage = GetComponent<Image>();
            if (canvasImage != null)
            {
                Color bgColor = canvasImage.color;
                bgColor.a = 0f; // 完全透明
                canvasImage.color = bgColor;
                Debug.Log("[ChatUI] Canvas背景を透明化しました");
            }
        }

        Debug.Log("[ChatUI] UI自動レイアウト完了");
    }

    /// <summary>
    /// Chatボタンを動的に作成
    /// </summary>
    private void CreateChatButton()
    {
        Debug.Log("[ChatUI] Chatボタンを動的に作成");

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) return;

        // ボタンGameObjectを作成
        GameObject chatButtonObj = new GameObject("ChatButton");
        chatButtonObj.transform.SetParent(transform, false);

        Image buttonImage = chatButtonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        Button buttonComponent = chatButtonObj.AddComponent<Button>();
        buttonComponent.targetGraphic = buttonImage;

        // テキストを追加
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(chatButtonObj.transform, false);

        TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = "Chat";
        buttonText.fontSize = 36;
        buttonText.color = Color.white;
        buttonText.alignment = TextAlignmentOptions.Center;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // ボタン配置
        RectTransform buttonRect = chatButtonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1, 1); // 右上
        buttonRect.anchorMax = new Vector2(1, 1);
        buttonRect.pivot = new Vector2(1, 1);
        buttonRect.anchoredPosition = new Vector2(-20, -450);
        buttonRect.sizeDelta = new Vector2(80, 80);

        // ボタンクリック時の処理
        buttonComponent.onClick.AddListener(() => ToggleChatArea());

        chatButton = buttonComponent;
        Debug.Log("[ChatUI] Chatボタンを作成しました");
    }

    private void OnDestroy()
    {
        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.OnMessageAdded -= OnMessageAdded;
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
        Debug.Log($"[ChatUI] OnMessageAdded: role={message.role}, content={message.content}");

        if (messageContainer == null)
        {
            Debug.LogError("[ChatUI] messageContainer が null！");
            return;
        }

        bool isUser = message.role == "user";
        AddBubble(message.content, isUser, message.modelUsed, message.contextUsed);
    }

    /// <summary>
    /// バブルを追加
    /// 構造: Content(VLG) → Row(LayoutElement のみ・高さ＝吹き出し) → Bubble(アンカーで左右寄せ) → Text
    /// 行に HLG を付けない（VLG+CSF とネストすると行高が潰れてメッセージが縦に重なることがある）
    /// </summary>
    /// <summary>
    /// TMP が本文中の &lt; &gt; をリッチタグと誤解して描画が壊れないよう、本文だけ noparse で包む
    /// </summary>
    private static string TmpWrapBodyForRichSuffix(string body)
    {
        if (body == null) body = "";
        body = body.Replace("</noparse>", "");
        return "<noparse>" + body + "</noparse>";
    }

    private void AddBubble(string text, bool isUser, string modelUsed, bool contextUsed)
    {
        text = text ?? "";
        var rowObj = new GameObject(isUser ? "UserRow" : "AIRow");
        rowObj.transform.SetParent(messageContainer, false);
        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.layoutPriority = 1;

        var bubbleObj = new GameObject(isUser ? "UserBubble" : "AIBubble");
        bubbleObj.transform.SetParent(rowObj.transform, false);

        var img = bubbleObj.AddComponent<Image>();
        img.color = isUser ? USER_BUBBLE_COLOR : AI_BUBBLE_COLOR;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(bubbleObj.transform, false);
        var textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(14, 8);
        textRT.offsetMax = new Vector2(-14, -8);

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 18;
        tmp.color = TEXT_COLOR;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (japaneseFont != null) tmp.font = japaneseFont;

        string timeStr = System.DateTime.Now.ToString("HH:mm");
        string suffix = "\n<size=11><color=#A0A5B0>";
        if (!isUser && !string.IsNullOrEmpty(modelUsed))
        {
            suffix += modelUsed;
            if (contextUsed) suffix += " | 履歴参照";
            suffix += "  ";
        }
        suffix += timeStr + "</color></size>";
        string fullText = TmpWrapBodyForRichSuffix(text) + suffix;
        tmp.text = fullText;
        tmp.richText = true;

        float contentW = GetContentWidth();
        float maxOuterBubble = Mathf.Max(150f, contentW * 0.85f); // コンテンツ幅の85%
        float innerMax = Mathf.Max(50f, maxOuterBubble - 30f); // 左右パディング

        // バブル確定前の ForceMeshUpdate は矩形が仮のままになり、長文でメッシュが重畳することがある
        Vector2 pref = tmp.GetPreferredValues(fullText, innerMax, 0);
        float bubbleW = Mathf.Clamp(pref.x + 28f, 100f, maxOuterBubble); // 最小幅100
        float bubbleH = Mathf.Max(pref.y + 20f, 50f); // 最小高さ50

        Debug.Log($"[ChatUI] テキスト推奨サイズ: {pref}, バブル: {bubbleW}x{bubbleH}");

        var bubbleRT = bubbleObj.GetComponent<RectTransform>();
        const float padX = 10f;
        if (isUser)
        {
            bubbleRT.anchorMin = new Vector2(1f, 0.5f);
            bubbleRT.anchorMax = new Vector2(1f, 0.5f);
            bubbleRT.pivot = new Vector2(1f, 0.5f);
            bubbleRT.sizeDelta = new Vector2(bubbleW, bubbleH);
            bubbleRT.anchoredPosition = new Vector2(-padX, 0f);
        }
        else
        {
            bubbleRT.anchorMin = new Vector2(0f, 0.5f);
            bubbleRT.anchorMax = new Vector2(0f, 0.5f);
            bubbleRT.pivot = new Vector2(0f, 0.5f);
            bubbleRT.sizeDelta = new Vector2(bubbleW, bubbleH);
            bubbleRT.anchoredPosition = new Vector2(padX, 0f);
        }

        rowLE.minHeight = bubbleH + 10f;
        rowLE.preferredHeight = bubbleH + 10f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRT);
        tmp.ForceMeshUpdate(true);

        var contentRT = messageContainer as RectTransform;
        if (contentRT != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);

        StartCoroutine(ScrollToBottom());

        Debug.Log($"[ChatUI] バブル追加: isUser={isUser}, size={bubbleRT.rect.size}");
    }

    private float GetContentWidth()
    {
        var rt = messageContainer as RectTransform;
        if (rt != null && rt.rect.width > 0)
            return rt.rect.width;
        // フォールバック: 画面幅の推定
        return Screen.width;
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
