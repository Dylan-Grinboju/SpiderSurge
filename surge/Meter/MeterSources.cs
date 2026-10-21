using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    // Marks a weapon a Care Package dropped until it makes its first kill
    public class CarePackageDrop : MonoBehaviour
    {
        public int PlayerId = -1;
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

            if (weapon != null)
            {
                CarePackageDrop drop = weapon.GetComponent<CarePackageDrop>();
                if (drop != null)
                {
                    SurgeMeter.Add(MeterSource.CarePackage, drop.PlayerId, Consts.Values.Meter.CarePackageChargePerWeapon);
                    Object.Destroy(drop);
                }
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
