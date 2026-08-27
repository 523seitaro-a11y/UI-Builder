using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(BlockManager))]
public sealed class BlockManagerEditor : Editor
{
    private readonly struct BlockPreset
    {
        public readonly string Name;
        public readonly string SpritePath;
        public readonly Vector2Int Footprint;
        public readonly bool IsBgmScrollBar;
        public readonly bool IsBrightnessScrollBar;

        public BlockPreset(
            string name,
            string spritePath,
            Vector2Int footprint,
            bool isBgmScrollBar = false,
            bool isBrightnessScrollBar = false)
        {
            Name = name;
            SpritePath = spritePath;
            Footprint = footprint;
            IsBgmScrollBar = isBgmScrollBar;
            IsBrightnessScrollBar = isBrightnessScrollBar;
        }

        public bool IsScrollBar => IsBgmScrollBar || IsBrightnessScrollBar;
    }

    private static readonly BlockPreset[] Presets =
    {
        new BlockPreset("MoveR", "Assets/Sprites/Block/MoveR.png", Vector2Int.one),
        new BlockPreset("MoveL", "Assets/Sprites/Block/MoveL.png", Vector2Int.one),
        new BlockPreset("Jump", "Assets/Sprites/Block/Jump.png", new Vector2Int(2, 1)),
        new BlockPreset("BGMScrollBar", "Assets/Sprites/Block/BGM.png", new Vector2Int(4, 1), isBgmScrollBar: true),
        new BlockPreset(
            "BrightnessScrollBar",
            "Assets/Sprites/Block/Brightness.png",
            new Vector2Int(4, 1),
            isBrightnessScrollBar: true)
    };

    private SerializedProperty blocksProperty;

    private void OnEnable()
    {
        blocksProperty = serializedObject.FindProperty("blocks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("ステージで使用するブロック", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("チェックを入れるとブロックを設定して上部パネルに表示し、外すと非表示にします。", MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            foreach (BlockPreset preset in Presets)
            {
                int index = FindBlockIndex(preset.Name);
                bool current = index >= 0 &&
                               blocksProperty.GetArrayElementAtIndex(index)
                                   .FindPropertyRelative("isEnabled").boolValue;
                bool next = EditorGUILayout.ToggleLeft(preset.Name, current);
                if (next != current)
                {
                    SetBlockEnabled(preset, next);
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                    EditorSceneManager.MarkSceneDirty(((BlockManager)target).gameObject.scene);
                    serializedObject.Update();
                }
            }
        }

        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, "m_Script", "blocks", "blockAvailabilityVersion");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("ブロック詳細設定", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(blocksProperty, true);
        serializedObject.ApplyModifiedProperties();
    }

    private void SetBlockEnabled(BlockPreset preset, bool enabled)
    {
        serializedObject.FindProperty("blockAvailabilityVersion").intValue = 1;
        int index = FindBlockIndex(preset.Name);
        bool isNew = index < 0;
        if (isNew)
        {
            index = blocksProperty.arraySize;
            blocksProperty.InsertArrayElementAtIndex(index);
        }

        SerializedProperty definition = blocksProperty.GetArrayElementAtIndex(index);
        definition.FindPropertyRelative("displayName").stringValue = preset.Name;

        if (enabled)
        {
            RectTransform source = definition.FindPropertyRelative("dragSource").objectReferenceValue as RectTransform;
            if (source == null)
            {
                source = FindSourceInScene(preset.Name) ?? CreateSource(preset);
            }

            if (source == null)
            {
                definition.FindPropertyRelative("isEnabled").boolValue = false;
                return;
            }

            GameObject sourceRoot = ResolveSourceRoot(source, preset.Name);
            definition.FindPropertyRelative("dragSource").objectReferenceValue = source;
            definition.FindPropertyRelative("sourceVisualRoot").objectReferenceValue = sourceRoot;
            definition.FindPropertyRelative("isEnabled").boolValue = true;
            SetSourceActive(sourceRoot, true);

            Image sourceImage = source.GetComponent<Image>() ?? source.GetComponentInChildren<Image>(true);
            if (preset.IsScrollBar)
            {
                definition.FindPropertyRelative("bgmTrackSource").objectReferenceValue = sourceImage;
                Image currentHandle = definition.FindPropertyRelative("bgmHandleSource").objectReferenceValue as Image;
                definition.FindPropertyRelative("bgmHandleSource").objectReferenceValue =
                    FindBgmHandle(sourceRoot, sourceImage, currentHandle);
            }
        }
        else
        {
            definition.FindPropertyRelative("isEnabled").boolValue = false;
            GameObject sourceRoot = definition.FindPropertyRelative("sourceVisualRoot").objectReferenceValue as GameObject;
            RectTransform source = definition.FindPropertyRelative("dragSource").objectReferenceValue as RectTransform;
            SetSourceActive(sourceRoot != null ? sourceRoot : source != null ? source.gameObject : null, false);
        }

        if (!isNew)
        {
            return;
        }

        definition.FindPropertyRelative("worldTemplate").objectReferenceValue = null;
        definition.FindPropertyRelative("footprint").vector2IntValue = preset.Footprint;
        definition.FindPropertyRelative("placementOffset").vector3Value = Vector3.zero;
        definition.FindPropertyRelative("availableCount").intValue = 1;
        definition.FindPropertyRelative("hideSourceWhenExhausted").boolValue = true;
        definition.FindPropertyRelative("isBgmScrollBar").boolValue = preset.IsBgmScrollBar;
        definition.FindPropertyRelative("isBrightnessScrollBar").boolValue = preset.IsBrightnessScrollBar;
        definition.FindPropertyRelative("moveSpeed").floatValue = 5f;
        definition.FindPropertyRelative("jumpPower").floatValue = 15f;
    }

