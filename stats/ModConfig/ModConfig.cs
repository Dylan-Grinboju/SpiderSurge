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
        public const string CycleInputKey = Prefix + "input.cycleInput";
        public const float MinScale = 0.5f;
        public const float MaxScale = 3f;
        public const float MaxRevealDelay = 10f;
        public static readonly string[] CycleInputs = { "dpad", "joystick", "both", "none" };

        public static readonly KeyValuePair<string, object>[] Defaults = new (string key, object value)[]
        {
            ("display.showStatsWindow", true),
            ("display.showPlayers", true),
            ("display.showPlayTime", true),
            ("display.showEnemyDeaths", true),
            ("display.autoScale", true),
            ("display.uiScale", 1.0f),
            ("display.bigUIOpacity", 100f),
            ("display.smallUIShowBackground", false),
            ("display.position.x", 10),
            ("display.position.y", 10),
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
            ("titles.enabled", true),
            ("titles.revealDelaySeconds", 2.0f),
            ("input.cycleInput", "dpad"),
        }.Select(pair => new KeyValuePair<string, object>(Prefix + pair.key, pair.value))
            .Concat(WeaponAccuracy.Weapons.Select(weapon => new KeyValuePair<string, object>(ColumnsPrefix + "accuracy." + weapon.Key, false))).ToArray();

        internal static bool ShowAccuracy(string weapon) => Column("accuracy." + weapon);

        // Display settings
        public static bool ShowStatsWindow => Bool("display.showStatsWindow");
        public static bool ShowPlayers => Bool("display.showPlayers");
        public static bool ShowPlayTime => Bool("display.showPlayTime");
        public static bool ShowEnemyDeaths => Bool("display.showEnemyDeaths");

        public static int DisplayPositionX => ValidatePosition(Settings.GetInt(Prefix + "display.position.x"), Screen.width);
        public static int DisplayPositionY => ValidatePosition(Settings.GetInt(Prefix + "display.position.y"), Screen.height);

        // UI Scaling settings
        public static bool AutoScale => Settings.GetBool(AutoScaleKey);
        public static float UIScale => Mathf.Clamp(Settings.GetFloat(UIScaleKey), MinScale, MaxScale);
        public static float BigUIOpacity => Mathf.Clamp(Settings.GetFloat(Prefix + "display.bigUIOpacity"), 0f, 100f);
        public static bool SmallUIShowBackground => Bool("display.smallUIShowBackground");

        // BigUI Column settings (Player Name is always required, all others are optional)
        public static bool BigUIShowKills => Column("kills");
        public static bool BigUIShowDeaths => Column("deaths");
        public static bool BigUIShowMaxKillStreak => Column("maxKillStreak");
        public static bool BigUIShowCurrentKillStreak => Column("currentKillStreak");
        public static bool BigUIShowMaxSoloKillStreak => Column("maxSoloKillStreak");
        public static bool BigUIShowCurrentSoloKillStreak => Column("currentSoloKillStreak");
        public static bool BigUIShowFriendlyKills => Column("friendlyKills");
        public static bool BigUIShowEnemyShields => Column("enemyShields");
        public static bool BigUIShowShieldsLost => Column("shieldsLost");
        public static bool BigUIShowFriendlyShields => Column("friendlyShields");
        public static bool BigUIShowAliveTime => Column("aliveTime");
        public static bool BigUIShowWaveClutches => Column("waveClutches");
        public static bool BigUIShowWebSwings => Column("webSwings");
        public static bool BigUIShowWebSwingTime => Column("webSwingTime");
        public static bool BigUIShowAirborneTime => Column("airborneTime");
        public static bool BigUIShowLavaDeaths => Column("lavaDeaths");
        public static bool BigUIShowAstralReturns => Column("astralReturns");

        // Computed stat columns
        public static bool BigUIShowTotalOffence => Column("totalOffence");
        public static bool BigUIShowTotalFriendlyHits => Column("totalFriendlyHits");
        public static bool BigUIShowTotalHitsTaken => Column("totalHitsTaken");

        // Enemy kill columns
        public static bool BigUIShowWaspKills => Column("enemyKills.wasp");
        public static bool BigUIShowWaspCocoonKills => Column("enemyKills.waspCocoon");
        public static bool BigUIShowPowerWaspKills => Column("enemyKills.powerWasp");
        public static bool BigUIShowRollerKills => Column("enemyKills.roller");
        public static bool BigUIShowWhispKills => Column("enemyKills.whisp");
        public static bool BigUIShowPowerWhispKills => Column("enemyKills.powerWhisp");
        public static bool BigUIShowMeleeWhispKills => Column("enemyKills.meleeWhisp");
        public static bool BigUIShowPowerMeleeWhispKills => Column("enemyKills.powerMeleeWhisp");
        public static bool BigUIShowKhepriKills => Column("enemyKills.khepri");
        public static bool BigUIShowPowerKhepriKills => Column("enemyKills.powerKhepri");
        public static bool BigUIShowHornetShamanKills => Column("enemyKills.hornetShaman");
        public static bool BigUIShowHornetKills => Column("enemyKills.hornet");

        // Weapon kill columns
        public static bool BigUIShowShotgunKills => Column("weaponKills.shotgun");
        public static bool BigUIShowRailShotKills => Column("weaponKills.railShot");
        public static bool BigUIShowDeathCubeKills => Column("weaponKills.deathCube");
        public static bool BigUIShowDeathRayKills => Column("weaponKills.deathRay");
        public static bool BigUIShowEnergyBallKills => Column("weaponKills.energyBall");
        public static bool BigUIShowParticleBladeKills => Column("weaponKills.particleBlade");
        public static bool BigUIShowKhepriStaffKills => Column("weaponKills.khepriStaff");
        public static bool BigUIShowLaserCannonKills => Column("weaponKills.laserCannon");
        public static bool BigUIShowLaserCubeKills => Column("weaponKills.laserCube");
        public static bool BigUIShowSawDiscKills => Column("weaponKills.sawDisc");
        public static bool BigUIShowExplosionKills => Column("weaponKills.explosions");

        // Tracking settings
        public static bool SaveStatsToFile => Bool("tracking.saveStatsToFile");

        // Titles settings
        public static bool TitlesEnabled => Bool("titles.enabled");
        public static float TitlesRevealDelaySeconds => Mathf.Clamp(Settings.GetFloat(Prefix + "titles.revealDelaySeconds"), 0f, MaxRevealDelay);

        public static string ConfiguredCycleInput
        {
            get
            {
                string value = Settings.GetString(CycleInputKey).ToLowerInvariant();
                return System.Array.IndexOf(CycleInputs, value) >= 0 ? value : "dpad";
            }
        }

        public static bool DpadTakenBySurge => Parts.Surge.Running && (SpiderSurge.BaseAbility.DpadUltimateBound ?? SpiderSurge.ModConfig.UltimateUseDpadActivation);

        public static string CycleInput => CycleInputFor(ConfiguredCycleInput);

        // Surge's ultimate on the D-pad takes the D-pad over
        public static string CycleInputFor(string configured)
        {
            if (DpadTakenBySurge)
            {
                if (configured == "dpad") return "none";
                if (configured == "both") return "joystick";
            }
            return configured;
        }

        // The small window is dragged with the mouse, so its position is saved once it stops moving
        public static void SetDisplayPosition(int x, int y)
        {
            Settings.SetLater(Prefix + "display.position.x", ValidatePosition(x, Screen.width));
            Settings.SetLater(Prefix + "display.position.y", ValidatePosition(y, Screen.height));
        }

        private static bool Bool(string key) => Settings.GetBool(Prefix + key);

        private static bool Column(string key) => Settings.GetBool(ColumnsPrefix + key);

        private static int ValidatePosition(int value, int limit)
        {
            return value < 0 || value >= limit ? 0 : value;
        }
    }
}
