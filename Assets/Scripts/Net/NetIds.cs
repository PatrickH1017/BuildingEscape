using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Numbers every pickup and guard so the host can talk about them. The floor generator creates the same
    /// things in the same order on every machine, so ids handed out in creation order already match.
    /// Things created later (shop items, backup guards) get their id from the host and are made with
    /// <see cref="PendingId"/> set. The registry is cleared whenever a new floor is generated.
    /// </summary>
    public static class NetIds
    {
        public const int None = -1;

        /// <summary>If set, the next registered thing takes this id (a host-announced spawn).</summary>
        public static int PendingId = None;

        static readonly Dictionary<int, Component> map = new();
        static int next;

        public static int Count => next;

        public static int Register(Component c)
        {
            int id = PendingId >= 0 ? PendingId : next;
            PendingId = None;
            map[id] = c;
            if (id >= next) next = id + 1;
            return id;
        }

        public static void Unregister(int id, Component c)
        {
            if (id >= 0 && map.TryGetValue(id, out var cur) && cur == c) map.Remove(id);
        }

        public static T Get<T>(int id) where T : Component
        {
            return map.TryGetValue(id, out var c) && c ? c as T : null;
        }

        public static IEnumerable<KeyValuePair<int, Component>> All => map;

        /// <summary>A new floor is being built: forget everything.</summary>
        public static void Reset()
        {
            map.Clear();
            next = 0;
            PendingId = None;
        }

        /// <summary>Hash of what exists, in order. Host and clients compare it after a floor load to catch a desync.</summary>
        public static int Checksum()
        {
            unchecked
            {
                int h = map.Count;
                foreach (var kv in map) h += (kv.Key + 1) * (kv.Value is GuardAI ? 3 : 5); // order independent
                return h;
            }
        }
    }
}