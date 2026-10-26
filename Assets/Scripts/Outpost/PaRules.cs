using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The hallway loudspeakers (the owner, 2026-10-18, from the first two playtests: "There should be loud speakers throughout the hallways that there is a microphone connected to, which a player can speak
    /// into (by pressing V) if they sign into the outpost computer", "The microphones should be present on the Nav map and the signed-in player should be able to click to deactivate certain loud speakers so
    /// that the guards don't hear the loud speakers", "If a guard is in range of the loud speaker noise they should rush to the outpost"). The pure rules: where the speakers hang, which are switched on (a
    /// bit each in a <c>ulong</c>), who is in earshot, how loud the PA is for a listener.
    /// </summary>
    public static class PaRules
    {
        /// <summary>One bit each in a <c>ulong</c>.</summary>
        public const int MaxSpeakers = 48;
        /// <summary>Metres of hallway to a speaker, and the shortest piece of hallway that gets one.</summary>
        public const float Spacing = 13f, ShortestHallway = 6f;
        /// <summary>An officer this close to a speaker that is on hears the PA and rushes to the outpost; how long he keeps rushing after the last call (s).</summary>
        public const float HearRange = 22f, AlertSeconds = 12f;
        /// <summary>How high the speakers hang (m).</summary>
        public const float Height = 3.2f;

        /// <summary>
        /// The round ceiling speaker (the owner, 2026-10-24: "they should just be attached to the ceiling like the attached image reference"): a flat disc <see cref="DiscDiameter"/> across and <see cref="DiscHeight"/> thick whose top is <see cref="CeilingGap"/> under the ceiling, with a
        /// darker grille disc under it. (<see cref="Height"/> is the old hanging height; the speakers keep their places for the PA, only the model sits on the ceiling.)
        /// </summary>
        public const float DiscDiameter = 0.40f, DiscHeight = 0.05f, GrilleDiameter = 0.30f, GrilleHeight = 0.012f, CeilingGap = 0.03f;

        /// <summary>
        /// Where a hallway's speaker is hung: at <paramref name="centre"/> (the PA's place), or the nearest spot in the hallway that no light panel (<paramref name="panels"/>, their frames) covers: a speaker on a light is wrong (the owner, 2026-10-24). The speaker moves along the hallway
        /// and across it, the smallest move first; where nothing is free it stays at the centre.
        /// </summary>
        public static Vector2 MountSpot(Vector2 centre, Rc hall, IReadOnlyList<Rc> panels)
        {
            float radius = DiscDiameter * 0.5f + 0.06f;
            bool alongX = hall.W >= hall.D;
            float half = (alongX ? hall.D : hall.W) * 0.5f;
            int across = (int)(Mathf.Max(0f, half - 0.45f) / 0.4f);
            var tries = new List<Vector2>();
            for (int a = -7; a <= 7; a++)
                for (int c = -across; c <= across; c++) tries.Add(new Vector2(a * 0.6f, c * 0.4f));
            tries.Sort((p, q) => (p.x * p.x + p.y * p.y).CompareTo(q.x * q.x + q.y * q.y));
            foreach (var t in tries)
            {
                var at = alongX ? new Vector2(centre.x + t.x, centre.y + t.y) : new Vector2(centre.x + t.y, centre.y + t.x);
                if (at.x < hall.x0 + 0.3f || at.x > hall.x1 - 0.3f || at.y < hall.z0 + 0.3f || at.y > hall.z1 - 0.3f) continue;
                if (!Covered(at, radius, panels)) return at;
            }
            return centre;
        }

        static bool Covered(Vector2 at, float radius, IReadOnlyList<Rc> panels)
        {
            if (panels == null) return false;
            var disc = new Rc(at.x - radius, at.y - radius, at.x + radius, at.y + radius);
            for (int i = 0; i < panels.Count; i++) if (disc.Overlaps(panels[i])) return true;
            return false;
        }
        /// <summary>How far from a speaker the PA is at full volume, and where it is silent (m).</summary>
        public const float FullRange = 6f, SilentRange = 28f;
        public const ulong AllOn = ulong.MaxValue;

        /// <summary>
        /// Where the speakers hang: along the middle of every corridor that is long enough, one every <see cref="Spacing"/> metres, the first and the last half a spacing from the ends; the same for the same
        /// corridors. More than <see cref="MaxSpeakers"/>: every other one goes until it fits. x and z of each.
        /// </summary>
        public static List<Vector2> Plan(IList<Rc> corridors)
        {
            var list = new List<Vector2>();
            for (int c = 0; c < corridors.Count; c++)
            {
                var r = corridors[c];
                bool alongX = r.W >= r.D;
                float len = alongX ? r.W : r.D;
                if (len < ShortestHallway) continue;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / Spacing));
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 0.5f) * len / n;
                    list.Add(alongX ? new Vector2(r.x0 + t, r.Cz) : new Vector2(r.Cx, r.z0 + t));
                }
            }
            while (list.Count > MaxSpeakers)
            {
                var thinned = new List<Vector2>();
                for (int i = 0; i < list.Count; i += 2) thinned.Add(list[i]);
                list = thinned;
            }
            return list;
        }

        /// <summary>How loud the PA is next to a speaker compared with a voice heard in the air.</summary>
        public const float PaVolume = 0.9f;

        /// <summary>What a listener hears of a voice: the voice in the air, or the PA out of the nearest speaker that is on, whichever is louder.</summary>
        public static float Mix(float airGain, float paGain) => Mathf.Max(airGain, paGain * PaVolume);

        public static ulong Bit(int i) => i >= 0 && i < MaxSpeakers ? 1UL << i : 0UL;
        public static bool IsOn(ulong mask, int i) => (mask & Bit(i)) != 0UL;
        public static ulong Toggle(ulong mask, int i) => mask ^ Bit(i);
        /// <summary>The mask with the first <paramref name="count"/> speakers on.</summary>
        public static ulong MaskOf(int count) => count <= 0 ? 0UL : count >= MaxSpeakers ? (1UL << MaxSpeakers) - 1UL : (1UL << count) - 1UL;
        public static int CountOn(ulong mask, int count) { int n = 0; for (int i = 0; i < count && i < MaxSpeakers; i++) if (IsOn(mask, i)) n++; return n; }

        /// <summary>Is a point within <paramref name="range"/> of a speaker that is switched on?</summary>
        public static bool InEarshot(Vector3 p, IReadOnlyList<Vector2> speakers, ulong mask, float range = HearRange)
        {
            float r2 = range * range;
            for (int i = 0; i < speakers.Count; i++)
            {
                if (!IsOn(mask, i)) continue;
                float dx = p.x - speakers[i].x, dz = p.z - speakers[i].y;
                if (dx * dx + dz * dz <= r2) return true;
            }
            return false;
        }

        /// <summary>How loud the PA is for something at <paramref name="distance"/> metres from a speaker: full to <see cref="FullRange"/>, then falling away to nothing at <see cref="SilentRange"/> (the voice's own curve).</summary>
        public static float GainAt(float distance)
        {
            if (distance <= FullRange) return 1f;
            if (distance >= SilentRange) return 0f;
            float t = (distance - FullRange) / (SilentRange - FullRange);
            return (1f - t) * (1f - t);
        }

        /// <summary>The loudest the PA is at a point (the nearest speaker that is on), and which speaker that is (-1 when none is on).</summary>
        public static float GainAt(Vector3 p, IReadOnlyList<Vector2> speakers, ulong mask, out int nearest)
        {
            float best = -1f, bestD = float.MaxValue; nearest = -1;
            for (int i = 0; i < speakers.Count; i++)
            {
                if (!IsOn(mask, i)) continue;
                float dx = p.x - speakers[i].x, dz = p.z - speakers[i].y;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < bestD) { bestD = d; nearest = i; }
            }
            if (nearest >= 0) best = GainAt(bestD);
            return Mathf.Max(0f, best);
        }
    }

    /// <summary>The loudspeakers of the floor on show and what is switched on (every machine has its own copy; the host publishes the changes, see <c>RunSync.Pa</c>).</summary>
    public static class PaState
    {
        /// <summary>The floor's speakers (x, z); the index is the bit in <see cref="Mask"/>.</summary>
        public static readonly List<Vector2> Speakers = new List<Vector2>();
        /// <summary>Which speakers are switched on (all, until somebody at the computer clicks one off).</summary>
        public static ulong Mask = PaRules.AllOn;
        /// <summary>The slot of the player who is speaking through the speakers right now, or -1.</summary>
        public static int LiveBy = -1;
        public static bool Live => LiveBy >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Speakers.Clear(); Mask = PaRules.AllOn; LiveBy = -1; }

        /// <summary>A new floor was built: its speakers, all switched on, nobody speaking.</summary>
        public static void NewFloor(IEnumerable<Vector2> speakers)
        {
            Speakers.Clear();
            if (speakers != null) Speakers.AddRange(speakers);
            Mask = PaRules.MaskOf(Speakers.Count);
            LiveBy = -1;
        }

        /// <summary>The player at the computer holds / lets go of V: the host (or this machine, offline) decides.</summary>
        public static void RequestLive(bool on)
        {
            if (Net.NetRole.Synced && Net.RunSync.Instance != null) Net.RunSync.Instance.RequestPa(on);
            else LiveBy = on ? 0 : -1;
        }

        /// <summary>The player at the computer clicked a speaker (or Select all / Deselect all): the host (or this machine, offline) decides.</summary>
        public static void RequestMask(ulong mask)
        {
            mask &= PaRules.MaskOf(Speakers.Count);
            if (Net.NetRole.Synced && Net.RunSync.Instance != null) Net.RunSync.Instance.RequestSpeakers(mask);
            else Mask = mask;
        }
    }

    /// <summary>One hallway loudspeaker on the ceiling (a round disc with a grille, flat against it): its place in <see cref="PaState.Speakers"/>.</summary>
    public sealed class LoudSpeaker : MonoBehaviour
    {
        public int Index;
        public bool On => PaRules.IsOn(PaState.Mask, Index);
    }

    /// <summary>
    /// Host / offline: while somebody is speaking through the speakers, every officer within earshot of one that is switched on drops what he is doing and rushes to the outpost (not one who is holding a player
    /// down, chasing, or down himself): <see cref="GuardAI.PaAlert"/>. A locked door stops him there like anybody. Created automatically.
    /// </summary>
    public sealed class PaDirector : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<PaDirector>()) return;
            var go = new GameObject("PaDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<PaDirector>();
        }

        float next;

        void Update()
        {
            if (!PaState.Live || !Net.NetRole.Simulates) return;
            if (Time.time < next) return;
            next = Time.time + 0.5f;
            var ctl = OutpostController.Active;
            if (ctl == null) return;
            foreach (var g in GuardAI.All)
            {
                if (!g || g.IsPuppet || g.Dead) continue;
                if (PaRules.InEarshot(g.transform.position, PaState.Speakers, PaState.Mask)) g.PaAlert(ctl.KeypadPosition);
            }
        }
    }
}
