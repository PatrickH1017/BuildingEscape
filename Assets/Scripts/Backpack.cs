using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The player's backpack. E picks up the nearest item (if it fits in the weight limit),
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
        float nextNearby;

        public IReadOnlyList<Pickup> Items => items;
        /// <summary>The melee weapon shown in the right hand (must be an item in the pack).</summary>
        public Pickup Equipped { get; private set; }
        public float Weight { get { float w = 0; foreach (var p in items) if (p) w += p.weight; return w; } }

        public bool Viewed
        {
            get
            {
                if (!rig) rig = FindFirstObjectByType<CameraFollow>();
                return !rig || !rig.target || rig.target == transform || rig.target.IsChildOf(transform);
            }
        }

        void Awake()
        {
            holder = new GameObject("BackpackItems").transform;
            holder.SetParent(transform, false);
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
            if (!Equipped && p.GetComponent<MeleeWeapon>()) Equipped = p;
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
            CleanUp();
            bool talk = Vendor.ShopOpen || (Vendor.Nearby && Vendor.Nearby.CanTalk);
            if (Input.GetKeyDown(inventoryKey) && !Vendor.ShopOpen) { UiOpen = !UiOpen; scroll = Vector2.zero; }

            target = (UiOpen || talk) ? null : Pickup.FindNearest(transform, reach);
            if (!UiOpen && !talk && Input.GetKeyDown(pickupKey))
            {
                if (target) TryStore(target);
                else Say("Nothing to pick up here");
            }

            if (UiOpen && Time.time >= nextNearby)
            {
                nextNearby = Time.time + 0.2f;
                nearby.Clear();
                Pickup.ScanNear(transform, reach + 0.8f, p => nearby.Add(p));
            }
        }

        // ------------------------------------------------------------------ ui

        void OnGUI()
        {
            if (!Viewed) return;
            var label = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            float w = Weight;

            // weight bar (bottom left, above the controls line)
            var bar = new Rect(12, Screen.height - 66, 260, 18);
            GUI.Box(bar, GUIContent.none);
            float f = Mathf.Clamp01(w / Mathf.Max(0.1f, capacity));
            var old = GUI.color;
            GUI.color = f > 0.9f ? Color.red : f > 0.65f ? Color.yellow : Color.green;
            GUI.DrawTexture(new Rect(bar.x + 2, bar.y + 2, (bar.width - 4) * f, bar.height - 4), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(bar.xMax + 8, bar.y - 6, 400, 28), $"Pack {w:0.#} / {capacity:0.#} kg   (Tab: inventory)", label);

            if (!UiOpen)
            {
                string prompt = null;
                if (target)
                {
                    bool fits = w + target.weight <= capacity + 0.001f;
                    prompt = fits
                        ? $"[E] Pick up {target.Label}  ({target.weight:0.#} kg" + (target.Value > 0 ? $", ${target.Value}" : "") + ")"
                        : $"{target.Label} ({target.weight:0.#} kg) is too heavy for your pack - [Q] carry it";
                }
                else if (Time.time < noticeUntil) prompt = notice;
                if (prompt != null)
                {
                    var c = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
                    GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 36), prompt, c);
                }
                if (target && Time.time < noticeUntil)
                    GUI.Label(new Rect(0, Screen.height * 0.62f + 34, Screen.width, 30), notice,
                        new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            DrawInventory(label, w);
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
                if (p.GetComponent<MeleeWeapon>())
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
            nearby.RemoveAll(p => !p || p.IsHeld || !p.gameObject.activeInHierarchy);
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
            if (p.Value > 0) s += $"   ${p.Value}   (${p.Value / p.weight:0}/kg)";
            return s;
        }
    }
}
