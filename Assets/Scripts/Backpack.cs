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
            if (!CanFit(p, out string why)) { Say(why); return false; }
            ApplyStored(p);
            return true;
        }

        /// <summary>Weight check; <paramref name="why"/> is the message when it does not fit.</summary>
        public bool CanFit(Pickup p, out string why)
        {
            why = null;
            if (Weight + p.weight <= capacity + 0.001f) return true;
            why = $"Too heavy for your pack ({p.weight:0.#} kg, {Mathf.Max(0, capacity - Weight):0.#} kg free)" +
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
            if (Viewed) Say($"Picked up {p.Label} ({p.weight:0.#} kg)");
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
            var label = HudStyles.Label(18);
            float w = Weight;
            // the weight bar and the prompts only draw on Repaint; the inventory window (buttons) needs every event
            if (Event.current.type == EventType.Repaint) DrawHud(label, w);
            if (UiOpen) DrawInventory(label, w);
        }

        /// <summary>The pack card (bottom left: the weight, the loot in the pack, what is in hand) and the pick-up prompt.</summary>
        void DrawHud(GUIStyle label, float w)
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
                    ? $"[E] Pick up {target.Label}  ({target.weight:0.#} kg" + (target.Value > 0 ? $", ${target.Value}" : "") + ")"
                    : $"{target.Label} ({target.weight:0.#} kg) is too heavy for your pack - [Q] carry it";
                warn = !fits;
            }
            else if (Time.time < noticeUntil) prompt = notice;
            float y = Screen.height * 0.62f;
            if (prompt != null) HudWidgets.Prompt(prompt, y, warn);
            if (target && Time.time < noticeUntil) HudWidgets.Prompt(notice, y + HudTheme.Px(62f), false, 17f, 1);
        }

        void DrawInventory(GUIStyle label, float w)
        {
            const float pw = 640f;
            float ph = Mathf.Min(Screen.height - 80f, 560f);
            var r = new Rect((Screen.width - pw) / 2f, (Screen.height - ph) / 2f, pw, ph);
            GUI.Box(r, "BACKPACK");
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 30, pw - 28, ph - 40));
            GUILayout.Label($"Weight {w:0.#} / {capacity:0.#} kg    (Tab to close)", label);
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("In your pack:", label);
            if (items.Count == 0) GUILayout.Label("  (empty)");
            Pickup toDrop = null;
            foreach (var p in items)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Describe(p) + (p == Equipped ? "   [in hand]" : ""), GUILayout.Width(380));
                if (RangedWeapon.IsWeapon(p))
                {
                    if (GUILayout.Button(p == Equipped ? "Unequip" : "Equip", GUILayout.Width(80))) SetEquipped(p == Equipped ? null : p);
                }
                else GUILayout.Space(84);
                if (GUILayout.Button("Drop", GUILayout.Width(80))) toDrop = p;
                GUILayout.EndHorizontal();
            }
            if (items.Count > 0 && GUILayout.Button("Drop everything", GUILayout.Width(180))) DropAll();
            if (toDrop) Drop(toDrop);

            GUILayout.Space(10);
            GUILayout.Label("On the ground nearby:", label);
            // pruned on the Layout event only, so the controls drawn for the later events of this frame are the same ones
            if (Event.current.type == EventType.Layout) nearby.RemoveAll(p => !p || p.IsHeld || !p.gameObject.activeInHierarchy);
            if (nearby.Count == 0) GUILayout.Label("  (nothing in reach)");
            Pickup toTake = null;
            foreach (var p in nearby)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Describe(p), GUILayout.Width(470));
                bool fits = w + p.weight <= capacity + 0.001f;
                var was = GUI.enabled; GUI.enabled = fits;
                if (GUILayout.Button(fits ? "Take" : "Too heavy", GUILayout.Width(80))) toTake = p;
                GUI.enabled = was;
                GUILayout.EndHorizontal();
            }
            if (toTake) { TryStore(toTake); nearby.Remove(toTake); }

            GUILayout.Space(8);
            GUILayout.Label("Tip: compare $ per kg - bring the best value for the space. The vendor pays 70% of value.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static string Describe(Pickup p)
        {
            string s = $"{p.Label}   {p.weight:0.#} kg";
            if (p.TryGetComponent(out RangedWeapon gun)) s += $"   {gun.Magazine.Rounds}/{gun.Magazine.Capacity} rounds";
            if (p.Value > 0) s += $"   ${p.Value}   (${p.Value / p.weight:0}/kg)";
            return s;
        }
    }
}
