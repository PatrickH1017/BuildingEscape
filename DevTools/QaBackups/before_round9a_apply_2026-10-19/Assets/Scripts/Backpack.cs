using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The player's backpack. Point the crosshair at an item (within reach) and press E to pick it up if it fits in the weight limit,
    /// Tab opens the inventory where you can drop things or take nearby items back.
    /// Stored items are switched off and ride along with you, also in the elevator.
    /// Deposit loot with F in the elevator (banks it) or at the basement vendor (sells it).
    /// </summary>
    public class Backpack : MonoBehaviour
    {
        public float capacity = 20f;
        public KeyCode pickupKey = KeyCode.E;
        public KeyCode inventoryKey = KeyCode.Tab;
        public float reach = 2.2f;

        public static bool UiOpen { get; private set; }
        public static void CloseUi() { UiOpen = false; }

        readonly List<Pickup> items = new();
        readonly List<Pickup> nearby = new();
        Transform holder;
        Pickup target;
        string notice; float noticeUntil;
        Vector2 scroll;
        CameraFollow rig;
        float nextNearby, nextRigLookup;
        WobblyPlayer wobbly;

        public IReadOnlyList<Pickup> Items => items;
        /// <summary>Is the crosshair on an item within reach that E would pick up right now?</summary>
        public bool HasPickupTarget => target != null;
        /// <summary>The weapon (a melee weapon or the pew) shown in the right hand (must be an item in the pack).</summary>
        public Pickup Equipped { get; private set; }
        public float Weight { get { float w = 0; foreach (var p in items) if (p) w += p.weight; return w; } }

        /// <summary>Is this the pack the camera follows (the one whose screens and keys this machine shows)? True while there is no camera.</summary>
        public bool Viewed
        {
            get
            {
                // looked up again (at most twice a second) when the camera rig is missing or was replaced
                if (!rig && Time.unscaledTime >= nextRigLookup)
                {
                    rig = FindFirstObjectByType<CameraFollow>();
                    nextRigLookup = Time.unscaledTime + 0.5f;
                }
                return !rig || !rig.target || rig.target == transform || rig.target.IsChildOf(transform);
            }
        }

        void Awake()
        {
            holder = new GameObject("BackpackItems").transform;
            holder.SetParent(transform, false);
            TryGetComponent(out wobbly);
        }

        void OnDisable() { if (UiOpen && Viewed) UiOpen = false; }

        // ------------------------------------------------------------------ logic

        public bool Contains(Pickup p) => items.Contains(p);

        /// <summary>Host: an item in the pack is used up (a key in a lock). The caller destroys the object.</summary>
        internal void RemoveUsed(Pickup p)
        {
            if (!p || !items.Remove(p)) return;
            if (Equipped == p) Equipped = null;
        }
        public List<Pickup> Snapshot() => new List<Pickup>(items);

        /// <summary>Where a dropped item lands (in front of the player).</summary>
        public Vector3 DropPosition() => transform.position + transform.forward * 1.3f + Vector3.up * 0.5f
                                         + Random.insideUnitSphere * 0.15f;

        /// <summary>Put it in the pack. In a networked run the host decides and everybody applies the result.</summary>
        public bool TryStore(Pickup p)
        {
            if (!p) return false;
            if (Net.NetRole.Synced) { Net.RunSync.Instance.RequestStore(p); return true; }
            if (p.TryGetComponent(out AmmoClip ammo)) return TakeClip(p, ammo);
            if (!CanFit(p, out string why)) { Say(why); return false; }
            ApplyStored(p);
            return true;
        }

        /// <summary>What a player is told who goes for a spare clip without a pistol to put it in, and who has taken one (the owner writes the words: these are placeholders).</summary>
        public const string NoPewForClip = "You have no pistol for this clip";
        public static string ClipTaken(int rounds) => "+" + rounds + " rounds for your pew";

        /// <summary>Offline: a spare clip (<see cref="AmmoClip"/>) is taken - its rounds go into the pew in this pack and the clip is used up; without a pew it stays where it lies. A networked run does the same through the host (RunSync.HostStore).</summary>
        bool TakeClip(Pickup p, AmmoClip clip)
        {
            if (!Vendor.GiveAmmo(this, clip.rounds)) { Say(NoPewForClip); return false; }
            int rounds = clip.rounds;
            Destroy(p.gameObject);
            if (Viewed) Say(ClipTaken(rounds));
            return true;
        }

        /// <summary>Weight check; <paramref name="why"/> is the message when it does not fit.</summary>
        public bool CanFit(Pickup p, out string why)
        {
            why = null;
            if (Weight + p.weight <= capacity + 0.001f) return true;
            why = $"Too heavy for your pack ({WeightUnits.Lbs(p.weight)}, {WeightUnits.Lbs(Mathf.Max(0, capacity - Weight))} free)" +
                  (p.Loot && p.Loot.lifters > 1 ? " - carry it with friends (Q)" : "");
            return false;
        }

        /// <summary>The item goes into the pack (no checks: the caller or the host already did them).</summary>
        internal void ApplyStored(Pickup p)
        {
            if (!p || items.Contains(p)) return;
            items.Add(p);
            if (!Equipped && RangedWeapon.IsWeapon(p)) Equipped = p;
            p.transform.SetParent(holder, true);
            if (p.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            p.gameObject.SetActive(false);
            if (Viewed) Say($"Picked up {p.Label} ({WeightUnits.Lbs(p.weight)})");
        }

        public void Drop(Pickup p)
        {
            if (!p) return;
            if (Net.NetRole.Synced) { Net.RunSync.Instance.RequestDrop(p); return; }
            ApplyDropped(p, DropPosition());
        }

        internal void ApplyDropped(Pickup p, Vector3 pos)
        {
            if (!p || !items.Remove(p)) return;
            if (Equipped == p) Equipped = null;
            var gen = FindFirstObjectByType<FloorGenerator>();
            var floor = gen ? gen.Current : null;
            p.transform.SetParent(floor, true);
            p.transform.position = pos;
            p.gameObject.SetActive(true);
            if (p.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }

        public void DropAll()
        {
            if (Net.NetRole.Synced) { Net.RunSync.Instance.RequestDropAll(); return; }
            foreach (var p in new List<Pickup>(items)) ApplyDropped(p, DropPosition());
        }

        /// <summary>Equip a weapon from the pack, or null to put it away.</summary>
        public void SetEquipped(Pickup p)
        {
            if (Net.NetRole.Synced) { Net.RunSync.Instance.RequestEquip(p); return; }
            Equipped = p;
        }

        internal void ApplyEquipped(Pickup p) { Equipped = p; }

        /// <summary>A message from the host about something this player tried (shown on the pack you are viewing).</summary>
        public static void NoticeToLocal(string text)
        {
            foreach (var b in FindObjectsByType<Backpack>(FindObjectsSortMode.None))
                if (b.Viewed) { b.Say(text); return; }
        }
        public void CleanUp() { items.RemoveAll(p => !p); if (!Equipped) Equipped = null; }

        public int LootCount { get { CleanUp(); int n = 0; foreach (var p in items) if (p.Loot && !p.Loot.Broken) n++; return n; } }

        public int LootValue(float rate = 1f)
        {
            CleanUp(); int v = 0;
            foreach (var p in items) if (p.Loot && !p.Loot.Broken) v += Mathf.RoundToInt(p.Loot.CurrentValue * rate);
            return v;
        }

        /// <summary>Removes every piece of loot from the pack and returns it (the caller banks / sells and destroys it).</summary>
        public List<HeavyLoot> TakeLoot()
        {
            CleanUp();
            var res = new List<HeavyLoot>();
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var l = items[i].Loot;
                if (!l) continue;
                items.RemoveAt(i);
                if (!l.Broken) res.Add(l);
            }
            return res;
        }

        void Say(string s) { notice = s; noticeUntil = Time.time + 2.5f; }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!Viewed) return;
            if (wobbly && wobbly.InCustody) { UiOpen = false; target = null; return; } // pinned or busted: hands are full of officer
            CleanUp();
            bool talk = Vendor.ShopOpen || ElevatorPanel.Open || OutpostUi.Open;
            if (Input.GetKeyDown(inventoryKey) && !Vendor.ShopOpen && !ElevatorPanel.Open && !OutpostUi.Open) { UiOpen = !UiOpen; scroll = Vector2.zero; }

            // E works what the crosshair is on: an item here, a door in DoorManager, the dealer in Vendor
            var aimed = (UiOpen || talk) ? AimTarget.Nothing : Crosshair.Aim;
            target = aimed.Kind == AimKind.Pickup ? aimed.Pickup : null;
            if (!UiOpen && !talk && Input.GetKeyDown(pickupKey))
            {
                if (target) TryStore(target);
                else if (aimed.Kind == AimKind.None)
                    Say(aimed.TooFar ? "Too far away - get closer" : "Nothing there - aim the crosshair at an item");
            }

            if (UiOpen && Time.time >= nextNearby)
            {
                nextNearby = Time.time + 0.2f;
                Pickup.ScanNear(transform, reach + 0.8f, nearby);
            }
        }

        // ------------------------------------------------------------------ ui

        void OnGUI()
        {
            if (!Viewed) return;
            if (!UiOpen) invCardH = 0f;
            float w = Weight;
            // the weight bar and the prompts only draw on Repaint; the inventory window (buttons) needs every event
            if (Event.current.type == EventType.Repaint) DrawHud(w);
            if (UiOpen) DrawInventory(w);
        }

        /// <summary>The pack card (bottom left: the weight, the loot in the pack, what is in hand) and the pick-up prompt.</summary>
        void DrawHud(float w)
        {
            HudWidgets.PackCard(new HudWidgets.PackModel
            {
                weight = w, capacity = capacity, lootCount = LootCount, lootValue = LootValue(), inHand = Equipped ? Equipped.Label : null,
            });
            if (UiOpen) return;

            string prompt = null;
            bool warn = false;
            if (target)
            {
                bool fits = w + target.weight <= capacity + 0.001f;
                prompt = fits
                    ? $"[E] Pick up {target.Label}  ({WeightUnits.Lbs(target.weight)}" + (target.Value > 0 ? $", ${target.Value}" : "") + ")"
                    : $"{target.Label} ({WeightUnits.Lbs(target.weight)}) is too heavy for your pack - [Q] carry it";
                warn = !fits;
            }
            else if (Time.time < noticeUntil) prompt = notice;
            float y = Screen.height * 0.62f;
            if (prompt != null) HudWidgets.Prompt(prompt, y, warn);
            if (target && Time.time < noticeUntil) HudWidgets.Prompt(notice, y + HudTheme.Px(HudWidgets.PromptStep), false, 17f, 1);
        }

        // ------------------------------------------------------------------ the inventory window (Tab), in the toon look of the menus (MenuKit)

        readonly MenuKit.Flow invFlow = new MenuKit.Flow();
        readonly MenuKit.Scroll invScroll = new MenuKit.Scroll();
        float invCardH;
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
            float pw = Mathf.Min(840f * s, Screen.width - 40f * s);
            float py = 126f * s;                                           // (the card stays under the banner at the top of the screen)
            float maxH = Mathf.Max(260f * s, Screen.height - py - 24f * s);
            float want = Mathf.Clamp(172f * s + invScroll.ContentHeight + 10f * s, 300f * s, Mathf.Min(maxH, 790f * s));    // (as tall as the lists were on the last frame)
            if (invCardH <= 0f) invCardH = want;
            else if (Event.current.type == EventType.Repaint) invCardH = Mathf.Lerp(invCardH, want, 1f - Mathf.Exp(-14f * Mathf.Min(0.1f, Time.unscaledDeltaTime)));
            float ph = Mathf.Min(invCardH, maxH);
            var panel = new Rect((Screen.width - pw) * 0.5f, Mathf.Max(py, (Screen.height - ph) * 0.5f + 16f * s), pw, ph);

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
                string ammo = rounds + " / " + rw.Magazine.Capacity + " rounds" + (rw.Magazine.InfiniteReserve ? "" : "  +" + rw.Magazine.Reserve + " spare");
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