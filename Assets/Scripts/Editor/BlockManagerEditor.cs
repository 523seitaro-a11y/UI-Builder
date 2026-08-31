using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        public readonly bool IsRandomStepBlock;
        public readonly bool IsUpwardDropdownBlock;
        public readonly bool IsPopupBlock;

        public BlockPreset(
            string name,
            string spritePath,
            Vector2Int footprint,
            bool isBgmScrollBar = false,
            bool isBrightnessScrollBar = false,
            bool isRandomStepBlock = false,
            bool isUpwardDropdownBlock = false,
            bool isPopupBlock = false)
        {
            Name = name;
            SpritePath = spritePath;
            Footprint = footprint;
            IsBgmScrollBar = isBgmScrollBar;
            IsBrightnessScrollBar = isBrightnessScrollBar;
            IsRandomStepBlock = isRandomStepBlock;
            IsUpwardDropdownBlock = isUpwardDropdownBlock;
            IsPopupBlock = isPopupBlock;
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
            isBrightnessScrollBar: true),
        new BlockPreset("RandomStep", null, new Vector2Int(3, 1), isRandomStepBlock: true),
        new BlockPreset("UpwardDropdown", null, new Vector2Int(3, 2), isUpwardDropdownBlock: true),
        new BlockPreset("Popup", null, new Vector2Int(5, 3), isPopupBlock: true)
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
                definition.FindPropertyRelative("brightnessIconSprite").objectReferenceValue =
                    preset.IsBrightnessScrollBar
                        ? FindSprite(preset.SpritePath, "BrightnessIcon")
                        : null;
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
        definition.FindPropertyRelative("isRandomStepBlock").boolValue = preset.IsRandomStepBlock;
        definition.FindPropertyRelative("isUpwardDropdownBlock").boolValue = preset.IsUpwardDropdownBlock;
        definition.FindPropertyRelative("isPopupBlock").boolValue = preset.IsPopupBlock;
        definition.FindPropertyRelative("usesDynamicCollider").boolValue =
            preset.IsRandomStepBlock || preset.IsUpwardDropdownBlock || preset.IsPopupBlock;
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
        RectTransform parent = FindSourceInScene("BlockListRoot");
        if (parent == null)
        {
            parent = FindSourceInScene("BlockBG");
        }
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
        source.sizeDelta = preset.IsBrightnessScrollBar
            ? new Vector2(450f, 100f)
            : preset.IsScrollBar
                ? new Vector2(240f, 64f)
                : new Vector2(72f, 72f);

        Image image = sourceObject.GetComponent<Image>();
        image.sprite = preset.IsBrightnessScrollBar
            ? FindSprite(preset.SpritePath, "BrightnessPalette")
            : string.IsNullOrWhiteSpace(preset.SpritePath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Sprite>(preset.SpritePath);
        image.preserveAspect = true;
        image.raycastTarget = true;

        if (preset.IsRandomStepBlock || preset.IsUpwardDropdownBlock || preset.IsPopupBlock)
        {
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            if (preset.IsRandomStepBlock || preset.IsPopupBlock)
            {
                source.sizeDelta = new Vector2(300f, 100f);
            }

            CreateDynamicSourceVisual(source, preset);
        }

        if (parent.GetComponent<LayoutGroup>() == null)
        {
            PositionAfterExistingSources(source, parent);
        }

        return source;
    }

    private static void CreateDynamicSourceVisual(RectTransform parent, BlockPreset preset)
    {
        GameObject visualObject = new GameObject("DynamicSourceVisual", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(visualObject, "可変ブロック表示を追加");
        RectTransform visualRoot = visualObject.GetComponent<RectTransform>();
        visualRoot.SetParent(parent, false);
        visualRoot.anchorMin = Vector2.zero;
        visualRoot.anchorMax = Vector2.one;
        visualRoot.offsetMin = Vector2.zero;
        visualRoot.offsetMax = Vector2.zero;
        parent = visualRoot;

        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite solid = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/LINESeedJP-Bold SDF.asset");
        Color32 dark = new Color32(59, 59, 59, 255);
        Color32 light = new Color32(235, 235, 235, 255);
        Color32 muted = new Color32(145, 145, 145, 255);

        if (preset.IsRandomStepBlock)
        {
            for (int i = 0; i < 2; i++)
            {
                CreateSourceImage(parent, $"Connector{i + 1}", solid, muted,
                    new Vector2(i == 0 ? -50f : 50f, 0f), new Vector2(42f, 7f));
            }

            for (int i = 0; i < 3; i++)
            {
                Vector2 position = new Vector2((i - 1) * 100f, 0f);
                CreateSourceImage(parent, $"Node{i + 1}Outer", circle, dark,
                    position, new Vector2(58f, 58f));
                CreateSourceImage(parent, $"Node{i + 1}Inner", circle, light,
                    position, new Vector2(46f, 46f));
                CreateSourceText(parent, $"Node{i + 1}Label", (i + 1).ToString(), font,
                    muted, position);
            }

            return;
        }

        if (preset.IsPopupBlock)
        {
            CreateSourceText(
                parent,
                "PopupLabel",
                "ポップアップを開く",
                font,
                dark,
                Vector2.zero,
                new Vector2(270f, 72f),
                28f);
            return;
        }

        Vector2 nodePosition = new Vector2(-8f, 0f);
        CreateSourceImage(parent, "SelectedNodeOuter", circle, dark,
            nodePosition, new Vector2(58f, 58f));
        CreateSourceImage(parent, "SelectedNodeInner", circle, dark,
            nodePosition, new Vector2(46f, 46f));
        CreateSourceText(parent, "SelectedNodeLabel", "A", font, Color.white, nodePosition);
        CreateSourceText(parent, "Chevron", "▲", font, dark,
            new Vector2(28f, 22f), new Vector2(32f, 32f), 24f);
    }

    private static Image CreateSourceImage(
        RectTransform parent,
        string objectName,
        Sprite sprite,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        Undo.RegisterCreatedObjectUndo(imageObject, "可変ブロック表示を追加");
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateSourceText(
        RectTransform parent,
        string objectName,
        string value,
        TMP_FontAsset font,
        Color color,
        Vector2 position,
        Vector2? size = null,
        float fontSize = 30f)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(textObject, "可変ブロック文字を追加");
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size ?? new Vector2(42f, 42f);
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = value;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate;
        label.raycastTarget = false;
        return label;
    }

    private static void CreateSourceLabel(RectTransform parent, string labelText)
    {
        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(labelObject, "ブロックラベルを追加");
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 4f);
        rect.offsetMax = new Vector2(-4f, -4f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/LINESeedJP-Bold SDF.asset");
        label.text = labelText;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 28f;
        label.color = Color.black;
        label.raycastTarget = false;
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

    private static Sprite FindSprite(string assetPath, string spriteName)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset is Sprite sprite && string.Equals(sprite.name, spriteName, StringComparison.Ordinal))
            {
                return sprite;
            }
        }

        return null;
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

