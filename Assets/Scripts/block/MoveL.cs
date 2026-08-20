using UnityEngine;

/// <summary>
/// このスクリプトを付けたスプライトを押している間、プレイヤーが左へ進みます。
/// スプライトには、クリック判定に使う Collider2D が必要です。
/// </summary>
public class MoveL : MonoBehaviour
{
    [Header("左移動の設定")]
    [Tooltip("左へ動かすプレイヤーの Rigidbody2D を指定します。")]
    [SerializeField] private Rigidbody2D playerBody;

    [Tooltip("長押し中の左方向への移動速度です。値を大きくすると速く進みます。")]
    [SerializeField] private float moveSpeed = 3f;

    // スプライトが押されている間だけ true になります。
    private bool isPressed;

    // 接触中の壁を調べるための配列です。毎フレームのメモリ確保を防ぐため再利用します。
    private readonly ContactPoint2D[] contactPoints = new ContactPoint2D[8];

    private void Awake()
    {
        // Inspectorで未設定の場合は、名前が「Player」のオブジェクトから自動取得します。
        if (playerBody == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                playerBody = player.GetComponent<Rigidbody2D>();
            }
        }
    }

    /// <summary>
    /// Collider2Dが付いたスプライトを押した瞬間に、Unityから呼ばれます。
    /// </summary>
    private void OnMouseDown()
    {
        if (playerBody == null)
        {
            Debug.LogWarning("MoveL: プレイヤーの Rigidbody2D が設定されていません。", this);
            return;
        }

        isPressed = true;
    }

    /// <summary>
    /// スプライトから指やマウスボタンを離したときに、Unityから呼ばれます。
    /// </summary>
    private void OnMouseUp()
    {
        isPressed = false;
        StopHorizontalMovement();
    }

    private void FixedUpdate()
    {
        if (!isPressed || playerBody == null)
        {
            return;
        }

        // 左側の壁に触れているときは、壁へ押し付ける速度を与えません。
        // 上下速度には触れないため、壁際でもジャンプや落下が止まりません。
        if (IsTouchingWallInMoveDirection(Vector2.left))
        {
            StopHorizontalMovement();
            return;
        }

        // 落下やジャンプの上下速度は保ったまま、左方向へ一定速度で移動します。
        Vector2 velocity = playerBody.linearVelocity;
        velocity.x = -moveSpeed;
        playerBody.linearVelocity = velocity;
    }

    private void OnDisable()
    {
        // オブジェクトが無効になった場合も、押しっぱなし状態を解除します。
        isPressed = false;
        StopHorizontalMovement();
    }

    private void StopHorizontalMovement()
    {
        if (playerBody == null)
        {
            return;
        }

        Vector2 velocity = playerBody.linearVelocity;
        velocity.x = 0f;
        playerBody.linearVelocity = velocity;
    }

    /// <summary>
    /// プレイヤーが指定した移動方向の壁に接しているかを調べます。
    /// </summary>
    private bool IsTouchingWallInMoveDirection(Vector2 moveDirection)
    {
        int contactCount = playerBody.GetContacts(contactPoints);

        for (int i = 0; i < contactCount; i++)
        {
            // 壁から受ける法線が移動方向と逆向きなら、その方向に壁があります。
            if (Vector2.Dot(contactPoints[i].normal, moveDirection) < -0.5f)
            {
                return true;
            }
        }

        return false;
    }
}