    private int FindBlockIndex(string blockName)
    {
        for (int i = 0; i < blocksProperty.arraySize; i++)
        {
            string displayName = blocksProperty.GetArrayElementAtIndex(i)
                .FindPropertyRelative("displayName").stringValue;
            if (string.Equals(displayName, blockName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private RectTransform FindSourceInScene(string sourceName)
    {
        BlockManager manager = (BlockManager)target;
        foreach (RectTransform rectTransform in FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rectTransform.gameObject.scene == manager.gameObject.scene &&
                string.Equals(rectTransform.name, sourceName, StringComparison.OrdinalIgnoreCase))
            {
                return rectTransform;
            }
        }

        return null;
    }

    private RectTransform CreateSource(BlockPreset preset)
    {
        RectTransform parent = FindSourceInScene("BlockBG");
        if (parent == null)
        {
            Debug.LogWarning($"BlockManager: {preset.Name} を作成する親の BlockBG が見つかりません。", target);
            return null;
        }

        GameObject sourceObject = new GameObject(
            preset.Name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        Undo.RegisterCreatedObjectUndo(sourceObject, $"{preset.Name}ブロックを追加");

        RectTransform source = sourceObject.GetComponent<RectTransform>();
        source.SetParent(parent, false);
        source.SetAsLastSibling();
        source.sizeDelta = preset.IsScrollBar ? new Vector2(240f, 64f) : new Vector2(72f, 72f);

        Image image = sourceObject.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(preset.SpritePath);
        image.preserveAspect = true;
        image.raycastTarget = true;

        if (parent.GetComponent<LayoutGroup>() == null)
        {
            PositionAfterExistingSources(source, parent);
        }

        return source;
    }

    private static GameObject ResolveSourceRoot(RectTransform source, string sourceName)
    {
        for (Transform current = source.transform; current != null; current = current.parent)
        {
            if (string.Equals(current.name, sourceName, StringComparison.OrdinalIgnoreCase))
            {
                return current.gameObject;
            }
        }

        return source.gameObject;
    }

    private static Image FindBgmHandle(GameObject sourceRoot, Image track, Image currentHandle)
    {
        if (currentHandle != null && currentHandle != track &&
            currentHandle.transform.IsChildOf(sourceRoot.transform))
        {
            return currentHandle;
        }

        Image[] images = sourceRoot.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image != track && string.Equals(image.name, "Icon", StringComparison.OrdinalIgnoreCase))
            {
                return image;
            }
        }

        foreach (Image image in images)
        {
            if (image != track && string.Equals(image.name, "Handle", StringComparison.OrdinalIgnoreCase))
            {
                return image;
            }
        }

        return track;
    }

    private static void PositionAfterExistingSources(RectTransform source, RectTransform parent)
    {
        bool found = false;
        float rightEdge = 0f;
        float y = 0f;
        foreach (RectTransform child in parent)
        {
            if (child == source || !child.gameObject.activeSelf)
            {
                continue;
            }

            float width = child.rect.width > 0f ? child.rect.width : child.sizeDelta.x;
            float candidate = child.anchoredPosition.x + width * 0.5f;
            if (!found || candidate > rightEdge)
            {
                found = true;
                rightEdge = candidate;
                y = child.anchoredPosition.y;
            }
        }

        source.anchoredPosition = found
            ? new Vector2(rightEdge + 12f + source.sizeDelta.x * 0.5f, y)
            : Vector2.zero;
    }

    private static void SetSourceActive(GameObject source, bool active)
    {
        if (source == null || source.activeSelf == active)
        {
            return;
        }

        Undo.RecordObject(source, active ? "ブロックを表示" : "ブロックを非表示");
        source.SetActive(active);
        EditorUtility.SetDirty(source);
    }
}

