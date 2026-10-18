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
    // Vanilla peers drop the message silently, so each side learns who runs the mod through the Hello/Welcome handshake.
    public static class Net
    {
        public delegate void Handler(ulong sender, BinaryReader reader);

        public const string MessageName = "SpiderSurge";
        public const ulong ServerId = 0UL;
        private const string HelloChannel = "core.hello";
        private const string WelcomeChannel = "core.welcome";
        private const string SettingsChannel = "core.settings";
        private const string ConnectPrefix = "SpiderSurge/";
        private const int MaxPayload = 63000;

        private static readonly Dictionary<uint, (string name, Handler handler)> _handlers = new Dictionary<uint, (string, Handler)>();
        private static readonly HashSet<ulong> _moddedClients = new HashSet<ulong>();
        private static readonly HashSet<ulong> _announcedClients = new HashSet<ulong>();
        private static readonly Dictionary<ulong, string> _clientVersions = new Dictionary<ulong, string>();
        private static readonly List<ulong> _targets = new List<ulong>();
        private static CustomMessagingManager _boundTo;
        private static NetworkManager _subscribedTo;
        private static bool _registered;

        public static event Action<ulong> PeerReady;
        public static event Action<ulong> PeerLeft;
        public static event Action JoinedHost;
        public static event Action LeftHost;

        public static bool HostHasMod { get; private set; }
        public static string HostVersion { get; private set; }

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

        public static IReadOnlyCollection<ulong> ModdedClients => _moddedClients;

        public static bool ClientHasMod(ulong clientId)
        {
            return clientId == Net.ServerId || _moddedClients.Contains(clientId);
        }

        // True from connection approval on, before the client's Hello arrives
        public static bool ClientRunsMod(ulong clientId)
        {
            return ClientHasMod(clientId) || _announcedClients.Contains(clientId);
        }

        // Another SpiderSurge version may not have the same custom enemies, so it is sent the base enemies a vanilla client gets
        public static bool ClientRunsThisVersion(ulong clientId)
        {
            return clientId == Net.ServerId || (_clientVersions.TryGetValue(clientId, out string version) && version == SpiderSurgeMod.Version);
        }

        internal static void PrepareConnect(NetworkManager manager)
        {
            if (manager != null)
            {
                manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(ConnectPrefix + SpiderSurgeMod.Version);
            }
        }

        internal static void OnApproval(byte[] connectionData, ulong clientId)
        {
            if (clientId == Net.ServerId || connectionData == null || connectionData.Length < ConnectPrefix.Length)
            {
                return;
            }
            string announced = Encoding.UTF8.GetString(connectionData);
            if (announced.StartsWith(ConnectPrefix, StringComparison.Ordinal))
            {
                _announcedClients.Add(clientId);
                _clientVersions[clientId] = announced.Substring(ConnectPrefix.Length);
            }
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
            if (!HostHasMod)
            {
                return;
            }
            Send(channel, Net.ServerId, Payload(write));
        }

        // The host sends to every client that runs the mod; Netcode never delivers to the host itself
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
            byte[] payload = Payload(write);
            if (includeLocal)
            {
                Invoke(channel, LocalClientId, payload);
            }
            _targets.Clear();
            foreach (ulong client in Manager.ConnectedClientsIds)
            {
                if (client != Net.ServerId && _moddedClients.Contains(client))
                {
                    _targets.Add(client);
                }
            }
            if (_targets.Count > 0)
            {
                Send(channel, _targets, payload);
            }
        }

        public static void SendTo(ulong clientId, string channel, Action<BinaryWriter> write)
        {
            if (!IsHost || clientId == Net.ServerId || !_moddedClients.Contains(clientId))
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
            Register(HelloChannel, OnHello);
            Register(WelcomeChannel, OnWelcome);
            Register(SettingsChannel, OnSettings);
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
            bool wasJoined = HostHasMod;
            _moddedClients.Clear();
            _announcedClients.Clear();
            _clientVersions.Clear();
            HostHasMod = false;
            HostVersion = null;
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
                    Send(HelloChannel, Net.ServerId, Payload(writer => writer.Write(SpiderSurgeMod.Version)));
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
                    _announcedClients.Remove(clientId);
                    _clientVersions.Remove(clientId);
                    if (_moddedClients.Remove(clientId))
                    {
                        Raise(PeerLeft, clientId);
                    }
                }
                else if (HostHasMod)
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
            if (!IsHost || sender == Net.ServerId)
            {
                return;
            }
            string version = reader.ReadString();
            _moddedClients.Add(sender);
            _clientVersions[sender] = version;
            Logger.LogInfo($"SpiderSurge {version} joined as client {sender}");
            if (version != SpiderSurgeMod.Version)
            {
                Logger.LogWarning($"Client {sender} runs SpiderSurge {version}, this host runs {SpiderSurgeMod.Version}");
            }
            SendTo(sender, WelcomeChannel, writer =>
            {
                writer.Write(SpiderSurgeMod.Version);
                WriteSharedSettings(writer, null);
            });
            Raise(PeerReady, sender);
        }

        private static void OnWelcome(ulong sender, BinaryReader reader)
        {
            if (!IsRemoteClient)
            {
                return;
            }
            HostVersion = reader.ReadString();
            HostHasMod = true;
            ReadSharedSettings(reader);
            Logger.LogInfo($"Joined a host running SpiderSurge {HostVersion}");
            if (HostVersion != SpiderSurgeMod.Version)
            {
                MainThread.PopupLater($"The host runs SpiderSurge {HostVersion} and you run {SpiderSurgeMod.Version}. Update so online features match.");
            }
            Raise(JoinedHost);
        }

        private static void OnSettings(ulong sender, BinaryReader reader)
        {
            if (IsRemoteClient && sender == Net.ServerId)
            {
                ReadSharedSettings(reader);
            }
        }

        private static void OnSettingChanged(string key)
        {
            if (!HasRemotePlayers || _moddedClients.Count == 0)
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

        private static void Raise(Action handler)
        {
            if (handler == null)
            {
                return;
            }
            foreach (Delegate listener in handler.GetInvocationList())
            {
                try
                {
                    ((Action)listener)();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"SpiderSurge net listener failed: {ex}");
                }
            }
        }

        private static void Raise(Action<ulong> handler, ulong clientId)
        {
            if (handler == null)
            {
                return;
            }
            foreach (Delegate listener in handler.GetInvocationList())
            {
                try
                {
                    ((Action<ulong>)listener)(clientId);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"SpiderSurge net listener failed: {ex}");
                }
            }
        }
    }
}
