using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Elevator car (same spot on every level). Loot dropped inside the car (not carried, not in a pack) is
    /// banked automatically toward the quota; weapons and other items are never banked. Once the quota is
    /// met and everyone is aboard, the doors close and the car drops to the basement; G in the basement
    /// rides up to the next unfinished floor. Loot and items left in the car ride along.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ElevatorExtraction : MonoBehaviour
    {
        public KeyCode bankKey = KeyCode.F;
        public KeyCode rideKey = KeyCode.G;
        public KeyCode downKey = KeyCode.B; // unused: finished floors send you down automatically
        public ElevatorDoor door;

        /// <summary>True while the doors are shut and the car is about to move.</summary>
        public static bool Sealing { get; private set; }
        bool sealing;

        readonly HashSet<HeavyLoot> inside = new();
        readonly Dictionary<HeavyLoot, float> since = new();
        readonly Dictionary<Throwable, float> itemSince = new();
        public float settleSeconds = 0.8f;
        string toast; float toastUntil;
        readonly HashSet<Throwable> items = new();
        readonly HashSet<Backpack> packs = new();
        int playersInside;

        public static bool EveryoneAboard { get; private set; }

        void Reset() { GetComponent<Collider>().isTrigger = true; }

        void OnTriggerEnter(Collider o)
        {
            var l = o.GetComponentInParent<HeavyLoot>(); if (l && inside.Add(l)) since[l] = Time.time;
            var t = o.GetComponentInParent<Throwable>(); if (t && items.Add(t)) itemSince[t] = Time.time;
            var bp = o.GetComponentInParent<Backpack>(); if (bp) packs.Add(bp);
            if (o.GetComponentInParent<WobblyPlayer>()) playersInside++;
        }

        void OnTriggerExit(Collider o)
        {
            var l = o.GetComponentInParent<HeavyLoot>(); if (l) { inside.Remove(l); since.Remove(l); }
            var t = o.GetComponentInParent<Throwable>(); if (t) { items.Remove(t); itemSince.Remove(t); }
            var bp = o.GetComponentInParent<Backpack>(); if (bp) packs.Remove(bp);
            if (o.GetComponentInParent<WobblyPlayer>()) playersInside = Mathf.Max(0, playersInside - 1);
        }

        void Update()
        {
            var hm = HeistManager.Instance;
            // loot is only banked on office floors; the basement vendor buys it instead
            bool canBank = hm && (hm.Phase == HeistPhase.Heist || hm.Phase == HeistPhase.FloorComplete);
            EveryoneAboard = playersInside > 0 && playersInside >= WobblyPlayerRegistry.All.Count;
            if (canBank) AutoBank();
            if (!sealing && EveryoneAboard && hm)
            {
                // quota met: once everyone is aboard the doors shut and the car drops to the basement
                if (hm.Phase == HeistPhase.FloorComplete) StartCoroutine(Seal(true));
                // in the basement: G shuts the doors and rides up to the next unfinished floor
                else if (hm.Phase == HeistPhase.Hub && Input.GetKeyDown(rideKey)) StartCoroutine(Seal(false));
            }
        }

        System.Collections.IEnumerator Seal(bool down)
        {
            sealing = true; Sealing = true;
            if (door) door.SetOpen(false);
            yield return new WaitForSeconds(door ? 1.6f : 0.2f);
            yield return new WaitForSeconds(0.8f);
            TowerManager.Instance?.RideElevator(down);
            yield return new WaitForSeconds(0.5f);
            // still here: the ride was refused, so open up again
            Sealing = false; sealing = false;
            if (door) door.SetOpen(true);
        }

        void OnDestroy() { EveryoneAboard = false; Sealing = false; }

        /// <summary>Banks loot that has been dropped in the car: set down (not held) and settled for a moment.</summary>
        void AutoBank()
        {
            // small items (mugs, shop gadgets) count for their small bank value; weapons never do
            items.RemoveWhere(t => !t || !t.gameObject.activeInHierarchy);
            foreach (var t in new List<Throwable>(items))
            {
                if (!t.TryGetComponent(out Pickup ip) || ip.bankValue <= 0 || ip.IsHeld) continue;
                if (!itemSince.TryGetValue(t, out float s0)) { itemSince[t] = Time.time; continue; }
                if (Time.time - s0 < settleSeconds) continue;
                if (t.TryGetComponent(out Rigidbody irb) && irb.linearVelocity.sqrMagnitude > 4f) { itemSince[t] = Time.time; continue; }
                HeistManager.Instance?.BankAmount(ip.bankValue);
                toast = $"+${ip.bankValue} banked: {ip.Label}"; toastUntil = Time.time + 3f;
                items.Remove(t); itemSince.Remove(t);
                Destroy(t.gameObject);
            }

            inside.RemoveWhere(l => !l || !l.gameObject.activeInHierarchy);
            if (inside.Count == 0) return;
            foreach (var l in new List<HeavyLoot>(inside))
            {
                if (!l || l.Broken) continue;
                bool carried = l.Holders > 0 || (l.TryGetComponent(out Pickup p) && p.IsHeld);
                if (carried) { since[l] = Time.time; continue; }
                if (!since.TryGetValue(l, out float t0)) { since[l] = Time.time; continue; }
                if (Time.time - t0 < settleSeconds) continue;
                if (l.TryGetComponent(out Rigidbody rb) && rb.linearVelocity.sqrMagnitude > 4f) { since[l] = Time.time; continue; }
                int v = l.CurrentValue;
                HeistManager.Instance?.Bank(l);
                toast = $"+${v} banked: {l.displayName}"; toastUntil = Time.time + 3f;
                inside.Remove(l); since.Remove(l);
                Destroy(l.gameObject);
            }
        }

        void OnGUI()
        {
            if (Time.time >= toastUntil || string.IsNullOrEmpty(toast)) return;
            var st = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            st.normal.textColor = new Color(0.5f, 1f, 0.5f);
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 40), toast, st);
        }

        /// <summary>Everything unbroken inside the car that should ride along. Empties the car's lists.</summary>
        public List<Rigidbody> TakeCargo()
        {
            var cargo = new List<Rigidbody>();
            foreach (var l in inside)
                if (l && l.gameObject.activeInHierarchy && !l.Broken && l.TryGetComponent(out Rigidbody rb)) cargo.Add(rb);
            foreach (var t in items)
                if (t && t.gameObject.activeInHierarchy && t.TryGetComponent(out Rigidbody rb)) cargo.Add(rb);
            inside.Clear();
            items.Clear();
            return cargo;
        }
    }
}
