using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using Values = SpiderSurge.Consts.Values.Bayonet;

namespace SpiderSurge.Weapons
{
    // A Baby Shotgun with a short Particle Blade strapped beside its barrels, spawning as a weapon of its own
    public static class Bayonet
    {
        private const string SpriteResource = "SpiderSurge.BayonetSprite";

        public static GameObject Prefab { get; private set; }
        private static bool _failed;

        public static void EnsureCreated()
        {
            if (Prefab != null || _failed) return;

            var shotgun = CustomWeapons.FindPrefab<Shotgun>("Mini Shotgun");
            var blade = CustomWeapons.FindPrefab<ParticleBlade>("Particle Blade");
            if (shotgun == null || blade == null) return;

            Sprite sprite = CustomWeapons.LoadSprite(SpriteResource, Values.Name, shotgun.GetComponent<SpriteRenderer>().sprite, Values.SpritePixelsPerUnit);
            if (sprite == null)
            {
                _failed = true;
                return;
            }

            Prefab = Build(shotgun, blade, sprite);
        }

        public static bool Is(Weapon weapon) => weapon != null && weapon.serializationWeaponName == Values.Id;

        private static GameObject Build(Shotgun shotgun, ParticleBlade blade, Sprite sprite)
        {
            GameObject bayonet = Object.Instantiate(shotgun.gameObject, CustomWeapons.Holder);
            bayonet.name = Values.Name;
            var gun = bayonet.GetComponent<Shotgun>();
            gun.serializationWeaponName = Values.Id;
            // A gun and a blade, so More Guns and More Particles both raise its spawn rate
            gun.type = gun.type.Union(blade.type).ToList();
            gun.usedInCustomTiers = false;
            bayonet.GetComponent<SpriteRenderer>().sprite = sprite;

            // The blade parts come from a whole copy of the Particle Blade, so their references to each other point at the copies
            var partsHolder = new GameObject("SpiderSurgeBladeParts");
            partsHolder.SetActive(false);
            GameObject parts = Object.Instantiate(blade.gameObject, partsHolder.transform);
            ParticleBlade partsBlade = parts.GetComponent<ParticleBlade>();
            ParticleField field = partsBlade.particleField;
            ForceField forceField = field.forceField;

            var sounds = new GameObject("BladeSounds");
            sounds.transform.SetParent(bayonet.transform, false);
            foreach (AudioSource sound in forceField.collisionSounds.Append(forceField.killSound).Append(partsBlade.beamSound).Append(partsBlade.offSound))
            {
                sound.transform.SetParent(sounds.transform, false);
            }
            // The blade hum follows its weapon's spin and speed
            foreach (var volume in sounds.GetComponentsInChildren<RotationVolume>(true))
            {
                volume.rb2D = bayonet.GetComponent<Rigidbody2D>();
            }
            forceField.particleCollision.transform.SetParent(bayonet.transform, false);

            field.transform.SetParent(bayonet.transform, false);
            field.transform.localPosition = Values.BladeOrigin;
            field.transform.localRotation = Quaternion.identity;
            var fieldCollider = field.GetComponent<BoxCollider2D>();
            // Off until the blade lights; BayonetBlade keeps the gun's balance when it toggles
            fieldCollider.enabled = false;

            var bladeHandler = bayonet.AddComponent<BayonetBlade>();
            bladeHandler.field = field;
            bladeHandler.beamSound = partsBlade.beamSound;
            bladeHandler.offSound = partsBlade.offSound;
            bladeHandler.durability = blade.maxAmmo;

            Object.Destroy(partsHolder);
            CustomPrefabNet.RegisterWeapon(bayonet, Values.Name);
            return bayonet;
        }
    }

    // Weapon.FixedUpdate dissolves a weapon without ammo; a Bayonet out of shells stays while its blade lasts
    [HarmonyPatch(typeof(Weapon), nameof(Weapon.FixedUpdate))]
    internal static class Bayonet_Tick_Patch
    {
        internal static Weapon Ticking;

        [HarmonyPrefix]
        private static void Prefix(Weapon __instance) => Ticking = __instance;

        [HarmonyFinalizer]
        private static void Finalizer() => Ticking = null;
    }

    [HarmonyPatch(typeof(Weapon), nameof(Weapon.Disintegrate))]
    internal static class Bayonet_Disintegrate_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(Weapon __instance)
        {
            return !ReferenceEquals(__instance, Bayonet_Tick_Patch.Ticking) || !BayonetBlade.KeepsWeapon(__instance);
        }
    }

    // The shotgun's recoil reaches it through Weapon.Impact, which isn't a hit
    [HarmonyPatch(typeof(Shotgun), "ShootClientRpc")]
    internal static class Bayonet_Recoil_Patch
    {
        internal static bool Firing;

        [HarmonyPrefix]
        private static void Prefix() => Firing = true;

        [HarmonyFinalizer]
        private static void Finalizer() => Firing = false;
    }

    // Hits wear the blade down as they wear down a Particle Blade
    [HarmonyPatch]
    internal static class Bayonet_Wear_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Weapon), nameof(Weapon.Impact));
            yield return AccessTools.Method(typeof(Weapon), nameof(Weapon.Damage));
        }

        [HarmonyPrefix]
        private static void Prefix(Weapon __instance, MethodBase __originalMethod, object[] __args, bool __runOriginal)
        {
            if (!__runOriginal || Bayonet_Recoil_Patch.Firing || !Net.IsAuthority || !Bayonet.Is(__instance)) return;
            if (__originalMethod.Name == nameof(Weapon.Impact) && !(bool)__args[3]) return;
            __instance.GetComponent<BayonetBlade>()?.Wear();
        }
    }

    // The game's overload expects a Particle Blade behind every energy field; a held Bayonet turns the ball as one would.
    // Runs after the core prefix, which records who turned it
    [HarmonyPatch(typeof(EnergyBall), "Overload", new[] { typeof(GameObject) })]
    internal static class Bayonet_Overload_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(EnergyBall __instance, GameObject go)
        {
            BayonetBlade blade = go != null && go.GetComponent<ParticleBlade>() == null ? go.GetComponent<BayonetBlade>() : null;
            if (blade == null) return true;
            if (go.GetComponent<Weapon>().owner != null) __instance.ChangeOwner(__instance.gameObject, blade.field.GetColor());
            return false;
        }
    }
}
