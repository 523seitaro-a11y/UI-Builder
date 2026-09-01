using UnityEngine;

/// <summary>
/// クリックされたとき、長押しリトライと同じ経路でビルドモードへ戻します。
/// </summary>
public sealed class RetryBlock : MonoBehaviour, BlockManager.IBlockOperationState
{
    [SerializeField] private StageManager stageManager;

    public bool IsOperating => false;

    public void Configure(StageManager manager)
    {
        stageManager = manager;
    }

    private void Awake()
    {
        if (stageManager == null)
        {
            stageManager = FindFirstObjectByType<StageManager>();
        }
    }

    private void OnMouseDown() => BeginOperation();

    public void BeginOperation()
    {
        stageManager?.ReturnToBuildMode();
    }

    public void CancelOperation()
    {
    }
}
