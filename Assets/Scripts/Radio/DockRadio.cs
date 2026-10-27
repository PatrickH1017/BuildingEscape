using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The radio of the Loading Docks (the owner, 2026-10-27: "add a radio that plays random soundtracks in the dock for some music. In the synty construction pack there is a radio already there ... If the player goes up the radio and uses
    /// it then they should get a screen where they can click the little buttons to change the station"): the POLYGON Construction jobsite radio on two stacked boxes in the corner east of the vending machines (built by
    /// <see cref="FloorGenerator"/>). It plays what <see cref="RadioState"/> says - the station, and in it the track and the second the shared clock gives (<see cref="RadioRules.Locate"/>), so every machine plays the same song at the
    /// same place - and every station runs on that clock the whole run, so a station switched back to is further on, not back at its start - from a speaker in the radio (at the music volume of the settings, heard as far as the doorway to the elevator room's half of the docks: <see cref="Gain"/>). E opens its screen (<see cref="RadioScreen"/>).
    /// </summary>
    public sealed class DockRadio : Interactable
    {
        /// <summary>The radio of the docks on show (null on a floor of the tower).</summary>
        public static DockRadio Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; }

        /// <summary>
        /// How loud it is and how far it carries (the owner, 2026-10-27: "I want the radio to be louder. You should be able to hear it up until the door that leads to the room with the elevator"): the music volume of the settings itself
        /// (<see cref="Gain"/>), at full up to <see cref="MinDistance"/>, then falling gently to <see cref="AtDoorway"/> of that at the doorway it is built to be heard to (about 19 m away), and to nothing <see cref="FadeBeyond"/> past it.
        /// (Before: 0.6 of the music volume, falling off as 2.5 m / distance - a tenth at the doorway.)
        /// </summary>
        public const float Gain = 1f, MinDistance = 4f, AtDoorway = 0.4f, FadeBeyond = 6f;
        /// <summary>How far it carries when no doorway is given.</summary>
        public const float DefaultHeardTo = 19f;
        /// <summary>A speaker more than this many seconds from where the shared clock says the song is, is moved there (a stream starts late, the game was paused...).</summary>
        const float ResyncSeconds = 1.5f;

        // ---- the radio's front (SM_Prop_Radio_01, measured in Blender: DevTools\Radio_2026-10-27\radio_buttons.py), in metres from the bottom of the model in its middle, across (x), up (y) and towards the front (z)
        /// <summary>The middle of each of the eight buttons of the pad (two columns of four), their size, and how far forward their faces are.</summary>
        public static readonly Vector2[] ButtonCentres =
        {
            new Vector2(-0.0313f, 0.2330f), new Vector2(0.0313f, 0.2330f), new Vector2(-0.0313f, 0.1944f), new Vector2(0.0313f, 0.1944f),
            new Vector2(-0.0313f, 0.1558f), new Vector2(0.0313f, 0.1558f), new Vector2(-0.0313f, 0.1171f), new Vector2(0.0313f, 0.1171f),
        };
        public static readonly Vector2 ButtonSize = new Vector2(0.0443f, 0.0219f);
        public const float ButtonFront = 0.1699f;
        /// <summary>The green display above the pad: its middle, its size and how far forward it is (it leans back 15 degrees).</summary>
        public static readonly Vector3 DisplayCentre = new Vector3(0f, 0.3614f, 0.1542f);
        public static readonly Vector2 DisplaySize = new Vector2(0.13f, 0.045f);

        RadioCatalog catalog;
        AudioSource speaker;
        float[][] lengths;
        float lastSeek = -10f;

        /// <summary>How far (m) the music is still clearly heard: the doorway it is built to be heard to.</summary>
        public float HeardTo { get; private set; } = DefaultHeardTo;

        /// <summary>How loud the music is at a distance, against its loudness close by (1): the speaker's own fall-off curve.</summary>
        public float LoudnessAt(float distance) => speaker ? speaker.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(Mathf.Clamp01(distance / Mathf.Max(0.01f, speaker.maxDistance))) : 0f;

        /// <summary>The fall-off: full up to MinDistance, then gently down to AtDoorway at the doorway, and to nothing FadeBeyond past it (distances as shares of the farthest).</summary>
        static AnimationCurve Falloff(float heardTo)
        {
            float max = heardTo + FadeBeyond, near = Mathf.Min(MinDistance, heardTo * 0.5f);
            float mid = (near + heardTo) * 0.5f;
            var c = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(near / max, 1f), new Keyframe(mid / max, Mathf.Lerp(1f, AtDoorway, 0.55f)), new Keyframe(heardTo / max, AtDoorway), new Keyframe(1f, 0f));
            for (int i = 1; i < c.length - 1; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        /// <summary>The model (the radio's own frame: <see cref="Point"/>).</summary>
        public Transform Model { get; private set; }
        /// <summary>What is playing: the station, the track's clip and its title (empty when nothing).</summary>
        public int StationNow { get; private set; } = -1;
        public AudioClip ClipNow => speaker ? speaker.clip : null;
        public string TitleNow => ClipNow ? RadioRules.TrackTitle(ClipNow.name) : "";
        public bool Playing => speaker && speaker.isPlaying;
        public AudioSource Speaker => speaker;

        /// <summary>A point of the radio's front in the world (see <see cref="ButtonCentres"/>: x across, y up, z forward, from the bottom of the model in its middle).</summary>
        public Vector3 Point(Vector3 local) => Model ? Model.TransformPoint(local) : transform.TransformPoint(local);

        /// <summary>The way the radio faces, in the world.</summary>
        public Vector3 Forward => Model ? Model.forward : transform.forward;
        public Vector3 Up => Model ? Model.up : transform.up;

        /// <summary>
        /// Builds the radio from the catalog with the bottom of the model in its middle at <paramref name="baseCentre"/>, facing <paramref name="yaw"/> degrees (0: +z). Null when the catalog is not set up. The model is a plain prop with a
        /// box to stand against; this object carries the speaker and is what E works.
        /// </summary>
        public static DockRadio Build(Transform parent, Vector3 baseCentre, float yaw, Vector3? heardTo = null)
        {
            var cat = RadioCatalog.Instance;
            if (cat == null || cat.radio == null) return null;
            var root = new GameObject("DockRadio");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(baseCentre, Quaternion.Euler(0f, yaw, 0f));
            var model = Instantiate(cat.radio, root.transform, false);
            model.name = "RadioModel";
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            // the model's own pivot is the bottom of it in its middle: line its box up with that, whatever the prefab's pivot is
            var rs = model.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                model.transform.position += baseCentre - new Vector3(b.center.x, b.min.y, b.center.z);
                var box = root.AddComponent<BoxCollider>();
                box.center = root.transform.InverseTransformPoint(b.center + (baseCentre - new Vector3(b.center.x, b.min.y, b.center.z)));
                box.size = new Vector3(0.6f, b.size.y, 0.46f);
            }
            var radio = root.AddComponent<DockRadio>();
            radio.Model = root.transform;                     // (the radio's frame: the box lined up on the root, which faces the way the model does)
            radio.catalog = cat;
            radio.HeardTo = heardTo.HasValue ? Vector3.Distance(new Vector3(baseCentre.x, 0f, baseCentre.z), new Vector3(heardTo.Value.x, 0f, heardTo.Value.z)) : DefaultHeardTo;
            radio.Setup();
            return radio;
        }

        void Setup()
        {
            reach = DefaultReach;
            FitAimBoxToChildren();
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.loop = false;
            speaker.spatialBlend = 1f;
            speaker.rolloffMode = AudioRolloffMode.Custom;
            speaker.minDistance = 1f;
            speaker.maxDistance = HeardTo + FadeBeyond;
            speaker.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Falloff(HeardTo));
            speaker.dopplerLevel = 0f;
            speaker.spread = 60f;
            speaker.priority = 40;
            lengths = new float[RadioRules.StationCount][];
            for (int s = 0; s < RadioRules.StationCount; s++)
            {
                var t = catalog.Tracks(s);
                lengths[s] = new float[t.Length];
                for (int i = 0; i < t.Length; i++) lengths[s][i] = t[i] ? t[i].length : 0f;
            }
            RadioState.PutOnIfOff();
        }

        protected override void OnEnable() { base.OnEnable(); Current = this; }
        protected override void OnDisable() { base.OnDisable(); if (Current == this) Current = null; if (RadioScreen.Open && RadioScreen.Radio == this) RadioScreen.Close(); }

        public override string Prompt => "[E] Use the radio";

        public override void Use(WobblyPlayer who) { RadioScreen.Show(this); }

        /// <summary>The track of a station and the second in it that the shared clock gives now (false: nothing to play).</summary>
        public bool Where(int station, out AudioClip clip, out float offset)
        {
            clip = null; offset = 0f;
            if (catalog == null || !RadioRules.ValidStation(station)) return false;
            var tracks = catalog.Tracks(station);
            if (tracks.Length == 0) return false;
            var order = RadioRules.Order(tracks.Length, RadioState.Seed + station * 7919);
            if (!RadioRules.Locate(lengths[station], order, RadioState.Elapsed(station), out int slot, out offset)) return false;
            clip = tracks[order[slot]];
            return clip != null;
        }

        void Update()
        {
            if (!speaker) return;
            speaker.volume = Mathf.Min(1f, MusicDirector.Volume * Gain * RadioRules.VolumeGain(RadioState.Volume));          // (its volume buttons: 8 of 10 is the music volume itself)
            StationNow = RadioState.Station;
            if (!RadioState.On || !Where(StationNow, out var clip, out float offset))          // (switched off: silent - the stations' clock runs on)
            {
                if (speaker.isPlaying) speaker.Stop();
                return;
            }
            if (speaker.clip != clip)
            {
                speaker.Stop();
                speaker.clip = clip;
                speaker.Play();
                speaker.time = Mathf.Clamp(offset, 0f, Mathf.Max(0f, clip.length - 0.05f));
                lastSeek = Time.unscaledTime;
                return;
            }
            // (a moment to let the stream start before it is checked; then only a real slip is corrected)
            if (Time.unscaledTime - lastSeek < 2f) return;
            if (!speaker.isPlaying || Mathf.Abs(speaker.time - offset) > ResyncSeconds)
            {
                if (!speaker.isPlaying) speaker.Play();
                speaker.time = Mathf.Clamp(offset, 0f, Mathf.Max(0f, clip.length - 0.05f));
                lastSeek = Time.unscaledTime;
            }
        }
    }
}
