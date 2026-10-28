using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    // One named Netcode message carries every mod message: a channel hash, then a byte payload written with BinaryWriter.
    // Every peer runs this SpiderSurge version, since the host refuses anyone else; a client says Hello once its handlers are bound.
    public static class Net
    {
        public delegate void Handler(ulong sender, BinaryReader reader);
        public delegate void Receiver(BinaryReader reader);

        public const string MessageName = "SpiderSurge";
        public const ulong ServerId = 0UL;
        private const string HelloChannel = "core.hello";
        private const string WelcomeChannel = "core.welcome";
        private const string SettingsChannel = "core.settings";
        private const string ConnectPrefix = "SpiderSurge/";
        private const int MaxPayload = 63000;

        private static readonly Dictionary<uint, (string name, Handler handler)> _handlers = new Dictionary<uint, (string, Handler)>();
        private static readonly HashSet<ulong> _readyClients = new HashSet<ulong>();
        private static readonly List<ulong> _targets = new List<ulong>();
        private static CustomMessagingManager _boundTo;
        private static NetworkManager _subscribedTo;
        private static bool _registered;
        private static bool _joinedHost;

        public static event Action<ulong> PeerReady;
        public static event Action<ulong> PeerLeft;
        public static event Action JoinedHost;
        public static event Action LeftHost;

        public static NetworkManager Manager => NetworkManager.Singleton;

        public static bool Available
        {
            get
            {
                var manager = Manager;
                return manager != null && manager.IsListening;
            }
        }

        public static bool IsHost => Available && Manager.IsHost;

        public static bool IsRemoteClient => Available && Manager.IsClient && !Manager.IsHost;

        // Offline play runs the game as its own host, so authority means "not a remote client"
        public static bool IsAuthority => !IsRemoteClient;

        public static bool HasRemotePlayers => IsHost && Manager.ConnectedClientsIds.Count > 1;

        public static bool IsOnline => IsRemoteClient || HasRemotePlayers;

        public static ulong LocalClientId => Available ? Manager.LocalClientId : Net.ServerId;

        internal static void PrepareConnect(NetworkManager manager)
        {
            if (manager != null)
            {
                manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(ConnectPrefix + SpiderSurgeMod.Version);
            }
        }

        // Mixed lobbies are not supported, so a client without this SpiderSurge version is refused
        internal static bool Approves(byte[] connectionData, ulong clientId)
        {
            if (clientId == Net.ServerId)
            {
                return true;
            }
            string announced = connectionData == null ? string.Empty : Encoding.UTF8.GetString(connectionData);
            string version = announced.StartsWith(ConnectPrefix, StringComparison.Ordinal) ? announced.Substring(ConnectPrefix.Length) : null;
            if (version != null && WithoutBuild(version) == WithoutBuild(SpiderSurgeMod.Version))
            {
                return true;
            }
            Logger.LogWarning($"Refused client {clientId}: it runs {(version == null ? "no SpiderSurge" : "SpiderSurge " + version)}, this host runs SpiderSurge {SpiderSurgeMod.Version}");
            return false;
        }

        // Build metadata such as "+local" says where a build was made, not what it contains
        private static string WithoutBuild(string version)
        {
            int plus = version.IndexOf('+');
            return plus < 0 ? version : version.Substring(0, plus);
        }

        public static void Register(string channel, Handler handler)
        {
            uint hash = Hash(channel);
            if (_handlers.TryGetValue(hash, out var existing) && existing.name != channel)
            {
                Logger.LogError($"SpiderSurge net channel '{channel}' collides with '{existing.name}'");
                return;
            }
            _handlers[hash] = (channel, handler);
        }

        // Host to client: the receiver runs only on a remote client, for the host's messages
        public static void RegisterFromHost(string channel, Receiver receiver) => Register(channel, FromHost(receiver));

        public static Handler FromHost(Receiver receiver) => (sender, reader) =>
        {
            if (IsRemoteClient && sender == ServerId) receiver(reader);
        };

        // Client to host: the handler runs on the host, and offline too when SendToServer's local call should reach it
        public static void RegisterFromClients(string channel, Handler handler, bool offline)
        {
            Register(channel, (sender, reader) =>
            {
                if (offline ? IsAuthority : IsHost) handler(sender, reader);
            });
        }

        public static void Unregister(string channel)
        {
            _handlers.Remove(Hash(channel));
        }

        // A client sends to the host; the host runs the handler at once as its own sender
        public static void SendToServer(string channel, Action<BinaryWriter> write)
        {
            if (!IsRemoteClient)
            {
                Invoke(channel, LocalClientId, Payload(write));
                return;
            }
            Send(channel, Net.ServerId, Payload(write));
        }

        // The host sends to every client that has said Hello; Netcode never delivers to the host itself
        public static void SendToAll(string channel, Action<BinaryWriter> write, bool includeLocal = false)
        {
            if (!IsHost)
            {
                if (includeLocal && !Available)
                {
                    Invoke(channel, LocalClientId, Payload(write));
                }
                return;
            }
            if (!includeLocal && !HasReadyClient())
            {
                return;
            }
            byte[] payload = Payload(write);
            if (includeLocal)
            {
                Invoke(channel, LocalClientId, payload);
            }
            _targets.Clear();
            foreach (ulong client in Manager.ConnectedClientsIds)
            {
                if (client != Net.ServerId && _readyClients.Contains(client))
                {
                    _targets.Add(client);
                }
            }
            if (_targets.Count > 0)
            {
                Send(channel, _targets, payload);
            }
        }

        private static bool HasReadyClient()
        {
            foreach (ulong client in Manager.ConnectedClientsIds)
            {
                if (client != Net.ServerId && _readyClients.Contains(client))
                {
                    return true;
                }
            }
            return false;
        }

        public static void SendTo(ulong clientId, string channel, Action<BinaryWriter> write)
        {
            if (!IsHost || clientId == Net.ServerId || !_readyClients.Contains(clientId))
            {
                return;
            }
            Send(channel, clientId, Payload(write));
        }

        internal static void Initialize()
        {
            if (_registered)
            {
                return;
            }
            _registered = true;
            RegisterFromClients(HelloChannel, OnHello, offline: false);
            RegisterFromHost(WelcomeChannel, OnWelcome);
            RegisterFromHost(SettingsChannel, ReadSharedSettings);
            Settings.Changed += OnSettingChanged;
            Bind();
        }

        internal static void Shutdown()
        {
            Settings.Changed -= OnSettingChanged;
            if (_boundTo != null)
            {
                try
                {
                    _boundTo.UnregisterNamedMessageHandler(MessageName);
                }
                catch
                {
                }
            }
            if (_subscribedTo != null)
            {
                _subscribedTo.OnClientConnectedCallback -= OnClientConnected;
                _subscribedTo.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            _boundTo = null;
            _subscribedTo = null;
            _registered = false;
            _handlers.Clear();
            ResetSession();
        }

        // Netcode makes a new CustomMessagingManager on every StartHost/StartClient, so the handler is bound again each time
        internal static void Bind()
        {
            var manager = Manager;
            if (manager == null)
            {
                return;
            }

            if (_subscribedTo != manager)
            {
                if (_subscribedTo != null)
                {
                    _subscribedTo.OnClientConnectedCallback -= OnClientConnected;
                    _subscribedTo.OnClientDisconnectCallback -= OnClientDisconnected;
                }
                _subscribedTo = manager;
                manager.OnClientConnectedCallback += OnClientConnected;
                manager.OnClientDisconnectCallback += OnClientDisconnected;
            }

            var messaging = manager.CustomMessagingManager;
            if (messaging == null || messaging == _boundTo)
            {
                return;
            }
            _boundTo = messaging;
            messaging.RegisterNamedMessageHandler(MessageName, OnMessage);
            ResetSession();
            Logger.LogInfo($"SpiderSurge net bound ({(manager.IsHost ? "host" : manager.IsClient ? "client" : "starting")})");
        }

        internal static void Tick()
        {
            var manager = Manager;
            if (manager != null && manager.CustomMessagingManager != _boundTo)
            {
                Bind();
            }
        }

        private static void ResetSession()
        {
            bool wasJoined = _joinedHost;
            _readyClients.Clear();
            _joinedHost = false;
            Settings.ClearRemote();
            if (wasJoined)
            {
                Raise(LeftHost);
            }
        }

        private static void OnClientConnected(ulong clientId)
        {
            try
            {
                if (IsRemoteClient && clientId == LocalClientId)
                {
                    Send(HelloChannel, Net.ServerId, Payload(null));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge net connect handling failed: {ex}");
            }
        }

        private static void OnClientDisconnected(ulong clientId)
        {
            try
            {
                if (IsHost)
                {
                    if (_readyClients.Remove(clientId))
                    {
                        Raise(PeerLeft, clientId);
                    }
                }
                else if (_joinedHost)
                {
                    ResetSession();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge net disconnect handling failed: {ex}");
            }
        }

        private static void OnHello(ulong sender, BinaryReader reader)
        {
            if (sender == Net.ServerId)
            {
                return;
            }
            _readyClients.Add(sender);
            Logger.LogInfo($"SpiderSurge client {sender} is ready");
            SendTo(sender, WelcomeChannel, writer => WriteSharedSettings(writer, null));
            Raise(PeerReady, sender);
        }

        private static void OnWelcome(BinaryReader reader)
        {
            _joinedHost = true;
            ReadSharedSettings(reader);
            Logger.LogInfo("Joined a SpiderSurge host");
            Raise(JoinedHost);
        }

        private static void OnSettingChanged(string key)
        {
            if (!HasRemotePlayers || _readyClients.Count == 0)
            {
                return;
            }
            if (key != null && !Parts.IsShared(key))
            {
                return;
            }
            SendToAll(SettingsChannel, writer => WriteSharedSettings(writer, key));
        }

        private static void WriteSharedSettings(BinaryWriter writer, string onlyUnder)
        {
            var shared = Settings.Keys.Where(key => Parts.IsShared(key) && Settings.Affects(onlyUnder, key)).ToList();
            writer.Write(shared.Count);
            foreach (string key in shared)
            {
                writer.Write(key);
                writer.Write(Settings.Raw(key) ?? string.Empty);
            }
        }

        private static void ReadSharedSettings(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var values = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                values[key] = reader.ReadString();
            }
            Settings.ApplyRemote(values);
        }

        private static void OnMessage(ulong sender, FastBufferReader payload)
        {
            uint hash = 0;
            try
            {
                // A client only ever hears from the host, but Netcode maps the host's transport id to a client id of its own
                // (EOS gives the host transport id 2, which becomes client id 1 on the client), so it is named ServerId here
                if (IsRemoteClient)
                {
                    sender = Net.ServerId;
                }
                payload.ReadValueSafe(out hash);
                payload.ReadValueSafe(out byte[] bytes);
                Invoke(hash, sender, bytes);
            }
            catch (Exception ex)
            {
                string name = _handlers.TryGetValue(hash, out var entry) ? entry.name : hash.ToString();
                Logger.LogError($"SpiderSurge net message '{name}' from {sender} failed: {ex}");
            }
        }

        private static void Invoke(string channel, ulong sender, byte[] payload)
        {
            try
            {
                Invoke(Hash(channel), sender, payload);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge net handler '{channel}' failed: {ex}");
            }
        }

        private static void Invoke(uint hash, ulong sender, byte[] payload)
        {
            if (!_handlers.TryGetValue(hash, out var entry))
            {
                return;
            }
            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream))
            {
                entry.handler(sender, reader);
            }
        }

        private static byte[] Payload(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                write?.Invoke(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void Send(string channel, ulong clientId, byte[] payload)
        {
            var messaging = Messaging();
            if (messaging == null)
            {
                return;
            }
            if (!Fits(channel, payload))
            {
                return;
            }
            var writer = Writer(channel, payload);
            try
            {
                messaging.SendNamedMessage(MessageName, clientId, writer, Delivery(payload));
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge net message '{channel}' could not be sent: {ex.Message}");
            }
            finally
            {
                writer.Dispose();
            }
        }

        private static void Send(string channel, List<ulong> clientIds, byte[] payload)
        {
            var messaging = Messaging();
            if (messaging == null)
            {
                return;
            }
            if (!Fits(channel, payload))
            {
                return;
            }
            var writer = Writer(channel, payload);
            try
            {
                messaging.SendNamedMessage(MessageName, clientIds, writer, Delivery(payload));
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge net message '{channel}' could not be sent: {ex.Message}");
            }
            finally
            {
                writer.Dispose();
            }
        }

        private static CustomMessagingManager Messaging()
        {
            Bind();
            return Available ? Manager.CustomMessagingManager : null;
        }

        // Netcode refuses a fragmented message over 64000 bytes
        private static bool Fits(string channel, byte[] payload)
        {
            if (payload.Length <= MaxPayload)
            {
                return true;
            }
            Logger.LogWarning($"SpiderSurge net message '{channel}' is too large to send ({payload.Length} bytes)");
            return false;
        }

        private static FastBufferWriter Writer(string channel, byte[] payload)
        {
            var writer = new FastBufferWriter(payload.Length + 16, Allocator.Temp, MaxPayload + 16);
            uint hash = Hash(channel);
            writer.WriteValueSafe(hash);
            writer.WriteValueSafe(payload);
            return writer;
        }

        private static NetworkDelivery Delivery(byte[] payload)
        {
            return payload.Length > 1200 ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.ReliableSequenced;
        }

        // FNV-1a, computed the same on every machine
        public static uint Hash(string text)
        {
            uint hash = 2166136261u;
            foreach (char c in text)
            {
                hash = (hash ^ c) * 16777619u;
            }
            return hash;
        }

        private static void Raise(Action handler) => Listeners.Raise(handler, "SpiderSurge net listener failed");

        private static void Raise(Action<ulong> handler, ulong clientId) => Listeners.Raise(handler, clientId, "SpiderSurge net listener failed");
    }
}
