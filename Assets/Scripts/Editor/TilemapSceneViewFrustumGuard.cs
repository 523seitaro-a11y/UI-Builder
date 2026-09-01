using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 6000.1のTilemap Editorが正投影Sceneビューでz=0を
/// ScreenToWorldPointへ渡す際に出すfrustum警告を防ぎます。
/// ゲームカメラには影響しません。
/// </summary>
[InitializeOnLoad]
internal static class TilemapSceneViewFrustumGuard
{
    private const float OrthographicNearClip = -0.01f;

    static TilemapSceneViewFrustumGuard()
    {
        SceneView.beforeSceneGui -= EnsureValidOrthographicFrustum;
        SceneView.beforeSceneGui += EnsureValidOrthographicFrustum;
    }

    private static void EnsureValidOrthographicFrustum(SceneView sceneView)
    {
        Camera sceneCamera = sceneView != null ? sceneView.camera : null;
        if (sceneCamera == null || !sceneCamera.orthographic ||
            sceneCamera.nearClipPlane <= OrthographicNearClip)
        {
            return;
        }

        sceneCamera.nearClipPlane = OrthographicNearClip;
    }
}
