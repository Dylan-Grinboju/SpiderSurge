using System;
using System.IO;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // The host sends the meter's numbers to every client and tells them what was bought; clients only ask the host to buy
    public static class MeterNet
    {
        private const string StateChannel = "surge.meter.state";
        private const string BuyChannel = "surge.meter.buy";
        private const string BoughtChannel = "surge.meter.bought";
        private const string RefusedChannel = "surge.meter.refused";

        private static bool _initialized;
        private static bool _dirty;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Net.Register(StateChannel, OnState);
            Net.Register(BuyChannel, OnBuy);
            Net.Register(BoughtChannel, OnBought);
            Net.Register(RefusedChannel, OnRefused);
            Net.PeerReady += OnPeerReady;
            Net.LeftHost += OnLeftHost;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;
            Net.Unregister(StateChannel);
            Net.Unregister(BuyChannel);
            Net.Unregister(BoughtChannel);
            Net.Unregister(RefusedChannel);
            Net.PeerReady -= OnPeerReady;
            Net.LeftHost -= OnLeftHost;
        }

        public static void MarkDirty()
        {
            if (Net.IsHost) _dirty = true;
        }

        // Called once per frame from SurgeGameModeManager.LateUpdate
        public static void Flush()
        {
            if (!_dirty) return;
            _dirty = false;
            if (!Net.HasRemotePlayers) return;
            Net.SendToAll(StateChannel, WriteState);
        }

        // Offline the handler runs at once on this machine, so one path serves both
        public static void SendBuy(ShopItem item, int buyerId)
        {
            Net.SendToServer(BuyChannel, writer =>
            {
                writer.Write((byte)item);
                writer.Write(buyerId);
            });
        }

        public static void SendBought(ShopItem item, int buyerId, string detail)
        {
            if (!Net.HasRemotePlayers) return;
            Net.SendToAll(BoughtChannel, writer =>
            {
                writer.Write((byte)item);
                writer.Write(buyerId);
                writer.Write(detail ?? string.Empty);
            });
        }

        private static void WriteState(BinaryWriter writer)
        {
            writer.Write(SurgeMeter.Charge);
            writer.Write(SurgeMeter.Points);
            writer.Write(SurgeMeter.ChargePerPoint);
            writer.Write(SurgeMeter.TotalCharge);
            writer.Write(SurgeMeter.PointsEarnedThisWave);
            writer.Write((byte)PerkShop.Offer.Length);
            foreach (ShopItem item in PerkShop.Offer) writer.Write((byte)item);
        }

        private static void OnPeerReady(ulong clientId)
        {
            Net.SendTo(clientId, StateChannel, WriteState);
        }

        private static void OnLeftHost()
        {
            SurgeMeter.ResetRun();
        }

        private static void OnState(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient || sender != Net.ServerId) return;
            try
            {
                int charge = reader.ReadInt32();
                int points = reader.ReadInt32();
                int perPoint = reader.ReadInt32();
                int total = reader.ReadInt32();
                int earnedThisWave = reader.ReadInt32();
                var offer = new ShopItem[reader.ReadByte()];
                for (int i = 0; i < offer.Length; i++) offer[i] = (ShopItem)reader.ReadByte();
                PerkShop.ApplyRemoteOffer(offer);
                SurgeMeter.ApplyRemote(charge, points, perPoint, total, earnedThisWave);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge could not read the meter state: {ex}");
            }
        }

        // A client may only buy in the name of its own players, and hears why when the host turns a buy down
        private static void OnBuy(ulong sender, BinaryReader reader)
        {
            if (!Net.IsAuthority) return;
            var item = (ShopItem)reader.ReadByte();
            int buyerId = reader.ReadInt32();
            if (sender != Net.ServerId && buyerId >= 0 && !Players.OwnedBy(buyerId, sender))
            {
                Logger.LogWarning($"SpiderSurge: client {sender} tried to buy from the shop as P{buyerId + 1}, who is not theirs");
                return;
            }
            string refused = PerkShop.Buy(item, buyerId);
            if (refused == null) return;
            if (sender == Net.ServerId) PerkShop.ShowRefused(item, refused);
            else Net.SendTo(sender, RefusedChannel, writer =>
            {
                writer.Write((byte)item);
                writer.Write(refused);
            });
        }

        private static void OnBought(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient || sender != Net.ServerId) return;
            var item = (ShopItem)reader.ReadByte();
            int buyerId = reader.ReadInt32();
            string detail = reader.ReadString();
            PerkShop.ShowBought(item, buyerId, detail);
        }

        private static void OnRefused(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient || sender != Net.ServerId) return;
            var item = (ShopItem)reader.ReadByte();
            PerkShop.ShowRefused(item, reader.ReadString());
        }
    }
}
