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
    // A Baby Shotgun with a short Particle Blade strapped beside its barrels, spawning as a weapon of its own
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

        internal static bool Is(SpawnableWeapon entry) => entry != null && entry.weaponObject != null && Is(entry.weaponObject.GetComponent<Weapon>());

        private static bool IsBabyShotgun(SpawnableWeapon entry)
        {
            Weapon weapon = entry != null && entry.weaponObject != null ? entry.weaponObject.GetComponent<Weapon>() : null;
            return weapon != null && weapon.serializationWeaponName == SerializationWeaponName.MiniShotgun;
        }

        // Adds the Bayonet after the Baby Shotgun, with its rarity; a list without the Baby Shotgun stays as it is
        internal static SpawnableWeapon AddTo(List<SpawnableWeapon> weapons)
        {
            if (weapons == null) return null;
            SpawnableWeapon existing = weapons.Find(Is);
            if (existing != null) return existing;
            int index = weapons.FindIndex(IsBabyShotgun);
            if (index < 0) return null;
            EnsureCreated();
            if (Prefab == null) return null;
            var entry = new SpawnableWeapon(Prefab.GetComponent<Weapon>(), (int)weapons[index].baseRarity);
            weapons.Insert(index + 1, entry);
            return entry;
        }

        internal static float BabyShotgunRarity(List<SpawnableWeapon> weapons)
        {
            SpawnableWeapon shotgun = weapons?.Find(IsBabyShotgun);
            return shotgun != null ? shotgun.baseRarity : 0f;
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

    // Survival's weapon list is copied from its config when a run starts; the Bayonet joins it next to the Baby Shotgun
    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.StartGame))]
    internal static class Bayonet_SurvivalWeapons_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(SurvivalMode __instance)
        {
            Bayonet.AddTo(GameFields.SurvivalWeapons?.Invoke(__instance));
        }
    }

    // In Versus the Bayonet has its own rarity button. The game saves its weapon rarities by position and drops the save when
    // the count differs, so its save holds only the game's weapons and the Bayonet's rarity is kept in the mod's data
    internal static class BayonetVersus
    {
        private const string Section = "versus.weapons";
        private const string RarityKey = "bayonet ";
        private const int MaxRarity = 5;

        private static int _takenAt = -1;
        private static SpawnableWeapon _taken;

        private static List<SpawnableWeapon> Weapons => VersusMode.instance != null ? VersusMode.instance.weapons : null;

        internal static SpawnableWeapon Entry => Weapons?.Find(Bayonet.Is);

        internal static void Ensure()
        {
            if (Weapons != null && Entry == null) Bayonet.AddTo(Weapons);
        }

        internal static void TakeOut()
        {
            List<SpawnableWeapon> weapons = Weapons;
            if (weapons == null || _taken != null) return;
            int index = weapons.FindIndex(Bayonet.Is);
            if (index < 0) return;
            _takenAt = index;
            _taken = weapons[index];
            weapons.RemoveAt(index);
        }

        internal static void PutBack()
        {
            List<SpawnableWeapon> weapons = Weapons;
            if (_taken != null && weapons != null && !weapons.Contains(_taken)) weapons.Insert(Mathf.Min(_takenAt, weapons.Count), _taken);
            _taken = null;
            _takenAt = -1;
        }

        // Until its rarity is saved, the Bayonet follows the Baby Shotgun's, so a player's own weapon choice stays as it was
        internal static void LoadRarity()
        {
            SpawnableWeapon entry = Entry;
            if (entry == null) return;
            entry.baseRarity = Bayonet.BabyShotgunRarity(Weapons);
            foreach (string line in ModData.Lines(Section))
            {
                if (line.StartsWith(RarityKey, System.StringComparison.Ordinal) && int.TryParse(line.Substring(RarityKey.Length), out int rarity))
                {
                    entry.baseRarity = Mathf.Clamp(rarity, 0, MaxRarity);
                }
            }
        }

        internal static void SaveRarity()
        {
            SpawnableWeapon entry = Entry;
            if (entry != null) ModData.Write(Section, new[] { RarityKey + (int)entry.baseRarity });
        }
    }

    [HarmonyPatch(typeof(GameSettings), "LoadVersusWeaponSettings")]
    internal static class Bayonet_VersusLoad_Patch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            BayonetVersus.Ensure();
            // The defaults are recorded with the Bayonet in place, so resetting the rarities covers it too
            VersusMode.instance?.SaveDefaultRarities();
            BayonetVersus.TakeOut();
        }

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            BayonetVersus.PutBack();
            BayonetVersus.LoadRarity();
        }
    }

    [HarmonyPatch(typeof(GameSettings), nameof(GameSettings.SaveVersusWeaponSettings))]
    internal static class Bayonet_VersusSave_Patch
    {
        [HarmonyPrefix]
        private static void Prefix() => BayonetVersus.TakeOut();

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            BayonetVersus.PutBack();
            BayonetVersus.SaveRarity();
        }
    }

    // The game's own profiles don't list the Bayonet, so with one of them it follows the Baby Shotgun
    [HarmonyPatch(typeof(VersusProfile), "SetWeapons")]
    internal static class Bayonet_VersusProfile_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(VersusProfile __instance)
        {
            SpawnableWeapon entry = BayonetVersus.Entry;
            if (entry == null || (__instance.weapons != null && __instance.weapons.Contains(Values.Id))) return;
            entry.baseRarity = Bayonet.BabyShotgunRarity(VersusMode.instance.weapons);
            BayonetVersus.SaveRarity();
        }
    }

    // The rarity button shows the weapon's localized name, and the Bayonet has no term of its own
    [HarmonyPatch(typeof(CustomTierWeaponButton), nameof(CustomTierWeaponButton.Setup))]
    internal static class Bayonet_WeaponButton_Patch
    {
        private static readonly AccessTools.FieldRef<CustomTierWeaponButton, TMPro.TextMeshProUGUI> Title =
            AccessTools.FieldRefAccess<CustomTierWeaponButton, TMPro.TextMeshProUGUI>("titleText");

        [HarmonyPostfix]
        private static void Postfix(CustomTierWeaponButton __instance, SpawnableWeapon weapon)
        {
            if (Bayonet.Is(weapon)) Title(__instance).text = Values.Name;
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
}
