$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$temp = Join-Path ([IO.Path]::GetTempPath()) ('SpiderSurge-migration-' + [guid]::NewGuid())
[void][IO.Directory]::CreateDirectory($temp)
$stubs = @'
using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
namespace Silk {
    public static class Config {
        public static string Root;
        public static string GetModConfigPath(string id) => Path.Combine(Root, id + ".yaml");
        public static void LoadModConfig(string id, Dictionary<string, object> defaults) => throw new NotSupportedException();
        public static T GetModConfigValue<T>(string id, string key, T fallback) => throw new NotSupportedException();
        public static void SetModConfigValue(string id, string key, object value) => throw new NotSupportedException();
    }
    public static class Logger {
        public static void LogInfo(string message) { }
        public static void LogWarning(string message) { }
        public static void LogError(string message) => throw new Exception(message);
    }
}
namespace UnityEngine {
    public static class Screen { public static int width = 1920; public static int height = 1080; }
    public static class Time { public static float unscaledTime; }
}
namespace StatsMod {
    public static class ModConfig {
        public const string PositionXKey = "stats.display.position.xPercent";
        public const string PositionYKey = "stats.display.position.yPercent";
        public static float Percent(double pixels, int size) => (float)Math.Max(0, Math.Min(100, pixels / size * 100));
    }
}
namespace SpiderSurge.Common {
    public static class MainThread { public static void PopupLater(string message) => throw new NotSupportedException(); }
    public static class ModUpdater { public const string CheckForUpdatesKey = "updater.checkForUpdates"; }
    public class Part {
        public string Key;
        public string BlockedBy;
        public bool CanDisable;
        public string SettingKey => "parts." + Key;
    }
    public static class Parts {
        public static readonly Part Surge = new Part { Key = "surge", CanDisable = true };
        public static readonly Part Stats = new Part { Key = "stats" };
        public static readonly Part[] All = { Surge, Stats };
    }
    public static class MigrationTest {
        public static void Reset() { Parts.Stats.BlockedBy = null; }
        public static void Run() => SettingsMigration.Run();
        public static object Read(string file, string key) {
            object node = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(ConfigPath(file)));
            foreach (string part in key.Split('.')) {
                if (!(node is IDictionary<object, object> map) || !map.TryGetValue(part, out node)) return null;
            }
            return node;
        }
        private static string ConfigPath(string file) => Silk.Config.GetModConfigPath(file);
    }
}
'@
try {
    $stubPath = Join-Path $temp 'Stubs.cs'
    [IO.File]::WriteAllText($stubPath, $stubs)
    $yaml = Join-Path $repo 'lib/YamlDotNet.dll'
    Add-Type -Path $yaml
    $refs = @(Get-ChildItem (Join-Path $PSHOME 'ref') -Filter '*.dll' | Select-Object -ExpandProperty FullName) + $yaml
    Add-Type -Path @($stubPath, (Join-Path $repo 'core/Settings.cs'), (Join-Path $repo 'core/SettingsMigration.cs'), (Join-Path $repo 'core/Listeners.cs')) -ReferencedAssemblies $refs
    $defaults = [Collections.Generic.Dictionary[string,object]]::new()
    @{
        'parts.fixes' = $true; 'parts.surge' = $true; 'teams.enabled' = $false
        'updater.checkForUpdates' = $true; 'surge.telemetry' = $false
        'surge.showTelemetryPrompt' = $true; 'surge.unlimitedPerkTime' = $true
        'surge.statsLogging' = $true; 'surge.indicator.radius' = [single]1.5
        'stats.display.showStatsWindow' = $true; 'stats.titles.enabled' = $true
        'stats.display.bigUI.columns.kills' = $true
        'stats.display.position.xPercent' = [single]0.5
        'stats.display.position.yPercent' = [single]0.5
    }.GetEnumerator() | ForEach-Object { $defaults.Add($_.Key, $_.Value) }
    [SpiderSurge.Common.Settings]::Define($defaults)
    $checks = 0
    function Assert-Equal($actual, $expected, [string]$label) {
        if ("$actual" -ne "$expected") { throw "$label expected '$expected', got '$actual'" }
        $script:checks++
    }
    function Start-Case([string]$name) {
        [Silk.Config]::Root = Join-Path $temp $name
        [void][IO.Directory]::CreateDirectory([Silk.Config]::Root)
        [SpiderSurge.Common.MigrationTest]::Reset()
    }
    function Write-Config([string]$id, [string]$yamlText) {
        [IO.File]::WriteAllText([Silk.Config]::GetModConfigPath($id), $yamlText)
    }
    function Read-Value([string]$key) { [SpiderSurge.Common.MigrationTest]::Read('SpiderSurge', $key) }

    Start-Case 'released-surge-and-stats'
    $surge = "TelemetryEnabled: true`nUnlimitedPerkChoosingTime: false`nEnableStatsLogging: false`nindicator.radius: 9`nindicator:`n  radius: 2.5`ndisplay:`n  showTelemetryConsentPrompt: false`n"
    $stats = "display:`n  showStatsWindow: true`n  position:`n    x: 960`n    y: 270`n  bigUI:`n    columns:`n      kills: false`ntitles:`n  enabled: false`ntracking:`n  enabled: false`nupdater:`n  checkForUpdates: false`n"
    $teams = "teams:`n  count: 3`nupdater:`n  checkForUpdates: false`n"
    Write-Config 'SpiderSurge' $surge
    Write-Config 'Stats_Mod' $stats
    Write-Config 'SpiderTeams' $teams
    [SpiderSurge.Common.MigrationTest]::Run()
    Assert-Equal (Read-Value 'surge.telemetry') 'true' 'Released telemetry consent survives'
    Assert-Equal (Read-Value 'surge.showTelemetryPrompt') 'false' 'Released consent prompt choice survives'
    Assert-Equal (Read-Value 'surge.unlimitedPerkTime') 'false' 'Released perk timer survives'
    Assert-Equal (Read-Value 'surge.statsLogging') 'false' 'Released logging choice survives'
    Assert-Equal (Read-Value 'surge.indicator.radius') '2.5' 'Nested released indicator beats obsolete flat value'
    Assert-Equal (Read-Value 'stats.display.position.xPercent') '50' 'Standalone Stats horizontal pixels convert'
    Assert-Equal (Read-Value 'stats.display.position.yPercent') '25' 'Standalone Stats vertical pixels convert'
    Assert-Equal (Read-Value 'stats.display.bigUI.columns.kills') 'false' 'Released column choice survives'
    Assert-Equal (Read-Value 'stats.titles.enabled') 'false' 'Released title choice survives'
    Assert-Equal (Read-Value 'stats.display.showStatsWindow') 'false' 'Disabled standalone tracking hides Stats window'
    Assert-Equal (Read-Value 'updater.checkForUpdates') 'false' 'Released updater opt-out survives'
    Assert-Equal (Read-Value 'teams.enabled') $null 'Teams settings are not imported'
    Assert-Equal ([IO.File]::ReadAllText([Silk.Config]::GetModConfigPath('SpiderSurge') + '.old')) $surge 'Surge backup preserves original bytes'
    Assert-Equal ([IO.File]::ReadAllText([Silk.Config]::GetModConfigPath('Stats_Mod') + '.old')) $stats 'Stats backup preserves original bytes'
    Assert-Equal (Test-Path ([Silk.Config]::GetModConfigPath('Stats_Mod'))) $false 'Imported Stats file moved aside'
    $migrated = [IO.File]::ReadAllText([Silk.Config]::GetModConfigPath('SpiderSurge'))
    [SpiderSurge.Common.MigrationTest]::Run()
    Assert-Equal ([IO.File]::ReadAllText([Silk.Config]::GetModConfigPath('SpiderSurge'))) $migrated 'Second startup does not rewrite settings'
    Assert-Equal ([IO.File]::ReadAllText([Silk.Config]::GetModConfigPath('SpiderTeams'))) $teams 'Unreleased Teams file stays untouched'
    Assert-Equal (Test-Path ([Silk.Config]::GetModConfigPath('SpiderTeams') + '.old')) $false 'No Teams backup created'

    Start-Case 'standalone-stats-only'
    Write-Config 'Stats_Mod' "titles:`n  enabled: false`n"
    Write-Config 'SpiderTeams' $teams
    [SpiderSurge.Common.MigrationTest]::Run()
    Assert-Equal (Read-Value 'teams.enabled') 'false' 'Teams keeps its default on first unified install'
    Assert-Equal (Read-Value 'updater.checkForUpdates') 'true' 'Unreleased Teams updater choice ignored'
    Assert-Equal (Read-Value 'stats.titles.enabled') 'false' 'Stats-only installation imports'

    Start-Case 'development-formats-only'
    $development = "parts:`n  stats: false`n  teams: false`nstats:`n  display:`n    position:`n      x: 960`nteams:`n  enabled: true`nUseDpadForUltimate: true`n"
    Write-Config 'SpiderSurge' $development
    Write-Config 'SpiderTeams' $teams
    [SpiderSurge.Common.MigrationTest]::Run()
    Assert-Equal ([IO.File]::ReadAllText([Silk.Config]::GetModConfigPath('SpiderSurge'))) $development 'Development formats and retired controls do not trigger migration'
    Assert-Equal (Test-Path ([Silk.Config]::GetModConfigPath('SpiderSurge') + '.old')) $false 'No development migration backup'
    Write-Config 'Stats_Mod' "titles:`n  enabled: false`n"
    [SpiderSurge.Common.MigrationTest]::Run()
    Assert-Equal (Read-Value 'teams.enabled') 'true' 'Importing released Stats preserves current Teams selection'
    Assert-Equal (Read-Value 'parts.stats') 'false' 'Released import does not rewrite development part flags'
    Assert-Equal (Read-Value 'stats.display.showStatsWindow') $null 'Development part flag does not hide Stats'
    Assert-Equal (Read-Value 'stats.display.position.xPercent') $null 'Unified development pixels are not converted'
    Assert-Equal (Read-Value 'stats.titles.enabled') 'false' 'Released Stats imports alongside development keys'

    Start-Case 'flat-released-surge'
    Write-Config 'SpiderSurge' "indicator.radius: 3.5`ndisplay.showTelemetryConsentPrompt: false`n"
    [SpiderSurge.Common.MigrationTest]::Run()
    Assert-Equal (Read-Value 'surge.indicator.radius') '3.5' 'Older flat Surge setting imports without nested value'
    Assert-Equal (Read-Value 'surge.showTelemetryPrompt') 'false' 'Older flat consent prompt imports'

    Start-Case 'legacy-stats-still-installed'
    Write-Config 'Stats_Mod' $stats
    [SpiderSurge.Common.Parts]::Stats.BlockedBy = 'StatsMod.dll'
    [SpiderSurge.Common.MigrationTest]::Run()
    Assert-Equal ([IO.File]::ReadAllText([Silk.Config]::GetModConfigPath('Stats_Mod'))) $stats 'Installed legacy mod keeps its settings'
    Assert-Equal (Test-Path ([Silk.Config]::GetModConfigPath('SpiderSurge'))) $false 'Blocked import does not create settings'
    Write-Output "PASS: $checks settings migration checks"
}
finally {
    $resolved = [IO.Path]::GetFullPath($temp)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (!$resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or !(Split-Path -Leaf $resolved).StartsWith('SpiderSurge-migration-')) { throw 'Unexpected test cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
