using System;
using System.Collections.Generic;
using HarmonyLib;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    // The game's own save, where versions before the progress file kept the achievements and Surge highscores
    internal static class GameSave
    {
        private static readonly AccessTools.FieldRef<GameSaveWrapper, ES3File> ContainerRef = AccessTools.FieldRefAccess<GameSaveWrapper, ES3File>("currentSaveFile");
        private static readonly AccessTools.FieldRef<GameSaveWrapper, bool> LocalRef = AccessTools.FieldRefAccess<GameSaveWrapper, bool>("localSaveGameCore");

        public static bool Ready => GameSaveWrapper.Instance != null && GameSaveWrapper.Initialized;

        // The Xbox app keeps the save in a cloud save container that GameSaveWrapper holds in memory; Steam keeps it in SaveFile.es3
        private static ES3File Container => LocalRef(GameSaveWrapper.Instance) ? null : ContainerRef(GameSaveWrapper.Instance);

        public static string[] Keys()
        {
            try
            {
                ES3File container = Container;
                if (container != null) return container.GetKeys();
                return ES3.FileExists() ? ES3.GetKeys() : Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Couldn't list the game's save: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public static bool Has(string key)
        {
            try
            {
                ES3File container = Container;
                return container != null ? container.KeyExists(key) : ES3.KeyExists(key);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Couldn't read the game's save: {ex.Message}");
                return false;
            }
        }

        public static T Load<T>(string key, T fallback) => GameSaveWrapper.Instance.Load(key, fallback);

        public static void Delete(IEnumerable<string> keys)
        {
            try
            {
                ES3File container = Container;
                foreach (string key in keys)
                {
                    if (container != null) container.DeleteKey(key);
                    else ES3.DeleteKey(key);
                }
                if (container != null) GameSaveWrapper.Instance.SaveData("GameSave", container.GetBytes());
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Couldn't remove the old SpiderSurge entries from the game's save: {ex.Message}");
            }
        }
    }
}
