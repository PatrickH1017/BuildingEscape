using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The pieces of the in-run HUD, built from <see cref="HudTheme"/>, <see cref="HudIcons"/> and <see cref="HudAnim"/>. Each widget takes plain
    /// values (a small model struct or a few arguments), never the game's classes, so the same code draws the real HUD and the test gallery.
    /// Call them from <c>OnGUI</c>; they draw only on the Repaint event.
    /// </summary>
    public static class HudWidgets
    {
        static float S => HudTheme.S;

        // text built from numbers is built once per number, not once per frame (see CachedText1 / CachedText2)
        static readonly CachedText1 intText = new CachedText1(string.Empty);
        static readonly CachedText1 ofText = new CachedText1("of ");
        static readonly CachedText1 nextQuotaText = new CachedText1(n => "NEXT QUOTA  " + HudTheme.Money(n));
        static readonly CachedText2 quotaText = new CachedText2((banked, quota) => "QUOTA  " + HudTheme.Money(banked) + " / " + HudTheme.Money(quota));
        static readonly CachedText1 lostText = new CachedText1(n => "LOST " + HudTheme.Money(n));
        static readonly CachedText1 runText = new CachedText1(n => "RUN TOTAL " + HudTheme.Money(n));
        static readonly CachedText1 freeText = new CachedText1("FREE ");
        static readonly CachedText1 pinnedText = new CachedText1("PINNED ");
        static readonly CachedText1 bustedText = new CachedText1("BUSTED ");
        static readonly CachedText2 weightText = new CachedText2((w, c) => (w / 10f).ToString("0.#") + " / " + (c / 10f).ToString("0.#") + " kg");
        static readonly CachedText2 lootText = new CachedText2((n, v) => n + " loot   " + HudTheme.Money(v));
        static readonly CachedText2 ammoText = new CachedText2((r, c) => r + " / " + c);
        static readonly CachedText1 backupInText = new CachedText1("BACKUP IN ");
        static string gunName, gunNameUpper;

        // ====================================================================== status card (top left)

        /// <summary>What the status card shows.</summary>
        public struct StatusModel
        {
            /// <summary>"Loading Docks" or "Floor 4".</summary>
            public string place;
            /// <summary>The floor's level (0 = the docks) and how many stops there are in all (shown as "n of total").</summary>
            public int level, stops;
            public bool docks, boss, met;
            public int cash, banked, quota, lost, runTotal, nextQuota;
            public int free, pinned, busted;
        }

        /// <summary>The card at the top left: floor badge, cash (big, green), quota bar, lost / run total chips, then the team row under it.</summary>
        public static void StatusCard(in StatusModel m)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S;
            float x = 20f * s, y = 16f * s, w = 410f * s, pad = 16f * s;
            float h = 150f * s;
            HudTheme.Panel(new Rect(x, y, w, h));

            // ---- the floor badge
            float bd = 84f * s;
            var badge = new Rect(x + pad, y + pad, bd, bd);
            Color badgeColour = m.boss ? HudTheme.Danger : m.docks ? HudTheme.Warning : HudTheme.Info;
            HudIcons.Badge(badge, badgeColour);
            if (m.docks)
            {
                HudTheme.Text(new Rect(badge.x, badge.y + bd * 0.12f, bd, bd * 0.4f), "DOCKS", 17f, HudTheme.Bright, TextAnchor.MiddleCenter);
                HudTheme.Text(new Rect(badge.x, badge.y + bd * 0.46f, bd, bd * 0.34f), "BASE", 14f, HudTheme.Cream, TextAnchor.MiddleCenter);
            }
            else
            {
                string top = m.boss ? "BOSS" : "FLOOR";
                HudTheme.Text(new Rect(badge.x, badge.y + bd * 0.1f, bd, bd * 0.24f), top, 13f, HudTheme.Cream, TextAnchor.MiddleCenter);
                HudTheme.Text(new Rect(badge.x, badge.y + bd * 0.26f, bd, bd * 0.46f), intText.Get(m.level), 40f, HudTheme.Bright, TextAnchor.MiddleCenter);
                if (m.stops > 0) HudTheme.Text(new Rect(badge.x, badge.y + bd * 0.68f, bd, bd * 0.2f), ofText.Get(m.stops - 1), 11f, HudTheme.Cream, TextAnchor.MiddleCenter);
            }

            // ---- cash: a coin and the money, big and green; it punches when it changes and counts up
            float rx = x + pad + bd + 14f * s, rw = w - (rx - x) - pad;
            int shownCash = HudAnim.Count(HudAnim.Id.Cash, m.cash);
            float punch = HudAnim.Bump(HudAnim.Id.Cash, m.cash);
            var cashRow = new Rect(rx, y + pad - 2f * s, rw, 46f * s);
            var mat = HudAnim.Begin(punch, new Vector2(cashRow.x + 20f * s, cashRow.center.y));
            HudIcons.Coin(new Rect(cashRow.x, cashRow.y + 2f * s, 42f * s, 42f * s));
            HudTheme.Text(new Rect(cashRow.x + 50f * s, cashRow.y, rw - 50f * s, cashRow.height), HudTheme.Money(shownCash), 44f, HudTheme.Cash);
            HudAnim.End(mat);

            // ---- the quota bar (or what replaces it on the docks and the boss floor)
            var bar = new Rect(rx, y + pad + 52f * s, rw, 32f * s);
            if (m.docks)
            {
                HudTheme.Pill(bar, HudTheme.Quota);
                HudTheme.Text(bar, m.nextQuota > 0 ? nextQuotaText.Get(m.nextQuota) : "BUY AND GET READY", 17f, HudTheme.Bright, TextAnchor.MiddleCenter);
            }
            else if (m.boss)
            {
                HudTheme.Pill(bar, HudTheme.Danger);
                HudTheme.Text(bar, "KNOCK OUT THE BOSS!", 17f, HudTheme.Bright, TextAnchor.MiddleCenter);
            }
            else
            {
                float f = m.quota > 0 ? Mathf.Clamp01(m.banked / (float)m.quota) : 0f;
                float bankedPunch = 1f + (HudAnim.Bump(HudAnim.Id.Banked, m.banked) - 1f) * 0.3f;      // (a gentler punch than the cash: the bar must stay inside its card)
                var mat2 = HudAnim.Begin(bankedPunch, bar.center);
                HudTheme.Bar(bar, m.met ? 1f : f, m.met ? HudTheme.Cash : HudTheme.Quota);
                string label = m.met ? "QUOTA MET!" : quotaText.Get(m.banked, m.quota);
                HudTheme.Text(bar, label, 16f, HudTheme.Bright, TextAnchor.MiddleCenter);
                HudAnim.End(mat2);
                if (m.met)
                {
                    float t = Time.unscaledTime;
                    HudIcons.Sparkle(new Vector2(bar.xMax - 10f * s, bar.y - 4f * s), 22f * s * (0.8f + 0.2f * Mathf.Sin(t * 6f)), HudTheme.Bright, t * 40f);
                    HudIcons.Sparkle(new Vector2(bar.x + 14f * s, bar.yMax + 2f * s), 15f * s * (0.8f + 0.2f * Mathf.Sin(t * 5f + 2f)), HudTheme.Quota, -t * 30f);
                }
            }

            // ---- chips under the badge: lost, run total
            float cy = y + pad + bd + 10f * s, ch = 28f * s, cx = x + pad;
            if (m.lost > 0 && !m.docks) cx += Chip(cx, cy, ch, lostText.Get(HudAnim.Count(HudAnim.Id.Lost, m.lost)), HudTheme.Lost) + 8f * s;
            if (m.runTotal > 0 || m.docks) cx += Chip(cx, cy, ch, runText.Get(HudAnim.Count(HudAnim.Id.RunTotal, m.runTotal)), HudTheme.Info) + 8f * s;

            TeamRow(x, y + h + 12f * s, m);
            HudAnim.DrawFloaters(new Vector2(x + w + 46f * s, y + pad + 22f * s), 30f, new Vector2(34f, -22f));
        }

        /// <summary>A chip sized to its text; returns its width.</summary>
        static float Chip(float x, float y, float h, string text, Color fill, float size = 15f)
        {
            float w = HudTheme.Width(text, size) + h * 0.9f;
            HudTheme.Chip(new Rect(x, y, w, h), text, fill, size);
            return w;
        }

        /// <summary>"FREE 2", and "PINNED 1" / "BUSTED 1" while there are any: how the team stands with the police.</summary>
        static void TeamRow(float x, float y, in StatusModel m)
        {
            float s = S, h = 30f * s;
            x += Chip(x, y, h, freeText.Get(m.free), HudTheme.Good) + 8f * s;
            if (m.pinned > 0)
            {
                float k = HudAnim.Pulse(9f, 0.04f);
                var mat = HudAnim.Begin(k, new Vector2(x, y + h * 0.5f));
                x += Chip(x, y, h, pinnedText.Get(m.pinned), HudTheme.Danger) + 8f * s;
                HudAnim.End(mat);
            }
            if (m.busted > 0) Chip(x, y, h, bustedText.Get(m.busted), HudTheme.Dim);
        }

        // ====================================================================== the controls (bottom)

        struct Hint { public string key, label; public Hint(string k, string l) { key = k; label = l; } }

        static readonly Hint[] row1 =
        {
            new Hint("WASD", "move"), new Hint("MOUSE", "look"), new Hint("ALT / V", "look around"), new Hint("SHIFT", "sprint"),
            new Hint("SPACE", "jump"), new Hint("CTRL / C", "crouch"), new Hint("E", "use what the crosshair is on"), new Hint("TAB", "inventory"),
            new Hint("Q", "grab / drop"), new Hint("LMB", "throw / swing weapon"),
        };

        static readonly Hint[] row2 =
        {
            new Hint("H", "hide"), new Hint("F", "elevator panel / sell (loading docks)"), new Hint("M", "mute"), new Hint("ESC", "cursor"),
        };

        const string BankHint = "Drop loot in the elevator to bank it";

        /// <summary>The key hints as two rows of key caps along the bottom; they shrink to fit a narrow window.</summary>
        public static void Controls()
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, avail = Screen.width - 40f * s;
            float y2 = Screen.height - 40f * s, y1 = y2 - 36f * s;
            HintRow(row1, null, 20f * s, y1, avail);
            HintRow(row2, BankHint, 20f * s, y2, avail);
        }

        static float RowWidth(Hint[] hints, string tail, float scale)
        {
            float s = S * scale, total = 0f;
            for (int i = 0; i < hints.Length; i++)
            {
                float h = 27f * s;
                total += Mathf.Max(h, HudTheme.TextWidthPx(hints[i].key, 14f * s) + h * 0.55f) + 7f * s + HudTheme.TextWidthPx(hints[i].label, 15f * s) + 20f * s;
            }
            if (tail != null) total += HudTheme.TextWidthPx(tail, 15f * s) + 20f * s;
            return total;
        }

        static void HintRow(Hint[] hints, string tail, float x, float y, float avail)
        {
            float fit = Mathf.Min(1f, avail / Mathf.Max(1f, RowWidth(hints, tail, 1f)));
            float s = S * fit, h = 27f * s;
            for (int i = 0; i < hints.Length; i++)
            {
                float kw = HudTheme.KeyCap(x, y, hints[i].key, h, 14f * (fit));
                float lw = HudTheme.TextWidthPx(hints[i].label, 15f * s);
                HudTheme.TextPx(new Rect(x + kw + 7f * s, y, lw + 12f, h), hints[i].label, 15f * s, HudTheme.Bright, TextAnchor.MiddleLeft);
                x += kw + 7f * s + lw + 20f * s;
            }
            if (tail != null) HudTheme.TextPx(new Rect(x, y, HudTheme.TextWidthPx(tail, 15f * s) + 12f, h), tail, 15f * s, HudTheme.Quota, TextAnchor.MiddleLeft);
        }

        // ====================================================================== banner (the moments that matter)

        /// <summary>A big ribbon near the top with a title and up to two lines under it; pops in with a spring.</summary>
        public static void Banner(string title, string sub, Color accent, string sub2 = null, int popId = HudAnim.Id.Banner)
        {
            if (Event.current.type != EventType.Repaint) return;
            float k = HudAnim.Pop(popId, !string.IsNullOrEmpty(title));
            if (k <= 0.01f || string.IsNullOrEmpty(title)) return;
            float s = S;
            float tw = Mathf.Min(HudTheme.Width(title, 58f) + 90f * s, Screen.width - 40f * s), th = 84f * s;
            var r = new Rect((Screen.width - tw) * 0.5f, 92f * s, tw, th);
            var mat = HudAnim.Begin(k, r.center);
            HudTheme.Body(r, accent, 30f, 5f, 6f, true, 2.5f);
            HudTheme.Text(r, title, 58f, HudTheme.Bright, TextAnchor.MiddleCenter);
            float y = r.yMax + 12f * s;
            if (!string.IsNullOrEmpty(sub)) y = BannerLine(sub, y, HudTheme.Card, 21f);
            if (!string.IsNullOrEmpty(sub2)) BannerLine(sub2, y, HudTheme.Quota, 19f);
            HudAnim.End(mat);
        }

        /// <summary>One pill of text under a banner; returns where the next one starts.</summary>
        static float BannerLine(string text, float y, Color fill, float size)
        {
            float s = S;
            float w = Mathf.Min(HudTheme.Width(text, size) + 52f * s, Screen.width - 40f * s), h = (size + 19f) * s;
            var r = new Rect((Screen.width - w) * 0.5f, y, w, h);
            HudTheme.Pill(r, fill, 3f, 3f, 2f);
            HudTheme.Text(r, text, size, fill == HudTheme.Quota ? HudTheme.Ink : HudTheme.Bright, TextAnchor.MiddleCenter, false);
            return r.yMax + 8f * s;
        }

        // ====================================================================== a toon button (the end screen)

        /// <summary>A big plain button in the toon style; it grows when the mouse is over it. Call it on every event, not only Repaint. Returns true when clicked.</summary>
        public static bool Button(Rect r, string text, Color fill, float size = 26f)
        {
            var ev = Event.current;
            bool hover = r.Contains(ev.mousePosition);
            if (ev.type == EventType.Repaint)
            {
                float s = S;
                bool down = hover && Input.GetMouseButton(0);
                float grow = down ? -2f * s : hover ? 4f * s : 0f;
                var rr = new Rect(r.x - grow, r.y - grow, r.width + grow * 2f, r.height + grow * 2f);
                HudTheme.Pill(rr, hover ? HudTheme.Light(fill, 0.18f) : fill, 4f, down ? 1f : 4f, 2f);
                HudTheme.Text(rr, text, size, HudTheme.Bright, TextAnchor.MiddleCenter);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // ====================================================================== prompts and notices

        static readonly string[] promptText = new string[2], promptKey = new string[2], promptRest = new string[2];

        /// <summary>
        /// A prompt in the lower middle of the screen (<paramref name="y"/> is its top, in screen pixels). A leading "[E]" is drawn as a key cap.
        /// <paramref name="warn"/> draws the words in orange. It pops in when it appears or its words change. <paramref name="slot"/> 0 or 1:
        /// two prompts on screen at once (a second line) each need their own.
        /// </summary>
        public static void Prompt(string text, float y, bool warn = false, float size = 22f, int slot = 0)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            int id = slot == 0 ? HudAnim.Id.Prompt : HudAnim.Id.Prompt2;
            if (!string.Equals(text, promptText[slot]))                  // new words: pop in again
            {
                HudAnim.Restart(id);
                promptText[slot] = text; promptKey[slot] = null; promptRest[slot] = text;
                if (text[0] == '[')
                {
                    int e = text.IndexOf(']');
                    if (e > 1) { promptKey[slot] = text.Substring(1, e - 1); promptRest[slot] = text.Substring(e + 1).TrimStart(); }
                }
            }
            string key = promptKey[slot], rest = promptRest[slot];
            float k = HudAnim.Pop(id, true);
            float s = S, h = (size >= 20f ? 54f : 44f) * s, pad = 18f * s, gap = 10f * s;
            float tw = HudTheme.Width(rest, size), kh = h * 0.63f;
            float kw = key != null ? Mathf.Max(kh, HudTheme.TextWidthPx(key, 18f * s) + kh * 0.55f) : 0f;
            float total = Mathf.Min(pad * 2f + tw + (key != null ? kw + gap : 0f) + 4f * s, Screen.width - 24f * s);
            var r = new Rect((Screen.width - total) * 0.5f, y, total, h);
            var mat = HudAnim.Begin(k, r.center);
            HudTheme.Pill(r, HudTheme.Card, 3.5f, 4f, 2f);
            float x = r.x + pad;
            if (key != null) { HudTheme.KeyCap(x, r.y + (h - kh) * 0.5f, key, kh, 18f); x += kw + gap; }
            HudTheme.Text(new Rect(x, r.y, r.xMax - pad - x + 6f * s, h), rest, size, warn ? HudTheme.Warning : HudTheme.Bright, TextAnchor.MiddleLeft);
            HudAnim.End(mat);
        }

        /// <summary>A short message on a coloured pill (backup called, the alarm, quota met), centred, its top at <paramref name="y"/> (screen pixels).</summary>
        public static void Notice(string text, float y, Color accent, float size = 24f, int popId = HudAnim.Id.Notice)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            float k = HudAnim.Pop(popId, true);
            float s = S, h = (size * 1.95f) * s, pad = 28f * s;
            float w = Mathf.Min(HudTheme.Width(text, size) + pad * 2f, Screen.width - 24f * s);
            var r = new Rect((Screen.width - w) * 0.5f, y, w, h);
            var mat = HudAnim.Begin(k, r.center);
            HudTheme.Pill(r, accent, 3.5f, 4f, 2f);
            HudTheme.Text(r, text, size, HudTheme.Bright, TextAnchor.MiddleCenter);
            HudAnim.End(mat);
        }

        // ====================================================================== pack card (bottom left)

        /// <summary>What the pack card shows.</summary>
        public struct PackModel
        {
            public float weight, capacity;
            public int lootCount, lootValue;
            /// <summary>The item in hand, or null.</summary>
            public string inHand;
        }

        /// <summary>The backpack: weight bar (green, orange, red as it fills), the loot in it, what is in hand.</summary>
        public static void PackCard(in PackModel m)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, w = 350f * s, h = 136f * s, pad = 14f * s;
            var r = new Rect(20f * s, Screen.height - 98f * s - h, w, h);
            HudTheme.Panel(r, 16f);
            float f = Mathf.Clamp01(m.weight / Mathf.Max(0.1f, m.capacity));
            Color wc = f > 0.9f ? HudTheme.Danger : f > 0.65f ? HudTheme.Warning : HudTheme.Good;
            float x = r.x + pad, iw = r.width - pad * 2f, y = r.y + pad - 2f * s;

            HudIcons.Pack(new Rect(x, y, 34f * s, 34f * s), HudTheme.Info);
            HudTheme.Text(new Rect(x + 42f * s, y, 90f * s, 34f * s), "PACK", 20f, HudTheme.Bright);
            float wk = HudAnim.Bump(HudAnim.Id.Weight, Mathf.RoundToInt(m.weight * 10f));
            var mat = HudAnim.Begin(wk, new Vector2(r.xMax - pad - 60f * s, y + 17f * s));
            HudTheme.Text(new Rect(x + iw * 0.35f, y, iw * 0.65f, 34f * s), weightText.Get(Mathf.RoundToInt(m.weight * 10f), Mathf.RoundToInt(m.capacity * 10f)), 19f, wc, TextAnchor.MiddleRight);
            HudAnim.End(mat);
            y += 38f * s;
            HudTheme.Bar(new Rect(x, y, iw, 20f * s), f, wc);
            y += 28f * s;
            if (m.lootCount > 0)
            {
                HudIcons.Bag(new Rect(x, y, 26f * s, 26f * s));
                HudTheme.Text(new Rect(x + 34f * s, y, iw - 34f * s, 26f * s), lootText.Get(m.lootCount, m.lootValue), 18f, HudTheme.Quota);
            }
            else HudTheme.Text(new Rect(x, y, iw, 26f * s), "no loot in the pack", 16f, HudTheme.Dim, TextAnchor.MiddleLeft, false);
            y += 30f * s;
            if (!string.IsNullOrEmpty(m.inHand))
            {
                float cw = Chip(x, y, 24f * s, "IN HAND", HudTheme.Info, 13f);
                HudTheme.Text(new Rect(x + cw + 8f * s, y - 2f * s, iw - cw - 8f * s, 28f * s), m.inHand, 17f, HudTheme.Bright);
            }
            else HudTheme.Text(new Rect(x, y - 2f * s, iw, 26f * s), "TAB  inventory", 15f, HudTheme.Muted, TextAnchor.MiddleLeft, false);
        }

        // ====================================================================== the pew's ammunition (bottom right)

        /// <summary>What the ammunition panel shows.</summary>
        public struct AmmoModel
        {
            public string name;
            public int rounds, capacity;
            public bool reloading;
            public float reloadProgress;
        }

        /// <summary>The gun's name, rounds left (big; orange at 3 or fewer, red at none), one bullet per round, a reload bar.</summary>
        public static void AmmoPanel(in AmmoModel m)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, w = 350f * s, h = 136f * s, pad = 14f * s;
            var r = new Rect(Screen.width - w - 20f * s, Screen.height - 98f * s - h, w, h);
            HudTheme.Panel(r, 16f);
            Color accent = m.reloading ? HudTheme.Warning : m.rounds == 0 ? HudTheme.Danger : m.rounds <= 3 ? HudTheme.Warning : HudTheme.Bright;
            float x = r.x + pad, iw = r.width - pad * 2f, y = r.y + pad - 2f * s;

            if (!string.Equals(m.name, gunName)) { gunName = m.name; gunNameUpper = (m.name ?? "GUN").ToUpperInvariant(); }
            Chip(x, y + 4f * s, 26f * s, gunNameUpper, HudTheme.Quota, 14f);
            float punch = HudAnim.Bump(HudAnim.Id.Rounds, m.rounds);
            var count = new Rect(x + iw * 0.4f, y - 4f * s, iw * 0.6f, 46f * s);
            var mat = HudAnim.Begin(punch, count.center);
            HudTheme.Text(count, m.reloading ? "RELOADING" : ammoText.Get(m.rounds, m.capacity), m.reloading ? 28f : 40f, accent, TextAnchor.MiddleRight);
            HudAnim.End(mat);
            y += 52f * s;

            float bh = 36f * s, bw = Mathf.Min(bh * 0.5f, (iw - (m.capacity - 1) * 4f * s) / Mathf.Max(1, m.capacity));
            float rowW = m.capacity * bw + (m.capacity - 1) * 4f * s;
            for (int i = 0; i < m.capacity; i++)
                HudIcons.Bullet(new Rect(x + (iw - rowW) * 0.5f + i * (bw + 4f * s), y, bw, bh), i >= m.rounds, i < m.rounds && m.rounds <= 3 ? HudTheme.Warning : HudTheme.Quota);
            y += bh + 10f * s;
            if (m.reloading) HudTheme.Bar(new Rect(x, y, iw, 16f * s), m.reloadProgress, HudTheme.Warning, 2.5f);
            else
            {
                float kx = x;
                kx += HudTheme.KeyCap(kx, y - 2f * s, "LMB", 22f * s, 12f) + 6f * s;
                float lw = HudTheme.TextWidthPx("fire", 14f * s);
                HudTheme.TextPx(new Rect(kx, y - 2f * s, lw + 12f, 22f * s), "fire", 14f * s, HudTheme.Bright, TextAnchor.MiddleLeft, false);
                kx += lw + 18f * s;
                kx += HudTheme.KeyCap(kx, y - 2f * s, "R", 22f * s, 12f) + 6f * s;
                HudTheme.TextPx(new Rect(kx, y - 2f * s, 100f * s, 22f * s), "reload", 14f * s, HudTheme.Bright, TextAnchor.MiddleLeft, false);
            }
        }

        // ====================================================================== small things

        /// <summary>A small pill at the top right with a status light and words (the microphone, who is talking); advances <paramref name="y"/>.</summary>
        public static void VoicePill(ref float y, string text, Color light, bool mic = false, bool muted = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, h = 38f * s;
            float w = HudTheme.Width(text, 16f) + h * 1.55f;
            var r = new Rect(Screen.width - w - 20f * s, y, w, h);
            HudTheme.Pill(r, HudTheme.Card, 2.8f, 2.5f, 1.5f);
            if (mic) HudIcons.Mic(new Rect(r.x + 6f * s, r.y + 2f * s, h - 4f * s, h - 4f * s), light, muted);
            else HudIcons.Dot(new Vector2(r.x + h * 0.6f, r.center.y), 16f * s, light);
            HudTheme.TextPx(new Rect(r.x + h * 1.12f, r.y, r.width - h * 1.2f, h), text, 16f * s, HudTheme.Bright, TextAnchor.MiddleLeft, false);
            y += h + 8f * s;
        }

        /// <summary>The tag that says the test menu is on.</summary>
        public static void TestTag()
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, h = 30f * s, w = 170f * s;
            var r = new Rect(Screen.width - w - 20f * s, 12f * s, w, h);
            HudTheme.Chip(r, "TEST MODE   F1", HudTheme.Warning, 14f);
        }

        /// <summary>"BACKUP IN 12": the countdown to the next officer, under the status card (a siren and the seconds).</summary>
        public static void BackupChip(int seconds)
        {
            if (Event.current.type != EventType.Repaint) return;
            float k = HudAnim.Pop(HudAnim.Id.BackupChip, true);
            float s = S, h = 38f * s;
            string text = backupInText.Get(seconds);
            float w = HudTheme.Width(text, 17f) + h * 1.5f;
            var r = new Rect(20f * s, 224f * s, w, h);
            var mat = HudAnim.Begin(k * HudAnim.Pulse(7f, 0.025f), r.center);
            HudTheme.Pill(r, HudTheme.Warning, 3f, 3f, 1.5f);
            HudIcons.Siren(new Rect(r.x + 6f * s, r.y + 1f * s, h - 2f * s, h - 2f * s), Mathf.FloorToInt(Time.unscaledTime * 3f) % 2 == 0, false);
            HudTheme.Text(new Rect(r.x + h * 1.1f, r.y, r.width - h * 1.2f, h), text, 17f, HudTheme.Bright, TextAnchor.MiddleLeft);
            HudAnim.End(mat);
        }

        // ====================================================================== the radio-call ring (a guard calling for backup)

        /// <summary>
        /// A ring that fills clockwise as a guard radios for backup (<paramref name="radius"/> in reference pixels, <paramref name="centre"/> in screen pixels).
        /// The big one (top of the screen) shows a flashing siren in the middle, the small ones above the guards' heads a "!".
        /// </summary>
        public static void CallRing(Vector2 centre, float radius, float progress, bool big)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, R = radius * s, ow = Mathf.Max(2f, (big ? 4f : 2.5f) * s), thick = (big ? 17f : 8.5f) * s;
            progress = Mathf.Clamp01(progress);
            HudTheme.Circle(centre + new Vector2(0f, 4f * s), (R + ow) * 2f, HudTheme.Alpha(HudTheme.Ink, 0.38f));
            if (big) HudTheme.Circle(centre, (R + ow + 2.2f * s) * 2f, HudTheme.Alpha(HudTheme.Cream, 0.62f));
            HudTheme.Circle(centre, (R + ow) * 2f, HudTheme.Ink);
            HudTheme.Circle(centre, R * 2f, new Color32(0x2B, 0x20, 0x68, 255));

            // the arc: a trail of round dots along the middle of the band (round dots are smoothed by Unity, rotated rectangles are not)
            if (progress > 0.002f)
            {
                var arc = Color.Lerp(HudTheme.Warning, HudTheme.Danger, progress);
                var lit = HudTheme.Alpha(HudTheme.Light(arc, 0.55f), 0.8f);
                float mid = R - thick * 0.5f - 0.4f * s, dotD = thick - 1.8f * s, sweep = progress * 2f * Mathf.PI;
                int dots = Mathf.Max(2, Mathf.CeilToInt(mid * sweep / (dotD * 0.28f)));
                for (int i = 0; i <= dots; i++)
                {
                    float a = sweep * i / dots;                                  // clockwise from the top
                    HudTheme.Circle(new Vector2(centre.x + Mathf.Sin(a) * mid, centre.y - Mathf.Cos(a) * mid), dotD, arc);
                }
                float edge = mid + thick * 0.2f;
                for (int i = 0; i <= dots; i += 2)
                {
                    float a = sweep * i / dots;
                    HudTheme.Circle(new Vector2(centre.x + Mathf.Sin(a) * edge, centre.y - Mathf.Cos(a) * edge), thick * 0.3f, lit);
                }
            }

            float inner = R - thick;
            HudTheme.Circle(centre, inner * 2f, HudTheme.Ink);
            HudTheme.Circle(centre, (inner - ow) * 2f, HudTheme.Card);
            if (big)
            {
                bool on = Mathf.FloorToInt(Time.unscaledTime * 4f) % 2 == 0;
                float d = (inner - ow) * 1.55f;
                HudIcons.Siren(new Rect(centre.x - d * 0.5f, centre.y - d * 0.5f, d, d), true, !on);
            }
            else HudTheme.TextPx(new Rect(centre.x - R, centre.y - R, R * 2f, R * 2f), "!", R * 1.15f, progress >= 1f ? HudTheme.Danger : HudTheme.Bright, TextAnchor.MiddleCenter, false);
        }

        // ====================================================================== pinned / busted

        /// <summary>
        /// The local player's own custody state, big in the middle of the screen: handcuffs, PINNED with a round seconds badge (the colour runs from
        /// yellow to red as <paramref name="fresh"/> goes from 1 to 0), or BUSTED with the buy-back price; and a line of help under it.
        /// </summary>
        public static void CustodyMine(bool pinned, int secondsLeft, float fresh, string help, int buyBack = 0)
        {
            if (Event.current.type != EventType.Repaint) return;
            int id = pinned ? HudAnim.Id.Pinned : HudAnim.Id.Busted;
            float k = HudAnim.Pop(id, true);
            float s = S, w = Mathf.Min(Screen.width - 40f * s, 780f * s), h = 150f * s;
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.32f, w, h);
            var mat = HudAnim.Begin(k, r.center);
            Color body = pinned ? Color.Lerp(HudTheme.Danger, HudTheme.Quota, Mathf.Clamp01(fresh) * 0.9f) : HudTheme.Dim;
            HudTheme.Body(r, body, 34f, 5f, 7f, true, 2.5f);
            HudIcons.Cuffs(new Rect(r.x + 26f * s, r.y + (h - 96f * s) * 0.5f, 96f * s, 96f * s), HudTheme.Light(HudTheme.Dim, 0.5f));
            float tx = r.x + 134f * s, tw = r.width - 134f * s - (pinned ? 150f * s : 24f * s);
            HudTheme.Text(new Rect(tx, r.y + 8f * s, tw, h - 16f * s), pinned ? "PINNED" : "BUSTED", 70f, HudTheme.Bright, TextAnchor.MiddleLeft);
            if (pinned)
            {
                float punch = HudAnim.Bump(HudAnim.Id.Ring, secondsLeft);
                var bc = new Vector2(r.xMax - 80f * s, r.center.y);
                var m2 = HudAnim.Begin(punch, bc);
                HudIcons.Badge(new Rect(bc.x - 56f * s, bc.y - 56f * s, 112f * s, 112f * s), HudTheme.Card);
                HudTheme.Text(new Rect(bc.x - 56f * s, bc.y - 56f * s, 112f * s, 112f * s), intText.Get(secondsLeft), 66f, HudTheme.Bright, TextAnchor.MiddleCenter);
                HudAnim.End(m2);
            }
            else if (buyBack > 0)
            {
                var cr = new Rect(r.xMax - 150f * s, r.center.y - 18f * s, 130f * s, 36f * s);
                HudTheme.Pill(cr, HudTheme.Card, 3f, 2f);
                HudIcons.Coin(new Rect(cr.x + 6f * s, cr.y + 3f * s, 30f * s, 30f * s));
                HudTheme.Text(new Rect(cr.x + 40f * s, cr.y, cr.width - 44f * s, cr.height), HudTheme.Money(buyBack), 20f, HudTheme.Quota, TextAnchor.MiddleLeft);
            }
            if (!string.IsNullOrEmpty(help))
            {
                float hw = Mathf.Min(HudTheme.Width(help, 21f) + 56f * s, Screen.width - 40f * s), hh = 42f * s;
                var hr = new Rect((Screen.width - hw) * 0.5f, r.yMax + 16f * s, hw, hh);
                HudTheme.Pill(hr, HudTheme.Card, 3f, 3f, 2f);
                HudTheme.Text(hr, help, 21f, HudTheme.Bright, TextAnchor.MiddleCenter, false);
            }
            HudAnim.End(mat);
        }

        /// <summary>A tag over a teammate who is down: handcuffs and "PINNED 9" (red) or "BUSTED" (grey). <paramref name="centre"/> is in screen pixels.</summary>
        public static void CustodyTag(Vector2 centre, bool pinned, int secondsLeft)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, h = 36f * s;
            string text = pinned ? pinnedText.Get(secondsLeft) : "BUSTED";
            float w = HudTheme.Width(text, 17f) + h * 2.0f;
            var r = new Rect(centre.x - w * 0.5f, centre.y - h * 0.5f, w, h);
            float k = pinned ? HudAnim.Pulse(9f, 0.05f) : 1f;
            var mat = HudAnim.Begin(k, centre);
            HudTheme.Pill(r, pinned ? HudTheme.Danger : HudTheme.Dim, 3f, 3f, 1.5f);
            HudIcons.Cuffs(new Rect(r.x + 8f * s, r.y + 1f * s, h - 2f * s, h - 2f * s), HudTheme.Light(HudTheme.Dim, 0.6f));
            HudTheme.Text(new Rect(r.x + h * 1.2f, r.y, r.width - h * 1.3f, h), text, 17f, HudTheme.Bright, TextAnchor.MiddleLeft);
            HudAnim.End(mat);
        }

        // ====================================================================== the crosshair

        /// <summary>
        /// The reticle: a dot with four ticks, ink-outlined. <paramref name="locked"/> pulls the ticks in; the whole thing gives a little punch when
        /// <paramref name="kind"/> changes (nothing / something to use / a foe). <paramref name="scale"/> is the player's size setting.
        /// </summary>
        public static void Reticle(float cx, float cy, Color colour, bool locked, int kind, float scale)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S * scale;
            float punch = HudAnim.Bump(HudAnim.Id.Reticle, kind);
            var mat = HudAnim.Begin(punch, new Vector2(cx, cy));
            float thick = Mathf.Max(3f, 4.5f * s), arm = 11f * s, gap = (locked ? 5f : 10f) * s, dot = 9f * s, ow = Mathf.Max(1.5f, 1.8f * s);
            for (int pass = 0; pass < 2; pass++)
            {
                float g = pass == 0 ? ow : 0f;
                var col = pass == 0 ? HudTheme.Ink : colour;
                float t = thick + g * 2f, a = arm + g * 2f;
                HudTheme.Rounded(new Rect(cx - t * 0.5f, cy - gap - arm - g, t, a), col, t * 0.5f);      // up
                HudTheme.Rounded(new Rect(cx - t * 0.5f, cy + gap - g, t, a), col, t * 0.5f);            // down
                HudTheme.Rounded(new Rect(cx - gap - arm - g, cy - t * 0.5f, a, t), col, t * 0.5f);      // left
                HudTheme.Rounded(new Rect(cx + gap - g, cy - t * 0.5f, a, t), col, t * 0.5f);            // right
                HudTheme.Circle(new Vector2(cx, cy), dot + g * 2f, col);
            }
            HudAnim.End(mat);
        }
    }
}
