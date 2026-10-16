using System.Collections.Generic;

namespace BuildingEscape.Net
{
    /// <summary>
    /// A token bucket per sender: each call costs a token, the bucket refills steadily, and a sender that spends faster than that
    /// is refused. The host uses it so one client cannot flood it (or, through it, the other players) with requests or voice packets.
    /// Pure C#: the clock is passed in, so it is tested without Unity.
    /// </summary>
    public sealed class RateLimiter
    {
        struct Bucket { public float tokens, last; }

        readonly Dictionary<ulong, Bucket> buckets = new();
        readonly float capacity, perSecond;

        /// <summary>How many calls were refused so far (a counter for diagnostics).</summary>
        public int Refused { get; private set; }

        /// <param name="capacity">The burst a sender may spend at once.</param>
        /// <param name="perSecond">How many calls per second a sender may keep up.</param>
        public RateLimiter(float capacity, float perSecond)
        {
            this.capacity = capacity;
            this.perSecond = perSecond;
        }

        /// <summary>True if <paramref name="who"/> may do this now (and it is counted); false if they are going too fast.</summary>
        public bool Allow(ulong who, float now, float cost = 1f)
        {
            if (!buckets.TryGetValue(who, out var b)) b = new Bucket { tokens = capacity, last = now };
            float elapsed = now - b.last;
            if (elapsed > 0f) b.tokens = System.Math.Min(capacity, b.tokens + elapsed * perSecond);
            b.last = now;
            bool ok = b.tokens >= cost;
            if (ok) b.tokens -= cost; else Refused++;
            buckets[who] = b;
            return ok;
        }

        public void Forget(ulong who) => buckets.Remove(who);
    }
}