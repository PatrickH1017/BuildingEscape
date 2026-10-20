# R0: the in-run inventory (Tab) in the toon look of the menus (MenuKit). Patches Backpack.cs and HudAnim.cs of the tree given by -Tree.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

# ---- HudAnim: one more pop id
Open-Src "$Tree\Assets\Scripts\Hud\HudAnim.cs"
if (-not $s.Contains('Inventory = 29')) {
  Rep 'Reload = 19, Met = 20, Checking = 21, Controls = 22, Reticle = 23, Ring = 24, BackupChip = 25, Sub = 26, Sub2 = 27, Esc = 28;' 'Reload = 19, Met = 20, Checking = 21, Controls = 22, Reticle = 23, Ring = 24, BackupChip = 25, Sub = 26, Sub2 = 27, Esc = 28, Inventory = 29, Computer = 30;'
  Save-Src
}

# ---- Backpack
Open-Src "$Tree\Assets\Scripts\Backpack.cs"
if ($s.Contains('InvRowHeight')) { "Backpack.cs is patched already"; return }

Rep @'
        void OnGUI()
        {
            if (!Viewed) return;
            var label = HudStyles.Label(18);
            float w = Weight;
            // the weight bar and the prompts only draw on Repaint; the inventory window (buttons) needs every event
            if (Event.current.type == EventType.Repaint) DrawHud(label, w);
            if (UiOpen) DrawInventory(label, w);
        }
'@ @'
        void OnGUI()
        {
            if (!Viewed) return;
            float w = Weight;
            // the weight bar and the prompts only draw on Repaint; the inventory window (buttons) needs every event
            if (Event.current.type == EventType.Repaint) DrawHud(w);
            if (UiOpen) DrawInventory(w);
        }
'@
Rep '        void DrawHud(GUIStyle label, float w)' '        void DrawHud(float w)'

$start = $s.IndexOf('        void DrawInventory(GUIStyle label, float w)')
if ($start -lt 0) { throw 'DrawInventory not found' }
$head = $s.Substring(0, $start)

