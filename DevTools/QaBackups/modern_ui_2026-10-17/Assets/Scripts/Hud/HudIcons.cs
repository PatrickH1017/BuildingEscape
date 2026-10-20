using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The HUD's little pictures, drawn from rounded rectangles and circles (no textures): coin, loot sack, backpack, siren, microphone,
    /// handcuffs, bullet, sparkle. Each draws inside the square rectangle you give it, in the toon style (ink outline, two-tone fill).
    /// </summary>
    public static class HudIcons
    {
        static Rect Sq(Rect r)
        {
            float d = Mathf.Min(r.width, r.height);
            return new Rect(r.center.x - d * 0.5f, r.center.y - d * 0.5f, d, d);
        }

        /// <summary>A shape in the ink colour with a toon fill inside it (outline in screen pixels).</summary>
        static void Shape(Rect r, Color fill, float radius, float ow)
        {
            HudTheme.Rounded(r, HudTheme.Ink, radius);
            HudTheme.Fill(new Rect(r.x + ow, r.y + ow, r.width - ow * 2f, r.height - ow * 2f), fill, Mathf.Max(0f, radius - ow), false);
        }

        static void ShapeCorners(Rect r, Color fill, Vector4 radii, float ow)
        {
            HudTheme.Rounded(r, HudTheme.Ink, radii);
            var inner = new Rect(r.x + ow, r.y + ow, r.width - ow * 2f, r.height - ow * 2f);
            var ri = new Vector4(Mathf.Max(0f, radii.x - ow), Mathf.Max(0f, radii.y - ow), Mathf.Max(0f, radii.z - ow), Mathf.Max(0f, radii.w - ow));
            HudTheme.Rounded(inner, HudTheme.Shade(fill, 0.24f), ri);
            GUI.BeginGroup(new Rect(inner.x, inner.y, inner.width, inner.height * 0.58f));
            HudTheme.Rounded(new Rect(0f, 0f, inner.width, inner.height), fill, ri);
            GUI.EndGroup();
        }

        /// <summary>A gold coin with a "$".</summary>
        public static void Coin(Rect area)
        {
            if (Event.current.type != EventType.Repaint) return;
            var r = Sq(area);
            float d = r.width, ow = Mathf.Max(2f, d * 0.075f);
            HudTheme.Rounded(new Rect(r.x, r.y + d * 0.06f, d, d), HudTheme.Alpha(HudTheme.Ink, 0.38f), d * 0.5f);
            Shape(r, HudTheme.Quota, d * 0.5f, ow);
            HudTheme.Ring(new Rect(r.x + d * 0.17f, r.y + d * 0.17f, d * 0.66f, d * 0.66f), HudTheme.Alpha(HudTheme.Amber, 0.55f), Mathf.Max(1.5f, d * 0.045f), d * 0.33f);
            HudTheme.TextPx(new Rect(r.x, r.y - d * 0.02f, d, d), "$", d * 0.5f, HudTheme.Amber, TextAnchor.MiddleCenter, false);
        }

        /// <summary>A loot sack: a gold bag tied at the neck, with a "$".</summary>
        public static void Bag(Rect area)
        {
            if (Event.current.type != EventType.Repaint) return;
            var r = Sq(area);
            float d = r.width, ow = Mathf.Max(2f, d * 0.07f);
            HudTheme.Rounded(new Rect(r.x + d * 0.1f, r.y + d * 0.3f, d * 0.8f, d * 0.7f), HudTheme.Alpha(HudTheme.Ink, 0.38f), d * 0.34f);
            Shape(new Rect(r.x + d * 0.34f, r.y + d * 0.04f, d * 0.32f, d * 0.26f), HudTheme.Shade(HudTheme.Quota, 0.2f), d * 0.08f, ow);   // the tie
            Shape(new Rect(r.x + d * 0.1f, r.y + d * 0.24f, d * 0.8f, d * 0.7f), HudTheme.Quota, d * 0.34f, ow);
            HudTheme.TextPx(new Rect(r.x, r.y + d * 0.2f, d, d * 0.76f), "$", d * 0.42f, HudTheme.Amber, TextAnchor.MiddleCenter, false);
        }

        /// <summary>A backpack.</summary>
        public static void Pack(Rect area, Color body)
        {
            if (Event.current.type != EventType.Repaint) return;
            var r = Sq(area);
            float d = r.width, ow = Mathf.Max(2f, d * 0.07f);
            HudTheme.Ring(new Rect(r.x + d * 0.3f, r.y + d * 0.02f, d * 0.4f, d * 0.4f), HudTheme.Ink, Mathf.Max(2f, d * 0.12f), d * 0.16f);
            Shape(new Rect(r.x + d * 0.12f, r.y + d * 0.2f, d * 0.76f, d * 0.76f), body, d * 0.24f, ow);
            Shape(new Rect(r.x + d * 0.26f, r.y + d * 0.56f, d * 0.48f, d * 0.3f), HudTheme.Shade(body, 0.28f), d * 0.1f, Mathf.Max(1.5f, ow * 0.8f));
        }

        /// <summary>A flashing siren; <paramref name="on"/> alternates red and blue lights (pass a time-based flag).</summary>
        public static void Siren(Rect area, bool on, bool blue)
        {
            if (Event.current.type != EventType.Repaint) return;
            var r = Sq(area);
            float d = r.width, ow = Mathf.Max(2f, d * 0.07f);
            var light = on ? (blue ? HudTheme.Info : HudTheme.Danger) : HudTheme.Shade(HudTheme.Danger, 0.35f);
            if (on)
            {
                var pivot = new Vector2(r.center.x, r.y + d * 0.5f);
                for (int i = -1; i <= 1; i++)
                {
                    var old = GUI.matrix;
                    GUIUtility.RotateAroundPivot(i * 48f, pivot);
                    HudTheme.Rounded(new Rect(pivot.x - d * 0.045f, r.y - d * 0.06f, d * 0.09f, d * 0.2f), HudTheme.Ink, d * 0.045f);
                    HudTheme.Rounded(new Rect(pivot.x - d * 0.025f, r.y - d * 0.03f, d * 0.05f, d * 0.15f), light, d * 0.025f);
                    GUI.matrix = old;
                }
            }
            ShapeCorners(new Rect(r.x + d * 0.2f, r.y + d * 0.22f, d * 0.6f, d * 0.5f), light, new Vector4(d * 0.3f, d * 0.3f, 0f, 0f), ow);
            Shape(new Rect(r.x + d * 0.1f, r.y + d * 0.68f, d * 0.8f, d * 0.26f), HudTheme.Dim, d * 0.07f, ow);
        }

        /// <summary>A microphone; <paramref name="muted"/> draws a red slash across it.</summary>
        public static void Mic(Rect area, Color body, bool muted)
        {
            if (Event.current.type != EventType.Repaint) return;
            var r = Sq(area);
            float d = r.width, ow = Mathf.Max(1.8f, d * 0.08f);
            Shape(new Rect(r.x + d * 0.34f, r.y + d * 0.06f, d * 0.32f, d * 0.5f), body, d * 0.16f, ow);
            GUI.BeginGroup(new Rect(r.x, r.y + d * 0.42f, d, d * 0.3f));
            HudTheme.Ring(new Rect(d * 0.2f, -d * 0.12f, d * 0.6f, d * 0.6f), HudTheme.Ink, Mathf.Max(2f, d * 0.12f), d * 0.3f);
            GUI.EndGroup();
            HudTheme.Rounded(new Rect(r.x + d * 0.44f, r.y + d * 0.68f, d * 0.12f, d * 0.16f), HudTheme.Ink, 1f);
            HudTheme.Rounded(new Rect(r.x + d * 0.28f, r.y + d * 0.82f, d * 0.44f, d * 0.12f), HudTheme.Ink, d * 0.05f);
            if (!muted) return;
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(-45f, r.center);
            HudTheme.Rounded(new Rect(r.center.x - d * 0.62f, r.center.y - d * 0.075f, d * 1.24f, d * 0.15f), HudTheme.Ink, d * 0.075f);
            HudTheme.Rounded(new Rect(r.center.x - d * 0.58f, r.center.y - d * 0.045f, d * 1.16f, d * 0.09f), HudTheme.Danger, d * 0.045f);
            GUI.matrix = old;
        }

        /// <summary>A pair of handcuffs.</summary>
        public static void Cuffs(Rect area, Color metal)
        {
            if (Event.current.type != EventType.Repaint) return;
            var r = Sq(area);
            float d = r.width, ow = Mathf.Max(1.8f, d * 0.06f), rad = d * 0.23f;
            float cy = r.center.y;
            // the chain: three small links between the cuffs, alternately flat and on edge
            for (int i = 0; i < 3; i++)
            {
                float x = r.x + d * (0.41f + i * 0.09f);
                bool flat = i % 2 == 0;
                var link = flat ? new Rect(x - d * 0.065f, cy - d * 0.035f, d * 0.13f, d * 0.07f) : new Rect(x - d * 0.035f, cy - d * 0.06f, d * 0.07f, d * 0.12f);
                HudTheme.Rounded(new Rect(link.x - ow, link.y - ow, link.width + ow * 2f, link.height + ow * 2f), HudTheme.Ink, link.height * 0.6f);
                HudTheme.Rounded(link, HudTheme.Light(metal, 0.2f), link.height * 0.5f);
            }
            // the two cuffs: a metal band with a hole
            for (int i = 0; i < 2; i++)
            {
                var c = new Vector2(r.x + d * (i == 0 ? 0.25f : 0.75f), cy);
                Shape(new Rect(c.x - rad, c.y - rad, rad * 2f, rad * 2f), metal, rad, ow);
                float hole = rad * 0.5f;
                HudTheme.Circle(c, hole * 2f + ow, HudTheme.Ink);
                HudTheme.Circle(c, hole * 2f, HudTheme.Field);
                HudTheme.Rounded(new Rect(c.x - rad * 0.16f, c.y - rad - ow * 0.2f - rad * 0.2f, rad * 0.32f, rad * 0.4f), HudTheme.Ink, rad * 0.08f);   // the lock
            }
        }

        /// <summary>One round of ammunition (brass); <paramref name="spent"/> draws it as an empty outline.</summary>
        public static void Bullet(Rect area, bool spent, Color brass)
        {
            if (Event.current.type != EventType.Repaint) return;
            float w = Mathf.Min(area.width, area.height * 0.5f), h = area.height;
            var r = new Rect(area.center.x - w * 0.5f, area.y, w, h);
            float ow = Mathf.Max(1.5f, w * 0.1f);
            if (spent)
            {
                HudTheme.Rounded(r, HudTheme.Alpha(HudTheme.Ink, 0.55f), new Vector4(w * 0.5f, w * 0.5f, w * 0.12f, w * 0.12f));
                var inner = new Rect(r.x + ow, r.y + ow, r.width - ow * 2f, r.height - ow * 2f);
                HudTheme.Rounded(inner, HudTheme.Alpha(HudTheme.Muted, 0.18f), new Vector4(w * 0.4f, w * 0.4f, w * 0.05f, w * 0.05f));
                return;
            }
            HudTheme.Rounded(new Rect(r.x, r.y + 2f, r.width, r.height), HudTheme.Alpha(HudTheme.Ink, 0.38f), new Vector4(w * 0.5f, w * 0.5f, w * 0.12f, w * 0.12f));
            ShapeCorners(r, brass, new Vector4(w * 0.5f, w * 0.5f, w * 0.12f, w * 0.12f), ow);
            HudTheme.Rounded(new Rect(r.x + ow, r.y + h * 0.72f, w - ow * 2f, h * 0.1f), HudTheme.Shade(brass, 0.45f), 1f);
        }

        /// <summary>A four-point sparkle (two thin diamonds crossing) centred on <paramref name="c"/>; rotation in degrees.</summary>
        public static void Sparkle(Vector2 c, float size, Color color, float degrees)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.matrix;
            // A square turned 45 degrees is a diamond; stretching it along one axis makes a long thin one. Two of them cross.
            // Point p goes: centre on c, turn 45, stretch, turn by `degrees`, move back.
            var back = Matrix4x4.TRS(new Vector3(-c.x, -c.y, 0f), Quaternion.identity, Vector3.one);
            var turn45 = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 45f));
            var place = Matrix4x4.TRS(new Vector3(c.x, c.y, 0f), Quaternion.Euler(0f, 0f, degrees), Vector3.one);
            float half = size * 0.3536f;                                  // half side of the square whose turned diagonal is `size` / 2
            for (int pass = 0; pass < 2; pass++)
            {
                float h = half * (pass == 0 ? 1.4f : 1f);
                var col = pass == 0 ? HudTheme.Ink : color;
                for (int arm = 0; arm < 2; arm++)
                {
                    var stretch = Matrix4x4.Scale(new Vector3(arm == 0 ? 0.3f : 1f, arm == 0 ? 1f : 0.3f, 1f));
                    GUI.matrix = old * place * stretch * turn45 * back;
                    GUI.DrawTexture(new Rect(c.x - h, c.y - h, h * 2f, h * 2f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, col, 0f, 0f);
                }
            }
            GUI.matrix = old;
        }

        /// <summary>A round dot with an ink outline (a status light).</summary>
        public static void Dot(Vector2 c, float d, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            HudTheme.Circle(c, d, HudTheme.Ink);
            float ow = Mathf.Max(1.5f, d * 0.16f);
            HudTheme.Circle(c, d - ow * 2f, color);
        }

        /// <summary>A round badge: ink outline, two-tone fill.</summary>
        public static void Badge(Rect area, Color fill)
        {
            if (Event.current.type != EventType.Repaint) return;
            var r = Sq(area);
            Shape(r, fill, r.width * 0.5f, Mathf.Max(2.5f, r.width * 0.06f));
        }
    }
}
