using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        public readonly bool IsPauseBlock;
        public readonly bool IsRetryBlock;
        public readonly bool IsSaveBlock;

        public BlockPreset(
            string name,
            string spritePath,
            Vector2Int footprint,
            bool isBgmScrollBar = false,
            bool isBrightnessScrollBar = false,
            bool isRandomStepBlock = false,
            bool isUpwardDropdownBlock = false,
            bool isPopupBlock = false,
            bool isPauseBlock = false,
            bool isRetryBlock = false,
            bool isSaveBlock = false)
        {
            Name = name;
            SpritePath = spritePath;
            Footprint = footprint;
            IsBgmScrollBar = isBgmScrollBar;
            IsBrightnessScrollBar = isBrightnessScrollBar;
            IsRandomStepBlock = isRandomStepBlock;
            IsUpwardDropdownBlock = isUpwardDropdownBlock;
            IsPopupBlock = isPopupBlock;
            IsPauseBlock = isPauseBlock;
            IsRetryBlock = isRetryBlock;
            IsSaveBlock = isSaveBlock;
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
            new Vector2Int(1, 4),
            isBrightnessScrollBar: true),
        new BlockPreset("RandomStep", null, new Vector2Int(3, 1), isRandomStepBlock: true),
        new BlockPreset("UpwardDropdown", null, new Vector2Int(3, 2), isUpwardDropdownBlock: true),
        new BlockPreset("Popup", null, new Vector2Int(5, 3), isPopupBlock: true),
        new BlockPreset(
            "Stop",
            "Assets/Sprites/Block/Stop.png",
            Vector2Int.one,
            isPauseBlock: true),
        new BlockPreset(
            "RetryBlock",
            "Assets/Sprites/Block/RetryButton.png",
            new Vector2Int(7, 7),
            isRetryBlock: true),
        new BlockPreset(
            "Save",
            "Assets/Sprites/Block/Save.png",
            new Vector2Int(2, 1),
            isSaveBlock: true)
    };

    private SerializedProperty blocksProperty;

    private void OnEnable()
    {
        blocksProperty = serializedObject.FindProperty("blocks");
        if (!Application.isPlaying)
        {
            RefreshEnabledBgmSource();
        }
    }

    private void RefreshEnabledBgmSource()
    {
        BlockPreset bgmPreset = Presets[3];
        serializedObject.Update();
        int index = FindBlockIndex(bgmPreset.Name);
        if (index < 0 ||
            !blocksProperty.GetArrayElementAtIndex(index)
                .FindPropertyRelative("isEnabled").boolValue)
        {
            return;
        }

        SetBlockEnabled(bgmPreset, true);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(((BlockManager)target).gameObject.scene);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("ステージで使用するブロック", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "各ブロックを使用できる個数を入力します。0にすると、そのブロックを使用しません。",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            foreach (BlockPreset preset in Presets)
            {
                int index = FindBlockIndex(preset.Name);
                int currentCount = 0;
                if (index >= 0)
                {
                    SerializedProperty definition = blocksProperty.GetArrayElementAtIndex(index);
                    if (definition.FindPropertyRelative("isEnabled").boolValue)
                    {
                        currentCount = Mathf.Max(
                            1,
                            definition.FindPropertyRelative("availableCount").intValue);
                    }
                }

                int nextCount = Mathf.Max(
                    0,
                    EditorGUILayout.DelayedIntField(preset.Name, currentCount));
                if (nextCount != currentCount)
                {
                    SetBlockCount(preset, nextCount);
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

    private void SetBlockCount(BlockPreset preset, int count)
    {
        int validCount = Mathf.Max(0, count);
        SetBlockEnabled(preset, validCount > 0);

        int index = FindBlockIndex(preset.Name);
        if (index < 0)
        {
            return;
        }

        SerializedProperty definition = blocksProperty.GetArrayElementAtIndex(index);
        definition.FindPropertyRelative("availableCount").intValue = validCount;
        EnsureAdditionalSources(preset, definition, validCount);
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
        if (isNew)
        {
            InitializeNewDefinition(definition, preset);
        }
        else if (!preset.IsBrightnessScrollBar)
        {
            ClearMismatchedBrightnessReferences(definition);
        }

        definition.FindPropertyRelative("displayName").stringValue = preset.Name;

        if (enabled)
        {
            if (preset.IsBrightnessScrollBar)
            {
                EnsureBrightnessInfrastructure();
            }

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
            if (preset.IsBrightnessScrollBar)
            {
                PlaceBrightnessSourceInBlockBg(source, sourceRoot);
            }
            definition.FindPropertyRelative("dragSource").objectReferenceValue = source;
            definition.FindPropertyRelative("sourceVisualRoot").objectReferenceValue = sourceRoot;
            definition.FindPropertyRelative("isEnabled").boolValue = true;
            SetSourceActive(sourceRoot, true);

            Image sourceImage = source.GetComponent<Image>() ?? source.GetComponentInChildren<Image>(true);
            if (string.Equals(preset.Name, "Jump", StringComparison.OrdinalIgnoreCase))
            {
                Undo.RecordObject(source, "Jumpブロックのサイズを更新");
                source.sizeDelta = new Vector2(
                    preset.Footprint.x * 100f,
                    preset.Footprint.y * 100f);
                EditorUtility.SetDirty(source);
            }

            if (preset.IsRetryBlock)
            {
                Undo.RecordObject(source, "リトライブロックのサイズを更新");
                source.sizeDelta = new Vector2(
                    preset.Footprint.x * 100f,
                    preset.Footprint.y * 100f);
                EditorUtility.SetDirty(source);
            }

            if (preset.IsPauseBlock)
            {
                Undo.RecordObject(source, "Stopブロックのサイズを更新");
                source.sizeDelta = new Vector2(100f, 100f);
                EditorUtility.SetDirty(source);
            }

            if (preset.IsScrollBar)
            {
                if (preset.IsBgmScrollBar && sourceImage != null)
                {
                    Undo.RecordObject(source, "音量バーのサイズを更新");
                    source.sizeDelta = new Vector2(400f, 100f);
                    Undo.RecordObject(sourceImage, "音量バースプライトを更新");
                    sourceImage.sprite = FindSprite(
                        "Assets/Sprites/UI/BGMScrollBarShadow.png",
                        "BGMScrollBarShadow_0");
                    sourceImage.color = Color.white;
                    sourceImage.preserveAspect = true;
                    sourceImage.raycastTarget = true;
                    EditorUtility.SetDirty(source);
                    EditorUtility.SetDirty(sourceImage);
                }

                if (preset.IsBrightnessScrollBar && sourceImage != null)
                {
                    Undo.RecordObject(source, "明るさバーのサイズを更新");
                    source.sizeDelta = new Vector2(100f, 400f);
                    Undo.RecordObject(sourceImage, "明るさバースプライトを更新");
                    sourceImage.sprite = FindSprite(
                        "Assets/Sprites/UI/BrightnessScrollBar.png",
                        "BrightnessScrollBar_0");
                    sourceImage.color = Color.white;
                    sourceImage.preserveAspect = true;
                    sourceImage.raycastTarget = true;
                    EditorUtility.SetDirty(source);
                    EditorUtility.SetDirty(sourceImage);
                }

                definition.FindPropertyRelative("bgmTrackSource").objectReferenceValue = sourceImage;
                Image currentHandle = definition.FindPropertyRelative("bgmHandleSource").objectReferenceValue as Image;
                if (preset.IsBgmScrollBar)
                {
                    currentHandle = EnsureBgmHandleSource(
                        sourceRoot,
                        sourceImage,
                        currentHandle,
                        FindSprite(preset.SpritePath, "BGM_0"));
                }
                definition.FindPropertyRelative("bgmHandleSource").objectReferenceValue =
                    FindBgmHandle(sourceRoot, sourceImage, currentHandle);
                definition.FindPropertyRelative("brightnessIconSprite").objectReferenceValue =
                    preset.IsBrightnessScrollBar
                        ? FindSprite(preset.SpritePath, "Brightness_0")
                        : null;
                definition.FindPropertyRelative("brightnessScrollBarSprite").objectReferenceValue =
                    preset.IsBrightnessScrollBar
                        ? FindSprite("Assets/Sprites/UI/BrightnessScrollBar.png", "BrightnessScrollBar_0")
                        : null;
            }

            definition.FindPropertyRelative("pausePlaySprite").objectReferenceValue =
                preset.IsPauseBlock
                    ? FindSprite("Assets/Sprites/Block/Play.png", "Play_0")
                    : null;
            definition.FindPropertyRelative("saveLoadSprite").objectReferenceValue =
                preset.IsSaveBlock
                    ? FindSprite("Assets/Sprites/Block/Load.png", "Load_0")
                    : null;

            EnsureAdditionalSources(
                preset,
                definition,
                Mathf.Max(1, definition.FindPropertyRelative("availableCount").intValue));
        }
        else
        {
            definition.FindPropertyRelative("isEnabled").boolValue = false;
            GameObject sourceRoot = definition.FindPropertyRelative("sourceVisualRoot").objectReferenceValue as GameObject;
            RectTransform source = definition.FindPropertyRelative("dragSource").objectReferenceValue as RectTransform;
            SetSourceActive(sourceRoot != null ? sourceRoot : source != null ? source.gameObject : null, false);
            SetAdditionalSourcesActive(definition, false);
        }

    }

    private static void InitializeNewDefinition(SerializedProperty definition, BlockPreset preset)
    {
        definition.FindPropertyRelative("isEnabled").boolValue = false;
        definition.FindPropertyRelative("dragSource").objectReferenceValue = null;
        definition.FindPropertyRelative("additionalDragSources").ClearArray();
        definition.FindPropertyRelative("worldTemplate").objectReferenceValue = null;
        definition.FindPropertyRelative("footprint").vector2IntValue = preset.Footprint;
        definition.FindPropertyRelative("placementOffset").vector3Value = Vector3.zero;
        definition.FindPropertyRelative("availableCount").intValue = 1;
        definition.FindPropertyRelative("hideSourceWhenExhausted").boolValue = true;
        definition.FindPropertyRelative("sourceVisualRoot").objectReferenceValue = null;
        definition.FindPropertyRelative("isBgmScrollBar").boolValue = preset.IsBgmScrollBar;
        definition.FindPropertyRelative("isBrightnessScrollBar").boolValue = preset.IsBrightnessScrollBar;
        definition.FindPropertyRelative("isRandomStepBlock").boolValue = preset.IsRandomStepBlock;
        definition.FindPropertyRelative("isUpwardDropdownBlock").boolValue = preset.IsUpwardDropdownBlock;
        definition.FindPropertyRelative("isPopupBlock").boolValue = preset.IsPopupBlock;
        definition.FindPropertyRelative("isPauseBlock").boolValue = preset.IsPauseBlock;
        definition.FindPropertyRelative("isRetryBlock").boolValue = preset.IsRetryBlock;
        definition.FindPropertyRelative("isSaveBlock").boolValue = preset.IsSaveBlock;
        definition.FindPropertyRelative("usesDynamicCollider").boolValue =
            preset.IsRandomStepBlock || preset.IsUpwardDropdownBlock || preset.IsPopupBlock;
        definition.FindPropertyRelative("moveSpeed").floatValue = 5f;
        definition.FindPropertyRelative("jumpPower").floatValue = 15f;
        definition.FindPropertyRelative("bgmTrackSource").objectReferenceValue = null;
        definition.FindPropertyRelative("bgmHandleSource").objectReferenceValue = null;
        definition.FindPropertyRelative("brightnessIconSprite").objectReferenceValue = null;
        definition.FindPropertyRelative("brightnessScrollBarSprite").objectReferenceValue = null;
        definition.FindPropertyRelative("pausePlaySprite").objectReferenceValue = null;
        definition.FindPropertyRelative("saveLoadSprite").objectReferenceValue = null;
    }

    private static void ClearMismatchedBrightnessReferences(SerializedProperty definition)
    {
        SerializedProperty dragSource = definition.FindPropertyRelative("dragSource");
        SerializedProperty sourceVisualRoot = definition.FindPropertyRelative("sourceVisualRoot");
        if (!IsBrightnessSource(dragSource.objectReferenceValue) &&
            !IsBrightnessSource(sourceVisualRoot.objectReferenceValue))
        {
            return;
        }

        dragSource.objectReferenceValue = null;
        sourceVisualRoot.objectReferenceValue = null;
        definition.FindPropertyRelative("additionalDragSources").ClearArray();
        definition.FindPropertyRelative("bgmTrackSource").objectReferenceValue = null;
        definition.FindPropertyRelative("bgmHandleSource").objectReferenceValue = null;
        definition.FindPropertyRelative("brightnessIconSprite").objectReferenceValue = null;
        definition.FindPropertyRelative("brightnessScrollBarSprite").objectReferenceValue = null;
    }

    private static bool IsBrightnessSource(UnityEngine.Object reference)
    {
        GameObject sourceObject = reference switch
        {
            GameObject gameObject => gameObject,
            Component component => component.gameObject,
            _ => null
        };

        for (Transform current = sourceObject != null ? sourceObject.transform : null;
             current != null;
             current = current.parent)
        {
            if (string.Equals(current.name, "BrightnessScrollBar", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
        RectTransform parent = preset.IsBrightnessScrollBar
            ? FindSourceInScene("BlockBG")
            : FindSourceInScene("BlockListRoot");
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
        sourceObject.layer = parent.gameObject.layer;

        RectTransform source = sourceObject.GetComponent<RectTransform>();
        source.SetParent(parent, false);
        source.SetAsLastSibling();
        source.sizeDelta = preset.IsBrightnessScrollBar
            ? new Vector2(100f, 400f)
            : preset.IsScrollBar
                ? new Vector2(400f, 100f)
                : preset.IsRetryBlock
                    ? new Vector2(preset.Footprint.x * 100f, preset.Footprint.y * 100f)
                    : preset.IsSaveBlock
                        ? new Vector2(200f, 100f)
                    : preset.IsPauseBlock
                    ? new Vector2(100f, 100f)
                    : string.Equals(preset.Name, "Jump", StringComparison.OrdinalIgnoreCase)
                        ? new Vector2(preset.Footprint.x * 100f, preset.Footprint.y * 100f)
                        : new Vector2(72f, 72f);

        Image image = sourceObject.GetComponent<Image>();
        image.sprite = preset.IsBrightnessScrollBar
            ? FindSprite("Assets/Sprites/UI/BrightnessScrollBar.png", "BrightnessScrollBar_0")
            : preset.IsBgmScrollBar
                ? FindSprite("Assets/Sprites/UI/BGMScrollBarShadow.png", "BGMScrollBarShadow_0")
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
            if (preset.IsBrightnessScrollBar)
            {
                PositionBrightnessSource(source, parent);
            }
            else
            {
                PositionAfterExistingSources(source, parent);
            }
        }

        return source;
    }

    private void EnsureAdditionalSources(
        BlockPreset preset,
        SerializedProperty definition,
        int totalCount)
    {
        RectTransform primarySource =
            definition.FindPropertyRelative("dragSource").objectReferenceValue as RectTransform;
        GameObject primaryRoot =
            definition.FindPropertyRelative("sourceVisualRoot").objectReferenceValue as GameObject;
        if (primarySource == null || primaryRoot == null)
        {
            return;
        }

        SerializedProperty additionalSources =
            definition.FindPropertyRelative("additionalDragSources");
        int desiredAdditionalCount = Mathf.Max(0, totalCount - 1);

        for (int i = additionalSources.arraySize - 1; i >= desiredAdditionalCount; i--)
        {
            SerializedProperty element = additionalSources.GetArrayElementAtIndex(i);
            RectTransform extra = element.objectReferenceValue as RectTransform;
            element.objectReferenceValue = null;
            additionalSources.DeleteArrayElementAtIndex(i);
            if (extra != null)
            {
                Undo.DestroyObjectImmediate(extra.gameObject);
            }
        }

        for (int i = 0; i < desiredAdditionalCount; i++)
        {
            RectTransform extra = i < additionalSources.arraySize
                ? additionalSources.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform
                : null;
            if (extra == null)
            {
                GameObject clone = Instantiate(primaryRoot, primaryRoot.transform.parent, false);
                Undo.RegisterCreatedObjectUndo(clone, $"{preset.Name}ブロックを複製");
                clone.name = $"{preset.Name} ({i + 2})";
                extra = clone.GetComponent<RectTransform>();
                if (extra == null)
                {
                    Undo.DestroyObjectImmediate(clone);
                    continue;
                }

                if (i >= additionalSources.arraySize)
                {
                    additionalSources.InsertArrayElementAtIndex(additionalSources.arraySize);
                }

                additionalSources.GetArrayElementAtIndex(i).objectReferenceValue = extra;
                RectTransform parent = extra.parent as RectTransform;
                if (parent != null && parent.GetComponent<LayoutGroup>() == null)
                {
                    PositionAfterExistingSources(extra, parent);
                }
            }

            extra.name = $"{preset.Name} ({i + 2})";
            if (primaryRoot.transform is RectTransform primaryRootRect)
            {
                extra.sizeDelta = primaryRootRect.sizeDelta;
            }
            SetSourceActive(extra.gameObject, true);
            EditorUtility.SetDirty(extra);
        }
    }

    private static void SetAdditionalSourcesActive(
        SerializedProperty definition,
        bool active)
    {
        SerializedProperty additionalSources =
            definition.FindPropertyRelative("additionalDragSources");
        for (int i = 0; i < additionalSources.arraySize; i++)
        {
            RectTransform source = additionalSources.GetArrayElementAtIndex(i)
                .objectReferenceValue as RectTransform;
            SetSourceActive(source != null ? source.gameObject : null, active);
        }
    }

    private static Image EnsureBgmHandleSource(
        GameObject sourceRoot,
        Image track,
        Image currentHandle,
        Sprite handleSprite)
    {
        Image handle = FindBgmHandle(sourceRoot, track, currentHandle);
        if (handle != null && handle != track)
        {
            Undo.RecordObject(handle, "音量バーの旧ハンドル表示を隠す");
            handle.enabled = false;
            EditorUtility.SetDirty(handle);
            return handle;
        }

        GameObject handleObject = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        Undo.RegisterCreatedObjectUndo(handleObject, "音量バーのハンドル参照を追加");
        handleObject.layer = track.gameObject.layer;
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.SetParent(track.transform, false);
        handleRect.sizeDelta = new Vector2(100f, 100f);

        handle = handleObject.GetComponent<Image>();
        handle.sprite = handleSprite;
        handle.color = Color.white;
        handle.preserveAspect = true;
        handle.raycastTarget = false;
        handle.enabled = false;
        return handle;
    }

    private void PlaceBrightnessSourceInBlockBg(RectTransform source, GameObject sourceRoot)
    {
        RectTransform blockBg = FindSourceInScene("BlockBG");
        if (blockBg == null || sourceRoot == null)
        {
            return;
        }

        if (!string.Equals(sourceRoot.name, "BrightnessScrollBar", StringComparison.Ordinal))
        {
            Undo.RecordObject(sourceRoot, "明るさバーの名称を変更");
            sourceRoot.name = "BrightnessScrollBar";
            EditorUtility.SetDirty(sourceRoot);
        }
        if (source.gameObject != sourceRoot &&
            !string.Equals(source.name, "BrightnessScrollBar", StringComparison.Ordinal))
        {
            Undo.RecordObject(source.gameObject, "明るさバーImageの名称を変更");
            source.name = "BrightnessScrollBar";
            EditorUtility.SetDirty(source.gameObject);
        }

        Transform rootTransform = sourceRoot.transform;
        if (rootTransform.parent != blockBg)
        {
            Undo.SetTransformParent(rootTransform, blockBg, "明るさバーをBlockBGへ移動");
            rootTransform.SetAsLastSibling();
        }

        SetLayerRecursively(sourceRoot, blockBg.gameObject.layer);

        SetSourceActive(sourceRoot, true);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.gameObject.layer == layer)
            {
                continue;
            }

            Undo.RecordObject(child.gameObject, "明るさバーのUIレイヤーを設定");
            child.gameObject.layer = layer;
            EditorUtility.SetDirty(child.gameObject);
        }
    }

    private void PositionBrightnessSource(RectTransform source, RectTransform blockBg)
    {
        RectTransform blockListRoot = FindSourceInScene("BlockListRoot");
        RectTransform positionReference = blockListRoot != null && blockListRoot.parent == blockBg
            ? blockListRoot
            : blockBg;
        PositionAfterExistingSources(source, positionReference);
    }

    private void EnsureBrightnessInfrastructure()
    {
        BlockManager manager = (BlockManager)target;
        SerializedProperty controllerProperty = serializedObject.FindProperty("brightnessController");
        ScreenBrightnessController controller =
            controllerProperty.objectReferenceValue as ScreenBrightnessController;
        if (controller == null || controller.gameObject.scene != manager.gameObject.scene)
        {
            foreach (ScreenBrightnessController candidate in FindObjectsByType<ScreenBrightnessController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene == manager.gameObject.scene)
                {
                    controller = candidate;
                    break;
                }
            }
        }

        if (controller == null)
        {
            GameObject controllerObject = new GameObject("ScreenBrightnessController");
            Undo.RegisterCreatedObjectUndo(controllerObject, "明るさ制御を追加");
            SceneManager.MoveGameObjectToScene(controllerObject, manager.gameObject.scene);
            controller = Undo.AddComponent<ScreenBrightnessController>(controllerObject);
        }

        Canvas canvas = null;
        foreach (Canvas candidate in FindObjectsByType<Canvas>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == manager.gameObject.scene && candidate.isRootCanvas)
            {
                canvas = candidate;
                break;
            }
        }

        Image overlay = FindSceneComponentByName<Image>(manager, "BrightnessOverlay");
        RectTransform visibilityLayer = FindSceneComponentByName<RectTransform>(
            manager, "BrightnessVisibilityLayer");
        if (canvas != null)
        {
            if (overlay == null)
            {
                GameObject overlayObject = new GameObject(
                    "BrightnessOverlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                Undo.RegisterCreatedObjectUndo(overlayObject, "明るさOverlayを追加");
                overlayObject.layer = canvas.gameObject.layer;
                RectTransform rect = overlayObject.GetComponent<RectTransform>();
                rect.SetParent(canvas.transform, false);
                StretchToParent(rect);
                rect.SetAsFirstSibling();
                overlay = overlayObject.GetComponent<Image>();
                overlay.color = new Color(0.23529412f, 0.23529412f, 0.23529412f, 0f);
                overlay.raycastTarget = false;
            }

            if (visibilityLayer == null)
            {
                GameObject layerObject = new GameObject(
                    "BrightnessVisibilityLayer",
                    typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(layerObject, "明るさ前面レイヤーを追加");
                layerObject.layer = canvas.gameObject.layer;
                visibilityLayer = layerObject.GetComponent<RectTransform>();
                visibilityLayer.SetParent(canvas.transform, false);
                StretchToParent(visibilityLayer);
                visibilityLayer.SetSiblingIndex(Mathf.Min(1, canvas.transform.childCount - 1));
            }
        }

        SerializedObject controllerObjectData = new SerializedObject(controller);
        controllerObjectData.FindProperty("darknessOverlay").objectReferenceValue = overlay;
        controllerObjectData.FindProperty("visibilityLayer").objectReferenceValue = visibilityLayer;
        controllerObjectData.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        controllerProperty.objectReferenceValue = controller;
    }

    private static T FindSceneComponentByName<T>(BlockManager manager, string objectName)
        where T : Component
    {
        foreach (T component in FindObjectsByType<T>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (component.gameObject.scene == manager.gameObject.scene &&
                string.Equals(component.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return component;
            }
        }

        return null;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
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
        Sprite fallback = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset is not Sprite sprite)
            {
                continue;
            }

            fallback ??= sprite;
            if (string.Equals(sprite.name, spriteName, StringComparison.Ordinal))
            {
                return sprite;
            }
        }

        return fallback;
    }

    private static void PositionAfterExistingSources(RectTransform source, RectTransform parent)
    {
        bool found = false;
        float rightEdge = 0f;
        float y = 0f;
        foreach (Transform childTransform in parent)
        {
            if (childTransform is not RectTransform child)
            {
                continue;
            }

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

