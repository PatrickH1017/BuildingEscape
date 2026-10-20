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
            HudTheme.Panel(panel, radius);          // (a dark card with a thin border and the amber line along its top edge)
        }

        /// <summary>The title tab that sits across the top edge of a card.</summary>
        public static void Ribbon(Rect panel, string title, Color fill, float size = 34f)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S;
            size = Mathf.Min(size, 28f);
            float rw = Mathf.Min(HudTheme.Width(title, size) + 90f * s + title.Length * 2f * s, panel.width - 40f * s);
            var r = new Rect(panel.center.x - rw * 0.5f, panel.y - 22f * s, rw, 46f * s);
            HudTheme.Body(r, HudTheme.Card, 18f, 3.5f, 3f, true, 0f);                                      // (a dark tab across the top edge, its title in caps and a line in the page's colour under it)
            HudTheme.Rounded(new Rect(r.x + 14f * s, r.yMax - 6f * s, r.width - 28f * s, Mathf.Max(2f, 3f * s)), fill, 1.5f * s);
            HudTheme.TextTracked(new Rect(r.x, r.y, r.width, r.height - 5f * s), title, size, HudTheme.Bright, 3f, TextAnchor.MiddleCenter);
        }

        /// <summary>A soft dark plate under a row of a list (a lobby, a player, a friend).</summary>
        public static void Plate(Rect r, float alpha = 0.16f)
        {
            if (Event.current.type == EventType.Repaint)
            {
                float rad = Mathf.Min(7f * S, r.height * 0.5f);
                HudTheme.Rounded(r, HudTheme.Edge, rad);
                float bw = Mathf.Max(1f, 1.2f * S);
                HudTheme.Rounded(new Rect(r.x + bw, r.y + bw, r.width - bw * 2f, r.height - bw * 2f), HudTheme.Alpha(HudTheme.Raised, Mathf.Clamp01(0.45f + alpha)), Mathf.Max(0f, rad - bw));
            }
        }
        /// <summary>The screen dimmed behind a card.</summary>
        public static void Dim(float alpha)
        {
            if (Event.current.type == EventType.Repaint)
                HudTheme.Rounded(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.04f, 0.05f, 0.08f, alpha), 0f);
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
            if (rx < row.xMax - 4f * s) HudTheme.Rounded(new Rect(rx, row.center.y - 0.75f * s, row.xMax - rx, Mathf.Max(1f, 1.5f * s)), HudTheme.Edge, 1f);
        }

        /// <summary>A pill with a status light and some words (Steam: signed in as ...).</summary>
        public static void StatusPill(Rect r, string text, Color light, float size = 15f)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S;
            HudTheme.Pill(r, HudTheme.Field, 2.5f, 0f, 0f);
            HudIcons.Dot(new Vector2(r.x + r.height * 0.55f, r.center.y), r.height * 0.42f, light);
            HudTheme.Text(new Rect(r.x + r.height * 1.0f, r.y, r.width - r.height * 1.2f, r.height), text, size, HudTheme.Bright, TextAnchor.MiddleLeft, false);
        }

        /// <summary>A pill of words on a colour (a message under the title).</summary>
        public static void Notice(Rect r, string text, Color fill, float size = 15f)
        {
            if (Event.current.type != EventType.Repaint) return;
            HudTheme.Pill(r, fill, 2.5f, 2.5f, 0f);
            HudTheme.Text(r, text, size, HudTheme.Bright, TextAnchor.MiddleCenter, false);
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

        enum ButtonKind { Primary, Quiet, Secondary }

        /// <summary>
        /// Which look a button gets from the colour its caller picked: green means go (the one amber button of a page), red means leave (an outline), anything
        /// else is a dark button with a stripe in that colour.
        /// </summary>
        static ButtonKind KindOf(Color c)
        {
            float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (mx < 0.2f) return ButtonKind.Secondary;
            if (c.g > c.r * 1.15f && c.g > c.b * 1.15f) return ButtonKind.Primary;
            if (c.r > c.g * 1.9f && c.r > c.b * 1.5f) return ButtonKind.Quiet;
            return ButtonKind.Secondary;
        }

        /// <summary>A flat button (see <see cref="ButtonKind"/>): it brightens under the mouse and sinks a pixel when pressed. Returns true when clicked.</summary>
        public static bool Button(Rect r, string text, Color fill, float size = 24f, bool enabled = true, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            bool clicked = Press(r, enabled, out bool hover, out bool down);
            if (Event.current.type == EventType.Repaint)
            {
                float s = S, rad = Mathf.Min(6f * s, r.height * 0.3f), bw = Mathf.Max(1f, 1.3f * s);
                var rr = down ? new Rect(r.x, r.y + s, r.width, r.height) : r;
                var kind = KindOf(fill);
                float tsize = Mathf.Min(size, kind == ButtonKind.Primary ? 34f : 26f) * (kind == ButtonKind.Primary ? 1f : 0.92f);
                Color textColour;
                if (!enabled)
                {
                    HudTheme.Rounded(rr, HudTheme.Edge, rad);
                    HudTheme.Rounded(new Rect(rr.x + bw, rr.y + bw, rr.width - bw * 2f, rr.height - bw * 2f), HudTheme.Field, Mathf.Max(0f, rad - bw));
                    textColour = HudTheme.Dim;
                }
                else if (kind == ButtonKind.Primary)
                {
                    HudTheme.Rounded(new Rect(rr.x, rr.y + 2f * s, rr.width, rr.height), new Color(0f, 0f, 0f, 0.28f), rad);
                    HudTheme.Rounded(rr, hover ? HudTheme.Light(HudTheme.Quota, 0.14f) : HudTheme.Quota, rad);
                    textColour = HudTheme.Ink;
                }
                else if (kind == ButtonKind.Quiet)
                {
                    var line = HudTheme.Light(fill, hover ? 0.25f : 0f);
                    HudTheme.Rounded(rr, HudTheme.Alpha(line, hover ? 0.9f : 0.55f), rad);
                    HudTheme.Rounded(new Rect(rr.x + bw, rr.y + bw, rr.width - bw * 2f, rr.height - bw * 2f), hover ? Color.Lerp(HudTheme.Card, fill, 0.14f) : HudTheme.Card, Mathf.Max(0f, rad - bw));
                    textColour = HudTheme.Light(fill, 0.3f);
                }
                else
                {
                    HudTheme.Rounded(new Rect(rr.x, rr.y + 2f * s, rr.width, rr.height), new Color(0f, 0f, 0f, 0.22f), rad);
                    HudTheme.Rounded(rr, HudTheme.Edge, rad);
                    HudTheme.Rounded(new Rect(rr.x + bw, rr.y + bw, rr.width - bw * 2f, rr.height - bw * 2f), hover ? HudTheme.Light(HudTheme.Raised, 0.08f) : HudTheme.Raised, Mathf.Max(0f, rad - bw));
                    HudTheme.Rounded(new Rect(rr.x + bw, rr.y + bw, Mathf.Max(3f, 4f * s), rr.height - bw * 2f), fill, new Vector4(Mathf.Max(0f, rad - bw), 0f, 0f, Mathf.Max(0f, rad - bw)));      // (the stripe in the button's colour)
                    textColour = HudTheme.Bright;
                }
                // the dark buttons of a page read from the left, like the mock-up; a button asked for a centred label (the footer ones) keeps it
                bool left = anchor == TextAnchor.MiddleLeft || kind != ButtonKind.Quiet && rr.width >= 200f * s && anchor == TextAnchor.MiddleCenter;
                var tr = left ? new Rect(rr.x + 24f * s, rr.y, rr.width - 56f * s, rr.height) : new Rect(rr.x + 10f * s, rr.y, rr.width - 20f * s, rr.height);
                HudTheme.TextTracked(tr, text, tsize, textColour, kind == ButtonKind.Primary ? 3f : 2.2f, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                if (enabled && left && kind != ButtonKind.Quiet)
                    HudTheme.TextPx(new Rect(rr.xMax - 36f * s, rr.y, 24f * s, rr.height), "\u203A", tsize * s * 1.1f, kind == ButtonKind.Primary ? HudTheme.Ink : HudTheme.Dim, TextAnchor.MiddleCenter, false);
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
                var rr = down ? new Rect(r.x, r.y + s, r.width, r.height) : r;
                float rad = Mathf.Min(6f * s, r.height * 0.3f), bw = Mathf.Max(1f, 1.3f * s);
                // selected: a dark plate tinted with the page's colour and a line in it; the others rest as dark buttons
                HudTheme.Rounded(rr, selected ? accent : HudTheme.Edge, rad);
                HudTheme.Rounded(new Rect(rr.x + bw, rr.y + bw, rr.width - bw * 2f, rr.height - bw * 2f),
                    selected ? Color.Lerp(HudTheme.Card, accent, 0.28f) : hover ? HudTheme.Light(HudTheme.Raised, 0.08f) : HudTheme.Raised, Mathf.Max(0f, rad - bw));
                HudTheme.Text(rr, text, size, selected ? HudTheme.Bright : HudTheme.Muted, TextAnchor.MiddleCenter, false);
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
                float s = S, ow = Mathf.Max(1f, 1.3f * s);
                var rr = hover && !selected ? Grow(r, 1f * s) : r;
                if (selected) HudTheme.Rounded(Grow(rr, 3.5f * s), HudTheme.Cream, 8f * s);                 // (a light ring round the colour in use)
                HudTheme.Rounded(Grow(rr, ow), HudTheme.Edge, 6f * s);
                HudTheme.Rounded(rr, c, 5f * s);
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
                        float kn = d * 0.72f;
                        HudTheme.Circle(c + new Vector2(0f, 1.5f * s), kn + 2f * s, new Color(0f, 0f, 0f, 0.3f));
                        HudTheme.Circle(c, kn + 2f * s, accent);
                        HudTheme.Circle(c, kn - 2f * s, HudTheme.Cream);
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
                float rowRad = Mathf.Min(7f * s, row.height * 0.3f), rbw = Mathf.Max(1f, 1.2f * s);
                HudTheme.Rounded(row, HudTheme.Edge, rowRad);
                HudTheme.Rounded(new Rect(row.x + rbw, row.y + rbw, row.width - rbw * 2f, row.height - rbw * 2f), hover ? HudTheme.Light(HudTheme.Raised, 0.06f) : HudTheme.Raised, Mathf.Max(0f, rowRad - rbw));
                HudTheme.Text(new Rect(row.x + 16f * s, row.y, row.width - 120f * s, row.height), label, 18f, HudTheme.Bright, TextAnchor.MiddleLeft, false);
                var sw = new Rect(row.xMax - 14f * s - 76f * s, row.center.y - 18f * s, 76f * s, 36f * s);
                HudTheme.Pill(sw, value ? HudTheme.Good : HudTheme.Dim, 3f, 0f);
                float kd = 24f * s, kx = value ? sw.xMax - 6f * s - kd : sw.x + 6f * s;
                HudTheme.Circle(new Vector2(kx + kd * 0.5f, sw.center.y), kd, HudTheme.Cream);
                var word = value ? new Rect(sw.x + 9f * s, sw.y, sw.width * 0.5f, sw.height) : new Rect(sw.xMax - 9f * s - sw.width * 0.5f, sw.y, sw.width * 0.5f, sw.height);
                HudTheme.Text(word, value ? "ON" : "OFF", 12f, value ? HudTheme.Good : HudTheme.Muted, value ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, false);
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
                float rad = Mathf.Min(6f * s, r.height * 0.3f), ow = Mathf.Max(1f, 1.4f * s);
                HudTheme.Rounded(r, focused ? HudTheme.Info : HudTheme.Edge, rad);                          // (a recessed field, with a blue line while it has the keyboard)
                HudTheme.Rounded(new Rect(r.x + ow, r.y + ow, r.width - ow * 2f, r.height - ow * 2f), HudTheme.Field, Mathf.Max(0f, rad - ow));
                if (string.IsNullOrEmpty(value) && !focused && !string.IsNullOrEmpty(hint))
                    HudTheme.Text(new Rect(r.x + 16f * s, r.y, r.width - 28f * s, r.height), hint, 17f, HudTheme.Dim, TextAnchor.MiddleLeft, false);
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
            fieldStyle.normal.textColor = fieldStyle.hover.textColor = fieldStyle.active.textColor = fieldStyle.focused.textColor = HudTheme.Bright;
            fieldStyle.onNormal.textColor = fieldStyle.onHover.textColor = fieldStyle.onActive.textColor = fieldStyle.onFocused.textColor = HudTheme.Bright;
            GUI.skin.settings.cursorColor = HudTheme.Bright;
            GUI.skin.settings.selectionColor = new Color(0.30f, 0.61f, 0.91f, 0.45f);
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
                        HudTheme.Rounded(track, HudTheme.Field, track.width * 0.5f);
                        HudTheme.Rounded(thumb, mine ? HudTheme.Light(HudTheme.Muted, 0.2f) : HudTheme.Dim, thumb.width * 0.5f);
                        break;
                }
            }

            float grab;
        }
    }
}
