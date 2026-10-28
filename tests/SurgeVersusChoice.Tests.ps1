$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$stubs = @'
namespace SpiderSurge.Common {
    public static class Parts {
        public class Part { public bool Running = true; }
        public static Part Surge = new Part();
    }
}
namespace SpiderSurge {
    internal enum RunEnd { Versus }
    public class SurgeGameModeManager {
        internal static void EndRun(RunEnd reason) { }
        public static SurgeGameModeManager Instance = new SurgeGameModeManager();
        public bool Active;
        public void ForceActive(bool active) { Active = active; }
    }
    public class PerksManager {
        public static PerksManager Instance = new PerksManager();
        public bool IsFirstNormalPerkSelection = true;
        public System.Collections.Generic.Dictionary<string,int> Levels = new System.Collections.Generic.Dictionary<string,int>();
        public void ResetPerks() { Levels.Clear(); IsFirstNormalPerkSelection = true; }
        public void SetPerkLevel(string perk,int level) { Levels[perk] = level; }
    }
    public static class PlayerAbilityHandler { public static void ResetSpawnTracking() { } }
    public static class StoragePersistenceManager { public static void ClearAllStoredWeapons() { } }
    public static class AmmoAbility { public static void ForgetWeaponSpawners() { } }
}
'@
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $repo 'teams/Integration/SurgeBridge.cs')) + $stubs)
$bridge = [SpiderSurge.PerksManager].Assembly.GetType('SpiderTeams.SurgeBridge')
$reset = $bridge.GetMethod('ResetPerks')
$set = $bridge.GetMethod('SetPerkLevel')
$checks = 0
function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected '$expected', got '$actual'" }
    $script:checks++
}
$perks = [SpiderSurge.PerksManager]::Instance
$perks.Levels['oldAbility'] = 1
Assert-Equal ($reset.Invoke($null, @())) $true 'Versus resets the old build'
Assert-Equal $perks.Levels.Count 0 'No old perks carry into the round'
Assert-Equal $perks.IsFirstNormalPerkSelection $false 'Versus never advertises a pending Survival choice to clients'
[void]$set.Invoke($null, @('pulse', 1))
[void]$set.Invoke($null, @('pulseUltimate', 1))
Assert-Equal $perks.Levels['pulse'] 1 'Assigned versus ability is retained'
Assert-Equal $perks.Levels['pulseUltimate'] 1 'Assigned versus ultimate is retained'
Assert-Equal $perks.IsFirstNormalPerkSelection $false 'Assigning abilities does not reopen the Survival choice'
[void]$reset.Invoke($null, @())
Assert-Equal $perks.IsFirstNormalPerkSelection $false 'Repeated round resets keep client input available'
$perks.ResetPerks()
Assert-Equal $perks.IsFirstNormalPerkSelection $true 'Normal Survival reset still requests its starting choice'
[SpiderSurge.Common.Parts]::Surge.Running = $false
Assert-Equal ($reset.Invoke($null, @())) $false 'Unavailable Surge does not reset perks'
Assert-Equal $perks.IsFirstNormalPerkSelection $true 'Unavailable bridge does not mutate Survival state'
Write-Output "PASS: $checks versus choice checks"
