using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    // The weapons a Care Package dropped and the ones they launched, so every kill with them charges the player who cast it.
    // Kept by reference, which still matches once the weapon is destroyed: a grenade, rocket or disc can kill after its weapon is gone
    public static class CarePackageDrops
    {
        private static readonly Dictionary<Weapon, int> casters = new Dictionary<Weapon, int>();

        public static void Mark(Weapon weapon, int playerId)
        {
            if (weapon != null && playerId >= 0) casters[weapon] = playerId;
        }

        public static bool TryGetCaster(Weapon weapon, out int playerId)
        {
            playerId = -1;
            return !ReferenceEquals(weapon, null) && casters.TryGetValue(weapon, out playerId);
        }

        public static void OnWeaponLaunched(Weapon launched, Weapon launcher)
        {
            if (TryGetCaster(launcher, out int playerId)) Mark(launched, playerId);
        }

        // Dropped weapons are destroyed with the scene
        public static void Clear() => casters.Clear();
    }

    // Marks a friendly wasp with the player whose Neural Backup summoned it, so its kills charge the meter
    public class FriendlyWaspOwner : MonoBehaviour
    {
        public int PlayerId = -1;
    }

    // The stinger runs on the host only; an enemy that was alive before the hit and dead after it was killed by the wasp
    [HarmonyPatch(typeof(FriendlyWaspStinger), nameof(FriendlyWaspStinger.OnCollisionEnter2D))]
    public class FriendlyWaspStinger_OnCollisionEnter2D_MeterPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Collision2D other, out EnemyHealthSystem __state)
        {
            __state = null;
            if (!SurgeMeter.CanEarn || other == null || other.gameObject == null) return;
            EnemyHealthSystem enemy = other.gameObject.GetComponentInParent<EnemyHealthSystem>();
            if (enemy != null && !enemy.IsDead() && enemy.needToKillToCompleteLevel) __state = enemy;
        }

        [HarmonyPostfix]
        public static void Postfix(FriendlyWaspStinger __instance, EnemyHealthSystem __state)
        {
            if (__state == null || !__state.IsDead()) return;
            FriendlyWaspOwner owner = __instance.GetComponentInParent<FriendlyWaspOwner>();
            if (owner != null && owner.PlayerId >= 0) SurgeMeter.Add(MeterSource.NeuralBackup, owner.PlayerId, Consts.Values.Meter.NeuralBackupChargePerWaspKill);
        }
    }

    // Kills come from the Stats part's kill credit, which knows the player and weapon behind each one, so they only count while Stats runs
    public static class MeterKills
    {
        public static void OnEnemyKilled(EnemyHealthSystem enemy, PlayerController player, Weapon weapon, float hitTime)
        {
            if (!SurgeMeter.CanEarn || enemy == null || !enemy.needToKillToCompleteLevel) return;

            if (CarePackageDrops.TryGetCaster(weapon, out int caster))
            {
                SurgeMeter.Add(MeterSource.CarePackage, caster, Consts.Values.Meter.CarePackageChargePerKill);
            }

            if (player == null) return;
            int playerId = player.playerID.Value;
            if (AmmoAbility.playerAmmoAbilities.TryGetValue(playerId, out AmmoAbility clip) && clip != null && clip.AbilityActiveAt(hitTime))
            {
                SurgeMeter.Add(MeterSource.BottomlessClip, playerId, Consts.Values.Meter.BottomlessClipChargePerKill);
            }
            ImmuneAbility field = ImmuneAbility.GetByHealthSystem(Players.HealthOf(playerId));
            if (field != null && field.AbilityActiveAt(hitTime))
            {
                SurgeMeter.Add(MeterSource.NullField, playerId, Consts.Values.Meter.NullFieldChargePerKill);
            }
        }
    }
}
