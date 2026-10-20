using System;
using System.Collections.Generic;

namespace SpiderSurge.Logging
{
    public class SpiderSurgeStatsSnapshot
    {
        public TimeSpan MatchDuration { get; set; }
        public int PlayerCount { get; set; }
        public int WavesSurvived { get; set; }
        public int PainLevel { get; set; }
        public string EndReason { get; set; } = SpiderSurgeStatsManager.EndReasons.Quit;
        public int LivesLeft { get; set; }
        public int MaxLives { get; set; }
        public bool StatsTracked { get; set; }
        public bool UnlimitedPerkTime { get; set; }
        public bool SurgeMode { get; set; }
        public bool AbilitiesOn { get; set; } = true;
        public List<PlayerStats> PlayerStats { get; set; } = new List<PlayerStats>();
        public List<string> GlobalPerks { get; set; } = new List<string>();
        public List<PerkChoiceRecord> PerkChoices { get; set; } = new List<PerkChoiceRecord>();
        public List<WaveRecord> Waves { get; set; } = new List<WaveRecord>();
        public TitlesReport Titles { get; set; }
        public MeterReport Meter { get; set; }
    }

    public class MeterReport
    {
        public int ChargePerPoint { get; set; }
        public int TotalCharge { get; set; }
        public int PointsEarned { get; set; }
        public int PointsSpent { get; set; }
        public Dictionary<string, int> ChargeBySource { get; set; } = new Dictionary<string, int>();
        public List<MeterPurchaseRecord> Purchases { get; set; } = new List<MeterPurchaseRecord>();
    }

    public class MeterPurchaseRecord
    {
        public int Wave { get; set; }
        public string Item { get; set; }
        public string Detail { get; set; }
    }

    public class TitlesReport
    {
        public bool Shown { get; set; }
        public int? NewlyUnlocked { get; set; }
        public int? UnlockedTotal { get; set; }
        public int CatalogSize { get; set; }
        public List<EarnedTitle> Earned { get; set; } = new List<EarnedTitle>();
    }

    public class EarnedTitle
    {
        public string Name { get; set; }
        public int PlayerId { get; set; }
        public bool OnScreen { get; set; }
    }

    public class PlayerStats
    {
        public int PlayerId { get; set; }
        public int AbilityActivationCount { get; set; }
        public int UltimateActivationCount { get; set; }
        public int Deaths { get; set; }
        public int LavaDeaths { get; set; }
        public int Kills { get; set; }
        public int FriendlyKills { get; set; }
        public int ShotsFired { get; set; }
        public int ShotsHit { get; set; }
        public int AliveSeconds { get; set; }
        public int Saves { get; set; }
        public int KillsDuringAbility { get; set; }
        public int MeterCharge { get; set; }
        public Dictionary<string, int> EnemyKills { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> WeaponHits { get; set; } = new Dictionary<string, int>();
    }

    public class PerkChoiceRecord
    {
        public int Wave { get; set; }
        public string Kind { get; set; }
        public List<string> Offered { get; set; } = new List<string>();
        public List<string> LuckyOffered { get; set; } = new List<string>();
        public string Picked { get; set; }
        public int Level { get; set; }
        public bool TimedOut { get; set; }
        public bool Rerolled { get; set; }
        public float DecisionSeconds { get; set; }
        public List<string> ShopOffer { get; set; } = new List<string>();
    }

    public class WaveRecord
    {
        public int Wave { get; set; }
        public string Map { get; set; }
        public string Result { get; set; }
        public float Seconds { get; set; }
        public int Deaths { get; set; } = -1;
        public int LivesAtStart { get; set; }
        public int AbilityUses { get; set; }
        public int UltimateUses { get; set; }
        public int Saves { get; set; }
        public int MeterCharge { get; set; }
    }
}
