using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The service elevator car (the same corner room on every level; it is the way to the Loading Docks).
    /// Loot dropped inside the car (not carried, not in a pack) is banked automatically toward the quota; weapons
    /// and other items are never banked.
    ///
    /// A wall panel (ElevatorPanel) has a button for every floor and a GO button: press F beside it, click the floor
    /// you want, then click GO. Everyone must be aboard. The car can only go to the next floor once the quota is met
    /// (not from 3, 6, 9, 12, where the team must return), or down to the Loading Docks; from the docks it goes to the
    /// next unfinished floor. Loot and items left in the car ride along. On floor 12 the car's back door is locked
    /// until the key from a guard is used on it (F).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ElevatorExtraction : MonoBehaviour
    {
        [Tooltip("F: use the key on the locked back door (floor 12). The panel itself is used with F too (see ElevatorPanel).")]
        public KeyCode directionKey = KeyCode.F;
        /// <summary>Panel action codes: 0 = key at the back door, 1 = GO, SelectBase + floor = choose that floor.</summary>
        public const int SelectBase = 10;
        public ElevatorDoor door;

        [Header("Panel (set by the floor generator)")]
        public Vector3 panelPosition;
        public float panelReach = 4.5f;
        [Tooltip("Floor 12 only: the locked back door to the hidden elevator.")]
        public ElevatorDoor backDoor;
        public Vector3 backDoorPosition;
        public float backDoorReach = 3.5f;

        /// <summary>The car on this floor (there is one).</summary>
        public static ElevatorExtraction Current { get; private set; }

        /// <summary>True while the doors are shut and the car is about to move.</summary>
        public static bool Sealing { get; private set; }
        bool sealing;

        public static bool EveryoneAboard { get; private set; }
        /// <summary>True when the chosen floor is above the car (kept for the network state).</summary>
        public static bool GoingUp { get; private set; } = true;
        /// <summary>The floor chosen on the panel (-1: nothing chosen yet).</summary>
        public static int Destination { get; private set; } = -1;
        int selected = -1;
        /// <summary>Floor 12: the back door has been unlocked.</summary>
        public static bool BackDoorOpen { get; private set; }
        /// <summary>The local player is inside the car (the panel prompt shows).</summary>
        public static bool LocalInside { get; private set; }

        readonly HashSet<HeavyLoot> inside = new();
        readonly Dictionary<HeavyLoot, float> since = new();
        readonly Dictionary<Throwable, float> itemSince = new();
        public float settleSeconds = 0.8f;
        static string toast; static float toastUntil;
        readonly HashSet<Throwable> items = new();
        readonly HashSet<Backpack> packs = new();
        readonly HashSet<WobblyPlayer> aboard = new(); // players standing in the car (busted ones do not need to be)

        void Reset() { GetComponent<Collider>().isTrigger = true; }

        void OnEnable()
        {
            Current = this;
            GoingUp = true; Destination = -1; selected = -1; BackDoorOpen = false; LocalInside = false;
        }

        void OnDisable() { if (Current == this) Current = null; }

        void OnTriggerEnter(Collider o)
        {
            var l = o.GetComponentInParent<HeavyLoot>(); if (l && inside.Add(l)) since[l] = Time.time;
            var t = o.GetComponentInParent<Throwable>(); if (t && items.Add(t)) itemSince[t] = Time.time;
            var bp = o.GetComponentInParent<Backpack>(); if (bp) packs.Add(bp);
            var wp = o.GetComponentInParent<WobblyPlayer>();
            if (wp)
            {
                aboard.Add(wp);
                if (Net.NetPlayer.IsLocalPlayer(wp)) LocalInside = true;
            }
        }

        void OnTriggerExit(Collider o)
        {
            var l = o.GetComponentInParent<HeavyLoot>(); if (l) { inside.Remove(l); since.Remove(l); }
            var t = o.GetComponentInParent<Throwable>(); if (t) { items.Remove(t); itemSince.Remove(t); }
            var bp = o.GetComponentInParent<Backpack>(); if (bp) packs.Remove(bp);
            var wp = o.GetComponentInParent<WobblyPlayer>();
            if (wp)
            {
                aboard.Remove(wp);
                if (Net.NetPlayer.IsLocalPlayer(wp)) LocalInside = false;
            }
        }

        // ------------------------------------------------------------------ state shared with clients

        /// <summary>A client's view of the panel (the host replicates it).</summary>
        public static void NetSetPanel(bool goingUp, int destination, bool backDoorOpen)
        {
            GoingUp = goingUp; Destination = destination;
            if (backDoorOpen != BackDoorOpen)
            {
                BackDoorOpen = backDoorOpen;
                if (Current && Current.backDoor) Current.backDoor.SetOpen(backDoorOpen);
            }
        }

        /// <summary>Client: take the host's elevator flags.</summary>
        public static void NetSetFlags(bool aboard, bool sealing) { EveryoneAboard = aboard; Sealing = sealing; }

        public static void ShowToast(string text) { toast = text; toastUntil = Time.time + 3f; }

        /// <summary>A message for everyone: the host tells the clients, offline just shows it.</summary>
        public static void Announce(string text)
        {
            if (Net.NetRole.IsHost) Net.RunSync.Instance?.BroadcastToast(text); else ShowToast(text);
        }

        // ------------------------------------------------------------------ per frame

        static Transform LocalPlayerTransform()
        {
            var np = Net.NetPlayer.Local;
            if (np) return np.transform;
            return WobblyPlayerRegistry.All.Count > 0 && WobblyPlayerRegistry.All[0] ? WobblyPlayerRegistry.All[0].transform : null;
        }

        static Backpack LocalPack()
        {
            var t = LocalPlayerTransform();
            return t ? t.GetComponent<Backpack>() : null;
        }

        static bool InputBlocked => Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open || TestMode.PanelOpen || ElevatorPanel.Open || OutpostUi.Open;

        /// <summary>A click on the wall panel from this machine: the host runs it, a client asks the host.</summary>
        public void LocalPress(int action)
        {
            if (Net.NetRole.IsRemoteClient) { Net.RunSync.Instance?.RequestPanel(action); return; }
            var me = LocalPlayerTransform();
            if (me) HostPress(me, LocalPack(), action);
        }

        void Update()
        {
            // F here only works the key on the floor-12 back door; the wall panel has its own script
            bool pressF = !InputBlocked && Input.GetKeyDown(directionKey);

            if (Net.NetRole.IsRemoteClient)
            {
                // the host runs the elevator; this machine mirrors its doors and passes on the button presses
                if (door) door.SetOpen(!Sealing);
                if (pressF && NearBackDoorLocal()) Net.RunSync.Instance?.RequestPanel(0);
                return;
            }

            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            if (!hm || !tm) return;

            // loot is only banked on office floors; the docks vendor buys it instead
            bool canBank = hm.Phase == HeistPhase.Heist || hm.Phase == HeistPhase.FloorComplete;
            EveryoneAboard = WobblyPlayerRegistry.AllAboard(aboard);
            if (canBank) AutoBank();

            RefreshPanel(hm, tm);

            var me = LocalPlayerTransform();
            if (me && pressF && NearBackDoorLocal()) HostPress(me, LocalPack(), 0);
        }

        bool NearBackDoorLocal()
        {
            var me = LocalPlayerTransform();
            return backDoor && me && (me.position - backDoorPosition).sqrMagnitude <= backDoorReach * backDoorReach;
        }

        /// <summary>Keeps the chosen floor only while the elevator can still go there.</summary>
        void RefreshPanel(HeistManager hm, TowerManager tm)
        {
            int up = TowerRules.UpFrom(hm.Floor, hm.Phase == HeistPhase.FloorComplete, tm.ResumeLevel);
            int down = TowerRules.DownFrom(hm.Floor);
            if (selected >= 0 && selected != up && selected != down) selected = -1;
            Destination = selected;
            GoingUp = selected < 0 || selected == up;
        }

        /// <summary>
        /// Why the panel will not take the car to floor <paramref name="f"/> (shown when a button that is not
        /// available is clicked). <paramref name="up"/> / <paramref name="down"/> are where the car can go (-1: nowhere).
        /// </summary>
        public static string WhyNot(int f, int current, int up, int down)
        {
            if (f == current) return $"You are already on {TowerRules.Label(f)}";
            if (TowerRules.IsBoss(current)) return "The elevator is out of service";
            if (f > current)
            {
                if (TowerRules.Beyond(f)) return $"The demo ends at {TowerRules.Label(TowerRules.DemoLastFloor)}: the rest of the tower is in the full game";
                if (up < 0 && TowerRules.IsDocks(current)) return "Finished floors are closed";
                if (up >= 0) return $"From here the elevator only goes up to {TowerRules.Label(up)}";
                if (TowerRules.IsGroupEnd(current)) return "Three floors done: return to the Loading Docks first";
                return "Meet the quota before going up";
            }
            if (down >= 0) return "Going down always leads to the Loading Docks";
            return "There is no way down from here";
        }

        /// <summary>
        /// A player pressed a panel button (host / offline). action 0 = the key at the back door, 1 = GO,
        /// SelectBase + n = choose floor n. Distance to the panel is checked here, so a client cannot press it from
        /// across the floor.
        /// </summary>
        public void HostPress(Transform who, Backpack pack, int action)
        {
            if (!who || sealing) return;
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            if (!hm || !tm || tm.Loading) return;
            bool nearPanel = (who.position - panelPosition).sqrMagnitude <= panelReach * panelReach;
            bool nearBack = backDoor && (who.position - backDoorPosition).sqrMagnitude <= backDoorReach * backDoorReach;

            if (action == 0)
            {
                if (nearBack && !BackDoorOpen) TryUnlockBackDoor(pack);
                return;
            }
            if (!nearPanel) return;

            RefreshPanel(hm, tm);
            int up = TowerRules.UpFrom(hm.Floor, hm.Phase == HeistPhase.FloorComplete, tm.ResumeLevel);
            int down = TowerRules.DownFrom(hm.Floor);

            if (action >= SelectBase)
            {
                int f = action - SelectBase;
                if (f < 0 || f >= TowerRules.Levels) return;
                if (f == up || f == down) selected = selected == f ? -1 : f;   // click again to un-choose
                else Announce(WhyNot(f, hm.Floor, up, down));
                RefreshPanel(hm, tm);
            }
            else if (action == 1)
            {
                if (Destination < 0)
                {
                    Announce(up < 0 && down < 0 ? "The elevator is out of service" : "Choose a floor on the panel first");
                    return;
                }
                if (!EveryoneAboard) { Announce("Everyone must be inside the elevator"); return; }
                StartCoroutine(Seal(Destination));
            }
        }

        void TryUnlockBackDoor(Backpack pack)
        {
            var key = ElevatorKey.InPack(pack);
            if (key == null) { Announce("The back door is locked. One of the guards has the key"); return; }
            int id = key.NetId;
            pack.RemoveUsed(key);
            Net.RunSync.Instance?.BroadcastConsumed(id);
            Destroy(key.gameObject);
            BackDoorOpen = true;
            if (backDoor) backDoor.SetOpen(true); // reaches clients through the run state
            Announce("The back door unlocks. A hidden elevator waits beyond it");
        }

        System.Collections.IEnumerator Seal(int destination)
        {
            sealing = true; Sealing = true;
            if (door) door.SetOpen(false);
            yield return new WaitForSeconds(door ? 1.6f : 0.2f);
            yield return new WaitForSeconds(0.8f);
            TowerManager.Instance?.RideTo(destination);
            yield return new WaitForSeconds(0.5f);
            // still here: the ride was refused, so open up again
            Sealing = false; sealing = false;
            if (door) door.SetOpen(true);
        }

        void OnDestroy() { EveryoneAboard = false; Sealing = false; LocalInside = false; if (Current == this) Current = null; }

        // ------------------------------------------------------------------ banking

        // scratch copies, so banking can remove from the sets while it walks them (and so a quiet frame makes no garbage)
        readonly List<Throwable> itemScratch = new();
        readonly List<HeavyLoot> lootScratch = new();

        /// <summary>Banks loot that has been dropped in the car: set down (not held) and settled for a moment.</summary>
        void AutoBank()
        {
            if (items.Count == 0 && inside.Count == 0) return;

            // small items (mugs, shop gadgets) count for their small bank value; weapons never do
            items.RemoveWhere(t => !t || !t.gameObject.activeInHierarchy);
            itemScratch.Clear(); itemScratch.AddRange(items);
            foreach (var t in itemScratch)
            {
                if (!t.TryGetComponent(out Pickup ip) || ip.bankValue <= 0 || ip.IsHeld) continue;
                if (!itemSince.TryGetValue(t, out float s0)) { itemSince[t] = Time.time; continue; }
                if (Time.time - s0 < settleSeconds) continue;
                if (t.TryGetComponent(out Rigidbody irb) && irb.linearVelocity.sqrMagnitude > 4f) { itemSince[t] = Time.time; continue; }
                HeistManager.Instance?.BankAmount(ip.bankValue);
                Announce($"+${ip.bankValue} banked: {ip.Label}");
                items.Remove(t); itemSince.Remove(t);
                Net.RunSync.Instance?.BroadcastConsumed(ip.NetId);
                Destroy(t.gameObject);
            }
            itemScratch.Clear();

            inside.RemoveWhere(l => !l || !l.gameObject.activeInHierarchy);
            if (inside.Count == 0) return;
            lootScratch.Clear(); lootScratch.AddRange(inside);
            foreach (var l in lootScratch)
            {
                if (!l || l.Broken) continue;
                bool carried = l.Holders > 0 || (l.TryGetComponent(out Pickup p) && p.IsHeld);
                if (carried) { since[l] = Time.time; continue; }
                if (!since.TryGetValue(l, out float t0)) { since[l] = Time.time; continue; }
                if (Time.time - t0 < settleSeconds) continue;
                if (l.TryGetComponent(out Rigidbody rb) && rb.linearVelocity.sqrMagnitude > 4f) { since[l] = Time.time; continue; }
                int v = l.CurrentValue;
                HeistManager.Instance?.Bank(l);
                Announce($"+${v} banked: {l.displayName}");
                inside.Remove(l); since.Remove(l);
                if (l.TryGetComponent(out Pickup lp)) Net.RunSync.Instance?.BroadcastConsumed(lp.NetId);
                Destroy(l.gameObject);
            }
            lootScratch.Clear();
        }

        // ------------------------------------------------------------------ prompts

        static readonly Color ToastColour = new Color(0.5f, 1f, 0.5f);

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;     // nothing here takes input
            if (Time.time < toastUntil && !string.IsNullOrEmpty(toast))
                HudWidgets.Notice(toast, Screen.height * 0.37f, HudTheme.Good, 24f, HudAnim.Id.Notice2);

            // the wall panel draws its own prompts (ElevatorPanel); this is only the locked back door on floor 12
            if (!backDoor || BackDoorOpen || !HeistManager.Instance || !NearBackDoorLocal()) return;
            HudWidgets.Prompt($"Back door is locked - [{directionKey}] to use the key", Screen.height * 0.72f - HudTheme.Px(HudWidgets.PromptStep), true, 19f, 1);
        }

        // ------------------------------------------------------------------ cargo

        /// <summary>Network ids of what is in the car (the host names them so every machine carries the same things).</summary>
        public ushort[] CargoIds()
        {
            var ids = new List<ushort>();
            foreach (var l in inside)
                if (l && l.gameObject.activeInHierarchy && !l.Broken && l.TryGetComponent(out Pickup p) && p.NetId >= 0) ids.Add((ushort)p.NetId);
            foreach (var t in items)
                if (t && t.gameObject.activeInHierarchy && t.TryGetComponent(out Pickup p) && p.NetId >= 0) ids.Add((ushort)p.NetId);
            return ids.ToArray();
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

    /// <summary>
    /// The hidden elevator behind the service car's back door on floor 12. It only goes to floor 13 (the boss).
    /// Everyone must be aboard and one player presses G.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BossElevator : MonoBehaviour
    {
        public KeyCode confirmKey = KeyCode.G;
        public ElevatorDoor door;

        public static BossElevator Current { get; private set; }
        public static bool LocalInside { get; private set; }
        public static bool EveryoneAboard { get; private set; }

        readonly HashSet<WobblyPlayer> aboard = new(); // players standing in the car (busted ones do not need to be)
        bool going;

        void Reset() { GetComponent<Collider>().isTrigger = true; }
        void OnEnable() { Current = this; LocalInside = false; EveryoneAboard = false; }
        void OnDisable() { if (Current == this) Current = null; }

        void OnTriggerEnter(Collider o)
        {
            var wp = o.GetComponentInParent<WobblyPlayer>();
            if (!wp) return;
            aboard.Add(wp);
            if (Net.NetPlayer.IsLocalPlayer(wp)) LocalInside = true;
        }

        void OnTriggerExit(Collider o)
        {
            var wp = o.GetComponentInParent<WobblyPlayer>();
            if (!wp) return;
            aboard.Remove(wp);
            if (Net.NetPlayer.IsLocalPlayer(wp)) LocalInside = false;
        }

        void Update()
        {
            bool blocked = Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open || TestMode.PanelOpen || OutpostUi.Open;
            bool press = !blocked && LocalInside && Input.GetKeyDown(confirmKey);
            if (Net.NetRole.IsRemoteClient)
            {
                if (press) Net.RunSync.Instance?.RequestPanel(1);
                return;
            }
            EveryoneAboard = WobblyPlayerRegistry.AllAboard(aboard);
            if (press)
            {
                var me = Net.NetPlayer.Local ? Net.NetPlayer.Local.transform
                         : (WobblyPlayerRegistry.All.Count > 0 ? WobblyPlayerRegistry.All[0].transform : null);
                HostPress(me, 1);
            }
        }

        /// <summary>A player pressed Go (host / offline).</summary>
        public void HostPress(Transform who, int action)
        {
            if (going || action != 1 || !who) return;
            var tm = TowerManager.Instance;
            if (tm == null || tm.Loading) return;
            if ((who.position - transform.position).sqrMagnitude > 6f * 6f) return;
            if (!EveryoneAboard) { ElevatorExtraction.Announce("Everyone must be inside the elevator"); return; }
            going = true;
            tm.RideTo(TowerRules.BossFloor);
        }

        void OnGUI()
        {
            if (!LocalInside || Event.current.type != EventType.Repaint) return;
            HudWidgets.Prompt($"Hidden elevator to {TowerRules.Label(TowerRules.BossFloor)}   [{confirmKey}] go" + (EveryoneAboard ? "" : "   (everyone must be inside)"),
                Screen.height * 0.72f, !EveryoneAboard);
        }
    }
}
