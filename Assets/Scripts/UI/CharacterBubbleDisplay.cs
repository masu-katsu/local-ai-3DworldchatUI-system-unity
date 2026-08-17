using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// キャラクター頭上にAI返答の吹き出しを表示する
/// World Space Canvas を使用し、キャラクター頭上に追従
/// </summary>
public class CharacterBubbleDisplay : MonoBehaviour
{
    public static CharacterBubbleDisplay Instance { get; private set; }

    [Header("キャラクター参照")]
    [SerializeField] private Transform characterHead;  // キャラクターの頭のTransform
    [SerializeField] private float headOffsetY = 1.0f; // 頭から上への距離

    [Header("吹き出しUI")]
    [SerializeField] private Canvas worldSpaceCanvas;  // World Space Canvas
    [SerializeField] private TextMeshProUGUI bubbleText; // 吹き出し内のテキスト
    [SerializeField] private Image bubbleBackground;   // 背景Image
    [SerializeField] private LayoutElement layoutElement; // テキスト幅調整用

    [Header("表示設定")]
    [SerializeField] private Color bubbleColor = new Color(0.28f, 0.30f, 0.36f); // AI吹き出し色
    [SerializeField] private bool useFixedBubbleSize = true; // 手動設定したサイズをそのまま使う
    [SerializeField] private Vector2 fixedBubbleSize = new Vector2(2000f, 500f); // 手動で決めた吹き出しサイズ
    [SerializeField] private float maxWidth = 2000f;   // 吹き出しの最大幅（固定サイズ時はこの値に揃える）
    [SerializeField] private float displayDuration = 6f; // 表示時間（秒）
    [SerializeField] private float fadeOutDuration = 1f; // フェードアウト時間（秒）
    [SerializeField] private TMP_FontAsset japaneseFont; // 日本語フォント

    private CanvasGroup canvasGroup;
    private Coroutine currentDisplayCoroutine;

    private void Awake()
    {
        // シングルトン初期化
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Debug.Log("[CharacterBubbleDisplay] Awake - Instance初期化");
        Debug.Log($"[CharacterBubbleDisplay] Display Duration: {displayDuration}秒, Fade Out: {fadeOutDuration}秒");

        // 自動で World Space Canvas を探す
        if (worldSpaceCanvas == null)
            worldSpaceCanvas = GetComponentInParent<Canvas>();

        Debug.Log($"[CharacterBubbleDisplay] Canvas: {(worldSpaceCanvas != null ? worldSpaceCanvas.name : "未設定")}");

        // キャラクター参照が未設定の場合は探す
        if (characterHead == null)
        {
            var characterBrain = FindObjectOfType<CharacterBrain>();
            if (characterBrain != null)
                characterHead = characterBrain.transform;
        }

        Debug.Log($"[CharacterBubbleDisplay] CharacterHead: {(characterHead != null ? characterHead.name : "未設定")}");

        // CanvasGroup があれば使用、なければ作成
        canvasGroup = worldSpaceCanvas != null ? worldSpaceCanvas.GetComponent<CanvasGroup>() : null;
        if (canvasGroup == null && worldSpaceCanvas != null)
            canvasGroup = worldSpaceCanvas.gameObject.AddComponent<CanvasGroup>();

        Debug.Log($"[CharacterBubbleDisplay] CanvasGroup: {(canvasGroup != null ? "作成済み" : "未設定")}");

        // テキスト初期化
        if (bubbleText != null && japaneseFont != null)
            bubbleText.font = japaneseFont;

        Debug.Log($"[CharacterBubbleDisplay] BubbleText: {(bubbleText != null ? bubbleText.name : "未設定")}");

        // 背景色初期化
        if (bubbleBackground != null)
            bubbleBackground.color = bubbleColor;

        Debug.Log($"[CharacterBubbleDisplay] BubbleBackground: {(bubbleBackground != null ? bubbleBackground.name : "未設定")}");

        // 最初は非表示
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        // 吹き出しをキャラクター頭上に常に追従させる（表示中でなくても）
        if (worldSpaceCanvas != null && characterHead != null)
        {
            Vector3 headTopPos = characterHead.position + Vector3.up * headOffsetY;
            worldSpaceCanvas.transform.position = headTopPos;
        }
    }

    /// <summary>
    /// AIの返答を吹き出しに表示する
    /// </summary>
    public void ShowBubble(string message)
    {
        Debug.Log($"[CharacterBubbleDisplay] ShowBubble呼び出し: '{message}'");

        if (canvasGroup == null)
        {
            Debug.LogError("[CharacterBubbleDisplay] canvasGroup が null です");
            return;
        }

        if (bubbleText == null)
        {
            Debug.LogError("[CharacterBubbleDisplay] bubbleText が null です");
            return;
        }

        // 前の表示をキャンセル
        if (currentDisplayCoroutine != null)
            StopCoroutine(currentDisplayCoroutine);

        // テキストを設定
        bubbleText.text = message;
        
        // テキストメッシュを即座に更新
        bubbleText.ForceMeshUpdate(true, true);
        
        Debug.Log($"[CharacterBubbleDisplay] テキスト設定完了: '{bubbleText.text}'");
        Debug.Log($"[CharacterBubbleDisplay] キャラクター位置: {(characterHead != null ? characterHead.position.ToString() : "null")}");
        Debug.Log($"[CharacterBubbleDisplay] 吹き出し位置: {worldSpaceCanvas.transform.position}");

        // テキストの高さに応じて吹き出しのサイズを調整
        AdjustBubbleSizeToText();

        // 吹き出し表示コルーチンを開始
        currentDisplayCoroutine = StartCoroutine(DisplayBubbleCoroutine());
    }

