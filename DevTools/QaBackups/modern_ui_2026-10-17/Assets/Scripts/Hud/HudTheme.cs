using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The look of the HUD and the menus, in one place: calm and modern, like an office's signage and security screens. Dark graphite cards with a thin
    /// border and a thin amber line along the top, flat fills (no gloss, no cartoon outlines), a soft shadow, small corners, outlined chips, caps in a
    /// condensed face, and a few colours that mean the same thing everywhere (money is green, the quota amber, danger red ...).
    /// (The cartoon "toon" look this replaced is kept in DevTools\QaBackups\old_toon_ui_2026-10-17.)
    /// <see cref="HudWidgets"/> builds the HUD's pieces from this, <see cref="HudIcons"/> draws the little pictures, <see cref="HudAnim"/>
    /// makes things pop. Everything here draws only on the Repaint event, uses no textures of its own (Unity draws the rounded
    /// rectangles) and makes no garbage while it draws.
    /// The font: a bundled <c>Resources/HudFont</c> (any .ttf the team drops there) wins, then Bahnschrift (Windows' DIN-style face), Segoe UI,
    /// then Trebuchet MS / Verdana / Arial in bold. Players without those still get a clean bold face.
    /// </summary>
    public static class HudTheme
    {
        // ------------------------------------------------------------------ colours and what they mean

        static Color Hex(int rgb) => new Color32((byte)((rgb >> 16) & 255), (byte)((rgb >> 8) & 255), (byte)(rgb & 255), 255);

        /// <summary>The darkest tone: shadows, dark text on a bright fill.</summary>
        public static readonly Color Ink = Hex(0x12151F);
        /// <summary>Money you can spend.</summary>
        public static readonly Color Cash = Hex(0x56D08A);
        /// <summary>What the floor asks for (the quota), loot worth money, and the amber accent line of every card.</summary>
        public static readonly Color Quota = Hex(0xE3A62B);
        /// <summary>Loot that broke, money lost.</summary>
        public static readonly Color Lost = Hex(0xE8897A);
        /// <summary>The police, the alarm, a player who is pinned or busted.</summary>
        public static readonly Color Danger = Hex(0xE5626E);
        /// <summary>Something to watch: a heavy pack, few rounds, backup on its way.</summary>
        public static readonly Color Warning = Hex(0xE8923A);
        /// <summary>Totals, information, the floor.</summary>
        public static readonly Color Info = Hex(0x4C9BE8);
        /// <summary>All clear: free, ready, quota met.</summary>
        public static readonly Color Good = Cash;
        /// <summary>The card colour everything sits on.</summary>
        public static readonly Color Card = Hex(0x1A1F2D);
        /// <summary>A control resting on a card (a button, a pick, a key cap).</summary>
        public static readonly Color Raised = Hex(0x262D40);
        /// <summary>A recessed field (a text box, a bar's track).</summary>
        public static readonly Color Field = Hex(0x10131C);
        /// <summary>The thin line round cards and controls.</summary>
        public static readonly Color Edge = Hex(0x363F57);
        /// <summary>Paper and light text.</summary>
        public static readonly Color Cream = Hex(0xE9EDF6);
        /// <summary>Main text.</summary>
        public static readonly Color Bright = Hex(0xF2F4F8);
        /// <summary>Labels and hints (on a card).</summary>
        public static readonly Color Muted = Hex(0xAAB4CC);
        /// <summary>Things that are off or not available.</summary>
        public static readonly Color Dim = Hex(0x6F7A95);

        static readonly Color[] playerColours = { Hex(0x56D08A), Hex(0xE3A62B), Hex(0x4CC3E8), Hex(0xE98BC4), Hex(0xE8923A), Hex(0xA98BE8) };
        /// <summary>The colour of "Player n" (1 = the first): the same on the player's own badge (<see cref="HudWidgets.PlayerBadge"/>) and on the security computer's Nav map, so "Player 2, go left" is easy to follow on both.</summary>
        public static Color PlayerColour(int number) => playerColours[Mathf.Abs(number - 1) % playerColours.Length];
        /// <summary>Amber ink for the "$" on a coin.</summary>
        public static readonly Color Amber = Hex(0x6B4300);

        /// <summary>The colour mixed towards the ink: the shaded half of a plate.</summary>
        public static Color Shade(Color c, float k = 0.3f) => Color.Lerp(c, Ink, k);
        /// <summary>The colour mixed towards white.</summary>
        public static Color Light(Color c, float k = 0.3f) => Color.Lerp(c, Color.white, k);
        /// <summary>The colour with another opacity.</summary>
        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
        /// <summary>How bright a colour looks (0 black ... 1 white).</summary>
        public static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        // ------------------------------------------------------------------ sizes

        /// <summary>1 at 1080 pixels of screen height; the HUD grows and shrinks with the window (never below 0.75 or above 2).</summary>
        public static float S => Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);

        /// <summary>A length in reference pixels, as pixels on this screen.</summary>
        public static float Px(float v) => Mathf.Round(v * S);

        // ------------------------------------------------------------------ shapes (all sizes here are screen pixels)

        static Texture2D White => Texture2D.whiteTexture;

        /// <summary>A filled rounded rectangle (<paramref name="radius"/> in screen pixels).</summary>
        public static void Rounded(Rect r, Color c, float radius)
        {
            if (r.width < 0.5f || r.height < 0.5f || c.a <= 0.001f) return;
            var old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(r, White, ScaleMode.StretchToFill, true, 0f, c, 0f, Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * 0.5f));
            GUI.color = old;
        }

        /// <summary>A filled rectangle with a radius per corner: top left, top right, bottom right, bottom left.</summary>
        public static void Rounded(Rect r, Color c, Vector4 radii)
        {
            if (r.width < 0.5f || r.height < 0.5f || c.a <= 0.001f) return;
            float m = Mathf.Min(r.width, r.height) * 0.5f;
            var old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(r, White, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero,
                new Vector4(Mathf.Min(radii.x, m), Mathf.Min(radii.y, m), Mathf.Min(radii.z, m), Mathf.Min(radii.w, m)));
            GUI.color = old;
        }

        /// <summary>Only the outline of a rounded rectangle.</summary>
        public static void Ring(Rect r, Color c, float width, float radius)
        {
            if (r.width < 0.5f || r.height < 0.5f || c.a <= 0.001f) return;
            var old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(r, White, ScaleMode.StretchToFill, true, 0f, c, Mathf.Max(0.5f, width), Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * 0.5f));
            GUI.color = old;
        }

        /// <summary>A filled circle.</summary>
        public static void Circle(Vector2 center, float diameter, Color c) =>
            Rounded(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), c, diameter * 0.5f);

        /// <summary>A flat fill (the arguments after the radius are only there from the days of the two-tone toon fill and are ignored).</summary>
        public static void Fill(Rect r, Color c, float radius, bool gloss = true, float litShare = 0.58f)
        {
            if (r.width < 1f || r.height < 1f) return;
            Rounded(r, c, radius);
        }

        /// <summary>The corner radius (screen pixels) a plate gets: a pill stays a pill, everything else is squarer than the radius asked for.</summary>
        static float Radius(float radiusPx, Rect r)
        {
            float half = Mathf.Min(r.width, r.height) * 0.5f;
            if (radiusPx >= half * 0.9f) return half;
            return Mathf.Min(radiusPx * 0.42f, 9f * S);
        }

        /// <summary>
        /// What a plate of this colour is drawn with: a dark colour stays as it is, with the thin Edge line round it; a bright accent colour becomes a dark
        /// card tinted with it and a thin line in the accent (white text stays readable on it).
        /// </summary>
        static void Tone(Color fill, out Color face, out Color border)
        {
            if (Luma(fill) <= 0.27f) { face = fill; border = Alpha(Edge, fill.a); return; }
            face = Color.Lerp(Card, fill, 0.20f); face.a = fill.a;
            border = fill;
        }

        /// <summary>
        /// A plate: a soft shadow, a thin border, the flat fill. <paramref name="radius"/> is in reference pixels (scaled with the screen and squared off);
        /// <paramref name="outline"/>, <paramref name="gloss"/> and <paramref name="rim"/> are only there from the toon look and are ignored; <paramref name="shadow"/> is how far
        /// the shadow lies below it (reference pixels, at most 3).
        /// </summary>
        public static void Body(Rect r, Color fill, float radius, float outline = 3.5f, float shadow = 4f, bool gloss = true, float rim = 0f)
        {
            float s = S, rad = Radius(radius * s, r), bw = Mathf.Max(1f, 1.3f * s);
            Tone(fill, out Color face, out Color border);
            if (shadow > 0f) Rounded(new Rect(r.x, r.y + Mathf.Min(shadow, 3f) * s * 0.9f, r.width, r.height), new Color(0f, 0f, 0f, 0.26f * fill.a), rad);
            Rounded(r, border, rad);
            Rounded(new Rect(r.x + bw, r.y + bw, r.width - bw * 2f, r.height - bw * 2f), face, Mathf.Max(0f, rad - bw));
        }

        /// <summary>A plate with fully round ends.</summary>
        public static void Pill(Rect r, Color fill, float outline = 3f, float shadow = 3f, float rim = 0f) =>
            Body(r, fill, r.height / S * 0.5f, outline, shadow, true, rim);

        /// <summary>The thin amber line along the top of a card.</summary>
        static void Accent(Rect r, float radius, float height)
        {
            float s = S, rad = Radius(radius * s, r), h = Mathf.Max(2f, height * s);
            Rounded(new Rect(r.x, r.y, r.width, h), Quota, new Vector4(rad, rad, 0f, 0f));
        }

        /// <summary>The card everything sits on.</summary>
        public static void Panel(Rect r, float radius = 18f) { Body(r, Card, radius, 4f, 5f, true, 2.2f); Accent(r, radius, 4f); }

        /// <summary>The small card of the in-run HUD (status, pack, ammunition): the same look as <see cref="Panel"/> with a thinner line, so it takes less room.</summary>
        public static void Plate(Rect r, float radius = 13f) { Body(r, Card, radius, 3f, 4f, true, 1.6f); Accent(r, radius, 3f); }

        /// <summary>A progress bar: a dark track, <paramref name="fraction"/> of it filled in <paramref name="fill"/>.</summary>
        public static void Bar(Rect r, float fraction, Color fill, float outline = 3f)
        {
            float s = S, bw = Mathf.Max(1f, 1.2f * s), rad = Mathf.Min(r.height * 0.5f, 4f * s);
            Rounded(r, Edge, rad);
            var inner = new Rect(r.x + bw, r.y + bw, r.width - bw * 2f, r.height - bw * 2f);
            Rounded(inner, Field, Mathf.Max(0f, rad - bw));
            fraction = Mathf.Clamp01(fraction);
            if (fraction <= 0.004f) return;
            var f = new Rect(inner.x + s, inner.y + s, inner.width - 2f * s, inner.height - 2f * s);
            float w = Mathf.Max(f.height, f.width * fraction);
            Rounded(new Rect(f.x, f.y, Mathf.Min(w, f.width), f.height), fill, Mathf.Max(0f, rad - bw - s));
        }

        /// <summary>
        /// A small outlined pill with centred text (a chip): a dark pill tinted with the colour, a thin line in the colour and the text in a light tone of it.
        /// (<paramref name="ink"/> is only there from the toon look and is ignored.)
        /// </summary>
        public static void Chip(Rect r, string text, Color fill, float textSize = 17f, Color? ink = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, bw = Mathf.Max(1f, 1.2f * s), half = r.height * 0.5f;
            bool dark = Luma(fill) <= 0.27f;
            Rounded(r, dark ? Edge : fill, half);
            Rounded(new Rect(r.x + bw, r.y + bw, r.width - bw * 2f, r.height - bw * 2f), dark ? fill : Color.Lerp(Card, fill, 0.14f), Mathf.Max(0f, half - bw));
            TextPx(r, text, textSize * s, dark ? Muted : Light(fill, 0.28f), TextAnchor.MiddleCenter, false);
        }

        /// <summary>A key cap with the key's name; draws at (x, y) with the given height and returns its width.</summary>
        public static float KeyCap(float x, float y, string key, float h, float textSize)
        {
            float s = S, bw = Mathf.Max(1f, 1.3f * s);
            float w = Mathf.Max(h, TextWidthPx(key, textSize * s) + h * 0.55f);
            var r = new Rect(x, y, w, h);
            float rad = Mathf.Min(h * 0.24f, 6f * s);
            Rounded(new Rect(r.x, r.y + 2f * s, r.width, r.height), new Color(0f, 0f, 0f, 0.35f), rad);
            Rounded(r, Edge, rad);
            Rounded(new Rect(r.x + bw, r.y + bw, r.width - bw * 2f, r.height - bw * 2f), Raised, Mathf.Max(0f, rad - bw));
            TextPx(r, key, textSize * s, Bright, TextAnchor.MiddleCenter, false);
            return w;
        }

        // ------------------------------------------------------------------ text

        static Font face;
        static bool faceBold;
        static GUIStyle scratch;
        static readonly GUIContent content = new GUIContent();

        static readonly string[] osFaces = { "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold", "Segoe UI", "Trebuchet MS", "Verdana", "Arial" };
        static readonly bool[] osFaceBold = { false, true, false, true, true, true, true };

        /// <summary>The HUD font (and whether it needs the bold style on top).</summary>
        static Font Face
        {
            get
            {
                if (face) return face;
                var bundled = Resources.Load<Font>("HudFont");
                if (bundled) { face = bundled; faceBold = false; return face; }
                string[] installed = Font.GetOSInstalledFontNames();
                for (int i = 0; i < osFaces.Length; i++)
                {
                    if (System.Array.IndexOf(installed, osFaces[i]) < 0) continue;
                    face = Font.CreateDynamicFontFromOSFont(osFaces[i], 32);
                    faceBold = osFaceBold[i];
                    return face;
                }
                face = Font.CreateDynamicFontFromOSFont("Arial", 32);
                faceBold = true;
                return face;
            }
        }

        /// <summary>The HUD font, and the font style that goes with it (bold for the plain system faces): for the menus' text boxes and wrapped text, which Unity draws itself.</summary>
        public static Font TextFont => Face;
        public static FontStyle TextStyle { get { var f = Face; return faceBold ? FontStyle.Bold : FontStyle.Normal; } }

        static GUIStyle Style(int px, TextAnchor anchor)
        {
            var st = scratch ??= new GUIStyle();
            st.font = Face;
            st.fontStyle = faceBold ? FontStyle.Bold : FontStyle.Normal;
            st.fontSize = px;
            st.alignment = anchor;
            st.clipping = TextClipping.Overflow;
            st.wordWrap = false;
            st.richText = false;
            return st;
        }

        /// <summary>A text and a size: the key of the width cache (a struct, so looking one up makes no garbage).</summary>
        struct WidthKey : System.IEquatable<WidthKey>
        {
            public string text;
            public int size;
            public bool Equals(WidthKey o) => size == o.size && string.Equals(text, o.text);
            public override bool Equals(object o) => o is WidthKey k && Equals(k);
            public override int GetHashCode() => text.GetHashCode() * 31 + size;
        }

        // Measuring a text costs time and makes a little garbage every time, so every width is measured once (these are the same
        // few texts every frame). A cap keeps a stream of different numbers from growing it for ever.
        static readonly Dictionary<WidthKey, float> widths = new Dictionary<WidthKey, float>(256);

        /// <summary>How wide a text is at a size in screen pixels (measured once, then remembered).</summary>
        public static float TextWidthPx(string text, float px)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            int size = Mathf.Max(8, Mathf.RoundToInt(px));
            var key = new WidthKey { text = text, size = size };
            if (widths.TryGetValue(key, out float w)) return w;
            var st = Style(size, TextAnchor.MiddleLeft);
            content.text = text;
            w = st.CalcSize(content).x;
            if (widths.Count > 600) widths.Clear();
            widths[key] = w;
            return w;
        }

        /// <summary>How wide a text is at a size in reference pixels (for laying a row out).</summary>
        public static float Width(string text, float size) => TextWidthPx(text, size * S);

        /// <summary>
        /// Text with a soft shadow under it when <paramref name="outline"/> is on (so it reads over the 3D scene). <paramref name="size"/> is in reference pixels;
        /// the rectangle is in screen pixels. The text shrinks to fit the rectangle's width.
        /// </summary>
        public static void Text(Rect r, string text, float size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool outline = true) =>
            TextPx(r, text, size * S, color, anchor, outline);

        /// <summary>The same with the size in screen pixels.</summary>
        public static void TextPx(Rect r, string text, float px, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool outline = true)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            int size = Mathf.Max(8, Mathf.RoundToInt(px));
            float w = TextWidthPx(text, size), maxW = Mathf.Max(24f, r.width);
            if (w > maxW) size = Mathf.Max(8, Mathf.FloorToInt(size * maxW / w));
            var st = Style(size, anchor);
            content.text = text;
            if (outline)
            {
                st.normal.textColor = new Color(0f, 0f, 0f, 0.62f);
                float off = Mathf.Clamp(size * 0.06f, 1f, 3f);
                GUI.Label(new Rect(r.x, r.y + off, r.width, r.height), content, st);
            }
            st.normal.textColor = color;
            GUI.Label(r, content, st);
        }

        // one cached string per ASCII letter, so spaced text makes no garbage
        static readonly string[] oneChar = MakeOneChar();
        static string[] MakeOneChar() { var a = new string[128]; for (int i = 0; i < a.Length; i++) a[i] = ((char)i).ToString(); return a; }
        static string One(char c) => c < 128 ? oneChar[c] : c.ToString();

        static float TrackedWidth(string text, int size, float track)
        {
            float w = 0f;
            for (int i = 0; i < text.Length; i++) w += TextWidthPx(One(text[i]), size) + (i < text.Length - 1 ? track : 0f);
            return w;
        }

        /// <summary>
        /// Letter-spaced text (caps titles and buttons look calmer with a little air between the letters). <paramref name="size"/> and <paramref name="tracking"/> are in
        /// reference pixels; the rectangle is in screen pixels. Shrinks to fit the width. For the menus, not the HUD (every letter is its own label).
        /// </summary>
        public static void TextTracked(Rect r, string text, float size, Color color, float tracking = 2f, TextAnchor anchor = TextAnchor.MiddleLeft, bool shadow = false)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            float s = S, track = tracking * s;
            int isize = Mathf.Max(8, Mathf.RoundToInt(size * s));
            float total = TrackedWidth(text, isize, track), maxW = Mathf.Max(24f, r.width);
            if (total > maxW)
            {
                float k = maxW / total;
                isize = Mathf.Max(8, Mathf.FloorToInt(isize * k));
                track *= k;
                total = TrackedWidth(text, isize, track);
            }
            float x = r.x;
            if (anchor == TextAnchor.UpperCenter || anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.LowerCenter) x = r.center.x - total * 0.5f;
            else if (anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight) x = r.xMax - total;
            var st = Style(isize, TextAnchor.MiddleLeft);
            for (int pass = shadow ? 0 : 1; pass < 2; pass++)
            {
                float px = x, dy = pass == 0 ? Mathf.Clamp(isize * 0.06f, 1f, 3f) : 0f;
                st.normal.textColor = pass == 0 ? new Color(0f, 0f, 0f, 0.62f) : color;
                for (int i = 0; i < text.Length; i++)
                {
                    string ch = One(text[i]);
                    float cw = TextWidthPx(ch, isize);
                    if (text[i] != ' ')
                    {
                        content.text = ch;
                        GUI.Label(new Rect(px, r.y + dy, cw + 8f, r.height), content, st);
                    }
                    px += cw + track;
                }
            }
        }
        static readonly Dictionary<int, string> moneyText = new Dictionary<int, string>(256);

        /// <summary>Money as "$1,250" (each figure is formatted once and remembered, so drawing it every frame makes no garbage).</summary>
        public static string Money(int v)
        {
            if (moneyText.TryGetValue(v, out var s)) return s;
            s = v < 0 ? "-$" + (-v).ToString("N0") : "$" + v.ToString("N0");
            if (moneyText.Count > 600) moneyText.Clear();
            moneyText[v] = s;
            return s;
        }
    }

    /// <summary>
    /// Remembered text for numbers the HUD shows every frame ("FREE 2", "NEXT QUOTA $4,300"): building a string each frame is garbage, so each
    /// one is built the first time its number comes up and handed back after that. One instance per kind of text.
    /// </summary>
    public sealed class CachedText1
    {
        readonly System.Func<int, string> make;
        readonly Dictionary<int, string> map = new Dictionary<int, string>(64);

        public CachedText1(System.Func<int, string> make) { this.make = make; }

        /// <summary>Text that is a prefix, the number and a suffix.</summary>
        public CachedText1(string prefix, string suffix = "") : this(n => prefix + n + suffix) { }

        public string Get(int n)
        {
            if (map.TryGetValue(n, out var s)) return s;
            s = make(n);
            if (map.Count > 400) map.Clear();
            map[n] = s;
            return s;
        }
    }

    /// <summary>Remembered text built from two numbers ("QUOTA $2,150 / $3,000", "6 / 10"); see <see cref="CachedText1"/>.</summary>
    public sealed class CachedText2
    {
        readonly System.Func<int, int, string> make;
        readonly Dictionary<long, string> map = new Dictionary<long, string>(64);

        public CachedText2(System.Func<int, int, string> make) { this.make = make; }

        public string Get(int a, int b)
        {
            long key = ((long)a << 32) | (uint)b;
            if (map.TryGetValue(key, out var s)) return s;
            s = make(a, b);
            if (map.Count > 400) map.Clear();
            map[key] = s;
            return s;
        }
    }
}