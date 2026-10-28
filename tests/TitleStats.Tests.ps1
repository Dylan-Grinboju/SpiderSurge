$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$baseline = '36e2638'
$files = 'stats/Tracking/TitlesManager.cs', 'stats/Tracking/TitleCatalog.cs'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
function Old([string]$path) {
    $text = git -C $repo show "${baseline}:$path"
    if ($LASTEXITCODE -ne 0) { throw "Cannot read $path at $baseline" }
    $text -join "`n"
}
function New([string]$path) { [IO.File]::ReadAllText((Join-Path $repo $path)) }
$usings = [Collections.Generic.SortedSet[string]]::new()
function Body([string]$source, [string]$namespace) {
    $lines = $source -split "`r?`n"
    $lines | Where-Object { $_ -match '^using .*;$' } | ForEach-Object { [void]$usings.Add($_) }
    $text = ($lines | Where-Object { $_ -notmatch '^using .*;$' }) -join "`n"
    if (-not $text.Contains('namespace StatsMod;')) { throw "File-scoped StatsMod namespace not found" }
    $text.Replace('namespace StatsMod;', "namespace StatsMod.$namespace {") + "`n}"
}
$variantStubs = @'
    public class GameStatsSnapshot { public GameMode GameMode { get; set; } public Dictionary<PlayerController, PlayerTracker.PlayerData> ActivePlayers { get; set; } }
    public static class StatsModApi {
        public static List<TitleEntry> GetExternalTitles() => new List<TitleEntry>();
        public static List<TitleDefinition> GetDescribedTitles() => new List<TitleDefinition>();
    }
    public static class RunReportSources {
        public static List<TitleEntry> Titles() => new List<TitleEntry>();
        public static List<TitleDefinition> DescribedTitles => new List<TitleDefinition>();
    }
    public static class Probe {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static object Call(object target, string method, params object[] args) => typeof(TitlesManager).GetMethod(method, Any).Invoke(target, args);
        public static string[] Requirements() => typeof(Req).GetFields().Select(f => f.Name + "=" + f.GetValue(null)).ToArray();
        public static List<string> Catalog()
        {
            var lines = typeof(Req).GetFields().Select(f => (string)f.GetValue(null)).Select(r => $"label {r}={TitleCatalog.Label(r)} / {TitleCatalog.HowToEarnLabel(r)}").ToList();
            lines.AddRange(TitleCatalog.Native.Select(t => $"native {t.Name}|{t.BonusPriority}|{string.Join(",", t.Requirements)}|{t.HowToEarn}|{t.Condition != null}"));
            return lines;
        }
        private static PlayerController Leader(StatLeaders leaders, string req)
        {
            var property = typeof(StatLeaders).GetProperty(req);
            object pair = property != null ? property.GetValue(leaders) : typeof(StatLeaders).GetProperty("Item").GetValue(leaders, new object[] { req });
            return ((KeyValuePair<PlayerController, PlayerTracker.PlayerData>)pair).Key;
        }
        public static List<string> Run(List<KeyValuePair<PlayerController, PlayerTracker.PlayerData>> players)
        {
            var manager = new TitlesManager();
            var leaders = (StatLeaders)Call(manager, "CalculateStatLeaders", players);
            var conditions = (System.Collections.IDictionary)Call(manager, "GetStatConditions", leaders);
            var lines = new List<string>();
            foreach (var field in typeof(Req).GetFields())
            {
                string req = (string)field.GetValue(null);
                lines.Add($"leader {req}={Leader(leaders, req).playerID.Value}");
                object condition = conditions[req];
                var type = condition.GetType();
                var leader = (KeyValuePair<PlayerController, PlayerTracker.PlayerData>)type.GetProperty("Leader").GetValue(condition);
                lines.Add($"condition {req}={type.GetProperty("RequirementName").GetValue(condition)}|{leader.Key.playerID.Value}|{type.GetProperty("HasStat").GetValue(condition)}|{type.GetProperty("FormattedDescription").GetValue(condition)}");
            }
            lines.Add($"conditions {conditions.Count}");
            foreach (var native in TitleCatalog.Native.Where(t => t.Condition != null)) lines.Add($"rule {native.Name}={native.Condition(leaders)}");
            if (players.Count > 1)
            {
                manager.CalculateAndStoreTitles(new GameStatsSnapshot { GameMode = GameMode.Survival, ActivePlayers = players.ToDictionary(p => p.Key, p => p.Value) });
                foreach (var title in manager.AllTitles)
                    lines.Add($"title {title.TitleName}|{title.PlayerId}|{title.PlayerName}|{title.Priority}|{string.Join(",", title.Requirements.OrderBy(r => r))}|{title.Description.Replace("\n", " / ")}");
            }
            return lines;
        }
    }
