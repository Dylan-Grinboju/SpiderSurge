using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class ShieldAbility : BaseAbility
    {
        public static Dictionary<PlayerInput, ShieldAbility> playerShields = new Dictionary<PlayerInput, ShieldAbility>();

        public override string PerkName => "shieldAbility";
        
        public override float BaseDuration => 1f;
        public override float DurationPerPerkLevel => 1f;
        
        public override float BaseCooldown => 30f;
        public override float CooldownPerPerkLevel => 10f;

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerShields[playerInput] = this;
            }
        }

        protected override void OnActivate()
        {
            if (spiderHealthSystem == null) return;

            if (spiderHealthSystem.HasShield())
            {
                Logger.LogInfo($"Player {playerInput.playerIndex} already has shield active");
                return;
            }

            spiderHealthSystem.EnableShield();
        }

        protected override void OnDeactivate()
        {
            if (spiderHealthSystem != null)
            {
                DestroyShield();
                spiderHealthSystem.DisableShield();
            }
        }

        private void LateUpdate()
        {
            if (isActive && spiderHealthSystem != null && !spiderHealthSystem.HasShield())
            {
                isActive = false;
                Logger.LogInfo($"ShieldAbility was broken by damage for player {playerInput.playerIndex}!");

                if (durationCoroutine != null)
                {
                    StopCoroutine(durationCoroutine);
                    durationCoroutine = null;
                }

                StartCooldown();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (playerInput != null && playerShields.ContainsKey(playerInput))
            {
                playerShields.Remove(playerInput);
            }
        }

        private void DestroyShield()
        {
            try
            {
                var breakShieldMethod = typeof(SpiderHealthSystem).GetMethod("BreakShieldClientRpc",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (breakShieldMethod != null)
                {
                    breakShieldMethod.Invoke(spiderHealthSystem, null);
                }
                else
                {
                    Logger.LogWarning($"Could not find BreakShieldClientRpc method for player {playerInput.playerIndex}");
                }
            }
            catch (System.Exception e)
            {
                Logger.LogError($"Failed to trigger shield explosion for player {playerInput.playerIndex}: {e.Message}");
            }
        }
    }
}