using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 個々のメッセージバブルを表示し、
/// 文章量に合わせて横幅と高さを調整する。
/// </summary>
public class MessageBubble : MonoBehaviour
{
    [Header("表示要素")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI metaText;

    [Header("吹き出し")]
    [SerializeField] private RectTransform bubblePanel;
    [SerializeField] private Image bubbleBackground;
    [SerializeField] private LayoutElement bubbleLayoutElement;

    [Header("サイズ")]
    [SerializeField] private float minBubbleWidth = 100f;

    [Range(0.1f, 1f)]
    [SerializeField] private float maxWidthRatio = 0.78f;

    [SerializeField] private float horizontalPadding = 44f;
    [SerializeField] private float verticalPadding = 30f;

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

        // メッセージ本文
        messageText.text = CleanMessage(message.content);

        // 時刻
        if (timeText != null)
        {
            timeText.text = message.timestamp.ToString("HH:mm");
        }

        // AIだけモデル名・処理時間を表示
        if (metaText != null)
        {
            bool isAssistant = message.role == "assistant";

            metaText.gameObject.SetActive(isAssistant);

            if (isAssistant)
            {
                string meta =
                    $"{message.modelUsed} | " +
                    $"{message.processingTime:F1}s";

                if (message.contextUsed)
                    meta += " | 履歴参照";

                metaText.text = meta;
            }
        }

        // TMPとLayoutの計算を更新
        messageText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();

        UpdateBubbleSize();
    }

    /// <summary>
    /// 文章量に合わせて吹き出しサイズを変更する。
    /// </summary>
    private void UpdateBubbleSize()
    {
        Debug.Log("[MessageBubble] UpdateBubbleSize");
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

        RectTransform rootRect = transform as RectTransform;
        RectTransform parentRect = transform.parent as RectTransform;

        if (parentRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        float availableWidth = parentRect.rect.width;

        if (availableWidth <= 0f)
        {
            Debug.LogWarning(
                "[MessageBubble] 親Contentの横幅を取得できません。",
                this
            );
            return;
        }

        float maxBubbleWidth = Mathf.Max(
            minBubbleWidth,
            availableWidth * maxWidthRatio
        );

        // 改行しない場合の本来の横幅
        Vector2 naturalSize = messageText.GetPreferredValues(
            messageText.text,
            Mathf.Infinity,
            Mathf.Infinity
        );

        float bubbleWidth = Mathf.Clamp(
            naturalSize.x + horizontalPadding,
            minBubbleWidth,
            maxBubbleWidth
        );

        // 決定した横幅で折り返した場合の高さを計算
        float textWidth = Mathf.Max(
            1f,
            bubbleWidth - horizontalPadding
        );

        Debug.Log(
            $"[MessageBubble] availableWidth={availableWidth}, " +
            $"naturalWidth={naturalSize.x}, " +
            $"bubbleWidth={bubbleWidth}, " +
            $"textWidth={textWidth}",
            this
        );

        Vector2 wrappedTextSize = messageText.GetPreferredValues(
            messageText.text,
            textWidth,
            Mathf.Infinity
        );

        // 時刻・メタ情報の高さも含める
        float additionalHeight = GetAdditionalTextHeight();

        float bubbleHeight =
            wrappedTextSize.y +
            additionalHeight +
            verticalPadding;

        bubbleLayoutElement.minWidth = bubbleWidth;
        bubbleLayoutElement.preferredWidth = bubbleWidth;
        bubbleLayoutElement.flexibleWidth = 0f;

        bubbleLayoutElement.minHeight = bubbleHeight;
        bubbleLayoutElement.preferredHeight = bubbleHeight;
        bubbleLayoutElement.flexibleHeight = 0f;


        // MessageTextの横幅も確実に設定する
        RectTransform messageRect = messageText.rectTransform;

        messageRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            textWidth
        );

        LayoutRebuilder.ForceRebuildLayoutImmediate(bubblePanel);

        if (rootRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
    }
    

    /// <summary>
    /// 時刻とメタ情報に必要な高さを取得する。
    /// </summary>
    private float GetAdditionalTextHeight()
    {
        float height = 0f;

        if (timeText != null && timeText.gameObject.activeSelf)
        {
            height += timeText.preferredHeight;
        }

        if (metaText != null && metaText.gameObject.activeSelf)
        {
            height += metaText.preferredHeight;
        }

        return height;
    }

    /// <summary>
    /// AIモデル由来の特殊トークンを削除する。
    /// </summary>
    private static string CleanMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        return message
            .Replace("<|assistant|>", "")
            .Replace("<|user|>", "")
            .Replace("<|system|>", "")
            .Trim();
    }
}