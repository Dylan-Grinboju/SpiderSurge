using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;
using Values = SpiderSurge.Consts.Values.Bayonet;

namespace SpiderSurge.Weapons
{
    // A Baby Shotgun with a short Particle Blade strapped beside its barrels; it replaces a share of Baby Shotgun spawns
    public static class Bayonet
    {
        private const string SpriteResource = "SpiderSurge.BayonetSprite";

        public static GameObject Prefab { get; private set; }
        private static bool _failed;

        public static void EnsureCreated()
        {
            if (Prefab != null || _failed) return;

            var shotgun = FindPrefab<Shotgun>("Mini Shotgun");
            var blade = FindPrefab<ParticleBlade>("Particle Blade");
            if (shotgun == null || blade == null) return;

            Sprite sprite = LoadSprite(shotgun.GetComponent<SpriteRenderer>());
            if (sprite == null)
            {
                _failed = true;
                return;
            }

            Prefab = Build(shotgun, blade, sprite);
        }

        public static bool Is(Weapon weapon) => weapon != null && weapon.serializationWeaponName == Values.Id;

        public static GameObject MaybeReplace(GameObject weapon, float roll)
        {
            if (weapon == null || roll >= Values.SpawnShare || !Net.IsAuthority) return weapon;
            var source = weapon.GetComponent<Weapon>();
            if (source == null || source.serializationWeaponName != SerializationWeaponName.MiniShotgun) return weapon;
            EnsureCreated();
            return Prefab != null ? Prefab : weapon;
        }

        private static T FindPrefab<T>(string name) where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(w => w.name == name && !w.gameObject.scene.IsValid());
        }

        private static GameObject Build(Shotgun shotgun, ParticleBlade blade, Sprite sprite)
        {
            // The prefab stays active under an inactive holder, so the spawner's plain Instantiate gives a live weapon
            var holder = new GameObject("SpiderSurgeWeapons");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);
            GameObject bayonet = Object.Instantiate(shotgun.gameObject, holder.transform);
            bayonet.name = Values.Name;
            bayonet.GetComponent<Shotgun>().serializationWeaponName = Values.Id;
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

            Object.Destroy(partsHolder);
            CustomPrefabNet.RegisterWeapon(bayonet, Values.Name);
            return bayonet;
        }

        // Same canvas, pivot and scale as the Baby Shotgun's sprite, so the grip, muzzle and shot line up
        private static Sprite LoadSprite(SpriteRenderer source)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SpriteResource))
                {
                    if (stream == null)
                    {
                        Logger.LogWarning("[SpiderSurge] The Bayonet sprite is missing from the build.");
                        return null;
                    }
                    var data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!texture.LoadImage(data)) return null;
                    texture.filterMode = source != null && source.sprite != null ? source.sprite.texture.filterMode : FilterMode.Bilinear;
                    texture.name = Values.Name;
                    Vector2 pivot = source != null && source.sprite != null
                        ? new Vector2(source.sprite.pivot.x / source.sprite.texture.width, source.sprite.pivot.y / source.sprite.texture.height)
                        : new Vector2(0.5f, 0.5f);
                    return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, Values.SpritePixelsPerUnit);
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[SpiderSurge] Could not load the Bayonet sprite: {ex.Message}");
                return null;
            }
        }
    }

    // A Baby Shotgun spawn sometimes comes as a Bayonet; the fair-weapons pick rolls from its index so every spawner agrees
    [HarmonyPatch]
    public static class Bayonet_Spawn_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SurvivalMode), nameof(SurvivalMode.GetRandomWeapon));
            yield return AccessTools.Method(typeof(VersusMode), nameof(VersusMode.GetRandomWeapon));
            yield return AccessTools.Method(typeof(VersusMode), nameof(VersusMode.GetWeapon));
        }

        [HarmonyPostfix]
        public static void Postfix(MethodBase __originalMethod, object[] __args, ref GameObject __result)
        {
            float roll = __originalMethod.Name == nameof(VersusMode.GetWeapon)
                ? (float)new System.Random((int)__args[0]).NextDouble()
                : Random.value;
            __result = Bayonet.MaybeReplace(__result, roll);
        }
    }
}
