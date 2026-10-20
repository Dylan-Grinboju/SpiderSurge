using System;
using System.Collections.Generic;
using UnityEngine;
using Logger = Silk.Logger;
using Random = UnityEngine.Random;

namespace SpiderTeams
{
    // Hands SpiderSurge's abilities to every spider in a versus match: one ability and one set of perks for everyone.
    internal static class SurgeVersus
    {
        private static readonly List<PlayerController> _candidates = new List<PlayerController>();
        private static int _round;
        private static int _lastPick = -1;
        private static bool _touched;
        private static bool _warnedRemote;

        public static bool RoundActive { get; private set; }
        public static int CurrentAbility { get; private set; } = -1;

        public static void OnMatchStarted()
        {
            _round = 0;
            _lastPick = -1;
            _warnedRemote = false;
        }

        // Host only. Runs before the round's spiders spawn, so SpiderSurge adds the abilities to them already unlocked.
        public static void OnRoundStarting(VersusMode versus, bool waitingRoom)
        {
            if (!TeamManager.IsHost || waitingRoom || versus == null || !versus.GameModeActive() || !ModConfig.SurgeEnabled || !SurgeBridge.Available)
            {
                Stop();
                return;
            }

            if (!AllPlayersLocal())
            {
                Stop();
                if (!_warnedRemote)
                {
                    _warnedRemote = true;
                    Logger.LogInfo("SpiderTeams: Surge abilities are off for this match because a player has no SpiderSurge");
                    Announcements.Broadcast("Surge abilities are off: a player has no SpiderSurge", Color.white, false);
                }
                return;
            }

            int ability = Pick(ModConfig.SurgeAbility);
            if (!Apply(ability))
            {
                Stop();
                return;
            }

            _round++;
            bool firstRound = _round == 1;
            RoundActive = true;
            SpiderSurge.Common.MatchHistory.MarkSurgeVersus();
            CurrentAbility = ability;
            _lastPick = ability;
            Logger.LogInfo($"SpiderTeams Surge round: {Describe(ability)}");

            if (firstRound || ModConfig.SurgeAbility == SurgeAbility.Random)
            {
                Announcements.BroadcastLater(Describe(ability), SurgeColor(), false, Consts.Surge.AnnounceDelay);
            }
        }

        public static void Stop()
        {
            if (!RoundActive && !_touched)
            {
                return;
            }

            RoundActive = false;
            CurrentAbility = -1;
            _touched = false;
            try
            {
                SurgeBridge.SetRunActive(false);
                SurgeBridge.ResetPerks();
                SurgeBridge.ClearStoredWeapons();
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams could not switch Surge abilities off: {ex}");
            }
        }

        public static string Describe(int ability)
        {
            if (ability < 0 || ability >= Consts.Surge.AbilityPerks.Length)
            {
                return string.Empty;
            }

            string text = "Everyone has " + Consts.Surge.AbilityNames[ability];
            if (ModConfig.SurgeUltimate)
            {
                text += " + " + Consts.Surge.UltimateNames[ability];
            }
            return text;
        }

        public static Color SurgeColor()
        {
            Color color;
            return ColorUtility.TryParseHtmlString(Consts.Surge.Color, out color) ? color : Color.cyan;
        }

        // Neural Backup: revive a dead teammate next to the caster. Returning null lets SpiderSurge summon its wasps instead.
        public static PlayerController ReviveTeammate(Component ability)
        {
            var manager = TeamManager.Instance;
            var lobby = LobbyController.instance;
            PlayerController caster = ability != null ? ability.GetComponentInParent<PlayerController>() : null;
            if (manager == null || lobby == null || caster == null || !TeamManager.TeamMatchActive)
            {
                return null;
            }

            int team = manager.GetTeam(caster);
            manager.CollectMembers(team, _candidates);
            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                if (_candidates[i] == caster || _candidates[i].isAlive)
                {
                    _candidates.RemoveAt(i);
                }
            }
            if (_candidates.Count == 0)
            {
                return null;
            }

            Vector3 origin = caster.spiderHealthSystem != null ? caster.spiderHealthSystem.transform.position : ability.transform.position;
            Transform spawn = NearestSpawn(lobby.GetSpawnPoints(), origin);
            if (spawn == null)
            {
                return null;
            }

            PlayerController revived = _candidates[Random.Range(0, _candidates.Count)];
            _candidates.Clear();
            revived.SpawnCharacter(spawn.position, spawn.rotation);
            Logger.LogInfo($"SpiderTeams: {TeamManager.DisplayName(caster)} revived {TeamManager.DisplayName(revived)}");
            Announcements.Broadcast($"{TeamManager.RichName(caster)} revived {TeamManager.RichName(revived)}", TeamPalette.TeamColor(team), false);
            return revived;
        }

        public static GameObject RandomWeapon()
        {
            var versus = VersusMode.instance;
            if (versus == null || !versus.GameModeActive())
            {
                return null;
            }

            var weapons = versus.GetWeapons();
            return weapons != null && weapons.Count > 0 ? versus.GetRandomWeapon(false) : null;
        }

        // A remote player's abilities run through SpiderSurge on its own machine, so every client must run the mod.
        private static bool AllPlayersLocal()
        {
            var players = TeamManager.Players;
            if (players == null)
            {
                return true;
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && !players[i].IsOwner && !SpiderSurge.Common.Net.ClientHasMod(players[i].OwnerClientId))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Apply(int ability)
        {
            _touched = true;
            try
            {
                if (!SurgeBridge.ResetPerks())
                {
                    return false;
                }

                SurgeBridge.SetPerkLevel(Consts.Surge.AbilityPerks[ability], 1);
                if (ModConfig.SurgeUltimate)
                {
                    SurgeBridge.SetPerkLevel(Consts.Surge.UltimatePerks[ability], 1);
                }
                SetLevel(Consts.Surge.CooldownPerk, ModConfig.SurgeCooldown);
                SetLevel(Consts.Surge.DurationPerk, ModConfig.SurgeDuration);
                SetLevel(Consts.Surge.ShortTermPerk, ModConfig.SurgeShortTerm ? 1 : 0);
                SetLevel(Consts.Surge.LongTermPerk, ModConfig.SurgeLongTerm ? 1 : 0);

                // Every round is a fresh spawn, so nobody's Null Field starts on its respawn cooldown and no stored weapon carries over.
                SurgeBridge.ResetSpawnTracking();
                SurgeBridge.ClearStoredWeapons();
                SurgeBridge.ForgetWeaponSpawners();
                return SurgeBridge.SetRunActive(true);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams could not hand out Surge abilities: {ex}");
                return false;
            }
        }

        private static void SetLevel(string perk, int level)
        {
            if (level > 0)
            {
                SurgeBridge.SetPerkLevel(perk, level);
            }
        }

        private static int Pick(SurgeAbility setting)
        {
            if (setting != SurgeAbility.Random)
            {
                return (int)setting;
            }

            int count = Consts.Surge.AbilityPerks.Length;
            if (_lastPick < 0)
            {
                return Random.Range(0, count);
            }
            int pick = Random.Range(0, count - 1);
            return pick >= _lastPick ? pick + 1 : pick;
        }

        private static Transform NearestSpawn(Transform[] points, Vector3 origin)
        {
            if (points == null)
            {
                return null;
            }

            Transform best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null)
                {
                    continue;
                }

                float distance = (points[i].position - origin).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = points[i];
                }
            }
            return best;
        }
    }
}
