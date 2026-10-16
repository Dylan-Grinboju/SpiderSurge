using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public static class IconLoader
    {
        private static Dictionary<string, Sprite> _loadedIcons = new Dictionary<string, Sprite>();
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
                        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (stream != null)
                            {
                                byte[] fileData = new byte[stream.Length];
                                stream.Read(fileData, 0, fileData.Length);

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
            if (lastDot >= 0)
            {
                return withoutExtension.Substring(lastDot + 1);
            }
            return withoutExtension;
        }

        public static Sprite GetIcon(string name)
        {
            if (_loadedIcons.TryGetValue(name, out Sprite sprite))
            {
                return sprite;
            }
            return null;
        }

        private static Sprite LoadSpriteFromData(byte[] data)
        {
            try
            {
                Texture2D texture = new Texture2D(2, 2);
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
}
