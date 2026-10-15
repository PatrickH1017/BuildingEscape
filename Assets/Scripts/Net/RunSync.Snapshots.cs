using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The host streams the position / rotation of every moving pickup and guard to the clients about 15 times a
    /// second (only what changed, plus a slow refresh), as unreliable messages; clients glide the puppets to them.
    /// Entry: id(2) kind(1) x y z(6) rotation(4) state(1) callProgress(1) flags(1) speed(1) = 17 bytes.
    /// </summary>
    public partial class RunSync
    {
        const string SnapshotMessage = "be.snap";
        const float SnapshotInterval = 1f / 15f;
        const float RefreshInterval = 2f;
        const int MaxPerMessage = 60;   // 60 * 17 + 2 bytes: well inside one unreliable packet
        const float PosScale = 64f;     // 1/64 m resolution in a short: +-512 m

        /// <summary>Changes every floor; snapshots from another floor are ignored.</summary>
        public static byte Epoch;

        struct Entry
        {
            public ushort id; public byte kind;
            public short x, y, z; public uint rot;
            public byte state, call, flags, speed;
        }

        struct Sent
        {
            public Vector3 pos; public Quaternion rot;
            public byte state, call, flags;
            public float time;
        }

        sealed class Puppet
        {
            public Transform t;
            public Vector3 pos; public Quaternion rot;
            public bool placed;
        }

        readonly Dictionary<int, Sent> sent = new();
        readonly List<Entry> pending = new();
        readonly Dictionary<int, Puppet> puppets = new();
        readonly List<int> deadPuppets = new();
        float nextSnapshot;

        void SpawnSnapshots()
        {
            if (!IsServer) NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessage, OnSnapshot);
        }

        void DespawnSnapshots()
        {
            if (!IsServer && NetworkManager != null && NetworkManager.CustomMessagingManager != null)
                NetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(SnapshotMessage);
        }

        /// <summary>A new floor was built: forget what was sent / received for the old one.</summary>
        public void OnFloorChanged()
        {
            sent.Clear();
            puppets.Clear();
        }

        // ------------------------------------------------------------------ host: send

        void HostSendSnapshots()
        {
            float now = Time.unscaledTime;
            if (now < nextSnapshot) return;
            nextSnapshot = now + SnapshotInterval;
            if (NetworkManager.ConnectedClientsIds.Count <= 1) return; // nobody to tell
            var tm = TowerManager.Instance;
            if (tm != null && tm.Loading) return;

            pending.Clear();
            foreach (var kv in NetIds.All)
            {
                var c = kv.Value;
                if (!c) continue;
                byte kind = 0, state = 0, call = 0, flags = 0, speed = 0;
                if (c is Pickup p)
                {
                    if (!p.isActiveAndEnabled) continue; // in a backpack
                }
                else if (c is GuardAI g)
                {
                    kind = 1;
                    state = (byte)g.State;
                    call = (byte)Mathf.RoundToInt(Mathf.Clamp01(g.CallProgress) * 255f);
                    flags = (byte)((g.Dead ? 1 : 0) | (g.IsCalling ? 2 : 0));
                    speed = (byte)Mathf.Clamp(Mathf.RoundToInt(g.CurrentSpeed * 20f), 0, 255);
                }
                else continue;

                var pos = c.transform.position;
                var rot = c.transform.rotation;
                if (sent.TryGetValue(kv.Key, out var s))
                {
                    bool changed = (pos - s.pos).sqrMagnitude > 0.0004f || Quaternion.Angle(rot, s.rot) > 0.7f
                                   || state != s.state || flags != s.flags || call != s.call;
                    if (!changed && now - s.time < RefreshInterval) continue;
                }
                sent[kv.Key] = new Sent { pos = pos, rot = rot, state = state, call = call, flags = flags, time = now };
                pending.Add(new Entry
                {
                    id = (ushort)kv.Key, kind = kind,
                    x = Quant(pos.x), y = Quant(pos.y), z = Quant(pos.z),
                    rot = PackRotation(rot), state = state, call = call, flags = flags, speed = speed,
                });
            }

            for (int i = 0; i < pending.Count; i += MaxPerMessage)
            {
                int n = Mathf.Min(MaxPerMessage, pending.Count - i);
                using var w = new FastBufferWriter(2 + n * 17 + 8, Allocator.Temp);
                w.WriteValueSafe(Epoch);
                w.WriteValueSafe((byte)n);
                for (int j = 0; j < n; j++)
                {
                    var e = pending[i + j];
                    w.WriteValueSafe(e.id); w.WriteValueSafe(e.kind);
                    w.WriteValueSafe(e.x); w.WriteValueSafe(e.y); w.WriteValueSafe(e.z);
                    w.WriteValueSafe(e.rot);
                    w.WriteValueSafe(e.state); w.WriteValueSafe(e.call); w.WriteValueSafe(e.flags); w.WriteValueSafe(e.speed);
                }
                NetworkManager.CustomMessagingManager.SendNamedMessageToAll(SnapshotMessage, w, NetworkDelivery.UnreliableSequenced);
            }
        }

        // ------------------------------------------------------------------ client: receive + smooth

        void OnSnapshot(ulong sender, FastBufferReader r)
        {
            r.ReadValueSafe(out byte epoch);
            r.ReadValueSafe(out byte n);
            bool current = epoch == Epoch;
            for (int i = 0; i < n; i++)
            {
                r.ReadValueSafe(out ushort id); r.ReadValueSafe(out byte kind);
                r.ReadValueSafe(out short x); r.ReadValueSafe(out short y); r.ReadValueSafe(out short z);
                r.ReadValueSafe(out uint rot);
                r.ReadValueSafe(out byte state); r.ReadValueSafe(out byte call); r.ReadValueSafe(out byte flags); r.ReadValueSafe(out byte speed);
                if (!current) continue;

                var c = NetIds.Get<Component>(id);
                if (c == null) continue;
                if (!puppets.TryGetValue(id, out var pp)) puppets[id] = pp = new Puppet { t = c.transform };
                pp.pos = new Vector3(x, y, z) / PosScale;
                pp.rot = UnpackRotation(rot);
                if (!pp.placed)
                {
                    pp.placed = true;
                    pp.t.SetPositionAndRotation(pp.pos, pp.rot);
                }
                if (c is GuardAI g) g.NetApply((GuardState)state, (flags & 1) != 0, (flags & 2) != 0, call / 255f, speed / 20f);
            }
        }

        // A puppet this close to where the host says it is counts as arrived: it is left alone (writing a transform every frame for
        // every loot item on the floor makes the physics engine treat all of them as moving).
        const float SettledDistanceSq = 0.000004f;    // 2 mm
        const float SettledRotationDot = 0.999999f;   // about 0.16 degrees

        void ClientSmoothPuppets()
        {
            if (puppets.Count == 0) return;
            float k = 1f - Mathf.Exp(-18f * Time.deltaTime);
            deadPuppets.Clear();
            foreach (var kv in puppets)
            {
                var pp = kv.Value;
                var t = pp.t;
                if (!t) { deadPuppets.Add(kv.Key); continue; }
                if (!t.gameObject.activeInHierarchy) { pp.placed = false; continue; } // in a backpack: re-place when it comes out
                t.GetPositionAndRotation(out var pos, out var rot);
                if ((pos - pp.pos).sqrMagnitude < SettledDistanceSq && Mathf.Abs(Quaternion.Dot(rot, pp.rot)) > SettledRotationDot) continue;
                t.SetPositionAndRotation(Vector3.Lerp(pos, pp.pos, k), Quaternion.Slerp(rot, pp.rot, k));
            }
            foreach (var id in deadPuppets) puppets.Remove(id);
        }

        // ------------------------------------------------------------------ packing

        static short Quant(float v) => (short)Mathf.Clamp(Mathf.RoundToInt(v * PosScale), short.MinValue, short.MaxValue);

        static readonly float[] q4 = new float[4];

        /// <summary>Smallest-three: drop the largest component (2 bits say which), keep the other three in 10 bits each.</summary>
        static uint PackRotation(Quaternion q)
        {
            q4[0] = q.x; q4[1] = q.y; q4[2] = q.z; q4[3] = q.w;
            int big = 0; float max = 0f;
            for (int i = 0; i < 4; i++)
                if (Mathf.Abs(q4[i]) > max) { max = Mathf.Abs(q4[i]); big = i; }
            float sign = q4[big] < 0f ? -1f : 1f;
            uint packed = (uint)big;
            int shift = 2;
            for (int i = 0; i < 4; i++)
            {
                if (i == big) continue;
                float v = Mathf.Clamp(q4[i] * sign / 0.70710678f, -1f, 1f) * 0.5f + 0.5f;
                packed |= (uint)Mathf.RoundToInt(v * 1023f) << shift;
                shift += 10;
            }
            return packed;
        }

        static Quaternion UnpackRotation(uint p)
        {
            int big = (int)(p & 3u);
            int shift = 2;
            float sum = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (i == big) continue;
                float v = (((p >> shift) & 1023u) / 1023f * 2f - 1f) * 0.70710678f;
                q4[i] = v;
                sum += v * v;
                shift += 10;
            }
            q4[big] = Mathf.Sqrt(Mathf.Max(0f, 1f - sum));
            return new Quaternion(q4[0], q4[1], q4[2], q4[3]);
        }
    }
}
