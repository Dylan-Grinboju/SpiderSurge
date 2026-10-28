using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class PerksManager : MonoBehaviour
    {
        public static PerksManager Instance { get; private set; }

        private bool _isFirstNormalPerkSelection = true;
        private bool _isUltUpgradePerkSelection;
        private bool _isUltSwapPerkSelection;

        public bool IsFirstNormalPerkSelection
        {
            get => _isFirstNormalPerkSelection && !SurgeGameModeManager.AbilitiesDisabled;
            set { _isFirstNormalPerkSelection = value; SurgeNet.MarkDirty(); }
        }

        public bool IsUltUpgradePerkSelection
        {
            get => _isUltUpgradePerkSelection && !SurgeGameModeManager.AbilitiesDisabled;
            set { _isUltUpgradePerkSelection = value; SurgeNet.MarkDirty(); }
        }

        public bool IsUltSwapPerkSelection
        {
            get => _isUltSwapPerkSelection && !SurgeGameModeManager.AbilitiesDisabled;
            set { _isUltSwapPerkSelection = value; SurgeNet.MarkDirty(); }
        }

        // Each ability with its ultimate and component, in the order GetChosenAbilityUltimate checks them
        private static readonly (string ability, string ultimate, System.Type type)[] Abilities =
        {
            (Consts.PerkNames.ImmuneAbility, Consts.PerkNames.ImmuneAbilityUltimate, typeof(ImmuneAbility)),
            (Consts.PerkNames.AmmoAbility, Consts.PerkNames.AmmoAbilityUltimate, typeof(AmmoAbility)),
            (Consts.PerkNames.PulseAbility, Consts.PerkNames.PulseAbilityUltimate, typeof(PulseAbility)),
            (Consts.PerkNames.StorageAbility, Consts.PerkNames.StorageAbilityUltimate, typeof(StorageAbility)),
            (Consts.PerkNames.FreezeAbility, Consts.PerkNames.FreezeAbilityUltimate, typeof(FreezeAbility))
        };

        // Ultimate perks require the base ability to be unlocked
        private static readonly Dictionary<string, string> BaseAbilities = Abilities.ToDictionary(entry => entry.ultimate, entry => entry.ability);

        // Ability perks - shown in special ability selection screen
        private readonly HashSet<string> abilityPerks = new HashSet<string>(Abilities.Select(entry => entry.ability));

        // Upgrade perks - shown in normal perk selection
        private readonly HashSet<string> upgradePerks = new HashSet<string> { Consts.PerkNames.AbilityCooldown, Consts.PerkNames.AbilityDuration, Consts.PerkNames.ShortTermInvestment, Consts.PerkNames.LongTermInvestment, Consts.PerkNames.PerkLuck };

        // Ability Ultimate perks - Ultimate versions of abilities (requires base ability)
        private readonly HashSet<string> abilityUltimatePerks = new HashSet<string>(Abilities.Select(entry => entry.ultimate));

        private readonly Dictionary<string, int> maxLevels = new Dictionary<string, int>
        {
            [Consts.PerkNames.ImmuneAbility] = 1,
            [Consts.PerkNames.AmmoAbility] = 1,
            [Consts.PerkNames.PulseAbility] = 1,
            [Consts.PerkNames.StorageAbility] = 1,
            [Consts.PerkNames.FreezeAbility] = 1,
            [Consts.PerkNames.AbilityCooldown] = 2,
            [Consts.PerkNames.AbilityDuration] = 2,
            [Consts.PerkNames.ShortTermInvestment] = 1,
            [Consts.PerkNames.LongTermInvestment] = 1,
            [Consts.PerkNames.PerkLuck] = 2,
            // Ultimate perks are level 1 only
            [Consts.PerkNames.ImmuneAbilityUltimate] = 1,
            [Consts.PerkNames.AmmoAbilityUltimate] = 1,
            [Consts.PerkNames.PulseAbilityUltimate] = 1,
            [Consts.PerkNames.StorageAbilityUltimate] = 1,
            [Consts.PerkNames.FreezeAbilityUltimate] = 1
        };

        private readonly Dictionary<string, int> perkLevels = new Dictionary<string, int>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // Ensure managers exist
                if (SurgeGameModeManager.Instance == null)
                {
                    GameObject surgeManager = new GameObject("SurgeGameModeManager");
                    surgeManager.AddComponent<SurgeGameModeManager>();
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // SpiderInspector's call requests unlock abilities in live tests through these
        public static void EnableImmuneAbility() => Enable(Consts.PerkNames.ImmuneAbility);
        public static void EnableAmmoAbility() => Enable(Consts.PerkNames.AmmoAbility);
        public static void EnablePulseAbility() => Enable(Consts.PerkNames.PulseAbility);
        public static void EnableStorageAbility() => Enable(Consts.PerkNames.StorageAbility);
        public static void EnableFreezeAbility() => Enable(Consts.PerkNames.FreezeAbility);
        public static void EnableImmuneUltimate() => Enable(Consts.PerkNames.ImmuneAbilityUltimate);
        public static void EnableAmmoUltimate() => Enable(Consts.PerkNames.AmmoAbilityUltimate);
        public static void EnablePulseUltimate() => Enable(Consts.PerkNames.PulseAbilityUltimate);
        public static void EnableStorageUltimate() => Enable(Consts.PerkNames.StorageAbilityUltimate);
        public static void EnableFreezeUltimate() => Enable(Consts.PerkNames.FreezeAbilityUltimate);

        // An ultimate also unlocks its base ability
        private static void Enable(string perkName)
        {
            var (ability, ultimate, type) = System.Array.Find(Abilities, entry => entry.ability == perkName || entry.ultimate == perkName);
            if (type == null)
            {
                Logger.LogError($"Cannot enable {perkName}: not an ability or ultimate perk");
                return;
            }
            bool isUltimate = perkName == ultimate;
            try
            {
                if (isUltimate) Instance.SetPerkLevel(ability, 1);
                Instance.SetPerkLevel(perkName, 1);

                // Register abilities with input interceptor now that they're unlocked
                PlayerAbilityHandler.ActiveSpiderControllers.RemoveAll(sc => sc == null);
                foreach (SpiderController player in PlayerAbilityHandler.ActiveSpiderControllers)
                {
                    var component = player.GetComponent(type) as BaseAbility;
                    if (component != null)
                    {
                        component.RegisterWithInputInterceptor();
                    }
                }
                Instance.UpdatePerkIcons();
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error enabling {type.Name}{(isUltimate ? " ultimate" : "")}: {ex.Message}");
            }
        }

        public bool IsAbilityPerk(string perkName)
        {
            return abilityPerks.Contains(perkName);
        }

        public bool IsUpgradePerk(string perkName)
        {
            return upgradePerks.Contains(perkName);
        }

        public IEnumerable<string> GetAbilityPerkNames()
        {
            return abilityPerks;
        }

        public IEnumerable<string> GetAbilityUltimatePerkNames()
        {
            return abilityUltimatePerks;
        }

        public int GetPerkLevel(string perkName)
        {
            return perkLevels.TryGetValue(perkName, out int level) ? level : 0;
        }

        public bool IsAvailable(string perkName)
        {
            if (!maxLevels.TryGetValue(perkName, out int maxLevel)) return false;
            // Luck is the only Surge perk that doesn't act through an ability
            if (SurgeGameModeManager.AbilitiesDisabled && perkName != Consts.PerkNames.PerkLuck) return false;
            int level = GetPerkLevel(perkName);
            if (level >= maxLevel) return false;
            if (BaseAbilities.TryGetValue(perkName, out string baseAbility) && GetPerkLevel(baseAbility) == 0) return false;

            if ((perkName == Consts.PerkNames.AbilityCooldown || perkName == Consts.PerkNames.AbilityDuration) && level == 1)
            {
                if (!HasAnyUltimate()) return false;
            }

            if (perkName == Consts.PerkNames.ShortTermInvestment || perkName == Consts.PerkNames.LongTermInvestment)
            {
                if (!HasAnyUltimate()) return false;
            }

            return true;
        }

        private bool HasAnyUltimate()
        {
            foreach (var entry in Abilities)
            {
                if (GetPerkLevel(entry.ultimate) > 0) return true;
            }
            return false;
        }

        public string GetChosenAbilityUltimate()
        {
            foreach (var entry in Abilities)
            {
                if (GetPerkLevel(entry.ability) > 0) return entry.ultimate;
            }
            return null;
        }

        public IEnumerable<string> GetAllPerkNames() => maxLevels.Keys;

        public string GetDisplayName(string name) => Consts.Descriptions.GetDisplayName(name, this);
        public string GetDescription(string name) => Consts.Descriptions.GetDescription(name, this);

        public string GetUpgradeDescription(string name) => Consts.Descriptions.GetUpgradeDescription(name, this);
        public int GetMaxLevel(string name) => maxLevels.TryGetValue(name, out int level) ? level : 1;

        public void SetPerkLevel(string perkKey, int level)
        {
            perkLevels[perkKey] = level;
            SyncModifierLevel(perkKey, level);
            SurgeNet.MarkDirty();
        }

        // The game's active perk icons read its own levels, which an ultimate swap and an ultimate's base ability don't go through
        private static void SyncModifierLevel(string key, int level)
        {
            ModifierManager manager = ModifierManager.instance;
            if (manager == null) return;
            int id = manager.GetModId(key);
            Modifier modifier = id == -1 ? null : manager.GetModById(id);
            if (modifier != null) modifier.levelInSurvival = level;
        }

        // A client takes the host's perk table as is and re-registers its local abilities against it
        public void ApplyRemote(Dictionary<string, int> levels, bool firstSelection, bool ultUpgrade, bool ultSwap)
        {
            perkLevels.Clear();
            foreach (var pair in levels)
            {
                perkLevels[pair.Key] = pair.Value;
            }
            _isFirstNormalPerkSelection = firstSelection;
            _isUltUpgradePerkSelection = ultUpgrade;
            _isUltSwapPerkSelection = ultSwap;
            RefreshAbilityRegistrations();
            UpdatePerkIcons();
        }

        public void RefreshAbilityRegistrations()
        {
            PlayerAbilityHandler.ActiveSpiderControllers.RemoveAll(sc => sc == null);
            foreach (SpiderController player in PlayerAbilityHandler.ActiveSpiderControllers)
            {
                foreach (var ability in player.GetComponents<BaseAbility>())
                {
                    ability.RegisterWithInputInterceptor();
                }
            }
        }

        public void OnSelected(string name)
        {
            if (BaseAbilities.ContainsKey(name)) HandleUltimateSelection(name);
            else if (IsAbilityPerk(name)) Enable(name);
        }

        private void HandleUltimateSelection(string newUltName)
        {
            string currentUltPath = GetChosenAbilityUltimate();

            if (currentUltPath != null && currentUltPath != newUltName && BaseAbilities.TryGetValue(currentUltPath, out string oldAbilityName))
            {
                Logger.LogInfo($"[Perk Swap] Swapping from {oldAbilityName}/{currentUltPath} to {newUltName}");

                SetPerkLevel(currentUltPath, 0);
                SetPerkLevel(oldAbilityName, 0);
            }
            Enable(newUltName);
        }

        public void ResetPerks()
        {
            perkLevels.Clear();
            _isFirstNormalPerkSelection = true;
            _isUltUpgradePerkSelection = false;
            _isUltSwapPerkSelection = false;
            SurgeNet.MarkDirty();
            UpdatePerkIcons();
        }

        public float GetPerkLuckChance()
        {
            int level = GetPerkLevel(Consts.PerkNames.PerkLuck);
            if (level == 1) return Consts.Values.Luck.Level1Chance;
            if (level == 2) return Consts.Values.Luck.Level2Chance;
            return 0f;
        }

        private void UpdatePerkIcons()
        {
            if (ModifierManager.instance == null) return;

            int durationId = ModifierManager.instance.GetModId(Consts.PerkNames.AbilityDuration);
            if (durationId == -1) return;

            Modifier durationMod = ModifierManager.instance.GetModById(durationId);
            if (durationMod == null || durationMod.data == null) return;

            Sprite newIcon;
            if (GetPerkLevel(Consts.PerkNames.PulseAbility) > 0)
            {
                newIcon = IconLoader.GetIcon("pulse_duration_perk");
            }
            else
            {
                newIcon = IconLoader.GetIcon("duration_perk");
            }

            if (newIcon != null)
            {
                durationMod.data.icon = newIcon;
            }
        }


    }
}