using System;
using System.Collections.Generic;

namespace SpiderSurge
{
    public enum ChallengeKind : byte
    {
        None, LastHeart, DoubleCooldown, NoRegularAbilities, LifeWager, NoUltimate, Overrun
    }

    public static class ChaosRules
    {
        public static string Name(ChallengeKind kind)
        {
            switch (kind)
            {
                case ChallengeKind.LastHeart: return "Last Heart";
                case ChallengeKind.DoubleCooldown: return "Double Cooldown";
                case ChallengeKind.NoRegularAbilities: return "No Regular Abilities";
                case ChallengeKind.LifeWager: return "Life Wager";
                case ChallengeKind.NoUltimate: return "No Ultimate";
                case ChallengeKind.Overrun: return "Overrun";
                default: return "";
            }
        }

        public static string Description(ChallengeKind kind)
        {
            switch (kind)
            {
                case ChallengeKind.LastHeart: return "Play with 1 team life. Borrowed lives return at the next map choice. Losing your last life ends the run.";
                case ChallengeKind.DoubleCooldown: return "Regular ability and ultimate cooldowns are doubled.";
                case ChallengeKind.NoRegularAbilities: return "Regular abilities are disabled. Your ultimate still works.";
                case ChallengeKind.LifeWager: return "Lose a team life: lose 1 chaos point and forfeit the reward. The penalty happens once and cannot take you below zero.";
                case ChallengeKind.NoUltimate: return "Ultimates are disabled. Regular abilities still work.";
                case ChallengeKind.Overrun: return "Face 50% more enemy budget each wave, on top of Surge difficulty.";
                default: return "";
            }
        }

        public static int Reward(ChallengeKind kind) => kind == ChallengeKind.None ? 0 :
            kind == ChallengeKind.LastHeart || kind == ChallengeKind.NoRegularAbilities || kind == ChallengeKind.Overrun ? 2 : 1;

        public static List<ChallengeKind> Eligible(int lives, bool abilities, bool ultimate)
        {
            var result = new List<ChallengeKind> { ChallengeKind.LifeWager, ChallengeKind.Overrun };
            if (lives >= 2) result.Add(ChallengeKind.LastHeart);
            if (abilities)
            {
                result.Add(ChallengeKind.DoubleCooldown);
                result.Add(ChallengeKind.NoRegularAbilities);
                if (ultimate) result.Add(ChallengeKind.NoUltimate);
            }
            return result;
        }

        public static int BonusSlot(double chance, int count, int selectedSlot) => chance < .75 && count > 0 ? Math.Max(0, Math.Min(count - 1, selectedSlot)) : -1;

        // "1 chaos point", "+2 chaos points", "-1 chaos point"
        public static string Points(int amount, bool signed = false) =>
            $"{(signed && amount > 0 ? "+" : "")}{amount} chaos point{(Math.Abs(amount) == 1 ? "" : "s")}";
    }
}
