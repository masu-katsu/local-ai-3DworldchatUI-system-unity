using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 個々のメッセージバブルを表示し、
/// Viewportの横幅に合わせてバブルサイズを自動調整する。
/// </summary>
public class MessageBubble : MonoBehaviour
{
    [Header("表示要素")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("吹き出し")]
    [SerializeField] private RectTransform bubblePanel;
    [SerializeField] private LayoutElement bubbleLayoutElement;

    [Header("サイズ")]
    [SerializeField] private float minBubbleWidth = 100f;

    [Range(0.1f, 1f)]
    [SerializeField] private float maxWidthRatio = 0.75f;

    [SerializeField] private float horizontalPadding = 22f;
    [SerializeField] private float verticalPadding = 15f;

    // Content
    private RectTransform contentRect;

    // Viewport
    private RectTransform viewportRect;

    // 前回確認したViewport横幅
    private float lastViewportWidth = -1f;

    // Layout再計算の多重実行防止
    private bool isUpdatingLayout = false;


    private void Awake()
    {
        CacheLayoutReferences();
    }


    private void Start()
    {
        RecalculateBubbleSize();
    }


    /// <summary>
    /// Gameビューのサイズ変更などで
    /// Viewport横幅が変わった場合だけ再計算する。
    /// </summary>
    private void LateUpdate()
    {
        if (!isActiveAndEnabled)
            return;

        if (viewportRect == null)
        {
            CacheLayoutReferences();
        }

        if (viewportRect == null)
            return;

        float currentWidth = viewportRect.rect.width;

        if (Mathf.Abs(
            currentWidth - lastViewportWidth
        ) > 0.5f)
        {
            RecalculateBubbleSize();
        }
    }


    /// <summary>
    /// メッセージを設定する。
    /// </summary>
    public void SetMessage(ChatMessage message)
    {
        if (message == null)
        {
            Debug.LogError(
                "[MessageBubble] ChatMessageがnullです。",
                this
            );
            return;
        }

        if (messageText == null)
        {
            Debug.LogError(
                "[MessageBubble] Message Textが未設定です。",
                this
            );
            return;
        }

        messageText.text =
            CleanMessage(message.content);

        messageText.ForceMeshUpdate();

        Canvas.ForceUpdateCanvases();

        RecalculateBubbleSize();
    }


    /// <summary>
    /// ContentとViewportの参照を取得する。
    /// </summary>
    private void CacheLayoutReferences()
    {
        // MessageBubbleルートの親 = Content
        contentRect =
            transform.parent as RectTransform;

        if (contentRect == null)
        {
            viewportRect = null;
            return;
        }

        // Contentの親 = Viewport
        viewportRect =
            contentRect.parent as RectTransform;
    }


    /// <summary>
    /// Viewportの横幅を基準に
    /// Bubbleの幅・高さを計算する。
    /// </summary>
    private void RecalculateBubbleSize()
    {
        if (isUpdatingLayout)
            return;

        if (messageText == null)
            return;

        if (bubblePanel == null)
        {
            Debug.LogWarning(
                "[MessageBubble] Bubble Panelが未設定です。",
                this
            );
            return;
        }

        if (bubbleLayoutElement == null)
        {
            Debug.LogWarning(
                "[MessageBubble] Bubble Layout Elementが未設定です。",
                this
            );
            return;
        }

        if (contentRect == null ||
            viewportRect == null)
        {
            CacheLayoutReferences();
        }

        if (contentRect == null)
        {
            Debug.LogWarning(
                "[MessageBubble] Contentを取得できません。",
                this
            );
            return;
        }

        if (viewportRect == null)
        {
            Debug.LogWarning(
                "[MessageBubble] Viewportを取得できません。",
                this
            );
            return;
        }

        isUpdatingLayout = true;

        try
        {
            Canvas.ForceUpdateCanvases();


            // ========================================
            // Viewport横幅
            // ========================================

            float viewportWidth =
                viewportRect.rect.width;

            if (viewportWidth <= 0f)
                return;

            lastViewportWidth =
                viewportWidth;


            // ========================================
            // 左右の固定余白
            // ========================================

            const float edgeMargin = 10f;

            float availableWidth =
                viewportWidth -
                edgeMargin * 2f;

            if (availableWidth <= 0f)
                return;


            // ========================================
            // Bubble最大横幅
            // ========================================

            float maxBubbleWidth =
                Mathf.Max(
                    minBubbleWidth,
                    availableWidth * maxWidthRatio
                );


            // ========================================
            // 改行しない場合の自然なテキスト幅
            // ========================================

            Vector2 naturalSize =
                messageText.GetPreferredValues(
                    messageText.text,
                    Mathf.Infinity,
                    Mathf.Infinity
                );


            // ========================================
            // Bubble横幅
            // ========================================

            float bubbleWidth =
                Mathf.Clamp(
                    naturalSize.x +
                    horizontalPadding * 2f,

                    minBubbleWidth,
                    maxBubbleWidth
                );


            // ========================================
            // Bubble内部で文字が使える幅
            // ========================================

            float textWidth =
                Mathf.Max(
                    1f,
                    bubbleWidth -
                    horizontalPadding * 2f
                );


            // ========================================
            // 折り返し後のテキストサイズ
            // ========================================

            Vector2 wrappedSize =
                messageText.GetPreferredValues(
                    messageText.text,
                    textWidth,
                    Mathf.Infinity
                );


            // ========================================
            // Bubble高さ
            // ========================================

            float bubbleHeight =
                wrappedSize.y +
                verticalPadding * 2f;


            // ========================================
            // LayoutElementへ通知
            // ========================================

            bubbleLayoutElement.minWidth =
                bubbleWidth;

            bubbleLayoutElement.preferredWidth =
                bubbleWidth;

            bubbleLayoutElement.flexibleWidth =
                0f;

            bubbleLayoutElement.minHeight =
                bubbleHeight;

            bubbleLayoutElement.preferredHeight =
                bubbleHeight;

            bubbleLayoutElement.flexibleHeight =
                0f;


            // ========================================
            // Layout再構築
            // ========================================

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                bubblePanel
            );

            RectTransform rootRect =
                transform as RectTransform;

            if (rootRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    rootRect
                );
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                contentRect
            );
        }
        finally
        {
            isUpdatingLayout = false;
        }
    }


    /// <summary>
    /// AIモデル由来の特殊トークンを削除する。
    /// </summary>
    private static string CleanMessage(
        string message
    )
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        return message
            .Replace("<|assistant|>", "")
            .Replace("<|user|>", "")
            .Replace("<|system|>", "")
            .Replace("<|im_start|>", "")
            .Replace("<|im_end|>", "")
            .Trim();
    }
}