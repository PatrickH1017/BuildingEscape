using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The door, elevator, officer and radio sounds that can come from the AudioCatalog or be generated, and (from KeypadBeep on) the
    /// guard outpost's and the safes' sounds, which are always generated.
    /// </summary>
    public enum SoundId { DoorOpen, DoorClose, ElevatorDing, Keys, KeysRun, RadioStatic, RadioCall,
                          KeypadBeep, HackWin, HackFail, Alarm, SafeOpen, SafeWrong, Pry, CrowbarSnap, Paper, AlarmOff, MetalLock, Punch, Munch, Ping, HitMarker, HitHead, HitKill }

    /// <summary>
    /// Finds the clip for a <see cref="SoundId"/>. A real clip assigned in the AudioCatalog wins; while there is none, a placeholder is
    /// generated from maths (<see cref="ProceduralSound"/>) the first time it is needed and kept for the rest of the session.
    /// Each sound comes in a few variants so repeats do not sound identical.
    /// </summary>
    public static class SoundBank
    {
        // how many generated variants each SoundId has (same order as the enum)
        static readonly int[] GeneratedVariants =
        {
            ProceduralSound.DoorVariants, ProceduralSound.DoorVariants, 1,
            ProceduralSound.KeyVariants, ProceduralSound.KeyVariants,
            ProceduralSound.RadioVariants, ProceduralSound.CallVariants,
            OutpostSound.KeypadVariants, 1, 1, 1, 1, 1, OutpostSound.PryVariants, 1, 1, 1, 1, OutpostSound.PunchVariants, OutpostSound.MunchVariants, OutpostSound.PingVariants, OutpostSound.HitVariants, OutpostSound.HitVariants, OutpostSound.HitVariants,
        };

        static AudioClip[][] made = new AudioClip[GeneratedVariants.Length][];
        static int prewarmCursor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()   // the editor may keep statics between play sessions (domain reload off)
        {
            made = new AudioClip[GeneratedVariants.Length][];
            prewarmCursor = 0;
        }

        static AudioClip[] Assigned(SoundId id)
        {
            var c = GameAudio.Catalog;
            if (!c || id >= SoundId.KeypadBeep) return null;      // (the outpost's sounds have no catalog slot: they are generated)
            AudioClip[] a;
            switch (id)
            {
                case SoundId.DoorOpen: a = c.doorOpen; break;
                case SoundId.DoorClose: a = c.doorClose; break;
                case SoundId.ElevatorDing: a = c.elevatorDing; break;
                case SoundId.Keys: a = c.keys; break;
                case SoundId.KeysRun: a = c.keysRun != null && c.keysRun.Length > 0 ? c.keysRun : c.keys; break;
                case SoundId.RadioStatic: a = c.radioStatic; break;
                default: a = c.radioCall; break;
            }
            return HasClip(a) ? a : null;
        }

        static bool HasClip(AudioClip[] a)
        {
            if (a == null) return false;
            for (int i = 0; i < a.Length; i++) if (a[i]) return true;
            return false;
        }

        /// <summary>How many different clips this sound has right now.</summary>
        public static int Variants(SoundId id)
        {
            var a = Assigned(id);
            return a != null ? a.Length : GeneratedVariants[(int)id];
        }

        /// <summary>The clip for a variant (any number: it wraps).</summary>
        public static AudioClip Get(SoundId id, int variant)
        {
            var a = Assigned(id);
            if (a != null)
            {
                for (int k = 0; k < a.Length; k++)
                {
                    var c = a[(Mathf.Abs(variant) + k) % a.Length];
                    if (c) return c;
                }
            }
            return Generated(id, Mathf.Abs(variant) % GeneratedVariants[(int)id]);
        }

        static AudioClip Generated(SoundId id, int variant)
        {
            var list = made[(int)id] ?? (made[(int)id] = new AudioClip[GeneratedVariants[(int)id]]);
            var clip = list[variant];
            if (clip) return clip;                       // (a destroyed clip counts as missing and is made again)
            var data = Build(id, variant);
            clip = AudioClip.Create($"generated {id} {variant}", data.Length, 1, Dsp.SampleRate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;   // nothing in a scene references it, so keep Unity from unloading it
            list[variant] = clip;
            return clip;
        }

        static float[] Build(SoundId id, int variant)
        {
            switch (id)
            {
                case SoundId.DoorOpen: return ProceduralSound.DoorOpen(variant);
                case SoundId.DoorClose: return ProceduralSound.DoorClose(variant);
                case SoundId.ElevatorDing: return ProceduralSound.ElevatorDing();
                case SoundId.Keys: return ProceduralSound.Keys(variant, false);
                case SoundId.KeysRun: return ProceduralSound.Keys(variant, true);
                case SoundId.RadioStatic: return ProceduralSound.RadioStatic(variant);
                case SoundId.KeypadBeep: return OutpostSound.KeypadBeep(variant);
                case SoundId.HackWin: return OutpostSound.HackWin();
                case SoundId.HackFail: return OutpostSound.HackFail();
                case SoundId.Alarm: return OutpostSound.Alarm();
                case SoundId.SafeOpen: return OutpostSound.SafeOpen();
                case SoundId.SafeWrong: return OutpostSound.SafeWrong();
                case SoundId.Pry: return OutpostSound.Pry(variant);
                case SoundId.CrowbarSnap: return OutpostSound.CrowbarSnap();
                case SoundId.Paper: return OutpostSound.Paper();
                case SoundId.AlarmOff: return OutpostSound.AlarmOff();
                case SoundId.MetalLock: return OutpostSound.MetalLock();
                case SoundId.Punch: return OutpostSound.Punch(variant);
                case SoundId.Munch: return OutpostSound.Munch(variant);
                case SoundId.Ping: return OutpostSound.Ping(variant);
                case SoundId.HitMarker: return OutpostSound.HitTick();
                case SoundId.HitHead: return OutpostSound.HitHead();
                case SoundId.HitKill: return OutpostSound.HitKill();
                default: return ProceduralSound.RadioCall(variant);
            }
        }

        /// <summary>
        /// Makes the next placeholder that does not exist yet (one per call, so a few frames at the start spread the work and the
        /// first use in the middle of a heist never hitches). Returns true once everything is made.
        /// </summary>
        public static bool PrewarmNext()
        {
            int total = 0;
            for (int i = 0; i < GeneratedVariants.Length; i++) total += GeneratedVariants[i];
            while (prewarmCursor < total)
            {
                int at = prewarmCursor++, id = 0;
                while (at >= GeneratedVariants[id]) { at -= GeneratedVariants[id]; id++; }
                if (Assigned((SoundId)id) != null) continue;      // a real clip replaces this one
                Generated((SoundId)id, at);
                break;
            }
            return prewarmCursor >= total;
        }
    }
}
