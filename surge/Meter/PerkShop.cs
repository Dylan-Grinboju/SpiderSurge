using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public enum ShopItem : byte
    {
        Reroll = 0,
        Life = 1,
        Perk = 2,
        MaxLives = 3,
        Sacrifice = 4,
    }

    // What the team can buy with chaos points while a perk screen is open. Purchases run on the host only.
    public static class PerkShop
    {
        private static readonly AccessTools.FieldRef<SurvivalMode, SurvivalConfig> Config = AccessTools.FieldRefAccess<SurvivalMode, SurvivalConfig>("_config");
        private static readonly AccessTools.FieldRef<SurvivalMode, Unity.Netcode.NetworkVariable<bool>> WaitingForPerk = AccessTools.FieldRefAccess<SurvivalMode, Unity.Netcode.NetworkVariable<bool>>("_waitingForPerk");
        private static readonly AccessTools.FieldRef<SurvivalMode, Unity.Netcode.NetworkVariable<int>> Lives = AccessTools.FieldRefAccess<SurvivalMode, Unity.Netcode.NetworkVariable<int>>("_lives");
        private static readonly MethodInfo GetRandomModifiers = AccessTools.Method(typeof(SurvivalMode), "GetRandomModifiers");
        private static readonly MethodInfo GetRandomLevels = AccessTools.Method(typeof(SurvivalMode), "GetRandomLevels");
        private static readonly MethodInfo TryGrantExtraLife = AccessTools.Method(typeof(SurvivalMode), "TryGrantExtraLife");

        // Pain-level modifiers and the blank card are never worth a purchase
        private static readonly HashSet<string> ExcludedPerks = new HashSet<string> { "objectification", "halvedAmmo", "nothing" };

        public static readonly ShopItem[] Items = { ShopItem.Reroll, ShopItem.Life, ShopItem.Perk, ShopItem.MaxLives, ShopItem.Sacrifice };

        // The items on sale on the current perk screen: the host draws them when the screen opens and keeps them through rerolls
        public static ShopItem[] Offer { get; private set; } = new ShopItem[0];
        public static event Action OfferChanged;
        public static int VisitId { get; private set; }
        public static int PurchasedMask { get; private set; }
        public static int BonusSlot { get; private set; } = -1;
        public static bool BonusPaid { get; private set; }
        private static int _visitWave = -1;
        public static bool SoldOut(ShopItem item) => (PurchasedMask & (1 << (int)item)) != 0;

        public static void BeginVisit(List<Modifier> upcoming)
        {
            if (!Net.IsAuthority || AbilitySelection.IsChoosing || RerollInProgress) return;
            int wave = SurvivalMode.instance.CurrentWave;
            if (_visitWave == wave) return;
            ChallengeController.Settle();
            _visitWave = wave;
            VisitId++;
            PurchasedMask = 0;
            BonusPaid = false;
            BonusSlot = ChaosRules.BonusSlot(UnityEngine.Random.value, upcoming.Count, UnityEngine.Random.Range(0, Math.Max(1, upcoming.Count)));
            RollOffer(upcoming);
            ChaosWallet.Notify();
        }

        public static void ResolveMap(Modifier modifier, GameLevel level)
        {
            if (!Net.IsAuthority || BonusPaid) return;
            BonusPaid = true;
            var cards = PerkChoiceWidget.Cards(SurvivalModeHud.instance);
            int index = cards.FindIndex(card => card.modifier == modifier && card.level == level);
            if (index >= 0 && index == BonusSlot) ChaosWallet.GrantPoints(1, "Map bonus");
            ChaosNet.MarkDirty();
        }

        // Set while a reroll re-shows the cards, so the perk timer prefix doesn't start a second countdown
        public static bool RerollInProgress { get; private set; }

        // Items that can be bought right now come first, so a full team isn't offered a life it can't take; upcoming are the perks about to go on the cards
        public static void RollOffer(List<Modifier> upcoming)
        {
            if (!Net.IsAuthority) return;
            var available = Items.Where(item => Unavailable(item, upcoming) == null).OrderBy(_ => UnityEngine.Random.value).ToList();
            var rest = Items.Where(item => !available.Contains(item)).OrderBy(_ => UnityEngine.Random.value);
            SetOffer(available.Concat(rest).Take(Consts.Values.Chaos.OfferSize).ToArray());
            ChaosNet.MarkDirty();
        }

        public static void ClearOffer()
        {
            VisitId++;
            PurchasedMask = 0;
            BonusSlot = -1;
            BonusPaid = false;
            _visitWave = -1;
            SetOffer(new ShopItem[0]);
        }

        internal static void ApplyRemoteOffer(int visit, int purchased, int bonus, bool paid, ShopItem[] offer)
        {
            if (!Net.IsRemoteClient) return;
            // The cards can show before this state arrives, so the bonus label follows the state too
            bool relabel = visit != VisitId || bonus != BonusSlot;
            VisitId = visit;
            PurchasedMask = purchased;
            BonusSlot = bonus;
            BonusPaid = paid;
            SetOffer(offer ?? new ShopItem[0]);
            if (relabel) MapBonusLabels.Refresh();
        }

        private static void SetOffer(ShopItem[] offer)
        {
            if (Offer.SequenceEqual(offer)) return;
            Offer = offer;
            OfferChanged?.Invoke();
        }

        public static bool IsOffered(ShopItem item) => Array.IndexOf(Offer, item) >= 0;

        public static int CostOf(ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Reroll: return Consts.Values.Chaos.RerollCost;
                case ShopItem.Life: return Consts.Values.Chaos.LifeCost;
                case ShopItem.Perk: return Consts.Values.Chaos.PerkCost;
                case ShopItem.Sacrifice: return 0;
                default: return Consts.Values.Chaos.MaxLivesCost;
            }
        }

        // What the item costs, as the panel prints it: points, or the life the Blood Pact takes
        public static string PriceOf(ShopItem item) => item == ShopItem.Sacrifice ? $"{Consts.Values.Chaos.SacrificeLives} life" : CostOf(item).ToString();

        public static string NameOf(ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Reroll: return "Reroll";
                case ShopItem.Life: return "Extra Life";
                case ShopItem.Perk: return "Mystery Perk";
                case ShopItem.Sacrifice: return "Blood Pact";
                default: return "Bigger Heart";
            }
        }

        public static string DescriptionOf(ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Reroll: return "New perk cards";
                case ShopItem.Life: return "+1 life, up to the max";
                case ShopItem.Perk: return "A random perk you don't have";
                case ShopItem.Sacrifice: return "Trade a life for a chaos point";
                default: return "+1 max lives and +1 life";
            }
        }

        public static string IconOf(ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Reroll: return "shop_reroll";
                case ShopItem.Life: return "shop_life";
                case ShopItem.Perk: return "shop_perk";
                case ShopItem.Sacrifice: return "shop_sacrifice";
                default: return "shop_maxlives";
            }
        }

        public static string Unavailable(ShopItem item) => Unavailable(item, null);

        // The reason an item can't be bought right now, or null; the price is checked separately so the panel can show both
        private static string Unavailable(ShopItem item, List<Modifier> onCards)
        {
            if (SoldOut(item)) return "Sold out";
            var mode = SurvivalMode.instance;
            if (item == ShopItem.Life && mode != null && mode.Lives >= mode.MaxLives) return "Lives are full";
            if (item == ShopItem.Sacrifice && mode != null && mode.Lives < Consts.Values.Chaos.SacrificeMinLives) return "Need a spare life";
            if (item == ShopItem.Perk && Net.IsAuthority && PerkPool(onCards).Count == 0) return "No perk left to give";
            return null;
        }

        public static bool IsOpen
        {
            get
            {
                var hud = SurvivalModeHud.instance;
                if (hud == null || hud.perkChoiceView == null || !hud.perkChoiceView.IsVisible || PerkChoiceWidget.ChoiceMade) return false;
                var mode = SurvivalMode.instance;
                if (Net.IsAuthority && mode != null)
                {
                    var waiting = WaitingForPerk(mode);
                    if (waiting != null && !waiting.Value) return false;
                }
                return true;
            }
        }

        // Any machine: asks the host (or itself) to buy. The panel refreshes from the chaos state that follows.
        public static void Request(ShopItem item, int buyerId)
        {
            if (!ChaosWallet.Enabled) return;
            if (!IsOpen || !IsOffered(item) || !ChaosWallet.CanAfford(CostOf(item)) || Unavailable(item) != null)
            {
                ShowRefused(item, null);
                return;
            }
            ChaosNet.SendBuy(item, buyerId);
        }

        // Host only: makes the purchase and returns null, or returns why it was refused
        internal static string Buy(ShopItem item, int buyerId, int visit)
        {
            if (!Net.IsAuthority || !ChaosWallet.Enabled || !IsOpen) return "The shop is closed";
            if (visit != VisitId) return "This shop visit has ended";
            if (!IsOffered(item)) return "Not on sale on this screen";
            int cost = CostOf(item);
            if (!ChaosWallet.CanAfford(cost)) return "Not enough chaos points";
            string unavailable = Unavailable(item);
            if (unavailable != null) return unavailable;

            PurchasedMask |= 1 << (int)item;
            string detail;
            try
            {
                switch (item)
                {
                    case ShopItem.Reroll: detail = Reroll(); break;
                    case ShopItem.Life: detail = GrantLife(); break;
                    case ShopItem.Perk: detail = GrantRandomPerk(); break;
                    case ShopItem.Sacrifice: detail = SellLife(); break;
                    default: detail = RaiseMaxLives(); break;
                }
            }
            catch (Exception ex)
            {
                PurchasedMask &= ~(1 << (int)item);
                Logger.LogError($"[Surge shop] {item} failed: {ex}");
                return "It didn't go through";
            }
            if (detail == null)
            {
                PurchasedMask &= ~(1 << (int)item);
                return "It didn't go through";
            }

            ChaosWallet.TrySpend(cost);
            Logger.LogInfo($"[Surge shop] {NameOf(item)} bought for {PriceOf(item)} by {Players.NameOf(buyerId)} ({detail})");
            Logging.SpiderSurgeStatsManager.Instance?.LogMeterPurchase(item, detail);
            if (item == ShopItem.MaxLives) SurgeAchievements.Unlock(SurgeAchievements.MaxCapacity, Players.ById(buyerId));
            ShowBought(item, buyerId, detail);
            ChaosNet.SendBought(item, buyerId, detail);
            return null;
        }

        // On the machine that asked; a refusal from the host comes with its reason, a local one is already on the panel
        public static void ShowRefused(ShopItem item, string reason)
        {
            SoundManager.Instance?.PlaySound(Consts.SoundNames.AbilityNotReady, Consts.SoundVolumes.AbilityNotReady * Consts.SoundVolumes.MasterVolume);
            if (!string.IsNullOrEmpty(reason)) Toast.Show($"{NameOf(item)}: {reason}", Color.white);
        }

        // The vanilla path sends the new cards to every client, and the Surge selection prefix runs again with the same phase flags
        private static string Reroll()
        {
            var mode = SurvivalMode.instance;
            var hud = SurvivalModeHud.instance;
            if (mode == null || hud == null || GetRandomModifiers == null || GetRandomLevels == null) return null;

            int count = Config(mode).basePerkChoiceCount + ModifierManager.instance.GetModLevel("picky");
            var mods = GetRandomModifiers.Invoke(mode, new object[] { count }) as List<Modifier>;
            var levels = GetRandomLevels.Invoke(mode, new object[] { count }) as List<GameLevel>;
            if (mods == null || levels == null || mods.Count == 0 || levels.Count < mods.Count) return null;

            Logging.SpiderSurgeStatsManager.Instance?.OnPerkChoiceRerolled();
            if (hud.perkCardsRoot != null)
            {
                foreach (Transform card in hud.perkCardsRoot) SettingsPage.StopClickAnimations(card.gameObject);
            }
            RerollInProgress = true;
            try
            {
                hud.ShowPerkChoices(mods, levels);
            }
            finally
            {
                RerollInProgress = false;
            }
            return string.Join(", ", mods.Select(mod => mod.data.key));
        }

        private static string GrantLife()
        {
            var mode = SurvivalMode.instance;
            if (mode == null || TryGrantExtraLife == null) return null;
            bool granted = (bool)TryGrantExtraLife.Invoke(mode, null);
            return granted ? $"{mode.Lives}/{mode.MaxLives}" : null;
        }

        // The Luck marks and the phase flags belong to the cards on screen, so they are kept around the level change
        private static string GrantRandomPerk()
        {
            var pool = PerkPool();
            if (pool.Count == 0) return null;
            Modifier perk = pool[UnityEngine.Random.Range(0, pool.Count)];

            var perks = PerksManager.Instance;
            var lucky = ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys;
            var luckyBefore = new List<string>(lucky);
            bool first = perks != null && perks.IsFirstNormalPerkSelection;
            bool ultUpgrade = perks != null && perks.IsUltUpgradePerkSelection;
            bool ultSwap = perks != null && perks.IsUltSwapPerkSelection;

            lucky.Clear();
            try
            {
                ModifierManager.instance.SetModLevel(perk, GameMode.Wave, 1);
            }
            finally
            {
                lucky.Clear();
                foreach (string key in luckyBefore) lucky.Add(key);
                if (perks != null)
                {
                    perks.IsFirstNormalPerkSelection = first;
                    perks.IsUltUpgradePerkSelection = ultUpgrade;
                    perks.IsUltSwapPerkSelection = ultSwap;
                }
                SurgeNet.SendStateNow();
            }
            string title = string.IsNullOrEmpty(perk.data.title) ? perk.data.key : perk.data.title;
            StatsMod.GameSessionManager.Instance?.RecordPerk(title + " (shop)");
            return perk.data.key;
        }

        private static string RaiseMaxLives()
        {
            var mode = SurvivalMode.instance;
            if (mode == null || mode._maxlives == null) return null;
            mode._maxlives.Value++;
            Lives(mode).Value = Math.Min(mode.MaxLives, mode.Lives + 1);
            return $"{mode.Lives}/{mode.MaxLives}";
        }

        // One life always stays, since the game ends the run when a lost wave takes the last one
        private static string SellLife()
        {
            var mode = SurvivalMode.instance;
            var lives = mode != null ? Lives(mode) : null;
            if (lives == null || lives.Value < Consts.Values.Chaos.SacrificeMinLives) return null;
            lives.Value -= Consts.Values.Chaos.SacrificeLives;
            ChaosWallet.GrantPoints(Consts.Values.Chaos.SacrificePoints, "Blood Pact");
            return $"{mode.Lives}/{mode.MaxLives}, {ChaosWallet.Points} chaos points";
        }

        // The perks on the cards stay out, since picking one after the shop gave it would raise it past its level
        private static List<Modifier> PerkPool(List<Modifier> onCards = null)
        {
            var manager = ModifierManager.instance;
            var perks = PerksManager.Instance;
            if (manager == null) return new List<Modifier>();
            var ults = perks != null ? new HashSet<string>(perks.GetAbilityUltimatePerkNames()) : new HashSet<string>();
            var shown = new HashSet<string>((onCards ?? PerkChoiceWidget.Cards(SurvivalModeHud.instance).Select(card => card.modifier))
                .Where(mod => mod?.data != null)
                .Select(mod => mod.data.key));
            return manager.GetNonMaxedSurvivalMods()
                .Where(mod => mod?.data != null
                    && mod.levelInSurvival == 0
                    && !ExcludedPerks.Contains(mod.data.key)
                    && !shown.Contains(mod.data.key)
                    && !(perks != null && perks.IsAbilityPerk(mod.data.key))
                    && !ults.Contains(mod.data.key))
                .ToList();
        }

        public static string DisplayNameOfPerk(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            var perks = PerksManager.Instance;
            if (perks != null && perks.GetAllPerkNames().Contains(key)) return perks.GetDisplayName(key);
            var manager = ModifierManager.instance;
            if (manager != null)
            {
                int id = manager.GetModId(key);
                if (id >= 0)
                {
                    string title = manager.GetModById(id).data.title;
                    if (!string.IsNullOrEmpty(title)) return title;
                }
            }
            return key;
        }

        // A popup on this machine only; the host sends every client the same purchase
        public static void ShowBought(ShopItem item, int buyerId, string detail)
        {
            if (item == ShopItem.Perk) PerkChoiceHud.RefreshPerks();
            string who = buyerId >= 0 ? Players.NameOf(buyerId) : "The team";
            string what;
            switch (item)
            {
                case ShopItem.Reroll: what = "rerolled the perks"; break;
                case ShopItem.Life: what = "bought an extra life"; break;
                case ShopItem.Perk: what = $"got <b>{DisplayNameOfPerk(detail)}</b>"; break;
                case ShopItem.Sacrifice: what = "traded a life for a chaos point"; break;
                default: what = "raised the max lives"; break;
            }
            Toast.Show($"{who} {what}", Hud.Pink);
        }
    }
}
