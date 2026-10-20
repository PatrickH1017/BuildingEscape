using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The pieces of the game's menus (the lobby front end and the Esc menu) in the toon look of the HUD (<see cref="HudTheme"/>): a tall card
    /// with a ribbon for its title, chunky buttons that grow under the mouse, sliders, switches, pick-one pills, colour swatches, a text box,
    /// scrolling and wrapped text. The owner asked for both menus to match the in-run HUD (2026-10-16).
    ///
    /// Everything is drawn at absolute positions (no GUILayout), so a page is a plain list of calls; <see cref="Flow"/> hands out the
    /// rectangles from top to bottom. Call the widgets from OnGUI on EVERY event (they read the mouse on the input events and draw only on
    /// Repaint, and every widget takes its control id exactly once per event, so the ids stay in step). Sizes are in reference pixels
    /// (1080 high, see <see cref="HudTheme.S"/>); rectangles are in screen pixels.
    /// A widget that is scrolled out of view cannot be clicked: <see cref="Scroll"/> narrows <c>clip</c> to the part you can see.
    /// </summary>
    public static class MenuKit
    {
        static float S => HudTheme.S;

        // ------------------------------------------------------------------ the frame

        /// <summary>The rectangle (in the current GUI coordinates) in which clicks count; a scroll area narrows it.</summary>
        static Rect clip = new Rect(-100000f, -100000f, 200000f, 200000f);

        /// <summary>Call at the top of a menu's OnGUI: a scroll area left open by an error must not leave the mouse clipped.</summary>
        public static void Begin() => clip = new Rect(-100000f, -100000f, 200000f, 200000f);

        static bool Hit(Rect r)
        {
            var m = Event.current.mousePosition;
            return r.Contains(m) && clip.Contains(m);
        }

        static Rect Grow(Rect r, float g) => new Rect(r.x - g, r.y - g, r.width + g * 2f, r.height + g * 2f);

        // ------------------------------------------------------------------ layout

        /// <summary>Hands out rows from top to bottom: <c>Row(height)</c> returns the next rectangle and moves down by it and the gap (reference pixels).</summary>
        public sealed class Flow
        {
            public float x, y, w;

            public Flow At(float x, float y, float w) { this.x = x; this.y = y; this.w = w; return this; }

            public Rect Row(float height, float gap = 10f)
            {
                float s = S;
                var r = new Rect(x, y, w, height * s);
                y += (height + gap) * s;
                return r;
            }

            public void Gap(float height) { y += height * S; }
        }

        /// <summary>The <paramref name="i"/>-th of <paramref name="n"/> equal cells of a row, <paramref name="gap"/> reference pixels apart.</summary>
        public static Rect Cell(Rect row, int i, int n, float gap = 10f)
        {
            float g = gap * S, cw = (row.width - g * (n - 1)) / n;
            return new Rect(row.x + i * (cw + g), row.y, cw, row.height);
        }

        // ------------------------------------------------------------------ the card and its ribbon

        /// <summary>
        /// The big card a menu sits on. Like <see cref="HudTheme.Panel"/>, but the shaded band is only about a hundred pixels deep at the
        /// bottom, so a tall card does not change colour half way down.
        /// </summary>
        public static void Sheet(Rect panel, float radius = 24f)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, rad = radius * s, ow = Mathf.Max(1.5f, 4f * s), g = 2.2f * s;
            HudTheme.Rounded(new Rect(panel.x, panel.y + 6f * s, panel.width, panel.height), HudTheme.Alpha(HudTheme.Ink, 0.38f), rad);
            HudTheme.Rounded(Grow(panel, g), HudTheme.Alpha(HudTheme.Cream, 0.62f), rad + g);
            HudTheme.Rounded(panel, HudTheme.Ink, rad);
            var inner = new Rect(panel.x + ow, panel.y + ow, panel.width - ow * 2f, panel.height - ow * 2f);
            float lit = Mathf.Clamp(1f - 100f * s / Mathf.Max(1f, panel.height), 0.5f, 0.95f);
            HudTheme.Fill(inner, HudTheme.Card, Mathf.Max(0f, rad - ow), true, lit);
        }

        /// <summary>The title tab that sits across the top edge of a card.</summary>
        public static void Ribbon(Rect panel, string title, Color fill, float size = 34f)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S;
            float rw = Mathf.Min(HudTheme.Width(title, size) + 90f * s, panel.width - 40f * s);
            var r = new Rect(panel.center.x - rw * 0.5f, panel.y - 24f * s, rw, 52f * s);
            HudTheme.Body(r, fill, 20f, 3.5f, 4f, true, 1.8f);
            HudTheme.Text(r, title, size, HudTheme.Bright, TextAnchor.MiddleCenter);
        }

        /// <summary>A soft dark plate under a row of a list (a lobby, a player, a friend).</summary>
        public static void Plate(Rect r, float alpha = 0.16f)
        {
            if (Event.current.type == EventType.Repaint) HudTheme.Rounded(r, HudTheme.Alpha(HudTheme.Ink, alpha), 12f * S);
        }
        /// <summary>The screen dimmed behind a card.</summary>
        public static void Dim(float alpha)
        {
            if (Event.current.type == EventType.Repaint)
                HudTheme.Rounded(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.05f, 0.03f, 0.16f, alpha), 0f);
        }

        // ------------------------------------------------------------------ text

        /// <summary>A line of text (no outline by default: the menus are calm), shrunk to fit the rectangle's width.</summary>
        public static void Label(Rect r, string text, float size, Color colour, TextAnchor anchor = TextAnchor.MiddleLeft, bool outline = false) =>
            HudTheme.Text(r, text, size, colour, anchor, outline);

        /// <summary>A section title: a small coloured chip with a thin rule running on to the right.</summary>
        public static void Header(Rect row, string title, Color accent, Color? ink = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, h = Mathf.Min(row.height, 26f * s), w = HudTheme.Width(title, 15f) + 34f * s;
            HudTheme.Chip(new Rect(row.x, row.y + (row.height - h) * 0.5f, w, h), title, accent, 15f, ink);
            float rx = row.x + w + 8f * s;
            if (rx < row.xMax - 4f * s) HudTheme.Rounded(new Rect(rx, row.center.y - 1.5f * s, row.xMax - rx, 3f * s), HudTheme.Alpha(HudTheme.Ink, 0.35f), 1.5f * s);
        }

        /// <summary>A pill with a status light and some words (Steam: signed in as ...).</summary>
        public static void StatusPill(Rect r, string text, Color light, float size = 15f)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S;
            HudTheme.Pill(r, HudTheme.Shade(HudTheme.Card, 0.3f), 2.5f, 2f, 0f);
            HudIcons.Dot(new Vector2(r.x + r.height * 0.55f, r.center.y), r.height * 0.42f, light);
            HudTheme.Text(new Rect(r.x + r.height * 1.0f, r.y, r.width - r.height * 1.2f, r.height), text, size, HudTheme.Bright, TextAnchor.MiddleLeft, false);
        }

        /// <summary>A pill of words on a colour (a message under the title).</summary>
        public static void Notice(Rect r, string text, Color fill, float size = 15f)
        {
            if (Event.current.type != EventType.Repaint) return;
            HudTheme.Pill(r, fill, 2.5f, 2.5f, 0f);
            HudTheme.Text(r, text, size, fill == HudTheme.Quota ? HudTheme.Ink : HudTheme.Bright, TextAnchor.MiddleCenter, false);
        }

        static GUIStyle wrapStyle;
        static readonly GUIContent wrapContent = new GUIContent();

        static GUIStyle WrapStyle(int px)
        {
            wrapStyle ??= new GUIStyle();
            wrapStyle.font = HudTheme.TextFont;
            wrapStyle.fontStyle = HudTheme.TextStyle;
            wrapStyle.fontSize = px;
            wrapStyle.wordWrap = true;
            wrapStyle.richText = false;
            wrapStyle.alignment = TextAnchor.UpperLeft;
            wrapStyle.clipping = TextClipping.Overflow;
            return wrapStyle;
        }

        struct WrapKey : System.IEquatable<WrapKey>
        {
            public string text;
            public int size, width;
            public bool Equals(WrapKey o) => size == o.size && width == o.width && string.Equals(text, o.text);
            public override bool Equals(object o) => o is WrapKey k && Equals(k);
            public override int GetHashCode() => (text.GetHashCode() * 31 + size) * 31 + width;
        }

        static readonly Dictionary<WrapKey, float> wrapHeights = new Dictionary<WrapKey, float>(64);

        /// <summary>How tall a text is when it is wrapped to <paramref name="width"/> screen pixels at <paramref name="px"/> (measured once, then remembered).</summary>
        public static float WrappedHeight(string text, float px, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            int size = Mathf.Max(8, Mathf.RoundToInt(px)), w = Mathf.RoundToInt(width);
            var key = new WrapKey { text = text, size = size, width = w };
            if (wrapHeights.TryGetValue(key, out float h)) return h;
            wrapContent.text = text;
            h = WrapStyle(size).CalcHeight(wrapContent, width);
            if (wrapHeights.Count > 200) wrapHeights.Clear();
            wrapHeights[key] = h;
            return h;
        }

        static readonly Dictionary<WrapKey, string> fitted = new Dictionary<WrapKey, string>(64);

        /// <summary>
        /// The text, cut short with "..." when it would not fit <paramref name="width"/> (screen pixels) even at 80 % of <paramref name="px"/>:
        /// a long name must not shrink to a speck. Text that fits (or only needs a little shrinking) is returned as it is.
        /// </summary>
        public static string Fit(string text, float px, float width)
        {
            if (string.IsNullOrEmpty(text) || HudTheme.TextWidthPx(text, px * 0.8f) <= width) return text;
            var key = new WrapKey { text = text, size = Mathf.RoundToInt(px), width = Mathf.RoundToInt(width) };
            if (fitted.TryGetValue(key, out string cut)) return cut;
            int n = text.Length;
            while (n > 1 && HudTheme.TextWidthPx(text.Substring(0, n).TrimEnd() + "...", px * 0.8f) > width) n--;
            cut = text.Substring(0, n).TrimEnd() + "...";
            if (fitted.Count > 200) fitted.Clear();
            fitted[key] = cut;
            return cut;
        }

        /// <summary>Wrapped text in the rectangle (size in screen pixels).</summary>
        public static void Wrapped(Rect r, string text, float px, Color colour)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            var st = WrapStyle(Mathf.Max(8, Mathf.RoundToInt(px)));
            st.normal.textColor = colour;
            wrapContent.text = text;
            GUI.Label(r, wrapContent, st);
        }

        // ------------------------------------------------------------------ buttons

        /// <summary>
        /// A press on a rectangle: true on the event the mouse is released over it after being pressed over it. <paramref name="hover"/> and
        /// <paramref name="down"/> are for drawing. A disabled control never takes the mouse.
        /// </summary>
        static bool Press(Rect r, bool enabled, out bool hover, out bool down)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var ev = Event.current;
            bool inside = Hit(r), mine = GUIUtility.hotControl == id;
            hover = enabled && inside && (GUIUtility.hotControl == 0 || mine);
            down = mine && inside;
            bool clicked = false;
            switch (ev.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (enabled && inside && ev.button == 0) { GUIUtility.hotControl = id; ev.Use(); }
                    break;
                case EventType.MouseUp:
                    if (mine) { GUIUtility.hotControl = 0; clicked = inside && enabled; ev.Use(); }
                    break;
            }
            return clicked;
        }

        /// <summary>A chunky toon button; it grows under the mouse and sinks when pressed. Returns true when clicked.</summary>
        public static bool Button(Rect r, string text, Color fill, float size = 24f, bool enabled = true, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            bool clicked = Press(r, enabled, out bool hover, out bool down);
            if (Event.current.type == EventType.Repaint)
            {
                float s = S;
                var rr = Grow(r, !enabled ? 0f : down ? -2f * s : hover ? 3f * s : 0f);
                Color c = !enabled ? HudTheme.Dim : hover ? HudTheme.Light(fill, 0.16f) : fill;
                HudTheme.Body(rr, c, Mathf.Min(18f, r.height / s * 0.3f), 3.5f, down ? 1f : enabled ? 4.5f : 2f, enabled, 0f);
                var tr = anchor == TextAnchor.MiddleLeft ? new Rect(rr.x + 18f * s, rr.y, rr.width - 30f * s, rr.height) : new Rect(rr.x + 10f * s, rr.y, rr.width - 20f * s, rr.height);
                HudTheme.Text(tr, text, size, enabled ? HudTheme.Bright : HudTheme.Muted, anchor);
            }
            return clicked;
        }

        /// <summary>A pick-one pill: lit in <paramref name="accent"/> while selected. Returns true when clicked.</summary>
        public static bool Pick(Rect r, string text, bool selected, Color accent, float size = 17f, bool enabled = true)
        {
            bool clicked = Press(r, enabled, out bool hover, out bool down);
            if (Event.current.type == EventType.Repaint)
            {
                float s = S;
                var rr = Grow(r, down ? -1.5f * s : 0f);
                Color fill = selected ? accent : hover ? HudTheme.Light(HudTheme.Card, 0.26f) : HudTheme.Light(HudTheme.Card, 0.14f);
                HudTheme.Body(rr, fill, Mathf.Min(14f, r.height / s * 0.3f), 3f, down ? 0.5f : 2.5f, true, 0f);
                HudTheme.Text(rr, text, size, selected ? HudTheme.Ink : HudTheme.Bright, TextAnchor.MiddleCenter, !selected);
            }
            return clicked;
        }

        /// <summary>
        /// A grid of pick-one pills laid out from the flow (<paramref name="columns"/> to a row); returns the index picked (the old one if no click).
        /// </summary>
        public static int Choice(Flow f, string[] labels, int selected, int columns, Color accent, float rowHeight = 44f, float size = 17f, float gap = 8f)
        {
            if (labels == null || labels.Length == 0) return selected;
            float s = S, g = gap * s, cw = (f.w - g * (columns - 1)) / columns, ch = rowHeight * s;
            int rows = (labels.Length + columns - 1) / columns;
            for (int i = 0; i < labels.Length; i++)
            {
                var r = new Rect(f.x + (i % columns) * (cw + g), f.y + (i / columns) * (ch + g), cw, ch);
                if (Pick(r, labels[i], i == selected, accent, size)) selected = i;
            }
            f.y += rows * (ch + g) + 2f * s;
            return selected;
        }

        /// <summary>A colour to pick: a rounded square in that colour with a cream ring when it is the one in use.</summary>
        public static bool Swatch(Rect r, Color c, bool selected)
        {
            bool clicked = Press(r, true, out bool hover, out bool down);
            if (Event.current.type == EventType.Repaint)
            {
                float s = S, ow = Mathf.Max(1.5f, 3f * s);
                var rr = selected ? Grow(r, 1.5f * s) : hover ? Grow(r, 1.5f * s) : r;
                HudTheme.Rounded(new Rect(rr.x, rr.y + 3f * s, rr.width, rr.height), HudTheme.Alpha(HudTheme.Ink, 0.38f), 11f * s);
                if (selected) HudTheme.Rounded(Grow(rr, 4f * s), HudTheme.Cream, 14f * s);
                HudTheme.Rounded(rr, HudTheme.Ink, 11f * s);
                var inner = new Rect(rr.x + ow, rr.y + ow, rr.width - ow * 2f, rr.height - ow * 2f);
                HudTheme.Rounded(inner, c, 8f * s);
                HudTheme.Rounded(new Rect(inner.x + 6f * s, inner.y + 4f * s, inner.width - 12f * s, 3f * s), new Color(1f, 1f, 1f, 0.3f), 1.5f * s);
            }
            return clicked;
        }

        // ------------------------------------------------------------------ sliders and switches

        static float ValueAt(Rect track, float mouseX, float min, float max) => Mathf.Lerp(min, max, Mathf.Clamp01((mouseX - track.x) / Mathf.Max(1f, track.width)));

        /// <summary>A slider: a dark track filled in <paramref name="accent"/> with a cream knob; drag it, or press anywhere on it.</summary>
        public static float Slider(Rect r, float value, float min, float max, Color accent)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var ev = Event.current;
            float s = S, kd = Mathf.Min(r.height, 32f * s);
            var track = new Rect(r.x + kd * 0.5f, r.center.y - 8f * s, r.width - kd, 16f * s);
            bool mine = GUIUtility.hotControl == id;
            switch (ev.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (ev.button == 0 && Hit(r)) { GUIUtility.hotControl = id; value = ValueAt(track, ev.mousePosition.x, min, max); ev.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (mine) { value = ValueAt(track, ev.mousePosition.x, min, max); ev.Use(); }
                    break;
                case EventType.MouseUp:
                    if (mine) { GUIUtility.hotControl = 0; ev.Use(); }
                    break;
                case EventType.Repaint:
                    {
                        float t = Mathf.InverseLerp(min, max, value);
                        HudTheme.Bar(track, t, accent, 2.5f);
                        var c = new Vector2(track.x + t * track.width, track.center.y);
                        float d = kd * (mine ? 1.12f : Hit(r) ? 1.06f : 1f);
                        HudTheme.Circle(c + new Vector2(0f, 3f * s), d, HudTheme.Alpha(HudTheme.Ink, 0.38f));
                        HudTheme.Circle(c, d, HudTheme.Ink);
                        HudTheme.Circle(c, d - 6.5f * s, HudTheme.Cream);
                        HudTheme.Circle(c, d * 0.3f, accent);
                    }
                    break;
            }
            return Mathf.Clamp(value, Mathf.Min(min, max), Mathf.Max(min, max));
        }

        /// <summary>
        /// A labelled slider from the flow: the label at the left, the value on a chip at the right, the slider under them. Returns the new value.
        /// </summary>
        public static float SliderRow(Flow f, string label, string valueText, float value, float min, float max, Color accent)
        {
            float s = S;
            var top = f.Row(26f, 2f);
            if (Event.current.type == EventType.Repaint)
            {
                HudTheme.Text(new Rect(top.x + 4f * s, top.y, top.width * 0.62f, top.height), label, 17f, HudTheme.Bright, TextAnchor.MiddleLeft, false);
                float cw = HudTheme.Width(valueText, 15f) + 26f * s;
                HudTheme.Chip(new Rect(top.xMax - cw, top.y + 1f * s, cw, 24f * s), valueText, accent, 15f);
            }
            var bar = f.Row(34f, 10f);
            return Slider(bar, value, min, max, accent);
        }

        /// <summary>A switch row: the label on a dim plate and an ON/OFF switch at the right; the whole row is the button.</summary>
        public static bool ToggleRow(Rect row, string label, bool value)
        {
            bool clicked = Press(row, true, out bool hover, out bool down);
            if (clicked) value = !value;
            if (Event.current.type == EventType.Repaint)
            {
                float s = S;
                HudTheme.Rounded(row, HudTheme.Alpha(HudTheme.Ink, hover ? 0.3f : 0.18f), 14f * s);
                HudTheme.Text(new Rect(row.x + 16f * s, row.y, row.width - 120f * s, row.height), label, 18f, HudTheme.Bright, TextAnchor.MiddleLeft, false);
                var sw = new Rect(row.xMax - 14f * s - 76f * s, row.center.y - 18f * s, 76f * s, 36f * s);
                HudTheme.Pill(sw, value ? HudTheme.Good : HudTheme.Dim, 3f, 2.5f);
                float kd = 26f * s, kx = value ? sw.xMax - 5f * s - kd : sw.x + 5f * s;
                HudTheme.Circle(new Vector2(kx + kd * 0.5f, sw.center.y), kd + 4f * s, HudTheme.Ink);
                HudTheme.Circle(new Vector2(kx + kd * 0.5f, sw.center.y), kd - 1f * s, HudTheme.Cream);
                var word = value ? new Rect(sw.x + 8f * s, sw.y, sw.width * 0.5f, sw.height) : new Rect(sw.xMax - 8f * s - sw.width * 0.5f, sw.y, sw.width * 0.5f, sw.height);
                HudTheme.Text(word, value ? "ON" : "OFF", 13f, HudTheme.Bright, value ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            }
            return value;
        }

        // ------------------------------------------------------------------ a text box

        static GUIStyle fieldStyle;

        /// <summary>
        /// A text box: a cream plate with the text on it (Unity's own text editing under a plain style, so the caret, selection and pasting work).
        /// The plate shows a blue ring while the box has the keyboard. <paramref name="controlName"/> must be unique on the page.
        /// </summary>
        public static string TextBox(Rect r, string value, int maxLength, string controlName, string hint = null)
        {
            float s = S;
            bool focused = GUI.GetNameOfFocusedControl() == controlName;
            if (Event.current.type == EventType.Repaint)
            {
                HudTheme.Rounded(new Rect(r.x, r.y + 3f * s, r.width, r.height), HudTheme.Alpha(HudTheme.Ink, 0.38f), 12f * s);
                if (focused) HudTheme.Rounded(Grow(r, 3.5f * s), HudTheme.Info, 15f * s);
                HudTheme.Rounded(r, HudTheme.Ink, 12f * s);
                float ow = Mathf.Max(1.5f, 3f * s);
                HudTheme.Rounded(new Rect(r.x + ow, r.y + ow, r.width - ow * 2f, r.height - ow * 2f), HudTheme.Cream, 9f * s);
                if (string.IsNullOrEmpty(value) && !focused && !string.IsNullOrEmpty(hint))
                    HudTheme.Text(new Rect(r.x + 16f * s, r.y, r.width - 28f * s, r.height), hint, 17f, HudTheme.Alpha(HudTheme.Ink, 0.4f), TextAnchor.MiddleLeft, false);
            }
            if (fieldStyle == null)
            {
                // an empty style, not a copy of the skin's text field: that one brings its own (dark) background textures, scaled copies included
                fieldStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false, richText = false };
            }
            fieldStyle.font = HudTheme.TextFont;
            fieldStyle.fontStyle = HudTheme.TextStyle;
            fieldStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(19f * s));
            fieldStyle.padding = new RectOffset(Mathf.RoundToInt(14f * s), Mathf.RoundToInt(10f * s), 0, 0);
            fieldStyle.normal.textColor = fieldStyle.hover.textColor = fieldStyle.active.textColor = fieldStyle.focused.textColor = HudTheme.Ink;
            fieldStyle.onNormal.textColor = fieldStyle.onHover.textColor = fieldStyle.onActive.textColor = fieldStyle.onFocused.textColor = HudTheme.Ink;
            GUI.skin.settings.cursorColor = HudTheme.Ink;
            GUI.skin.settings.selectionColor = new Color(0.25f, 0.7f, 1f, 0.45f);
            GUI.SetNextControlName(controlName);
            return GUI.TextField(r, value ?? "", maxLength, fieldStyle);
        }

        // ------------------------------------------------------------------ scrolling

        /// <summary>
        /// A scrolling page: call <see cref="Begin"/>, lay the page out with a <see cref="Flow"/> starting at <c>(inset, top)</c>, call
        /// <see cref="End"/> with where the flow ended. The mouse wheel scrolls; a slim bar (drag it) shows where you are. What is scrolled out of
        /// view cannot be clicked. The page's height is the one measured on the previous frame, so a page that grows takes a frame to get its bar.
        /// </summary>
        public sealed class Scroll
        {
            public float pos;
            float contentH;
            Rect savedClip;

            /// <summary>The y a page starts at inside the area (negative while scrolled).</summary>
            public float Top => -pos;

            /// <summary>How tall the page was on the last frame (screen pixels): a card that fits its page asks for this.</summary>
            public float ContentHeight => contentH;

            /// <summary>How much room the page has across (the bar takes some when there is one); the page starts <paramref name="inset"/> from the left.</summary>
            public float Begin(Rect view, float inset)
            {
                float s = S, over = contentH - view.height, max = over > 10f * s ? over : 0f;    // (a few pixels over is only the gap after the last row: no bar for that)
                pos = Mathf.Clamp(pos, 0f, max);
                GUI.BeginGroup(view);
                savedClip = clip;
                clip = new Rect(0f, 0f, view.width, view.height);
                return view.width - inset * 2f - (max > 0f ? 14f * s : 0f);
            }

            /// <summary>Ends the area; <paramref name="flowY"/> is the flow's y after the last row.</summary>
            public void End(Rect view, float flowY)
            {
                GUI.EndGroup();
                clip = savedClip;
                float s = S;
                contentH = Mathf.Max(0f, flowY + pos);
                float over = contentH - view.height, max = over > 10f * s ? over : 0f;
                var ev = Event.current;
                int id = GUIUtility.GetControlID(FocusType.Passive);
                if (max <= 0f) return;

                // the bar: a thin track at the right edge and a thumb as tall as the share of the page that is in view
                var track = new Rect(view.xMax - 11f * s, view.y + 4f * s, 9f * s, view.height - 8f * s);
                float thumbH = Mathf.Max(40f * s, track.height * view.height / contentH);
                float thumbY = track.y + (track.height - thumbH) * (pos / max);
                var thumb = new Rect(track.x, thumbY, track.width, thumbH);
                bool mine = GUIUtility.hotControl == id;
                switch (ev.GetTypeForControl(id))
                {
                    case EventType.ScrollWheel:
                        if (view.Contains(ev.mousePosition)) { pos = Mathf.Clamp(pos + ev.delta.y * 22f * s, 0f, max); ev.Use(); }
                        break;
                    case EventType.MouseDown:
                        if (ev.button == 0 && Grow(track, 5f * s).Contains(ev.mousePosition))
                        {
                            GUIUtility.hotControl = id;
                            grab = thumb.Contains(ev.mousePosition) ? ev.mousePosition.y - thumb.y : thumbH * 0.5f;
                            pos = Mathf.Clamp((ev.mousePosition.y - grab - track.y) / Mathf.Max(1f, track.height - thumbH), 0f, 1f) * max;
                            ev.Use();
                        }
                        break;
                    case EventType.MouseDrag:
                        if (mine)
                        {
                            pos = Mathf.Clamp((ev.mousePosition.y - grab - track.y) / Mathf.Max(1f, track.height - thumbH), 0f, 1f) * max;
                            ev.Use();
                        }
                        break;
                    case EventType.MouseUp:
                        if (mine) { GUIUtility.hotControl = 0; ev.Use(); }
                        break;
                    case EventType.Repaint:
                        HudTheme.Rounded(track, HudTheme.Alpha(HudTheme.Ink, 0.4f), track.width * 0.5f);
                        HudTheme.Body(thumb, mine ? HudTheme.Light(HudTheme.Info, 0.2f) : HudTheme.Info, 4.5f, 1.5f, 0f, false, 0f);
                        break;
                }
            }

            float grab;
        }
    }
}
