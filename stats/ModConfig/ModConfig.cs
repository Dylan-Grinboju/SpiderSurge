using System.Collections.Generic;
using System.Linq;
using SpiderSurge.Common;
using UnityEngine;

namespace StatsMod
{
    public static class ModConfig
    {
        public const string Prefix = "stats.";
        public const string ColumnsPrefix = Prefix + "display.bigUI.columns.";
        public const string AutoScaleKey = Prefix + "display.autoScale";
        public const string UIScaleKey = Prefix + "display.uiScale";
        public const string PositionXKey = Prefix + "display.position.xPercent";
        public const string PositionYKey = Prefix + "display.position.yPercent";
        public const string SaveHistoryKey = Prefix + "tracking.saveHistory";
        public const float MinScale = 0.5f;
        public const float MaxScale = 3f;
        public const float MaxRevealDelay = 10f;
        private static readonly Dictionary<string, string> _keys = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _columnKeys = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _accuracyKeys = new Dictionary<string, string>();

        public static readonly KeyValuePair<string, object>[] Defaults = new (string key, object value)[]
        {
            ("display.showStatsWindow", true),
            ("display.startingPreset", "default"),
            ("display.showPlayers", true),
            ("display.showPlayTime", true),
            ("display.showEnemyDeaths", true),
            ("display.autoScale", true),
            ("display.uiScale", 1.0f),
            ("display.bigUIOpacity", 100f),
            ("display.smallUIShowBackground", false),
            ("display.position.xPercent", 0.5f),
            ("display.position.yPercent", 1f),
            ("display.bigUI.columns.totalOffence", true),
            ("display.bigUI.columns.totalFriendlyHits", true),
            ("display.bigUI.columns.totalHitsTaken", true),
            ("display.bigUI.columns.kills", false),
            ("display.bigUI.columns.deaths", false),
            ("display.bigUI.columns.maxKillStreak", true),
            ("display.bigUI.columns.currentKillStreak", false),
            ("display.bigUI.columns.maxSoloKillStreak", false),
            ("display.bigUI.columns.currentSoloKillStreak", false),
            ("display.bigUI.columns.friendlyKills", false),
            ("display.bigUI.columns.enemyShields", false),
            ("display.bigUI.columns.shieldsLost", false),
            ("display.bigUI.columns.friendlyShields", false),
            ("display.bigUI.columns.aliveTime", true),
            ("display.bigUI.columns.waveClutches", true),
            ("display.bigUI.columns.webSwings", false),
            ("display.bigUI.columns.webSwingTime", false),
            ("display.bigUI.columns.airborneTime", false),
            ("display.bigUI.columns.lavaDeaths", false),
            ("display.bigUI.columns.astralReturns", false),
            ("display.bigUI.columns.accuracy.overall", false),
            ("display.bigUI.columns.enemyKills.wasp", false),
            ("display.bigUI.columns.enemyKills.waspCocoon", false),
            ("display.bigUI.columns.enemyKills.powerWasp", false),
            ("display.bigUI.columns.enemyKills.roller", false),
            ("display.bigUI.columns.enemyKills.whisp", false),
            ("display.bigUI.columns.enemyKills.powerWhisp", false),
            ("display.bigUI.columns.enemyKills.meleeWhisp", false),
            ("display.bigUI.columns.enemyKills.powerMeleeWhisp", false),
            ("display.bigUI.columns.enemyKills.khepri", false),
            ("display.bigUI.columns.enemyKills.powerKhepri", false),
            ("display.bigUI.columns.enemyKills.hornetShaman", false),
            ("display.bigUI.columns.enemyKills.hornet", false),
            ("display.bigUI.columns.weaponKills.shotgun", false),
            ("display.bigUI.columns.weaponKills.railShot", false),
            ("display.bigUI.columns.weaponKills.deathCube", false),
            ("display.bigUI.columns.weaponKills.deathRay", false),
            ("display.bigUI.columns.weaponKills.energyBall", false),
            ("display.bigUI.columns.weaponKills.particleBlade", false),
            ("display.bigUI.columns.weaponKills.khepriStaff", false),
            ("display.bigUI.columns.weaponKills.laserCannon", false),
            ("display.bigUI.columns.weaponKills.laserCube", false),
            ("display.bigUI.columns.weaponKills.sawDisc", false),
            ("display.bigUI.columns.weaponKills.explosions", false),
            ("tracking.saveStatsToFile", true),
            ("tracking.saveHistory", true),
            ("display.presets.combat", true),
            ("display.presets.enemies", true),
            ("display.presets.weapons", true),
            ("display.presets.accuracy", true),
            ("display.presets.movement", true),
            ("titles.enabled", true),
            ("titles.revealDelaySeconds", 2.0f),
        }.Select(pair => new KeyValuePair<string, object>(Prefix + pair.key, pair.value))
            .Concat(WeaponAccuracy.Weapons.Select(weapon => new KeyValuePair<string, object>(ColumnsPrefix + "accuracy." + weapon.Key, false))).ToArray();

        internal static bool ShowAccuracy(string weapon) => Settings.GetBool(FullKey(_accuracyKeys, ColumnsPrefix + "accuracy.", weapon));

        // Display settings
        public static bool ShowStatsWindow => Bool("display.showStatsWindow");
        public static bool ShowPlayers => Bool("display.showPlayers");
        public static bool ShowPlayTime => Bool("display.showPlayTime");
        public static bool ShowEnemyDeaths => Bool("display.showEnemyDeaths");

        public static Vector2 DisplayPosition => new Vector2(Mathf.Clamp(Settings.GetFloat(PositionXKey), 0f, 100f), Mathf.Clamp(Settings.GetFloat(PositionYKey), 0f, 100f));

        // UI Scaling settings
        public static bool AutoScale => Settings.GetBool(AutoScaleKey);
        public static float UIScale => Mathf.Clamp(Settings.GetFloat(UIScaleKey), MinScale, MaxScale);
        public static float BigUIOpacity => Mathf.Clamp(Settings.GetFloat(Prefix + "display.bigUIOpacity"), 0f, 100f);
        public static bool SmallUIShowBackground => Bool("display.smallUIShowBackground");

        // Tracking settings
        public static bool SaveStatsToFile => Bool("tracking.saveStatsToFile");

        // Titles settings
        public static bool TitlesEnabled => Bool("titles.enabled");
        public static float TitlesRevealDelaySeconds => Mathf.Clamp(Settings.GetFloat(Prefix + "titles.revealDelaySeconds"), 0f, MaxRevealDelay);

        // The small window is dragged with the mouse, so its position is saved once it stops moving, as a percentage of the screen so a new resolution keeps it in place
        public static void SetDisplayPosition(Vector2 position)
        {
            Settings.SetLater(PositionXKey, Percent(position.x, Screen.width));
            Settings.SetLater(PositionYKey, Percent(position.y, Screen.height));
        }

        public static float Percent(double pixels, int size)
        {
            return size > 0 ? (float)System.Math.Round(Mathf.Clamp01((float)(pixels / size)) * 100f, 3) : 0f;
        }

        private static bool Bool(string key) => Settings.GetBool(FullKey(_keys, Prefix, key));

        internal static bool Column(string key) => Settings.GetBool(FullKey(_columnKeys, ColumnsPrefix, key));

        // The stats windows read these every frame, so each full key is joined once
        private static string FullKey(Dictionary<string, string> keys, string prefix, string key)
        {
            if (!keys.TryGetValue(key, out string full))
            {
                full = prefix + key;
                keys[key] = full;
            }
            return full;
        }
    }
}
