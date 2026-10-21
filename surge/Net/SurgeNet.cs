using System;
using System.Collections.Generic;
using System.IO;
using SpiderSurge.Common;
using SpiderSurge.Enemies;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public enum AbilityKind : byte
    {
        Immune = 0,
        Ammo = 1,
        Pulse = 2,
        Storage = 3,
        Freeze = 4,
    }

    public enum SurgeFx : byte
    {
        PulseRing = 0,
        Immune = 1,
        Freeze = 2,
        BlackHole = 3,
        Clip = 4,
        CareDrop = 5,
    }

    public enum EnemyFx : byte
    {
        Frozen = 0,
        LaserPhase = 1,
        LaserHold = 2,
        CloudFrozen = 3,
        ShieldBroken = 4,
        Swallowed = 5,
        ShotFrozen = 6,
        ChargeCalmed = 7,
        HatchCancelled = 8,
    }

    // The host owns the run: mode, perks, phase and wave go to clients as one state message; clients ask the host to run
    // their abilities and the host echoes the visuals every machine should draw.
    public static class SurgeNet
    {
        private const string StateChannel = "surge.state";
        private const string AbilityChannel = "surge.ability";
        private const string FxChannel = "surge.fx";
        private const string EnemyChannel = "surge.enemy";
        private const string FreezeCooldownChannel = "surge.freeze.cooldown";

        private static bool _initialized;
        private static bool _dirty;
        private static int _lastRemoteWave;
        private static bool _awaitingFirstState;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;
            Net.Register(StateChannel, OnState);
            Net.Register(AbilityChannel, OnAbility);
            Net.Register(FxChannel, OnFx);
            Net.Register(EnemyChannel, OnEnemyFx);
            Net.Register(FreezeCooldownChannel, OnFreezeCooldown);
            Net.PeerReady += OnPeerReady;
            Net.JoinedHost += OnJoinedHost;
            Net.LeftHost += OnLeftHost;
            MeterNet.Initialize();
        }

        public static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }
            _initialized = false;
            Net.Unregister(StateChannel);
            Net.Unregister(AbilityChannel);
            Net.Unregister(FxChannel);
            Net.Unregister(EnemyChannel);
            Net.Unregister(FreezeCooldownChannel);
            Net.PeerReady -= OnPeerReady;
            Net.JoinedHost -= OnJoinedHost;
            Net.LeftHost -= OnLeftHost;
            MeterNet.Shutdown();
        }

        public static void MarkDirty()
        {
            if (Net.IsHost)
            {
                _dirty = true;
            }
        }

        // Called once per frame from SurgeGameModeManager.LateUpdate
        public static void Flush()
        {
            if (_dirty)
            {
                SendStateNow();
            }
        }

        public static void SendStateNow()
        {
            _dirty = false;
            if (!Net.HasRemotePlayers)
            {
                return;
            }
            Net.SendToAll(StateChannel, WriteState);
        }

        internal static void SendFreezeCooldown(int playerId, float remaining)
        {
            if (!Net.IsHost || !Net.HasRemotePlayers) return;
            Net.SendToAll(FreezeCooldownChannel, writer =>
            {
                writer.Write(playerId);
                writer.Write(remaining);
            });
        }

        private static void OnFreezeCooldown(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient || sender != Net.ServerId) return;
            int playerId = reader.ReadInt32();
            float remaining = reader.ReadSingle();
            PlayerAbilityHandler.FindAbility(playerId, AbilityKind.Freeze)?.ApplyCooldownRemaining(remaining);
        }

        public static void SendAbility(int playerId, AbilityKind kind, bool ultimate)
        {
            if (!Net.IsRemoteClient)
            {
                return;
            }
            Net.SendToServer(AbilityChannel, writer =>
            {
                writer.Write(playerId);
                writer.Write((byte)kind);
                writer.Write(ultimate);
            });
        }

        public static void SendFx(SurgeFx fx, int playerId, Vector3 position, float radius, bool flag)
        {
            if (!Net.HasRemotePlayers)
            {
                return;
            }
            Net.SendToAll(FxChannel, writer =>
            {
                writer.Write((byte)fx);
                writer.Write(playerId);
                writer.Write(position);
                writer.Write(radius);
                writer.Write(flag);
            });
        }

        // Effects on an enemy or shot that only the host runs: the freeze tint and hold, and the laser ring's timing
        public static void SendEnemyFx(EnemyFx fx, NetworkObject target, float value)
        {
            if (!Net.HasRemotePlayers || target == null || !target.IsSpawned)
            {
                return;
            }
            Net.SendToAll(EnemyChannel, writer =>
            {
                writer.Write((byte)fx);
                writer.Write(target.NetworkObjectId);
                writer.Write(value);
            });
        }

        private static void OnPeerReady(ulong clientId)
        {
            Enemies.CustomEnemyNet.RegisterHandlers();
            Net.SendTo(clientId, StateChannel, WriteState);

            // Players join a run already going, so they also get the enemies Freeze Time is holding right now; the Black Hole's holds aren't shown as frozen
            foreach (FrozenEnemy frozen in UnityEngine.Object.FindObjectsOfType<FrozenEnemy>())
            {
                NetworkObject target = frozen.GetComponent<NetworkObject>();
                float left = FrozenEnemy.TimeLeftOn(frozen.GetComponent<EnemyHealthSystem>());
                if (target == null || !target.IsSpawned || left <= 0f) continue;
                Net.SendTo(clientId, EnemyChannel, writer =>
                {
                    writer.Write((byte)EnemyFx.Frozen);
                    writer.Write(target.NetworkObjectId);
                    writer.Write(left);
                });
            }
            foreach (FrozenCloud frozen in UnityEngine.Object.FindObjectsOfType<FrozenCloud>())
            {
                NetworkObject target = frozen.GetComponent<NetworkObject>();
                float left = frozen.TimeLeft;
                if (target == null || !target.IsSpawned || left <= 0f) continue;
                Net.SendTo(clientId, EnemyChannel, writer =>
                {
                    writer.Write((byte)EnemyFx.CloudFrozen);
                    writer.Write(target.NetworkObjectId);
                    writer.Write(left);
                });
            }
            foreach (FrozenShot frozen in UnityEngine.Object.FindObjectsOfType<FrozenShot>())
            {
                NetworkObject target = frozen.GetComponent<NetworkObject>();
                float left = frozen.TimeLeft;
                if (target == null || !target.IsSpawned || left <= 0f) continue;
                Net.SendTo(clientId, EnemyChannel, writer =>
                {
                    writer.Write((byte)EnemyFx.ShotFrozen);
                    writer.Write(target.NetworkObjectId);
                    writer.Write(left);
                });
            }

            // The game sends a shield's break only as it happens, so a joiner would see every broken shield still up
            foreach (EnemyHealthSystem enemy in UnityEngine.Object.FindObjectsOfType<EnemyHealthSystem>())
            {
                NetworkObject target = enemy.NetworkObject;
                if (enemy.shield == null || enemy.shield.activeSelf || target == null || !target.IsSpawned) continue;
                Net.SendTo(clientId, EnemyChannel, writer =>
                {
                    writer.Write((byte)EnemyFx.ShieldBroken);
                    writer.Write(target.NetworkObjectId);
                    writer.Write(0f);
                });
            }
        }

        private static void OnJoinedHost()
        {
            _lastRemoteWave = 0;
            _awaitingFirstState = true;
            Enemies.CustomEnemyNet.RegisterHandlers();
        }

        private static void OnLeftHost()
        {
            _lastRemoteWave = 0;
            _awaitingFirstState = false;
            SurgeGameModeManager.Instance?.ResetRun();
            SurgeGameModeManager.SelectMode(false);
            PerksManager.Instance?.ResetPerks();
            ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Clear();
            StoragePersistenceManager.ClearAllStoredWeapons();
        }

        private static void WriteState(BinaryWriter writer)
        {
            var mode = SurgeGameModeManager.Instance;
            var perks = PerksManager.Instance;
            writer.Write(SurgeGameModeManager.IsModeEnabled);
            writer.Write(mode != null && mode.IsActive);
            writer.Write(mode != null && mode.RunUsedSurge);
            writer.Write(mode != null && mode.NoAbilitiesThisRun);
            int wave = SurvivalMode.instance != null && SurvivalMode.instance.GameModeActive() ? SurvivalMode.instance.CurrentWave : 0;
            writer.Write(wave);
            writer.Write(perks != null && perks.IsFirstNormalPerkSelection);
            writer.Write(perks != null && perks.IsUltUpgradePerkSelection);
            writer.Write(perks != null && perks.IsUltSwapPerkSelection);

            var levels = new List<KeyValuePair<string, int>>();
            if (perks != null)
            {
                foreach (string name in perks.GetAllPerkNames())
                {
                    int level = perks.GetPerkLevel(name);
                    if (level > 0)
                    {
                        levels.Add(new KeyValuePair<string, int>(name, level));
                    }
                }
            }
            writer.Write(levels.Count);
            foreach (var pair in levels)
            {
                writer.Write(pair.Key);
                writer.Write(pair.Value);
            }

            var lucky = ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys;
            writer.Write(lucky.Count);
            foreach (string key in lucky)
            {
                writer.Write(key);
            }
            writer.Write(PlayerAbilityHandler.SpawnEpoch);
        }

        private static void OnState(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient || sender != 0)
            {
                return;
            }
            try
            {
                bool modeEnabled = reader.ReadBoolean();
                bool active = reader.ReadBoolean();
                bool runUsed = reader.ReadBoolean();
                bool noAbilities = reader.ReadBoolean();
                int wave = reader.ReadInt32();
                bool first = reader.ReadBoolean();
                bool ultUpgrade = reader.ReadBoolean();
                bool ultSwap = reader.ReadBoolean();

                var levels = new Dictionary<string, int>();
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadString();
                    levels[name] = reader.ReadInt32();
                }

                var lucky = ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys;
                lucky.Clear();
                int luckyCount = reader.ReadInt32();
                for (int i = 0; i < luckyCount; i++)
                {
                    lucky.Add(reader.ReadString());
                }
                int spawnEpoch = reader.ReadInt32();

                SurgeGameModeManager.SelectMode(modeEnabled);
                SurgeGameModeManager.Instance?.ApplyRemote(active, runUsed, noAbilities);
                PerksManager.Instance?.ApplyRemote(levels, first, ultUpgrade, ultSwap);
                PlayerAbilityHandler.ApplyRemoteEpoch(spawnEpoch);

                // Spiders already alive when a client joins mid-run started before Surge was active there
                if (_awaitingFirstState)
                {
                    _awaitingFirstState = false;
                    if (active)
                    {
                        PlayerAbilityHandler.AttachToExistingSpiders();
                        PerkChoiceRefresh.Refresh();
                    }
                }

                if (wave != _lastRemoteWave)
                {
                    bool advanced = wave > _lastRemoteWave && _lastRemoteWave > 0;
                    _lastRemoteWave = wave;
                    if (advanced && active)
                    {
                        PlayerAbilityHandler.OnWaveStarted(wave);
                    }
                }
                if (!active)
                {
                    _lastRemoteWave = 0;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge could not read the surge state: {ex}");
            }
        }

        // A client may only trigger its own players' abilities; the host's instance runs the same checks the client did
        private static void OnAbility(ulong sender, BinaryReader reader)
        {
            if (!Net.IsHost)
            {
                return;
            }
            int playerId = reader.ReadInt32();
            var kind = (AbilityKind)reader.ReadByte();
            bool ultimate = reader.ReadBoolean();

            if (!Players.OwnedBy(playerId, sender))
            {
                Logger.LogWarning($"SpiderSurge: client {sender} tried to use an ability of P{playerId + 1}, who is not theirs");
                return;
            }

            var ability = PlayerAbilityHandler.FindAbility(playerId, kind);
            if (ability == null)
            {
                Logger.LogWarning($"SpiderSurge: no {kind} ability on P{playerId + 1} for client {sender}'s request");
                return;
            }
            ability.ActivateFromNetwork(ultimate);
        }

        private static void OnFx(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient || sender != 0)
            {
                return;
            }
            var fx = (SurgeFx)reader.ReadByte();
            int playerId = reader.ReadInt32();
            Vector3 position = reader.ReadVector3();
            float radius = reader.ReadSingle();
            bool flag = reader.ReadBoolean();

            // Only the host spawns a Care Package's weapons, so the caster draws the drops too
            if (fx == SurgeFx.CareDrop)
            {
                AmmoAbility.ShowRemoteDrop(position);
                return;
            }

            // A local caster already drew its own effect when it pressed the button
            if (Players.IsLocal(playerId))
            {
                return;
            }

            switch (fx)
            {
                case SurgeFx.PulseRing:
                    PulseAbility.DrawRemotePulse(playerId, position, radius, flag);
                    break;
                case SurgeFx.Immune:
                    ImmuneAbility.ShowRemoteImmunity(playerId, flag);
                    break;
                case SurgeFx.Freeze:
                    FreezeAbility.DrawRemote(playerId, position, radius, flag);
                    break;
                case SurgeFx.BlackHole:
                    StorageAbility.DrawRemoteBlackHole(playerId, radius);
                    break;
                case SurgeFx.Clip:
                    AmmoAbility.ShowRemoteClip(playerId, position, flag);
                    break;
            }
        }

        private static void OnEnemyFx(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient || sender != 0)
            {
                return;
            }
            var fx = (EnemyFx)reader.ReadByte();
            ulong objectId = reader.ReadUInt64();
            float value = reader.ReadSingle();
            if (!Net.Manager.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject target) || target == null)
            {
                return;
            }
            switch (fx)
            {
                case EnemyFx.Frozen:
                    FrozenEnemy.ShowFrozen(target.GetComponent<EnemyHealthSystem>(), value);
                    break;
                case EnemyFx.CloudFrozen:
                    FrozenCloud.ShowFrozen(target.GetComponent<DeathCloud>(), value);
                    break;
                case EnemyFx.ShotFrozen:
                    FrozenShot.ShowFrozen(target, value);
                    break;
                case EnemyFx.ChargeCalmed:
                    foreach (WhispBrain whisp in target.GetComponentsInChildren<WhispBrain>()) FrozenEnemy.CalmChargeLight(whisp);
                    break;
                case EnemyFx.HatchCancelled:
                    SpawnerBrain.ShowHatchCancelled(target);
                    break;
                case EnemyFx.LaserPhase:
                    target.GetComponentInChildren<LaserRing>(true)?.Sync(value);
                    break;
                case EnemyFx.LaserHold:
                    target.GetComponentInChildren<LaserRing>(true)?.Hold();
                    break;
                case EnemyFx.Swallowed:
                    SwallowedTarget.ShowSwallowed(target.GetComponent<EnemyHealthSystem>(), value);
                    break;
                case EnemyFx.ShieldBroken:
                    GameObject shield = target.GetComponent<EnemyHealthSystem>()?.shield;
                    if (shield != null) shield.SetActive(false);
                    break;
            }
        }
    }
}
