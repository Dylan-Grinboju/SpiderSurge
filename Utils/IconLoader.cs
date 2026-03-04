using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Logger = Silk.Logger;

namespace SpiderSurge;

public static class IconLoader
{
    private static Dictionary<string, Sprite> _loadedIcons = [];
    private static bool _initialized = false;

    public static void LoadAllIcons()
    {
        if (_initialized) return;

        try
        {
            LoadEmbeddedIcons();
            _initialized = true;
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"[IconLoader] Error loading icons: {ex.Message}");
        }
    }

    private static void LoadEmbeddedIcons()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string[] resourceNames = assembly.GetManifestResourceNames();

        foreach (string resourceName in resourceNames)
        {
            if (resourceName.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                string iconName = ExtractIconName(resourceName);

                try
                {
                    using Stream stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        using MemoryStream ms = new();
                        stream.CopyTo(ms);
                        byte[] fileData = ms.ToArray();

                        Sprite sprite = LoadSpriteFromData(fileData);
                        if (sprite != null)
                        {
                            _loadedIcons[iconName] = sprite;
                        }
                    }
                    else
                    {
                        Logger.LogWarning($"[IconLoader] Could not load resource stream: {resourceName}");
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[IconLoader] Failed to load embedded icon '{resourceName}': {ex.Message}");
                }
            }
        }
    }

    private static string ExtractIconName(string resourceName)
    {
        string withoutExtension = resourceName.Substring(0, resourceName.Length - 4);
        int lastDot = withoutExtension.LastIndexOf('.');
        return lastDot >= 0 ? withoutExtension.Substring(lastDot + 1) : withoutExtension;
    }

    public static Sprite GetIcon(string name) => _loadedIcons.TryGetValue(name, out Sprite sprite) ? sprite : null;

    private static Sprite LoadSpriteFromData(byte[] data)
    {
        try
        {
            Texture2D texture = new(2, 2);
            if (texture.LoadImage(data))
            {
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"[IconLoader] Failed to load sprite from data: {ex.Message}");
        }
        return null;
    }
}
