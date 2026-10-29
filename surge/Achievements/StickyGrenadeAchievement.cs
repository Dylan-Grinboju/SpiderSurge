using HarmonyLib;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge
{
    internal sealed class StickyGrenadeOrigin : MonoBehaviour
    {
        internal EnemyHealthSystem TriggeredBy;
        internal PlayerController Player;
    }

    internal static class StickyGrenadeAchievement
    {
        internal static void Triggered(Mine mine, EnemyHealthSystem enemy)
        {
            var origin = mine.GetComponent<StickyGrenadeOrigin>() ?? mine.gameObject.AddComponent<StickyGrenadeOrigin>();
            origin.TriggeredBy = enemy;
        }

        // Carry the actual triggering enemy onto this mine's blast, excluding player attacks and chained explosions.
        internal static void OnExplosionCreated(Explosion explosion, NetworkBehaviour source)
        {
            if (!Net.IsAuthority || !(source is Mine mine)) return;
            Component attack = SurgeAttacks.Source;
            if (StatsMod.HitLogic.CurrentAttacker != null) return;
            EnemyHealthSystem enemy = attack != null ? AbilityAchievementTracker.EnemyOf(attack)
                : mine.GetComponent<StickyGrenadeOrigin>()?.TriggeredBy;
            if (enemy == null || enemy.IsDead() || enemy.GetComponentInChildren<FriendlyWaspStinger>(true) != null) return;
            var origin = explosion.gameObject.AddComponent<StickyGrenadeOrigin>();
            origin.TriggeredBy = enemy;
            origin.Player = Owners.TryGet(mine.gameObject, out Owners.Record owner) && owner.Armer != null
                ? owner.Armer : Owners.PlayerOf(mine);
        }

        internal static void EnemyKilled(EnemyHealthSystem enemy, Explosion explosion)
        {
            var origin = explosion.GetComponent<StickyGrenadeOrigin>();
            if (origin != null && origin.TriggeredBy == enemy)
                SurgeAchievements.Unlock(SurgeAchievements.WasntMe, origin.Player);
        }
    }

    [HarmonyPatch(typeof(Mine), "OnTriggerStay2D")]
    internal static class StickyGrenadeTriggerAchievementPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Collider2D other, bool ____triggered, out EnemyHealthSystem __state)
        {
            __state = Net.IsAuthority && !____triggered ? other.GetComponentInParent<EnemyHealthSystem>() : null;
        }

        [HarmonyPostfix]
        private static void Postfix(Mine __instance, bool ____triggered, EnemyHealthSystem __state)
        {
            if (__state != null && ____triggered)
                AchievementTracker.Run("Wasn't Me", static trigger => StickyGrenadeAchievement.Triggered(trigger.mine, trigger.enemy), (mine: __instance, enemy: __state));
        }
    }
}
