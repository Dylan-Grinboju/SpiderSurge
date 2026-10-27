using System;
using System.Collections.Generic;

namespace SpiderSurge
{
    public enum ChallengeKind : byte
    {
        None, PoisonedHeart, DoubleCooldown, NoRegularAbilities, LifeWager, NoUltimate, Overrun
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
                case ChallengeKind.Overrun: return "Face 50% more enemies.";
                default: return "";
            }
        }

        public static int Reward(ChallengeKind kind) => kind == ChallengeKind.None ? 0 :
            kind == ChallengeKind.PoisonedHeart || kind == ChallengeKind.NoRegularAbilities || kind == ChallengeKind.Overrun ? 2 : 1;

        public static List<ChallengeKind> Eligible(int lives, bool abilities, bool ultimate)
        {
            var result = new List<ChallengeKind> { ChallengeKind.LifeWager, ChallengeKind.Overrun };
            if (lives >= 2) result.Add(ChallengeKind.PoisonedHeart);
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
