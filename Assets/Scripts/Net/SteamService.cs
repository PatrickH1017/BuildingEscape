using Steamworks;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Owns the Steam API lifetime: initialise once, pump callbacks every frame, shut down on quit.
    /// Everything else asks this class whether Steam is usable.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SteamService : MonoBehaviour
    {
        public static SteamService Instance { get; private set; }

        public bool Ready { get; private set; }
        public string Error { get; private set; }
        public CSteamID LocalId { get; private set; }
        public string LocalName { get; private set; } = "Player";

        void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
            TryInit();
        }

        /// <summary>Tries again, e.g. after the player started Steam.</summary>
        public void Retry() { if (!Ready) TryInit(); }

        void TryInit()
        {
            try
            {
                Ready = SteamAPI.Init();
                Error = Ready ? null : "Steam is not running, or steam_appid.txt is missing. Start Steam and press Retry.";
            }
            catch (System.Exception e)
            {
                Ready = false;
                Error = "Steam could not be loaded: " + e.Message;
            }
            if (!Ready) return;
            LocalId = SteamUser.GetSteamID();
            LocalName = SteamFriends.GetPersonaName();
        }

        void Update()
        {
            if (!Ready) return;
            try { SteamAPI.RunCallbacks(); }
            catch (System.InvalidOperationException)
            {
                // An Editor script reload resets the Steamworks.NET dispatcher while Steam itself stays up.
                // Attach again instead of throwing every frame; lobby callbacks registered earlier are gone.
                Ready = false;
                TryInit();
                Debug.LogWarning("[Steam] Callback dispatcher was reset (script reload?). Re-attached; rejoin the lobby.");
            }
        }

        void OnApplicationQuit()
        {
            if (!Ready) return;
            SteamAPI.Shutdown();
            Ready = false;
        }
    }
}