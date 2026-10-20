using System.Collections.Generic;
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
    ///
    /// It also makes no garbage. OnGUI runs several times a frame (layout, repaint, every mouse and key event), and a new
    /// GUIStyle (a native object) for every label every time was a steady stream of it. So a label is only measured and
    /// drawn on the Repaint event (the only one that draws anything), and one scratch style and one GUIContent are
    /// reused for all of them. Get the style you pass in from <see cref="HudStyles"/>.
    /// </summary>
    public static class HudText
    {
        public const int MinFont = 12;

        static GUIStyle scratch;
        static readonly GUIContent content = new GUIContent();

        public static void Label(Rect r, string text, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            var ev = Event.current;
            if (ev != null && ev.type != EventType.Repaint) return;   // a label only draws on Repaint

            // the scratch style takes everything that shows from the one given, and then never clips or wraps (unless it must)
            var s = scratch ??= new GUIStyle();
            s.font = style.font;
            s.fontSize = style.fontSize;
            s.fontStyle = style.fontStyle;
            s.alignment = style.alignment;
            s.richText = style.richText;
            s.padding = style.padding;
            s.normal.textColor = style.normal.textColor;
            s.clipping = TextClipping.Overflow;
            s.wordWrap = false;
            content.text = text;
            float maxW = Mathf.Max(60f, Mathf.Min(r.width, Screen.width - 8f) - 8f);

            int size = s.fontSize > 0 ? s.fontSize : 14;
            var measured = s.CalcSize(content);
            float w = measured.x;
            if (w > maxW)
            {
                size = Mathf.Max(MinFont, Mathf.FloorToInt(size * maxW / w));
                s.fontSize = size;
                measured = s.CalcSize(content);
                w = measured.x;
            }

            float h;
            if (w > maxW) { s.wordWrap = true; h = s.CalcHeight(content, maxW); } // too long even when small: wrap it
            else h = measured.y;
            h += 4f; // room above and below the letters

            var rr = new Rect(r.x, r.y, r.width, Mathf.Max(r.height, h));
            bool middle = s.alignment == TextAnchor.MiddleLeft || s.alignment == TextAnchor.MiddleCenter || s.alignment == TextAnchor.MiddleRight;
            if (middle) rr.y = r.y + (r.height - rr.height) * 0.5f;
            if (rr.yMax > Screen.height - 2f) rr.y = Screen.height - 2f - rr.height;
            if (rr.y < 2f) rr.y = 2f;
            GUI.Label(rr, content, s);
        }
    }

    /// <summary>
    /// Label styles shared by every on-screen message, so no OnGUI has to make its own copy of the skin's label every frame.
    /// A style is made the first time it is asked for and then kept (one per size / alignment / weight / colour).
    /// Do not change what you get back. Keep the colours to a few fixed ones (a fading colour would make a style per step).
    /// </summary>
    public static class HudStyles
    {
        static readonly Dictionary<ulong, GUIStyle> cache = new Dictionary<ulong, GUIStyle>();

        /// <summary>A label in the skin's own text colour.</summary>
        public static GUIStyle Label(int size, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle fontStyle = FontStyle.Normal) =>
            Get(size, anchor, fontStyle, false, default);

        /// <summary>A label in the given colour.</summary>
        public static GUIStyle Label(int size, TextAnchor anchor, FontStyle fontStyle, Color color) =>
            Get(size, anchor, fontStyle, true, color);

        static GUIStyle Get(int size, TextAnchor anchor, FontStyle fontStyle, bool tinted, Color32 tint)
        {
            ulong key = (uint)size | ((ulong)(int)anchor << 12) | ((ulong)(int)fontStyle << 16) | (tinted ? 1UL << 20 : 0UL)
                        | ((ulong)tint.r << 24) | ((ulong)tint.g << 32) | ((ulong)tint.b << 40) | ((ulong)tint.a << 48);
            if (cache.TryGetValue(key, out var style)) return style;
            style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, fontStyle = fontStyle };
            if (tinted) style.normal.textColor = tint;
            cache[key] = style;
            return style;
        }
    }
}
