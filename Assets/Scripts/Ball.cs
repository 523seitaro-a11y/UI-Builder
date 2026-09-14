using UnityEngine;

[DisallowMultipleComponent]
public class Ball : MonoBehaviour
{
    public enum ScaleMode
    {
        [Tooltip("現在のサイズに1.1を掛ける（1.1倍 / 10%拡大）")]
        Multiply,
        [Tooltip("現在のサイズに1.1を加算する（+1.1）")]
        Add
    }

    [Header("サイズ変更設定")]
    [Tooltip("サイズの変更方式（Multiply: 1.1倍 / Add: +1.1ずつ加算）")]
    [SerializeField] private ScaleMode scaleMode = ScaleMode.Multiply;

    [Tooltip("壁に当たった時のサイズ変化量（Multiplyなら1.1倍、Addなら+1.1）")]
    [SerializeField] private float scaleRate = 1.1f;

    [Tooltip("最大サイズの制限（0以下の場合は無制限）")]
    [SerializeField] private float maxScale = 10f;

    [Header("壁の判定設定")]
    [Tooltip("壁として判定するタグ（空欄の場合はタグ判定を行いません）")]
    [SerializeField] private string wallTag = "Wall";

    [Tooltip("オブジェクト名に 'Wall' が含まれている場合も壁と判定するか")]
    [SerializeField] private bool matchNameContainsWall = true;

    [Header("ボールの移動・初速（任意）")]
    [Tooltip("ゲーム開始時に自動でボールを動かすか")]
    [SerializeField] private bool autoLaunch = true;

    [Tooltip("初速度の強さ")]
    [SerializeField] private float initialSpeed = 5f;

    [Tooltip("発射方向（(0, 0) にした場合はランダムな斜め方向に発射）")]
    [SerializeField] private Vector2 launchDirection = new Vector2(1f, 1f);

    [Tooltip("壁との衝突後もボールの移動速度を一定に維持するか")]
    [SerializeField] private bool maintainSpeed = true;

    private Rigidbody2D rb2d;
    private Rigidbody rb3d;

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        rb3d = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (autoLaunch)
        {
            Launch();
        }
    }

    private void FixedUpdate()
    {
        if (!maintainSpeed) return;

        if (rb2d != null)
        {
#if UNITY_6000_0_OR_NEWER
            Vector2 currentVelocity = rb2d.linearVelocity;
#else
            Vector2 currentVelocity = rb2d.velocity;
#endif
            if (currentVelocity.sqrMagnitude > 0.001f)
            {
#if UNITY_6000_0_OR_NEWER
                rb2d.linearVelocity = currentVelocity.normalized * initialSpeed;
#else
                rb2d.velocity = currentVelocity.normalized * initialSpeed;
#endif
            }
        }
        else if (rb3d != null)
        {
#if UNITY_6000_0_OR_NEWER
            Vector3 currentVelocity = rb3d.linearVelocity;
#else
            Vector3 currentVelocity = rb3d.velocity;
#endif
            if (currentVelocity.sqrMagnitude > 0.001f)
            {
#if UNITY_6000_0_OR_NEWER
                rb3d.linearVelocity = currentVelocity.normalized * initialSpeed;
#else
                rb3d.velocity = currentVelocity.normalized * initialSpeed;
#endif
            }
        }
    }

    /// <summary>
    /// ボールを発射する処理
    /// </summary>
    public void Launch()
    {
        Vector2 dir = launchDirection.normalized;
        if (dir == Vector2.zero)
        {
            // ランダムな斜め45度前後の角度を決定
            float angle = Random.Range(30f, 60f) * Mathf.Deg2Rad;
            float signX = Random.value < 0.5f ? -1f : 1f;
            float signY = Random.value < 0.5f ? -1f : 1f;
            dir = new Vector2(Mathf.Cos(angle) * signX, Mathf.Sin(angle) * signY).normalized;
        }

        if (rb2d != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb2d.linearVelocity = dir * initialSpeed;
#else
            rb2d.velocity = dir * initialSpeed;
#endif
        }
        else if (rb3d != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb3d.linearVelocity = new Vector3(dir.x, dir.y, 0f) * initialSpeed;
#else
            rb3d.velocity = new Vector3(dir.x, dir.y, 0f) * initialSpeed;
#endif
        }
    }

    /// <summary>
    /// 2Dの衝突判定
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsWall(collision.gameObject))
        {
            OnHitWall();
        }
    }

    /// <summary>
    /// 3Dの衝突判定
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (IsWall(collision.gameObject))
        {
            OnHitWall();
        }
    }

    /// <summary>
    /// 対象が壁かどうかを判定
    /// </summary>
    private bool IsWall(GameObject target)
    {
        // 1. Tagで判定
        if (!string.IsNullOrEmpty(wallTag) && target.CompareTag(wallTag))
        {
            return true;
        }

        // 2. Wallコンポーネントで判定
        if (target.GetComponent<Wall>() != null)
        {
            return true;
        }

        // 3. オブジェクト名に "Wall" が含まれているかで判定
        if (matchNameContainsWall && target.name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 壁に当たった時のサイズ拡大処理
    /// </summary>
    private void OnHitWall()
    {
        Vector3 currentScale = transform.localScale;
        Vector3 newScale;

        if (scaleMode == ScaleMode.Multiply)
        {
            newScale = currentScale * scaleRate;
        }
        else
        {
            newScale = currentScale + Vector3.one * scaleRate;
        }

        // 最大サイズ制限
        if (maxScale > 0f)
        {
            newScale = Vector3.Min(newScale, Vector3.one * maxScale);
        }

        transform.localScale = newScale;
    }
}
