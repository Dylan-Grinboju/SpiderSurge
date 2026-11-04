$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = Get-Content (Join-Path $repo 'stats/GameSession/GameSessionManager.cs') -Raw
function Method([string]$signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Missing method $signature" }
    $end = $source.IndexOf('{', $start) + 1
    $depth = 1
    while ($depth -gt 0) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    $source.Substring($start, $end - $start)
}
$body = @('public TimeSpan GetCurrentSessionTime()', 'public void WriteSession(', 'public void ReadSession(', 'private static void WriteTail(') | ForEach-Object { Method $_ }
$stubs = @'
#pragma warning disable 0649, 0414
using System;
using System.Collections.Generic;
using System.IO;
public static class Time { public static float unscaledTime; }
public static class Net { public static bool IsRemoteClient; }
public enum GameMode { None, Survival, Versus }
public sealed class Enemies { public int EnemiesKilled; public void SetEnemiesKilled(int value) { EnemiesKilled = value; } }
public static class Strings { public static void WriteText(this BinaryWriter writer, string value) { writer.Write(value); } }
public class SessionProbe {
    bool isSurvivalActive, isVersusActive, isPaused, isMirrored, restartRequested, remoteRestartRequested;
    GameMode currentGameMode, lastGameMode;
    DateTime sessionStartTime, pauseStartTime;
    TimeSpan lastGameDuration;
    float remoteSessionSeconds, remoteReceivedAt;
    int painLevel;
    const int MaxListSent = 32;
    readonly Enemies enemiesTracker = new Enemies();
    readonly List<string> mapsPlayed = new List<string>(), perksChosen = new List<string>();
    public bool IsActive => isSurvivalActive || isVersusActive;
    public void Host(bool paused, bool active) {
        isSurvivalActive = active; isPaused = paused;
        pauseStartTime = DateTime.Now; sessionStartTime = pauseStartTime.AddSeconds(-30);
        currentGameMode = GameMode.Survival; mapsPlayed.Add("Arena"); perksChosen.Add("Luck");
    }
'@
$tests = @'
    public static int Run() {
        int checks = 0;
        foreach (bool paused in new[] { false, true }) foreach (bool active in new[] { false, true }) {
            Net.IsRemoteClient = false;
            var host = new SessionProbe(); host.Host(paused, active);
            var client = new SessionProbe();
            using (var stream = new MemoryStream()) {
                host.WriteSession(new BinaryWriter(stream)); stream.Position = 0;
                Time.unscaledTime = 100f; Net.IsRemoteClient = true;
                client.ReadSession(new BinaryReader(stream));
                if (stream.Position != stream.Length || client.isPaused != paused || client.mapsPlayed[0] != "Arena") throw new Exception("Session payload misaligned");
                checks++;
                double before = client.GetCurrentSessionTime().TotalSeconds;
                Time.unscaledTime += 5f;
                double expected = active && !paused ? 5 : 0;
                if (Math.Abs(client.GetCurrentSessionTime().TotalSeconds - before - expected) > 0.001) throw new Exception("Incorrect paused/active remote interpolation");
                checks++;
            }
        }
        return checks;
    }
}
'@
Add-Type -TypeDefinition ($stubs + ($body -join "`n") + $tests) -WarningAction SilentlyContinue
Write-Output ("PASS: {0} remote session timer checks" -f [SessionProbe]::Run())
