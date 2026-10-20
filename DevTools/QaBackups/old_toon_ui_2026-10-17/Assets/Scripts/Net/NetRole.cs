using Unity.Netcode;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Who simulates the world. Offline and the host run all gameplay rules (guards, loot physics,
    /// banking, the vendor); a remote client only simulates its own player and shows what the host sends.
    /// </summary>
    public static class NetRole
    {
        /// <summary>True on a connected client that is not the host: gameplay objects are puppets here.</summary>
        public static bool IsRemoteClient
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening && !nm.IsServer;
            }
        }

        /// <summary>True in a networked run on the host.</summary>
        public static bool IsHost
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening && nm.IsServer;
            }
        }

        /// <summary>Offline or host: this machine decides what happens.</summary>
        public static bool Simulates => !IsRemoteClient;

        /// <summary>True while a networked run is on and the world sync object exists.</summary>
        public static bool Synced => NetSession.Online && RunSync.Instance != null;
    }
}