$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
function Get-Method([string]$file, [string]$signature) {
    $source = [IO.File]::ReadAllText((Join-Path $repo $file))
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
$protection = Get-Method 'surge/Abilities/BlackHoleField.cs' 'public static bool ProtectsFromPoison('
$poison = Get-Method 'surge/Abilities/BlackHolePoisonPatch.cs' 'private static bool Prefix('
$movement = Get-Method 'surge/Abilities/FrozenEnemy.cs' 'private static bool Prefix(DeathCloud __instance)'
$stubs = @'
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
    public struct Vector2 { public float x; public static Vector2 zero => new Vector2(); }
    public class Rigidbody2D { public Vector2 velocity; }
    public class GameObject {
        public object Component;
        public T GetComponent<T>() where T : class => Component as T;
    }
    public class Transform {
        public EnemyHealthSystem Enemy;
        public Vector2 position;
        public T GetComponentInParent<T>() where T : class => Enemy as T;
    }
    public class Collider2D {
        public GameObject gameObject;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
}
public class SpiderHealthSystem { public bool dead, astralDead; }
public class EnemyHealthSystem {
    public bool Frozen, Held, Swallowed, Dazed;
    public UnityEngine.GameObject gameObject;
    public UnityEngine.Transform transform = new UnityEngine.Transform();
    public EnemyHealthSystem() { gameObject = new UnityEngine.GameObject { Component = this }; }
}
public class DeathCloud {
    public UnityEngine.Transform origin, transform = new UnityEngine.Transform();
    public UnityEngine.Rigidbody2D rb2d = new UnityEngine.Rigidbody2D();
    public bool Frozen;
    public Dictionary<UnityEngine.GameObject, float> inRange = new Dictionary<UnityEngine.GameObject, float>();
}
namespace SpiderSurge {
    public static class Consts { public static class Values { public static class Storage { public const float BlackHolePullTime = 3f; } } }
    public class BlackHoleField {
        private static readonly List<BlackHoleField> ActiveFields = new List<BlackHoleField>();
        private SpiderHealthSystem _caster;
        private float _elapsed;
        public bool isActiveAndEnabled = true;
        private bool CasterAlive => _caster != null && !_caster.dead && !_caster.astralDead;
        public static BlackHoleField Open(SpiderHealthSystem caster) {
            var field = new BlackHoleField { _caster = caster }; ActiveFields.Add(field); return field;
        }
        public void Expire() { _elapsed = 3f; }
        PROTECTION
    }
    public static class FrozenEnemy {
        public static bool IsFrozen(EnemyHealthSystem enemy) => enemy != null && enemy.Frozen;
        public static bool IsHeld(EnemyHealthSystem enemy) => enemy != null && (enemy.Frozen || enemy.Held);
        public static float ThawTimeOf(EnemyHealthSystem enemy) => 10f;
    }
    public static class FrozenCloud {
        public static bool IsFrozen(DeathCloud cloud) => cloud.Frozen;
        public static void Freeze(DeathCloud cloud, float until) { cloud.Frozen = true; }
    }
    public static class FreezeAbility { public static bool HasLineOfSight(UnityEngine.Vector2 a, UnityEngine.Vector2 b) => true; }
    public static class SwallowedTarget {
        public static bool IsSwallowed(UnityEngine.GameObject go) => go != null && go.GetComponent<EnemyHealthSystem>().Swallowed;
    }
    namespace Weapons { public static class DazedEnemy { public static bool IsDazed(EnemyHealthSystem enemy) => enemy != null && enemy.Dazed; } }
    public static class Poison {
        private static Dictionary<UnityEngine.GameObject, float> Exposure(DeathCloud cloud) => cloud.inRange;
        POISON
        public static bool Contact(DeathCloud cloud, UnityEngine.Collider2D other) => Prefix(cloud, other);
    }
    public static class Movement {
        MOVEMENT
        public static bool CanMove(DeathCloud cloud) => Prefix(cloud);
    }
    public static class BlackHolePoisonTests {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        public static int Run() {
            var caster = new SpiderHealthSystem();
            var other = new SpiderHealthSystem();
            var casterBody = new UnityEngine.GameObject { Component = caster };
            var otherBody = new UnityEngine.GameObject { Component = other };
            var casterCollider = new UnityEngine.Collider2D { gameObject = casterBody };
            var otherCollider = new UnityEngine.Collider2D { gameObject = otherBody };
            var cloud = new DeathCloud();
            cloud.inRange[casterBody] = -100f;
            cloud.inRange[otherBody] = -100f;
            Check(Poison.Contact(cloud, casterCollider), "Poison works before casting");
            var field = BlackHoleField.Open(caster);
            Check(!Poison.Contact(cloud, casterCollider), "Caster protected from an existing poison exposure");
            Check(!cloud.inRange.ContainsKey(casterBody), "Old exposure must not survive protection");
            Check(Poison.Contact(cloud, otherCollider) && cloud.inRange.ContainsKey(otherBody), "Other players remain vulnerable");
            Check(!Poison.Contact(new DeathCloud(), casterCollider), "Protection also covers newly spawned clouds");
            Check(Poison.Contact(cloud, new UnityEngine.Collider2D { gameObject = new UnityEngine.GameObject() }), "Non-player contacts untouched");
            caster.dead = true;
            Check(!BlackHoleField.ProtectsFromPoison(caster), "Dead caster protection ends");
            caster.dead = false; caster.astralDead = true;
            Check(!BlackHoleField.ProtectsFromPoison(caster), "Astral death protection ends");
            caster.astralDead = false; field.isActiveAndEnabled = false;
            Check(!BlackHoleField.ProtectsFromPoison(caster), "Disabled field protection ends");
            field.isActiveAndEnabled = true;
            var overlap = BlackHoleField.Open(caster);
            field.Expire();
            Check(BlackHoleField.ProtectsFromPoison(caster), "Overlapping field retains protection");
            overlap.Expire();
            Check(Poison.Contact(cloud, casterCollider) && !cloud.inRange.ContainsKey(casterBody), "Poison resumes with fresh exposure after expiry");
            Check(!BlackHoleField.ProtectsFromPoison(new SpiderHealthSystem()), "Respawned body inherits no protection");
            Check(!BlackHoleField.ProtectsFromPoison(null), "Null body has no protection");
            var shaman = new EnemyHealthSystem();
            cloud.origin = new UnityEngine.Transform { Enemy = shaman };
            Check(Movement.CanMove(cloud), "Ordinary shaman cloud still chases");
            shaman.Held = true; cloud.rb2d.velocity = new UnityEngine.Vector2 { x = 9f };
            Check(!Movement.CanMove(cloud) && cloud.rb2d.velocity.x == 0f, "Pulled shaman cannot drag its cloud");
            Check(!cloud.Frozen, "Black Hole does not apply Freeze Time cosmetics");
            shaman.Held = false; shaman.Swallowed = true;
            Check(!Movement.CanMove(cloud), "Swallow animation cannot drag cloud");
            shaman.Swallowed = false;
            Check(Movement.CanMove(cloud), "Released shaman resumes cloud movement");
            shaman.Frozen = true;
            Check(!Movement.CanMove(cloud) && cloud.Frozen, "Freeze Time still freezes cloud");
            shaman.Frozen = false; cloud.Frozen = false; shaman.Dazed = true;
            Check(!Movement.CanMove(cloud), "Daze still stops cloud");
            cloud.origin = null;
            Check(Movement.CanMove(cloud), "Missing shaman leaves ordinary movement intact");
            return checks;
        }
    }
}
'@
$code = $stubs.Replace('PROTECTION', $protection).Replace('POISON', $poison).Replace('MOVEMENT', $movement)
Add-Type -TypeDefinition $code -WarningAction Stop
$checks = [SpiderSurge.BlackHolePoisonTests]::Run()
Write-Output "$checks Black Hole poison regression checks passed."
