$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$controller = [IO.File]::ReadAllText((Join-Path $repo 'surge/Meter/ChallengeController.cs'))
$controller = $controller.Substring(0, $controller.IndexOf('    [HarmonyPatch')) + '}'
$stubs = @'
using System;
using System.Collections.Generic;
public class PlayerController { }
public class GameLevel { }
public class SurvivalMode {
    public static SurvivalMode instance = new SurvivalMode();
    public Unity.Netcode.NetworkVariable<int> lives = new Unity.Netcode.NetworkVariable<int> { Value = 3 };
    public int Lives => lives.Value;
    public int MaxLives = 3, CurrentWave = 3;
    public bool GameModeActive() => true;
}
public class LevelController {
    public void LoadLevelWithTransition(GameLevel level, float delay, string name, string author) { }
}
public class SurvivalModeHud {
    public static SurvivalModeHud instance = new SurvivalModeHud();
    public void ClosePerkChoiseView() { }
}
public class HudController { public static HudController instance = new HudController(); public bool paused; }
public class GameController {
    public static GameController instance = new GameController();
    public void DisableInputs() { }
    public void EnableInputs() { }
}
public class TimeScaleController {
    public static TimeScaleController instance = new TimeScaleController();
    public void Pause() { }
    public void Reset() { }
}
namespace HarmonyLib { }
namespace Unity.Netcode { public class NetworkVariable<T> { public T Value; } }
namespace UnityEngine {
    public static class Random { public static float value = 1f; public static int Range(int min, int max) => min; }
    public class Object { public static T[] FindObjectsOfType<T>() => new T[0]; }
}
namespace Silk { public static class Logger { public static void LogInfo(string value) { } public static void LogError(string value) { } } }
namespace SpiderSurge.Common {
    public static class Net { public static bool IsAuthority = true; public const ulong ServerId = 0; }
    public static class Players { public static List<PlayerController> All = new List<PlayerController>(); }
    public static class Parts { public static Part Stats = new Part(); public class Part { public bool Running = true; } }
    public static class GameFields {
        public static Unity.Netcode.NetworkVariable<int> Lives(SurvivalMode mode) => mode.lives;
    }
    public static class Listeners { public static void Raise(Action action, string message) { action?.Invoke(); } }
}
namespace SpiderSurge.Logging { public class ChaosEventRecord { public int Wave; public string Text; } }
namespace SpiderSurge {
    public static class SurgeGameModeManager { public static bool IsSurgeRunActive = true, AbilitiesDisabled; }
    public static class AbilitySelection { public static bool IsChoosing; }
    public class PerksManager {
        public static PerksManager Instance = new PerksManager();
        public string[] GetAbilityPerkNames() => new string[0];
        public string[] GetAbilityUltimatePerkNames() => new string[0];
        public int GetPerkLevel(string key) => 0;
    }
    public class BaseAbility { public void RescaleChallengeCooldown(float value) { } }
    public static class PerkShop { public static int VisitId; public static void ClearOffer() { } }
    public static class ChallengePanel {
        public static bool IsShown = true;
        public static void Hide() { }
        public static void Show(bool visible) { }
    }
    public static class ChaosNet {
        public static void Flush() { }
        public static void MarkDirty() { }
        public static void Announce(string text) { }
    }
    public enum FeedbackCue { Confirm, Cancel, Failure }
    public static class FeedbackSounds { public static void Broadcast(FeedbackCue cue) { } }
}
'@
$sources = @($controller, $stubs)
foreach ($file in 'ChaosWallet', 'ChaosRules', 'CleanHandsProgress') {
    $sources += [IO.File]::ReadAllText((Join-Path $repo "surge/Meter/$file.cs"))
}
$imports = foreach ($source in $sources) { [regex]::Matches($source, '(?m)^using [^\r\n]+;').Value }
$body = ($sources | ForEach-Object { [regex]::Replace($_, '(?m)^using [^\r\n]+;\r?\n', '') }) -join "`n"
Add-Type -TypeDefinition ((($imports | Select-Object -Unique) -join "`n") + "`n" + $body)
$checks = 0
function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected '$expected', got '$actual'" }
    $script:checks++
}
function Start-Challenge([int]$points) {
    [SpiderSurge.Common.Net]::IsAuthority = $true
    [SpiderSurge.ChaosWallet]::ResetRun()
    [SpiderSurge.ChaosWallet]::GrantPoints($points, 'Setup')
    $flags = [Reflection.BindingFlags]'NonPublic,Static'
    $active = [SpiderSurge.ChallengeController].GetMethod('SetActive', $flags)
    [void]$active.Invoke($null, @([SpiderSurge.ChallengeKind]::CleanHands))
}

