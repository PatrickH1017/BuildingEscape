using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Shows the "calling for backup" circle: a ring that fills as a guard radios for help (the player's 2 seconds to get out of view).
    /// A big one at the top of the screen for the guard furthest along, and a small one above each calling
    /// guard's head. When it is full, backup has been called. Created automatically.
    /// </summary>
    public class BackupHud : MonoBehaviour
    {
        static readonly Color Alarm = new Color(1f, 0.15f, 0.1f);
        static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color MessageColour = new Color(1f, 0.3f, 0.2f);

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

            if (Time.time < hm.BackupMessageUntil && !string.IsNullOrEmpty(hm.BackupMessage))
                HudText.Label(new Rect(0, 215f, Screen.width, 40f), hm.BackupMessage,
                    HudStyles.Label(26, TextAnchor.MiddleCenter, FontStyle.Bold, MessageColour));
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
                DrawRing(new Vector2(sp.x, Screen.height - sp.y), 22f, p, 5f, false);
            }
            if (bestGuard) DrawRing(new Vector2(Screen.width / 2f, 130f), 52f, best, 9f, true);
        }

        /// <summary>A circular loading bar: a dim full circle with a red arc filling clockwise from the top.</summary>
        static void DrawRing(Vector2 c, float radius, float progress, float thickness, bool big)
        {
            int segs = big ? 72 : 36;                // the small rings above the guards' heads do not need as many pieces
            var white = Texture2D.whiteTexture;
            var old = GUI.color;
            float segLen = 2f * Mathf.PI * radius / segs + 1.5f;

            for (int i = 0; i < segs; i++)
            {
                bool filled = (i + 0.5f) / segs <= progress;
                GUI.color = filled ? Alarm : Dim;
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(i * 360f / segs, c);
                GUI.DrawTexture(new Rect(c.x - segLen / 2f, c.y - radius - thickness / 2f, segLen, thickness), white);
                GUI.matrix = m;
            }

            // the icon in the middle: a warning mark (a radio call going out)
            GUI.color = Color.white;
            GUI.Label(new Rect(c.x - radius, c.y - radius, radius * 2f, radius * 2f), "!",
                HudStyles.Label(big ? 44 : 20, TextAnchor.MiddleCenter, FontStyle.Bold, progress >= 1f ? Alarm : Color.white));

            if (big)
                HudText.Label(new Rect(c.x - 350f, c.y + radius + 6f, 700f, 28f),
                    "He is checking it out - get out of his view before he calls backup!",
                    HudStyles.Label(18, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white));
            GUI.color = old;
        }
    }
}
