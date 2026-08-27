using UnityEngine;

/// <summary>
/// 背景用のQuadを、カメラの視野角(FieldOfView)とアスペクト比(画面の縦横比)に合わせて
/// 自動的にスケーリングするスクリプト。
/// 
/// 使い方:
/// 1. このスクリプトを背景用のQuadオブジェクトにアタッチする
/// 2. Inspectorで targetCamera に Main Camera をセットする(未設定ならCamera.mainを自動使用)
/// 3. distanceFromCamera に、Quadを置きたいカメラからの距離を入れる
///    (前後にキャラクターなどが入る場合は、それより奥の距離にすること)
/// 
/// これにより、Android/Windows/解像度変更/画面回転などで画面のアスペクト比が変わっても、
/// Quadが常にカメラの視界いっぱいに収まるようにスケールを自動調整します。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class BackgroundQuadFitter : MonoBehaviour
{
    [Tooltip("基準にするカメラ。未設定の場合は Camera.main を使用します。")]
    public Camera targetCamera;

    [Tooltip("カメラからこのQuadまでの距離(ワールド単位)。")]
    public float distanceFromCamera = 10f;

    [Tooltip("毎フレーム自動調整するか。falseの場合は画面サイズ変更時のみ再計算します。")]
    public bool updateEveryFrame = true;

    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastFov;

    void OnEnable()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        FitToCamera();
        CacheCurrentState();
    }

    void Update()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        if (updateEveryFrame)
        {
            FitToCamera();
        }
        else if (Screen.width != lastScreenWidth ||
                 Screen.height != lastScreenHeight ||
                 !Mathf.Approximately(targetCamera.fieldOfView, lastFov))
        {
            FitToCamera();
            CacheCurrentState();
        }
    }

    /// <summary>
    /// カメラのFOVとアスペクト比から、Quadが画面いっぱいに収まる
    /// 位置・スケールを計算して適用する。
    /// </summary>
    public void FitToCamera()
    {
        if (targetCamera == null) return;

        // Quadをカメラの正面・指定距離の位置に配置
        transform.position = targetCamera.transform.position +
                              targetCamera.transform.forward * distanceFromCamera;

        // Quadをカメラの方向に正対させる
        transform.rotation = Quaternion.LookRotation(
            transform.position - targetCamera.transform.position,
            targetCamera.transform.up);

        // 透視投影(Perspective)の場合: FOVと距離から必要な高さ・幅を計算
        if (!targetCamera.orthographic)
        {
            float heightAtDistance = 2f * distanceFromCamera *
                Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float widthAtDistance = heightAtDistance * targetCamera.aspect;

            // デフォルトのQuadは1x1ユニットなので、そのままスケール値として使える
            transform.localScale = new Vector3(widthAtDistance, heightAtDistance, 1f);
        }
        else
        {
            // 正投影(Orthographic)カメラの場合
            float height = targetCamera.orthographicSize * 2f;
            float width = height * targetCamera.aspect;
            transform.localScale = new Vector3(width, height, 1f);
        }
    }

    private void CacheCurrentState()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastFov = targetCamera != null ? targetCamera.fieldOfView : 0f;
    }
}
