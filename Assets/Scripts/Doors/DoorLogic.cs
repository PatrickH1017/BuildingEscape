using System;

namespace BuildingEscape
{
    /// <summary>What a door has just started to do (the sound follows it).</summary>
    public enum DoorEvent { None, Opening, Closing }

    /// <summary>
    /// The brain of an automatic sliding door, free of Unity types so it can be tested outside Unity. It opens as soon as somebody is
    /// near, stays open while anybody is near and for a moment after, then closes - unless something is in the doorway, in which case
    /// it stays (or goes back) open. The caller only gives the door a collider once <see cref="IsShut"/>, so a door that is opening,
    /// open or closing can never knock anybody over.
    /// </summary>
    public struct DoorLogic
    {
        /// <summary>A closing door turned round before it is this far shut makes no new opening sound (it never really closed).</summary>
        public const float SilentReopenAbove = 0.5f;
        /// <summary>While closing, in this last stretch the caller must check whether something is in the way (see <see cref="NeedsBlockCheck"/>).</summary>
        public const float CheckZone = 0.3f;
        /// <summary>When something is in the doorway, the door looks again after this many seconds.</summary>
        public const float BlockedRetry = 0.25f;

        /// <summary>0 = shut, 1 = wide open.</summary>
        public float Open;
        /// <summary>The direction it is heading.</summary>
        public bool WantOpen;
        /// <summary>An open door does not start closing before this time.</summary>
        public float CloseAfter;

        public bool IsShut => Open <= 0f && !WantOpen;
        /// <summary>True while the leaves are travelling.</summary>
        public bool Moving => WantOpen ? Open < 1f : Open > 0f;

        /// <summary>
        /// Whether the caller must find out if something is in the doorway before calling <see cref="Step"/>. That is a physics query,
        /// so it is only made when the answer matters: when the door is about to start closing, and in the last stretch of closing.
        /// </summary>
        public bool NeedsBlockCheck(float now, bool occupied)
        {
            if (occupied) return false;
            if (WantOpen) return Open >= 1f && now >= CloseAfter;
            return Open > 0f && Open < CheckZone;
        }

        /// <summary>
        /// Moves the door on by <paramref name="dt"/> seconds and says if it just started to open or close.
        /// <paramref name="occupied"/>: somebody is near the door. <paramref name="blocked"/>: something is in the doorway itself (it
        /// only has to be right when <see cref="NeedsBlockCheck"/> says so). <paramref name="linger"/>: how long it stays open after
        /// the last person went away.
        /// </summary>
        public DoorEvent Step(float now, float dt, bool occupied, bool blocked, float openSeconds, float closeSeconds, float linger)
        {
            var ev = DoorEvent.None;
            if (occupied)
            {
                CloseAfter = now + linger;
                if (!WantOpen)
                {
                    ev = Open < SilentReopenAbove ? DoorEvent.Opening : DoorEvent.None;
                    WantOpen = true;
                }
            }
            else if (WantOpen)
            {
                if (Open >= 1f && now >= CloseAfter)
                {
                    if (blocked) CloseAfter = now + BlockedRetry;            // something is in the doorway: stay open, look again soon
                    else { WantOpen = false; ev = DoorEvent.Closing; }
                }
            }
            else if (blocked && Open < CheckZone)
            {
                WantOpen = true;                                             // something is in the way of the last stretch: back open
                CloseAfter = now + linger;
            }

            if (WantOpen) Open = Math.Min(1f, Open + dt / Math.Max(0.01f, openSeconds));
            else Open = Math.Max(0f, Open - dt / Math.Max(0.01f, closeSeconds));
            return ev;
        }
    }

    /// <summary>The shapes of a door and its sensor.</summary>
    public static class DoorGeometry
    {
        /// <summary>The leaves are a hair narrower than half the doorway so the two halves show a seam.</summary>
        public const float Seam = 0.01f;

        /// <summary>
        /// Width of each of the two leaves at an opening of 0..1. They do not slide sideways (the wall beside a doorway may hold
        /// furniture or end at a corner): each one narrows towards its own jamb, as if it slid into the wall.
        /// </summary>
        public static float LeafWidth(float halfGap, float open) =>
            Math.Max(0f, (halfGap - Seam) * (1f - Math.Min(1f, Math.Max(0f, open))));

        /// <summary>Centre of a leaf along the doorway, counted from the middle: side -1 is the leaf at the low end, +1 the other.</summary>
        public static float LeafCentre(float halfGap, float leafWidth, int side) => side * (halfGap - leafWidth * 0.5f);

        /// <summary>
        /// Is a point within the door's sensor? The sensor is a box centred on the doorway: <paramref name="reachAlong"/> each way along
        /// the wall (counted from the middle) and <paramref name="reachAcross"/> each way through it.
        /// </summary>
        public static bool InSensor(float px, float pz, float cx, float cz, bool alongX, float reachAlong, float reachAcross)
        {
            float dx = Math.Abs(px - cx), dz = Math.Abs(pz - cz);
            float along = alongX ? dx : dz, across = alongX ? dz : dx;
            return along <= reachAlong && across <= reachAcross;
        }
    }
}
