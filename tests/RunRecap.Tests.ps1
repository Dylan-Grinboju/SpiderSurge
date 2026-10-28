$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$stubs = @'
using System.IO;
using System.Linq;
using StatsMod;
using System.Collections.Generic;
public class SurvivalMode { public static SurvivalMode instance = new SurvivalMode(); public int CurrentWave = 53; }
namespace StatsMod {
    public enum GameMode { None, Survival, Versus }
    public class GameSessionManager {
        public static GameSessionManager Instance = new GameSessionManager();
        public GameMode CurrentGameMode;
        public bool IsActive;
    }
}
namespace SpiderSurge {
    public static class SurgeGameModeManager { public static bool IsSurgeRunActive, AbilitiesDisabled; }
    public class PerksManager {
        public static PerksManager Instance = new PerksManager();
        public Dictionary<string,int> Levels = new Dictionary<string,int>();
        public IEnumerable<string> GetAllPerkNames() => Levels.Keys;
        public int GetPerkLevel(string key) => Levels[key];
        public bool IsAbilityPerk(string key) => key == "ability";
        public string[] GetAbilityUltimatePerkNames() => new[] { "ultimate" };
        public bool IsUpgradePerk(string key) => key == "duration" || key == "cooldown";
        public string GetDisplayName(string key) => key;
    }
    public static class SurgeAchievements {
        public class Achievement { public string Id, Name; public Achievement(string id,string name) { Id=id;Name=name; } }
        public static Achievement[] All = { new Achievement("old","Old achievement"), new Achievement("new","New achievement") };
        public static HashSet<string> Unlocked = new HashSet<string>();
        public static bool IsUnlocked(string key) => Unlocked.Contains(key);
    }
    public static class ChaosWallet { public static int ChallengesCompleted=2, PointsEarned=7, PointsSpent=5; }
}
namespace SpiderSurge.Common {
    public static class Parts { public class Part { public bool Running=true; } public static Part Surge=new Part(); }
    public static class WriterExtensions { public static void WriteText(this System.IO.BinaryWriter writer,string text) => writer.Write(text ?? ""); }
}
'@
$source = [IO.File]::ReadAllText((Join-Path $repo 'core/RunRecap.cs'))
Add-Type -TypeDefinition ($stubs + "`n" + $source.Replace('using System.IO;', '').Replace('using System.Linq;', '').Replace('using StatsMod;', '')) -ReferencedAssemblies @('System.Linq','System.Collections')
$type = [SpiderSurge.PerksManager].Assembly.GetType('SpiderSurge.Common.RunRecap', $true)
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
function Invoke-Recap([string]$method, [object[]]$arguments = @()) { $type.GetMethod($method,$flags).Invoke($null,$arguments) }
function Last { $type.GetProperty('Last',$flags).GetValue($null) }
$checks=0
function Assert-Equal($actual,$expected,[string]$label) {
    if ($actual -ne $expected) { throw "$label expected '$expected', got '$actual'" }
    $script:checks++
}
[StatsMod.GameSessionManager]::Instance.CurrentGameMode=[StatsMod.GameMode]::Survival
[SpiderSurge.SurgeGameModeManager]::IsSurgeRunActive=$true
[void][SpiderSurge.SurgeAchievements]::Unlocked.Add('old')
[void](Invoke-Recap Begin)
[void][SpiderSurge.SurgeAchievements]::Unlocked.Add('new')
[SpiderSurge.PerksManager]::Instance.Levels.Add('ability',1)
[SpiderSurge.PerksManager]::Instance.Levels.Add('ultimate',1)
[SpiderSurge.PerksManager]::Instance.Levels.Add('duration',2)
[SpiderSurge.PerksManager]::Instance.Levels.Add('cooldown',1)
[void](Invoke-Recap Capture)
Assert-Equal (Last).Build 'ability + ultimate + duration + cooldown' 'Survival final build'
Assert-Equal (Last).Wave 53 'Final wave'
Assert-Equal (Last).Challenges 2 'Completed challenges'
Assert-Equal (Last).Earned 7 'Earned Chaos'
Assert-Equal (Last).Spent 5 'Spent Chaos'
Assert-Equal (Last).Achievements 'New achievement' 'Old achievements excluded'
Assert-Equal ([StatsMod.GameSessionManager]::Instance.IsActive) $false 'Stopped session fixture'
Assert-Equal ($null -ne (Last)) $true 'Stopped Survival still captures before current mode is reset'
[StatsMod.GameSessionManager]::Instance.CurrentGameMode=[StatsMod.GameMode]::Versus
[void](Invoke-Recap Capture)
Assert-Equal ($null -eq (Last)) $true 'Surge-enabled Versus cannot create a Survival recap'
[StatsMod.GameSessionManager]::Instance.CurrentGameMode=[StatsMod.GameMode]::None
[void](Invoke-Recap Capture)
Assert-Equal ($null -eq (Last)) $true 'Lobby cannot create a Survival recap'
[StatsMod.GameSessionManager]::Instance.CurrentGameMode=[StatsMod.GameMode]::Survival
[SpiderSurge.SurgeGameModeManager]::IsSurgeRunActive=$false
[void](Invoke-Recap Capture)
Assert-Equal ($null -eq (Last)) $true 'Plain Survival clears a previous recap'
[SpiderSurge.SurgeGameModeManager]::IsSurgeRunActive=$true
[SpiderSurge.SurgeGameModeManager]::AbilitiesDisabled=$true
[void](Invoke-Recap Capture)
Assert-Equal (Last).Build 'No abilities' 'No-abilities run description'
[SpiderSurge.SurgeGameModeManager]::AbilitiesDisabled=$false
[SpiderSurge.PerksManager]::Instance.Levels.Clear()
[void](Invoke-Recap Capture)
Assert-Equal (Last).Build 'No ability chosen' 'Unconfirmed starting choice description'
(Last).Build='Build with · separator'
$stream=[IO.MemoryStream]::new()
$writer=[IO.BinaryWriter]::new($stream)
[void](Invoke-Recap Write @($writer))
$writer.Flush()
$stream.Position=0
[void](Invoke-Recap Clear)
[void](Invoke-Recap Read @([IO.BinaryReader]::new($stream)))
Assert-Equal (Last).Build 'Build with · separator' 'Snapshot build round trip'
Assert-Equal (Last).Summary 'Wave 53 reached  ·  2 challenges completed  ·  Chaos: +7 earned / 5 spent' 'Snapshot summary round trip'
Assert-Equal $stream.Position $stream.Length 'Snapshot payload fully consumed'
$stream=[IO.MemoryStream]::new()
$writer=[IO.BinaryWriter]::new($stream)
[void](Invoke-Recap Clear)
[void](Invoke-Recap Write @($writer))
$writer.Flush()
$stream.Position=0
[void](Invoke-Recap Capture)
[void](Invoke-Recap Read @([IO.BinaryReader]::new($stream)))
Assert-Equal ($null -eq (Last)) $true 'Empty snapshot clears a stale recap'
Write-Output "PASS: $checks run recap checks"
