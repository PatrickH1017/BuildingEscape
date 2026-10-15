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
        /// <summary>An officer is about to walk through the door, or is in it (set by the manager about ten times a second): it opens for him.</summary>
        public bool GuardNear;
        /// <summary>A player opened it with E: it stays open until a player closes it again.</summary>
        public bool Latched { get; private set; }

        readonly Transform leafLow, leafHigh;
        readonly Renderer rendLow, rendHigh;
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
            bool held = Latched || GuardNear;
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
            Logic.Open = 1f; Logic.WantOpen = true; Logic.CloseAfter = Time.time + Linger;
            Apply();
        }

        /// <summary>Opens the door at once, without a sound, and holds it open (a machine that learns late that a player opened it).</summary>
        public void SnapLatched()
        {
            Latched = true;
            Logic.Open = 1f; Logic.WantOpen = true; Logic.CloseAfter = Time.time + Linger;
            Apply();
        }

        void Apply()
        {
            float w = DoorGeometry.LeafWidth(HalfGap, Logic.Open);
            SetLeaf(leafLow, rendLow, -1, w);
            SetLeaf(leafHigh, rendHigh, +1, w);
            bool shut = Logic.IsShut;
            if (blocker.enabled != shut) blocker.enabled = shut;
        }

        void SetLeaf(Transform t, Renderer r, int side, float width)
        {
            bool visible = width > 0.02f;
            if (r.enabled != visible) r.enabled = visible;
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
