using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Values = SpiderSurge.Consts.Values.PoisonDart;

namespace SpiderSurge.Weapons
{
    // A spider hit by the Poison Dart stumbles in small random circles and can't jump, aim, shoot or swap weapons until it wears off.
    // The host decides the daze; the spider's owner steers it, since the owner drives its spider's movement
    public class DazedSpider : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<SpiderController, Vector2> MoveInput = AccessTools.FieldRefAccess<SpiderController, Vector2>("_movementInputLocal");

        private SpiderHealthSystem _health;
        private SpiderController _controller;
        private float _until;
        private DazeVfx _vfx;
        private readonly Wander _wander = new Wander();
        private float _nextInput;
        // The stick the player holds when the daze ends takes over again
        private Vector2 _heldMove;

        public float TimeLeft => Mathf.Max(0f, _until - Time.time);

        public static bool IsDazed(Component target) => target != null && target.GetComponentInParent<DazedSpider>()?.TimeLeft > 0f;

        public static bool Daze(SpiderHealthSystem health, float duration)
        {
            if (!Apply(health, duration)) return false;
            SurgeNet.SendEnemyFx(EnemyFx.SpiderDazed, health.rootObject.GetComponent<NetworkObject>(), duration);
            return true;
        }

        public static void ShowDazed(NetworkObject target, float duration) => Apply(target.GetComponentInChildren<SpiderHealthSystem>(), duration);

        private static bool Apply(SpiderHealthSystem health, float duration)
        {
            if (health == null || health.dead || health.astralDead || health.rootObject == null || duration <= 0f) return false;
            var dazed = health.rootObject.GetComponent<DazedSpider>();
            if (dazed != null)
            {
                dazed._until = Mathf.Max(dazed._until, Time.time + duration);
                return true;
            }
            dazed = health.rootObject.AddComponent<DazedSpider>();
            dazed._health = health;
            dazed._until = Time.time + duration;
            dazed._controller = dazed.GetComponent<SpiderController>();
            if (dazed._controller != null) dazed._heldMove = MoveInput(dazed._controller);
            // A held trigger lets go, as if the player released it
            var weapons = dazed.GetComponentInChildren<SpiderWeaponManager>();
            if (Net.IsAuthority && weapons != null && weapons.equippedWeapon != null) weapons.equippedWeapon.Use(false);
            dazed._vfx = DazeVfx.Attach(health.transform, 6f);
            return true;
        }

        internal static void HoldMove(Component target, Vector2 move)
        {
            var dazed = target.GetComponentInParent<DazedSpider>();
            if (dazed != null) dazed._heldMove = move;
        }

        private bool Steers => _controller != null && _controller.IsOwner && _controller.enabled && GameController.instance != null && GameController.instance.acceptingInputs;

        private void FixedUpdate()
        {
            Vector2 direction = _wander.Step(Time.fixedDeltaTime);
            if (!Steers || Time.time < _nextInput || FrozenSpider.IsFrozen(this)) return;
            _nextInput = Time.time + Values.SpiderInputInterval;
            _controller.SetMoveVector(direction * Values.SpiderWanderInput);
        }

        private void Update()
        {
            if (_health == null || _health.dead || _health.astralDead || TimeLeft <= 0f) Destroy(this);
        }

        private void OnDestroy()
        {
            _until = 0f;
            if (_vfx != null) _vfx.Release();
            _vfx = null;
            if (Steers) _controller.SetMoveVector(_heldMove);
        }
    }

    [HarmonyPatch]
    internal static class DazedSpider_Input_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string method in new[] { "OnJump", "EnqueueJumpServerRpc", "OnAim", "OnPoint" })
                yield return AccessTools.Method(typeof(SpiderController), method);
            foreach (string method in new[] { "OnFire", "OnFireServerRpc", "OnEquip", "OnEquipServerRpc", "OnPoint", "OnPointServerRpc", "OnAim", "OnAimServerRpc" })
                yield return AccessTools.Method(typeof(SpiderWeaponManager), method);
        }

        private static bool Prefix(Component __instance) => !DazedSpider.IsDazed(__instance);
    }

    // The player's stick doesn't steer a dazed spider; what they hold is kept for when it wears off
    [HarmonyPatch(typeof(SpiderController), "OnMove")]
    internal static class DazedSpider_Move_Patch
    {
        private static bool Prefix(SpiderController __instance, InputValue input)
        {
            if (!DazedSpider.IsDazed(__instance)) return true;
            DazedSpider.HoldMove(__instance, input.Get<Vector2>());
            return false;
        }
    }
}