$new = @'
        // ------------------------------------------------------------------ the inventory window (Tab), in the toon look of the menus (MenuKit)

        readonly MenuKit.Flow invFlow = new MenuKit.Flow();
        readonly MenuKit.Scroll invScroll = new MenuKit.Scroll();
        const float InvRowHeight = 62f;
        const int ActNone = 0, ActDrop = 1, ActEquip = 2, ActTake = 3;

        /// <summary>
        /// The window: the weight bar and what the loot in the pack is worth at the top, what is in the pack (Equip / Put away and Drop for each) and what lies within reach
        /// (Take, or "Too heavy") below, how to close it at the bottom. Every control is drawn on every event, in the same order, so the ids stay in step; what a click asks for is
        /// done after the window is drawn (the lists must not change while they are being walked).
        /// </summary>
        void DrawInventory(float w)
        {
            MenuKit.Begin();
            float s = HudTheme.S, pad = 24f * s, inset = 8f * s;
            float pw = Mathf.Min(840f * s, Screen.width - 40f * s), ph = Mathf.Min(790f * s, Screen.height - 64f * s);
            var panel = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f + 16f * s, pw, ph);

            MenuKit.Dim(0.5f);
            var mat = HudAnim.Begin(HudAnim.Pop(HudAnim.Id.Inventory, true), panel.center);
            MenuKit.Sheet(panel);
            MenuKit.Ribbon(panel, "BACKPACK", HudTheme.Info);

            // ---- the weight bar, and what the loot in the pack is worth
            float fill = Mathf.Clamp01(w / Mathf.Max(0.1f, capacity));
            Color wc = fill > 0.9f ? HudTheme.Danger : fill > 0.65f ? HudTheme.Warning : HudTheme.Good;
            float top = panel.y + 58f * s;
            if (Event.current.type == EventType.Repaint)
            {
                HudIcons.Pack(new Rect(panel.x + pad, top, 30f * s, 30f * s), HudTheme.Info);
                float bx = panel.x + pad + 40f * s, bw = pw * 0.34f;
                HudTheme.Bar(new Rect(bx, top + 7f * s, bw, 16f * s), fill, wc, 2.5f);
                HudTheme.Text(new Rect(bx + bw + 12f * s, top, 230f * s, 30f * s), WeightUnits.Num(w) + " / " + WeightUnits.Lbs(capacity), 18f, wc, TextAnchor.MiddleLeft, false);
                int loot = LootCount;
                if (loot > 0)
                {
                    string text = loot + " LOOT   " + HudTheme.Money(LootValue());
                    float cw = HudTheme.Width(text, 17f) + 36f * s;
                    HudTheme.Chip(new Rect(panel.xMax - pad - cw, top + 2f * s, cw, 28f * s), text, HudTheme.Quota, 17f);
                }
            }

            // ---- the lists
            float footerH = 44f * s, listTop = top + 42f * s, listBottom = panel.yMax - pad - footerH;
            var view = new Rect(panel.x + pad - inset, listTop, pw - pad * 2f + inset * 2f, Mathf.Max(10f, listBottom - listTop));
            float cwl = invScroll.Begin(view, inset);
            var f = invFlow.At(inset, invScroll.Top, cwl);
            Pickup picked = null;
            int action = ActNone;

            MenuKit.Header(f.Row(30f, 8f), "IN YOUR PACK", HudTheme.Info);
            bool any = false;
            for (int i = 0; i < items.Count; i++)
            {
                var p = items[i];
                if (!p) continue;
                any = true;
                int a = ItemRow(f, p, true, w);
                if (a != ActNone) { picked = p; action = a; }
            }
            if (!any) EmptyRow(f, "Nothing in your pack yet - aim at something and press E.");
            else if (MenuKit.Button(f.Row(40f, 10f), "DROP EVERYTHING", HudTheme.Danger, 19f)) { picked = null; action = -1; }

            f.Gap(6f);
            MenuKit.Header(f.Row(30f, 8f), "ON THE GROUND NEARBY", HudTheme.Cash);
            // pruned on the Layout event only, so the controls drawn for the later events of this frame are the same ones
            if (Event.current.type == EventType.Layout) nearby.RemoveAll(p => !p || p.IsHeld || !p.gameObject.activeInHierarchy);
            if (nearby.Count == 0) EmptyRow(f, "Nothing in reach.");
            for (int i = 0; i < nearby.Count; i++)
            {
                var p = nearby[i];
                if (!p) continue;
                int a = ItemRow(f, p, false, w);
                if (a != ActNone) { picked = p; action = a; }
            }
            invScroll.End(view, f.y);

            // ---- the foot: the tip, and the key that closes the window
            if (Event.current.type == EventType.Repaint)
            {
                float fy = panel.yMax - pad - footerH + 8f * s;
                HudTheme.Text(new Rect(panel.x + pad, fy, pw - pad * 2f - 160f * s, 28f * s),
                              "Tip: compare $ per lb - bring the best value for the space. The vendor pays 70% of value.", 15f, HudTheme.Muted, TextAnchor.MiddleLeft, false);
                float kw = Mathf.Max(28f * s, HudTheme.TextWidthPx("TAB", 15f * s) + 28f * s * 0.55f);      // (the cap's width: see HudTheme.KeyCap)
                float kx = panel.xMax - pad - kw - 70f * s;
                HudTheme.KeyCap(kx, fy, "TAB", 28f * s, 15f);
                HudTheme.Text(new Rect(kx + kw + 8f * s, fy, 70f * s, 28f * s), "CLOSE", 15f, HudTheme.Bright, TextAnchor.MiddleLeft, false);
            }
            HudAnim.End(mat);

            if (action == -1) DropAll();
            else if (action == ActDrop && picked) Drop(picked);
            else if (action == ActEquip && picked) SetEquipped(picked == Equipped ? null : picked);
            else if (action == ActTake && picked) { TryStore(picked); nearby.Remove(picked); }
        }

        /// <summary>A dim plate with a few words (an empty list).</summary>
        static void EmptyRow(MenuKit.Flow f, string text)
        {
            float s = HudTheme.S;
            var r = f.Row(40f, 8f);
            MenuKit.Plate(r, 0.1f);
            MenuKit.Label(new Rect(r.x + 16f * s, r.y, r.width - 32f * s, r.height), text, 16f, HudTheme.Dim);
        }

        /// <summary>
        /// One item of the window: a badge, its name, what it weighs and is worth, and the buttons. Returns what was clicked (<see cref="ActDrop"/>, <see cref="ActEquip"/> or
        /// <see cref="ActTake"/>), <see cref="ActNone"/> otherwise.
        /// </summary>
        int ItemRow(MenuKit.Flow f, Pickup p, bool inPack, float weightNow)
        {
            float s = HudTheme.S;
            var r = f.Row(InvRowHeight, 8f);
            MenuKit.Plate(r, 0.18f);
            int act = ActNone;
            bool gun = RangedWeapon.IsWeapon(p);

            // the buttons, from the right
            float bh = 40f * s, by = r.y + (r.height - bh) * 0.5f, gap = 8f * s, x = r.xMax - 10f * s;
            if (inPack)
            {
                var dropRect = new Rect(x - 96f * s, by, 96f * s, bh);
                x = dropRect.x - gap;
                if (MenuKit.Button(dropRect, "DROP", HudTheme.Warning, 19f)) act = ActDrop;
                if (gun)
                {
                    var equipRect = new Rect(x - 128f * s, by, 128f * s, bh);
                    x = equipRect.x - gap;
                    if (MenuKit.Button(equipRect, p == Equipped ? "PUT AWAY" : "EQUIP", HudTheme.Info, 19f)) act = ActEquip;
                }
            }
            else
            {
                bool fits = weightNow + p.weight <= capacity + 0.001f;
                var takeRect = new Rect(x - 132f * s, by, 132f * s, bh);
                x = takeRect.x - gap;
                if (MenuKit.Button(takeRect, fits ? "TAKE" : "TOO HEAVY", HudTheme.Good, fits ? 19f : 16f, fits)) act = ActTake;
            }

            if (Event.current.type != EventType.Repaint) return act;

            // the badge: a coin for something worth money, a bullet for a gun, a coloured disc with its first letter for the rest
            float bs = 42f * s;
            var badge = new Rect(r.x + 10f * s, r.y + (r.height - bs) * 0.5f, bs, bs);
            if (p.Value > 0) HudIcons.Coin(badge);
            else
            {
                Color c = gun ? HudTheme.Info : HudTheme.Dim;
                HudTheme.Circle(badge.center, bs, HudTheme.Ink);
                HudTheme.Circle(badge.center, bs - 6f * s, c);
                string label = p.Label;
                HudTheme.Text(badge, string.IsNullOrEmpty(label) ? "?" : label.Substring(0, 1).ToUpperInvariant(), 20f, HudTheme.Bright, TextAnchor.MiddleCenter, false);
            }

            // the name (with a chip when it is in hand) and the line under it
            float tx = badge.xMax + 14f * s, textW = Mathf.Max(60f * s, x - tx);
            bool inHand = inPack && p == Equipped;
            float chipW = inHand ? HudTheme.Width("IN HAND", 12f) + 24f * s : 0f;
            string name = MenuKit.Fit(p.Label, 21f * s, textW - chipW - (inHand ? 10f * s : 0f));
            HudTheme.Text(new Rect(tx, r.y + 7f * s, textW - chipW - 10f * s, 28f * s), name, 21f, HudTheme.Bright, TextAnchor.MiddleLeft, false);
            if (inHand) HudTheme.Chip(new Rect(tx + Mathf.Min(HudTheme.TextWidthPx(name, 21f * s), textW - chipW - 10f * s) + 10f * s, r.y + 10f * s, chipW, 22f * s), "IN HAND", HudTheme.Info, 12f);

            float dx = tx, dy = r.y + 34f * s;
            string weight = WeightUnits.Lbs(p.weight);
            HudTheme.Text(new Rect(dx, dy, 110f * s, 22f * s), weight, 15f, HudTheme.Muted, TextAnchor.MiddleLeft, false);
            dx += HudTheme.Width(weight, 15f) + 18f * s;
            if (p.TryGetComponent(out RangedWeapon rw))
            {
                int rounds = rw.Magazine.Rounds;
                string ammo = rounds + " / " + rw.Magazine.Capacity + " rounds";
                HudTheme.Text(new Rect(dx, dy, 160f * s, 22f * s), ammo, 15f, rounds <= 3 ? HudTheme.Warning : HudTheme.Bright, TextAnchor.MiddleLeft, false);
                dx += HudTheme.Width(ammo, 15f) + 18f * s;
            }
            if (p.Value > 0)
            {
                string money = HudTheme.Money(p.Value);
                HudTheme.Text(new Rect(dx, dy, 110f * s, 22f * s), money, 16f, HudTheme.Quota, TextAnchor.MiddleLeft, false);
                dx += HudTheme.Width(money, 16f) + 16f * s;
                string perLb = "$" + WeightUnits.PerLb(p.Value, p.weight).ToString("0") + " per lb";
                HudTheme.Text(new Rect(dx, dy, 140f * s, 22f * s), perLb, 15f, HudTheme.Dim, TextAnchor.MiddleLeft, false);
            }
            return act;
        }
    }
}
'@
$new = $new.Replace("`r`n", "`n").Replace("`n", $eol)
$script:s = $head + $new
Save-Src
