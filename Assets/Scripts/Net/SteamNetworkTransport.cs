using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Netcode transport over Steam's relay network (ISteamNetworkingSockets, P2P). No port forwarding,
    /// no server: the host listens on its SteamID and clients connect to it.
    /// Call PrepareHost()/PrepareClient() before StartHost()/StartClient().
    /// </summary>
    public class SteamNetworkTransport : NetworkTransport
    {
        public override ulong ServerClientId => 0;

        const int VirtualPort = 0;
        // Per-client receive budget on the host: a modified client cannot flood the host.
        const double BurstBytes = 1024 * 1024, RefillBytesPerSecond = 128 * 1024;

        /// <summary>Host: the most connections (accepted plus still being accepted) it will have at once; further ones are turned away.</summary>
        public int MaxConnections = 8;

        // Traffic counters (read by the QA tests and diagnostics).
        public long BytesSent, BytesReceived, MessagesSent, MessagesReceived, SendFailures;

        bool isServer;
        HSteamListenSocket listenSocket = HSteamListenSocket.Invalid;
        HSteamNetConnection clientConnection = HSteamNetConnection.Invalid;
        CSteamID pendingHost;

        // Netcode client id <-> Steam connection. Id 0 is the server, so clients start at 1.
        readonly Dictionary<ulong, HSteamNetConnection> idToConnection = new();
        readonly Dictionary<uint, ulong> connectionToId = new();
        readonly Dictionary<ulong, Budget> budgets = new();
        readonly HashSet<uint> accepting = new();   // accepted, but not connected yet
        ulong nextClientId = 1;
        float nextSendWarning;

        readonly Queue<(NetworkEvent type, ulong clientId)> pendingEvents = new();
        Callback<SteamNetConnectionStatusChangedCallback_t> statusChanged;

        // One reusable receive buffer: Netcode consumes the payload before the next PollEvent.
        byte[] receiveBuffer = new byte[2048];
        static readonly IntPtr[] messagePtr = new IntPtr[1];

        public override void Initialize(NetworkManager networkManager = null) { }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Dev only: use Steam's socket layer over loopback IP instead of P2P. Steam refuses a P2P
        /// connection to your own account, so this lets two copies on one machine test the real transport.
        /// </summary>
        [HideInInspector] public bool devDirectIp;
        const ushort DevPort = 27777;
#endif

        public void PrepareHost() { isServer = true; }

        public void PrepareClient(ulong hostSteamId64)
        {
            isServer = false;
            pendingHost = new CSteamID(hostSteamId64);
        }

        /// <summary>Host only: the Steam account behind a connected Netcode client (for join checks).</summary>
        public bool TryGetRemoteSteamId(ulong clientId, out CSteamID id)
        {
            id = CSteamID.Nil;
            if (!idToConnection.TryGetValue(clientId, out var conn)) return false;
            if (!SteamNetworkingSockets.GetConnectionInfo(conn, out var info)) return false;
            id = info.m_identityRemote.GetSteamID();
            return id.IsValid();
        }

        // ------------------------------------------------------------------ start / stop

        // The first connection over Steam's relay can take a while to set up, and a hiccup of a few seconds must not drop a player.
        static void ConfigureTimeouts()
        {
            SetGlobalInt(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial, 25000);
            SetGlobalInt(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected, 15000);
        }

        static void SetGlobalInt(ESteamNetworkingConfigValue setting, int value)
        {
            var ptr = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(ptr, value);
                SteamNetworkingUtils.SetConfigValue(setting, ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero,
                                                    ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, ptr);
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        public override bool StartServer()
        {
            isServer = true;
            if (!SteamAPI.IsSteamRunning()) { Debug.LogError("[SteamTransport] Steam is not running."); return false; }
            SteamNetworkingUtils.InitRelayNetworkAccess();
            ConfigureTimeouts();
            statusChanged ??= Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (devDirectIp)
            {
                var any = new SteamNetworkingIPAddr();
                any.Clear();
                any.m_port = DevPort;
                listenSocket = SteamNetworkingSockets.CreateListenSocketIP(ref any, 0, null);
                return listenSocket != HSteamListenSocket.Invalid;
            }
#endif
            listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(VirtualPort, 0, null);
            return listenSocket != HSteamListenSocket.Invalid;
        }

        public override bool StartClient()
        {
            isServer = false;
            if (!SteamAPI.IsSteamRunning()) { Debug.LogError("[SteamTransport] Steam is not running."); return false; }
            SteamNetworkingUtils.InitRelayNetworkAccess();
            ConfigureTimeouts();
            statusChanged ??= Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (devDirectIp)
            {
                var local = new SteamNetworkingIPAddr();
                local.Clear();
                local.ParseString("127.0.0.1:" + DevPort);
                clientConnection = SteamNetworkingSockets.ConnectByIPAddress(ref local, 0, null);
                return clientConnection != HSteamNetConnection.Invalid;
            }
#endif
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(pendingHost);
            clientConnection = SteamNetworkingSockets.ConnectP2P(ref identity, VirtualPort, 0, null);
            return clientConnection != HSteamNetConnection.Invalid;
        }

        public override void Shutdown()
        {
            foreach (var c in idToConnection.Values) SteamNetworkingSockets.CloseConnection(c, 0, "Server shutting down", false);
            idToConnection.Clear(); connectionToId.Clear(); budgets.Clear(); accepting.Clear();
            if (listenSocket != HSteamListenSocket.Invalid)
            {
                SteamNetworkingSockets.CloseListenSocket(listenSocket);
                listenSocket = HSteamListenSocket.Invalid;
            }
            if (clientConnection != HSteamNetConnection.Invalid)
            {
                SteamNetworkingSockets.CloseConnection(clientConnection, 0, "Shutting down", false);
                clientConnection = HSteamNetConnection.Invalid;
            }
            pendingEvents.Clear();
            nextClientId = 1;
        }

        void OnDestroy() { statusChanged?.Dispose(); }

        // ------------------------------------------------------------------ connection events

        void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t d)
        {
            var state = d.m_info.m_eState;
            bool closed = state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer
                       || state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally;

            if (isServer)
            {
                if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting
                    && d.m_info.m_hListenSocket == listenSocket)
                {
                    // Anybody who knows the host's Steam id can try to connect, lobby member or not: do not let strangers fill the host up.
                    if (!ShouldAccept(d.m_info))
                    {
                        SteamNetworkingSockets.CloseConnection(d.m_hConn, 0, "Not accepted", false);
                        return;
                    }
                    accepting.Add(d.m_hConn.m_HSteamNetConnection);
                    var r = SteamNetworkingSockets.AcceptConnection(d.m_hConn);
                    if (r != EResult.k_EResultOK) { accepting.Remove(d.m_hConn.m_HSteamNetConnection); Debug.LogWarning($"[SteamTransport] AcceptConnection failed: {r}"); }
                }
                else if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                {
                    accepting.Remove(d.m_hConn.m_HSteamNetConnection);
                    if (connectionToId.ContainsKey(d.m_hConn.m_HSteamNetConnection)) return;
                    ulong id = nextClientId++;
                    idToConnection[id] = d.m_hConn;
                    connectionToId[d.m_hConn.m_HSteamNetConnection] = id;
                    pendingEvents.Enqueue((NetworkEvent.Connect, id));
                }
                else if (closed)
                {
                    accepting.Remove(d.m_hConn.m_HSteamNetConnection);
                    if (connectionToId.TryGetValue(d.m_hConn.m_HSteamNetConnection, out var id))
                    {
                        pendingEvents.Enqueue((NetworkEvent.Disconnect, id));
                        Forget(id, d.m_hConn);
                    }
                    SteamNetworkingSockets.CloseConnection(d.m_hConn, 0, null, false);
                }
            }
            else
            {
                if (d.m_hConn.m_HSteamNetConnection != clientConnection.m_HSteamNetConnection) return;
                if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                    pendingEvents.Enqueue((NetworkEvent.Connect, ServerClientId));
                else if (closed)
                {
                    pendingEvents.Enqueue((NetworkEvent.Disconnect, ServerClientId));
                    SteamNetworkingSockets.CloseConnection(d.m_hConn, 0, null, false);
                }
            }
        }

        /// <summary>Host: room for one more, and not the Steam account that already has a connection here.</summary>
        bool ShouldAccept(SteamNetConnectionInfo_t info)
        {
            if (idToConnection.Count + accepting.Count >= MaxConnections)
            {
                Debug.LogWarning("[SteamTransport] Turning a connection away: the host is full.");
                return false;
            }
            var who = info.m_identityRemote.GetSteamID();
            bool oneConnectionPerAccount = who.IsValid();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (devDirectIp) oneConnectionPerAccount = false;   // the loopback test runs every player on one machine, so on one Steam account
#endif
            if (oneConnectionPerAccount)
            {
                foreach (var existing in idToConnection.Values)
                    if (SteamNetworkingSockets.GetConnectionInfo(existing, out var other) && other.m_identityRemote.GetSteamID() == who)
                    {
                        Debug.LogWarning("[SteamTransport] Turning a connection away: that Steam account is already connected.");
                        return false;
                    }
            }
            return true;
        }

        void Forget(ulong id, HSteamNetConnection conn)
        {
            connectionToId.Remove(conn.m_HSteamNetConnection);
            idToConnection.Remove(id);
            budgets.Remove(id);
        }

        // ------------------------------------------------------------------ data

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery delivery)
        {
            if (!TryGetConnection(clientId, out var conn)) return;
            int flags = delivery == NetworkDelivery.Unreliable || delivery == NetworkDelivery.UnreliableSequenced
                ? Constants.k_nSteamNetworkingSend_Unreliable
                : Constants.k_nSteamNetworkingSend_Reliable;

            var handle = GCHandle.Alloc(payload.Array, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject() + payload.Offset;
                var result = SteamNetworkingSockets.SendMessageToConnection(conn, ptr, (uint)payload.Count, flags, out _);
                if (result == EResult.k_EResultOK) { BytesSent += payload.Count; MessagesSent++; }
                else
                {
                    SendFailures++;
                    if (Time.unscaledTime >= nextSendWarning)
                    {
                        nextSendWarning = Time.unscaledTime + 5f;
                        Debug.LogWarning($"[SteamTransport] A message to {clientId} was not sent: {result} ({SendFailures} so far).");
                    }
                }
            }
            finally { handle.Free(); }
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            receiveTime = Time.realtimeSinceStartup;
            payload = default;

            if (pendingEvents.Count > 0)
            {
                var e = pendingEvents.Dequeue();
                clientId = e.clientId;
                return e.type;
            }

            if (isServer)
            {
                ulong flooder = 0; bool hasFlooder = false;
                foreach (var pair in idToConnection)
                {
                    var res = TryReceive(pair.Value, pair.Key, true, out payload);
                    if (res == Receive.Data) { clientId = pair.Key; return NetworkEvent.Data; }
                    if (res == Receive.OverBudget) { flooder = pair.Key; hasFlooder = true; break; }
                }
                if (hasFlooder) DropFlooder(flooder);
            }
            else if (clientConnection != HSteamNetConnection.Invalid
                     && TryReceive(clientConnection, ServerClientId, false, out payload) == Receive.Data)
            {
                clientId = ServerClientId;
                return NetworkEvent.Data;
            }

            clientId = 0;
            return NetworkEvent.Nothing;
        }

        enum Receive { None, Data, OverBudget }

        Receive TryReceive(HSteamNetConnection conn, ulong clientId, bool limit, out ArraySegment<byte> payload)
        {
            payload = default;
            if (SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, messagePtr, 1) <= 0) return Receive.None;

            var msg = SteamNetworkingMessage_t.FromIntPtr(messagePtr[0]);
            if (limit && !WithinBudget(clientId, msg.m_cbSize))
            {
                SteamNetworkingMessage_t.Release(messagePtr[0]);
                return Receive.OverBudget;
            }
            BytesReceived += msg.m_cbSize; MessagesReceived++;
            if (receiveBuffer.Length < msg.m_cbSize) receiveBuffer = new byte[Mathf.NextPowerOfTwo(msg.m_cbSize)];
            if (msg.m_cbSize > 0) Marshal.Copy(msg.m_pData, receiveBuffer, 0, msg.m_cbSize);
            SteamNetworkingMessage_t.Release(messagePtr[0]);
            payload = new ArraySegment<byte>(receiveBuffer, 0, msg.m_cbSize);
            return Receive.Data;
        }

        bool WithinBudget(ulong clientId, int bytes)
        {
            if (!budgets.TryGetValue(clientId, out var b)) budgets[clientId] = b = new Budget(BurstBytes);
            return b.TryConsume(bytes, Time.realtimeSinceStartupAsDouble, BurstBytes, RefillBytesPerSecond);
        }

        void DropFlooder(ulong id)
        {
            if (!idToConnection.TryGetValue(id, out var conn)) return;
            Debug.LogWarning($"[SteamTransport] Client {id} exceeded its receive budget; disconnecting it.");
            SteamNetworkingSockets.CloseConnection(conn, 0, "Sending too much data", false);
            pendingEvents.Enqueue((NetworkEvent.Disconnect, id));
            Forget(id, conn);
        }

        // Token bucket: burst capacity, refilled at a steady rate.
        class Budget
        {
            double tokens, last = -1;
            public Budget(double start) { tokens = start; }
            public bool TryConsume(int bytes, double now, double capacity, double refill)
            {
                if (last >= 0) tokens = Math.Min(capacity, tokens + (now - last) * refill);
                last = now;
                if (tokens < bytes) return false;
                tokens -= bytes;
                return true;
            }
        }

        bool TryGetConnection(ulong clientId, out HSteamNetConnection conn)
        {
            if (isServer) return idToConnection.TryGetValue(clientId, out conn);
            conn = clientConnection;
            return clientId == ServerClientId && conn != HSteamNetConnection.Invalid;
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (!isServer || !idToConnection.TryGetValue(clientId, out var conn)) return;
            SteamNetworkingSockets.CloseConnection(conn, 0, "Disconnected by host", false);
            Forget(clientId, conn);
        }

        public override void DisconnectLocalClient()
        {
            if (isServer || clientConnection == HSteamNetConnection.Invalid) return;
            SteamNetworkingSockets.CloseConnection(clientConnection, 0, "Left the game", false);
            clientConnection = HSteamNetConnection.Invalid;
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            if (!TryGetConnection(clientId, out var conn)) return 0;
            var status = new SteamNetConnectionRealTimeStatus_t();
            var lane = new SteamNetConnectionRealTimeLaneStatus_t();
            return SteamNetworkingSockets.GetConnectionRealTimeStatus(conn, ref status, 0, ref lane) == EResult.k_EResultOK
                ? (ulong)Mathf.Max(0, status.m_nPing) : 0;
        }
    }
}