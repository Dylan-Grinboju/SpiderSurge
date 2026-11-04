$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $repo 'surge/Maps/SurgeMaps.cs'))
function Get-Method([string]$signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Method not found: $signature" }
    $open = $source.IndexOf('{', $start)
    $depth = 1
    $end = $open + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw "Unclosed method: $signature" }
    $source.Substring($start, $end - $start)
}
$receiver = Get-Method 'private static void OnMapFromHost('
$build = Get-Method 'public static void BuildOnClient('
$stubs = @'
using System.IO;
using System.Collections.Generic;
namespace UnityEngine.SceneManagement {
    public struct Scene { public string name; }
    public static class SceneManager {
        public static string Name = "MapEditorBase";
        public static Scene GetActiveScene() => new Scene { name = Name };
    }
}
public class GameLevel { public bool zeroGravity; }
public class LevelController {
    public static LevelController instance = new LevelController();
    public GameLevel activeLevel = new GameLevel();
}
public enum EditorState { Editing, Playing }
public class CustomMapEditor {
    public static CustomMapEditor instance;
    public static EditorState editorState;
    public class Parent { public int childCount; }
    public Parent objParent = new Parent();
    public int Loads;
    public void LoadMap(string path) { Loads++; objParent.childCount = 1; }
}
public static class VersusMode { public static string CustomMapString; }
namespace SpiderSurge.Maps {
    public static class Net { public static bool IsAuthority; }
    public class SurgeMap {
        public string Path = "surge/test", json = "layout";
        public GameLevel level = new GameLevel();
    }
    public static class SurgeMaps {
        public const string SceneName = "MapEditorBase";
        public static SurgeMap Current;
        public static Dictionary<string, SurgeMap> _byId = new Dictionary<string, SurgeMap>();
        private static void Load() { }
'@
Add-Type -TypeDefinition ($stubs + $receiver + $build + '}}')
$method = [SpiderSurge.Maps.SurgeMaps].GetMethod('OnMapFromHost', [Reflection.BindingFlags]'NonPublic,Static')
$checks = 0
function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected '$expected', got '$actual'" }
    $script:checks++
}
function Receive([string]$id) {
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    $writer.Write($id)
    $stream.Position = 0
    $reader = [IO.BinaryReader]::new($stream)
    try { [void]$method.Invoke($null, @($reader)) } finally { $reader.Dispose() }
}

foreach ($id in 'satellite', 'derelict', 'caldera') {
    $layout = Get-Content -Raw (Join-Path $repo "surge/Maps/Layouts/$id.json") | ConvertFrom-Json
    $map = [SpiderSurge.Maps.SurgeMap]::new()
    $map.level.zeroGravity = $layout.zeroGravity
    [SpiderSurge.Maps.SurgeMaps]::_byId[$id] = $map
    [SpiderSurge.Maps.SurgeMaps]::Current = $null
    [UnityEngine.SceneManagement.SceneManager]::Name = 'MapEditorBase'
    $controller = [LevelController]::new()
    $controller.activeLevel.zeroGravity = -not $layout.zeroGravity
    [LevelController]::instance = $controller
    $editor = [CustomMapEditor]::new()
    [CustomMapEditor]::instance = $editor
    Receive $id
    Assert-Equal ([object]::ReferenceEquals($controller.activeLevel, $map.level)) $true "$id late join refreshes the cached level"
    Assert-Equal $controller.activeLevel.zeroGravity $layout.zeroGravity "$id uses the host map's gravity"
    Assert-Equal $editor.Loads 1 "$id builds its client layout"

    $controller.activeLevel = [GameLevel]::new()
    Receive $id
    Assert-Equal ([object]::ReferenceEquals($controller.activeLevel, $map.level)) $true "$id refreshes the level even if the layout already exists"
    Assert-Equal $editor.Loads 1 "$id does not rebuild an existing layout"
}

[UnityEngine.SceneManagement.SceneManager]::Name = 'Lobby'
$previous = [GameLevel]::new()
[LevelController]::instance.activeLevel = $previous
Receive 'satellite'
Assert-Equal ([object]::ReferenceEquals([LevelController]::instance.activeLevel, $previous)) $true 'An upcoming map does not change the current scene gravity'
Assert-Equal ([SpiderSurge.Maps.SurgeMaps]::Current.level.zeroGravity) $true 'An upcoming map is retained for the scene load'

[UnityEngine.SceneManagement.SceneManager]::Name = 'MapEditorBase'
Receive ''
Assert-Equal ([object]::ReferenceEquals([LevelController]::instance.activeLevel, $previous)) $true 'Ending a Surge map does not assign a missing level'
Assert-Equal ([SpiderSurge.Maps.SurgeMaps]::Current -eq $null) $true 'An empty map message clears the current map'
Write-Output "PASS: $checks Surge map join checks"
