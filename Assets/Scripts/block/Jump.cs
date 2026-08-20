using UnityEngine;

/// <summary>
/// このスクリプトを付けたスプライトをクリックすると、プレイヤーがジャンプします。
/// スプライトには、クリック判定に使う Collider2D が必要です。
/// </summary>
public class Jump : MonoBehaviour
{
    [Header("ジャンプの設定")]
    [Tooltip("ジャンプさせるプレイヤーの Rigidbody2D を指定します。")]
    [SerializeField] private Rigidbody2D playerBody;

    [Tooltip("1回クリックしたときに上方向へ加える力です。値を大きくすると高くジャンプします。")]
    [SerializeField] private float jumpPower = 5f;

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

        // Impulseを使い、クリックした瞬間に上向きの力を加えます。
        playerBody.AddForce(Vector2.up * jumpPower, ForceMode2D.Impulse);
    }
}
