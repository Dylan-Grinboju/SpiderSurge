using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge
{
    public class FrozenSpider : MonoBehaviour
    {
        private SpiderHealthSystem _health;
        private SpiderController _controller;
        private SpiderWeaponManager _weapons;
        private Weapon _weapon;
        private float _weaponHeldAt;
        private float? _rayChargeLeft;
        private bool _chargeHeld;
        private bool _weaponEnabled;
        private static readonly AccessTools.FieldRef<DeathRay, float> RayChargeStart = AccessTools.FieldRefAccess<DeathRay, float>("_chargeStartTime");
        private static readonly AccessTools.FieldRef<KhepriStaff, float> StaffReadyTime = AccessTools.FieldRefAccess<KhepriStaff, float>("_readyTime");
        private float _thawTime;
        private bool _controllerEnabled;
        private bool _held;
        private FrostAura _frost;
        private readonly List<BodyState> _bodies = new List<BodyState>();
        private readonly List<BodyState> _weaponBodies = new List<BodyState>();
        private static readonly PropertyInfo AnimatorSpeed = AccessTools.Property(AccessTools.TypeByName("UnityEngine.Animator"), "speed");
        private readonly Dictionary<Component, float> _animators = new Dictionary<Component, float>();

        private struct BodyState
        {
            public Rigidbody2D Body;
            public RigidbodyConstraints2D Constraints;
            public Vector2 Velocity;
            public float AngularVelocity;
        }

        public float TimeLeft => Mathf.Max(0f, _thawTime - Time.time);
        public static bool IsFrozen(Component target) => target != null && target.GetComponentInParent<FrozenSpider>()?.TimeLeft > 0f;

        // The host chooses every other living spider in the same unobstructed ring, including teammates.
        public static bool FreezeAround(Vector2 origin, float range, float duration, int casterId)
        {
            bool any = false;
            if (Players.All == null) return false;
            foreach (PlayerController player in Players.All)
            {
                if (player == null || player.playerID.Value == casterId) continue;
                SpiderHealthSystem health = player.spiderHealthSystem;
                if (health == null || health.dead || health.astralDead || health.rootObject == null || SwallowedTarget.IsSwallowed(health.rootObject)) continue;
                if (Vector2.Distance(origin, health.transform.position) > range || !FreezeAbility.HasLineOfSight(origin, health.transform.position)) continue;
                Apply(health, duration);
                SurgeNet.SendEnemyFx(EnemyFx.SpiderFrozen, health.rootObject.GetComponent<NetworkObject>(), duration);
                any = true;
            }
            return any;
        }

        // Owners also hold their local physics and input; the host remains authoritative about who is frozen.
        public static void ShowFrozen(NetworkObject target, float duration) => Apply(target.GetComponentInChildren<SpiderHealthSystem>(), duration);

        private static void Apply(SpiderHealthSystem health, float duration)
        {
            if (health == null || health.dead || health.astralDead || health.rootObject == null || duration <= 0f) return;
            var frozen = health.rootObject.GetComponent<FrozenSpider>();
            if (frozen != null)
            {
                frozen._thawTime = Mathf.Max(frozen._thawTime, Time.time + duration);
                return;
            }
            frozen = health.rootObject.AddComponent<FrozenSpider>();
            frozen._health = health;
            frozen._held = true;
            frozen._thawTime = Time.time + duration;
            frozen._controller = frozen.GetComponent<SpiderController>();
            frozen._controllerEnabled = frozen._controller != null && frozen._controller.enabled;
            if (frozen._controller != null) frozen._controller.enabled = false;
            frozen._weapons = frozen.GetComponentInChildren<SpiderWeaponManager>();
            if (frozen._weapons != null)
            {
                frozen._weapon = frozen._weapons.equippedWeapon;
                if (Net.IsAuthority)
                {
                    frozen._weapons.tryingToPickupWeapon.Value = false;
                    if (frozen._weapon is Railvolver railvolver) railvolver.Use(false);
                }
                frozen.HoldCharge();
                if (frozen._weapon != null) Hold(frozen._weapon.gameObject, frozen._weaponBodies);
            }
            Hold(frozen.gameObject, frozen._bodies);
            foreach (Component animator in frozen.GetComponentsInChildren(AnimatorSpeed.DeclaringType))
            {
                frozen._animators[animator] = (float)AnimatorSpeed.GetValue(animator, null);
                AnimatorSpeed.SetValue(animator, 0f, null);
            }
            frozen._frost = FrostAura.Attach(health.transform, 10f);
        }

        private static void Hold(GameObject target, List<BodyState> states)
        {
            foreach (Rigidbody2D body in target.GetComponentsInChildren<Rigidbody2D>())
            {
                states.Add(new BodyState { Body = body, Constraints = body.constraints, Velocity = body.velocity, AngularVelocity = body.angularVelocity });
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
            }
        }

        private void Update()
        {
            if (_weapons == null || _weapons.equippedWeapon != _weapon)
            {
                RestoreCharge();
                Restore(_weaponBodies);
            }
            if (_health == null || _health.dead || _health.astralDead || TimeLeft <= 0f || !SurgeGameModeManager.IsSurgeRunActive)
            {
                Thaw();
                Destroy(this);
            }
        }

        // Charged shots resume their remaining windup after the holder thaws, on every peer.
        private void HoldCharge()
        {
            if (!(_weapon is DeathRay) && !(_weapon is KhepriStaff)) return;
            _chargeHeld = true;
            _weaponHeldAt = Time.time;
            _weaponEnabled = _weapon.enabled;
            _weapon.enabled = false;
            if (_weapon is DeathRay ray && ray.IsInvoking("Fire"))
            {
                _rayChargeLeft = Mathf.Max(0f, RayChargeStart(ray) + ray.chargeTime - Time.time);
                ray.CancelInvoke("Fire");
            }
        }

        private void RestoreCharge()
        {
            if (!_chargeHeld) return;
            _chargeHeld = false;
            if (_weapon == null) return;
            float held = Time.time - _weaponHeldAt;
            if (_weapon is DeathRay ray && _rayChargeLeft.HasValue)
            {
                RayChargeStart(ray) += held;
                ray.Invoke("Fire", _rayChargeLeft.Value);
                _rayChargeLeft = null;
            }
            else if (_weapon is KhepriStaff staff) StaffReadyTime(staff) += held;
            _weapon.enabled = _weaponEnabled;
        }

        private static void Restore(List<BodyState> states, bool momentum = true)
        {
            foreach (BodyState state in states)
            {
                if (state.Body == null) continue;
                state.Body.constraints = state.Constraints;
                if (momentum)
                {
                    state.Body.velocity = state.Velocity;
                    state.Body.angularVelocity = state.AngularVelocity;
                }
            }
            states.Clear();
        }

        private void Thaw()
        {
            if (!_held) return;
            _held = false;
            _thawTime = 0f;
            bool alive = _health != null && !_health.dead && !_health.astralDead;
            Restore(_bodies, alive);
            RestoreCharge();
            Restore(_weaponBodies);
            if (_health != null && !_health.dead && _controller != null) _controller.enabled = _controllerEnabled;
            foreach (var pair in _animators) if (pair.Key != null) AnimatorSpeed.SetValue(pair.Key, pair.Value, null);
            _animators.Clear();
            if (_frost != null)
            {
                _frost.Release(alive);
                if (alive) FrozenEnemy.PlayThawSound();
                _frost = null;
            }
        }

        private void OnDisable() => Thaw();
    }

    [HarmonyPatch]
    internal static class FrozenSpiderInputPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SpiderController), "OnJump");
            yield return AccessTools.Method(typeof(SpiderController), "EnqueueJumpServerRpc");
            yield return AccessTools.Method(typeof(WeaponManager), "FixedUpdate");
            foreach (string method in new[] { "OnFire", "OnFireServerRpc", "OnEquip", "OnEquipServerRpc", "OnPoint", "OnPointServerRpc", "OnAim", "OnAimServerRpc" })
                yield return AccessTools.Method(typeof(SpiderWeaponManager), method);
        }

        private static bool Prefix(Component __instance) => !FrozenSpider.IsFrozen(__instance);
    }
}
