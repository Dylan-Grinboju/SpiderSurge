using Silk;
using Logger = Silk.Logger;
using System;
using UnityEngine;

namespace SpiderSurge
{
    public static class ModConfig
    {
        private const string ModId = SpiderSurge.ModId;

        // Display settings
        public static bool enableSurgeMode => Config.GetModConfigValue(ModId, "EnableSurgeMode", true);

    }
}
