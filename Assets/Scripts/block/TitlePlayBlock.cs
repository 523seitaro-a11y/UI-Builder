using UnityEngine;

/// <summary>ステージ共通のクリック判定から、タイトルのプレイ操作を実行します。</summary>
public sealed class TitlePlayBlock : MonoBehaviour, BlockManager.IBlockClickState
{
    public void Click()
    {
        if (!ScreenTransition.IsTransitioning)
            FindFirstObjectByType<TitleBuildManager>()?.OpenStageSelect();
    }
}
