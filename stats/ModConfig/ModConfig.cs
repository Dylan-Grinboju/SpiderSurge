using Silk;
using Logger = Silk.Logger;
using System;
using UnityEngine;

namespace StatsMod
{
    public static class ModConfig
    {
        private const string ModId = StatsMod.ModId;

        // Display settings
        public static bool ShowStatsWindow => Config.GetModConfigValue(ModId, "display.showStatsWindow", true);
        public static bool ShowPlayers => Config.GetModConfigValue(ModId, "display.showPlayers", true);
        public static bool ShowPlayTime => Config.GetModConfigValue(ModId, "display.showPlayTime", true);
        public static bool ShowEnemyDeaths => Config.GetModConfigValue(ModId, "display.showEnemyDeaths", true);

        public static int DisplayPositionX => ValidatePositionX(Config.GetModConfigValue(ModId, "display.position.x", 10));
        public static int DisplayPositionY => ValidatePositionY(Config.GetModConfigValue(ModId, "display.position.y", 10));

        // UI Scaling settings
        public static bool AutoScale => Config.GetModConfigValue(ModId, "display.autoScale", true);
        public static float UIScale => ValidateScale(Config.GetModConfigValue(ModId, "display.uiScale", 1.0f));
        public static float BigUIOpacity => ValidateOpacity(Config.GetModConfigValue(ModId, "display.bigUIOpacity", 100f));
        public static bool SmallUIShowBackground => Config.GetModConfigValue(ModId, "display.smallUIShowBackground", true);

        // BigUI Column settings (Player Name is always required, all others are optional)
        public static bool BigUIShowKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.kills", true);
        public static bool BigUIShowDeaths => Config.GetModConfigValue(ModId, "display.bigUI.columns.deaths", true);
        public static bool BigUIShowMaxKillStreak => Config.GetModConfigValue(ModId, "display.bigUI.columns.maxKillStreak", true);
        public static bool BigUIShowCurrentKillStreak => Config.GetModConfigValue(ModId, "display.bigUI.columns.currentKillStreak", false);
        public static bool BigUIShowMaxSoloKillStreak => Config.GetModConfigValue(ModId, "display.bigUI.columns.maxSoloKillStreak", false);
        public static bool BigUIShowCurrentSoloKillStreak => Config.GetModConfigValue(ModId, "display.bigUI.columns.currentSoloKillStreak", false);
        public static bool BigUIShowFriendlyKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.friendlyKills", true);
        public static bool BigUIShowEnemyShields => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyShields", true);
        public static bool BigUIShowShieldsLost => Config.GetModConfigValue(ModId, "display.bigUI.columns.shieldsLost", true);
        public static bool BigUIShowFriendlyShields => Config.GetModConfigValue(ModId, "display.bigUI.columns.friendlyShields", true);
        public static bool BigUIShowAliveTime => Config.GetModConfigValue(ModId, "display.bigUI.columns.aliveTime", false);
        public static bool BigUIShowWaveClutches => Config.GetModConfigValue(ModId, "display.bigUI.columns.waveClutches", false);
        public static bool BigUIShowWebSwings => Config.GetModConfigValue(ModId, "display.bigUI.columns.webSwings", false);
        public static bool BigUIShowWebSwingTime => Config.GetModConfigValue(ModId, "display.bigUI.columns.webSwingTime", false);
        public static bool BigUIShowAirborneTime => Config.GetModConfigValue(ModId, "display.bigUI.columns.airborneTime", false);
        public static bool BigUIShowLavaDeaths => Config.GetModConfigValue(ModId, "display.bigUI.columns.lavaDeaths", false);
        public static bool BigUIShowAstralReturns => Config.GetModConfigValue(ModId, "display.bigUI.columns.astralReturns", false);

        // Computed stat columns
        public static bool BigUIShowTotalOffence => Config.GetModConfigValue(ModId, "display.bigUI.columns.totalOffence", false);
        public static bool BigUIShowTotalFriendlyHits => Config.GetModConfigValue(ModId, "display.bigUI.columns.totalFriendlyHits", false);
        public static bool BigUIShowTotalHitsTaken => Config.GetModConfigValue(ModId, "display.bigUI.columns.totalHitsTaken", false);

        // Enemy kill columns
        public static bool BigUIShowWaspKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyKills.wasp", false);
        public static bool BigUIShowPowerWaspKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyKills.powerWasp", false);
        public static bool BigUIShowRollerKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyKills.roller", false);
        public static bool BigUIShowWhispKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyKills.whisp", false);
        public static bool BigUIShowPowerWhispKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyKills.powerWhisp", false);
        public static bool BigUIShowMeleeWhispKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyKills.meleeWhisp", false);
        public static bool BigUIShowPowerMeleeWhispKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyKills.powerMeleeWhisp", false);
        public static bool BigUIShowKhepriKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyKills.khepri", false);
        public static bool BigUIShowPowerKhepriKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyKills.powerKhepri", false);
        public static bool BigUIShowHornetShamanKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyKills.hornetShaman", false);
        public static bool BigUIShowHornetKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.enemyKills.hornet", false);

