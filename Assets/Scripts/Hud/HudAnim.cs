using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The bounce of the toon HUD: things pop in with a spring, a number that changes gives a little punch, a money figure counts up,
    /// and "+$250" floaters rise from where the money went. The state lives in small fixed arrays indexed by an id (see <see cref="Id"/>),
    /// so nothing is allocated while the HUD draws. Call these from the Repaint event only (they advance by the frame's time).
    /// </summary>
    public static class HudAnim
    {
        /// <summary>One id per animated thing.</summary>
        public static class Id
        {
            public const int Cash = 0, Banked = 1, Lost = 2, RunTotal = 3, Prompt = 4, Prompt2 = 5, Banner = 6, Notice = 7, Notice2 = 8,
                Backup = 9, Pinned = 10, Busted = 11, Ammo = 12, Pack = 13, Loot = 14, Voice = 15, Status = 16, Weight = 17, Rounds = 18,
                Reload = 19, Met = 20, Checking = 21, Controls = 22, Reticle = 23, Ring = 24, BackupChip = 25, Sub = 26, Sub2 = 27, Esc = 28, Inventory = 29, Computer = 30;
        }

        public const int Slots = 32;
        const float Never = -100f;

        static readonly float[] shownAt = new float[Slots], hiddenAt = new float[Slots], bumpAt = new float[Slots], eased = new float[Slots], lastCall = new float[Slots];
        static readonly bool[] visible = new bool[Slots], bumpPrimed = new bool[Slots], countPrimed = new bool[Slots];
        static readonly int[] lastValue = new int[Slots];

        static HudAnim()
        {
            for (int i = 0; i < Slots; i++) { shownAt[i] = Never; hiddenAt[i] = Never; bumpAt[i] = Never; lastCall[i] = Never; }
        }

        static float Now => Time.unscaledTime;

        /// <summary>A spring from 0 up past 1 and back: 0, 1.2, 0.95, 1 ... (t in seconds).</summary>
        public static float Spring(float t) => t <= 0f ? 0f : 1f - Mathf.Exp(-9f * t) * Mathf.Cos(15f * t);

        /// <summary>
        /// The scale of something that appears and disappears: 0 while hidden, a springy pop to 1 when it appears, a quick shrink when it goes.
        /// </summary>
        public static float Pop(int id, bool show)
        {
            float now = Now;
            // a widget that is only drawn while it applies is simply not called when it goes away: a gap in the calls counts as "hidden"
            bool away = now - lastCall[id] > 0.25f;
            lastCall[id] = now;
            if (show && away) visible[id] = false;
            if (show != visible[id])
            {
                visible[id] = show;
                if (show) shownAt[id] = now; else hiddenAt[id] = now;
            }
            if (show) return Spring(now - shownAt[id]);
            return Mathf.Clamp01(1f - (now - hiddenAt[id]) / 0.1f);
        }

        /// <summary>Starts the pop again (a prompt whose words changed).</summary>
        public static void Restart(int id) => shownAt[id] = Now;

        /// <summary>1 normally; jumps to about 1.28 and settles when <paramref name="value"/> changes.</summary>
        public static float Bump(int id, int value)
        {
            if (!bumpPrimed[id]) { bumpPrimed[id] = true; lastValue[id] = value; }
            else if (value != lastValue[id]) { lastValue[id] = value; bumpAt[id] = Now; }
            float t = Now - bumpAt[id];
            return t > 0.6f ? 1f : 1f + 0.28f * Mathf.Exp(-7f * t) * Mathf.Cos(16f * t);
        }

        /// <summary>The number to show for a target that changes: it runs towards the target instead of jumping.</summary>
        public static int Count(int id, int target)
        {
            if (!countPrimed[id]) { countPrimed[id] = true; eased[id] = target; }
            float diff = target - eased[id];
            if (Mathf.Abs(diff) < 1f) eased[id] = target;
            else
            {
                float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime);
                eased[id] += diff * (1f - Mathf.Exp(-10f * dt));
            }
            return Mathf.RoundToInt(eased[id]);
        }

        /// <summary>1 +- amount, round and round; for something that wants attention.</summary>
        public static float Pulse(float speed, float amount) => 1f + Mathf.Sin(Now * speed) * amount;

        /// <summary>A side-to-side shake that fades (a value that just went wrong); <paramref name="since"/> is Now when it started.</summary>
        public static float Shake(float since, float pixels)
        {
            float t = Now - since;
            return t < 0f || t > 0.5f ? 0f : Mathf.Sin(t * 70f) * pixels * (1f - t * 2f);
        }

        /// <summary>Runs <paramref name="draw"/> scaled around a pivot (use for the pop of a whole widget). Restores the matrix after.</summary>
        public static Matrix4x4 Begin(float scale, Vector2 pivot)
        {
            var old = GUI.matrix;
            scale = Mathf.Max(scale, 0.02f);              // a scale of 0 is not an invertible matrix (Unity refuses it and logs)
            if (Mathf.Abs(scale - 1f) > 0.002f) GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), pivot);
            return old;
        }

        /// <summary>Ends what <see cref="Begin"/> started.</summary>
        public static void End(Matrix4x4 old) => GUI.matrix = old;

        // ------------------------------------------------------------------ floaters ("+$250")

        struct Floater { public string text; public Color color; public float born; public float x; }

        static readonly Floater[] floaters = new Floater[8];
        static int nextFloater;
        const float FloatSeconds = 1.5f;

        /// <summary>Starts a floating text (it rises and fades near wherever <see cref="DrawFloaters"/> is drawn).</summary>
        public static void Float(string text, Color color)
        {
            var f = new Floater { text = text, color = color, born = Now, x = (nextFloater % 3 - 1) * 22f };
            floaters[nextFloater] = f;
            nextFloater = (nextFloater + 1) % floaters.Length;
        }

        /// <summary>Draws the floaters drifting from <paramref name="origin"/> (screen pixels) by <paramref name="drift"/> (reference pixels) as they age.</summary>
        public static void DrawFloaters(Vector2 origin, float size, Vector2 drift)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = HudTheme.S;
            for (int i = 0; i < floaters.Length; i++)
            {
                var f = floaters[i];
                if (f.text == null) continue;
                float t = Now - f.born;
                if (t > FloatSeconds) { floaters[i].text = null; continue; }
                float rise = 1f - Mathf.Pow(1f - t / FloatSeconds, 2.2f);
                float a = t < 1.0f ? 1f : 1f - (t - 1.0f) / (FloatSeconds - 1.0f);
                float pop = 0.7f + 0.3f * Mathf.Clamp01(Spring(t * 1.3f));
                var c = new Vector2(origin.x + (f.x * 0.3f + drift.x * rise) * s, origin.y + (drift.y * rise + f.x * 0.4f) * s);
                var old = GUI.color;
                var mat = Begin(pop, c);
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                HudTheme.Text(new Rect(c.x - 120f * s, c.y - 20f * s, 240f * s, 40f * s), f.text, size, f.color, TextAnchor.MiddleCenter);
                GUI.color = old;
                End(mat);
            }
        }
    }
}
