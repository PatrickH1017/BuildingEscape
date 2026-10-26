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

        // ---- the weapon bar (the owner, 2026-10-19 night: "a tool bar (hot bar) of 3 items so players can click 1, 2, 3 to switch between weapons"; 2026-10-20 night: six shortcuts, 1 to 6): weapons of the pack come onto it in the order they came into
        // the pack, each into the first empty slot; since 2026-10-22 the player arranges it by dragging in the Tab window (the owner: "drag equipment such as weapons into different shortcut menus from their backpack or from a different shortcut ...
        // if they drag to another item it should swap spots with the item"). The layout is WeaponBarRules'; what is kept here is one key per slot (the weapon's instance id) and, rebuilt from it, where each slot's weapon is in the pack's list.
        readonly int[] barKeys = WeaponBarRules.NewLayout();
        readonly List<int> weaponKeys = new List<int>();
        readonly int[] barSlots = new int[WeaponBarRules.Slots];
        readonly string[] barNames = new string[WeaponBarRules.Slots];

        void RefreshBar()
        {
            weaponKeys.Clear();
            for (int i = 0; i < items.Count; i++) if (items[i] && RangedWeapon.IsWeapon(items[i])) weaponKeys.Add(items[i].GetInstanceID());
            WeaponBarRules.Reconcile(barKeys, weaponKeys);
            for (int s = 0; s < barSlots.Length; s++) barSlots[s] = -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (!items[i]) continue;
                int slot = WeaponBarRules.SlotOf(barKeys, items[i].GetInstanceID());
                if (slot >= 0) barSlots[slot] = i;
            }
        }

        /// <summary>The bar slot (0 to 5) a weapon of the pack is on, or -1 when it is not on the bar (or is no weapon of this pack).</summary>
        public int BarSlotOf(Pickup p)
        {
            if (!p || !items.Contains(p)) return -1;
            RefreshBar();
            return WeaponBarRules.SlotOf(barKeys, p.GetInstanceID());
        }

        /// <summary>A weapon of the pack is dropped on a slot of the bar: it takes the slot, and the weapon that stood there takes its old place (its old slot, or off the bar). False when nothing changed.</summary>
        public bool MoveToBarSlot(Pickup p, int slot)
        {
            if (!p || !RangedWeapon.IsWeapon(p) || !items.Contains(p)) return false;
            RefreshBar();
            return WeaponBarRules.MoveToSlot(barKeys, p.GetInstanceID(), slot);
        }

        /// <summary>A weapon of the pack is dropped on another: they swap spots on the bar (one off the bar: the other takes its slot). False when nothing changed.</summary>
        public bool SwapBarSpots(Pickup a, Pickup b)
        {
            if (!a || !b || !RangedWeapon.IsWeapon(a) || !RangedWeapon.IsWeapon(b) || !items.Contains(a) || !items.Contains(b)) return false;
            RefreshBar();
            return WeaponBarRules.SwapWith(barKeys, a.GetInstanceID(), b.GetInstanceID());
        }

        /// <summary>The weapon of a bar slot (0 to 5), or null for an empty slot.</summary>
        public Pickup BarWeapon(int slot)
        {
            RefreshBar();
            return slot >= 0 && slot < barSlots.Length && barSlots[slot] >= 0 && barSlots[slot] < items.Count ? items[barSlots[slot]] : null;
        }

        /// <summary>Takes the weapon of a bar slot in hand (the keys 1 to 6); false for an empty slot, which changes nothing.</summary>
        public bool SelectSlot(int slot)
        {
            var p = BarWeapon(slot);
            if (!p) return false;
            if (p != Equipped) SetEquipped(p);
            return true;
        }
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

        void OnDisable() { CancelDrag(); if (UiOpen && Viewed) UiOpen = false; }

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
        /// <summary>The same for a clip of any gun: what a player is told who goes for a clip of a gun he does not carry, one who carries the most clips already, and who has taken one (placeholders: the owner writes the words).</summary>
        public static string NoGunForClip(RangedWeapon.Kind kind) => kind == RangedWeapon.Kind.Pew ? NoPewForClip : "You have no " + RangedWeapon.Specs[(int)kind].name + " for this clip";
        /// <summary>What a player is told who carries the most clips of a gun already: "three" for most guns, "twelve" for the pistol (the owner writes the words: this keeps the old sentence with the right number).</summary>
        public static string TooManyClipsFor(GunMagazine m) => "You can't carry more than " + ClipWord(m.Capacity > 0 ? m.MaxRounds / m.Capacity : 3) + " clips";
        static string ClipWord(int n) => n >= 1 && n <= 12 ? new[] { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve" }[n - 1] : n.ToString();
        public static string ClipTaken(int rounds, RangedWeapon.Kind kind) => kind == RangedWeapon.Kind.Pew ? ClipTaken(rounds) : "+" + rounds + " rounds for your " + RangedWeapon.Specs[(int)kind].name;

        /// <summary>Offline: a spare clip (<see cref="AmmoClip"/>) is taken - its rounds go into the pew in this pack and the clip is used up; without a pew it stays where it lies. A networked run does the same through the host (RunSync.HostStore).</summary>
        bool TakeClip(Pickup p, AmmoClip clip)
        {
            var gun = Vendor.GunOf(this, clip.kind);
            if (gun == null) { Say(NoGunForClip(clip.kind)); return false; }
            if (gun.Magazine.Room < clip.rounds) { Say(TooManyClipsFor(gun.Magazine)); return false; }               // (three clips at most: the clip stays where it lies)
            Vendor.GiveAmmo(this, clip.rounds, clip.kind);
            int rounds = clip.rounds;
            Destroy(p.gameObject);
            if (Viewed) Say(ClipTaken(rounds, clip.kind));
            return true;
        }

        /// <summary>Weight check; <paramref name="why"/> is the message when it does not fit.</summary>
        public bool CanFit(Pickup p, out string why)
        {
            why = null;
            if (p.TryGetComponent(out ArmourItem _) && PlayerArmour.Wears(this)) { why = ArmourRules.AlreadyWearing; return false; }        // (one piece of armour at a time)
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
            foreach (var p in new List<Pickup>(items)) if (p && p.Loot) ApplyDropped(p, DropPosition());          // (loot only: weapons, armour and the other equipment stay)
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
            // a drag whose mouse-up never reached the window (the mouse let go outside the game view) is given up two frames after the button is up (not sooner: this frame's mouse-up event is still to come)
            if (dragLive || dragItem != null)
            {
                if (Input.GetMouseButton(0)) dragUpFrames = 0;
                else if (++dragUpFrames >= 2) CancelDrag();
            }
            if (!Viewed) return;
            if (wobbly && wobbly.InCustody) { UiOpen = false; target = null; return; } // pinned or busted: hands are full of officer
            CleanUp();
            bool talk = Vendor.ShopOpen || ElevatorPanel.Open || OutpostUi.Open;
            if (Input.GetKeyDown(inventoryKey) && !Vendor.ShopOpen && !ElevatorPanel.Open && !OutpostUi.Open) { UiOpen = !UiOpen; scroll = Vector2.zero; }
            if (!UiOpen && !talk && !EscapeMenu.Open && !TestMode.PanelOpen)
                for (int d = 1; d <= WeaponBarRules.Slots; d++)
                    if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d)) SelectSlot(WeaponBarRules.SlotOfKey(d));

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
            dragId = GUIUtility.GetControlID(FocusType.Passive);                    // (the first control of this window on every event, so its id is the same every time)
            if (!UiOpen) { invCardH = 0f; CancelDrag(); }
            var ev = Event.current;
            mouse = ev.mousePosition;                                               // (screen pixels: read here, outside the window's scrolling area and its pop-up animation)
            if (dragLive && (!dragItem || !items.Contains(dragItem))) CancelDrag(); // (the weapon was dropped or taken while it was dragged)
            if (dragItem && !dragLive && ev.type == EventType.MouseDrag && (mouse - dragDown).sqrMagnitude > DragStartPx * DragStartPx)
            {
                dragLive = true;                                                    // (the mouse has moved: it is a drag, not a click; no button of the window answers the mouse until it is let go)
                GUIUtility.hotControl = dragId;
            }
            float w = Weight;
            // the weight bar and the prompts only draw on Repaint; the inventory window (buttons) needs every event
            if (ev.type == EventType.Repaint) DrawHud(w);
            if (UiOpen)
            {
                DrawInventory(w);                                                   // (a weapon row the mouse comes up over is what the dragged weapon is dropped on: dropOnItem)
                DrawBarOnTop();                                                     // (the slot the mouse comes up over: dropOnSlot)
                FinishDrag();
            }
        }

        /// <summary>The weapon bar and the pick-up prompt (the pack card that used to be at the bottom left is gone: the Tab window shows the weight).</summary>
        void DrawHud(float w)
        {
            // (no pack card at the bottom left any more - the owner, 2026-10-20 night: "remove the backpack from the bottom left corner": the weight and how much more fits are in the Tab window)
            RefreshBar();
            int inHand = FillBarNames();
            if (!UiOpen && !(SafePanel.Instance && SafePanel.Instance.Showing))           // (the safe's keypad is tall and its bottom lines lay under the bar: no bar while it is up; with the Tab window open the bar is drawn over its dimmed screen: DrawBarOnTop)
                HudWidgets.WeaponBar(new HudWidgets.WeaponBarModel { names = barNames, equipped = inHand });
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
            RefreshBar();                                                  // (the slots the weapons are on: for the order of the list, the "SLOT" chips and what a drop does)
            float s = HudTheme.S, pad = 24f * s, inset = 8f * s;
            float pw = Mathf.Min(840f * s, Screen.width - 40f * s);
            float py = 126f * s;                                           // (the card stays under the banner at the top of the screen)
            float maxH = Mathf.Max(260f * s, Screen.height - py - 150f * s);        // (the weapon bar under the window is free to drop a weapon on: the card ends above it)
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
                float bx = panel.x + pad + 40f * s, bw = pw * 0.30f;
                HudTheme.Bar(new Rect(bx, top + 7f * s, bw, 16f * s), fill, wc, 2.5f);
                string weightText = WeightUnits.Num(w) + " / " + WeightUnits.Lbs(capacity);
                HudTheme.Text(new Rect(bx + bw + 12f * s, top, 230f * s, 30f * s), weightText, 18f, wc, TextAnchor.MiddleLeft, false);
                int loot = LootCount;
                float limit = panel.xMax - pad;                                   // (how far right the "left to carry" chip may go)
                if (loot > 0)
                {
                    string text = loot + " LOOT   " + HudTheme.Money(LootValue());
                    float cw = HudTheme.Width(text, 17f) + 36f * s;
                    HudTheme.Chip(new Rect(panel.xMax - pad - cw, top + 2f * s, cw, 28f * s), text, HudTheme.Quota, 17f);
                    limit = panel.xMax - pad - cw - 8f * s;
                }
                // how much more fits (the owner, 2026-10-20 night: with the pack card gone, "you have to click tab to see how much more you can carry")
                string leftText = WeightUnits.Lbs(Mathf.Max(0f, capacity - w)) + " left to carry";
                float lw = HudTheme.Width(leftText, 14f) + 28f * s, lx = bx + bw + 12f * s + HudTheme.Width(weightText, 18f) + 14f * s;
                if (lx + lw <= limit) HudTheme.Chip(new Rect(lx, top + 2f * s, lw, 28f * s), leftText, capacity - w < 2f ? HudTheme.Danger : HudTheme.Cash, 14f);
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
            SortEquipByBar(invEquip);
            if (invEquip.Count + invLoot.Count + invOther.Count == 0) EmptyRow(f, "Nothing in your pack yet - aim at something and press E.");
            else
            {
                ItemSection(f, "EQUIPMENT", HudTheme.Info, SlotHint, invEquip, true, w, ref picked, ref action, HudTheme.Dim);
                ItemSection(f, "LOOT", HudTheme.Quota, SumValue(invLoot), invLoot, true, w, ref picked, ref action);
                ItemSection(f, "OTHER", HudTheme.Dim, null, invOther, true, w, ref picked, ref action);
                if (MenuKit.Button(f.Row(40f, 10f), "DROP ALL LOOT", HudTheme.Danger, 19f)) { picked = null; action = -1; }
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

        // ---- dragging a weapon (the owner, 2026-10-22: "give players the ability to drag equipment such as weapons into different shortcut menus from their backpack or from a different shortcut. Meaning I want them to be able to
        // rearrange the equipment by dragging the item. if they drag to another item it should swap spots with the item"). With the Tab window open, a weapon is picked up by pressing the mouse on its row in the pack list or on its slot of the
        // bar and moving the mouse; it is dropped by letting go over a slot of the bar (it takes the slot, what was there goes to the weapon's old place) or over another weapon of the list (they swap spots). Anywhere else it goes nowhere.
        // The layout is this player's own and is not sent to the others (the weapon in hand is, as ever).
        const float DragStartPx = 6f;
        public const string SlotHint = "DRAG TO A SLOT TO ARRANGE";
        Pickup dragItem;                    // the weapon the mouse went down on (a click if the mouse never moves)
        bool dragLive;                      // the mouse moved: it is being dragged
        Vector2 dragDown, mouse;            // where the mouse went down, and where it is (screen pixels)
        Pickup dropOnItem;                  // the weapon row the mouse came up over, found while the window is drawn (the mouse-up event only)
        int dropOnSlot = -1;                // the bar slot the mouse came up over (the mouse-up event only)
        int dragId, dragUpFrames;

        /// <summary>Is a weapon being dragged right now (for tests)?</summary>
        public bool Dragging => dragLive && dragItem;

        /// <summary>The names on the bar's slots, and which slot is in hand (-1 for none).</summary>
        int FillBarNames()
        {
            int inHand = -1;
            for (int i = 0; i < barSlots.Length; i++)
            {
                var slotItem = barSlots[i] >= 0 && barSlots[i] < items.Count ? items[barSlots[i]] : null;
                barNames[i] = slotItem ? slotItem.Label : null;
                if (slotItem && slotItem == Equipped) inHand = i;
            }
            return inHand;
        }

        /// <summary>The weapons of the list: the ones on the bar first in the order of the slots, then the others in the order they came into the pack (so a swap on the bar is a swap in the list).</summary>
        void SortEquipByBar(List<Pickup> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var p = list[i];
                int key = EquipOrder(p), j = i - 1;
                while (j >= 0 && EquipOrder(list[j]) > key) { list[j + 1] = list[j]; j--; }
                list[j + 1] = p;
            }
        }

        int EquipOrder(Pickup p)
        {
            int slot = WeaponBarRules.SlotOf(barKeys, p.GetInstanceID());
            return slot >= 0 ? slot : 100 + items.IndexOf(p);
        }

        /// <summary>The row of a weapon in the pack list: the mouse going down on it (not on one of its buttons, which take the event first) picks the weapon up; the mouse coming up over it while another weapon is dragged drops that one here.</summary>
        void GrabAndDrop(Pickup p, Rect r)
        {
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 0 && dragItem == null && GUIUtility.hotControl == 0 && MenuKit.Over(r)) { dragItem = p; dragLive = false; dragDown = mouse; dragUpFrames = 0; }
            else if (ev.type == EventType.MouseUp && ev.button == 0 && dragLive && p != dragItem && MenuKit.Over(r)) dropOnItem = p;
        }

        /// <summary>
        /// The weapon bar while the window is open: drawn over the dimmed screen (the window leaves the room under it), the slots lit while a weapon is dragged over them. A weapon of a slot is picked up from here too; the slot the mouse comes up over is
        /// where a dragged weapon is dropped (<see cref="dropOnSlot"/>).
        /// </summary>
        void DrawBarOnTop()
        {
            if (SafePanel.Instance && SafePanel.Instance.Showing) return;
            RefreshBar();
            int inHand = FillBarNames();
            var ev = Event.current;
            int hover = -1;
            for (int i = 0; i < WeaponBarRules.Slots; i++) if (HudWidgets.WeaponBarSlot(i).Contains(mouse)) { hover = i; break; }
            if (ev.type == EventType.MouseDown && ev.button == 0 && dragItem == null && GUIUtility.hotControl == 0 && hover >= 0 && barSlots[hover] >= 0 && barSlots[hover] < items.Count)
            {
                dragItem = items[barSlots[hover]]; dragLive = false; dragDown = mouse; dragUpFrames = 0;
                ev.Use();                                                  // (nothing else answers this press)
            }
            if (ev.type == EventType.MouseUp && ev.button == 0 && dragLive) dropOnSlot = hover;
            int pickedFrom = dragLive && dragItem ? WeaponBarRules.SlotOf(barKeys, dragItem.GetInstanceID()) : -1;
            if (ev.type == EventType.Repaint) HudWidgets.WeaponBar(new HudWidgets.WeaponBarModel { names = barNames, equipped = inHand, dragging = dragLive, hover = dragLive ? hover : -1, source = pickedFrom });
        }

        /// <summary>The end of every event of the window: the mouse let go drops the weapon (or not), and a weapon in the air is drawn at the mouse.</summary>
        void FinishDrag()
        {
            var ev = Event.current;
            if (dragItem == null) return;
            if (ev.type == EventType.MouseUp && ev.button == 0)
            {
                if (dragLive)
                {
                    if (dropOnSlot >= 0) MoveToBarSlot(dragItem, dropOnSlot);
                    else if (dropOnItem && dropOnItem != dragItem) SwapBarSpots(dragItem, dropOnItem);
                    ev.Use();
                }
                CancelDrag();
            }
            else if (dragLive && ev.type == EventType.Repaint) DrawDragGhost();
        }

        /// <summary>The weapon in the air: a see-through plate with its name, at the mouse.</summary>
        void DrawDragGhost()
        {
            float s = HudTheme.S;
            string label = dragItem ? dragItem.Label : "";
            float w = Mathf.Max(130f * s, HudTheme.Width(label, 17f) + 40f * s), h = 40f * s;
            var r = new Rect(Mathf.Clamp(mouse.x - w * 0.5f, 4f, Mathf.Max(4f, Screen.width - w - 4f)), Mathf.Clamp(mouse.y - h * 0.5f, 4f, Mathf.Max(4f, Screen.height - h - 4f)), w, h);
            HudTheme.Glass(r, 0.55f, 1f, null, HudTheme.Info);
            HudTheme.Text(r, MenuKit.Fit(label, 17f * s, w - 24f * s), 17f, HudTheme.Bright, TextAnchor.MiddleCenter);
        }

        /// <summary>Lets go of a weapon (dropped or not) and gives the mouse back to the window's buttons.</summary>
        void CancelDrag()
        {
            if (dragId != 0 && GUIUtility.hotControl == dragId) GUIUtility.hotControl = 0;
            dragItem = null; dragLive = false; dropOnItem = null; dropOnSlot = -1; dragUpFrames = 0;
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
        void ItemSection(MenuKit.Flow f, string title, Color dot, string right, List<Pickup> list, bool inPack, float weightNow, ref Pickup picked, ref int action, Color? rightColor = null)
        {
            if (list.Count == 0) return;
            float s = HudTheme.S;
            var r = f.Row(24f, 4f);
            if (Event.current.type == EventType.Repaint)
            {
                HudTheme.Circle(new Vector2(r.x + 12f * s, r.center.y), 14f * s, HudTheme.Ink);
                HudTheme.Circle(new Vector2(r.x + 12f * s, r.center.y), 9f * s, dot);
                HudTheme.Text(new Rect(r.x + 30f * s, r.y, 200f * s, r.height), title, 15f, HudTheme.Muted, TextAnchor.MiddleLeft, false);
                if (!string.IsNullOrEmpty(right)) HudTheme.Text(new Rect(r.xMax - 240f * s, r.y, 232f * s, r.height), right, 15f, rightColor ?? HudTheme.Quota, TextAnchor.MiddleRight, false);
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
            if (Event.current.type == EventType.Repaint && inPack && gun && dragLive)
            {
                if (p == dragItem) HudTheme.Rounded(r, HudTheme.Alpha(HudTheme.Ink, 0.35f), 12f * s);                                  // (the weapon in the mouse's hand: dimmed where it was)
                else if (MenuKit.Over(r)) HudTheme.Rounded(r, HudTheme.Alpha(HudTheme.Info, 0.30f), 12f * s);                         // (the weapon it would swap places with)
            }

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

            if (inPack && gun) GrabAndDrop(p, r);

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
            int barSlot = inPack && gun ? WeaponBarRules.SlotOf(barKeys, p.GetInstanceID()) : -1;                  // (the shortcut it is on, if any: the owner, 2026-10-22)
            string slotText = barSlot >= 0 ? "SLOT " + (barSlot + 1) : null;
            float chipW = inHand ? HudTheme.Width("IN HAND", 12f) + 24f * s : 0f;
            float slotW = slotText != null ? HudTheme.Width(slotText, 12f) + 24f * s : 0f;
            float chipsW = chipW + slotW + ((inHand ? 1 : 0) + (slotText != null ? 1 : 0)) * 10f * s;               // (the chips and the gap before each)
            string name = MenuKit.Fit(p.Label, 21f * s, textW - chipsW);
            HudTheme.Text(new Rect(tx, r.y + 7f * s, textW - chipsW, 28f * s), name, 21f, HudTheme.Bright, TextAnchor.MiddleLeft, false);
            float chipX = tx + Mathf.Min(HudTheme.TextWidthPx(name, 21f * s), textW - chipsW) + 10f * s;
            if (inHand) { HudTheme.Chip(new Rect(chipX, r.y + 10f * s, chipW, 22f * s), "IN HAND", HudTheme.Info, 12f); chipX += chipW + 10f * s; }
            if (slotText != null) HudTheme.Chip(new Rect(chipX, r.y + 10f * s, slotW, 22f * s), slotText, HudTheme.Quota, 12f);

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
