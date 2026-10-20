using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// An officer as the doors see him: the route he is walking (flat points: where he stands, then the corners of his path, or a
    /// straight line ahead of him when his path is not known) and how fast he goes (m/s). One is kept per officer and refilled.
    /// </summary>
    public sealed class Walker
    {
        public const int MaxPoints = 12;
        public readonly float[] X = new float[MaxPoints], Z = new float[MaxPoints];
        public int Count;
        public float Speed;
    }

    /// <summary>
    /// One double sliding door in a doorway of a partition wall: two leaves that narrow into the jambs, and a collider that only
    /// exists while the door is completely shut. A player opens it by pressing E at it and closes it by pressing E again (it stays
    /// open until then). An officer opens it by himself when he is about to walk through it (on patrol, going to look at something or
    /// chasing) and it closes behind him; walking past a door, or standing about next to it, opens nothing
    /// (<see cref="AnyoneWalkingIn"/>). The decisions are made by <see cref="DoorLogic"/>; <see cref="DoorManager"/> tells it what the
    /// players asked and where the officers are heading. A plain class, not a component: a floor has dozens of doors and one manager
    /// runs them all.
    /// </summary>
    public sealed class SlidingDoor
    {
        /// <summary>Seconds to open / close a narrow doorway and the wide (cross aisle) one.</summary>
        public const float OpenSeconds = 0.25f, WideOpenSeconds = 0.35f, CloseSeconds = 0.45f, WideCloseSeconds = 0.55f;
        /// <summary>How long a door an officer opened stays open after he went away.</summary>
        public const float Linger = 1.1f;
        /// <summary>Thickness of the leaves (the wall is thicker, so they sit a little way into the doorway).</summary>
        public const float LeafThickness = 0.12f;

        /// <summary>
        /// How far ahead, in seconds of walking, a door looks for an officer who is about to go through it. The door needs up to 0.35 s
        /// to open, the manager looks every 0.1 s, and a guard's body (0.5 m across) reaches the leaves before his middle does.
        /// </summary>
        public const float LookAheadSeconds = 0.9f;
        const float WalkSlack = 0.25f;           // a walk into the doorway a little outside the gap's width counts too
        /// <summary>
        /// How close to the wall's middle a walk has to come, in front of or behind it, to count as going into the doorway. A guard (0.5 m
        /// across) cannot walk closer to a wall than 0.6 m, so one who walks along the wall past a doorway never gets in.
        /// </summary>
        public const float WalkZone = 0.5f;
        const float WideDoorAbove = 3f;          // doorways wider than this are the 4.8 m cross aisle ones

        public readonly Vector3 Centre;          // middle of the doorway, on the floor
        public readonly bool AlongX;             // the wall (and the doorway) runs along world x
        public readonly float HalfGap, Height;
        /// <summary>How thick the wall is (and so how deep the doorway).</summary>
        public readonly float Depth;
        public readonly bool Wide;

        public DoorLogic Logic;
        /// <summary>
        /// A locked door (the guard outpost's) is not worked with E - E starts the lock hack instead - until somebody opens the lock. Officers
        /// still open it for themselves (it is their outpost) and it shuts and stays locked behind them.
        /// </summary>
        public bool Locked;
        /// <summary>A door that never opens, for anybody (a restroom: "Players should not be allowed to enter the restroom"): <see cref="Locked"/> as well, but no lock to hack and officers do not open it either.</summary>
        public bool Permanent;
        /// <summary>Locked from the security computer's Hacked terminal: shut at once and nobody (players or officers) opens it until the computer unlocks it.</summary>
        public bool ComputerLocked { get; private set; }

        public void SetComputerLocked(bool locked, float now)
        {
            if (ComputerLocked == locked) return;
            ComputerLocked = locked;
            if (locked) { Latched = false; GuardNear = false; Logic.CloseNow(now); }
        }
        /// <summary>An officer is about to walk through the door, or is in it (set by the manager about ten times a second): it opens for him.</summary>
        public bool GuardNear;
        /// <summary>A player opened it with E: it stays open until a player closes it again.</summary>
        public bool Latched { get; private set; }

        readonly Transform leafLow, leafHigh;
        /// <summary>The two leaves (the builder hangs the outpost door's panes of glass on them, see <see cref="AddDecor"/>).</summary>
        public Transform LowLeaf => leafLow;
        public Transform HighLeaf => leafHigh;
        readonly Renderer rendLow, rendHigh;
        // things hung on a leaf (the panes of glass in the outpost's door): drawn exactly while the leaf is
        readonly List<Renderer> decorLow = new List<Renderer>(), decorHigh = new List<Renderer>();
        readonly BoxCollider blocker;
        readonly Vector3 clearCentre, clearHalf;
        readonly Quaternion clearRotation;
        int plays;                               // makes each opening sound a different variant
        float obstructedUntil;                   // something was in the doorway when the door last looked

        /// <param name="door">The door's own transform: at the doorway on the floor, its x axis along the wall.</param>
        /// <param name="blocker">The collider that closes the doorway (switched on only while the door is shut).</param>
        /// <param name="depth">How thick the wall is.</param>
        public SlidingDoor(Transform door, Transform low, Transform high, BoxCollider blocker, bool alongX, float gapWidth, float height, float depth)
        {
            leafLow = low; leafHigh = high; this.blocker = blocker;
            rendLow = low.GetComponent<Renderer>(); rendHigh = high.GetComponent<Renderer>();
            Centre = door.position; AlongX = alongX; HalfGap = gapWidth * 0.5f; Height = height; Depth = depth;
            Wide = gapWidth > WideDoorAbove;

            // The space the shut door itself would take up in the doorway: just inside the jambs, from just above the floor to just
            // under the beam, and exactly as deep as the wall (plus a hair for the physics skin). A door only refuses to close on what it
            // would really overlap: somebody standing beside the door, even right up against it, does not hold it open.
            const float inset = 0.1f, bottom = 0.2f, top = 0.1f, extra = 0.03f;
            float halfHeight = (height - bottom - top) * 0.5f;
            clearHalf = new Vector3(HalfGap - inset, halfHeight, depth * 0.5f + extra);
            clearCentre = new Vector3(Centre.x, bottom + halfHeight, Centre.z);
            clearRotation = door.rotation;

            Logic = new DoorLogic();             // starts shut
            Apply();
        }

        /// <summary>Hangs something on a leaf (<paramref name="side"/> -1: the low leaf, +1: the high one): it shows and hides with the leaf.</summary>
        public void AddDecor(int side, Renderer r)
        {
            if (!r) return;
            (side < 0 ? decorLow : decorHigh).Add(r);
            r.enabled = (side < 0 ? rendLow : rendHigh).enabled;
        }

        // ---- how the leaves leave the doorway: sliding into the wall beside it (see DoorSlide) instead of narrowing into the jambs
        bool slide;
        float lowTravel, highTravel, lowDepth, highDepth;

        /// <summary>
        /// The leaves keep their size and slide sideways out of the doorway, <paramref name="plan"/> says how far and where (inside the wall, or in front of a glass front). A plan that says
        /// "no" leaves the door as it is: its leaves narrow into the jambs.
        /// </summary>
        public void SetSlide(DoorSlide.Plan plan)
        {
            if (!plan.slide) return;
            slide = true;
            lowTravel = plan.lowTravel; highTravel = plan.highTravel;
            float lane = Depth * 0.5f + LeafThickness * 0.5f + 0.012f;           // (in front of the wall's face, not touching it)
            lowDepth = plan.lowLane * lane; highDepth = plan.highLane * lane;
            Apply();
        }

        /// <summary>Does this door slide into the wall?</summary>
        public bool Slides => slide;

        // ---- a door whose walls have no room for its leaves swings open instead (hinged at the jambs)
        bool swing;
        int swingSign = 1;
        const float SwingDegrees = 92f;

        /// <summary>
        /// The leaves are hinged at the jambs and swing open towards the side <paramref name="sign"/> (+1 / -1 across the wall, in the door's own frame): for a door that cannot slide into the
        /// wall beside it (<see cref="DoorSlide"/>). The doorway's approach is kept clear of furniture on both sides, so the leaves have room.
        /// </summary>
        public void SetSwing(int sign)
        {
            swing = true;
            swingSign = sign >= 0 ? 1 : -1;
            Apply();
        }

        /// <summary>Does this door swing open (hinged leaves)?</summary>
        public bool Swings => swing;

        public bool IsShut => Logic.IsShut;
        /// <summary>The doorway can be walked through exactly when this is false.</summary>
        public bool BlockerEnabled => blocker.enabled;

        /// <summary>
        /// Does anything need doing this frame? A shut door nobody is at does not, and neither does a door that a player opened and
        /// that is wide open: it stays that way until somebody closes it.
        /// </summary>
        public bool NeedsTick
        {
            get
            {
                if (Latched && Logic.WantOpen && Logic.Open >= 1f) return false;
                return Latched || GuardNear || Logic.WantOpen || Logic.Moving;
            }
        }

        /// <summary>Is anyone standing close enough at the start of a floor that he must not be left inside a shut door? (Players; the old, tighter sensor.)</summary>
        public bool AnyoneAtStart(List<Vector3> players)
        {
            for (int i = 0; i < players.Count; i++)
                if (DoorGeometry.InSensor(players[i].x, players[i].z, Centre.x, Centre.z, AlongX, HalfGap + 0.7f, 2.1f)) return true;
            return false;
        }

        /// <summary>
        /// Is an officer about to walk through this doorway: does his route, within the next <see cref="LookAheadSeconds"/> of walking, go
        /// into the doorway's zone (<see cref="WalkZone"/>)? Officers who walk past, away, or stand still do not count.
        /// </summary>
        public bool AnyoneWalkingIn(List<Walker> walkers, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var w = walkers[i];
                if (DoorGeometry.RouteEnters(w.X, w.Z, w.Count, w.Speed * LookAheadSeconds, Centre.x, Centre.z, AlongX, HalfGap + WalkSlack, WalkZone)) return true;
            }
            return false;
        }

        /// <summary>Is anyone standing in the doorway itself (within the wall's thickness and a little more)?</summary>
        public bool AnyoneInside(List<Vector3> actors)
        {
            for (int i = 0; i < actors.Count; i++)
                if (DoorGeometry.InSensor(actors[i].x, actors[i].z, Centre.x, Centre.z, AlongX, HalfGap, Depth * 0.5f + 0.4f)) return true;
            return false;
        }

        /// <summary>
        /// How good a door this is for a player standing at <paramref name="from"/> and facing <paramref name="facing"/> who presses E
        /// (lower is better, <see cref="float.MaxValue"/>: out of reach).
        /// </summary>
        public float UseScore(Vector3 from, Vector3 facing, float reach) =>
            DoorGeometry.UseScore(from.x, from.z, facing.x, facing.z, Centre.x, Centre.z, AlongX, HalfGap, reach);

        /// <summary>
        /// Does the ray (from <paramref name="origin"/> along <paramref name="direction"/>, up to <paramref name="maxT"/> metres) pass through the
        /// doorway? That is how a player points at a door with the crosshair: open or shut, he aims at where the door is.
        /// </summary>
        public bool RayHit(Vector3 origin, Vector3 direction, float maxT, out float tEnter, out float tExit) =>
            AimMath.RayDoorway(origin.x, origin.y, origin.z, direction.x, direction.y, direction.z,
                               Centre.x, Centre.z, AlongX, HalfGap, Height, Depth, maxT, out tEnter, out tExit);

        /// <summary>
        /// A player asked for the door to be open (<paramref name="open"/>) or shut. Open: it opens and stays open. Shut: it starts
        /// closing at once, unless an officer is at it or something is in the doorway (then it stays open and closes by itself later).
        /// </summary>
        public void SetOpen(bool open, float now)
        {
            if (open && ComputerLocked) return;
            Latched = open;
            if (!open) Logic.CloseNow(now);
        }

        /// <summary>Did the door recently find something in the doorway that stops it closing? (The prompt says so.)</summary>
        public bool Obstructed(float now) => now < obstructedUntil;

        /// <summary>Is anything solid in the doorway (a dropped crate, a body)? Only asked when the door is about to close.</summary>
        bool Blocked() =>
            Physics.CheckBox(clearCentre, clearHalf, clearRotation, ~0, QueryTriggerInteraction.Ignore);

        /// <summary>Advances the door by one frame. <paramref name="soundBudget"/> is shared by all doors: each sound uses one.</summary>
        public void Tick(float now, float dt, ref int soundBudget)
        {
            bool held = !Permanent && !ComputerLocked && (Latched || GuardNear);
            bool blocked = Logic.NeedsBlockCheck(now, held) && Blocked();
            if (blocked) obstructedUntil = now + 0.6f;
            float open = Wide ? WideOpenSeconds : OpenSeconds, close = Wide ? WideCloseSeconds : CloseSeconds;
            var ev = Logic.Step(now, dt, held, blocked, open, close, Linger);
            Apply();
            if (ev != DoorEvent.None && soundBudget > 0) { soundBudget--; PlaySound(ev); }
        }

        /// <summary>
        /// Opens the door at once, without a sound (for a door somebody is already standing in when the floor starts). It is not held
        /// open: it closes after <see cref="Linger"/>, as soon as nothing is in the doorway.
        /// </summary>
        public void SnapOpen()
        {
            if (Permanent) return;
            Logic.Open = 1f; Logic.WantOpen = true; Logic.CloseAfter = Time.time + Linger;
            Apply();
        }

        /// <summary>Opens the door at once, without a sound, and holds it open (a machine that learns late that a player opened it).</summary>
        public void SnapLatched()
        {
            if (Permanent || ComputerLocked) return;
            Latched = true;
            Logic.Open = 1f; Logic.WantOpen = true; Logic.CloseAfter = Time.time + Linger;
            Apply();
        }

        void Apply()
        {
            if (slide) ApplySlide();
            else if (swing) ApplySwing();
            else
            {
                float w = DoorGeometry.LeafWidth(HalfGap, Logic.Open);
                SetLeaf(leafLow, rendLow, decorLow, -1, w);
                SetLeaf(leafHigh, rendHigh, decorHigh, +1, w);
            }
            bool shut = Logic.IsShut;
            if (blocker.enabled != shut) blocker.enabled = shut;
        }

        /// <summary>Each leaf slides out of the doorway as far as its plan says (eased); one that goes in front of a glass front steps out of the doorway's line first. A leaf that is all the way inside the wall is not drawn.</summary>
        void ApplySlide()
        {
            float o = Mathf.Clamp01(Logic.Open), e = o * o * (3f - 2f * o), stepOut = Mathf.Clamp01(o / 0.25f);
            float w = HalfGap - DoorGeometry.Seam;
            SlideLeaf(leafLow, rendLow, decorLow, -1, w, lowTravel * e, lowDepth * stepOut, o >= 1f && lowDepth == 0f && lowTravel >= w - 0.01f);
            SlideLeaf(leafHigh, rendHigh, decorHigh, +1, w, highTravel * e, highDepth * stepOut, o >= 1f && highDepth == 0f && highTravel >= w - 0.01f);
        }

        /// <summary>Both leaves turn about their hinges at the jambs (eased, up to 92 degrees). The hinge moves half a leaf's thickness inwards as the door opens, so an open leaf lies flush against the side of the doorway.</summary>
        void ApplySwing()
        {
            float o = Mathf.Clamp01(Logic.Open), e = o * o * (3f - 2f * o);
            float w = HalfGap - DoorGeometry.Seam;
            float hingeX = HalfGap - LeafThickness * 0.5f * e;
            float angle = SwingDegrees * e * swingSign;
            SwingLeaf(leafLow, rendLow, decorLow, -1, w, hingeX, angle);
            SwingLeaf(leafHigh, rendHigh, decorHigh, +1, w, hingeX, angle);
        }

        void SwingLeaf(Transform t, Renderer r, List<Renderer> decor, int side, float width, float hingeX, float angle)
        {
            if (!r.enabled)
            {
                r.enabled = true;
                for (int i = 0; i < decor.Count; i++) if (decor[i]) decor[i].enabled = true;
            }
            // the low leaf is hinged at -x and reaches towards +x: turned by -angle about y it swings towards +z; the high leaf is hinged at +x and reaches towards -x: +angle swings it towards +z
            var q = Quaternion.Euler(0f, side < 0 ? -angle : angle, 0f);
            var hinge = new Vector3(side * hingeX, Height * 0.5f, 0f);
            t.localRotation = q;
            t.localPosition = hinge + q * new Vector3(-side * width * 0.5f, 0f, 0f);
            t.localScale = new Vector3(width, Height, LeafThickness);
        }

        void SlideLeaf(Transform t, Renderer r, List<Renderer> decor, int side, float width, float moved, float depth, bool insideWall)
        {
            bool visible = !insideWall;
            if (r.enabled != visible)
            {
                r.enabled = visible;
                for (int i = 0; i < decor.Count; i++) if (decor[i]) decor[i].enabled = visible;
            }
            if (!visible) return;
            t.localPosition = new Vector3(side * ((HalfGap + DoorGeometry.Seam) * 0.5f + moved), Height * 0.5f, depth);
            t.localScale = new Vector3(width, Height, LeafThickness);
        }

        void SetLeaf(Transform t, Renderer r, List<Renderer> decor, int side, float width)
        {
            bool visible = width > 0.02f;
            if (r.enabled != visible)
            {
                r.enabled = visible;
                for (int i = 0; i < decor.Count; i++) if (decor[i]) decor[i].enabled = visible;
            }
            if (!visible) return;
            t.localPosition = new Vector3(DoorGeometry.LeafCentre(HalfGap, width, side), Height * 0.5f, 0f);
            t.localScale = new Vector3(width, Height, LeafThickness);
        }

        void PlaySound(DoorEvent ev)
        {
            // every door has its own pitch and starts at its own variant, so a row of doors does not sound like one sample
            int seed = Mathf.Abs(Mathf.RoundToInt(Centre.x * 31f + Centre.z * 17f)) + plays++;
            float pitch = (Wide ? 0.82f : 1f) * (0.94f + 0.012f * (seed * 7 % 11));
            GameAudio.PlaySound(ev == DoorEvent.Opening ? SoundId.DoorOpen : SoundId.DoorClose,
                                Centre + Vector3.up * 1.2f, Wide ? 0.85f : 0.65f, pitch, true, 24f, 2.5f, seed);
        }
    }
}
