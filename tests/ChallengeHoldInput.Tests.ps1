param([string]$ButtonSource)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$stubs = @'
using System;
using System.Reflection;
using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace UnityEngine {
    public class MonoBehaviour {
        public GameObject gameObject = new GameObject();
        public RectTransform transform = new RectTransform();
        public T GetComponentInParent<T>() where T : new() => new T();
    }
    public class GameObject {
        public GameObject(string name = "", params Type[] types) { }
        public RectTransform transform => new RectTransform();
        public T GetComponent<T>() where T : new() => new T();
    }
    public class RectTransform {
        public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta;
        public Rect rect = new Rect();
        public void SetParent(RectTransform parent, bool world) { }
    }
    public struct Rect { public float width; }
    public struct Vector2 {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2();
    }
    public class Canvas { public bool enabled = true; }
    public static class Application { public static bool isFocused = true; }
    public static class Time { public static float unscaledTime; }
    public static class Mathf { public static float Max(float x, float y) => Math.Max(x, y); }
}
namespace TMPro { public class TMP_Text { } }
namespace UnityEngine.UI {
    public class Button { public bool Interactable = true; public bool IsInteractable() => Interactable; }
    public class Image { public object color; public bool raycastTarget; }
}
namespace UnityEngine.EventSystems {
    public class EventSystem { public static EventSystem current; public GameObject currentSelectedGameObject; public BaseInputModule currentInputModule; }
    public class BaseInputModule { public EventSystem Events; public T GetComponent<T>() where T : class => Events as T; }
    public class BaseEventData { public BaseInputModule currentInputModule; }
    public class PointerEventData : BaseEventData { public enum InputButton { Left, Right } public InputButton button; }
    public interface IPointerDownHandler { void OnPointerDown(PointerEventData e); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData e); }
    public interface IPointerExitHandler { void OnPointerExit(PointerEventData e); }
    public interface ISubmitHandler { void OnSubmit(BaseEventData e); }
    public interface IDeselectHandler { void OnDeselect(BaseEventData e); }
}
namespace UnityEngine.InputSystem {
    public class InputControl { }
    public class InputAction { public InputControl activeControl; public InputControl[] controls = new InputControl[0]; }
    public class InputActionReference { public InputAction action; }
    public class Mouse { public static Mouse current; public ButtonControl leftButton = new ButtonControl(); }
}
namespace UnityEngine.InputSystem.Controls { public class ButtonControl : InputControl { public bool isPressed, wasPressedThisFrame; } }
namespace UnityEngine.InputSystem.UI { public class InputSystemUIInputModule : BaseInputModule { public InputActionReference submit; } }
namespace SpiderSurge.Common {
    public static class Net { public static bool IsAuthority = true; }
    public static class SettingsPage { public static void SetText(TMP_Text label, string text) { } }
}
public class HudController { public static HudController instance; public bool paused; }
namespace SpiderSurge {
    public static class Hud { public static object Pink; }
    public static class ChallengeController { public static bool Pending = true; }
    public static class ChaosNet { public static int Sent; public static void SendChallenge(bool accept) { Sent++; } }
}
'@
if (!$ButtonSource) { $ButtonSource = Join-Path $repo 'surge/UI/ChallengeHoldButton.cs' }
$sources = @((Join-Path $repo 'surge/UI/HoldConfirmation.cs'), $ButtonSource) | ForEach-Object {
    [regex]::Replace([IO.File]::ReadAllText($_), '(?m)^using [^\r\n]+;\r?\n', '')
}
Add-Type -TypeDefinition ($stubs + ($sources -join "`n"))
$type = [SpiderSurge.ChallengeHoldButton]
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
function Set-Field($target, $name, $value) { $type.GetField($name, $flags).SetValue($target, $value) }
function Tick($target, [single]$time) {
    [UnityEngine.Time]::unscaledTime = $time
    [void]$type.GetMethod('Update', $flags).Invoke($target, @())
}
$checks = 0
function Assert-Equal($actual, $expected, $label) {
    if ($actual -ne $expected) { throw "$label expected '$expected', got '$actual'" }
    $script:checks++
}
foreach ($scenario in 'Originating module', 'Changed current system', 'Missing active control', 'Early release', 'Deselection', 'Pause', 'Remote client') {
    $button = [SpiderSurge.ChallengeHoldButton]::new()
    Set-Field $button '_button' ([UnityEngine.UI.Button]::new())
    Set-Field $button '_canvas' ([UnityEngine.Canvas]::new())
    Set-Field $button '_hold' ([SpiderSurge.HoldConfirmation]::new())
    $pad = [UnityEngine.InputSystem.Controls.ButtonControl]::new()
    $action = [UnityEngine.InputSystem.InputAction]::new()
    $action.controls = @($pad)
    $module = [UnityEngine.InputSystem.UI.InputSystemUIInputModule]::new()
    $module.submit = [UnityEngine.InputSystem.InputActionReference]::new()
    $module.submit.action = $action
    $events = [UnityEngine.EventSystems.EventSystem]::new()
    $events.currentInputModule = $module
    $events.currentSelectedGameObject = $button.gameObject
    $module.Events = $events
    [UnityEngine.EventSystems.EventSystem]::current = $events
    $setModule = $type.GetMethod('SetInputModule', $flags)
    if ($setModule) { [void]$setModule.Invoke($button, @($module)) }
    [SpiderSurge.ChaosNet]::Sent = 0
    [SpiderSurge.Common.Net]::IsAuthority = $true
    [HudController]::instance = [HudController]::new()
    Tick $button 0
    $pad.isPressed = $pad.wasPressedThisFrame = $true
    if ($scenario -ne 'Missing active control') { $action.activeControl = $pad }
    if ($scenario -eq 'Originating module') { [UnityEngine.EventSystems.EventSystem]::current = [UnityEngine.EventSystems.EventSystem]::new() }
    if ($scenario -eq 'Remote client') { [SpiderSurge.Common.Net]::IsAuthority = $false }
    $submit = [UnityEngine.EventSystems.BaseEventData]::new()
    $submit.currentInputModule = $module
    $button.OnSubmit($submit)
    if ($scenario -eq 'Changed current system') { [UnityEngine.EventSystems.EventSystem]::current = [UnityEngine.EventSystems.EventSystem]::new() }
    Tick $button 1
    Assert-Equal ([SpiderSurge.ChaosNet]::Sent) 0 "$scenario does not confirm on press"
    if ($scenario -eq 'Early release') { $pad.isPressed = $false }
    if ($scenario -eq 'Deselection') { $events.currentSelectedGameObject = $null; $button.OnDeselect($submit) }
    if ($scenario -eq 'Pause') { [HudController]::instance.paused = $true }
    Tick $button 2
    $expected = if ($scenario -in 'Early release', 'Deselection', 'Pause', 'Remote client') { 0 } else { 1 }
    Assert-Equal ([SpiderSurge.ChaosNet]::Sent) $expected "$scenario completion"
    Tick $button 3
    Assert-Equal ([SpiderSurge.ChaosNet]::Sent) $expected "$scenario sends once"
}
Write-Output "PASS: $checks challenge input regression checks (stubbed Unity input)"
