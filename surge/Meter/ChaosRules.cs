using System;
using System.Collections.Generic;

namespace SpiderSurge
{
    public enum ChallengeKind : byte
    {
        None, PoisonedHeart, DoubleCooldown, NoRegularAbilities, LifeWager, NoUltimate, Overrun, CleanHands
    }

    public static class ChaosRules
    {
        public static string Name(ChallengeKind kind)
        {
            switch (kind)
            {
                case ChallengeKind.PoisonedHeart: return "Poisoned Heart";
                case ChallengeKind.DoubleCooldown: return "Double Cooldown";
                case ChallengeKind.NoRegularAbilities: return "No Abilities";
                case ChallengeKind.LifeWager: return "Life Wager";
                case ChallengeKind.NoUltimate: return "No Ultimate";
                case ChallengeKind.Overrun: return "Overrun";
                case ChallengeKind.CleanHands: return "Clean Hands";
                default: return "";
            }
        }

        public static string Description(ChallengeKind kind)
        {
            switch (kind)
            {
                case ChallengeKind.PoisonedHeart: return "Survive with 1 life. The other lives come back when the challenge ends.";
                case ChallengeKind.DoubleCooldown: return "Double all ability cooldowns.";
                case ChallengeKind.NoRegularAbilities: return "Survive without abilities. Ultimates remain usable.";
                case ChallengeKind.LifeWager: return "Lose no team lives. Failure costs 1 chaos point.";
                case ChallengeKind.NoUltimate: return "Survive without ultimates.";
                case ChallengeKind.Overrun: return "Survive larger enemy waves. Enemies arrive at their usual pace.";
                case ChallengeKind.CleanHands: return "Finish without killing a teammate or breaking a teammate's shield.";
                default: return "";
            }
        }

        public static int Reward(ChallengeKind kind) => kind == ChallengeKind.None ? 0 : kind == ChallengeKind.PoisonedHeart ? 3 :
            kind == ChallengeKind.NoRegularAbilities || kind == ChallengeKind.Overrun ? 2 : 1;

        public static List<ChallengeKind> Eligible(int lives, bool abilities, bool ultimate, int players = 1)
        {
            var result = new List<ChallengeKind> { ChallengeKind.LifeWager, ChallengeKind.Overrun };
            if (players >= 2) result.Add(ChallengeKind.CleanHands);
            if (lives >= 2) result.Add(ChallengeKind.PoisonedHeart);
            if (abilities)
            {
                result.Add(ChallengeKind.DoubleCooldown);
                result.Add(ChallengeKind.NoRegularAbilities);
                if (ultimate) result.Add(ChallengeKind.NoUltimate);
            }
            return result;
        }

        public static void AvoidRecent(List<ChallengeKind> eligible, ChallengeKind last, ChallengeKind previous)
        {
            if (eligible.Count > 1) eligible.Remove(last);
            if (eligible.Count > 1) eligible.Remove(previous);
        }

        public static string Flavor(ChallengeKind kind)
        {
            switch (kind)
            {
                case ChallengeKind.PoisonedHeart: return "One heart. No second chances.";
                case ChallengeKind.DoubleCooldown: return "Make every cast count.";
                case ChallengeKind.NoRegularAbilities: return "Back to webs and weapons.";
                case ChallengeKind.LifeWager: return "Something to lose. Something to prove.";
                case ChallengeKind.NoUltimate: return "Save the grand finale.";
                case ChallengeKind.Overrun: return "They brought reinforcements.";
                case ChallengeKind.CleanHands: return "Aim at the other guys.";
                default: return "";
            }
        }

        public static string Icon(ChallengeKind kind)
        {
            switch (kind)
            {
                case ChallengeKind.PoisonedHeart: return "shop_sacrifice";
                case ChallengeKind.DoubleCooldown: return "cooldown_perk";
                case ChallengeKind.NoRegularAbilities: return "short_term_perk";
                case ChallengeKind.LifeWager: return "shop_life";
                case ChallengeKind.NoUltimate: return "long_term_perk";
                case ChallengeKind.Overrun: return "pulse_ability";
                default: return "immune_ability";
            }
        }

        public static int BonusSlot(double chance, int count, int selectedSlot) => chance < .75 && count > 0 ? Math.Max(0, Math.Min(count - 1, selectedSlot)) : -1;

        // "1 chaos point", "+2 chaos points", "-1 chaos point"
        public static string Points(int amount, bool signed = false) =>
            $"{(signed && amount > 0 ? "+" : "")}{amount} chaos point{(Math.Abs(amount) == 1 ? "" : "s")}";
    }
}
