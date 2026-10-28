$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $repo 'surge/Meter/CleanHandsProgress.cs')))
$checks = 0

function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected '$expected', got '$actual'" }
    $script:checks++
}

$progress = [SpiderSurge.CleanHandsProgress]::new()
Assert-Equal $progress.Failed $false 'Clean Hands starts eligible'
$first = $progress.RecordHarm()
$second = $progress.RecordHarm()
Assert-Equal $progress.Failed $true 'Two credited teammate kills fail the challenge'
Assert-Equal ($progress.Revert($first)) $true 'An astral return removes its own kill'
Assert-Equal $progress.Failed $true 'Another credited kill still fails the challenge'
Assert-Equal ($progress.Revert($first)) $false 'A duplicate return cannot undo another kill'
Assert-Equal ($progress.Revert($second)) $true 'The second astral return removes its kill'
Assert-Equal $progress.Failed $false 'No remaining credited kills restores eligibility'
[void]$progress.RecordHarm()
$kill = $progress.RecordHarm()
[void]$progress.Revert($kill)
Assert-Equal $progress.Failed $true 'A teammate shield break survives an astral return'
$old = $progress.RecordHarm()
$progress.Reset()
Assert-Equal $progress.Failed $false 'The next challenge starts clean'
$current = $progress.RecordHarm()
Assert-Equal ($progress.Revert($old)) $false 'A previous challenge kill cannot affect this challenge'
Assert-Equal $progress.Failed $true 'A previous challenge return cannot clear current harm'
[void]$progress.Revert($current)
Assert-Equal $progress.Failed $false 'The current challenge kill can still be reverted'
Assert-Equal ($progress.Revert($null)) $false 'A kill outside Clean Hands has no effect'