'@
$oldCode = ($files | ForEach-Object { Body (Old $_) 'Old' }) -join "`n"
$newCode = ($files | ForEach-Object { Body (New $_) 'New' }) -join "`n"
$accuracyLines = (New 'stats/Models/WeaponAccuracy.cs') -split "`r?`n"
$accuracyLines | Where-Object { $_ -match '^using .*;$' } | ForEach-Object { [void]$usings.Add($_) }
$accuracy = ($accuracyLines | Where-Object { $_ -notmatch '^using .*;$' }) -join "`n"
foreach ($using in 'using System;', 'using System.Collections.Generic;', 'using System.Linq;', 'using System.Reflection;') { [void]$usings.Add($using) }
$stubs = @'
namespace Silk { public static class Logger { public static void LogInfo(string m) { } public static void LogWarning(string m) { } public static void LogError(string m) { } } }
namespace UnityEngine {
    public struct Color { public float r, g, b, a; public static Color white => new Color { r = 1, g = 1, b = 1, a = 1 }; }
    public class Texture { }
    public class RenderTexture : Texture { }
}
public class PlayerId { public int Value; }
public class PlayerController { public PlayerId playerID = new PlayerId(); }
namespace StatsMod {
    public enum GameMode { None, Survival, Versus }
    public static class SpiderPortraits {
        public static RenderTexture CopyOf(PlayerController player) => new RenderTexture();
        internal static void DisposeTexture(RenderTexture texture) { }
    }
    public class PlayerTracker {
        public static PlayerTracker Instance = new PlayerTracker();
        public Dictionary<PlayerController, PlayerData> GetActivePlayers() => new Dictionary<PlayerController, PlayerData>();
        public class PlayerData {
            public int Deaths { get; set; }
            public int LavaDeaths { get; set; }
            public int Kills { get; set; }
            public int KillsWhileAirborne { get; set; }
            public int KillsWhileSolo { get; set; }
            public int FriendlyKills { get; set; }
            public int WaveClutches { get; set; }
            public int AstralReturns { get; set; }
            public int EnemyShieldsTakenDown { get; set; }
            public int FriendlyShieldsHit { get; set; }
            public int ShieldsLost { get; set; }
            public int MaxKillStreak { get; set; }
            public int MaxKillStreakWhileSolo { get; set; }
            public string PlayerName { get; set; }
            public Color PlayerColor { get; set; }
            public Color SecondaryColor { get; set; }
            public float TotalAliveTime { get; set; }
            public int WebSwings { get; set; }
            public float AirborneTime { get; set; }
            public float HighestPoint { get; set; }
            public Dictionary<string, int> WeaponHits { get; set; } = new[] { "Shotgun", "RailShot", "DeathCube", "DeathRay", "EnergyBall", "Particle Blade", "KhepriStaff", "Laser Cannon", "Laser Cube", "SawDisc", "Explosions", "Snowball", "Pulse" }.ToDictionary(k => k, k => 0);
            public WeaponAccuracy ShootingAccuracy { get; } = new WeaponAccuracy();
            public Dictionary<string, WeaponAccuracy> AccuracyByWeapon { get; } = new Dictionary<string, WeaponAccuracy>();
            public Dictionary<string, int> EnemyKills { get; set; } = new[] { "Wasp", "Whisp", "Power Whisp", "Khepri", "Power Khepri", "Hornet" }.ToDictionary(k => k, k => 0);
        }
    }
}
namespace Harness {
    using StatsMod;
    public static class Snapshots {
        private static readonly float[] Floats = { 0f, 0f, 0.05f, 0.15f, 1.25f, 1.2499999f, 2.5f, 999.95f, 1000f, 1234.56f };
        private static readonly int[] Counts = { 0, 0, 0, 1, 1, 2, 3 };
        private static readonly string[] Weapons = { "Shotgun", "RailShot", "DeathCube", "DeathRay", "EnergyBall", "Particle Blade", "KhepriStaff", "Laser Cannon", "Laser Cube", "SawDisc", "Explosions", "Snowball", "Pulse" };
        private static readonly string[] Enemies = { "Wasp", "Whisp", "Power Whisp", "Khepri", "Power Khepri", "Hornet" };

        private static void Shots(WeaponAccuracy accuracy, Random random, int maxShots)
        {
            int fired = random.Next(maxShots + 1);
            accuracy.Set(fired, fired == 0 ? 0 : random.Next(fired + 1));
        }

