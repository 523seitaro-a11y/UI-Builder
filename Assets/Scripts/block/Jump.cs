using UnityEngine;

/// <summary>
/// このスクリプトを付けたスプライトをクリックすると、プレイヤーがジャンプします。
/// スプライトには、クリック判定に使う Collider2D が必要です。
/// </summary>
public class Jump : MonoBehaviour, BlockManager.IBlockOperationState
{
    public bool IsOperating => isJumping;

    [Header("ジャンプの設定")]
    [Tooltip("ジャンプさせるプレイヤーの Rigidbody2D を指定します。")]
    [SerializeField] private Rigidbody2D playerBody;

    [Tooltip("1回クリックしたときに上方向へ加える力です。値を大きくすると高くジャンプします。")]
    [SerializeField] private float jumpPower = 5f;

    [Tooltip("地面として扱うレイヤーを指定します。初期値ではすべてのレイヤーを地面として判定します。")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("この値より上向きの接触面だけを地面として扱います。")]
    [Range(0f, 1f)]
    [SerializeField] private float minimumGroundNormalY = 0.5f;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
    private bool isJumping;

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
    /// Collider2Dが付いたスプライトをクリックしたとき、Unityから呼ばれます。
    /// </summary>
    private void OnMouseDown()
    {
        if (playerBody == null)
        {
            Debug.LogWarning("Jump: プレイヤーの Rigidbody2D が設定されていません。", this);
            return;
        }

        if (!IsGrounded())
        {
            return;
        }

        // Impulseを使い、クリックした瞬間に上向きの力を加えます。
        isJumping = true;
        playerBody.AddForce(Vector2.up * jumpPower, ForceMode2D.Impulse);
    }

    private void FixedUpdate()
    {
        if (isJumping && IsGrounded())
        {
            isJumping = false;
        }
    }

    public void CancelOperation() => isJumping = false;

    private void OnDisable() => CancelOperation();

    private bool IsGrounded()
    {
        // ジャンプ直後、物理更新前の接触情報が残っていても連続ジャンプさせません。
        if (playerBody.linearVelocity.y > 0.05f)
        {
            return false;
        }

        int contactCount = playerBody.GetContacts(contacts);
        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = contacts[i];
            int otherLayerMask = 1 << contact.otherCollider.gameObject.layer;

            if ((groundLayers.value & otherLayerMask) != 0 &&
                contact.normal.y >= minimumGroundNormalY)
            {
                return true;
            }
        }

        return false;
    }
}
