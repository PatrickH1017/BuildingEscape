using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Draws on-screen messages so they are never cut off. A plain GUI.Label wraps a long message onto a second line
    /// and then clips it to its rectangle (which was only one line high), so the tops and bottoms of the letters
    /// disappeared on smaller windows. HudText.Label instead:
    ///   1. keeps the message on ONE line and shrinks the font until it fits the width (never below MinFont),
    ///   2. only wraps if it is still too wide at the smallest size,
    ///   3. makes the rectangle as tall as the text needs (growing around the middle for centred text),
    ///   4. never lets clipping cut glyphs, and keeps the rectangle inside the window.
    /// Use it exactly like GUI.Label(rect, text, style).
    /// </summary>
    public static class HudText
    {
        public const int MinFont = 12;

        public static void Label(Rect r, string text, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            var s = new GUIStyle(style) { clipping = TextClipping.Overflow, wordWrap = false };
            var content = new GUIContent(text);
            float maxW = Mathf.Max(60f, Mathf.Min(r.width, Screen.width - 8f) - 8f);

            int size = s.fontSize > 0 ? s.fontSize : 14;
            float w = s.CalcSize(content).x;
            if (w > maxW)
            {
                size = Mathf.Max(MinFont, Mathf.FloorToInt(size * maxW / w));
                s.fontSize = size;
                w = s.CalcSize(content).x;
            }

            float h;
            if (w > maxW) { s.wordWrap = true; h = s.CalcHeight(content, maxW); } // too long even when small: wrap it
            else h = s.CalcSize(content).y;
            h += 4f; // room above and below the letters

            var rr = new Rect(r.x, r.y, r.width, Mathf.Max(r.height, h));
            bool middle = s.alignment == TextAnchor.MiddleLeft || s.alignment == TextAnchor.MiddleCenter || s.alignment == TextAnchor.MiddleRight;
            if (middle) rr.y = r.y + (r.height - rr.height) * 0.5f;
            if (rr.yMax > Screen.height - 2f) rr.y = Screen.height - 2f - rr.height;
            if (rr.y < 2f) rr.y = 2f;
            GUI.Label(rr, content, s);
        }
    }
}
