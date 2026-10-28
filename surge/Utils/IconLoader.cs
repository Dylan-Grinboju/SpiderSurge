using UnityEngine;
using System.Collections.Generic;
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
            EmbeddedResources.Load(".png", "IconLoader", "icon", nameof(LoadSpriteFromData), (iconName, fileData) =>
            {
                Sprite sprite = LoadSpriteFromData(fileData);
                if (sprite != null)
                {
                    _loadedIcons[iconName] = sprite;
                }
            });
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
