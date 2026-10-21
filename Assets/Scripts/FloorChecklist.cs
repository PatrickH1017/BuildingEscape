using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>What the scan and the journal's checklist tell apart (the owner, 2026-10-20, over his 2026-10-19 colours): safes and valuables glow gold, unique items such as weapons red and cash green.</summary>
    public enum ScanKind { Valuable = 0, Unique = 1, Safe = 2, Cash = 3 }

    /// <summary>
    /// The words and the colours of the checklist and the scan, and when a line of the checklist is done: without Unity types, so they are tested outside Unity. The owner, 2026-10-19: "On the journal should be a tab that has check boxes
    /// and a list: All valuables collected / All unique items collected / All safes opened / All cash collected. After collecting all of one of those the check box after the text should get checked (green check) and the text should get a
    /// line through it crossing it out (green color)" and "The scan should make safes glow red, unique items purple (for example a weapon), cash green, valuables gold".
    /// </summary>
    public static class ChecklistRules
    {
        public const int Count = 4;
        /// <summary>How long a floor has to be there before its checklist starts counting (the things of a new floor are made and placed in the first moments).</summary>
        public const float SettleSeconds = 1.5f;

        public static string Label(ScanKind k) =>
            k == ScanKind.Valuable ? "All valuables collected" : k == ScanKind.Unique ? "All unique items collected" : k == ScanKind.Safe ? "All safes opened" : "All cash collected";

        /// <summary>The glow of a kind as red, green and blue (0 to 1): safes and valuables gold, unique items (weapons) red, cash green (the owner, 2026-10-20: "safes and valuables should be gold when scanning, weapons should be red, and cash should be green").</summary>
        public static void Rgb(ScanKind k, out float r, out float g, out float b)
        {
            switch (k)
            {
                case ScanKind.Safe: r = 1.00f; g = 0.80f; b = 0.18f; break;
                case ScanKind.Unique: r = 0.95f; g = 0.22f; b = 0.22f; break;
                case ScanKind.Cash: r = 0.28f; g = 0.92f; b = 0.42f; break;
                default: r = 1.00f; g = 0.80f; b = 0.18f; break;
            }
        }
    }

    /// <summary>
    /// One floor's checklist: a line is done when none of its kind is left to collect, and stays done (a thing put down again later does not take the tick back). A floor that never had a thing of a kind has that line done at once
    /// (nothing is left). Counting starts <see cref="ChecklistRules.SettleSeconds"/> after the first count of a floor.
    /// </summary>
    public sealed class ChecklistState
    {
        readonly bool[] done = new bool[ChecklistRules.Count];
        readonly int[] first = { -1, -1, -1, -1 };
        float startedAt = -1f;

        public void Reset()
        {
            for (int i = 0; i < done.Length; i++) { done[i] = false; first[i] = -1; }
            startedAt = -1f;
        }

        public bool Done(ScanKind k) => done[(int)k];
        /// <summary>How many of a kind there were when counting began (-1: it has not begun).</summary>
        public int First(ScanKind k) => first[(int)k];

        /// <summary><paramref name="remaining"/>: how many of each kind are left to collect now; <paramref name="now"/>: the time, in seconds.</summary>
        public void Update(int[] remaining, float now)
        {
            if (startedAt < 0f) startedAt = now;
            if (now - startedAt < ChecklistRules.SettleSeconds) return;
            for (int i = 0; i < done.Length; i++)
            {
                if (first[i] < 0) first[i] = remaining[i];
                if (remaining[i] <= 0) done[i] = true;
            }
        }
    }

    /// <summary>What lies on the floor to be collected, by kind: the same rules the scan and the checklist use.</summary>
    public static class FloorChecklist
    {
        /// <summary>The label of a bundle of cash (the safes' cash and the bits of cash that lie in them).</summary>
        public const string CashLabel = "Cash bundle";

        /// <summary>Which kind a pickup is, or -1 when the scan and the checklist do not care for it (a spare clip, a mug, a smashed thing).</summary>
        public static int Classify(Pickup p)
        {
            if (!p) return -1;
            if (p.TryGetComponent(out AmmoClip _)) return -1;
            if (p.TryGetComponent(out ArmourItem _) || p.TryGetComponent(out RangedWeapon _) || p.TryGetComponent(out MeleeWeapon _)) return (int)ScanKind.Unique;
            if (p.Label == CashLabel) return (int)ScanKind.Cash;
            if (p.Loot && p.Loot.Broken) return -1;
            return LootScanRules.Worth(p.Value) ? (int)ScanKind.Valuable : -1;
        }

        /// <summary>Is it lying about (not in a pack, not in a hand, not carried by somebody, not in the elevator car where it is about to be banked)?</summary>
        public static bool Lying(Pickup p, Collider car)
        {
            if (!p || !p.isActiveAndEnabled || p.IsHeld) return false;
            var l = p.Loot;
            if (l && l.Holders > 0) return false;
            return !(car && car.bounds.Contains(p.transform.position));
        }

        /// <summary>How many of each kind (by <see cref="ScanKind"/>) are left to collect on the floor: what lies about, and what is still inside a shut safe (and the safe itself).</summary>
        public static void Remaining(int[] into)
        {
            for (int i = 0; i < into.Length; i++) into[i] = 0;
            var carObj = ElevatorExtraction.Current;
            Collider car = carObj && carObj.TryGetComponent(out Collider cc) ? cc : null;
            foreach (var p in Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None))
            {
                int k = Classify(p);
                if (k >= 0 && Lying(p, car)) into[k]++;
            }
            foreach (var s in FloorSafe.OnShow)
            {
                if (!s || s.IsOpen) continue;
                into[(int)ScanKind.Safe]++;
                into[(int)ScanKind.Unique] += s.GunCount;                // (what a shut safe holds is out of the world until it is open: the guns and pieces of armour, the cash, the watch)
                into[(int)ScanKind.Cash] += s.BundleCount;
                into[(int)ScanKind.Valuable] += s.WatchCount;
            }
            var gun = GunSafeProp.Active;
            if (gun && !gun.IsOpen) { into[(int)ScanKind.Safe]++; into[(int)ScanKind.Unique]++; }
        }
    }

    /// <summary>
    /// Counts what is left to collect on the floor the player is on, twice a second, and keeps the journal's checklist (<see cref="ChecklistState"/>). Purely local: every machine has the same things in the same places and sees the same safes open.
    /// </summary>
    public sealed class FloorChecklistTracker : MonoBehaviour
    {
        public static FloorChecklistTracker Instance { get; private set; }

        readonly ChecklistState state = new ChecklistState();
        readonly int[] counts = new int[ChecklistRules.Count];
        int floor = -1, built;
        float next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("FloorChecklist");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<FloorChecklistTracker>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Is this line of the checklist done on the floor the player is on? Nothing is tracked at the docks.</summary>
        public static bool IsDone(ScanKind k) => Instance && Instance.floor >= 1 && Instance.built != 0 && Instance.state.Done(k);

        /// <summary>How many of a kind the floor had when counting began (-1: not yet; for tests).</summary>
        public static int FirstCount(ScanKind k) => Instance ? Instance.state.First(k) : -1;

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            int f = hm != null ? hm.Floor : 0;
            bool ready = hm != null && tm != null && !tm.Loading && f >= 1;
            var gen = ready ? FindFirstObjectByType<FloorGenerator>() : null;
            int token = gen && gen.Current ? gen.Current.GetInstanceID() : 0;
            if (!ready || token == 0) { if (floor != f || built != 0) { floor = f; built = 0; state.Reset(); } return; }
            if (f != floor || token != built) { floor = f; built = token; state.Reset(); }
            FloorChecklist.Remaining(counts);
            state.Update(counts, Time.unscaledTime);
        }
    }
}
