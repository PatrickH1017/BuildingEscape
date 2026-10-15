using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The speech bubble over the vendor's head. When your character walks up to him (back from a floor, or just
    /// into the dock) a bubble pops up with a line and fades after a few seconds; walk away and come back and it
    /// shows the next line. Local to each player's screen: nothing is sent over the network.
    ///
    /// The lines are placeholders for the game's own writing: edit <see cref="lines"/> on the Vendor object.
    /// </summary>
    public class VendorChat : MonoBehaviour
    {
        [Tooltip("What the vendor is looking at / hangs the bubble above (his head).")]
        public Transform head;
        [Tooltip("One line per visit, in order, then round again.")]
        public string[] lines = { "Shocked you made it back" };
        [Tooltip("How close a player has to be, in metres, for him to speak.")]
        public float range = 10f;
        public float showSeconds = 5f;

        int next;
        string text = "";
        float shownAt = -99f, alpha;
        bool near;

        /// <summary>True while the bubble is up (for tests).</summary>
        public bool Visible => alpha > 0.5f;
        public string Text => text;

        void Update()
        {
            float d = NearestLocalPlayer();
            if (!near && d <= range)
            {
                near = true;
                shownAt = Time.time;
                if (lines != null && lines.Length > 0) text = lines[next++ % lines.Length];
            }
            else if (near && d > range + 2f) near = false;

            bool want = near && Time.time - shownAt < showSeconds && !Vendor.ShopOpen && !EscapeMenu.Open && !string.IsNullOrEmpty(text);
            alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, Time.deltaTime * 4f);
        }

        float NearestLocalPlayer()
        {
            if (!head) return float.MaxValue;
            float best = float.MaxValue;
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p || !Net.NetPlayer.IsLocalPlayer(p)) continue;
                float d = Vector3.Distance(p.transform.position, head.position);
                if (d < best) best = d;
            }
            return best;
        }

        GUIStyle style;
        static Texture2D white;

        void OnGUI()
        {
            if (alpha < 0.01f || !head) return;
            var cam = Camera.main;
            if (!cam) return;
            var sp = cam.WorldToScreenPoint(head.position + Vector3.up * 0.75f);
            if (sp.z <= 0.1f) return;

            float s = Screen.height / 720f;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
            }
            if (!white) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            style.fontSize = Mathf.RoundToInt(20f * s);
            style.normal.textColor = new Color(0.1f, 0.1f, 0.12f, alpha);

            float maxW = 340f * s, padX = 16f * s, padY = 10f * s;
            var content = new GUIContent(text);
            float w = Mathf.Min(maxW, style.CalcSize(content).x + padX * 2f);
            float h = style.CalcHeight(content, w - padX * 2f) + padY * 2f;
            float x = sp.x - w / 2f, y = Screen.height - sp.y - h - 14f * s;
            x = Mathf.Clamp(x, 6f * s, Screen.width - w - 6f * s);

            var old = GUI.color;
            var border = new Rect(x - 2f * s, y - 2f * s, w + 4f * s, h + 4f * s);
            GUI.color = new Color(0.1f, 0.1f, 0.12f, alpha);
            GUI.DrawTexture(border, white);
            GUI.color = new Color(1f, 1f, 0.94f, alpha);
            GUI.DrawTexture(new Rect(x, y, w, h), white);
            // the little tail pointing at him
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(sp.x, y + h));
            GUI.color = new Color(1f, 1f, 0.94f, alpha);
            GUI.DrawTexture(new Rect(sp.x - 7f * s, y + h - 7f * s, 14f * s, 14f * s), white);
            GUI.matrix = m;
            GUI.color = old;
            GUI.Label(new Rect(x, y, w, h), text, style);
        }
    }
}
