using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

internal sealed class CursorController : MonoBehaviour
{
    private const string CursorSettingsResourceName = "CursorSettings";
    private const float ReferenceViewportHeight = 1080f;
    private const float ReferenceCursorSize = 80f;
    private const byte HotspotAlphaThreshold = 200;
    private const byte HotspotColorThreshold = 240;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameObject controllerObject = new GameObject(nameof(CursorController));
        DontDestroyOnLoad(controllerObject);
        controllerObject.AddComponent<CursorController>();
    }

    private void Awake()
    {
        CursorSettings settings = Resources.Load<CursorSettings>(CursorSettingsResourceName);
        Texture2D sourceTexture = settings != null ? settings.CursorTexture : null;
        if (sourceTexture == null)
        {
            Debug.LogError("The game cursor settings or cursor texture could not be loaded.");
            enabled = false;
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        Vector2 normalizedHotspot = CalculateNormalizedHotspot(sourceTexture);
        string imageUrl = "data:image/png;base64," +
            Convert.ToBase64String(sourceTexture.EncodeToPNG());

        CursorViewport_Install(
            imageUrl,
            ReferenceCursorSize,
            ReferenceViewportHeight,
            normalizedHotspot.x,
            normalizedHotspot.y);
        Cursor.visible = false;
#else
        Vector2 hotspot = CalculateNormalizedHotspot(sourceTexture);
        hotspot.Scale(new Vector2(sourceTexture.width, sourceTexture.height));
        Cursor.SetCursor(sourceTexture, hotspot, CursorMode.ForceSoftware);
#endif
    }

    private static Vector2 CalculateNormalizedHotspot(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int bestDistance = int.MaxValue;
        long sumX = 0;
        long sumY = 0;
        int pointCount = 0;

        for (int y = 0; y < texture.height; y++)
        {
            int topY = texture.height - 1 - y;
            for (int x = 0; x < texture.width; x++)
            {
                Color32 pixel = pixels[y * texture.width + x];
                if (pixel.a < HotspotAlphaThreshold ||
                    pixel.r < HotspotColorThreshold ||
                    pixel.g < HotspotColorThreshold ||
                    pixel.b < HotspotColorThreshold)
                {
                    continue;
                }

                int distance = x + topY;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    sumX = x;
                    sumY = topY;
                    pointCount = 1;
                }
                else if (distance == bestDistance)
                {
                    sumX += x;
                    sumY += topY;
                    pointCount++;
                }
            }
        }

        if (pointCount == 0)
        {
            return Vector2.zero;
        }

        float hotspotX = ((float)sumX / pointCount + 0.5f) / texture.width;
        float hotspotY = ((float)sumY / pointCount + 0.5f) / texture.height;
        return new Vector2(hotspotX, hotspotY);
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private void OnDestroy()
    {
        CursorViewport_Remove();
        Cursor.visible = true;
    }

    [DllImport("__Internal")]
    private static extern void CursorViewport_Install(
        string imageUrl,
        float referenceCursorSize,
        float referenceViewportHeight,
        float normalizedHotspotX,
        float normalizedHotspotY);

    [DllImport("__Internal")]
    private static extern void CursorViewport_Remove();
#endif
}
