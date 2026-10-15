using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// On-screen feedback for pinned / busted players and the repeating backup call: a big "PINNED" countdown when the
    /// local player is held down, a "BUSTED" notice when they were arrested, a label over every teammate who is down,
    /// and "next officer in N s" once backup has been called. Created automatically (like BackupHud).
    /// </summary>
    public class CustodyHud : MonoBehaviour
    {
        static readonly Color BackupColour = new Color(1f, 0.55f, 0.35f);
        static readonly Color PinnedFresh = new Color(1f, 0.85f, 0.2f), PinnedAlmostOut = new Color(1f, 0.2f, 0.15f);
        static readonly Color BustedColour = new Color(0.85f, 0.85f, 0.9f);
        static readonly Color PinnedTag = new Color(1f, 0.3f, 0.2f), BustedTag = new Color(0.8f, 0.8f, 0.85f);
        const int PinnedColourSteps = 8;      // the big countdown fades yellow -> red in this many steps (each step is one cached style)

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
            if (hm.Phase == HeistPhase.Heist && hm.BackupActive && hm.BackupIn >= 0)
                HudText.Label(new Rect(0, 255f, Screen.width, 30f), $"BACKUP: another officer in {hm.BackupIn} s",
                    HudStyles.Label(20, TextAnchor.MiddleCenter, FontStyle.Bold, BackupColour));

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
            var small = HudStyles.Label(22, TextAnchor.MiddleCenter);
            float y = Screen.height * 0.30f;
            if (p.Custody == CustodyState.Pinned)
            {
                int left = Mathf.CeilToInt(p.PinLeft);
                float fresh = Mathf.Round(Mathf.Clamp01(p.PinLeft / CustodyRules.PinSeconds) * PinnedColourSteps) / PinnedColourSteps;
                var big = HudStyles.Label(54, TextAnchor.MiddleCenter, FontStyle.Bold, Color.Lerp(PinnedAlmostOut, PinnedFresh, fresh));
                HudText.Label(new Rect(0, y, Screen.width, 70f), $"PINNED  {left}", big);
                HudText.Label(new Rect(0, y + 64f, Screen.width, 32f), "A teammate has to hit the officer to get you free", small);
            }
            else
            {
                var big = HudStyles.Label(54, TextAnchor.MiddleCenter, FontStyle.Bold, BustedColour);
                HudText.Label(new Rect(0, y, Screen.width, 70f), "BUSTED", big);
                HudText.Label(new Rect(0, y + 64f, Screen.width, 32f),
                    $"Your team can buy you back from the vendor at the Loading Docks (${CustodyRules.BuyBackPrice(p.BustedOnLevel)})", small);
            }
        }

        /// <summary>A label above a teammate who is down.</summary>
        static void DrawLabel(Camera cam, WobblyPlayer p)
        {
            var sp = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 1.6f);
            if (sp.z <= 0.1f) return;
            bool pinned = p.Custody == CustodyState.Pinned;
            var st = HudStyles.Label(22, TextAnchor.MiddleCenter, FontStyle.Bold, pinned ? PinnedTag : BustedTag);
            string text = pinned ? $"PINNED {Mathf.CeilToInt(p.PinLeft)}" : "BUSTED";
            HudText.Label(new Rect(sp.x - 120f, Screen.height - sp.y - 16f, 240f, 32f), text, st);
        }
    }
}
