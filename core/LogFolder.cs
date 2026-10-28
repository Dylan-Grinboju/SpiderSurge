using System;
using System.IO;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    // Silk/Logs next to the game, where each part keeps its own folder of log files
    internal static class LogFolder
    {
        public static string Root
        {
            get
            {
                string gameDirectory = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(string.IsNullOrEmpty(gameDirectory) ? Environment.CurrentDirectory : gameDirectory, "Silk", "Logs");
            }
        }

        // Falls back to the current directory when the folder can't be made
        public static string Create(string name, string label)
        {
            try
            {
                string folder = Path.Combine(Root, name);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    Logger.LogInfo($"Created {label} log directory: {folder}");
                }
                return folder;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create Silk/Logs/{name} directory, using current directory: {ex.Message}");
                return Environment.CurrentDirectory;
            }
        }
    }
}
