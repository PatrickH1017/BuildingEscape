using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The sounds of the pew: the gunshots and their tails, the reload and the draw come from the Free Weapon SFX pack (Handgun) through
    /// the AudioCatalog; the dry click of an empty gun is generated (the pack has none). Played through GameAudio's voice pool.
    /// A player's own gun plays flat; somebody else's (an armed guard's) plays in 3D from where it is.
    /// </summary>
    public static class GunSounds
    {
        /// <summary>The reload is five sounds (slide lock, magazine out, magazine drawn, magazine in, charge): when each starts, in seconds of a reload.</summary>
        public static readonly float[] ReloadStepTimes = { 0f, 0.40f, 0.85f, 1.20f, 1.55f };

        // counters for the tests
        public static int ShotsPlayed, TailsPlayed, ReloadStepsPlayed, DryClicksPlayed, EquipsPlayed, AimsPlayed;

        const float OtherMaxDistance = 70f, OtherMinDistance = 8f;
        static AudioClip click;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            click = null;
            ShotsPlayed = TailsPlayed = ReloadStepsPlayed = DryClicksPlayed = EquipsPlayed = AimsPlayed = 0;
        }

        static bool Has(AudioClip[] a) { if (a == null) return false; foreach (var c in a) if (c) return true; return false; }

        /// <summary>The pack's clips are not loaded until first played (a hitch on the first shot): load them when the gun is first drawn.</summary>
        public static void Prewarm()
        {
            var c = GameAudio.Catalog;
            if (!c) return;
            foreach (var set in new[] { c.gunShot, c.gunTail, c.gunReload, c.gunEquip, c.gunAim })
                if (set != null) foreach (var clip in set) if (clip && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }

        /// <summary>A gunshot (and the room's tail under it). Returns true if a real clip played.</summary>
        public static bool Shot(Vector3 pos, bool own)
        {
            var c = GameAudio.Catalog;
            if (!c || !Has(c.gunShot)) return false;
            int i = Random.Range(0, c.gunShot.Length);
            float pitch = Random.Range(0.97f, 1.03f);
            Play(c.gunShot[i], pos, 1f, pitch, own);
            ShotsPlayed++;
            if (Has(c.gunTail)) { Play(c.gunTail[i % c.gunTail.Length], pos, 1f, 1f, own); TailsPlayed++; }
            return true;
        }

        /// <summary>One of the five sounds of a reload (0..4).</summary>
        public static void ReloadStep(int step, Vector3 pos, bool own)
        {
            var c = GameAudio.Catalog;
            if (!c || !Has(c.gunReload)) return;
            var clip = c.gunReload[Mathf.Clamp(step, 0, c.gunReload.Length - 1)];
            Play(clip, pos, 1f, 1f, own);
            ReloadStepsPlayed++;
        }

        /// <summary>The gun is drawn (equipped): a rustle of leather and metal.</summary>
        public static void Draw(Vector3 pos, bool own)
        {
            var c = GameAudio.Catalog;
            if (!c || !Has(c.gunEquip)) return;
            Play(GameAudio.Pick(c.gunEquip), pos, 1f, 1f, own);
            EquipsPlayed++;
        }

        /// <summary>A guard brings his gun up to aim at somebody: the quiet warning that he has one.</summary>
        public static void Aim(Vector3 pos, bool own)
        {
            var c = GameAudio.Catalog;
            if (!c || !Has(c.gunAim)) return;
            Play(GameAudio.Pick(c.gunAim), pos, 0.9f, 1f, own);
            AimsPlayed++;
        }

        /// <summary>The trigger of an empty gun.</summary>
        public static void Dry(Vector3 pos, bool own)
        {
            Play(Click(), pos, 0.8f, Random.Range(0.95f, 1.05f), own);
            DryClicksPlayed++;
        }

        static void Play(AudioClip clip, Vector3 pos, float volume, float pitch, bool own)
        {
            if (!clip) return;
            if (own) GameAudio.Play(clip, pos, volume, pitch, false);
            else GameAudio.Play(clip, pos, volume, pitch, true, OtherMaxDistance, OtherMinDistance);
        }

        // ------------------------------------------------------------------ the dry click (made from maths: the pack has none)

        static AudioClip Click()
        {
            if (click) return click;
            int n = Dsp.Samples(0.08f);
            var d = new float[n];
            var r = new SynthRandom(4242);
            // the hammer falls (a sharp tick), a hair later the slide's own, softer tick
            Tick(d, 0, r, 3100f, 0.0035f, 1f);
            Tick(d, Dsp.Samples(0.034f), r, 1900f, 0.0045f, 0.55f);
            Dsp.FadeEnds(d, 0.002f);
            Dsp.Normalize(d, 0.85f);
            click = AudioClip.Create("gun dry click", n, 1, Dsp.SampleRate, false);
            click.SetData(d, 0);
            click.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return click;
        }

        static void Tick(float[] o, int at, SynthRandom r, float hz, float decaySeconds, float gain)
        {
            var bp = new Svf();
            bp.Set(hz, 3.5f);
            float env = 1f, dec = Dsp.DecayPerSample(decaySeconds);
            for (int i = 0; at + i < o.Length; i++)
            {
                o[at + i] += bp.Band(r.Signed()) * env * gain * 3f;
                env *= dec;
                if (env < 0.001f) break;
            }
        }
    }
}
