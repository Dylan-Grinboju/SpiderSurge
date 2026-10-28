using System;
using System.IO;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // The host sends the wallet and challenge state to every client and tells them what was bought; clients only ask the host to buy
    public static class ChaosNet
    {
        private const string StateChannel = "surge.chaos.state";
        private const string ChallengeChannel = "surge.chaos.challenge";
        private const string BuyChannel = "surge.chaos.buy";
        private const string BoughtChannel = "surge.chaos.bought";
        private const string RefusedChannel = "surge.chaos.refused";
        private const string ToastChannel = "surge.chaos.toast";

        private static readonly (string channel, Net.Handler handler)[] Channels =
        {
            (StateChannel, OnState),
            (ChallengeChannel, OnChallenge),
            (BuyChannel, OnBuy),
            (BoughtChannel, OnBought),
            (RefusedChannel, OnRefused),
            (ToastChannel, OnToast),
        };

        private static bool _initialized;
        private static bool _dirty;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            foreach (var (channel, handler) in Channels) Net.Register(channel, handler);
            Net.PeerReady += OnPeerReady;
            Net.LeftHost += OnLeftHost;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;
            foreach (var (channel, _) in Channels) Net.Unregister(channel);
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
                writer.Write(PerkShop.VisitId);
            });
        }

        public static void SendBought(ShopItem item, int buyerId, string detail)
        {
            if (!Net.HasRemotePlayers) return;
            Net.SendToAll(BoughtChannel, writer =>
            {
                writer.Write((byte)item);
                writer.Write(buyerId);
                writer.WriteText(detail);
            });
        }

        // Host only: shows a challenge result on every machine
        public static void Announce(string text)
        {
            if (Net.IsAuthority) Net.SendToAll(ToastChannel, writer => writer.Write(text), includeLocal: true);
        }

        private static void OnToast(ulong sender, BinaryReader reader)
        {
            if (sender == Net.ServerId) Toast.Show(reader.ReadString(), Hud.Pink);
        }

        private static void WriteState(BinaryWriter writer)
        {
            writer.Write(ChaosWallet.Points);
            writer.Write(ChaosWallet.PointsEarned);
            writer.Write(ChaosWallet.PointsSpent);
            writer.Write(PerkShop.VisitId);
            writer.Write(PerkShop.PurchasedMask);
            writer.Write(PerkShop.PurchasedPerkKey);
            writer.Write(PerkShop.BonusSlot);
            writer.Write(PerkShop.BonusPaid);
            writer.Write((byte)PerkShop.Offer.Length);
            foreach (ShopItem item in PerkShop.Offer) writer.Write((byte)item);
            writer.Write((byte)ChallengeController.Offered);
            writer.Write((byte)ChallengeController.Active);
            writer.Write(ChallengeController.Pending);
            writer.Write(ChallengeController.WagerFailed);
        }

        public static void SendChallenge(bool accept)
        {
            Net.SendToServer(ChallengeChannel, writer =>
            {
                writer.Write(PerkShop.VisitId);
                writer.Write(accept);
            });
        }

        private static void OnChallenge(ulong sender, BinaryReader reader)
        {
            if (!Net.IsAuthority) return;
            ChallengeController.Decide(sender, reader.ReadInt32(), reader.ReadBoolean());
        }

        private static void OnPeerReady(ulong clientId)
        {
            Net.SendTo(clientId, StateChannel, WriteState);
        }

        private static void OnLeftHost()
        {
            ChaosWallet.ResetRun();
        }

        private static void OnState(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient) return;
            try
            {
                int points = reader.ReadInt32();
                int earned = reader.ReadInt32();
                int spent = reader.ReadInt32();
                int visit = reader.ReadInt32();
                int purchased = reader.ReadInt32();
                string purchasedPerk = reader.ReadString();
                int bonus = reader.ReadInt32();
                bool paid = reader.ReadBoolean();
                var offer = new ShopItem[reader.ReadByte()];
                for (int i = 0; i < offer.Length; i++) offer[i] = (ShopItem)reader.ReadByte();
                var offered = (ChallengeKind)reader.ReadByte();
                var active = (ChallengeKind)reader.ReadByte();
                bool pending = reader.ReadBoolean();
                bool failed = reader.ReadBoolean();
                PerkShop.ApplyRemoteOffer(visit, purchased, bonus, paid, offer, purchasedPerk);
                ChaosWallet.ApplyRemote(points, earned, spent);
                ChallengeController.ApplyRemote(offered, active, pending, failed);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge could not read the chaos state: {ex}");
            }
        }

        // A client may only buy in the name of its own players, and hears why when the host turns a buy down
        private static void OnBuy(ulong sender, BinaryReader reader)
        {
            if (!Net.IsAuthority) return;
            var item = (ShopItem)reader.ReadByte();
            int buyerId = reader.ReadInt32();
            int visit = reader.ReadInt32();
            if (sender != Net.ServerId && buyerId >= 0 && !Players.OwnedBy(buyerId, sender))
            {
                Logger.LogWarning($"SpiderSurge: client {sender} tried to buy from the shop as P{buyerId + 1}, who is not theirs");
                return;
            }
            string refused = PerkShop.Buy(item, buyerId, visit);
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
            if (!Net.IsRemoteClient) return;
            var item = (ShopItem)reader.ReadByte();
            int buyerId = reader.ReadInt32();
            string detail = reader.ReadString();
            PerkShop.ShowBought(item, buyerId, detail);
        }

        private static void OnRefused(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient) return;
            var item = (ShopItem)reader.ReadByte();
            PerkShop.ShowRefused(item, reader.ReadString());
        }
    }
}
