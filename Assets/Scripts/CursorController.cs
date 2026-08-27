using UnityEngine;

internal static class CursorController
{
    private const string CursorSettingsResourceName = "CursorSettings";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyGameCursor()
    {
        CursorSettings settings = Resources.Load<CursorSettings>(CursorSettingsResourceName);
        Texture2D cursorTexture = settings != null ? settings.CursorTexture : null;
        if (cursorTexture == null)
        {
            Debug.LogError("The game cursor settings or cursor texture could not be loaded.");
            return;
        }

        Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.ForceSoftware);
    }
}