using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Run explicitly with Unity -batchmode -executeMethod OutlineRegressionCheck.Run.
// This editor-only check is never invoked automatically or included in the game build.
public static class OutlineRegressionCheck
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static void Run()
    {
        if (!Application.isBatchMode)
        {
            throw new InvalidOperationException("Run this check in a separate Unity batchmode process.");
        }

        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CheckContact();
            CheckOverlay();
            Debug.Log("OUTLINE_REGRESSION_PASS: contact gaps, solid contact, no colliders/raycast, overlay ring above darkness and track, transparent center, sliced sprite, player occlusion at every brightness.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckContact()
    {
        BlockManager manager = new GameObject("Manager").AddComponent<BlockManager>();
        Rigidbody2D player = new GameObject("Player").AddComponent<Rigidbody2D>();
        player.gravityScale = 0;
        BoxCollider2D playerCollider = player.gameObject.AddComponent<BoxCollider2D>();
        BoxCollider2D handle = new GameObject("Handle").AddComponent<BoxCollider2D>();
        typeof(BlockManager).GetField("playerBody", Flags).SetValue(manager, player);
        typeof(BlockManager).GetField("playerCollider", Flags).SetValue(manager, playerCollider);
        Type blockType = typeof(BlockManager).GetNestedType("PlacedBlock", BindingFlags.NonPublic);
        object block = Activator.CreateInstance(blockType, true);
        blockType.GetField("bgmHandle", Flags).SetValue(block, handle.transform);
        blockType.GetField("bgmHandleCollider", Flags).SetValue(block, handle);
        MethodInfo attach = typeof(BlockManager).GetMethod("TryAttachPlayerToBgmHandle", Flags);
        foreach (float gap in new[] { 0.19f, 0.06f, 0.03f })
        {
            player.position = new Vector2(0, 1f + gap);
            Physics2D.SyncTransforms();
            Require(!(bool)attach.Invoke(manager, new[] { block }), "Attached above handle at gap " + gap);
        }
        player.position = new Vector2(0, 1f);
        Physics2D.SyncTransforms();
        Require((bool)attach.Invoke(manager, new[] { block }), "Actual handle contact must attach");
        player.position = new Vector2(1.1f, 1f);
        Physics2D.SyncTransforms();
        Require(!(bool)attach.Invoke(manager, new[] { block }), "Side miss must not attach");

        // A real resting polygon retains the solver's gap; zero-gap boxes alone do
        // not catch a tolerance that rejects a player standing on the handle.
        UnityEngine.Object.DestroyImmediate(playerCollider);
        PolygonCollider2D polygon = player.gameObject.AddComponent<PolygonCollider2D>();
        polygon.SetPath(0, new[]
        {
            new Vector2(0f, 0.351f), new Vector2(0.394f, -0.346f),
            new Vector2(0.394f, -0.45f), new Vector2(-0.396f, -0.45f),
            new Vector2(-0.396f, -0.346f)
        });
        typeof(BlockManager).GetField("playerCollider", Flags).SetValue(manager, polygon);
        player.position = new Vector2(0f, 3f);
        player.gravityScale = 4f;
        SimulationMode2D previousSimulationMode = Physics2D.simulationMode;
        try
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 180; i++) Physics2D.Simulate(0.02f);
            Require(polygon.IsTouching(handle), "Polygon must settle onto the handle");
            Require((bool)attach.Invoke(manager, new[] { block }), "Resting polygon contact must attach despite solver gap");
        }
        finally
        {
            Physics2D.simulationMode = previousSimulationMode;
        }
        UnityEngine.Object.DestroyImmediate(manager.gameObject);
        UnityEngine.Object.DestroyImmediate(player.gameObject);
        UnityEngine.Object.DestroyImmediate(handle.gameObject);
    }

    private static void CheckOverlay()
    {
        Shader shader = Resources.Load<Shader>("BlockOutlineOverlay");
        Require(shader != null && shader.isSupported, "Overlay shader missing or unsupported");
        Camera camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = 2;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        RenderTexture target = new RenderTexture(256, 256, 24);
        camera.targetTexture = target;
        Canvas canvas = new GameObject("Canvas", typeof(RectTransform)).AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        Image darkness = CreateImage(canvas.transform, "Darkness", new Color(0.235f, 0.235f, 0.235f));
        darkness.rectTransform.anchorMin = Vector2.zero;
        darkness.rectTransform.anchorMax = Vector2.one;
        darkness.rectTransform.sizeDelta = Vector2.zero;
        RectTransform layer = new GameObject("Visibility", typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(canvas.transform, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.sizeDelta = Vector2.zero;
        Texture2D texture = new Texture2D(16, 16);
        Color[] white = new Color[256];
        for (int i = 0; i < white.Length; i++) white[i] = Color.white;
        texture.SetPixels(white);
        texture.Apply();
        SpriteRenderer source = new GameObject("Source").AddComponent<SpriteRenderer>();
        source.sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16,
            0, SpriteMeshType.FullRect, Vector4.one * 4f);
        BoxCollider2D sourceCollider = source.gameObject.AddComponent<BoxCollider2D>();
        Vector2 colliderSize = sourceCollider.size;
        BlockOutlineGraphic outline = BlockOutlineGraphic.Create(layer, source, camera);
        Require(outline != null, "Overlay creation failed");
        // Deliberately create track after the outline to exercise the LateUpdate ordering.
        Image track = CreateImage(layer, "Track", new Color(0.35f, 0.35f, 0.35f));
        track.rectTransform.sizeDelta = new Vector2(220, 16);
        outline.SetOutline(true, Color.white, 0.06f);
        typeof(BlockOutlineGraphic).GetMethod("LateUpdate", Flags).Invoke(outline, null);
        Require(!outline.raycastTarget, "Outline intercepts pointer input");
        Require(outline.GetComponentsInChildren<Collider2D>().Length == 0, "Outline has physics collider");
        Require(sourceCollider.size == colliderSize, "Outline changed source collider");
        Require(outline.transform.GetSiblingIndex() > track.transform.GetSiblingIndex(), "Track covers outline");
        Texture2D pixels = Render(camera, target);
        Require(pixels.GetPixel(163, 128).r > 0.9f, "Handle outline must cover track");
        Require(pixels.GetPixel(128, 163).r > 0.9f, "Outline must cover darkness");
        Require(pixels.GetPixel(128, 128).r < 0.8f, "Outline incorrectly fills block center");
        File.WriteAllBytes("Logs/outline-overlay-check.png", pixels.EncodeToPNG());

        source.drawMode = SpriteDrawMode.Sliced;
        source.size = new Vector2(2, 1);
        outline.SetVerticesDirty();
        pixels = Render(camera, target);
        Require(pixels.GetPixel(195, 128).r > 0.9f, "Sliced outline width/position incorrect");
        Require(pixels.GetPixel(128, 128).r < 0.8f, "Sliced outline filled center");
        outline.SetOutline(false, Color.white, 0.06f);
        pixels = Render(camera, target);
        Require(pixels.GetPixel(195, 128).r < 0.8f, "Hidden outline still rendered");
        foreach (var message in ShaderUtil.GetShaderMessages(shader))
        {
            Require(message.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, message.message);
        }

        CheckTrackOcclusion(camera, target, darkness, track, source);
    }

    private static void CheckTrackOcclusion(
        Camera camera, RenderTexture target, Image darkness, Image track, SpriteRenderer player)
    {
        Shader shader = Resources.Load<Shader>("BrightnessTrackOverlay");
        Require(shader != null && shader.isSupported, "Track shader missing or unsupported");
        Material material = new Material(shader);
        material.SetTexture("_PlayerTex", player.sprite.texture);
        material.SetVector("_PlayerUVRect", UnityEngine.Sprites.DataUtility.GetOuterUV(player.sprite));
        material.SetFloat("_PlayerMaskEnabled", 1f);
        track.material = material;
        track.color = new Color(0.8f, 0.8f, 0.8f);
        BrightnessTrackPlayerMask mask = track.gameObject.AddComponent<BrightnessTrackPlayerMask>();
        player.drawMode = SpriteDrawMode.Simple;

        foreach (float angle in new[] { 0f, 90f })
        {
            track.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            foreach (float brightness in new[] { 0f, 0.1f, 0.5f, 0.99f, 1f })
            {
                darkness.color = new Color(0.235f, 0.235f, 0.235f, 1f - brightness);
                track.gameObject.SetActive(false);
                Texture2D baseline = Render(camera, target);
                track.gameObject.SetActive(true);
                mask.Configure(player, camera);
                Texture2D actual = Render(camera, target);
                Require(Mathf.Abs(actual.GetPixel(128, 128).r - baseline.GetPixel(128, 128).r) < 0.02f,
                    "Track covered player at brightness " + brightness + ", angle " + angle);
                Vector2Int outsidePlayer = angle == 0f ? new Vector2Int(220, 128) : new Vector2Int(128, 220);
                Require(actual.GetPixel(outsidePlayer.x, outsidePlayer.y).r >
                        baseline.GetPixel(outsidePlayer.x, outsidePlayer.y).r + 0.1f,
                    "Track incorrectly disappeared outside player");
                UnityEngine.Object.DestroyImmediate(baseline);
                UnityEngine.Object.DestroyImmediate(actual);
            }
        }
        foreach (var message in ShaderUtil.GetShaderMessages(shader))
        {
            Require(message.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, message.message);
        }
        track.material = null;
        UnityEngine.Object.DestroyImmediate(material);
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Texture2D Render(Camera camera, RenderTexture target)
    {
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        Texture2D result = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
