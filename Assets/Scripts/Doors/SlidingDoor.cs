using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// One automatic double sliding door in a doorway of a partition wall: two leaves that narrow into the jambs, and a collider that
    /// only exists while the door is completely shut. The decisions are made by <see cref="DoorLogic"/>; <see cref="DoorManager"/>
    /// tells it who is near. A plain class, not a component: a floor has dozens of doors and one manager runs them all.
    /// </summary>
    public sealed class SlidingDoor
    {
        /// <summary>Seconds to open / close a narrow doorway and the wide (cross aisle) one.</summary>
        public const float OpenSeconds = 0.25f, WideOpenSeconds = 0.35f, CloseSeconds = 0.45f, WideCloseSeconds = 0.55f;
        /// <summary>How long a door stays open after the last person went away.</summary>
        public const float Linger = 1.1f;
        /// <summary>Thickness of the leaves (the wall is thicker, so they sit a little way into the doorway).</summary>
        public const float LeafThickness = 0.12f;

        const float SenseAlongMargin = 0.7f;     // somebody this far beside the doorway opens it...
        const float SenseAcross = 2.1f;          // ...and so does somebody this far in front of or behind it
        const float WideDoorAbove = 3f;          // doorways wider than this are the 4.8 m cross aisle ones

        public readonly Vector3 Centre;          // middle of the doorway, on the floor
        public readonly bool AlongX;             // the wall (and the doorway) runs along world x
        public readonly float HalfGap, Height;
        public readonly bool Wide;

        public DoorLogic Logic;
        /// <summary>Somebody is near (set by the manager about ten times a second).</summary>
        public bool Occupied;

        readonly Transform leafLow, leafHigh;
        readonly Renderer rendLow, rendHigh;
        readonly BoxCollider blocker;
        readonly Vector3 clearCentre, clearHalf;
        readonly Quaternion clearRotation;
        int plays;                               // makes each opening sound a different variant

        /// <param name="door">The door's own transform: at the doorway on the floor, its x axis along the wall.</param>
        /// <param name="blocker">The collider that closes the doorway (switched on only while the door is shut).</param>
        /// <param name="depth">How thick the wall is.</param>
        public SlidingDoor(Transform door, Transform low, Transform high, BoxCollider blocker, bool alongX, float gapWidth, float height, float depth)
        {
            leafLow = low; leafHigh = high; this.blocker = blocker;
            rendLow = low.GetComponent<Renderer>(); rendHigh = high.GetComponent<Renderer>();
            Centre = door.position; AlongX = alongX; HalfGap = gapWidth * 0.5f; Height = height;
            Wide = gapWidth > WideDoorAbove;

            // The space somebody (or something dropped) would take up in the doorway: just inside the jambs, from just above the floor
            // to just under the beam, and deep enough to hold a body standing at the doorway.
            const float inset = 0.1f, bottom = 0.2f, top = 0.1f, extra = 0.35f;
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

        /// <summary>Does anything need doing this frame? (A shut door with nobody near does not.)</summary>
        public bool NeedsTick => Occupied || Logic.WantOpen || Logic.Moving;

        /// <summary>Is the point within reach of the doorway's sensor?</summary>
        public bool SensorContains(Vector3 p) =>
            DoorGeometry.InSensor(p.x, p.z, Centre.x, Centre.z, AlongX, HalfGap + SenseAlongMargin, SenseAcross);

        public bool AnyoneNear(List<Vector3> actors)
        {
            for (int i = 0; i < actors.Count; i++) if (SensorContains(actors[i])) return true;
            return false;
        }

        /// <summary>Is anything solid in the doorway (a dropped crate, a body)? Only asked when the door is about to close.</summary>
        bool Blocked() =>
            Physics.CheckBox(clearCentre, clearHalf, clearRotation, ~0, QueryTriggerInteraction.Ignore);

        /// <summary>Advances the door by one frame. <paramref name="soundBudget"/> is shared by all doors: each sound uses one.</summary>
        public void Tick(float now, float dt, ref int soundBudget)
        {
            bool blocked = Logic.NeedsBlockCheck(now, Occupied) && Blocked();
            float open = Wide ? WideOpenSeconds : OpenSeconds, close = Wide ? WideCloseSeconds : CloseSeconds;
            var ev = Logic.Step(now, dt, Occupied, blocked, open, close, Linger);
            Apply();
            if (ev != DoorEvent.None && soundBudget > 0) { soundBudget--; PlaySound(ev); }
        }

        /// <summary>Opens the door at once, without a sound (for a door somebody is already standing in).</summary>
        public void SnapOpen()
        {
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
