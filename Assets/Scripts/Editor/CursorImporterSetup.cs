using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class CursorImporterSetup
{
    private const string CursorAssetPath = "Assets/Sprites/UI/Cursor.png";
    private const string CursorSettingsAssetPath = "Assets/Resources/CursorSettings.asset";
    private const int CursorMaxSize = 48;

    static CursorImporterSetup()
    {
        EditorApplication.delayCall += Apply;
    }

    [MenuItem("Tools/Apply Game Cursor Settings")]
    internal static void Apply()
    {
        TextureImporter importer = AssetImporter.GetAtPath(CursorAssetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Cursor texture was not found at {CursorAssetPath}.");
            return;
        }

        bool needsReimport = !importer.isReadable
            || importer.textureType != TextureImporterType.Cursor
            || importer.maxTextureSize != CursorMaxSize;

        if (needsReimport)
        {
            importer.isReadable = true;
            importer.textureType = TextureImporterType.Cursor;
            importer.maxTextureSize = CursorMaxSize;
            importer.SaveAndReimport();
        }

        Texture2D cursorTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorAssetPath);
        if (cursorTexture == null)
        {
            Debug.LogError($"Cursor texture could not be loaded from {CursorAssetPath}.");
            return;
        }

        PlayerSettings.defaultCursor = cursorTexture;
        PlayerSettings.cursorHotspot = Vector2.zero;

        CursorSettings settings = AssetDatabase.LoadAssetAtPath<CursorSettings>(CursorSettingsAssetPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<CursorSettings>();
            AssetDatabase.CreateAsset(settings, CursorSettingsAssetPath);
        }

        SerializedObject serializedSettings = new SerializedObject(settings);
        SerializedProperty textureProperty = serializedSettings.FindProperty("cursorTexture");
        if (textureProperty.objectReferenceValue != cursorTexture)
        {
            textureProperty.objectReferenceValue = cursorTexture;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
    }
}
