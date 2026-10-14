using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Marks the player that is placed in the run scene for single-player / direct Play.
    /// In a networked run the host spawns a player per client instead, so this one removes itself.
    /// </summary>
    public class OfflinePlayerOnly : MonoBehaviour
    {
        void Awake() { if (NetSession.Online) Destroy(gameObject); }
    }
}