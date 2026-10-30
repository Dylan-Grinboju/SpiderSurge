$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$stubs = @'
namespace UnityEngine { public struct Color { public Color(float r, float g, float b, float a = 1f) {} } }
public class PlayerController {
    public class PlayerId { public int Value; }
    public PlayerId playerID = new PlayerId();
    public PlayerController(int id) { playerID.Value = id; }
}
namespace SpiderSurge.Common {
    public static class Players { public static List<PlayerController> All = new List<PlayerController>(); }
}
namespace SpiderSurge {
    public class PerksManager {
        public static PerksManager Instance = new PerksManager();
        internal PersonalLoadouts Loadouts = new PersonalLoadouts();
        public Dictionary<string, int> Levels = new Dictionary<string, int>();
        public bool HasPersonalLoadouts => Loadouts.Any;
        public int GetPerkLevel(string key) => Loadouts.Contains(key) ? 1 : Levels.TryGetValue(key, out int value) ? value : 0;
    }
    public static class PerkSynergyTests {
        private static int checks;
        private static void Check(bool value, string message) { if (!value) throw new System.Exception(message); checks++; }
        public static int Run() {
            string[] abilities = { "immune", "ammo", "pulse", "storage", "freeze" };
            string[] names = { "Null Field", "Overcharge", "Kinetic Pulse", "Pocket Dimension", "Freeze Time", "Neural Backup", "Thermal Detonation", "Rewind" };
            string[] keys = { M.StartShields, M.PositiveEncouragement, M.SafetyNet, M.Efficiency, M.BiggerBoom, M.TooCool, M.MoreGuns, M.MoreBoom, M.MoreParticles, M.Adrenaline, M.Blademaster, M.CoolExplosions };
            var pm = PerksManager.Instance;
            foreach (string ability in abilities) foreach (string ultimate in abilities) {
                pm.Loadouts.Clear(); pm.Levels.Clear();
                Players.All = new List<PlayerController> { new PlayerController(0), new PlayerController(1), null };
                pm.Loadouts.Set(1, ability, false); pm.Loadouts.Set(1, ultimate + "Ultimate", true);
                foreach (string key in keys) foreach (var levels in new[] { (0,1), (1,2), (0,2) }) {
                    var expected = new HashSet<string>();
                    if (key == M.StartShields || key == M.PositiveEncouragement || key == M.SafetyNet) {
                        if (ability == "immune") expected.Add("Null Field");
                        if (ultimate == "immune") expected.Add("Neural Backup");
                    } else if (key == M.Efficiency && ability == "ammo") expected.Add("Overcharge");
                    else if (key == M.BiggerBoom) {
                        if (ability == "pulse" && levels.Item1 == 0) expected.Add("Kinetic Pulse");
                        if (ultimate == "pulse" && levels.Item2 == 2) expected.Add("Thermal Detonation");
                    } else if (key == M.TooCool && ultimate == "pulse") expected.Add("Thermal Detonation");
                    else if ((key == M.MoreGuns || key == M.MoreBoom || key == M.MoreParticles) && ability == "storage") expected.Add("Pocket Dimension");
                    else if (key == M.Adrenaline || key == M.Blademaster || key == M.CoolExplosions) {
                        if (ability == "freeze") expected.Add("Freeze Time");
                        if (ultimate == "freeze") expected.Add("Rewind");
                    }
                    string label = PerkSynergy.Label(key, levels.Item1, levels.Item2);
                    string detail = PerkSynergy.Describe(key, levels.Item2, levels.Item1);
                    string context = ability + "/" + ultimate + " " + key;
                    Check((label != null) == (expected.Count > 0), "Wrong label visibility: " + context);
                    Check((detail != null) == (expected.Count > 0), "Wrong detail visibility: " + context);
                    Check(PerkSynergy.Improves(key, levels.Item1, levels.Item2) == (expected.Count > 0), "Wrong synergy bonus: " + context);
                    foreach (string name in names) {
                        Check((label?.Contains(name) ?? false) == expected.Contains(name), "Wrong label action: " + context + " " + name);
                        Check((detail?.Contains(name) ?? false) == expected.Contains(name), "Wrong detail action: " + context + " " + name);
                    }
                    string text = PerkSynergy.Decorate("Native description", key, levels.Item1, levels.Item2, levels.Item2 == 2);
                    Check(PerkSynergy.Decorate(text, key, levels.Item1, levels.Item2, levels.Item2 == 2) == text, "Refresh duplicates text: " + context);
                }
                int selected = PerkSynergy.SelectionStamp;
                Players.All.RemoveAt(1);
                Check(PerkSynergy.SelectionStamp != selected || selected == 0, "Departure did not invalidate hints");
                foreach (string key in keys) {
                    Check(PerkSynergy.Describe(key, 1) == null, "Departed player's selection leaked");
                    Check(PerkSynergy.Label(key, 0, 2) == null, "Departed player's label leaked");
                }
            }
            pm.Loadouts.Clear(); pm.Levels.Clear();
            Players.All = new List<PlayerController> { new PlayerController(0), new PlayerController(1) };
            pm.Loadouts.Set(99, "immune", false); pm.Levels["ammo"] = 1;
            Check(PerkSynergy.Describe(M.StartShields, 1) == null, "Absent slot counted as selected");
            Check(PerkSynergy.Describe(M.Efficiency, 1) == null, "Shared unlock leaked into personal loadouts");
            pm.Loadouts.Set(0, "immuneUltimate", true); pm.Loadouts.Set(1, "immuneUltimate", true);
            Players.All.RemoveAt(1);
            Check(PerkSynergy.Label(M.StartShields, 0, 1).Contains("Neural Backup"), "Remaining owner's synergy disappeared");
            pm.Loadouts.Set(0, "freezeUltimate", true);
            Check(PerkSynergy.Describe(M.StartShields, 1) == null, "Swapped ultimate left stale synergy");
            Check(PerkSynergy.Label(M.Adrenaline, 0, 1).Contains("Rewind"), "Swapped ultimate not reflected");
            string decorated = "Native description\n<color=#00FFFF>Synergized: Neural Backup</color>";
            Check(PerkSynergy.Decorate(decorated, M.StartShields, 0, 1, false) == "Native description", "Stale label retained");
            pm.Loadouts.Clear(); pm.Levels.Clear();
            pm.Levels["pulse"] = 1; pm.Levels["pulseUltimate"] = 1;
            Check(PerkSynergy.Label(M.BiggerBoom, 0, 2).Contains("Kinetic Pulse / Thermal Detonation"), "Shared versus loadout not supported");
            Players.All.Clear();
            Check(PerkSynergy.Label(M.BiggerBoom, 0, 2) == null, "Empty roster counted as a selection");
            return checks;
        }
    }
}
'@
$source = 'using System.Collections.Generic; using System.IO; using System.Linq; using SpiderSurge.Common; using P = SpiderSurge.Consts.PerkNames; using M = SpiderSurge.Consts.ModifierNames;'
foreach ($file in @('surge/Core/Consts.cs','surge/Core/PersonalLoadouts.cs','surge/Core/PerkSynergy.cs')) {
    $source += [IO.File]::ReadAllText((Join-Path $repo $file)) -replace '(?m)^using .*?;\r?\n',''
}
Add-Type -TypeDefinition ($source + $stubs)
Write-Output ('PASS: {0} roster and named synergy checks' -f [SpiderSurge.PerkSynergyTests]::Run())
