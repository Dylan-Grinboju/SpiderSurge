$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $repo 'surge/Net/SurgeNet.cs'))
$start = $source.IndexOf('private static void OnState(')
if ($start -lt 0) { throw 'State receiver not found' }
$open = $source.IndexOf('{', $start)
$depth = 1
$end = $open + 1
while ($depth -gt 0 -and $end -lt $source.Length) {
    if ($source[$end] -eq '{') { $depth++ }
    if ($source[$end] -eq '}') { $depth-- }
    $end++
}
if ($depth -ne 0) { throw 'Unclosed state receiver' }
$receiver = $source.Substring($start, $end - $start)
$stubs = @'
using System;
using System.IO;
using System.Collections.Generic;
using Logger = Silk.Logger;
namespace Silk { public static class Logger { public static int Errors; public static void LogError(string message) { Errors++; } } }
namespace SpiderSurge {
    public static class Net { public static bool IsRemoteClient = true; }
    public class SurgeGameModeManager {
        public static SurgeGameModeManager Instance = new SurgeGameModeManager();
        public static bool IsSurgeRunActive;
        public static void SelectMode(bool mode) { }
        public void ApplyRemote(bool active, bool used, bool noAbilities) { IsSurgeRunActive = active; }
    }
    public class PerksManager {
        public static PerksManager Instance = new PerksManager();
        public Dictionary<string,int> Levels;
        public void ApplyRemote(Dictionary<string,int> levels, bool first, bool upgrade, bool swap) { Levels = levels; }
    }
    public static class ModifierManager_GetNonMaxedSurvivalMods_Patch { public static HashSet<string> LuckyPerkKeys = new HashSet<string>(); }
    public static class PlayerAbilityHandler {
        public static int Attachments, WaveRefunds;
        public static void ApplyRemoteEpoch(int epoch) { }
        public static void AttachToExistingSpiders() { Attachments++; }
        public static void OnWaveStarted(int wave) { WaveRefunds++; }
    }
    public static class PerkChoiceRefresh { public static void Refresh() { } }
    public static class ReceiverTest {
        private static bool _awaitingFirstState = true;
        private static int _lastRemoteWave;
'@
Add-Type -TypeDefinition ($stubs + $receiver + '}}')
$method = [SpiderSurge.ReceiverTest].GetMethod('OnState', [Reflection.BindingFlags]'NonPublic,Static')
$checks = 0
function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected '$expected', got '$actual'" }
    $script:checks++
}
function Receive([bool]$active, [int]$wave = 0, [ulong]$sender = 0) {
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    $writer.Write($false); $writer.Write($active); $writer.Write($active); $writer.Write($false)
    $writer.Write($wave); $writer.Write($false); $writer.Write($false); $writer.Write($false)
    $writer.Write([int]1); $writer.Write('pulse'); $writer.Write([int]1)
    $writer.Write([int]0); $writer.Write([int]1)
    $stream.Position = 0
    $reader = [IO.BinaryReader]::new($stream)
    [void]$method.Invoke($null, @($sender, $reader))
    $reader.Dispose()
}
Receive $false
Assert-Equal ([SpiderSurge.PlayerAbilityHandler]::Attachments) 0 'Inactive waiting-room snapshot does not attach abilities'
Receive $true
Assert-Equal ([SpiderSurge.PlayerAbilityHandler]::Attachments) 1 'First active round attaches spiders that spawned before its state'
Assert-Equal ([SpiderSurge.PerksManager]::Instance.Levels['pulse']) 1 'The active build is applied before attachment'
Receive $true
Assert-Equal ([SpiderSurge.PlayerAbilityHandler]::Attachments) 1 'Repeated active state does not repeatedly scan spiders'
Receive $false
Receive $true
Assert-Equal ([SpiderSurge.PlayerAbilityHandler]::Attachments) 2 'A later activation also attaches existing spiders'
Receive $false
Receive $true 0 7
Assert-Equal ([SpiderSurge.SurgeGameModeManager]::IsSurgeRunActive) $false 'A non-host sender cannot activate Surge'
[SpiderSurge.Net]::IsRemoteClient = $false
Receive $true
Assert-Equal ([SpiderSurge.SurgeGameModeManager]::IsSurgeRunActive) $false 'The host ignores client state snapshots'
[SpiderSurge.Net]::IsRemoteClient = $true
Receive $true 1
Receive $true 2
Assert-Equal ([SpiderSurge.PlayerAbilityHandler]::WaveRefunds) 1 'Advancing a replicated wave still refunds cooldowns once'
Receive $true 2
Assert-Equal ([SpiderSurge.PlayerAbilityHandler]::WaveRefunds) 1 'Repeated wave state does not refund cooldowns again'
Assert-Equal ([Silk.Logger]::Errors) 0 'All valid state payloads were read without errors'
Write-Output "PASS: $checks Surge state checks"
