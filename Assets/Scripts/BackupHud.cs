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
            {
                var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Bold };
                st.normal.textColor = new Color(1f, 0.3f, 0.2f);
                HudText.Label(new Rect(0, 215f, Screen.width, 40f), hm.BackupMessage, st);
            }
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
            const int segs = 72;
            var white = Texture2D.whiteTexture;
            var old = GUI.color;
            float segLen = 2f * Mathf.PI * radius / segs + 1.5f;

            for (int i = 0; i < segs; i++)
            {
                bool filled = (i + 0.5f) / segs <= progress;
                GUI.color = filled ? new Color(1f, 0.15f, 0.1f, 1f) : new Color(0f, 0f, 0f, 0.55f);
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(i * 360f / segs, c);
                GUI.DrawTexture(new Rect(c.x - segLen / 2f, c.y - radius - thickness / 2f, segLen, thickness), white);
                GUI.matrix = m;
            }

            // the icon in the middle: a warning mark (a radio call going out)
            GUI.color = progress >= 1f ? new Color(1f, 0.15f, 0.1f) : Color.white;
            var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = big ? 44 : 20, fontStyle = FontStyle.Bold };
            st.normal.textColor = GUI.color;
            GUI.color = Color.white;
            GUI.Label(new Rect(c.x - radius, c.y - radius, radius * 2f, radius * 2f), "!", st);

            if (big)
            {
                var cap = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
                cap.normal.textColor = Color.white;
                HudText.Label(new Rect(c.x - 350f, c.y + radius + 6f, 700f, 28f),
                    "He is checking it out - get out of his view before he calls backup!", cap);
            }
            GUI.color = old;
        }
    }
}
