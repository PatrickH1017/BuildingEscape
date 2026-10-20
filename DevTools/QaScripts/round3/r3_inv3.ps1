# R0 (3rd step): the owner: "In the inventory UI, equipable items should be separate from loot" - the pack and the ground are each split into EQUIPMENT, LOOT (most valuable first) and OTHER.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
Open-Src "$Tree\Assets\Scripts\Backpack.cs"
if ($s.Contains('SplitItems')) { "already patched"; return }

Rep @'
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
'@ @'
            // what is in the pack: the things that can be held (weapons) apart from the loot, which is sorted with the most valuable first
            MenuKit.Header(f.Row(30f, 8f), "IN YOUR PACK", HudTheme.Info);
            SplitItems(items);
            if (invEquip.Count + invLoot.Count + invOther.Count == 0) EmptyRow(f, "Nothing in your pack yet - aim at something and press E.");
            else
            {
                ItemSection(f, "EQUIPMENT", HudTheme.Info, null, invEquip, true, w, ref picked, ref action);
                ItemSection(f, "LOOT", HudTheme.Quota, SumValue(invLoot), invLoot, true, w, ref picked, ref action);
                ItemSection(f, "OTHER", HudTheme.Dim, null, invOther, true, w, ref picked, ref action);
                if (MenuKit.Button(f.Row(40f, 10f), "DROP EVERYTHING", HudTheme.Danger, 19f)) { picked = null; action = -1; }
            }

            f.Gap(6f);
            MenuKit.Header(f.Row(30f, 8f), "ON THE GROUND NEARBY", HudTheme.Cash);
            // pruned on the Layout event only, so the controls drawn for the later events of this frame are the same ones
            if (Event.current.type == EventType.Layout) nearby.RemoveAll(p => !p || p.IsHeld || !p.gameObject.activeInHierarchy);
            SplitItems(nearby);
            if (invEquip.Count + invLoot.Count + invOther.Count == 0) EmptyRow(f, "Nothing in reach.");
            else
            {
                ItemSection(f, "EQUIPMENT", HudTheme.Info, null, invEquip, false, w, ref picked, ref action);
                ItemSection(f, "LOOT", HudTheme.Quota, SumValue(invLoot), invLoot, false, w, ref picked, ref action);
                ItemSection(f, "OTHER", HudTheme.Dim, null, invOther, false, w, ref picked, ref action);
            }
            invScroll.End(view, f.y);
'@

Rep @'
        /// <summary>A dim plate with a few words (an empty list).</summary>
'@ @'
        // the three kinds of thing a list is split into (reused: the window is drawn on every event)
        readonly List<Pickup> invEquip = new List<Pickup>(), invLoot = new List<Pickup>(), invOther = new List<Pickup>();
        static readonly System.Comparison<Pickup> ByValueDescending = (a, b) => b.Value.CompareTo(a.Value);

        /// <summary>Sorts a list of items into <see cref="invEquip"/> (what can be held: weapons), <see cref="invLoot"/> (worth money, the most valuable first) and <see cref="invOther"/>.</summary>
        void SplitItems(List<Pickup> from)
        {
            invEquip.Clear(); invLoot.Clear(); invOther.Clear();
            for (int i = 0; i < from.Count; i++)
            {
                var p = from[i];
                if (!p) continue;
                if (RangedWeapon.IsWeapon(p)) invEquip.Add(p);
                else if (p.Value > 0) invLoot.Add(p);
                else invOther.Add(p);
            }
            invLoot.Sort(ByValueDescending);
        }

        static string SumValue(List<Pickup> list)
        {
            int sum = 0;
            for (int i = 0; i < list.Count; i++) if (list[i]) sum += list[i].Value;
            return sum > 0 ? HudTheme.Money(sum) : null;
        }

        /// <summary>A small title (a coloured dot, the words, and what the lot is worth) and the rows under it; nothing at all for an empty list.</summary>
        void ItemSection(MenuKit.Flow f, string title, Color dot, string right, List<Pickup> list, bool inPack, float weightNow, ref Pickup picked, ref int action)
        {
            if (list.Count == 0) return;
            float s = HudTheme.S;
            var r = f.Row(24f, 4f);
            if (Event.current.type == EventType.Repaint)
            {
                HudTheme.Circle(new Vector2(r.x + 12f * s, r.center.y), 14f * s, HudTheme.Ink);
                HudTheme.Circle(new Vector2(r.x + 12f * s, r.center.y), 9f * s, dot);
                HudTheme.Text(new Rect(r.x + 30f * s, r.y, 200f * s, r.height), title, 15f, HudTheme.Muted, TextAnchor.MiddleLeft, false);
                if (!string.IsNullOrEmpty(right)) HudTheme.Text(new Rect(r.xMax - 240f * s, r.y, 232f * s, r.height), right, 15f, HudTheme.Quota, TextAnchor.MiddleRight, false);
            }
            for (int i = 0; i < list.Count; i++)
            {
                int a = ItemRow(f, list[i], inPack, weightNow);
                if (a != ActNone) { picked = list[i]; action = a; }
            }
        }

        /// <summary>A dim plate with a few words (an empty list).</summary>
'@
Save-Src
