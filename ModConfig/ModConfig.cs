using Silk;
using Logger = Silk.Logger;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSurge
{
    public static class ModConfig
    {
        private const string ModId = SpiderSurgeMod.ModId;

        // Updater settings
        public static bool CheckForUpdates => Config.GetModConfigValue<bool>(ModId, "updater.checkForUpdates", true);
    }
}