        // Weapon kill columns
        public static bool BigUIShowShotgunKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.weaponKills.shotgun", false);
        public static bool BigUIShowRailShotKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.weaponKills.railShot", false);
        public static bool BigUIShowDeathCubeKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.weaponKills.deathCube", false);
        public static bool BigUIShowDeathRayKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.weaponKills.deathRay", false);
        public static bool BigUIShowEnergyBallKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.weaponKills.energyBall", false);
        public static bool BigUIShowParticleBladeKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.weaponKills.particleBlade", false);
        public static bool BigUIShowKhepriStaffKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.weaponKills.khepriStaff", false);
        public static bool BigUIShowLaserCannonKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.weaponKills.laserCannon", false);
        public static bool BigUIShowLaserCubeKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.weaponKills.laserCube", false);
        public static bool BigUIShowSawDiscKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.weaponKills.sawDisc", false);
        public static bool BigUIShowExplosionKills => Config.GetModConfigValue(ModId, "display.bigUI.columns.weaponKills.explosions", false);

        // Tracking settings
        public static bool TrackingEnabled => Config.GetModConfigValue(ModId, "tracking.enabled", true);
        public static bool SaveStatsToFile => Config.GetModConfigValue(ModId, "tracking.saveStatsToFile", true);

        // Updater settings
        public static bool CheckForUpdates => Config.GetModConfigValue(ModId, "updater.checkForUpdates", true);

        // Titles settings
        public static bool TitlesEnabled => Config.GetModConfigValue(ModId, "titles.enabled", true);
        public static float TitlesRevealDelaySeconds => ValidateTitlesRevealDelay(Config.GetModConfigValue(ModId, "titles.revealDelaySeconds", 2.0f));

        // Input settings
        public static string CycleInput => ValidateCycleInput(Config.GetModConfigValue(ModId, "input.cycleInput", "dpad"));

        private static float ValidateTitlesRevealDelay(float value)
        {
            if (value < 0f)
            {
                Logger.LogWarning($"Titles reveal delay {value} is too small, clamping to 0");
                return 0f;
            }
            if (value > 10f)
            {
                Logger.LogWarning($"Titles reveal delay {value} is too large, clamping to 10");
                return 10f;
            }
            return value;
        }

        public static void SetDisplayPosition(int x, int y)
        {
            // Validate the values before setting them
            x = ValidatePositionX(x);
            y = ValidatePositionY(y);

            Config.SetModConfigValue(ModId, "display.position.x", x);
            Config.SetModConfigValue(ModId, "display.position.y", y);
        }

        // Validation methods to ensure config values are within acceptable ranges
        private static int ValidatePositionX(int value)
        {
            if (value < 0)
            {
                Logger.LogWarning($"Position value {value} is negative, clamping to 0");
                return 0;
            }
            if (Screen.width <= value)
            {
                Logger.LogWarning($"Position value {value} exceeds screen width {Screen.width}, clamping to 0");
                return 0;
            }
            return value;
        }

        private static int ValidatePositionY(int value)
        {
            if (value < 0)
            {
                Logger.LogWarning($"Position value {value} is negative, clamping to 0");
                return 0;
            }
            if (Screen.height <= value)
            {
                Logger.LogWarning($"Position value {value} exceeds screen height {Screen.height}, clamping to 0");
                return 0;
            }
            return value;
        }

        private static float ValidateScale(float value)
        {
            if (value < 0.5f)
            {
                Logger.LogWarning($"UI Scale value {value} is too small, clamping to 0.5");
                return 0.5f;
            }
            if (value > 3.0f)
            {
                Logger.LogWarning($"UI Scale value {value} is too large, clamping to 3.0");
                return 3.0f;
            }
            return value;
        }

        private static float ValidateOpacity(float value)
        {
            if (value < 0f)
            {
                Logger.LogWarning($"BigUI Opacity value {value} is too small, clamping to 0");
                return 0f;
            }
            if (value > 100f)
            {
                Logger.LogWarning($"BigUI Opacity value {value} is too large, clamping to 100");
                return 100f;
            }
            return value;
        }

        private static string ValidateCycleInput(string value)
        {
            string lower = value.ToLower();
            if (lower == "none" || lower == "dpad" || lower == "joystick" || lower == "both")
            {
                return lower;
            }
            Logger.LogWarning($"Invalid CycleInput value '{value}', defaulting to 'dpad'");
            return "dpad";
        }
    }
}
