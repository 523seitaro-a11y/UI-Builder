using UnityEngine;

/// <summary>Routes native mouse/touch handlers through the owning block manager's input state.</summary>
[DisallowMultipleComponent]
public sealed class BlockInputGuard : MonoBehaviour
{
    [SerializeField] private BlockManager owner;

    public void Configure(BlockManager manager) => owner = manager;

    public static bool AllowsInput(MonoBehaviour behaviour)
    {
        if (behaviour == null || !behaviour.isActiveAndEnabled)
        {
            return false;
        }

        BlockInputGuard guard = behaviour.GetComponentInParent<BlockInputGuard>();
        return guard == null || (guard.owner != null && guard.owner.CanOperatePlacedBlocks);
    }
}