        public static List<KeyValuePair<PlayerController, PlayerTracker.PlayerData>> Create(Random random, int count, bool uniform)
        {
            var players = new List<KeyValuePair<PlayerController, PlayerTracker.PlayerData>>();
            Func<int> n = () => uniform ? 1 : Counts[random.Next(Counts.Length)];
            Func<float> f = () => uniform ? 2.5f : Floats[random.Next(Floats.Length)];
            for (int i = 0; i < count; i++)
            {
                var data = new PlayerTracker.PlayerData
                {
                    PlayerName = "P" + i, Deaths = n(), LavaDeaths = n(), Kills = n(), KillsWhileAirborne = n(), KillsWhileSolo = n(), FriendlyKills = n(),
                    WaveClutches = n(), AstralReturns = n(), EnemyShieldsTakenDown = n(), FriendlyShieldsHit = n(), ShieldsLost = n(), MaxKillStreak = n(),
                    MaxKillStreakWhileSolo = n(), WebSwings = n(), TotalAliveTime = f(), AirborneTime = f(), HighestPoint = f(),
                };
                foreach (string weapon in Weapons) data.WeaponHits[weapon] = n();
                foreach (string enemy in Enemies) data.EnemyKills[enemy] = n();
                if (!uniform)
                {
                    Shots(data.ShootingAccuracy, random, 16);
                    foreach (string weapon in new[] { "Railvolver", "DoubleRailvolver", "Shotgun" })
                    {
                        if (random.Next(2) == 0) continue;
                        var accuracy = new WeaponAccuracy();
                        Shots(accuracy, random, 6);
                        data.AccuracyByWeapon[weapon] = accuracy;
                    }
                }
                var player = new PlayerController();
                player.playerID.Value = i;
                players.Add(new KeyValuePair<PlayerController, PlayerTracker.PlayerData>(player, data));
            }
            return players;
        }

        public static List<KeyValuePair<PlayerController, PlayerTracker.PlayerData>> Zero(int count) =>
            Enumerable.Range(0, count).Select(i => new KeyValuePair<PlayerController, PlayerTracker.PlayerData>(new PlayerController { playerID = new PlayerId { Value = i } }, new PlayerTracker.PlayerData())).ToList();
    }
}
'@
$all = ($usings -join "`n") + "`n" + $stubs + "`n" + $accuracy + "`n" + $oldCode + "`nnamespace StatsMod.Old {`n" + $variantStubs + "`n}`n" + $newCode + "`nnamespace StatsMod.New {`n" + $variantStubs + "`n}"
Add-Type -TypeDefinition $all -ReferencedAssemblies @('System.Linq', 'System.Collections')
$checks = 0
function Assert-Same($old, $new, [string]$label) {
    $old = @($old); $new = @($new)
    if ($old.Count -ne $new.Count) { throw "$label line count: old $($old.Count), new $($new.Count)" }
    for ($i = 0; $i -lt $old.Count; $i++) {
        if ($old[$i] -cne $new[$i]) { throw "$label differs:`n  old: $($old[$i])`n  new: $($new[$i])" }
    }
    $script:checks += $old.Count
}
$oldReqs = [StatsMod.Old.Probe]::Requirements()
Assert-Same $oldReqs ([StatsMod.New.Probe]::Requirements()) 'Req constants'
if ($oldReqs.Count -ne 35) { throw "Expected 35 Req constants, found $($oldReqs.Count)" }
Assert-Same ([StatsMod.Old.Probe]::Catalog()) ([StatsMod.New.Probe]::Catalog()) 'Labels and catalog'
$random = [Random]::new(20261028)
$snapshots = 0; $zeroConditions = 0; $titled = 0
for ($round = 0; $round -lt 3000; $round++) {
    $count = 1 + ($round % 5)
    $uniform = $round % 97 -eq 0
    $players = [Harness.Snapshots]::Create($random, $count, $uniform)
    $old = [StatsMod.Old.Probe]::Run($players)
    $new = [StatsMod.New.Probe]::Run($players)
    Assert-Same $old $new "Snapshot $round ($count players)"
    $snapshots++
    $zeroConditions += @($old | Where-Object { $_ -like 'condition *|False|*' }).Count
    $titled += @($old | Where-Object { $_ -like 'title *' }).Count
}
$zero = [Harness.Snapshots]::Zero(3)
Assert-Same ([StatsMod.Old.Probe]::Run($zero)) ([StatsMod.New.Probe]::Run($zero)) 'All-zero snapshot'
if ($zeroConditions -eq 0 -or $titled -eq 0) { throw 'Snapshots never produced unmet conditions or titles' }
Write-Output "PASS: $checks title stat checks over $($snapshots + 1) snapshots ($zeroConditions unmet conditions, $titled titles)"
