using HarmonyLib;
using UnityEngine;

namespace SpiderSurge
{
    // Marks a friendly wasp with the Neural Backup cast that summoned it, so its kills charge the meter for that cast
    public class FriendlyWaspOwner : MonoBehaviour
    {
        public int PlayerId = -1;
        public int CastId;
    }

    // Marks a weapon a Care Package dropped until someone picks it up
    public class CarePackageDrop : MonoBehaviour
    {
        public int PlayerId = -1;
        public int CastId;
    }

    // The stinger runs on the host only; an enemy that was alive before the hit and dead after it was killed by the wasp
    [HarmonyPatch(typeof(FriendlyWaspStinger), nameof(FriendlyWaspStinger.OnCollisionEnter2D))]
    public class FriendlyWaspStinger_OnCollisionEnter2D_MeterPatch
    {
        [HarmonyPrefix]
        public static void Prefix(FriendlyWaspStinger __instance, Collision2D other, out EnemyHealthSystem __state)
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
            if (owner == null || owner.PlayerId < 0) return;
            SurgeMeter.Add(MeterSource.NeuralBackup, owner.PlayerId, owner.CastId, Consts.Values.Meter.NeuralBackupChargePerEvent, Consts.Values.Meter.NeuralBackupCapPerCast);
        }
    }
}
