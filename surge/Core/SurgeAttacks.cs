using System;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // Surge's view of the core attack scope: the damaging source achievements check, the Pulse ultimate's protected caster and Overkill's last wasp
    internal static class SurgeAttacks
    {
        internal struct Slot
        {
            public EnemyHealthSystem LastWasp;
            public PlayerController ThermalCaster;
            public SpiderHealthSystem Protected;
            public bool? BigGrenade;
        }

        private static readonly AttackView<Slot> View = new AttackView<Slot>(Open);

        // The actual damaging source, so shields, unrelated deaths and chained explosions cannot count as a blade or stinger kill
        public static void Register()
        {
            View.Register(AccessTools.Method(typeof(ForceField), "Damage"));
            View.Register(AccessTools.Method(typeof(WaspStinger), "OnCollisionEnter2D"));
            View.Register(AccessTools.Method(typeof(FriendlyWaspStinger), "OnCollisionEnter2D"));
            View.Register(AccessTools.Method(typeof(RollerStrut), "OnCollisionEnter2D"));
            View.Register(AccessTools.Method(typeof(MeleeHazard), "OnCollisionEnter2D"));
            View.Register(AccessTools.Method(typeof(SawDisc), "TryDamage"));
            View.Register(AccessTools.Method(typeof(RailShot), "ProcessHit"));
            View.Register(AccessTools.Method(typeof(DeathCloud), "OnTriggerStay2D"));
            View.Register(AccessTools.Method(typeof(WhispBrain), "ProcessCollision"));
            View.Register(AccessTools.Method(typeof(Explosion), "KnockBack"));
            View.Register(AccessTools.Method(typeof(SpiderHealthSystem), "AfterlifeExplosion"));
            View.Register(AccessTools.Method(typeof(PulseAbility), "ApplyPulsePhysics"));
        }

        public static void Unregister() => View.Unregister();

        internal static Component Source => View.Source;

        internal static Explosion CurrentExplosion
        {
            get
            {
                int index = View.Innermost<Explosion>();
                return index >= 0 ? (Explosion)View.SourceAt(index) : null;
            }
        }

        // The pulse ultimate's own explosion never hurts its caster
        internal static SpiderHealthSystem ProtectedSpider
        {
            get
            {
                int index = View.Innermost<Explosion>();
                return index >= 0 ? View.Slot(index).Protected : null;
            }
        }

        internal static EnemyHealthSystem LastWasp
        {
            get
            {
                int index = View.Innermost<PulseAbility>();
                return index >= 0 ? View.Slot(index).LastWasp : null;
            }
        }

        internal static PlayerController ThermalCaster
        {
            get
            {
                int index = View.Innermost<PulseAbility>();
                return index >= 0 ? View.Slot(index).ThermalCaster : null;
            }
        }

        // Whether the running knockback is a Big Grenade blast; its name is read at most once per knockback
        internal static bool IsBigGrenadeBlast(out Explosion explosion)
        {
            int index = View.Innermost<Explosion>();
            if (index < 0)
            {
                explosion = null;
                return false;
            }
            explosion = (Explosion)View.SourceAt(index);
            ref Slot slot = ref View.Slot(index);
            return slot.BigGrenade ??= explosion.name.StartsWith(AchievementTracker.BigGrenadeExplosion, StringComparison.Ordinal);
        }

        private static bool Open(ref Slot slot, Component source, object info)
        {
            if (source is Explosion explosion)
            {
                slot.Protected = explosion.GetComponent<PulseExplosionCaster>()?.Health;
            }
            else if (source is PulseAbility pulse && PulseAbility.IsDeadly(pulse))
            {
                try
                {
                    slot.LastWasp = AbilityAchievementTracker.LastPlainWasp();
                    slot.ThermalCaster = PulseAbility.CasterOf(pulse);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Achievement tracking failed (Overkill): {ex.Message}");
                }
            }
            return true;
        }
    }
}
