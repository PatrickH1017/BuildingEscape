using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Shows the "calling for backup" circle: a ring that fills as a guard radios for help (the player's 2 seconds to get out of view).
    /// A big one at the top of the screen for the guard furthest along, and a small one above each calling
    /// guard's head. When it is full, backup has been called. Created automatically. The drawing is <see cref="HudWidgets.CallRing"/>.
    /// </summary>
    public class BackupHud : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<BackupHud>()) return;
            var go = new GameObject("BackupHud");
            DontDestroyOnLoad(go);
            go.AddComponent<BackupHud>();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var hm = HeistManager.Instance;
            if (!hm) return;
            float s = HudTheme.S;

            if (Time.time < hm.BackupMessageUntil && !string.IsNullOrEmpty(hm.BackupMessage))
                HudWidgets.Notice(hm.BackupMessage, 292f * s, HudTheme.Danger, 24f, HudAnim.Id.Notice);
            if (hm.Phase != HeistPhase.Heist && hm.Phase != HeistPhase.FloorComplete) return;

            float best = 0f; GuardAI bestGuard = null;
            var cam = Camera.main;
            foreach (var g in GuardAI.All)
            {
                if (!g || !g.IsCalling) continue;
                float p = g.CallProgress;
                if (p > best) { best = p; bestGuard = g; }
                if (!cam) continue;
                var sp = cam.WorldToScreenPoint(g.transform.position + Vector3.up * 2.7f);
                if (sp.z <= 0f) continue;
                HudWidgets.CallRing(new Vector2(sp.x, Screen.height - sp.y), 22f, p, false);
            }
            if (!bestGuard) return;
            // the big ring sits at the top; under a quota-met banner it moves below it
            float y = (hm.Phase == HeistPhase.FloorComplete ? 380f : 142f) * s;
            HudWidgets.CallRing(new Vector2(Screen.width * 0.5f, y), 62f, best, true);
            HudWidgets.Notice("He is checking it out - get out of his view before he calls backup!", y + 86f * s, HudTheme.Warning, 19f, HudAnim.Id.Checking);
        }
    }
}
