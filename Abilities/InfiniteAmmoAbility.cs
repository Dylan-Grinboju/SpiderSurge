using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
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

        private SpiderWeaponManager weaponManager;
        private float storedMaxAmmo = 0f;
        private static FieldInfo networkAmmoField;

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerInfiniteAmmo[playerInput] = this;
            }

            // Cache reflection field for networkAmmo
            if (networkAmmoField == null)
            {
                networkAmmoField = typeof(Weapon).GetField("networkAmmo",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (networkAmmoField == null)
                {
                    Logger.LogError("InfiniteAmmoAbility: Could not find networkAmmo field!");
                }
            }
        }

        protected override void Start()
        {
            base.Start();
            // SpiderWeaponManager is a child component, not a parent
            weaponManager = GetComponentInChildren<SpiderWeaponManager>();
            if (weaponManager == null && playerController != null)
            {
                // Try from the player controller's health system
                weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
            }
        }

        protected override void Update()
        {
            base.Update();

            if (weaponManager == null)
            {
                weaponManager = GetComponentInChildren<SpiderWeaponManager>();
                if (weaponManager == null && playerController != null)
                {
                    weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
                }
            }

            if (isActive && weaponManager != null && weaponManager.equippedWeapon != null)
            {
                var weapon = weaponManager.equippedWeapon;

                if (storedMaxAmmo <= 0f || weapon.maxAmmo > storedMaxAmmo)
                {
                    storedMaxAmmo = weapon.maxAmmo;
                }

                if (weapon.ammo < storedMaxAmmo)
                {
                    SetWeaponAmmo(weapon, storedMaxAmmo);
                }
            }
        }

        private void SetWeaponAmmo(Weapon weapon, float value)
        {
            if (weapon == null) return;

            // First try using the normal setter (works if we're host)
            weapon.ammo = value;

            // If that didn't work (we're not host), use reflection
            if (weapon.ammo < value && networkAmmoField != null)
            {
                try
                {
                    var networkAmmo = networkAmmoField.GetValue(weapon) as NetworkVariable<float>;
                    if (networkAmmo != null)
                    {
                        networkAmmo.Value = value;
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"InfiniteAmmoAbility: Failed to set ammo via reflection: {ex.Message}");
                }
            }
        }

        protected override void OnActivate()
        {
            storedMaxAmmo = 0f;

            if (weaponManager != null && weaponManager.equippedWeapon != null)
            {
                storedMaxAmmo = weaponManager.equippedWeapon.maxAmmo;
                float previousAmmo = weaponManager.equippedWeapon.ammo;

                SetWeaponAmmo(weaponManager.equippedWeapon, storedMaxAmmo);
            }
            else
            {
                Logger.LogWarning($"Infinite Ammo ACTIVATED for player {playerInput.playerIndex} - no weapon equipped");
            }
        }

        protected override void OnDeactivate()
        {
            storedMaxAmmo = 0f;
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
