using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The pieces of the in-run HUD, built from <see cref="HudTheme"/>, <see cref="HudIcons"/> and <see cref="HudAnim"/>. Each widget takes plain
    /// values (a small model struct or a few arguments), never the game's classes, so the same code draws the real HUD and the test gallery.
    /// Call them from <c>OnGUI</c>; they draw only on the Repaint event.
    ///
    /// The HUD is kept small on purpose (the owner asked for less of the screen to be covered, 2026-10-16): compact cards in the corners, a
    /// slim banner, and no key hints at all - every control is listed in the Keybinds screen (<see cref="KeybindScreen"/>, opened from
    /// Settings and from the Esc menu). Sizes below are reference pixels at 1080 high (see <see cref="HudTheme.S"/>).
    /// </summary>
    public static class HudWidgets
    {
        static float S => HudTheme.S;

        // text built from numbers is built once per number, not once per frame (see CachedText1 / CachedText2)
        static readonly CachedText1 intText = new CachedText1(string.Empty);
        static readonly CachedText2 floorText = new CachedText2((level, last) => "FLOOR " + level + "/" + last);
        static readonly CachedText1 nextQuotaText = new CachedText1(n => "NEXT QUOTA  " + HudTheme.Money(n));
        static readonly CachedText2 quotaText = new CachedText2((banked, quota) => "QUOTA  " + HudTheme.Money(banked) + " / " + HudTheme.Money(quota));
        static readonly CachedText1 lostText = new CachedText1(n => "LOST " + HudTheme.Money(n));
        static readonly CachedText1 runText = new CachedText1(n => "RUN TOTAL " + HudTheme.Money(n));
        static readonly CachedText1 freeText = new CachedText1("FREE ");
        static readonly CachedText1 pinnedText = new CachedText1("PINNED ");
        static readonly CachedText1 bustedText = new CachedText1("BUSTED ");
        static readonly CachedText2 weightText = new CachedText2((w, c) => WeightUnits.Num(w / 10f) + " / " + WeightUnits.Num(c / 10f) + " lbs");
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
            /// <summary>The floor's level (0 = the docks) and how many stops there are in all (shown as "level/last").</summary>
            public int level, stops;
            public bool docks, boss, met;
            /// <summary>At the docks with the demo's last floor finished: nothing left to climb to.</summary>
            public bool demoDone;
            public int cash, banked, quota, lost, runTotal, nextQuota;
            public int free, pinned, busted;
        }

        /// <summary>
        /// The card at the top left: the cash (big, green) with the floor tag at its right end and the quota bar under it; under the card a row of
        /// small chips (lost, run total, and how the team stands with the police - only when there is a team or somebody is down).
        /// </summary>
        public static void StatusCard(in StatusModel m)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S;
            float x = 14f * s, y = 12f * s, w = 268f * s, h = 78f * s, pad = 9f * s;
            HudTheme.Plate(new Rect(x, y, w, h));

            // ---- the floor tag, at the right end of the first row
            string tag = m.docks ? "DOCKS" : m.boss ? "BOSS" : floorText.Get(m.level, Mathf.Max(0, m.stops - 1));
            Color tagColour = m.boss ? HudTheme.Danger : m.docks ? HudTheme.Warning : HudTheme.Info;
            float tagW = Mathf.Max(66f * s, HudTheme.Width(tag, 14f) + 22f * s);
            float rowY = y + pad - 1f * s, rowH = 36f * s;
            HudTheme.Chip(new Rect(x + w - pad - tagW, rowY + 5f * s, tagW, 26f * s), tag, tagColour, 14f);

            // ---- cash: a coin and the money, big and green; it punches when it changes and counts up
            int shownCash = HudAnim.Count(HudAnim.Id.Cash, m.cash);
            float punch = HudAnim.Bump(HudAnim.Id.Cash, m.cash);
            var cashRow = new Rect(x + pad, rowY, w - pad * 2f - tagW - 8f * s, rowH);
            var mat = HudAnim.Begin(punch, new Vector2(cashRow.x + 15f * s, cashRow.center.y));
            HudIcons.Coin(new Rect(cashRow.x, cashRow.y + 3f * s, 30f * s, 30f * s));
            HudTheme.Text(new Rect(cashRow.x + 36f * s, cashRow.y, cashRow.width - 36f * s, rowH), HudTheme.Money(shownCash), 32f, HudTheme.Cash);
            HudAnim.End(mat);

            // ---- the quota bar (or what replaces it on the docks and the boss floor)
            var bar = new Rect(x + pad, y + pad + 40f * s, w - pad * 2f, 20f * s);
            if (m.docks)
            {
                HudTheme.Pill(bar, HudTheme.Quota, 2.5f, 2.5f);
                string dockText = m.nextQuota > 0 ? nextQuotaText.Get(m.nextQuota) : m.demoDone ? "DEMO COMPLETE" : "BUY AND GET READY";
                HudTheme.Text(bar, dockText, 14f, HudTheme.Ink, TextAnchor.MiddleCenter, false);        // (ink on the yellow bar reads better than outlined white)
            }
            else if (m.boss)
            {
                HudTheme.Pill(bar, HudTheme.Danger, 2.5f, 2.5f);
                HudTheme.Text(bar, "KNOCK OUT THE BOSS!", 14f, HudTheme.Bright, TextAnchor.MiddleCenter);
            }
            else
            {
                float f = m.quota > 0 ? Mathf.Clamp01(m.banked / (float)m.quota) : 0f;
                float bankedPunch = 1f + (HudAnim.Bump(HudAnim.Id.Banked, m.banked) - 1f) * 0.3f;      // (a gentler punch than the cash: the bar must stay inside its card)
                var mat2 = HudAnim.Begin(bankedPunch, bar.center);
                HudTheme.Bar(bar, m.met ? 1f : f, m.met ? HudTheme.Cash : HudTheme.Quota, 2.5f);
                string label = m.met ? "QUOTA MET!" : quotaText.Get(m.banked, m.quota);
                HudTheme.BarLabel(bar, m.met ? 1f : f, label, 14f, 2.5f);
                HudAnim.End(mat2);
                if (m.met)
                {
                    float t = Time.unscaledTime;
                    HudIcons.Sparkle(new Vector2(bar.xMax - 8f * s, bar.y - 3f * s), 16f * s * (0.8f + 0.2f * Mathf.Sin(t * 6f)), HudTheme.Bright, t * 40f);
                    HudIcons.Sparkle(new Vector2(bar.x + 10f * s, bar.yMax + 1f * s), 11f * s * (0.8f + 0.2f * Mathf.Sin(t * 5f + 2f)), HudTheme.Quota, -t * 30f);
                }
            }

            // ---- chips under the card: lost, run total, the team
            float cy = y + h + 5f * s, ch = 22f * s, cx = x;
            if (m.lost > 0 && !m.docks) cx += Chip(cx, cy, ch, lostText.Get(HudAnim.Count(HudAnim.Id.Lost, m.lost)), HudTheme.Lost) + 6f * s;
            if (m.runTotal > 0 || m.docks) cx += Chip(cx, cy, ch, runText.Get(HudAnim.Count(HudAnim.Id.RunTotal, m.runTotal)), HudTheme.Info) + 6f * s;
            TeamChips(cx, cy, ch, m);
            HudAnim.DrawFloaters(new Vector2(x + w + 28f * s, y + pad + 18f * s), 24f, new Vector2(28f, -18f));
        }

        /// <summary>A chip sized to its text; returns its width.</summary>
        static float Chip(float x, float y, float h, string text, Color fill, float size = 13f)
        {
            float w = HudTheme.Width(text, size) + h * 0.9f;
            HudTheme.Chip(new Rect(x, y, w, h), text, fill, size);
            return w;
        }

        /// <summary>
        /// "FREE 2", and "PINNED 1" / "BUSTED 1" while there are any: how the team stands with the police. A player on their own has nothing to
        /// compare, so "FREE 1" is not shown for them (it comes back as soon as there is a team or somebody is down).
        /// </summary>
        static void TeamChips(float x, float y, float h, in StatusModel m)
        {
            float s = S;
            if (m.free + m.pinned + m.busted > 1) x += Chip(x, y, h, freeText.Get(m.free), HudTheme.Good) + 6f * s;
            if (m.pinned > 0)
            {
                float k = HudAnim.Pulse(9f, 0.04f);
                var mat = HudAnim.Begin(k, new Vector2(x, y + h * 0.5f));
                x += Chip(x, y, h, pinnedText.Get(m.pinned), HudTheme.Danger) + 6f * s;
                HudAnim.End(mat);
            }
            if (m.busted > 0) Chip(x, y, h, bustedText.Get(m.busted), HudTheme.Dim);
        }

        // ====================================================================== player badge (top right)

        static readonly CachedText1 playerText = new CachedText1("PLAYER ");

        /// <summary>
        /// "PLAYER 2" on a chip in the player's own colour, at the top right: the number the person at the security computer sees on the Nav map
        /// (the owner, 2026-10-17: players should know what number they are, so the computer can say "Player 2, go left").
        /// </summary>
        public static void PlayerBadge(int number)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S;
            string text = playerText.Get(number);
            float w = Mathf.Max(132f * s, HudTheme.Width(text, 20f) + 40f * s), h = 36f * s;
            HudTheme.Chip(new Rect(Screen.width - w - 16f * s, 14f * s, w, h), text, HudTheme.PlayerColour(number), 20f, HudTheme.Ink);
        }

        /// <summary>
        /// Where the second thing of the top right column starts (reference pixels): right under the player badge (14 + 36 tall + a 6 gap). The owner, 2026-10-20 night: the badge and another bubble were drawn on top of each other ("put them below each
        /// other so we can decide which one we want"): the test-mode tag (offline) and the voice pills (online) now start here, the pvp board under them.
        /// </summary>
        public const float TopRightBelowBadge = 56f;

        // ====================================================================== banner (the moments that matter)

        /// <summary>A slim ribbon at the top middle with a title and up to two lines under it; pops in with a spring.</summary>
        public static void Banner(string title, string sub, Color accent, string sub2 = null, int popId = HudAnim.Id.Banner)
        {
            if (Event.current.type != EventType.Repaint) return;
            float k = HudAnim.Pop(popId, !string.IsNullOrEmpty(title));
            if (k <= 0.01f || string.IsNullOrEmpty(title)) return;
            float s = S;
            float tw = Mathf.Min(HudTheme.Width(title, 32f) + 60f * s, Screen.width - 40f * s), th = 48f * s;
            var r = new Rect((Screen.width - tw) * 0.5f, 14f * s, tw, th);
            var mat = HudAnim.Begin(k, r.center);
            HudTheme.Body(r, accent, 20f, 3.5f, 4f, true, 1.8f);
            HudTheme.Text(r, title, 32f, HudTheme.Bright, TextAnchor.MiddleCenter);
            float y = r.yMax + 8f * s;
            if (!string.IsNullOrEmpty(sub)) y = BannerLine(sub, y, HudTheme.Card, 16f);
            if (!string.IsNullOrEmpty(sub2)) BannerLine(sub2, y, HudTheme.Quota, 14f);
            HudAnim.End(mat);
        }

        /// <summary>One pill of text under a banner; returns where the next one starts.</summary>
        static float BannerLine(string text, float y, Color fill, float size)
        {
            float s = S;
            float w = Mathf.Min(HudTheme.Width(text, size) + 36f * s, Screen.width - 40f * s), h = (size + 12f) * s;
            var r = new Rect((Screen.width - w) * 0.5f, y, w, h);
            HudTheme.Pill(r, fill, 2.5f, 2.5f, 1.5f);
            HudTheme.Text(r, text, size, fill == HudTheme.Quota ? HudTheme.Ink : HudTheme.Bright, TextAnchor.MiddleCenter, false);
            return r.yMax + 6f * s;
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

        /// <summary>The prompts and notices are drawn a little smaller than the size their callers ask for (they asked for big, 2026-10-16: less screen).</summary>
        const float PromptShrink = 0.82f, NoticeShrink = 0.85f;

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
            float sz = size * PromptShrink;
            float s = S, h = (sz + 20f) * s, pad = 14f * s, gap = 8f * s;
            float tw = HudTheme.Width(rest, sz), kh = h * 0.64f;
            float kw = key != null ? Mathf.Max(kh, HudTheme.TextWidthPx(key, 15f * s) + kh * 0.55f) : 0f;
            float total = Mathf.Min(pad * 2f + tw + (key != null ? kw + gap : 0f) + 4f * s, Screen.width - 24f * s);
            var r = new Rect((Screen.width - total) * 0.5f, y, total, h);
            var mat = HudAnim.Begin(k, r.center);
            HudTheme.Pill(r, HudTheme.Card, 3f, 3.5f, 1.6f);
            float x = r.x + pad;
            if (key != null) { HudTheme.KeyCap(x, r.y + (h - kh) * 0.5f, key, kh, 15f); x += kw + gap; }
            HudTheme.Text(new Rect(x, r.y, r.xMax - pad - x + 6f * s, h), rest, sz, warn ? HudTheme.Warning : HudTheme.Bright, TextAnchor.MiddleLeft);
            HudAnim.End(mat);
        }

        /// <summary>The height of a prompt's pill at its default size, in reference pixels (for stacking a second prompt above or below the first).</summary>
        public const float PromptStep = 46f;

        /// <summary>A short message on a coloured pill (backup called, the alarm, quota met), centred, its top at <paramref name="y"/> (screen pixels).</summary>
        public static void Notice(string text, float y, Color accent, float size = 24f, int popId = HudAnim.Id.Notice)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            float k = HudAnim.Pop(popId, true);
            float sz = size * NoticeShrink;
            float s = S, h = sz * 1.9f * s, pad = 22f * s;
            float w = Mathf.Min(HudTheme.Width(text, sz) + pad * 2f, Screen.width - 24f * s);
            var r = new Rect((Screen.width - w) * 0.5f, y, w, h);
            var mat = HudAnim.Begin(k, r.center);
            HudTheme.Pill(r, accent, 3f, 3.5f, 1.6f);
            HudTheme.Text(r, text, sz, HudTheme.Bright, TextAnchor.MiddleCenter);
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

        /// <summary>
        /// The backpack, as small as it can be: the weight bar (green, orange, red as it fills) always; a line for the loot in it and a line for
        /// what is in hand only while there is something to say. The card grows upwards from the bottom corner.
        /// NOT DRAWN ANY MORE (the owner, 2026-10-20 night: "remove the backpack from the bottom left corner"): the weight, and how much more fits, are in the Tab window (<c>Backpack.DrawInventory</c>). Kept for a quick way back.
        /// </summary>
        public static void PackCard(in PackModel m)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, w = 236f * s, pad = 10f * s;
            bool loot = m.lootCount > 0, hand = !string.IsNullOrEmpty(m.inHand);
            float h = pad + 26f * s + 4f * s + 13f * s + (loot ? 6f * s + 22f * s : 0f) + (hand ? 5f * s + 22f * s : 0f) + pad;
            var r = new Rect(14f * s, Screen.height - 14f * s - h, w, h);
            HudTheme.Plate(r);
            float f = Mathf.Clamp01(m.weight / Mathf.Max(0.1f, m.capacity));
            Color wc = f > 0.9f ? HudTheme.Danger : f > 0.65f ? HudTheme.Warning : HudTheme.Good;
            Color wt = f > 0.9f ? HudTheme.Light(HudTheme.Danger, 0.4f) : wc;            // (on the dark card a lighter red reads, the bar keeps the strong one)
            float x = r.x + pad, iw = r.width - pad * 2f, y = r.y + pad;

            HudIcons.Pack(new Rect(x, y, 24f * s, 24f * s), HudTheme.Info);
            HudTheme.Text(new Rect(x + 30f * s, y, 70f * s, 26f * s), "PACK", 16f, HudTheme.Bright);
            float wk = HudAnim.Bump(HudAnim.Id.Weight, Mathf.RoundToInt(m.weight * 10f));
            var mat = HudAnim.Begin(wk, new Vector2(r.xMax - pad - 40f * s, y + 13f * s));
            HudTheme.Text(new Rect(x + iw * 0.4f, y, iw * 0.6f, 26f * s), weightText.Get(Mathf.RoundToInt(m.weight * 10f), Mathf.RoundToInt(m.capacity * 10f)), 15f, wt, TextAnchor.MiddleRight);
            HudAnim.End(mat);
            y += 30f * s;
            HudTheme.Bar(new Rect(x, y, iw, 13f * s), f, wc, 2.5f);
            y += 13f * s;
            if (loot)
            {
                y += 6f * s;
                HudIcons.Bag(new Rect(x, y, 22f * s, 22f * s));
                HudTheme.Text(new Rect(x + 28f * s, y, iw - 28f * s, 22f * s), lootText.Get(m.lootCount, m.lootValue), 15f, HudTheme.Quota);
                y += 22f * s;
            }
            if (hand)
            {
                y += 5f * s;
                float cw = Chip(x, y + 1f * s, 20f * s, "IN HAND", HudTheme.Info, 11f);
                HudTheme.Text(new Rect(x + cw + 7f * s, y, iw - cw - 7f * s, 22f * s), m.inHand, 15f, HudTheme.Bright);
            }
        }

        // ====================================================================== the weapon bar (bottom centre)

        /// <summary>What the weapon bar shows: the name of the weapon in each slot (null = empty) and which slot is in hand (-1 for none).</summary>
        public struct WeaponBarModel
        {
            public string[] names;
            public int equipped;
            /// <summary>A weapon is being dragged (the pack window is open): the slot the mouse is over lights up (<see cref="hover"/>, -1 for none) and the slot the weapon was picked up from is dimmed (<see cref="source"/>, -1 for none).</summary>
            public bool dragging;
            public int hover, source;
        }

        /// <summary>Where slot <paramref name="i"/> (0 to 5) of the weapon bar is, in screen pixels: the one place that knows the bar's geometry, for drawing it and for dropping a weapon on it.</summary>
        public static Rect WeaponBarSlot(int i)
        {
            float s = S, sh = 48f * s, gap = 6f * s;
            float room = Screen.width - 2f * 262f * s - (WeaponBarRules.Slots - 1) * gap;
            float sw = Mathf.Clamp(room / WeaponBarRules.Slots, 56f * s, 176f * s);
            float total = WeaponBarRules.Slots * sw + (WeaponBarRules.Slots - 1) * gap;
            float x0 = (Screen.width - total) * 0.5f, y = Screen.height - 74f * s - sh;
            return new Rect(x0 + i * (sw + gap), y, sw, sh);
        }

        /// <summary>A chip with its left edge at <paramref name="x"/> (for the other screens that draw one); returns its width.</summary>
        public static float ChipAt(float x, float y, float h, string text, Color fill, float size = 13f) => Chip(x, y, h, text, fill, size);

        /// <summary>
        /// The weapon bar (the owner, 2026-10-19 night; six slots since 2026-10-20 night): plates above the stamina bar, each with its key (1 to 6) and the weapon in that slot (<see cref="WeaponBarRules"/>); the one in hand is bright, with a bar under it.
        /// The plates are see-through (<see cref="HudTheme.Glass"/>) and as wide as the room that is left either side for the corners of the screen (the ammunition panel on the right, the same room on the left) allows, at most 176 px each.
        /// </summary>
        public static void WeaponBar(in WeaponBarModel m)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, sh = 48f * s, pad = 8f * s;
            for (int i = 0; i < WeaponBarRules.Slots; i++)
            {
                var r = WeaponBarSlot(i);
                bool on = m.equipped == i, has = m.names != null && i < m.names.Length && !string.IsNullOrEmpty(m.names[i]);
                bool drop = m.dragging && m.hover == i, src = m.dragging && m.source == i;
                // see-through plates (the owner, 2026-10-20 night): the one in hand has a bright pale-blue edge, an empty slot a faint one; while a weapon is dragged (2026-10-22) the slot under the mouse lights up in blue and the one it came from is dimmed
                HudTheme.Glass(r, drop ? HudTheme.GlassAlpha * 1.8f : HudTheme.GlassAlpha, drop ? 1f : src ? 0.2f : on ? 0.95f : has ? 0.5f : 0.28f, null, drop ? HudTheme.Info : on ? new Color(0.84f, 0.91f, 1f) : (Color?)null);
                float kw = HudTheme.KeyCap(r.x + pad, r.y + (sh - 28f * s) * 0.5f - 2f * s, (i + 1).ToString(), 28f * s, 15f);
                if (has)
                {
                    float tx = r.x + pad + kw + 8f * s, tw = r.xMax - pad - tx;
                    HudTheme.Text(new Rect(tx, r.y, tw, sh - 6f * s), MenuKit.Fit(m.names[i], 15f * s, tw), 15f, src ? HudTheme.Dim : on ? HudTheme.Bright : HudTheme.Muted, TextAnchor.MiddleLeft);
                }
                if (on) HudTheme.Bar(new Rect(r.x + pad, r.yMax - 12f * s, r.width - pad * 2f, 6f * s), 1f, HudTheme.Info, 1.5f);
            }
        }

        // ====================================================================== the pew's ammunition (bottom right)

        /// <summary>What the ammunition panel shows.</summary>
        public struct AmmoModel
        {
            public string name;
            public int rounds, capacity;
            /// <summary>The bullets carried besides the ones in the gun: the second number, with no upper limit (the owner, 2026-10-23: "In the ammo UI the second number should be unbound for however many bullets you have"). -1 = not shown (the gun's reserve is endless).</summary>
            public int reserve;
            public bool reloading;
            public float reloadProgress;
            /// <summary>While reloading: how many rounds this reload puts in the gun and how many are in so far; the ring counts them when <see cref="counted"/> (the shotgun: "for the shotgun it should display a counter as well for each shell getting loaded").</summary>
            public int loads, loaded;
            public bool counted;
            /// <summary>The basement: the ammunition never runs out.</summary>
            public bool unlimited;
        }

        /// <summary>The dots of the reload ring, and how many of them are lit at this fraction (a fraction of 0 lights none, 1 all of them).</summary>
        public const int RingDots = 60;
        public static int RingDotsLit(float fraction) => Mathf.RoundToInt(Mathf.Clamp01(fraction) * RingDots);

        /// <summary>A ring that fills clockwise from the top (IMGUI has no arcs: a string of dots): <paramref name="fraction"/> of it is <paramref name="fill"/>, the rest <paramref name="back"/>.</summary>
        public static void ProgressRing(Vector2 centre, float diameter, float dot, float fraction, Color fill, Color back)
        {
            float radius = (diameter - dot) * 0.5f;
            int lit = RingDotsLit(fraction);
            for (int i = 0; i < RingDots; i++)
            {
                float a = (-90f + 360f * i / RingDots) * Mathf.Deg2Rad;
                HudTheme.Circle(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, dot, i < lit ? fill : back);
            }
        }

        /// <summary>
        /// The gun's name, "rounds in the gun / bullets carried" (orange at 3 rounds or fewer, red at none) and one bullet icon per round of the magazine; while it reloads the icons give way to a RING that fills clockwise (the owner,
        /// 2026-10-23: "A circular loading bar for reloading progress"), with a counter in it for the shotgun that steps up for every shell. A small panel in the bottom right corner.
        /// </summary>
        public static void AmmoPanel(in AmmoModel m)
        {
            if (Event.current.type != EventType.Repaint) return;
            bool ring = m.reloading && !m.unlimited;
            float s = S, w = 196f * s, h = (ring ? 100f : 68f) * s, pad = 9f * s;
            var r = new Rect(Screen.width - 14f * s - w, Screen.height - 14f * s - h, w, h);
            HudTheme.Glass(r, 0.3f, 0.5f);                                       // (see-through, like the weapon bar: the owner, 2026-10-20 night)
            Color accent = m.reloading ? HudTheme.Warning : m.rounds == 0 ? HudTheme.Danger : m.rounds <= 3 ? HudTheme.Warning : HudTheme.Bright;
            float x = r.x + pad, iw = r.width - pad * 2f, y = r.y + pad - 1f * s;

            if (!string.Equals(m.name, gunName)) { gunName = m.name; gunNameUpper = (m.name ?? "GUN").ToUpperInvariant(); }
            Chip(x, y + 4f * s, 20f * s, gunNameUpper, HudTheme.Quota, 12f);
            float punch = HudAnim.Bump(HudAnim.Id.Rounds, m.rounds);
            var count = new Rect(x + iw * 0.38f, y - 2f * s, iw * 0.62f, 30f * s);
            var mat = HudAnim.Begin(punch, count.center);
            HudTheme.Text(count, m.unlimited ? "UNLIMITED" : m.reloading ? "RELOADING" : ammoText.Get(m.rounds, m.reserve >= 0 ? m.reserve : m.capacity), m.unlimited || m.reloading ? 18f : 28f, m.unlimited ? HudTheme.Good : accent == HudTheme.Danger ? HudTheme.Light(accent, 0.4f) : accent, TextAnchor.MiddleRight);
            HudAnim.End(mat);
            y += 32f * s;

            if (ring)
            {
                float d = 56f * s, dot = 6f * s;
                var centre = new Vector2(r.center.x, r.yMax - pad - d * 0.5f);
                ProgressRing(centre, d, dot, m.reloadProgress, HudTheme.Warning, HudTheme.Alpha(HudTheme.Ink, 0.45f));
                if (m.counted && m.loads > 0) HudTheme.Text(new Rect(centre.x - d * 0.35f, centre.y - d * 0.25f, d * 0.7f, d * 0.5f), (m.rounds + m.loaded) + "/" + m.capacity, 15f, HudTheme.Bright, TextAnchor.MiddleCenter);
                return;
            }
            float bh = 18f * s, gap = 3f * s, bw = Mathf.Min(bh * 0.5f, (iw - (m.capacity - 1) * gap) / Mathf.Max(1, m.capacity));
            float rowW = m.capacity * bw + (m.capacity - 1) * gap;
            for (int i = 0; i < m.capacity; i++)
                HudIcons.Bullet(new Rect(x + (iw - rowW) * 0.5f + i * (bw + gap), y, bw, bh), i >= m.rounds, i < m.rounds && m.rounds <= 3 ? HudTheme.Warning : HudTheme.Quota);
        }

        // ====================================================================== small things

        /// <summary>A small pill at the top right with a status light and words (the microphone, who is talking); advances <paramref name="y"/>.</summary>
        public static void VoicePill(ref float y, string text, Color light, bool mic = false, bool muted = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, h = 30f * s;
            float w = HudTheme.Width(text, 14f) + h * 1.5f;
            var r = new Rect(Screen.width - w - 14f * s, y, w, h);
            HudTheme.Pill(r, HudTheme.Card, 2.5f, 2.5f, 1.4f);
            if (mic) HudIcons.Mic(new Rect(r.x + 5f * s, r.y + 1f * s, h - 2f * s, h - 2f * s), light, muted);
            else HudIcons.Dot(new Vector2(r.x + h * 0.55f, r.center.y), 13f * s, light);
            HudTheme.TextPx(new Rect(r.x + h * 1.05f, r.y, r.width - h * 1.12f, h), text, 14f * s, HudTheme.Bright, TextAnchor.MiddleLeft, false);
            y += h + 6f * s;
        }

        /// <summary>The tag that says the test menu is on.</summary>
        public static void TestTag()
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, h = 24f * s, w = 138f * s;
            var r = new Rect(Screen.width - w - 16f * s, TopRightBelowBadge * s, w, h);          // (under the player badge, not behind it)
            HudTheme.Chip(r, "TEST MODE   F1", HudTheme.Warning, 12f);
        }

        /// <summary>"BACKUP IN 12": the countdown to the next officer, under the status chips (a siren and the seconds).</summary>
        public static void BackupChip(int seconds)
        {
            if (Event.current.type != EventType.Repaint) return;
            float k = HudAnim.Pop(HudAnim.Id.BackupChip, true);
            float s = S, h = 30f * s;
            string text = backupInText.Get(seconds);
            float w = HudTheme.Width(text, 15f) + h * 1.5f;
            var r = new Rect(14f * s, 122f * s, w, h);
            var mat = HudAnim.Begin(k * HudAnim.Pulse(7f, 0.025f), r.center);
            HudTheme.Pill(r, HudTheme.Warning, 2.5f, 2.5f, 1.4f);
            HudIcons.Siren(new Rect(r.x + 5f * s, r.y + 1f * s, h - 2f * s, h - 2f * s), Mathf.FloorToInt(Time.unscaledTime * 3f) % 2 == 0, false);
            HudTheme.Text(new Rect(r.x + h * 1.1f, r.y, r.width - h * 1.2f, h), text, 15f, HudTheme.Bright, TextAnchor.MiddleLeft);
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
            HudTheme.Circle(centre, R * 2f, HudTheme.Track);

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
        /// The local player's own custody state, in the middle of the screen: handcuffs, PINNED with a round seconds badge (the colour runs from
        /// yellow to red as <paramref name="fresh"/> goes from 1 to 0), or BUSTED with the buy-back price; and a line of help under it.
        /// </summary>
        public static void CustodyMine(bool pinned, int secondsLeft, float fresh, string help, int buyBack = 0)
        {
            if (Event.current.type != EventType.Repaint) return;
            int id = pinned ? HudAnim.Id.Pinned : HudAnim.Id.Busted;
            float k = HudAnim.Pop(id, true);
            float s = S, w = Mathf.Min(Screen.width - 40f * s, 560f * s), h = 104f * s;
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.32f, w, h);
            var mat = HudAnim.Begin(k, r.center);
            Color body = pinned ? Color.Lerp(HudTheme.Danger, HudTheme.Quota, Mathf.Clamp01(fresh) * 0.9f) : HudTheme.Dim;
            HudTheme.Body(r, body, 24f, 4f, 5f, true, 2f);
            HudIcons.Cuffs(new Rect(r.x + 18f * s, r.y + (h - 68f * s) * 0.5f, 68f * s, 68f * s), HudTheme.Light(HudTheme.Dim, 0.5f));
            float tx = r.x + 96f * s, tw = r.width - 96f * s - (pinned ? 108f * s : 18f * s);
            HudTheme.Text(new Rect(tx, r.y + 6f * s, tw, h - 12f * s), pinned ? "PINNED" : "BUSTED", 48f, HudTheme.Bright, TextAnchor.MiddleLeft);
            if (pinned)
            {
                float punch = HudAnim.Bump(HudAnim.Id.Ring, secondsLeft);
                var bc = new Vector2(r.xMax - 58f * s, r.center.y);
                var m2 = HudAnim.Begin(punch, bc);
                HudIcons.Badge(new Rect(bc.x - 40f * s, bc.y - 40f * s, 80f * s, 80f * s), HudTheme.Card);
                HudTheme.Text(new Rect(bc.x - 40f * s, bc.y - 40f * s, 80f * s, 80f * s), intText.Get(secondsLeft), 46f, HudTheme.Bright, TextAnchor.MiddleCenter);
                HudAnim.End(m2);
            }
            else if (buyBack > 0)
            {
                var cr = new Rect(r.xMax - 114f * s, r.center.y - 15f * s, 98f * s, 30f * s);
                HudTheme.Pill(cr, HudTheme.Card, 2.5f, 2f);
                HudIcons.Coin(new Rect(cr.x + 5f * s, cr.y + 3f * s, 24f * s, 24f * s));
                HudTheme.Text(new Rect(cr.x + 32f * s, cr.y, cr.width - 36f * s, cr.height), HudTheme.Money(buyBack), 16f, HudTheme.Quota, TextAnchor.MiddleLeft);
            }
            if (!string.IsNullOrEmpty(help))
            {
                float hw = Mathf.Min(HudTheme.Width(help, 16f) + 40f * s, Screen.width - 40f * s), hh = 32f * s;
                var hr = new Rect((Screen.width - hw) * 0.5f, r.yMax + 12f * s, hw, hh);
                HudTheme.Pill(hr, HudTheme.Card, 2.5f, 2.5f, 1.5f);
                HudTheme.Text(hr, help, 16f, HudTheme.Bright, TextAnchor.MiddleCenter, false);
            }
            HudAnim.End(mat);
        }

        /// <summary>A tag over a teammate who is down: handcuffs and "PINNED 9" (red) or "BUSTED" (grey). <paramref name="centre"/> is in screen pixels.</summary>
        public static void CustodyTag(Vector2 centre, bool pinned, int secondsLeft)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, h = 28f * s;
            string text = pinned ? pinnedText.Get(secondsLeft) : "BUSTED";
            float w = HudTheme.Width(text, 14f) + h * 1.9f;
            var r = new Rect(centre.x - w * 0.5f, centre.y - h * 0.5f, w, h);
            float k = pinned ? HudAnim.Pulse(9f, 0.05f) : 1f;
            var mat = HudAnim.Begin(k, centre);
            HudTheme.Pill(r, pinned ? HudTheme.Danger : HudTheme.Dim, 2.5f, 2.5f, 1.4f);
            HudIcons.Cuffs(new Rect(r.x + 6f * s, r.y + 1f * s, h - 2f * s, h - 2f * s), HudTheme.Light(HudTheme.Dim, 0.6f));
            HudTheme.Text(new Rect(r.x + h * 1.15f, r.y, r.width - h * 1.25f, h), text, 14f, HudTheme.Bright, TextAnchor.MiddleLeft);
            HudAnim.End(mat);
        }

        // ====================================================================== the sniper rifle's scope

        /// <summary>
        /// Looking down the sniper rifle's scope (the owner, 2026-10-20 night: "a normal crosshair in the scope"): everything outside a round lens is dark, the lens has a thin rim and a plain fine cross with a small gap in the middle.
        /// <paramref name="share"/> 0..1 fades it in; <paramref name="onFoe"/> turns the dot in the middle red.
        /// </summary>
        public static void Scope(float share, bool onFoe)
        {
            if (Event.current.type != EventType.Repaint || share <= 0.002f) return;
            float s = S, cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            float lens = Screen.height * 0.47f;
            float outer = Mathf.Sqrt(cx * cx + cy * cy) + 8f;                                      // (a ring that reaches past the corners of the screen: all that is outside the lens)
            float a = Mathf.Clamp01(share);
            HudTheme.Ring(new Rect(cx - outer, cy - outer, outer * 2f, outer * 2f), new Color(0.015f, 0.015f, 0.02f, a), outer - lens, outer);
            HudTheme.Ring(new Rect(cx - lens, cy - lens, lens * 2f, lens * 2f), new Color(0.2f, 0.2f, 0.23f, a), Mathf.Max(3f, 5f * s), lens);
            var line = new Color(0.02f, 0.02f, 0.03f, 0.92f * a);
            float thin = Mathf.Max(2f, 2f * s), gap = 16f * s, reach = lens - Mathf.Max(3f, 5f * s);
            HudTheme.Rounded(new Rect(cx - reach, cy - thin * 0.5f, reach - gap, thin), line, 0f);          // left
            HudTheme.Rounded(new Rect(cx + gap, cy - thin * 0.5f, reach - gap, thin), line, 0f);           // right
            HudTheme.Rounded(new Rect(cx - thin * 0.5f, cy - reach, thin, reach - gap), line, 0f);          // up
            HudTheme.Rounded(new Rect(cx - thin * 0.5f, cy + gap, thin, reach - gap), line, 0f);           // down
            HudTheme.Circle(new Vector2(cx, cy), Mathf.Max(4f, 5f * s), onFoe ? new Color(1f, 0.25f, 0.2f, a) : line);
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
