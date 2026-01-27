using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class InfiniteAmmoAbility : BaseAbility
    {
        public static Dictionary<PlayerInput, InfiniteAmmoAbility> playerInfiniteAmmo = new Dictionary<PlayerInput, InfiniteAmmoAbility>();

        public override string PerkName => "infiniteAmmoAbility";

        public override float BaseDuration => 5f;
        public override float DurationPerPerkLevel => 5f;

        public override float BaseCooldown => 40f;
        public override float CooldownPerPerkLevel => 5f;

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerInfiniteAmmo[playerInput] = this;
            }
        }

        protected override void OnActivate()
        {
            // TODO: Implement infinite ammo logic
            Logger.LogInfo($"Infinite Ammo ACTIVATED for player {playerInput.playerIndex}");
        }

        protected override void OnDeactivate()
        {
            // TODO: Implement infinite ammo deactivation logic
            Logger.LogInfo($"Infinite Ammo DEACTIVATED for player {playerInput.playerIndex}");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (playerInput != null && playerInfiniteAmmo.ContainsKey(playerInput))
            {
                playerInfiniteAmmo.Remove(playerInput);
            }
        }
    }
}
