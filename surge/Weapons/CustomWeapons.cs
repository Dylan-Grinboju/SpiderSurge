using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Weapons
{
    // A mod weapon that spawns as one of its own, next to the game weapon it is built from and as often as it until its rarity is set
    internal sealed class CustomWeapon
    {
        public readonly SerializationWeaponName Id;
        public readonly string Name;
        // Its rarity's key in the mod's data
        public readonly string Key;
        public readonly SerializationWeaponName Anchor;
        private readonly System.Func<GameObject> _prefab;

        public CustomWeapon(SerializationWeaponName id, string name, string key, SerializationWeaponName anchor, System.Func<GameObject> prefab)
        {
            Id = id;
            Name = name;
            Key = key;
            Anchor = anchor;
            _prefab = prefab;
        }

        public bool Is(Weapon weapon) => weapon != null && weapon.serializationWeaponName == Id;

        public bool Is(SpawnableWeapon entry) => Is(CustomWeapons.WeaponOf(entry));

        public bool IsAnchor(SpawnableWeapon entry)
        {
            Weapon weapon = CustomWeapons.WeaponOf(entry);
            return weapon != null && weapon.serializationWeaponName == Anchor;
        }

        public float AnchorRarity(List<SpawnableWeapon> weapons)
        {
            SpawnableWeapon anchor = weapons?.Find(IsAnchor);
            return anchor != null ? anchor.baseRarity : 0f;
        }

        // Adds it after its anchor, with the anchor's rarity; a list without the anchor stays as it is
        public SpawnableWeapon AddTo(List<SpawnableWeapon> weapons)
        {
            if (weapons == null) return null;
            SpawnableWeapon existing = weapons.Find(Is);
            if (existing != null) return existing;
            int index = weapons.FindIndex(IsAnchor);
            if (index < 0) return null;
            GameObject prefab = _prefab();
            if (prefab == null) return null;
            var entry = new SpawnableWeapon(prefab.GetComponent<Weapon>(), (int)weapons[index].baseRarity);
            weapons.Insert(index + 1, entry);
            return entry;
        }
    }

    public static class CustomWeapons
    {
        internal static readonly CustomWeapon[] All =
        {
            new CustomWeapon(Consts.Values.Bayonet.Id, Consts.Values.Bayonet.Name, "bayonet", SerializationWeaponName.MiniShotgun, () =>
            {
                Bayonet.EnsureCreated();
                return Bayonet.Prefab;
            }),
            new CustomWeapon(Consts.Values.PoisonDart.Id, Consts.Values.PoisonDart.Name, "poisondart", SerializationWeaponName.GrenadeLauncher, () =>
            {
                PoisonDart.EnsureCreated();
                return PoisonDart.Prefab;
            }),
            new CustomWeapon(Consts.Values.Repulsor.Id, Consts.Values.Repulsor.Name, "repulsor", SerializationWeaponName.MiniShotgun, () =>
            {
                Repulsor.EnsureCreated();
                return Repulsor.Prefab;
            }),
            new CustomWeapon(Consts.Values.PoisonGrenade.Id, Consts.Values.PoisonGrenade.Name, "poisongrenade", SerializationWeaponName.Grenade, () =>
            {
                PoisonGrenade.EnsureCreated();
                return PoisonGrenade.Prefab;
            }),
        };

        private static Transform _holder;

        public static void EnsureCreated()
        {
            Bayonet.EnsureCreated();
            PoisonDart.EnsureCreated();
            Repulsor.EnsureCreated();
            PoisonGrenade.EnsureCreated();
        }

        // Prefabs stay active under an inactive holder, so the spawner's plain Instantiate gives a live weapon
        internal static Transform Holder
        {
            get
            {
                if (_holder != null) return _holder;
                var holder = new GameObject("SpiderSurgeWeapons");
                holder.SetActive(false);
                Object.DontDestroyOnLoad(holder);
                _holder = holder.transform;
                return _holder;
            }
        }

        internal static Weapon WeaponOf(SpawnableWeapon entry) => entry != null && entry.weaponObject != null ? entry.weaponObject.GetComponent<Weapon>() : null;

        internal static CustomWeapon Of(SerializationWeaponName id) => All.FirstOrDefault(weapon => weapon.Id == id);

        internal static CustomWeapon Of(SpawnableWeapon entry)
        {
            Weapon weapon = WeaponOf(entry);
            return weapon != null ? Of(weapon.serializationWeaponName) : null;
        }

        public static string NameOf(SerializationWeaponName id) => Of(id)?.Name;

        internal static bool IsCustom(SpawnableWeapon entry) => Of(entry) != null;

        internal static void AddTo(List<SpawnableWeapon> weapons)
        {
            foreach (CustomWeapon weapon in All) weapon.AddTo(weapons);
        }

        internal static T FindPrefab<T>(string name) where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(w => w.name == name && !w.gameObject.scene.IsValid());
        }

        // Same pivot as the sprite it replaces, so grips, muzzles and shots line up. An optional sprite not in the build is null without a warning
        internal static Sprite LoadSprite(string resource, string name, Sprite source, float pixelsPerUnit, bool optional = false)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        if (!optional) Logger.LogWarning($"[SpiderSurge] The {name} sprite is missing from the build.");
                        return null;
                    }
                    var data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!texture.LoadImage(data)) return null;
                    texture.filterMode = source != null ? source.texture.filterMode : FilterMode.Bilinear;
                    texture.name = name;
                    Vector2 pivot = source != null
                        ? new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height)
                        : new Vector2(0.5f, 0.5f);
                    return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, pixelsPerUnit);
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[SpiderSurge] Could not load the {name} sprite: {ex.Message}");
                return null;
            }
        }
    }

    // Survival's weapon list is copied from its config when a run starts; the mod's weapons join it next to their anchors
    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.StartGame))]
    internal static class CustomWeapons_SurvivalWeapons_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(SurvivalMode __instance)
        {
            CustomWeapons.AddTo(GameFields.SurvivalWeapons?.Invoke(__instance));
        }
    }

    // In Versus each mod weapon has its own rarity button. The game saves its weapon rarities by position and drops the save when
    // the count differs, so its save holds only the game's weapons and the mod's weapons' rarities are kept in the mod's data
    internal static class CustomWeaponsVersus
    {
        private const string Section = "versus.weapons";
        private const int MaxRarity = 5;

        private static readonly List<KeyValuePair<int, SpawnableWeapon>> _taken = new List<KeyValuePair<int, SpawnableWeapon>>();

        private static List<SpawnableWeapon> Weapons => VersusMode.instance != null ? VersusMode.instance.weapons : null;

        internal static void Ensure()
        {
            if (Weapons != null) CustomWeapons.AddTo(Weapons);
        }

        internal static void TakeOut()
        {
            List<SpawnableWeapon> weapons = Weapons;
            if (weapons == null || _taken.Count > 0) return;
            for (int i = 0; i < weapons.Count; i++)
            {
                if (CustomWeapons.IsCustom(weapons[i])) _taken.Add(new KeyValuePair<int, SpawnableWeapon>(i, weapons[i]));
            }
            for (int i = _taken.Count - 1; i >= 0; i--) weapons.RemoveAt(_taken[i].Key);
        }

        internal static void PutBack()
        {
            List<SpawnableWeapon> weapons = Weapons;
            if (weapons != null)
            {
                foreach (var taken in _taken)
                {
                    if (!weapons.Contains(taken.Value)) weapons.Insert(Mathf.Min(taken.Key, weapons.Count), taken.Value);
                }
            }
            _taken.Clear();
        }

        // Until its rarity is saved, each follows its anchor's, so a player's own weapon choice stays as it was
        internal static void LoadRarity()
        {
            List<SpawnableWeapon> weapons = Weapons;
            if (weapons == null) return;
            foreach (CustomWeapon weapon in CustomWeapons.All)
            {
                SpawnableWeapon entry = weapons.Find(weapon.Is);
                if (entry == null) continue;
                entry.baseRarity = weapon.AnchorRarity(weapons);
                string key = weapon.Key + " ";
                foreach (string line in ModData.Lines(Section))
                {
                    if (line.StartsWith(key, System.StringComparison.Ordinal) && int.TryParse(line.Substring(key.Length), out int rarity))
                    {
                        entry.baseRarity = Mathf.Clamp(rarity, 0, MaxRarity);
                    }
                }
            }
        }

        internal static void SaveRarity()
        {
            List<SpawnableWeapon> weapons = Weapons;
            if (weapons == null) return;
            var lines = new List<string>();
            foreach (CustomWeapon weapon in CustomWeapons.All)
            {
                SpawnableWeapon entry = weapons.Find(weapon.Is);
                if (entry != null) lines.Add(weapon.Key + " " + (int)entry.baseRarity);
            }
            if (lines.Count > 0) ModData.Write(Section, lines.ToArray());
        }

        // The game's own profiles don't list the mod's weapons, so with one of them each follows its anchor
        internal static void FollowProfile(VersusProfile profile)
        {
            List<SpawnableWeapon> weapons = Weapons;
            if (weapons == null) return;
            foreach (CustomWeapon weapon in CustomWeapons.All)
            {
                SpawnableWeapon entry = weapons.Find(weapon.Is);
                if (entry == null || (profile.weapons != null && profile.weapons.Contains(weapon.Id))) continue;
                entry.baseRarity = weapon.AnchorRarity(weapons);
            }
            SaveRarity();
        }
    }

    [HarmonyPatch(typeof(GameSettings), "LoadVersusWeaponSettings")]
    internal static class CustomWeapons_VersusLoad_Patch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            CustomWeaponsVersus.Ensure();
            // The defaults are recorded with the mod's weapons in place, so resetting the rarities covers them too
            VersusMode.instance?.SaveDefaultRarities();
            CustomWeaponsVersus.TakeOut();
        }

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            CustomWeaponsVersus.PutBack();
            CustomWeaponsVersus.LoadRarity();
        }
    }

    [HarmonyPatch(typeof(GameSettings), nameof(GameSettings.SaveVersusWeaponSettings))]
    internal static class CustomWeapons_VersusSave_Patch
    {
        [HarmonyPrefix]
        private static void Prefix() => CustomWeaponsVersus.TakeOut();

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            CustomWeaponsVersus.PutBack();
            CustomWeaponsVersus.SaveRarity();
        }
    }

    [HarmonyPatch(typeof(VersusProfile), "SetWeapons")]
    internal static class CustomWeapons_VersusProfile_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(VersusProfile __instance) => CustomWeaponsVersus.FollowProfile(__instance);
    }

    // The rarity button shows the weapon's localized name, and the mod's weapons have no term of their own
    [HarmonyPatch(typeof(CustomTierWeaponButton), nameof(CustomTierWeaponButton.Setup))]
    internal static class CustomWeapons_WeaponButton_Patch
    {
        private static readonly AccessTools.FieldRef<CustomTierWeaponButton, TMPro.TextMeshProUGUI> Title =
            AccessTools.FieldRefAccess<CustomTierWeaponButton, TMPro.TextMeshProUGUI>("titleText");

        [HarmonyPostfix]
        private static void Postfix(CustomTierWeaponButton __instance, SpawnableWeapon weapon)
        {
            CustomWeapon custom = CustomWeapons.Of(weapon);
            if (custom != null) Title(__instance).text = custom.Name;
        }
    }
}
