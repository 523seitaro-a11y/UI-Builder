using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Playerを含むシーンを開いた際、Player.csとアニメーション用参照の不足分を補います。
/// </summary>
[InitializeOnLoad]
public static class PlayerAnimationSceneSetup
{
    static PlayerAnimationSceneSetup()
    {
        EditorApplication.delayCall += EnsureLoadedScenes;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EnsureScene(scene);
        }
    }

    private static void EnsureLoadedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            EnsureScene(SceneManager.GetSceneAt(i));
        }
    }

    private static void EnsureScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(candidate.name, "Player", StringComparison.Ordinal))
                {
                    continue;
                }

                EnsurePlayer(candidate.gameObject, scene);
            }
        }
    }

    private static void EnsurePlayer(GameObject playerObject, Scene scene)
    {
        Player player = playerObject.GetComponent<Player>();
        bool wasAdded = player == null;
        if (wasAdded)
        {
            player = Undo.AddComponent<Player>(playerObject);
        }

        SerializedObject serializedPlayer = new SerializedObject(player);
        serializedPlayer.Update();
        bool changed = wasAdded;
        changed |= AssignIfMissing(
            serializedPlayer.FindProperty("playerBody"),
            playerObject.GetComponent<Rigidbody2D>());
        changed |= AssignIfMissing(
            serializedPlayer.FindProperty("playerCollider"),
            playerObject.GetComponent<Collider2D>());
        changed |= AssignIfMissing(
            serializedPlayer.FindProperty("playerRenderer"),
            playerObject.GetComponent<SpriteRenderer>());
        changed |= AssignIfMissing(
            serializedPlayer.FindProperty("stageManager"),
            FindInScene<StageManager>(scene));

        changed |= AssignSprite(serializedPlayer, "idle0", "Assets/Sprites/Player/Idle0.png", "Idle0_0");
        changed |= AssignSprite(serializedPlayer, "idle1", "Assets/Sprites/Player/Idle1.png", "Idle1_0");
        changed |= AssignSprite(serializedPlayer, "run0", "Assets/Sprites/Player/Run0.png", "Run0_0");
        changed |= AssignSprite(serializedPlayer, "run1", "Assets/Sprites/Player/Run1.png", "Run1_0");
        changed |= AssignSprite(serializedPlayer, "run2", "Assets/Sprites/Player/Run2.png", "Run2_0");
        changed |= AssignSprite(serializedPlayer, "run3", "Assets/Sprites/Player/Run3.png", "Run3_0");
        changed |= AssignSprite(serializedPlayer, "jump0", "Assets/Sprites/Player/Jump0.png", "Jump0_0");
        changed |= AssignSprite(serializedPlayer, "jump1", "Assets/Sprites/Player/Jump1.png", "Jump1_0");
        changed |= AssignSprite(serializedPlayer, "jump2", "Assets/Sprites/Player/Jump2.png", "Jump2_0");
        changed |= AssignSprite(serializedPlayer, "jump3", "Assets/Sprites/Player/Jump3.png", "Jump3_0");

        if (!changed)
        {
            return;
        }

        serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool AssignSprite(
        SerializedObject target,
        string propertyName,
        string assetPath,
        string spriteName)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        return AssignIfMissing(property, FindSprite(assetPath, spriteName));
    }

    private static bool AssignIfMissing(
        SerializedProperty property,
        UnityEngine.Object value)
    {
        if (property == null || property.objectReferenceValue != null || value == null)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
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

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }
}
