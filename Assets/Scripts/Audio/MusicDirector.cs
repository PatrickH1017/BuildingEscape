using System.Threading.Tasks;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Decides what music plays, on every machine, from state that is already shared (nothing is sent over the network):
    ///   - INTENSE: from the moment backup is called until the floor is over (the elevator seals, the run ends or the next floor
    ///     loads). Every floor of a run has a different track, chosen from the run seed so the whole team hears the same one.
    ///   - ELEVATOR ("lounge" muzak): while the local player is in an elevator car, and during the ride.
    ///   - otherwise silence.
    /// The tracks are generated in code (MusicPlan / ProceduralMusic) unless real clips are assigned in the AudioCatalog. A track is
    /// rendered on a worker thread as soon as its floor loads, so it is ready long before anyone calls backup. Created automatically.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        public enum Mood { None, Elevator, Intense }

        const string VolumeKey = "OvertimeEscape.MusicVolume";
        const float DefaultVolume = 0.7f;
        const float IntenseGain = 0.7f, ElevatorGain = 0.8f;     // balance of the two kinds of music against each other
        const float RideGrace = 5f;                              // keep elevator music this long after leaving the car (the ride loads a floor)

        static float volume = -1f;

        /// <summary>Music volume, 0-1, saved between sessions (the Esc menu has the slider). The master volume applies on top.</summary>
        public static float Volume
        {
            get { if (volume < 0f) volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume)); return volume; }
            set { volume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(VolumeKey, volume); }
        }

        public static MusicDirector Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            volume = -1f;                                        // read the saved value again (the editor may keep statics between sessions)
            if (FindFirstObjectByType<MusicDirector>()) return;
            var go = new GameObject("MusicDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<MusicDirector>();
        }

        /// <summary>What the music is doing right now.</summary>
        public Mood Current { get; private set; }
        /// <summary>Name of the track made for the current floor (empty when real clips are used or there is none).</summary>
        public string TrackName { get; private set; } = "";
        /// <summary>The intense loop for the current floor exists and can play.</summary>
        public bool IntenseReady => intenseClip != null;
        /// <summary>The intense loop is audible right now (for tests and diagnostics).</summary>
        public bool IntensePlaying => intenseSource && intenseSource.isPlaying;

        AudioSource intenseSource, elevatorSource;
        float intenseLevel, elevatorLevel;                       // 0-1 fades

        // the intense loop belongs to one floor of one run
        int trackFloor = int.MinValue, trackSeed;
        TrackSpec trackSpec;
        Task<float[]> intenseTask;
        AudioClip intenseClip;
        bool intenseOwned;                                       // true: made here, destroyed here (false: a clip from the catalog)

        Task<float[]> loungeTask;
        AudioClip loungeClip;

        bool wasLoading, dingPending;
        float dingAt, lastInCar = -99f;

        void Awake()
        {
            Instance = this;
            intenseSource = NewSource();
            elevatorSource = NewSource();
            var real = GameAudio.Catalog ? GameAudio.Catalog.elevatorMusic : null;
            if (!FirstClip(real)) loungeTask = Task.Run(() => ProceduralMusic.RenderLounge());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (intenseOwned && intenseClip) Destroy(intenseClip);
            if (loungeClip) Destroy(loungeClip);
        }

        AudioSource NewSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = true; s.spatialBlend = 0f;
            s.priority = 0;                                      // music is never the sound that gets dropped
            s.volume = 0f;
            return s;
        }

        static AudioClip FirstClip(AudioClip[] a)
        {
            if (a == null) return null;
            for (int i = 0; i < a.Length; i++) if (a[i]) return a[i];
            return null;
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            SoundBank.PrewarmNext();                             // spread making the placeholder sound effects over the first frames

            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            if (hm && hm.Floor >= 0) EnsureTrack(hm.Floor, tm ? tm.baseSeed : 0);
            PollRenders();
            WatchArrival(tm);

            Current = Choose(hm, tm);
            float dt = Time.unscaledDeltaTime;
            var elevatorClip = loungeClip ? loungeClip : FirstClip(GameAudio.Catalog ? GameAudio.Catalog.elevatorMusic : null);
            bool wantIntense = Current == Mood.Intense && intenseClip;
            bool wantElevator = Current == Mood.Elevator && elevatorClip;
            intenseLevel = Mathf.MoveTowards(intenseLevel, wantIntense ? 1f : 0f, dt / (wantIntense ? 0.8f : 1.5f));
            elevatorLevel = Mathf.MoveTowards(elevatorLevel, wantElevator ? 1f : 0f, dt / (wantElevator ? 1.0f : 1.2f));
            Apply(intenseSource, intenseClip, intenseLevel * IntenseGain);
            Apply(elevatorSource, elevatorClip, elevatorLevel * ElevatorGain);
        }

        /// <summary>What should be playing: decided from shared run state plus where the local player is.</summary>
        Mood Choose(HeistManager hm, TowerManager tm)
        {
            bool inCar = ElevatorExtraction.LocalInside || BossElevator.LocalInside;
            if (inCar) lastInCar = Time.unscaledTime;
            if (!hm) return Mood.None;
            var phase = hm.Phase;
            if (phase == HeistPhase.Won || phase == HeistPhase.Failed || phase == HeistPhase.Lobby) return Mood.None;

            bool loading = tm && tm.Loading;
            bool sealing = ElevatorExtraction.Sealing;
            bool floorLive = (phase == HeistPhase.Heist || phase == HeistPhase.FloorComplete) && !loading && hm.Floor >= 1;
            // backup was called on this floor and the floor is not over yet
            if (floorLive && !sealing && (hm.BackupCalled || hm.BackupActive)) return Mood.Intense;

            bool riding = loading || sealing;
            if (inCar || (riding && Time.unscaledTime - lastInCar < RideGrace)) return Mood.Elevator;
            return Mood.None;
        }

        /// <summary>Fades a looping source to a level, starting or stopping it as needed.</summary>
        static void Apply(AudioSource s, AudioClip clip, float level)
        {
            float v = level * Volume;
            if (v <= 0.0005f || !clip)
            {
                if (s.isPlaying) s.Stop();
                return;
            }
            if (s.clip != clip) s.clip = clip;
            s.volume = v;
            if (!s.isPlaying) s.Play();
        }

        /// <summary>The arrival chime, a moment after a floor has finished loading, for whoever is standing in the car.</summary>
        void WatchArrival(TowerManager tm)
        {
            bool loading = tm && tm.Loading;
            if (wasLoading && !loading) { dingPending = true; dingAt = Time.unscaledTime + 0.6f; }
            wasLoading = loading;
            if (!dingPending || Time.unscaledTime < dingAt) return;
            dingPending = false;
            if (ElevatorExtraction.LocalInside || BossElevator.LocalInside)
                GameAudio.PlaySound(SoundId.ElevatorDing, Vector3.zero, 0.5f, 1f, false);
        }

        // ------------------------------------------------------------------ the track of the floor

        /// <summary>When the floor (or the run) changes: free the old loop and start making the new floor's.</summary>
        void EnsureTrack(int floor, int seed)
        {
            if (floor == trackFloor && seed == trackSeed) return;
            trackFloor = floor; trackSeed = seed;
            if (intenseOwned && intenseClip) Destroy(intenseClip);
            intenseClip = null; intenseOwned = false; intenseTask = null; trackSpec = null; TrackName = "";
            if (floor < 1) return;                               // the docks have no guards and no backup

            var real = GameAudio.Catalog ? GameAudio.Catalog.intenseMusic : null;
            if (real != null && real.Length > 0)
            {
                // real music was supplied: pick one by the same never-repeat order
                int start = MusicPlan.IndexFor(seed, floor, real.Length);
                for (int k = 0; k < real.Length && !intenseClip; k++) intenseClip = real[(start + k) % real.Length];
                return;
            }
            trackSpec = MusicPlan.TrackFor(seed, floor);
            var spec = trackSpec;
            intenseTask = Task.Run(() => ProceduralMusic.RenderIntense(spec));
        }

        /// <summary>Turns finished background renders into AudioClips (clips can only be made on the main thread).</summary>
        void PollRenders()
        {
            if (intenseTask != null && intenseTask.IsCompleted)
            {
                var done = intenseTask; intenseTask = null;
                if (done.IsFaulted) Debug.LogWarning("MusicDirector: the intense track could not be made: " + done.Exception?.GetBaseException().Message);
                else if (trackSpec != null)
                {
                    intenseClip = MakeClip(trackSpec.Name, done.Result);
                    intenseOwned = true;
                    TrackName = trackSpec.Name;
                }
            }
            if (loungeTask != null && loungeTask.IsCompleted)
            {
                var done = loungeTask; loungeTask = null;
                if (done.IsFaulted) Debug.LogWarning("MusicDirector: the elevator track could not be made: " + done.Exception?.GetBaseException().Message);
                else loungeClip = MakeClip("Elevator Lounge", done.Result);
            }
        }

        static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create("music " + name, samples.Length, 1, Dsp.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
