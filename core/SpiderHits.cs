using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SpiderSurge.Common
{
    // Hits on a spider: the gates decide in the order the parts add them whether a hit lands, and the observers then see only
    // the hits that land, after every other mod's prefix had its say
    internal static class SpiderHits
    {
        public delegate bool Gate(SpiderHealthSystem spider, Vector3 force, Vector3 point, bool stun);
        public delegate void Observer(SpiderHealthSystem spider, Vector3 force, Vector3 point, bool stun);

        public static readonly Pipeline Damage = new Pipeline();
        public static readonly Pipeline Impact = new Pipeline();

        internal sealed class Pipeline
        {
            private readonly List<Gate> _gates = new List<Gate>();
            private readonly List<Observer> _observers = new List<Observer>();

            public void Gate(Gate gate, bool on)
            {
                if (on) _gates.Add(gate); else _gates.Remove(gate);
            }

            public void Observe(Observer observer, bool on)
            {
                if (on) _observers.Add(observer); else _observers.Remove(observer);
            }

            internal bool Allows(SpiderHealthSystem spider, Vector3 force, Vector3 point, bool stun)
            {
                foreach (Gate gate in _gates)
                {
                    if (!gate(spider, force, point, stun)) return false;
                }
                return true;
            }

            internal void Landed(SpiderHealthSystem spider, Vector3 force, Vector3 point, bool stun)
            {
                foreach (Observer observer in _observers) observer(spider, force, point, stun);
            }
        }

        [HarmonyPatch(typeof(SpiderHealthSystem), nameof(SpiderHealthSystem.Damage), new[] { typeof(Vector3), typeof(Vector3), typeof(bool) })]
        private static class DamageGatesPatch
        {
            [HarmonyPriority(Priority.First)]
            static bool Prefix(SpiderHealthSystem __instance, Vector3 force, Vector3 point, bool stun) => Damage.Allows(__instance, force, point, stun);
        }

        [HarmonyPatch(typeof(SpiderHealthSystem), nameof(SpiderHealthSystem.Damage), new[] { typeof(Vector3), typeof(Vector3), typeof(bool) })]
        private static class DamageObserversPatch
        {
            [HarmonyPriority(Priority.Last)]
            static void Prefix(SpiderHealthSystem __instance, Vector3 force, Vector3 point, bool stun, bool __runOriginal)
            {
                if (__runOriginal) Damage.Landed(__instance, force, point, stun);
            }
        }

        [HarmonyPatch(typeof(SpiderHealthSystem), nameof(SpiderHealthSystem.Impact), new[] { typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool) })]
        private static class ImpactGatesPatch
        {
            [HarmonyPriority(Priority.First)]
            static bool Prefix(SpiderHealthSystem __instance, Vector3 force, Vector3 point, bool stun) => Impact.Allows(__instance, force, point, stun);
        }

        [HarmonyPatch(typeof(SpiderHealthSystem), nameof(SpiderHealthSystem.Impact), new[] { typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool) })]
        private static class ImpactObserversPatch
        {
            [HarmonyPriority(Priority.Last)]
            static void Prefix(SpiderHealthSystem __instance, Vector3 force, Vector3 point, bool stun, bool __runOriginal)
            {
                if (__runOriginal) Impact.Landed(__instance, force, point, stun);
            }
        }
    }
}
