using System;
using System.Collections.Generic;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    // Tints spiders through the vanilla no-save cosmetics RPC, only in match levels where the customizer can't save the tint.
    public class TeamVisuals : MonoBehaviour
    {
        public static TeamVisuals Instance { get; private set; }

        private sealed class Entry
        {
            public SkinData Original;
            public Color Applied;
            public float LastSend;
        }

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private float _nextTick;
        private bool _loggedError;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            ModConfig.Changed += OnConfigChanged;
        }

        private void OnDisable()
        {
            ModConfig.Changed -= OnConfigChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnConfigChanged()
        {
            _nextTick = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextTick)
            {
                return;
            }
            _nextTick = Time.unscaledTime + Consts.Timing.RecolorInterval;
            Reconcile(false);
        }

        // Called from StartRound before SpawnPlayers, so new spiders spawn already tinted.
        public void ApplyBeforeSpawn()
        {
            Reconcile(true);
        }

        public static Color OriginalPrimary(PlayerController player)
        {
            if (player == null)
            {
                return Color.white;
            }

            Entry entry;
            var visuals = Instance;
            if (visuals != null && visuals._entries.TryGetValue(player.playerID.Value, out entry) && entry.Original != null)
            {
                return entry.Original.primary;
            }
            return player.spiderCosmetics != null ? player.spiderCosmetics.primary : Color.white;
        }

        public void Forget(PlayerController player)
        {
            if (player != null)
            {
                _entries.Remove(player.playerID.Value);
            }
        }

        public void RestoreAll()
        {
            try
            {
                if (_entries.Count == 0)
                {
                    return;
                }

                var players = Players.All;
                if (players != null && Net.IsHost)
                {
                    for (int i = 0; i < players.Count; i++)
                    {
                        var player = players[i];
                        Entry entry;
                        if (player == null || !_entries.TryGetValue(player.playerID.Value, out entry))
                        {
                            continue;
                        }

                        var current = player.spiderCosmetics;
                        if (current != null && current.primary != entry.Applied)
                        {
                            continue;
                        }
                        player.SetSpiderCosmeticsClientRpc(Clone(entry.Original), LiveSpiderId(player));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams could not restore spider colors: {ex}");
            }
            finally
            {
                _entries.Clear();
            }
        }

        private static bool ShouldTint(bool inLevel)
        {
            if (!ModConfig.RecolorSpiders || !TeamManager.HostTeamMatch)
            {
                return false;
            }
            return inLevel || (!LobbyController.inLobby && !LobbyController.inWaitingRoom);
        }

        private void Reconcile(bool inLevel)
        {
            try
            {
                if (ShouldTint(inLevel))
                {
                    TintAll();
                }
                else if (_entries.Count > 0)
                {
                    RestoreAll();
                }
            }
            catch (Exception ex)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Logger.LogError($"SpiderTeams recolor failed: {ex}");
                }
            }
        }

        // Anything that changed a tinted skin since the last send becomes that player's new original.
        private void TintAll()
        {
            var manager = TeamManager.Instance;
            var players = Players.All;
            if (manager == null || players == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player.spiderCosmetics == null)
                {
                    continue;
                }

                int team = manager.GetTeam(player);
                if (team < 0)
                {
                    continue;
                }

                Color tint = TeamPalette.MemberColor(team, manager.GetMemberRank(player));
                SkinData current = player.spiderCosmetics;
                Entry entry;
                if (!_entries.TryGetValue(player.playerID.Value, out entry))
                {
                    entry = new Entry { Original = Clone(current) };
                    _entries[player.playerID.Value] = entry;
                    Send(player, entry, tint, now);
                    continue;
                }

                if (now - entry.LastSend < Consts.Timing.RecolorSendCooldown)
                {
                    continue;
                }

                if (current.primary != entry.Applied)
                {
                    entry.Original = Clone(current);
                    Send(player, entry, tint, now);
                    continue;
                }

                if (current.secondary != entry.Original.secondary || !current.hatId.Equals(entry.Original.hatId))
                {
                    entry.Original.secondary = current.secondary;
                    entry.Original.hatId = current.hatId;
                }
                if (tint != entry.Applied)
                {
                    Send(player, entry, tint, now);
                }
            }
        }

        private static void Send(PlayerController player, Entry entry, Color tint, float now)
        {
            SkinData skin = Clone(entry.Original);
            skin.primary = tint;
            entry.Applied = tint;
            entry.LastSend = now;
            player.SetSpiderCosmeticsClientRpc(skin, LiveSpiderId(player));
        }

        // The RPC relinks the player to whatever object this id names, so only a spawned spider of this player is passed.
        private static ulong LiveSpiderId(PlayerController player)
        {
            GameObject spider = ReflectionCache.ActiveCharacter(player);
            if (spider == null)
            {
                return 0;
            }

            var networkObject = spider.GetComponent<NetworkObject>();
            return networkObject != null && networkObject.IsSpawned ? networkObject.NetworkObjectId : 0;
        }

        private static SkinData Clone(SkinData source)
        {
            return new SkinData
            {
                controllerName = source.controllerName,
                primary = source.primary,
                secondary = source.secondary,
                hatId = source.hatId
            };
        }
    }
}
