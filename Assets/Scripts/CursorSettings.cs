using UnityEngine;

public sealed class CursorSettings : ScriptableObject
{
    [SerializeField] private Texture2D cursorTexture;

    public Texture2D CursorTexture => cursorTexture;
}