using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    // Marks a weapon a Care Package dropped until it makes its first kill
    public class CarePackageDrop : MonoBehaviour
    {
        public int PlayerId = -1;
    }

    // Marks a weapon recalled from the Pocket Dimension until it makes its first kill
    public class PocketDimensionWeapon : MonoBehaviour
    {
        public int PlayerId = -1;

        public static void Mark(Weapon weapon, int playerId)
        {
            PocketDimensionWeapon marker = weapon.GetComponent<PocketDimensionWeapon>();
            if (marker == null) marker = weapon.gameObject.AddComponent<PocketDimensionWeapon>();
            marker.PlayerId = playerId;
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
                PocketDimensionWeapon recalled = weapon.GetComponent<PocketDimensionWeapon>();
                if (recalled != null)
                {
                    SurgeMeter.Add(MeterSource.PocketDimension, recalled.PlayerId, Consts.Values.Meter.PocketDimensionChargePerWeapon);
                    Object.Destroy(recalled);
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
