using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// On-screen feedback for pinned / busted players and the repeating backup call: a big "PINNED" countdown when the
    /// local player is held down, a "BUSTED" notice when they were arrested, a tag over every teammate who is down,
    /// and "backup in N" once backup has been called. Created automatically (like BackupHud). The drawing is in <see cref="HudWidgets"/>.
    /// </summary>
    public class CustodyHud : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<CustodyHud>()) return;
            var go = new GameObject("CustodyHud");
            DontDestroyOnLoad(go);
            go.AddComponent<CustodyHud>();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var hm = HeistManager.Instance;
            if (!hm) return;

            // backup: the next officer is on a timer once anyone has called
            if (hm.Phase == HeistPhase.Heist && hm.BackupActive && hm.BackupIn >= 0) HudWidgets.BackupChip(hm.BackupIn);

            if (hm.Phase != HeistPhase.Heist && hm.Phase != HeistPhase.FloorComplete && hm.Phase != HeistPhase.Hub) return;

            var cam = Camera.main;
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p || !p.InCustody) continue;
                bool mine = Net.NetPlayer.IsLocalPlayer(p);
                if (mine) DrawMine(p);
                else if (cam) DrawLabel(cam, p);
            }
        }

        /// <summary>The local player's own state, big in the middle of the screen.</summary>
        static void DrawMine(WobblyPlayer p)
        {
            if (p.Custody == CustodyState.Pinned)
                HudWidgets.CustodyMine(true, Mathf.CeilToInt(p.PinLeft), Mathf.Clamp01(p.PinLeft / CustodyRules.PinSeconds),
                    "A teammate has to hit the officer to get you free");
            else
            {
                int price = CustodyRules.BuyBackPrice(p.BustedOnLevel);
                HudWidgets.CustodyMine(false, 0, 0f, $"Your team can buy you back from the vendor at the Loading Docks (${price})", price);
            }
        }

        /// <summary>A tag above a teammate who is down.</summary>
        static void DrawLabel(Camera cam, WobblyPlayer p)
        {
            var sp = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 1.6f);
            if (sp.z <= 0.1f) return;
            HudWidgets.CustodyTag(new Vector2(sp.x, Screen.height - sp.y), p.Custody == CustodyState.Pinned, Mathf.CeilToInt(p.PinLeft));
        }
    }
}
