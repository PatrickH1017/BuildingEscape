using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The look of the in-run HUD, in one place: a cartoon ("toon") style to match the toon characters. Ink outlines, bright
    /// saturated fills in two flat tones (a lit top, a shaded bottom, like cel shading), a small hard drop shadow, outlined text, key caps,
    /// and a few colours that mean the same thing everywhere (money is green, the quota gold, danger red ...).
    /// <see cref="HudWidgets"/> builds the HUD's pieces from this, <see cref="HudIcons"/> draws the little pictures, <see cref="HudAnim"/>
    /// makes things bounce. Everything here draws only on the Repaint event, uses no textures of its own (Unity draws the rounded
    /// rectangles) and makes no garbage while it draws.
    /// "Cleaner" pass (2026-10-17): the same cartoon, drawn with a lighter hand: thinner ink lines, a smaller and fainter shadow, a quieter
    /// gloss, a slimmer sticker rim, a thin and even text outline (the few numbers that set this are under "the finish" below).
    /// The font: a bundled <c>Resources/HudFont</c> (any .ttf the team drops there) wins, then "Arial Rounded MT Bold" (ships with
    /// Office), then Trebuchet MS / Verdana / Arial in bold. Players without the rounded font still get a clean bold face.
    /// </summary>
    public static class HudTheme
    {
        // ------------------------------------------------------------------ colours and what they mean

        static Color Hex(int rgb) => new Color32((byte)((rgb >> 16) & 255), (byte)((rgb >> 8) & 255), (byte)(rgb & 255), 255);

        /// <summary>Outlines, shadows, and text on light parts.</summary>
        public static readonly Color Ink = Hex(0x1B1236);
        /// <summary>Money you can spend.</summary>
        public static readonly Color Cash = Hex(0x45E07C);
        /// <summary>What the floor asks for (the quota) and loot worth money.</summary>
        public static readonly Color Quota = Hex(0xFFC61F);
        /// <summary>Loot that broke, money lost.</summary>
        public static readonly Color Lost = Hex(0xFF7060);
        /// <summary>The police, the alarm, a player who is pinned or busted.</summary>
        public static readonly Color Danger = Hex(0xFF4046);
        /// <summary>Something to watch: a heavy pack, few rounds, backup on its way.</summary>
        public static readonly Color Warning = Hex(0xFF9A24);
        /// <summary>Totals, information, the floor.</summary>
        public static readonly Color Info = Hex(0x40B4FF);
        /// <summary>All clear: free, ready, quota met.</summary>
        public static readonly Color Good = Cash;
        /// <summary>The card colour everything sits on.</summary>
        public static readonly Color Card = Hex(0x4C3CA8);
        /// <summary>Key caps and paper.</summary>
        public static readonly Color Cream = Hex(0xFFF3D8);
        /// <summary>Main text.</summary>
        public static readonly Color Bright = Hex(0xFFFFFF);
        /// <summary>Labels and hints (on a card).</summary>
        public static readonly Color Muted = Hex(0xD6CDFA);
        /// <summary>Things that are off or not available.</summary>
        public static readonly Color Dim = Hex(0x9C94C8);
        /// <summary>The dark trough of a bar, a slider and a text field.</summary>
        public static readonly Color Track = Hex(0x2B2068);
        /// <summary>The quiet fill of the small tags and header chips that sit on a card (a lighter card colour, so they do not compete with the buttons).</summary>
        public static readonly Color Tag = Hex(0x6A5CC6);

        static readonly Color[] playerColours = { Hex(0x45E07C), Hex(0xFFC61F), Hex(0x40D8FF), Hex(0xFF7ACB), Hex(0xFF9A24), Hex(0xB58CFF) };
        /// <summary>The colour of "Player n" (1 = the first): the same on the player's own badge (<see cref="HudWidgets.PlayerBadge"/>) and on the security computer's Nav map, so "Player 2, go left" is easy to follow on both.</summary>
        public static Color PlayerColour(int number) => playerColours[Mathf.Abs(number - 1) % playerColours.Length];
        /// <summary>Amber ink for the "$" on a coin.</summary>
        public static readonly Color Amber = Hex(0x9B5A00);

        /// <summary>The colour mixed towards the ink: the shaded half of a plate.</summary>
        public static Color Shade(Color c, float k = 0.3f) => Color.Lerp(c, Ink, k);
        /// <summary>The colour mixed towards white.</summary>
        public static Color Light(Color c, float k = 0.3f) => Color.Lerp(c, Color.white, k);
        /// <summary>The colour with another opacity.</summary>
        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
        /// <summary>The outline colour for white text on a coloured plate: the plate's own colour, much darker (softer than a black-ish outline, and it keeps the candy feel).</summary>
        public static Color Edge(Color plate) => Color.Lerp(plate, Ink, 0.62f);

        // ------------------------------------------------------------------ the finish (how heavy the toon look is)

        // Callers still pass their own outline / shadow / rim widths (reference pixels); these scale them, so one edit here changes every plate.
        /// <summary>The ink outline, as a share of the width the caller asks for.</summary>
        const float OutlineK = 0.78f;
        /// <summary>The depth of the hard drop shadow, and how dark it is.</summary>
        const float ShadowK = 0.55f, ShadowA = 0.30f;
        /// <summary>The width of the pale sticker rim, and how opaque.</summary>
        const float RimK = 0.65f, RimA = 0.55f;
        /// <summary>How much darker the shaded lower half of a plate is than the lit upper half (the two-tone cel look).</summary>
        public const float ShadeAmount = 0.18f;
        /// <summary>How opaque the little gloss line on a tall plate is.</summary>
        const float GlossA = 0.20f;

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

        /// <summary>
        /// The two-tone toon fill: the whole shape in the shaded colour, then the upper <paramref name="litShare"/> of it again in the lit
        /// colour (the same curve, cut by a clip), and, on a tall plate, a short faint gloss line near the top.
        /// </summary>
        public static void Fill(Rect r, Color c, float radius, bool gloss = true, float litShare = 0.58f)
        {
            if (r.width < 1f || r.height < 1f) return;
            Rounded(r, Shade(c, ShadeAmount), radius);
            GUI.BeginGroup(new Rect(r.x, r.y, r.width, r.height * litShare));
            Rounded(new Rect(0f, 0f, r.width, r.height), c, radius);
            GUI.EndGroup();
            if (gloss && r.height >= 30f * S)
            {
                float g = Mathf.Max(2f, 2f * S), inset = Mathf.Min(radius, r.width * 0.3f) * 0.8f + 4f * S;
                float len = (r.width - inset * 2f) * 0.5f;
                if (len > 10f * S) Rounded(new Rect(r.x + inset, r.y + Mathf.Max(2f, 3f * S), len, g), new Color(1f, 1f, 1f, GlossA), g * 0.5f);
            }
        }

        /// <summary>
        /// A toon plate: a small hard drop shadow, an optional pale rim, the ink outline, then the two-tone fill. Sizes: <paramref name="radius"/>,
        /// <paramref name="outline"/>, <paramref name="shadow"/> and <paramref name="rim"/> are reference pixels (scaled with the screen, and
        /// lightened by the finish constants above).
        /// </summary>
        public static void Body(Rect r, Color fill, float radius, float outline = 3.5f, float shadow = 4f, bool gloss = true, float rim = 0f, float litShare = 0.58f)
        {
            float s = S, ow = Mathf.Max(1.5f, outline * OutlineK * s), rad = radius * s;
            float g = rim > 0f ? Mathf.Max(1f, rim * RimK * s) : 0f;       // a pale sticker rim round the outline, so a plate still stands out from a dark scene
            if (shadow > 0f) Rounded(new Rect(r.x - g, r.y - g + shadow * ShadowK * s, r.width + g * 2f, r.height + g * 2f), Alpha(Ink, ShadowA), rad + g);
            if (g > 0f) Rounded(new Rect(r.x - g, r.y - g, r.width + g * 2f, r.height + g * 2f), Alpha(Cream, RimA), rad + g);
            Rounded(r, Ink, rad);
            Fill(new Rect(r.x + ow, r.y + ow, r.width - ow * 2f, r.height - ow * 2f), fill, Mathf.Max(0f, rad - ow), gloss, litShare);
        }

        /// <summary>A plate with fully round ends.</summary>
        public static void Pill(Rect r, Color fill, float outline = 3f, float shadow = 3f, float rim = 0f) =>
            Body(r, fill, r.height / S * 0.5f, outline, shadow, true, rim);

        /// <summary>The card everything sits on.</summary>
        public static void Panel(Rect r, float radius = 18f) => Body(r, Card, radius, 4f, 5f, true, 2.2f);

        /// <summary>The small card of the in-run HUD (status, pack, ammunition): the same look as <see cref="Panel"/> with thinner lines, so it takes less room.</summary>
        public static void Plate(Rect r, float radius = 13f) => Body(r, Card, radius, 3f, 4f, true, 1.6f);

        /// <summary>A chunky progress bar: a dark track, <paramref name="fraction"/> of it filled in <paramref name="fill"/>.</summary>
        public static void Bar(Rect r, float fraction, Color fill, float outline = 3f)
        {
            float s = S, ow = Mathf.Max(1.5f, outline * OutlineK * s);
            Rounded(new Rect(r.x, r.y + 2f * s, r.width, r.height), Alpha(Ink, ShadowA), r.height * 0.5f);
            Rounded(r, Ink, r.height * 0.5f);
            var inner = new Rect(r.x + ow, r.y + ow, r.width - ow * 2f, r.height - ow * 2f);
            Rounded(inner, Track, inner.height * 0.5f);
            fraction = Mathf.Clamp01(fraction);
            if (fraction <= 0.004f) return;
            var f = new Rect(inner.x + 1.5f * s, inner.y + 1.5f * s, inner.width - 3f * s, inner.height - 3f * s);
            float w = Mathf.Max(f.height, f.width * fraction);
            Fill(new Rect(f.x, f.y, Mathf.Min(w, f.width), f.height), fill, f.height * 0.5f);
        }

        /// <summary>
        /// The label of a progress bar drawn by <see cref="Bar"/> (give it the same <paramref name="outline"/>): white on the dark track and ink on the bright fill, cut where the fill ends, so the words read
        /// across both halves (white on the yellow fill was the one place on the HUD where small text could not be read).
        /// </summary>
        public static void BarLabel(Rect r, float fraction, string text, float size, float outline = 3f)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            float s = S, ow = Mathf.Max(1.5f, outline * OutlineK * s);
            var inner = new Rect(r.x + ow, r.y + ow, r.width - ow * 2f, r.height - ow * 2f);
            var f = new Rect(inner.x + 1.5f * s, inner.y + 1.5f * s, inner.width - 3f * s, inner.height - 3f * s);
            fraction = Mathf.Clamp01(fraction);
            float edge = fraction <= 0.004f ? r.x : f.x + Mathf.Min(Mathf.Max(f.height, f.width * fraction), f.width);
            edge = Mathf.Clamp(edge, r.x, r.xMax);
            if (edge < r.xMax)
            {
                GUI.BeginGroup(new Rect(edge, r.y, r.xMax - edge, r.height));
                TextPx(new Rect(r.x - edge, 0f, r.width, r.height), text, size * s, Bright, TextAnchor.MiddleCenter);
                GUI.EndGroup();
            }
            if (edge > r.x)
            {
                GUI.BeginGroup(new Rect(r.x, r.y, edge - r.x, r.height));
                TextPx(new Rect(0f, 0f, r.width, r.height), text, size * s, Ink, TextAnchor.MiddleCenter, false);
                GUI.EndGroup();
            }
        }

        /// <summary>A small plate with round ends and centred text (a chip).</summary>
        public static void Chip(Rect r, string text, Color fill, float textSize = 17f, Color? ink = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            Pill(r, fill, 2.5f, 2f);
            TextPx(r, text, textSize * S, ink ?? Ink, TextAnchor.MiddleCenter, false);
        }

        /// <summary>A key cap with the key's name; draws at (x, y) with the given height and returns its width.</summary>
        public static float KeyCap(float x, float y, string key, float h, float textSize)
        {
            float s = S, ow = Mathf.Max(1.5f, 2.2f * s);
            float w = Mathf.Max(h, TextWidthPx(key, textSize * s) + h * 0.55f);
            var r = new Rect(x, y, w, h);
            Rounded(new Rect(r.x, r.y + 1.8f * s, r.width, r.height), Alpha(Ink, ShadowA), h * 0.3f);
            Rounded(r, Ink, h * 0.3f);
            var inner = new Rect(r.x + ow, r.y + ow, r.width - ow * 2f, r.height - ow * 2f);
            Rounded(inner, Hex(0xD9BF86), h * 0.26f);                       // the shaded lip
            Rounded(new Rect(inner.x, inner.y, inner.width, inner.height - 3f * s), Cream, h * 0.26f);   // the cap
            TextPx(new Rect(inner.x, inner.y - 1.5f * s, inner.width, inner.height - 3f * s), key, textSize * s, Ink, TextAnchor.MiddleCenter, false);
            return w;
        }

        // ------------------------------------------------------------------ text

        static Font face;
        static bool faceBold;
        static GUIStyle scratch;
        static readonly GUIContent content = new GUIContent();
        // the eight directions of a round outline (big text), and the four diagonals of a light one (everything else)
        static readonly Vector2[] ring8 =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.72f, 0.72f), new Vector2(-0.72f, 0.72f), new Vector2(0.72f, -0.72f), new Vector2(-0.72f, -0.72f),
        };
        static readonly Vector2[] diagonals = { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) };

        static readonly string[] osFaces = { "Arial Rounded MT Bold", "Trebuchet MS", "Verdana", "Arial" };

        /// <summary>The HUD font (and whether it needs the bold style on top).</summary>
        static Font Face
        {
            get
            {
                if (face) return face;
                var bundled = Resources.Load<Font>("HudFont");
                if (bundled) { face = bundled; faceBold = false; return face; }
                string[] installed = Font.GetOSInstalledFontNames();
                foreach (var name in osFaces)
                {
                    if (System.Array.IndexOf(installed, name) < 0) continue;
                    face = Font.CreateDynamicFontFromOSFont(name, 32);
                    faceBold = name != "Arial Rounded MT Bold";
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
        /// The size (screen pixels, at most <paramref name="px"/>) at which the longest of the texts fits <paramref name="width"/> screen pixels:
        /// a row of buttons or pills that all use it shows one size of lettering instead of each text shrinking by itself.
        /// </summary>
        public static float FitPx(string[] texts, float px, float width)
        {
            float widest = 0f;
            if (texts != null) for (int i = 0; i < texts.Length; i++) widest = Mathf.Max(widest, TextWidthPx(texts[i], px));
            return widest > width && widest > 0f ? Mathf.Floor(px * width / widest) : px;
        }

        /// <summary>
        /// Text with a toon outline: a thin even outline in the ink colour (or <paramref name="edge"/>): one pixel round small and medium text,
        /// two or more round big text. <paramref name="size"/> is in reference pixels; the rectangle is in screen pixels. The text shrinks to fit
        /// the rectangle's width.
        /// </summary>
        public static void Text(Rect r, string text, float size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool outline = true, Color? edge = null) =>
            TextPx(r, text, size * S, color, anchor, outline, edge);

        /// <summary>The same with the size in screen pixels.</summary>
        public static void TextPx(Rect r, string text, float px, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool outline = true, Color? edge = null)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            int size = Mathf.Max(8, Mathf.RoundToInt(px));
            float w = TextWidthPx(text, size), maxW = Mathf.Max(24f, r.width);
            if (w > maxW) size = Mathf.Max(8, Mathf.FloorToInt(size * maxW / w));
            var st = Style(size, anchor);
            content.text = text;
            if (outline)
            {
                // every pass is one more label (each makes a little garbage), so the outline is as cheap as it can be while still reading as one:
                // 8 passes for big text (it must look round), 4 diagonal passes for the rest. The offsets are whole pixels, so the outline stays crisp.
                st.normal.textColor = edge ?? Ink;
                if (size >= 34)
                {
                    float ow = Mathf.Clamp(Mathf.Round(size * 0.055f), 2f, 5f);
                    for (int i = 0; i < 8; i++)
                        GUI.Label(new Rect(r.x + Mathf.Round(ring8[i].x * ow), r.y + Mathf.Round(ring8[i].y * ow), r.width, r.height), content, st);
                }
                else
                {
                    float ow = size >= 28 ? 2f : 1f;
                    for (int i = 0; i < 4; i++)
                        GUI.Label(new Rect(r.x + diagonals[i].x * ow, r.y + diagonals[i].y * ow, r.width, r.height), content, st);
                }
            }
            st.normal.textColor = color;
            GUI.Label(r, content, st);
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
