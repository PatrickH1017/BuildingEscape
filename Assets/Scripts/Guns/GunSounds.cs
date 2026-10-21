using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The sounds of the guns: the gunshots and their tails, the reload and the draw come from the Free Weapon SFX pack through the AudioCatalog - its Handgun folder for the pew and the sub machine gun, its Assault Rifle
    /// folder for the rifle and the sniper rifle, its Shotgun folder for the shotgun (<see cref="RangedWeapon.SoundSet"/>); the dry click of an empty gun is generated (the pack has none). Played through GameAudio's voice pool.
    /// A player's own gun plays flat; somebody else's (an armed guard's) plays in 3D from where it is.
    /// </summary>
    public static class GunSounds
    {
        /// <summary>The pistol's reload is five sounds (slide lock, magazine out, magazine drawn, magazine in, charge): when each starts, in seconds of a reload of 2.2 s (a longer or shorter reload stretches them).</summary>
        public static readonly float[] ReloadStepTimes = { 0f, 0.40f, 0.85f, 1.20f, 1.55f };
        /// <summary>The rifle's is four (magazine out, drawn, in, charge), the shotgun's five (charge, pump back, shell drawn, chambering, pump forward).</summary>
        static readonly float[] RifleReloadTimes = { 0f, 0.45f, 1.00f, 1.60f };
        static readonly float[] ShotgunReloadTimes = { 0f, 0.45f, 0.95f, 1.45f, 1.95f };

        /// <summary>When each sound of a reload of this set starts (see <see cref="ReloadStepTimes"/>).</summary>
        public static float[] ReloadTimes(RangedWeapon.SoundSet set) => set == RangedWeapon.SoundSet.Rifle ? RifleReloadTimes : set == RangedWeapon.SoundSet.Shotgun ? ShotgunReloadTimes : ReloadStepTimes;

        // counters for the tests
        public static int ShotsPlayed, TailsPlayed, ReloadStepsPlayed, DryClicksPlayed, EquipsPlayed, AimsPlayed;
        /// <summary>How many of the shots played were the suppressed ones of a gun with a silencer on (for the tests).</summary>
        public static int SilencedShotsPlayed;

        const float OtherMaxDistance = 70f, OtherMinDistance = 8f;
        static AudioClip click;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            click = null;
            silencedHand = silencedRifle = null; silencedTail = null; SilencedShotsPlayed = 0;
            ShotsPlayed = TailsPlayed = ReloadStepsPlayed = DryClicksPlayed = EquipsPlayed = AimsPlayed = 0;
        }

        static bool Has(AudioClip[] a) { if (a == null) return false; foreach (var c in a) if (c) return true; return false; }

        // the clips of a set; a set the catalog has none for plays the handgun's
        static AudioClip[] Pick(AudioClip[] set, AudioClip[] handgun) => Has(set) ? set : handgun;
        static AudioClip[] Shots(AudioCatalog c, RangedWeapon.SoundSet s) => s == RangedWeapon.SoundSet.Rifle ? Pick(c.rifleShot, c.gunShot) : s == RangedWeapon.SoundSet.Shotgun ? Pick(c.shotgunShot, c.gunShot) : c.gunShot;
        static AudioClip[] Tails(AudioCatalog c, RangedWeapon.SoundSet s) => s == RangedWeapon.SoundSet.Rifle ? Pick(c.rifleTail, c.gunTail) : s == RangedWeapon.SoundSet.Shotgun ? Pick(c.shotgunTail, c.gunTail) : c.gunTail;
        static AudioClip[] Reloads(AudioCatalog c, RangedWeapon.SoundSet s) => s == RangedWeapon.SoundSet.Rifle ? Pick(c.rifleReload, c.gunReload) : s == RangedWeapon.SoundSet.Shotgun ? Pick(c.shotgunReload, c.gunReload) : c.gunReload;
        static AudioClip[] Equips(AudioCatalog c, RangedWeapon.SoundSet s) => s == RangedWeapon.SoundSet.Rifle ? Pick(c.rifleEquip, c.gunEquip) : s == RangedWeapon.SoundSet.Shotgun ? Pick(c.shotgunEquip, c.gunEquip) : c.gunEquip;
        static AudioClip[] Aims(AudioCatalog c, RangedWeapon.SoundSet s) => s == RangedWeapon.SoundSet.Rifle ? Pick(c.rifleAim, c.gunAim) : s == RangedWeapon.SoundSet.Shotgun ? Pick(c.shotgunAim, c.gunAim) : c.gunAim;

        /// <summary>The pack's clips are not loaded until first played (a hitch on the first shot): load them when a gun is first drawn.</summary>
        public static void Prewarm()
        {
            var c = GameAudio.Catalog;
            if (!c) return;
            SilencedShots(RangedWeapon.SoundSet.Handgun); SilencedShots(RangedWeapon.SoundSet.Rifle); SilencedTail();            // (made now, not at the first shot)
            foreach (var set in new[] { c.gunShot, c.gunTail, c.gunReload, c.gunEquip, c.gunAim, c.rifleShot, c.rifleTail, c.rifleReload, c.rifleEquip, c.rifleAim, c.shotgunShot, c.shotgunTail, c.shotgunReload, c.shotgunEquip, c.shotgunAim })
                if (set != null) foreach (var clip in set) if (clip && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }

        /// <summary>A gunshot (and the room's tail under it). Returns true if a real clip played. <paramref name="pitch"/> is the gun's own (a sniper rifle's is deeper, a sub machine gun's lighter).</summary>
        public static bool Shot(Vector3 pos, bool own, bool silenced = false, RangedWeapon.SoundSet set = RangedWeapon.SoundSet.Handgun, float pitch = 1f)
        {
            var c = GameAudio.Catalog;
            if (!c) return false;
            if (silenced)
            {
                // a gun with a silencer on (the owner, 2026-10-20: "the weapons should sound silenced"): the suppressed shot made here - a dull pap with its thump and the action working - and a soft room under it, not the loud recording turned down
                var quiet = SilencedShots(set);
                Play(quiet[Random.Range(0, quiet.Length)], pos, SilencedShotVolume, Random.Range(0.97f, 1.03f) * pitch, own);
                ShotsPlayed++; SilencedShotsPlayed++;
                Play(SilencedTail(), pos, SilencedTailVolume, 1f, own); TailsPlayed++;
                return true;
            }
            var shots = Shots(c, set);
            if (!Has(shots)) return false;
            int i = Random.Range(0, shots.Length);
            float p = Random.Range(0.97f, 1.03f) * pitch;
            Play(shots[i], pos, 1f, p, own);
            ShotsPlayed++;
            var tails = Tails(c, set);
            if (Has(tails)) { Play(tails[i % tails.Length], pos, 1f, 1f, own); TailsPlayed++; }
            return true;
        }

        /// <summary>How loud a silenced shot and the room's tail under it are (1 is an officer's gun).</summary>
        public const float SilencedShotVolume = 0.5f, SilencedTailVolume = 0.2f;

        /// <summary>Does a gun of this class sound silenced (the owner, 2026-10-20: "the weapons should sound silenced other than the shotgun, sniper, and rocket launcher")? The same rule as the silencer itself (<see cref="RangedWeapon.TakesSilencer"/>): the pistol, the sub machine gun and the rifle; the shotgun, the sniper rifle and a rocket launcher (when there is one) never.</summary>
        public static bool SoundsSilenced(RangedWeapon.WeaponClass c) => RangedWeapon.TakesSilencer(c);

        // ------------------------------------------------------------------ the shot of a gun with a silencer (made from maths: the pack has none)

        static AudioClip[] silencedHand, silencedRifle;
        static AudioClip silencedTail;
        const int SilencedVariants = 3;

        /// <summary>The suppressed shots of a gun with a silencer on: the handgun's (the pew, the sub machine gun) or the rifle's, a few of each so that a burst does not repeat itself. Made on first use.</summary>
        public static AudioClip[] SilencedShots(RangedWeapon.SoundSet set)
        {
            bool rifle = set == RangedWeapon.SoundSet.Rifle;
            var have = rifle ? silencedRifle : silencedHand;
            if (have != null && have.Length == SilencedVariants && have[0]) return have;
            have = new AudioClip[SilencedVariants];
            for (int v = 0; v < have.Length; v++) have[v] = MakeSilencedShot(rifle, v);
            if (rifle) silencedRifle = have; else silencedHand = have;
            return have;
        }

        /// <summary>The soft room that is left of a silenced shot: a low rumble that is gone in a quarter of a second.</summary>
        public static AudioClip SilencedTail()
        {
            if (silencedTail) return silencedTail;
            var r = new SynthRandom(7777);
            int n = Dsp.Samples(0.30f);
            var d = new float[n];
            var lp = new Svf(); lp.Set(850f, 0.7f);
            float env = 1f, dec = Dsp.DecayPerSample(0.075f);
            int ramp = Dsp.Samples(0.012f);
            for (int i = 0; i < n; i++)
            {
                d[i] = lp.Low(r.Signed()) * env * (i < ramp ? i / (float)ramp : 1f);
                env *= dec;
            }
            Dsp.FadeEnds(d, 0.02f);
            Dsp.Normalize(d, 0.5f);
            silencedTail = AudioClip.Create("gun silenced tail", n, 1, Dsp.SampleRate, false);
            silencedTail.SetData(d, 0);
            silencedTail.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return silencedTail;
        }

        /// <summary>
        /// One suppressed shot: the muzzle's "pap" (noise through a band pass, gone in a few hundredths of a second), the gas' low thump (a sine falling away) and, a moment later, the slide or the bolt working - the click a silenced gun is known by.
        /// A handgun's is higher and snappier, a rifle's has more body and a longer thump.
        /// </summary>
        static AudioClip MakeSilencedShot(bool rifle, int variant)
        {
            var r = new SynthRandom((rifle ? 7300 : 7100) + variant * 37);
            int n = Dsp.Samples(rifle ? 0.34f : 0.26f);
            var d = new float[n];
            var pap = new Svf(); pap.Set((rifle ? 1700f : 2500f) * r.Range(0.92f, 1.08f), 0.85f);
            var body = new Svf(); body.Set((rifle ? 480f : 640f) * r.Range(0.92f, 1.08f), 0.7f);
            float envPap = 1f, decPap = Dsp.DecayPerSample(rifle ? 0.032f : 0.021f);
            float envBody = 1f, decBody = Dsp.DecayPerSample(rifle ? 0.085f : 0.055f);
            float phase = 0f;
            float attack = Dsp.Samples(0.0007f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Dsp.SampleRate;
                float noise = r.Signed();
                float a = pap.Band(noise) * envPap * 2.4f;
                float b = body.Low(noise) * envBody * 1.3f;
                float hz = Mathf.Lerp(rifle ? 130f : 160f, rifle ? 62f : 76f, Mathf.Min(1f, t / 0.05f));
                phase += hz / Dsp.SampleRate; phase -= Mathf.Floor(phase);
                float thump = Dsp.Sine(phase) * Mathf.Exp(-t / (rifle ? 0.045f : 0.032f)) * 0.7f;
                d[i] = (a + b + thump) * Mathf.Min(1f, (i + 1) / attack);
                envPap *= decPap; envBody *= decBody;
            }
            // the action working: the slide (or the bolt) goes back and forward a moment after the shot
            Tick(d, Dsp.Samples(rifle ? 0.040f : 0.030f), r, 3200f * r.Range(0.9f, 1.1f), 0.0030f, 0.55f);
            Tick(d, Dsp.Samples(rifle ? 0.070f : 0.052f), r, 1700f * r.Range(0.9f, 1.1f), 0.0045f, 0.42f);
            int fade = Dsp.Samples(0.02f);                                  // (only the end is faded: the shot begins at once)
            for (int i = 0; i < fade && i < n; i++) d[n - 1 - i] *= i / (float)fade;
            Dsp.Normalize(d, 0.9f);
            var clip = AudioClip.Create(rifle ? "rifle shot silenced" : "handgun shot silenced", n, 1, Dsp.SampleRate, false);
            clip.SetData(d, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clip;
        }

        /// <summary>One of the sounds of a reload (0..3 or 0..4, see <see cref="ReloadTimes"/>).</summary>
        public static void ReloadStep(int step, Vector3 pos, bool own, RangedWeapon.SoundSet set = RangedWeapon.SoundSet.Handgun)
        {
            var c = GameAudio.Catalog;
            if (!c) return;
            var reload = Reloads(c, set);
            if (!Has(reload)) return;
            var clip = reload[Mathf.Clamp(step, 0, reload.Length - 1)];
            Play(clip, pos, 1f, 1f, own);
            ReloadStepsPlayed++;
        }

        /// <summary>The gun is drawn (equipped): a rustle of leather and metal.</summary>
        public static void Draw(Vector3 pos, bool own, RangedWeapon.SoundSet set = RangedWeapon.SoundSet.Handgun)
        {
            var c = GameAudio.Catalog;
            if (!c) return;
            var equip = Equips(c, set);
            if (!Has(equip)) return;
            Play(GameAudio.Pick(equip), pos, 1f, 1f, own);
            EquipsPlayed++;
        }

        /// <summary>A guard brings his gun up to aim at somebody: the quiet warning that he has one.</summary>
        public static void Aim(Vector3 pos, bool own, RangedWeapon.SoundSet set = RangedWeapon.SoundSet.Handgun)
        {
            var c = GameAudio.Catalog;
            if (!c) return;
            var aim = Aims(c, set);
            if (!Has(aim)) return;
            Play(GameAudio.Pick(aim), pos, 0.9f, 1f, own);
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
