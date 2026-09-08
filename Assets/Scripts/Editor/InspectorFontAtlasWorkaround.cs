using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

/// <summary>Workaround for Unity 6000.1.0f1 persisting temporary Inspector font atlases.</summary>
[InitializeOnLoad]
internal static class InspectorFontAtlasWorkaround
{
    private static bool installed;

    static InspectorFontAtlasWorkaround()
    {
        // TextCore registers its callback during Editor initialization. Wrap it afterward.
        EditorApplication.delayCall += Install;
    }

    private static void Install()
    {
        // This accesses an internal hook verified against this Editor version only.
        if (installed || Application.unityVersion != "6000.1.0f1")
        {
            return;
        }

        FieldInfo callbackField = typeof(FontAsset).GetField("OnFontAssetTextureChanged",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (callbackField?.GetValue(null) is not Action<Texture, FontAsset> callbacks)
        {
            return;
        }

        Action<Texture, FontAsset> replacement = null;
        foreach (Action<Texture, FontAsset> callback in callbacks.GetInvocationList())
        {
            if (callback.Method.DeclaringType?.FullName ==
                    "UnityEditor.TextCore.Text.TextEditorResourceManager" &&
                callback.Method.Name == "AddTextureToAsset")
            {
                // Inspector fallback fonts inherit DontSaveInEditor onto new atlas pages.
                // Keep those pages in memory; never clear their flags or save them as assets.
                replacement += (texture, font) =>
                {
                    if (texture != null && (texture.hideFlags & HideFlags.DontSaveInEditor) != 0)
                    {
                        return;
                    }
                    callback(texture, font);
                };
                installed = true;
            }
            else
            {
                replacement += callback;
            }
        }

        if (installed)
        {
            callbackField.SetValue(null, replacement);
        }
    }
}
