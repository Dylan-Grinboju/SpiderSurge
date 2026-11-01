$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $repo 'surge/Maps/SurgeMapHudPatches.cs'))
$stubs = @'
namespace HarmonyLib {
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
    public class HarmonyPatch : System.Attribute {
        public HarmonyPatch(System.Type type, string method) { }
    }
}
namespace UnityEngine.SceneManagement {
    public struct Scene { public string name; }
    public static class SceneManager {
        public static string Name = "MapEditorBase";
        public static Scene GetActiveScene() => new Scene { name = Name };
    }
}
public class SurvivalMode {
    public static SurvivalMode instance = new SurvivalMode();
    public int CurrentWave = 12;
    public bool HudActive = true;
    public bool GameModeHudActive() => HudActive;
}
public class SurvivalModeHud {
    public View survivalHud = new View();
    public Text waveCounter = new Text();
    public class View {
        public bool IsHidden = true;
        public int Shows, Hides;
        public void Show() { IsHidden = false; Shows++; }
        public void Hide() { IsHidden = true; Hides++; }
    }
    public class Text { public string text = "old"; public Transform transform = new Transform(); }
    public class Transform { public Parent parent = new Parent(); }
    public class Parent { public GameObject gameObject = new GameObject(); }
    public class GameObject { public bool Active; public void SetActive(bool value) { Active = value; } }
}
namespace SpiderSurge {
    public static class SurgeGameModeManager { public static bool IsSurgeRunActive = true; }
}
namespace SpiderSurge.Maps {
    public static class SurgeMaps {
        public static object Current = new object();
        public const string SceneName = "MapEditorBase";
    }
    public static class SurgeMapHudTest {
        static readonly System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        public static bool Display(SurvivalModeHud hud) => (bool)typeof(SurgeMapSurvivalHudPatch).GetMethod("Prefix", Flags).Invoke(null, new object[] { hud });
        public static bool Wave(SurvivalModeHud hud) => (bool)typeof(SurgeMapWaveCounterPatch).GetMethod("Prefix", Flags).Invoke(null, new object[] { hud });
    }
}
'@
Add-Type -TypeDefinition ($source + $stubs)
$checks = 0
function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected '$expected', got '$actual'" }
    $script:checks++
}

$hud = [SurvivalModeHud]::new()
Assert-Equal ([SpiderSurge.Maps.SurgeMapHudTest]::Display($hud)) $false 'Surge maps bypass the custom Versus HUD guard'
Assert-Equal $hud.survivalHud.IsHidden $false 'Survival HUD is visible on a Surge map'
[void][SpiderSurge.Maps.SurgeMapHudTest]::Display($hud)
Assert-Equal $hud.survivalHud.Shows 1 'Visible HUD does not restart its show animation each frame'
Assert-Equal ([SpiderSurge.Maps.SurgeMapHudTest]::Wave($hud)) $false 'Surge maps bypass the custom scene wave guard'
Assert-Equal $hud.waveCounter.text '12' 'Current wave is displayed'
Assert-Equal $hud.waveCounter.transform.parent.gameObject.Active $true 'Wave counter is visible'
[SurvivalMode]::instance.CurrentWave = 13
[void][SpiderSurge.Maps.SurgeMapHudTest]::Wave($hud)
Assert-Equal $hud.waveCounter.text '13' 'Wave counter advances within the same map'
[SurvivalMode]::instance.HudActive = $false
[void][SpiderSurge.Maps.SurgeMapHudTest]::Display($hud)
Assert-Equal $hud.survivalHud.IsHidden $true 'Native HUD visibility still hides an inactive round'
[void][SpiderSurge.Maps.SurgeMapHudTest]::Display($hud)
Assert-Equal $hud.survivalHud.Hides 1 'Hidden HUD does not restart its hide animation each frame'

foreach ($scenario in 'Vanilla scene', 'Ordinary custom map', 'Stopped Surge run') {
    [UnityEngine.SceneManagement.SceneManager]::Name = 'MapEditorBase'
    [SpiderSurge.Maps.SurgeMaps]::Current = [object]::new()
    [SpiderSurge.SurgeGameModeManager]::IsSurgeRunActive = $true
    switch ($scenario) {
        'Vanilla scene' { [UnityEngine.SceneManagement.SceneManager]::Name = 'Lobby' }
        'Ordinary custom map' { [SpiderSurge.Maps.SurgeMaps]::Current = $null }
        'Stopped Surge run' { [SpiderSurge.SurgeGameModeManager]::IsSurgeRunActive = $false }
    }
    $hud = [SurvivalModeHud]::new()
    Assert-Equal ([SpiderSurge.Maps.SurgeMapHudTest]::Display($hud)) $true "$scenario retains native HUD behavior"
    Assert-Equal ([SpiderSurge.Maps.SurgeMapHudTest]::Wave($hud)) $true "$scenario retains native wave behavior"
    Assert-Equal $hud.survivalHud.Shows 0 "$scenario does not force the HUD visible"
    Assert-Equal $hud.waveCounter.text 'old' "$scenario does not overwrite the wave counter"
}
Write-Output "PASS: $checks Surge map HUD checks"
