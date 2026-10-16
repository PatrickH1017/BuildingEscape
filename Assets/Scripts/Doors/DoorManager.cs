using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Runs all the sliding doors of one floor (a child of the floor, so it goes with it).
    ///
    /// Players work the doors: point the crosshair at one (close enough) and press E to open it (it stays open), press E again to
    /// close it. This script takes the door the <see cref="Crosshair"/> is on, shows the "[E] Open door" prompt and sends the request. In a networked run the host decides and
    /// tells everybody (RunSync.RequestDoor / DoorSetRpc), so every machine has the same doors open; offline the door is simply set.
    /// Officers open doors by themselves too, for any walk through one: on patrol, going to look in a room, running at a player.
    /// About ten times a second this looks at the route each officer is walking (<see cref="GuardAI.GetRoute"/>); a door he is
    /// about to go through opens in time (<see cref="SlidingDoor.AnyoneWalkingIn"/>) and closes behind him. An officer who walks past a
    /// door, or stands about, opens nothing. Every frame only the doors in motion or waiting to close are moved.
    ///
    /// A door is addressed by its place in <see cref="Doors"/>: the floor is built the same way on every machine, so the same number
    /// is the same door everywhere.
    /// </summary>
    public sealed class DoorManager : MonoBehaviour
    {
        /// <summary>How far from the doorway (measured to its nearest point) a player can still work the door.</summary>
        public const float UseReach = 2.2f;
        /// <summary>The key that opens and closes a door.</summary>
        public const KeyCode UseKey = KeyCode.E;

        const float ScanEvery = 0.1f;
        const int MaxSoundsPerFrame = 3;

        /// <summary>The doors of the floor that is on show.</summary>
        public static DoorManager Active { get; private set; }

        /// <summary>True while a door prompt is on screen: the crosshair is on a door and E is about to work it.</summary>
        public static bool PromptShown { get; private set; }

        readonly List<SlidingDoor> doors = new List<SlidingDoor>();
        readonly List<Vector3> guards = new List<Vector3>();
        readonly List<Walker> walkers = new List<Walker>();     // (one per officer ever seen; the first walkerCount are the ones walking now)
        int walkerCount;
        readonly List<Vector3> everyone = new List<Vector3>();
        float nextScan;
        int promptDoor = -1;                       // the door the local player would work right now

        public int Count => doors.Count;
        public IReadOnlyList<SlidingDoor> Doors => doors;

        public void Add(SlidingDoor d) { doors.Add(d); }

        public SlidingDoor Get(int index) => index >= 0 && index < doors.Count ? doors[index] : null;

        void Awake() { Active = this; }

        void Start()
        {
            // an event about a door that was sent while this machine was still building the floor was missed: ask for the doors that are open
            if (Net.NetRole.IsRemoteClient && Net.RunSync.Instance != null) Net.RunSync.Instance.RequestDoorStates();
        }

        void OnDisable() { if (Active == this) PromptShown = false; }

        void OnDestroy()
        {
            if (Active == this) { Active = null; PromptShown = false; }
        }

        /// <summary>Opens, without a sound, every door somebody is standing at right now (nobody starts a floor inside a shut door).</summary>
        public void OpenDoorsWithPeople()
        {
            GatherPlayers(everyone);
            GatherGuards(guards);
            for (int i = 0; i < doors.Count; i++)
                if (doors[i].AnyoneAtStart(everyone) || doors[i].AnyoneInside(guards)) doors[i].SnapOpen();   // (an officer only counts if he is in the doorway itself)
        }

        void Update()
        {
            float now = Time.time, dt = Time.deltaTime;
            bool scan = now >= nextScan;
            if (scan)
            {
                nextScan = now + ScanEvery;
                GatherWalkers();
            }
            HandleUse();

            int sounds = MaxSoundsPerFrame;
            for (int i = 0; i < doors.Count; i++)
            {
                var d = doors[i];
                if (scan) d.GuardNear = d.AnyoneWalkingIn(walkers, walkerCount);
                if (d.NeedsTick) d.Tick(now, dt, ref sounds);
            }
        }

        /// <summary>Where the players are.</summary>
        static void GatherPlayers(List<Vector3> into)
        {
            into.Clear();
            var all = WobblyPlayerRegistry.All;
            for (int i = 0; i < all.Count; i++) if (all[i]) into.Add(all[i].transform.position);
        }

        /// <summary>Where the officers on their feet are (every one, walking or not).</summary>
        static void GatherGuards(List<Vector3> into)
        {
            into.Clear();
            var gs = GuardAI.All;
            for (int i = 0; i < gs.Count; i++)
            {
                var g = gs[i];
                if (!g || g.Dead || g.State == GuardState.Stunned) continue;
                into.Add(g.transform.position);
            }
        }

        /// <summary>The officers who are walking somewhere right now: the route each is on and how fast he goes.</summary>
        void GatherWalkers()
        {
            walkerCount = 0;
            var gs = GuardAI.All;
            for (int i = 0; i < gs.Count; i++)
            {
                var g = gs[i];
                if (!g) continue;
                if (walkerCount == walkers.Count) walkers.Add(new Walker());
                var w = walkers[walkerCount];
                w.Count = g.GetRoute(w.X, w.Z, out w.Speed);
                if (w.Count >= 2) walkerCount++;
            }
        }

        // ------------------------------------------------------------------ the local player works a door

        /// <summary>The player this machine's camera follows (null if there is none), and the backpack that goes with it.</summary>
        static WobblyPlayer LocalPlayer(out Backpack pack)
        {
            pack = null;
            var all = WobblyPlayerRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (!p || !Net.NetPlayer.IsLocalPlayer(p)) continue;
                if (p.TryGetComponent(out Backpack b))
                {
                    if (!b.Viewed) continue;
                    pack = b;
                }
                return p;
            }
            return null;
        }

        /// <summary>Can this player work a door right now? Not in custody, not hiding and not in a menu. (What E does is decided by what the crosshair is on.)</summary>
        static bool CanUse(WobblyPlayer p)
        {
            if (p.InCustody || p.IsHidden) return false;
            return !(Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || EscapeMenu.Open || TestMode.PanelOpen || OutpostUi.Open);
        }

        /// <summary>
        /// The door a ray points at (the crosshair's): the index of the nearest doorway the ray passes through that the player at
        /// <paramref name="body"/> can reach, or -1. <paramref name="enter"/> / <paramref name="exit"/> are where along the ray it goes in and
        /// out; <paramref name="farEnter"/> is where it enters a doorway that is aimed at but too far away (infinity if none).
        /// </summary>
        public int Aimed(Vector3 origin, Vector3 direction, float maxT, Vector3 body, float reach, out float enter, out float exit, out float farEnter)
        {
            int best = -1;
            enter = exit = farEnter = float.PositiveInfinity;
            for (int i = 0; i < doors.Count; i++)
            {
                var d = doors[i];
                float dx = d.Centre.x - body.x, dz = d.Centre.z - body.z;
                if (dx * dx + dz * dz > 144f) continue;                       // a door farther than 12 m is never the one
                if (!d.RayHit(origin, direction, maxT, out float en, out float ex)) continue;
                if (d.UseScore(body, Vector3.zero, reach) == float.MaxValue) { if (en < farEnter) farEnter = en; continue; }
                if (en < enter) { best = i; enter = en; exit = ex; }
            }
            return best;
        }

        /// <summary>The index of the door that a player standing at <paramref name="from"/>, facing <paramref name="facing"/>, would work; -1 if none is in reach.</summary>
        public int Nearest(Vector3 from, Vector3 facing)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < doors.Count; i++)
            {
                float s = doors[i].UseScore(from, facing, UseReach);
                if (s < bestScore) { bestScore = s; best = i; }
            }
            return best;
        }

        void HandleUse()
        {
            // the door under the crosshair (the crosshair works it out once a frame for everybody)
            int index = -1;
            var me = LocalPlayer(out _);
            if (me && CanUse(me))
            {
                var aim = Crosshair.Aim;
                if (aim.Kind == AimKind.Door && aim.Door >= 0 && aim.Door < doors.Count) index = aim.Door;
            }
            promptDoor = index;
            PromptShown = index >= 0;
            if (index >= 0 && Input.GetKeyDown(UseKey))
            {
                var d = doors[index];
                if (d.Locked) OutpostController.Active?.UseLockedDoor(me);     // (a locked door is hacked, not opened)
                else Use(index, !d.Logic.WantOpen);
            }
        }

        /// <summary>
        /// The local player asks for a door to be opened or shut. Offline it is done at once; in a networked run the host checks the
        /// player is at the door and tells everybody.
        /// </summary>
        public void Use(int index, bool open)
        {
            var d = Get(index);
            if (d == null || (open && d.Locked)) return;
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestDoor(index, open);
            else d.SetOpen(open, Time.time);
        }

        /// <summary>Is this door locked (nobody can open it with E until the lock is hacked)?</summary>
        public bool IsLocked(int index)
        {
            var d = Get(index);
            return d != null && d.Locked;
        }

        /// <summary>Host check: is this player close enough to the door to have worked it (the player's view of the world is a moment old).</summary>
        public bool InUseReach(int index, Vector3 playerPosition, float slack)
        {
            var d = Get(index);
            return d != null && d.UseScore(playerPosition, Vector3.zero, UseReach + slack) < float.MaxValue;
        }

        // ------------------------------------------------------------------ what the host announces

        /// <summary>The host said a door is to be open or shut.</summary>
        public void NetSet(int index, bool open)
        {
            var d = Get(index);
            if (d != null) d.SetOpen(open, Time.time);
        }

        /// <summary>Host: the doors that players have opened and nobody has shut (a machine that joined the floor late asks for them).</summary>
        public ushort[] OpenByPlayers()
        {
            var list = new List<ushort>();
            for (int i = 0; i < doors.Count && i <= ushort.MaxValue; i++) if (doors[i].Latched) list.Add((ushort)i);
            return list.ToArray();
        }

        /// <summary>Client: the host's list of the doors players have opened; they are opened here at once, without a sound.</summary>
        public void NetSetStates(ushort[] open)
        {
            if (open == null) return;
            for (int i = 0; i < open.Length; i++)
            {
                var d = Get(open[i]);
                if (d != null && !d.Latched) d.SnapLatched();
            }
        }

        // ------------------------------------------------------------------ prompt

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (promptDoor < 0 || promptDoor >= doors.Count) return;
            var d = doors[promptDoor];
            string text = d.Locked ? (OutpostController.Active != null ? OutpostController.Active.LockPrompt : "Locked")
                        : d.Obstructed(Time.time) ? "Something is in the doorway" : d.Logic.WantOpen ? "[E] Close door" : "[E] Open door";
            HudWidgets.Prompt(text, Screen.height * 0.62f, !text.StartsWith("["));
        }
    }
}