Start-Challenge 3
$first = [SpiderSurge.ChallengeController]::FriendlyHarm()
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 2 'First friendly harm costs one point'
Assert-Equal ([SpiderSurge.ChallengeController]::Failed) $true 'The reward is lost'
$second = [SpiderSurge.ChallengeController]::FriendlyHarm()
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 2 'Further harm does not charge again'
[SpiderSurge.ChallengeController]::RevertFriendlyHarm($first)
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 2 'Another remaining kill prevents a refund'
[SpiderSurge.ChallengeController]::RevertFriendlyHarm($first)
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 2 'A duplicate reversal cannot refund'
[SpiderSurge.ChallengeController]::RevertFriendlyHarm($second)
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 3 'Reversing all harm refunds exactly the deducted point'
Assert-Equal ([SpiderSurge.ChallengeController]::Failed) $false 'Reversing all harm restores the reward'
Assert-Equal ([SpiderSurge.ChaosWallet]::PointsEarned) 3 'Refund is not new earnings'
Assert-Equal ([SpiderSurge.ChaosWallet]::PointsSpent) 0 'Penalty is not a shop purchase'
$again = [SpiderSurge.ChallengeController]::FriendlyHarm()
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 2 'New harm after restoration fails the challenge again'
[SpiderSurge.ChallengeController]::Settle()
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 2 'Settlement keeps the penalty and grants no reward'
Assert-Equal ([SpiderSurge.ChaosWallet]::ChallengesCompleted) 0 'A failed challenge is not completed'
[SpiderSurge.ChallengeController]::RevertFriendlyHarm($again)
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 2 'A reversal after settlement cannot refund an expired challenge'

Start-Challenge 0
$empty = [SpiderSurge.ChallengeController]::FriendlyHarm()
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 0 'An empty wallet never goes negative'
[SpiderSurge.ChallengeController]::RevertFriendlyHarm($empty)
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 0 'An unpaid penalty cannot create a refund'
[SpiderSurge.ChallengeController]::Settle()
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 1 'A restored challenge still grants its normal reward'

Start-Challenge 2
[void][SpiderSurge.ChallengeController]::FriendlyHarm()
$kill = [SpiderSurge.ChallengeController]::FriendlyHarm()
[SpiderSurge.ChallengeController]::RevertFriendlyHarm($kill)
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 1 'A shield break still prevents refunding a reversed kill'
[SpiderSurge.ChallengeController]::Settle()
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 1 'The shield break still forfeits the reward'

Start-Challenge 2
$old = [SpiderSurge.ChallengeController]::FriendlyHarm()
Start-Challenge 2
$current = [SpiderSurge.ChallengeController]::FriendlyHarm()
[SpiderSurge.ChallengeController]::RevertFriendlyHarm($old)
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 1 'A previous challenge token cannot refund current harm'
[SpiderSurge.Common.Net]::IsAuthority = $false
[void][SpiderSurge.ChallengeController]::FriendlyHarm()
[SpiderSurge.ChallengeController]::RevertFriendlyHarm($current)
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 1 'A remote client cannot deduct or refund points'
Assert-Equal ([SpiderSurge.ChallengeController]::Failed) $true 'A remote client cannot restore eligibility'
[SpiderSurge.Common.Net]::IsAuthority = $true
[SpiderSurge.ChallengeController]::RevertFriendlyHarm($current)
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 2 'The authority can refund the current challenge once'
[SpiderSurge.ChallengeController]::RevertFriendlyHarm($current)
Assert-Equal ([SpiderSurge.ChaosWallet]::Points) 2 'Duplicate reversal cannot refund twice'
Write-Output "PASS: $checks Clean Hands penalty checks"
