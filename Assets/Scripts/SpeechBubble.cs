using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Draws a comic speech bubble on screen above a point in the world (cream box, dark outline, a little tail pointing down at
    /// the speaker). Shared by the vendor and the guards. Only call it from OnGUI during a Repaint event.
    /// </summary>
    public static class SpeechBubble
    {
        static Texture2D white;
        static GUIStyle style;
        static readonly GUIContent content = new GUIContent();

        /// <summary>
        /// Screen position (GUI space: y down) of the point the bubble hangs from, or false when the point is behind the camera.
        /// </summary>
        public static bool ToGui(Camera cam, Vector3 worldPoint, out Vector2 gui)
        {
            gui = default;
            if (!cam) return false;
            var sp = cam.WorldToScreenPoint(worldPoint);
            if (sp.z <= 0.1f) return false;
            gui = new Vector2(sp.x, Screen.height - sp.y);
            return true;
        }

        /// <summary>
        /// Draws the bubble with its tail at <paramref name="anchor"/> (GUI-space point, see <see cref="ToGui"/>).
        /// <paramref name="fontAt720p"/> and <paramref name="maxWidthAt720p"/> scale with the window height.
        /// </summary>
        public static void Draw(Vector2 anchor, string text, float alpha, float fontAt720p = 20f, float maxWidthAt720p = 340f)
        {
            if (alpha < 0.01f || string.IsNullOrEmpty(text)) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
            if (!white) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); white.hideFlags = HideFlags.DontUnloadUnusedAsset; }

            float s = Screen.height / 720f;
            style.fontSize = Mathf.RoundToInt(fontAt720p * s);
            style.normal.textColor = new Color(0.1f, 0.1f, 0.12f, alpha);

            float padX = 16f * s, padY = 10f * s;
            content.text = text;
            float w = Mathf.Min(maxWidthAt720p * s, style.CalcSize(content).x + padX * 2f);
            float h = style.CalcHeight(content, w - padX * 2f) + padY * 2f;
            float x = anchor.x - w / 2f, y = anchor.y - h - 14f * s;
            x = Mathf.Clamp(x, 6f * s, Screen.width - w - 6f * s);

            var old = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.12f, alpha);
            GUI.DrawTexture(new Rect(x - 2f * s, y - 2f * s, w + 4f * s, h + 4f * s), white);   // outline
            GUI.color = new Color(1f, 1f, 0.94f, alpha);
            GUI.DrawTexture(new Rect(x, y, w, h), white);
            // the little tail pointing at the speaker
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(anchor.x, y + h));
            GUI.DrawTexture(new Rect(anchor.x - 7f * s, y + h - 7f * s, 14f * s, 14f * s), white);
            GUI.matrix = m;
            GUI.color = old;
            GUI.Label(new Rect(x, y, w, h), content, style);
        }
    }
}