$stubs = @'
public class EnemyHealthSystem {
    public bool Dead;
    public UnityEngine.Transform transform = new UnityEngine.Transform();
    public Unity.Netcode.NetworkObject Network = new Unity.Netcode.NetworkObject();
    public bool IsDead() => Dead;
    public T GetComponent<T>() where T : class => Network as T;
}
public class SurvivalModeHud {
    public static SurvivalModeHud instance = new SurvivalModeHud();
    public View survivalHud = new View();
    public class View { public bool IsVisible = true; }
}
public static class LevelController { public static bool IsLoading; }
namespace Unity.Netcode { public class NetworkObject { public bool IsSpawned = true; } }
namespace UnityEngine {
    public class MonoBehaviour { public bool enabled = true; }
    public class Transform { public Vector3 position = new Vector3(.5f, .5f, 1f); }
    public struct Vector3 {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public class Camera {
        public static Camera main = new Camera();
        public Vector3 WorldToViewportPoint(Vector3 point) => point;
    }
    public class GUIStyle { public int fontSize; }
    public enum TextAnchor { MiddleCenter }
    public enum EventType { Repaint }
    public class Event { public static Event current = new Event(); public EventType type; }
    public static class Time { public static float deltaTime = 1f; }
    public static class Screen { public static int width = 1066, height = 600; }
    public static class Mathf {
        public static float Min(float a, float b) => System.Math.Min(a, b);
        public static int RoundToInt(float value) => (int)System.Math.Round(value);
    }
    public struct Rect {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) {
            this.x = x; this.y = y; this.width = width; this.height = height;
        }
    }
}
namespace SpiderSurge.Common {
    public class ProgressSection<T> {
        public delegate bool Parser(string line, out string key, out T value);
        private System.Collections.Generic.HashSet<string> keys = new System.Collections.Generic.HashSet<string>();
        public ProgressSection(string name, Parser parse, System.Func<string,T,string> format, System.Func<T,T,T> combine) { }
        public bool Contains(string key) => keys.Contains(key);
        public void Add(string key, T value) => keys.Add(key);
    }
    public static class GameMenus { public static bool Open; }
    public static class Hud {
        public static int FontVersion, Ink, Pink, Text;
        public static void EnsureFonts() { }
        public static UnityEngine.GUIStyle Style(int size, bool bold, UnityEngine.TextAnchor anchor, bool wrap) => new UnityEngine.GUIStyle();
        public static void Fill(UnityEngine.Rect rect, int color) { }
        public static void Label(UnityEngine.Rect rect, string text, UnityEngine.GUIStyle style, int color) { }
    }
}
namespace SpiderSurge {
    public static class SurgeGameModeManager { public static bool IsSurgeRunActive = true; }
    public static class ChaosWallet { public static bool Enabled = true; }
    public static class ChallengeController { public static bool Pending; }
}
'@
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $repo 'surge/UI/EncounterHints.cs')) + $stubs)
$hints = [SpiderSurge.EncounterHints]::new()
$flags = [Reflection.BindingFlags]'NonPublic,Instance'
$awake = $hints.GetType().GetMethod('Awake', $flags)
$update = $hints.GetType().GetMethod('Update', $flags)
$current = $hints.GetType().GetField('current', $flags)
[void]$awake.Invoke($hints, @())
Assert-Equal $hints.enabled $false 'Idle hints do not run an empty OnGUI every frame'
$firstEnemy = [EnemyHealthSystem]::new()
$survivor = [EnemyHealthSystem]::new()
[SpiderSurge.Common.GameMenus]::Open = $true
Assert-Equal ([SpiderSurge.EncounterHints]::Encounter('cocoon', 'Cocoon hint', $firstEnemy)) $false 'Queuing does not mark a hint shown'
Assert-Equal $hints.enabled $true 'A new encounter wakes the hint component'
Assert-Equal ([SpiderSurge.EncounterHints]::Encounter('cocoon', 'Cocoon hint', $survivor)) $false 'Other enemies keep retrying while the hint waits'
$firstEnemy.Dead = $true
[SpiderSurge.Common.GameMenus]::Open = $false
[void]$update.Invoke($hints, @())
Assert-Equal ($null -eq $current.GetValue($hints)) $true 'A dead queued enemy is discarded'
Assert-Equal ([SpiderSurge.EncounterHints]::Encounter('cocoon', 'Cocoon hint', $survivor)) $false 'A surviving enemy requeues the discarded hint'
[void]$update.Invoke($hints, @())
Assert-Equal ($null -ne $current.GetValue($hints)) $true 'The survivor supplies a visible hint'
Assert-Equal ([SpiderSurge.EncounterHints]::Encounter('cocoon', 'Cocoon hint', $survivor)) $false 'An incomplete display still permits retry'
1..5 | ForEach-Object { [void]$update.Invoke($hints, @()) }
Assert-Equal ([SpiderSurge.EncounterHints]::Encounter('cocoon', 'Cocoon hint', $survivor)) $true 'Only the completed display suppresses later attempts'
[void]$update.Invoke($hints, @())
Assert-Equal ($null -eq $current.GetValue($hints)) $true 'Repeated attempts did not duplicate the hint'
Assert-Equal $hints.enabled $false 'The component sleeps after its last hint completes'
[void][SpiderSurge.EncounterHints]::Encounter('laser', 'Laser hint', $survivor)
[void]$update.Invoke($hints, @())
[SpiderSurge.EncounterHints]::ResetRun()
Assert-Equal $hints.enabled $false 'A run reset sleeps the hint component'
Assert-Equal ($null -eq $current.GetValue($hints)) $true 'A synchronous restart immediately clears the visible hint'
Assert-Equal ([SpiderSurge.EncounterHints]::Encounter('laser', 'Laser hint', $survivor)) $false 'Restart does not save an unfinished hint as seen'
[void]$update.Invoke($hints, @())
[LevelController]::IsLoading = $true
1..7 | ForEach-Object { [void]$update.Invoke($hints, @()) }
Assert-Equal ($null -eq $current.GetValue($hints)) $true 'Loading clears visible and queued hints'
[LevelController]::IsLoading = $false
Assert-Equal ([SpiderSurge.EncounterHints]::Encounter('laser', 'Laser hint', $survivor)) $false 'Loading time does not complete an unseen hint'
[void]$update.Invoke($hints, @())
$survivor.Dead = $true
[void]$update.Invoke($hints, @())
Assert-Equal ($null -eq $current.GetValue($hints)) $true 'An enemy dying during a hint cancels that display'
$replacement = [EnemyHealthSystem]::new()
Assert-Equal ([SpiderSurge.EncounterHints]::Encounter('laser', 'Laser hint', $replacement)) $false 'Another living enemy can retry an interrupted hint'
Write-Output "PASS: $checks review regression checks"