    /// <summary>
    /// テキスト量に応じて吹き出しのサイズを動的に調整
    /// </summary>
    private void AdjustBubbleSizeToText()
    {
        if (bubbleText == null || bubbleBackground == null) return;

        RectTransform panelRect = bubbleBackground.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.pivot = new Vector2(0.5f, 0f);
        }

        if (useFixedBubbleSize)
        {
            if (panelRect != null)
                panelRect.sizeDelta = fixedBubbleSize;

            if (layoutElement != null)
            {
                layoutElement.preferredWidth = fixedBubbleSize.x;
                layoutElement.preferredHeight = fixedBubbleSize.y;
            }

            Debug.Log($"[CharacterBubbleDisplay] 固定サイズを適用: Width={fixedBubbleSize.x}, Height={fixedBubbleSize.y}");
            return;
        }

        // テキストを強制更新
        bubbleText.ForceMeshUpdate(true, true);

        // テキストの実際のサイズを取得
        RectTransform textRect = bubbleText.GetComponent<RectTransform>();
        if (textRect == null) return;

        // 推奨サイズを計算
        float preferredWidth = LayoutUtility.GetPreferredWidth(textRect);
        float preferredHeight = LayoutUtility.GetPreferredHeight(textRect);

        // パディングを追加
        float paddingH = 40f;
        float paddingV = 30f;

        // 最大幅を設定
        float maxBubbleWidth = maxWidth;
        float finalWidth = Mathf.Min(preferredWidth + paddingH, maxBubbleWidth);
        float finalHeight = preferredHeight + paddingV;

        // 背景パネルのサイズを更新
        if (panelRect != null)
        {
            panelRect.sizeDelta = new Vector2(finalWidth, finalHeight);
        }

        // LayoutElement を更新
        if (layoutElement != null)
        {
            layoutElement.preferredWidth = finalWidth;
            layoutElement.preferredHeight = finalHeight;
        }

        Debug.Log($"[CharacterBubbleDisplay] テキスト幅: {preferredWidth}, 高さ: {preferredHeight}");
        Debug.Log($"[CharacterBubbleDisplay] 吹き出し最終サイズ: Width={finalWidth}, Height={finalHeight}");
    }

    private IEnumerator DisplayBubbleCoroutine()
    {
        if (canvasGroup == null) yield break;

        Debug.Log("[CharacterBubbleDisplay] フェードイン開始");
        // フェードイン
        yield return StartCoroutine(FadeCanvasGroup(0f, 1f, 0.2f));

        Debug.Log($"[CharacterBubbleDisplay] 表示継続 ({displayDuration}秒)");
        // 表示継続
        yield return new WaitForSeconds(displayDuration);

        Debug.Log("[CharacterBubbleDisplay] フェードアウト開始");
        // フェードアウト
        yield return StartCoroutine(FadeCanvasGroup(1f, 0f, fadeOutDuration));

        Debug.Log("[CharacterBubbleDisplay] 吹き出し表示終了");
        currentDisplayCoroutine = null;
    }

    private IEnumerator FadeCanvasGroup(float fromAlpha, float toAlpha, float duration)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = toAlpha;
    }

    /// <summary>
    /// 頭上オフセット Y を設定
    /// </summary>
    public void SetHeadOffsetY(float offset)
    {
        headOffsetY = offset;
        Debug.Log($"[CharacterBubbleDisplay] HeadOffsetY設定: {headOffsetY}");
    }

    /// <summary>
    /// キャラクター頭部への参照を設定
    /// </summary>
    public void SetCharacterHead(Transform headTransform)
    {
        characterHead = headTransform;
    }

    /// <summary>
    /// World Space Canvas への参照を設定
    /// </summary>
    public void SetCanvas(Canvas canvas)
    {
        worldSpaceCanvas = canvas;
        if (canvasGroup == null)
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// 吹き出しテキストコンポーネントを設定
    /// </summary>
    public void SetBubbleText(TextMeshProUGUI text)
    {
        bubbleText = text;
        Debug.Log($"[CharacterBubbleDisplay] BubbleText設定: {(bubbleText != null ? bubbleText.name : "null")}");
        Debug.Log($"[CharacterBubbleDisplay] 設定されたフォント: {(bubbleText.font != null ? bubbleText.font.name : "null")}");
    }

    /// <summary>
    /// 吹き出し背景Image コンポーネントを設定
    /// </summary>
    public void SetBubbleBackground(Image background)
    {
        bubbleBackground = background;
        if (bubbleBackground != null)
            bubbleBackground.color = bubbleColor;
    }

    /// <summary>
    /// レイアウトエレメントを設定
    /// </summary>
    public void SetLayoutElement(LayoutElement layout)
    {
        layoutElement = layout;
    }
}
