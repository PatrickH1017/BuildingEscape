using System.Collections.Generic;
using BuildingEscape.Net;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BuildingEscape
{
    /// <summary>
    /// The run-start cinematic ("the Intro"): the players' characters - the loading dock workers - talk in a circle in the break room of the Loading Docks until the TV comes on, then line up in front of it and watch the boss tell them, on the
    /// wall-mounted TV (<see cref="DockTv"/>), that their bonuses are gone this year because he is buying a private jet; when the broadcast ends one of them says to another
    /// "Hey call that guy you were talking about." The owner's rules (2026-10-16 night and 2026-10-17): it plays every time a new run is started; HOLDING SPACE skips it; in multiplayer
    /// it is skipped, for everybody, once at least 50 % of the party members are holding it (<see cref="IntroRules"/>; the host counts, <c>RunSync.Intro.cs</c>).
    ///
    /// How it works: <c>RunSync.OnNetworkSpawn</c> arms a director (<see cref="Arm"/>) on every machine of a new run. The director waits until this machine's Loading Docks are built; the host
    /// (when everybody has arrived) fixes a start time a moment ahead and every machine plays the same seconds of it. Two cameras make the pictures. The boss's office
    /// (<see cref="BossOffice"/>, far from the map, its own layer and lights) is filmed by one into a render texture that is the TV's screen: the monologue. The other stands in the docks
    /// and films the TV and the workers - stand-ins dressed as the players' characters (a lone player gets a coworker beside them) - in a chain of shots (<see cref="DockShot"/>); its
    /// picture is drawn by IMGUI above every other screen (so the HUD is hidden) with letterbox bars, the words typed out as subtitles and the "hold Space" meter. The players' own
    /// bodies stand still in the elevator meanwhile (<c>OutpostUi.Kind.Intro</c>: the same switch the lock hack uses, which every other screen and control already respects) and are hidden
    /// so that nobody is seen twice. The words are in <see cref="IntroContent"/>.
    ///
    /// An OFFLINE run (PLAY SOLO in the menu, or the Greybox scene played directly in the Editor) has no network session and so no RunSync to arm the director:
    /// <see cref="WatchScenes"/> arms one when the run scene loads without a session. It waits for the docks the same way but runs on this machine's own
    /// clock, and one player holding Space for a second is enough to skip it.
    /// It is off in the Editor's batch tests (so every scenario runs as before) and the built player's autopilot skips it unless it is testing it.
    /// </summary>
    public sealed class IntroCinematic : MonoBehaviour
    {
        const float Aspect = 2.39f;

        public static IntroCinematic Current { get; private set; }
        public static bool Playing => Current != null && Current.state == State.Playing;
        /// <summary>Seconds into the Intro that is playing (negative in the lead-in), -1 when none is.</summary>
        public static float Clock => Playing ? Current.t : -1f;
        /// <summary>Tests: no Intro at all (the host marks it skipped at once).</summary>
        public static bool SkipForTests;
        /// <summary>Tests: the local player is holding Space.</summary>
        public static bool TestHold;
        /// <summary>Tests: play it in a batch run too.</summary>
        public static bool ForceInBatch;
        /// <summary>How many Intros this session has shown.</summary>
        public static int TimesPlayed { get; private set; }
        public static bool Enabled => !Application.isBatchMode || ForceInBatch;

        /// <summary>The workers who are in the scene now (for tests), the one who speaks and the one they speak to (indices; -1 before they stand there).</summary>
        public int WorkerCount => actors.Count;
        public int SpeakerAt => speakerAt;
        public int ListenerAt => listenerAt;
        public string WorkerLabel(int i) => i >= 0 && i < actors.Count ? actors[i].label : null;
        public Vector3 WorkerFoot(int i) => i >= 0 && i < actors.Count ? actors[i].foot : Vector3.zero;
        public float WorkerYaw(int i) => i >= 0 && i < actors.Count ? actors[i].yaw : 0f;
        /// <summary>Tests: where the worker stands in the circle and where in the line, the circle's middle, and whether they are on the walk between the two now.</summary>
        public Vector3 WorkerRingFoot(int i) => i >= 0 && i < actors.Count ? actors[i].ringFoot : Vector3.zero;
        public Vector3 WorkerLineFoot(int i) => i >= 0 && i < actors.Count ? actors[i].lineFoot : Vector3.zero;
        public Vector3 RingCentre => ringCentre;
        public bool WorkerWalking(int i)
        {
            if (i < 0 || i >= actors.Count) return false;
            float w = IntroRules.WalkProgress(t, actors[i].departAt, actors[i].metres);
            return w > 0f && w < 1f;
        }
        /// <summary>Is this worker a Tiny Toon person (as the cutscene wants them) and not one of the colourful Party characters?</summary>
        public bool WorkerIsToon(int i) => i >= 0 && i < actors.Count && !actors[i].party && actors[i].anim != null;
        public bool HasTv => tv != null;
        public Camera DockCamera => dockCam;

        /// <summary>Tests (an offline Intro): jump to this second of it.</summary>
        public void TestSeek(float seconds) { if (offline && state == State.Playing) { offlineStart = Time.unscaledTimeAsDouble - seconds; t = seconds; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; TimesPlayed = 0; SkipForTests = false; TestHold = false; ForceInBatch = false; }

        enum State { Waiting, Playing, Done }

        struct Rig
        {
            public Vector3 posA, posB, lookA, lookB; public float fovA, fovB;
            public Rig(Vector3 posA, Vector3 posB, Vector3 lookA, Vector3 lookB, float fovA, float fovB)
            { this.posA = posA; this.posB = posB; this.lookA = lookA; this.lookB = lookB; this.fovA = fovA; this.fovB = fovB; }
        }

        // what the TV shows: the office camera, in the office's own coordinates (indexed by IntroShot): each shot drifts from A to B over its beat
        static readonly Rig[] Rigs =
        {
            // (the boss's head is at about y 1.5, z 1.3; the Window shot swings from the jet's first half to its second: its look points are on the jet's path)
            /* Wide        */ new Rig(new Vector3(-3.0f, 1.65f, -5.5f), new Vector3(-2.2f, 1.60f, -5.0f), new Vector3(0.3f, 1.10f, 1.2f), new Vector3(0.2f, 1.25f, 1.2f), 42f, 38f),
            /* Medium      */ new Rig(new Vector3(0.45f, 1.45f, -3.2f), new Vector3(0.30f, 1.42f, -2.8f), new Vector3(0f, 1.20f, 1.3f), new Vector3(0f, 1.24f, 1.3f), 27f, 25f),
            /* MediumClose */ new Rig(new Vector3(-0.55f, 1.42f, -2.3f), new Vector3(-0.35f, 1.42f, -1.9f), new Vector3(0f, 1.28f, 1.3f), new Vector3(0f, 1.28f, 1.3f), 22f, 20f),
            /* Desk        */ new Rig(new Vector3(1.15f, 1.25f, -1.6f), new Vector3(0.75f, 1.20f, -1.3f), new Vector3(-0.15f, 1.12f, 0.4f), new Vector3(-0.05f, 1.20f, 0.6f), 34f, 30f),
            /* Window      */ new Rig(new Vector3(-1.0f, 1.50f, 2.5f), new Vector3(-1.0f, 1.55f, 2.5f), new Vector3(20f, 12f, 64f), new Vector3(-20f, 12f, 64f), 34f, 34f),
            /* Low         */ new Rig(new Vector3(0.35f, 0.95f, -2.7f), new Vector3(0.20f, 1.00f, -2.1f), new Vector3(0f, 1.38f, 1.3f), new Vector3(0f, 1.42f, 1.3f), 30f, 26f),
        };

        // where the cutscene's camera is in the docks, in the TV's own frame (side = along its right, up, outward = out of the wall into the room): in the break room, which is 4.6 m deep, so the workers stand 2 m out and the cameras stay inside it
        static readonly Rig[] DockRigs =
        {
            /* Establish */ new Rig(new Vector3(0.9f, 2.0f, 3.9f), new Vector3(1.2f, 2.0f, 4.1f), new Vector3(-1.2f, 1.3f, 2.2f), new Vector3(-0.15f, 1.9f, 0.3f), 66f, 46f),          // (the break room from its north side: the circle on the right in front of the counters, the dark TV on the left; it holds there while they talk and eases over to the Behind set-up - its first pose - while the TV comes on and they line up)
            /* Behind    */ new Rig(new Vector3(1.2f, 2.0f, 4.1f), new Vector3(0.8f, 1.95f, 3.7f), new Vector3(-0.15f, 1.9f, 0.3f), new Vector3(-0.05f, 2.0f, 0.3f), 46f, 42f),
            /* TvClose   */ new Rig(new Vector3(0.20f, 1.90f, 1.5f), new Vector3(0.0f, 1.95f, 1.2f), new Vector3(0f, DockTv.CentreHeight, 0f), new Vector3(0f, DockTv.CentreHeight, 0f), 30f, 27f),
            /* Faces     */ new Rig(new Vector3(-1.2f, 1.5f, 0.7f), new Vector3(-0.9f, 1.5f, 0.9f), new Vector3(0.1f, 1.62f, 2.1f), new Vector3(0.0f, 1.62f, 2.1f), 40f, 36f),
            /* TwoShot   */ new Rig(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 36f, 33f),     // (made from where the two of them stand: see PlaceDockCamera)
            /* Bay       */ new Rig(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 40f, 34f),     // (made from the vendor's bay: see PlaceDockCamera)
        };

        /// <summary>How far in front of the TV the workers stand (the break room is 4.6 m deep); every other one stands a quarter of a metre further back.</summary>
        const float DockWorkerOut = 2.0f;

        /// <summary>
        /// Where the circle of workers stands, in the TV's frame (the owner, 2026-10-20: "have them all in the break room in a circle talking then the tv plays and then they all line up to watch it"): 2.2 m west of the TV's middle and 2.6 m
        /// out from its wall, on the west side of the break room in front of the counters (the fridge, the desk and the chairs are further west; the camera of the opening looks at it from the north-east corner).
        /// </summary>
        const float RingSide = -2.2f, RingOut = 2.6f;
        /// <summary>The Establish shot lasts the dark TV and the TV coming on (two beats): the camera makes one move across both.</summary>
        static readonly float EstablishSeconds = IntroRules.Beats[IntroRules.TvOnBeat].end;
        /// <summary>The circle talks in soft blips (the Intro's voices, quietly: whoever's turn it is, in a pitch of their own) until a moment before the TV comes on; false = silent.</summary>
        static readonly bool CircleVoices = true;

        /// <summary>The Intro's first picture comes this long after the docks of an offline run are built (online the host starts it <see cref="IntroRules.LeadIn"/> after everybody has arrived).</summary>
        const float OfflineLeadIn = 0.6f;
        /// <summary>The worker who speaks, and the one they speak to, turn to each other this long into the last line's beat.</summary>
        const float TurnAfter = 0.2f;

        /// <summary>The beat that holds the workers' own line.</summary>
        static readonly int WorkerBeat = FindWorkerBeat();

        static int FindWorkerBeat()
        {
            for (int i = 0; i < IntroRules.Beats.Length; i++) if (IntroRules.Beats[i].speaker == Speaker.Worker) return i;
            return IntroRules.Beats.Length - 1;
        }

        sealed class Actor
        {
            public Transform root;
            public CharacterAnimator anim;
            public string label;
            public Vector3 foot;                                            // where they stand now: in the circle, on the walk, or in the line
            public Vector3 ringFoot, lineFoot;                              // their spot in the circle and their place in the line
            public float yaw, ringYaw, walkYaw, noticeDelay, departAt, metres, lookYaw, phase;       // (ringYaw: facing the middle of the circle; walkYaw: facing the way they walk; noticeDelay: how long after the TV comes on they look round at it; departAt, metres: when the walk starts and how long it is; lookYaw: how far the head is turned; phase: their own rhythm)
            public bool party;           // a Party character: its face can change (a Tiny Toon person has one face)
            public int face = 1, shownFace = -1;
            public int ownFace = 1;
            public Transform head, spine;                 // a Tiny Toon person has one face: how they feel shows in their head and chest (see Pose)
            public float headPitch, spinePitch;
        }

        State state;
        bool offline;                    // an offline run: no RunSync, this machine's own clock, one player, a plain hold of Space to skip
        double offlineStart = -1.0;      // the (unscaled) time of the Intro's second 0
        float t, held;
        bool voting, shown, whooshed, tvClicked;
        int lastChars = -1, lastBeat = -1;
        BossOffice office;
        DockTv tv;
        Camera officeCam, dockCam;
        RenderTexture tvRt, rt;
        AudioSource voice, hiss;
        AudioClip[] blips;
        AudioClip whoosh, hissClip;
        readonly List<Actor> actors = new List<Actor>();
        int speakerAt = -1, listenerAt = -1;
        Vector3 ringCentre;
        float nextMurmur;
        readonly List<Renderer> hiddenPlayers = new List<Renderer>();

        /// <summary>Called by <c>RunSync.OnNetworkSpawn</c> on every machine of a new run (not for a player who is already looking at a later floor).</summary>
        public static void Arm(Transform parent)
        {
            if (!Enabled || Current) return;
            BossOffice.Prewarm();
            var go = new GameObject("IntroDirector");
            go.transform.SetParent(parent, false);
            Current = go.AddComponent<IntroCinematic>();
        }

        /// <summary>The run scene was loaded with no network session (PLAY SOLO, or Play pressed on the Greybox scene in the Editor): there is no RunSync to arm the
        /// Intro, so do it here. An online run is left to <c>RunSync.OnNetworkSpawn</c>.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void WatchScenes()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == NetConst.RunScene) ArmOffline();          // (the run scene was the first one: Play pressed on it in the Editor)
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == NetConst.RunScene) ArmOffline();
        }

        static void ArmOffline()
        {
            if (!Enabled || Current || NetSession.Online) return;
            BossOffice.Prewarm();
            Current = new GameObject("IntroDirector").AddComponent<IntroCinematic>();
            Current.offline = true;
        }

        bool LocalReady()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            bool player = offline ? WobblyPlayerRegistry.All.Count > 0 : NetPlayer.Local != null;
            return hm != null && tm != null && !tm.Loading && hm.Floor == 0 && hm.Phase != HeistPhase.Lobby && player;
        }

        void Update()
        {
            if (state == State.Done) return;
            if (offline) { UpdateOffline(); return; }
            var sync = RunSync.Instance;
            if (sync == null || !sync.IsSpawned) { if (state == State.Playing) Finish(); return; }

            if (state == State.Waiting)
            {
                if (SkipForTests && sync.IsServer) sync.HostSkipIntroNow();
                if (sync.IntroOver) { Finish(); return; }               // (a machine that arrives late, a skipped or finished Intro)
                if (!LocalReady()) return;
                if (sync.IsServer && sync.IntroStart < 0.0 && sync.EveryoneHere) sync.HostStartIntro();
                if (sync.IntroStart >= 0.0) Begin();
                return;
            }

            t = (float)(sync.NetworkManager.ServerTime.Time - sync.IntroStart);
            held = IntroRules.Hold(held, Input.GetKey(KeyCode.Space) || TestHold, Time.unscaledDeltaTime);
            if (!voting && IntroRules.VoteSaved(voting, held)) { voting = true; sync.RequestIntroHold(true); }      // (the bar filled: the vote is saved, letting go of Space does not take it back)
            if (sync.IntroOver || IntroRules.Over(t)) Finish();
        }

        /// <summary>An offline run: nobody to wait for and nobody to vote; the clock is this machine's, and a held Space skips it (1 of 1).</summary>
        void UpdateOffline()
        {
            if (SkipForTests || NetSession.Online) { Finish(); return; }                       // (tests; or a session came up under us)
            if (state == State.Waiting)
            {
                if (!LocalReady()) return;
                offlineStart = Time.unscaledTimeAsDouble + OfflineLeadIn;
                Begin();
                return;
            }
            t = (float)(Time.unscaledTimeAsDouble - offlineStart);
            held = IntroRules.Hold(held, Input.GetKey(KeyCode.Space) || TestHold, Time.unscaledDeltaTime);
            if (IntroRules.HoldDone(held) || IntroRules.Over(t)) Finish();
        }

        /// <summary>The run's seed (the same on every machine of a run): it picks which worker says the last line.</summary>
        int RunSeed()
        {
            if (!offline && RunSync.Instance != null && RunSync.Instance.IsSpawned) return RunSync.Instance.Seed;
            var tm = TowerManager.Instance;
            return tm ? tm.baseSeed : 1;
        }

        /// <summary>The look of a player's character as the lobby chose it (what every machine shows them in), or the default for their slot.</summary>
        static CharacterLook LookOf(WobblyPlayer p)
        {
            var looks = PartyCharacterFactory.PlayerLooks;
            int slot = 0;
            if (p && p.TryGetComponent(out NetPlayer np) && np.IsSpawned) slot = Mathf.Max(0, np.Slot);
            if (p && p.TryGetComponent(out PlayerVisual pv) && pv.lookOverride != null) return pv.lookOverride;
            if (!NetSession.Online && PlayerAppearance.TryLoadSaved(out var saved)) return saved.ToLook();
            return looks[slot % looks.Length];
        }

        void Begin()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            state = State.Playing;
            dockGen = FindFirstObjectByType<FloorGenerator>();
            TimesPlayed++;

            office = BossOffice.Build(BossOffice.DefaultOrigin);
            CloseBay();                                                // (the vendor is the guy they will call: he is not to be seen yet)
            tv = DockTv.Current;
            int w = Mathf.Clamp(Screen.width, 640, 1920), h = Mathf.RoundToInt(w / Aspect);
            // the TV's picture: the office camera, rendered by hand each frame (with the office's own flat ambient light, see RenderOffice); 16:9 on the TV
            // (with no TV in the docks it is the whole picture, as in the first version of the Intro, and 2.39:1)
            tvRt = tv != null ? new RenderTexture(1024, 576, 24, RenderTextureFormat.ARGB32) { name = "IntroTv", antiAliasing = 2 }
                              : new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "IntroPicture", antiAliasing = 4 };
            var oc = new GameObject("IntroOfficeCamera");
            oc.transform.SetParent(transform, false);
            officeCam = oc.AddComponent<Camera>();
            officeCam.cullingMask = 1 << BossOffice.Layer;
            officeCam.clearFlags = CameraClearFlags.SolidColor;
            officeCam.backgroundColor = BossOffice.Haze;
            officeCam.nearClipPlane = 0.08f; officeCam.farClipPlane = 9000f; officeCam.allowHDR = false;
            officeCam.targetTexture = tvRt;
            officeCam.enabled = false;
            if (tv != null)
            {
                // the picture the player sees: a camera in the docks
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "IntroPicture", antiAliasing = 4 };
                var dc = new GameObject("IntroCamera");
                dc.transform.SetParent(transform, false);
                dockCam = dc.AddComponent<Camera>();
                dockCam.cullingMask = ~(1 << BossOffice.Layer);
                dockCam.clearFlags = CameraClearFlags.SolidColor;
                dockCam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
                dockCam.nearClipPlane = 0.06f; dockCam.farClipPlane = 120f; dockCam.allowHDR = false;
                dockCam.targetTexture = rt;
                tv.ShowOff();                                          // (the TV is dark until the announcement: IntroRules.TvOnAt)
                BuildActors();
                HidePlayers();
            }
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0f; voice.playOnAwake = false;
            blips = MakeBlips(); whoosh = MakeWhoosh();
            OutpostUi.Show(OutpostUi.Kind.Intro);                     // the player stands still, nothing else answers E, Tab or the mouse
            shown = true;
            LateUpdate();
            Debug.Log($"[Intro] the TV scene was built in {watch.ElapsedMilliseconds} ms: tv {(tv != null)}, {actors.Count} workers (speaker {(speakerAt >= 0 ? actors[speakerAt].label : "-")}, listener {(listenerAt >= 0 ? actors[listenerAt].label : "-")})");
        }

        // ------------------------------------------------------------------ the workers

        /// <summary>
        /// Stand-ins dressed as the players' characters, in a circle talking and then in a row in front of the TV: the same on every machine (the players in slot order, the speaker picked from the run's seed). A lone
        /// player gets a coworker on their left, and the coworker is who speaks.
        /// </summary>
        void BuildActors()
        {
            var cast = new List<(CharacterLook look, string label)>();
            bool coworker = false;
            if (NetSession.Online && NetPlayer.All.Count > 0)
            {
                foreach (var p in NetPlayer.Ordered())
                {
                    int slot = p.TryGetComponent(out NetPlayer np) && np.IsSpawned ? Mathf.Max(0, np.Slot) : cast.Count;
                    cast.Add((ToonOf(LookOf(p), slot), IntroContent.PlayerLabel(slot)));
                }
            }
            else if (WobblyPlayerRegistry.All.Count > 0) cast.Add((ToonOf(LookOf(WobblyPlayerRegistry.All[0]), 0), IntroContent.PlayerLabel(0)));
            if (cast.Count == 0) return;
            if (cast.Count == 1)
            {
                // somebody for the lone player to be spoken to: a coworker in a look of their own
                var mine = cast[0].look;
                var other = PartyCharacterFactory.PlayerLooks[1].WithModel(PickToon(1, mine.model));          // (a Tiny Toon person other than the player's)
                if (string.IsNullOrEmpty(other.model) && Mathf.Abs(other.body.r - mine.body.r) + Mathf.Abs(other.body.g - mine.body.g) + Mathf.Abs(other.body.b - mine.body.b) < 0.3f) other = PartyCharacterFactory.PlayerLooks[2];   // (no toon people on this machine: two Party characters, in different colours)
                cast.Insert(0, (other, IntroContent.CoworkerLabel));
                coworker = true;
            }
            int speaker = IntroRules.SpeakerIndex(cast.Count, coworker, RunSeed());
            int listener = IntroRules.ListenerIndex(cast.Count, speaker);
            // five workers hang out in the break room (the owner, 2026-10-20): the players' characters, the coworker of a lone player, and more coworkers to make five (after the speaker and the listener are known: they are among the first)
            for (int k = 0; cast.Count < IntroRules.Workers; k++) cast.Add((FillerLook(k, cast), IntroContent.CoworkerLabel));
            var row = IntroRules.RowOrder(cast.Count, speaker, listener);
            float spacing = IntroRules.Spacing(cast.Count);
            var faceTv = Quaternion.LookRotation(-tv.Forward, Vector3.up);
            // the line in front of the TV (left to right) and the circle (the owner, 2026-10-20: they talk in a circle first and line up when the TV comes on): each place in the line is paired with the spot of the circle that makes all the walks shortest, so none cross
            int n = row.Length;
            var line = new Vector3[n];
            for (int pos = 0; pos < n; pos++)
                line[pos] = tv.Point((pos - (n - 1) * 0.5f) * spacing, 0f, DockWorkerOut + (pos % 2 == 0 ? 0f : 0.25f));
            ringCentre = tv.Point(RingSide, 0f, RingOut);
            var ring = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                float ang = Mathf.PI * 2f * k / n;
                ring[k] = tv.Point(RingSide + IntroRules.RingRadius * Mathf.Sin(ang), 0f, RingOut + IntroRules.RingRadius * Mathf.Cos(ang));
            }
            var spot = PairWithRing(line, ring);
            for (int pos = 0; pos < n; pos++)
            {
                int who = row[pos];
                var ringFoot = ring[spot[pos]];
                var toMiddle = ringCentre - ringFoot; toMiddle.y = 0f;
                float ringYaw = Quaternion.LookRotation(toMiddle, Vector3.up).eulerAngles.y;
                var walk = line[pos] - ringFoot; walk.y = 0f;
                var host = new GameObject("IntroWorker_" + cast[who].label);
                host.transform.SetParent(transform, false);
                host.transform.SetPositionAndRotation(ringFoot + Vector3.up, Quaternion.Euler(0f, ringYaw, 0f));               // (the pack's characters are dressed on a 2 m capsule: its middle is a metre above the feet)
                var anim = PartyCharacterFactory.Attach(host, cast[who].look);
                var actor = new Actor
                {
                    root = host.transform, anim = anim, label = cast[who].label, foot = ringFoot, ringFoot = ringFoot, lineFoot = line[pos], yaw = ringYaw, ringYaw = ringYaw,
                    walkYaw = walk.sqrMagnitude > 0.01f ? Quaternion.LookRotation(walk, Vector3.up).eulerAngles.y : faceTv.eulerAngles.y, metres = walk.magnitude,
                    party = !ToonAvailable(cast[who].look.model), ownFace = cast[who].look.face, phase = pos * 1.9f,
                };
                var animator = anim ? anim.GetComponent<Animator>() : null;
                if (!actor.party && animator && animator.isHuman) { actor.head = animator.GetBoneTransform(HumanBodyBones.Head); actor.spine = animator.GetBoneTransform(HumanBodyBones.Spine); }
                actors.Add(actor);
                if (who == speaker) speakerAt = actors.Count - 1;
                if (who == listener) listenerAt = actors.Count - 1;
            }
            // when each of them looks round at the TV (the one nearest it first) and sets off: the walks end one after the other, the shortest first, so the longest start first
            for (int i = 0; i < actors.Count; i++)
            {
                int shorter = 0, nearer = 0;
                float mine = Vector3.Dot(actors[i].ringFoot - tv.Origin, tv.Forward);
                for (int j = 0; j < actors.Count; j++)
                {
                    if (j == i) continue;
                    if (actors[j].metres < actors[i].metres || (actors[j].metres == actors[i].metres && j < i)) shorter++;
                    if (Vector3.Dot(actors[j].ringFoot - tv.Origin, tv.Forward) < mine) nearer++;
                }
                actors[i].departAt = IntroRules.DepartAt(actors[i].metres, shorter);
                actors[i].noticeDelay = 0.05f * nearer;
            }
        }

        /// <summary>Which spot of the circle each place in the line is paired with (<c>result[place]</c>) so that the walks from the circle to the line are as short as can be altogether - and then none of them cross: the least total distance over every way of pairing them (120 for five).</summary>
        static int[] PairWithRing(Vector3[] line, Vector3[] ring)
        {
            int n = line.Length;
            var best = new int[n]; var perm = new int[n];
            for (int i = 0; i < n; i++) { best[i] = i; perm[i] = i; }
            float bestCost = float.MaxValue;
            void Go(int k, float cost)
            {
                if (cost >= bestCost) return;
                if (k == n) { bestCost = cost; System.Array.Copy(perm, best, n); return; }
                for (int i = k; i < n; i++)
                {
                    (perm[k], perm[i]) = (perm[i], perm[k]);
                    var d = line[k] - ring[perm[k]]; d.y = 0f;
                    Go(k + 1, cost + d.magnitude);
                    (perm[k], perm[i]) = (perm[i], perm[k]);
                }
            }
            Go(0, 0f);
            return best;
        }

        /// <summary>A coworker in a look of his own to make up the five workers: a Tiny Toon person that nobody in the cast is, if there is one (Party characters in other colours when this machine has no toon people).</summary>
        static CharacterLook FillerLook(int k, List<(CharacterLook look, string label)> cast)
        {
            var looks = PartyCharacterFactory.PlayerLooks;
            var used = new HashSet<string>();
            foreach (var c in cast) if (!string.IsNullOrEmpty(c.look.model)) used.Add(c.look.model);
            string pick = null;
            var cat = CharacterCatalog.Instance;
            if (cat != null)
            {
                var ids = new List<string>();
                foreach (var id in ToonCycle) if (cat.Find(id) != null) ids.Add(id);
                for (int j = 0; j < ids.Count && pick == null; j++)
                {
                    var id = ids[(k * 2 + 3 + j) % ids.Count];
                    if (!used.Contains(id)) pick = id;
                }
                if (pick == null && ids.Count > 0) pick = ids[(k * 2 + 3) % ids.Count];
            }
            var look = looks[(2 + k) % looks.Length];
            return pick != null ? look.WithModel(pick) : look;
        }

        // ------------------------------------------------------------------ Tiny Toon workers (the owner, 2026-10-17: the cutscene's characters are the toon people, not the colourful Party ones)

        /// <summary>The dock workers the cutscene casts, in a fixed order (the same on every machine): a player whose character this machine does not know is one of them by slot (the office people; the owner, 2026-10-17 night).</summary>
        static string[] ToonCycle => PlayerAppearance.DefaultToons;

        static bool ToonAvailable(string id) => !string.IsNullOrEmpty(id) && CharacterCatalog.Instance != null && CharacterCatalog.Instance.Find(id) != null;

        /// <summary>The toon person for this slot, other than <paramref name="avoid"/> when there is a choice; null when this machine has no toon people.</summary>
        static string PickToon(int slot, string avoid)
        {
            var cat = CharacterCatalog.Instance;
            if (cat == null) return null;
            var ids = new List<string>();
            foreach (var id in ToonCycle) if (cat.Find(id) != null) ids.Add(id);
            if (ids.Count == 0) foreach (var c in cat.Players()) ids.Add(c.id);
            if (ids.Count == 0) return null;
            int at = ((slot % ids.Count) + ids.Count) % ids.Count;
            if (ids[at] == avoid && ids.Count > 1) at = (at + 1) % ids.Count;
            return ids[at];
        }

        /// <summary>A player who chose a Tiny Toon person is shown as them; anybody else (the colourful Party character) is a toon person by slot.</summary>
        static CharacterLook ToonOf(CharacterLook look, int slot)
        {
            if (ToonAvailable(look.model)) return look;
            string id = PickToon(slot, null);
            return id == null ? look : look.WithModel(id);
        }

        /// <summary>
        /// A Tiny Toon person has one face, so their mood shows in how they stand: the boss takes the bonuses and the head and chest drop; when the broadcast ends they lift and shake a little (angry).
        /// Called after the animation has run (from LateUpdate), so the offsets are on top of it and do not build up.
        /// </summary>
        void Pose(Actor a, int index, int mood, float dt)
        {
            if (a.head == null && a.spine == null) return;
            a.headPitch = Mathf.MoveTowards(a.headPitch, mood == 1 ? 26f : mood == 2 ? -6f : 0f, 70f * dt);
            a.spinePitch = Mathf.MoveTowards(a.spinePitch, mood == 1 ? 10f : mood == 2 ? -3f : 0f, 35f * dt);
            float shake = mood == 2 ? Mathf.Sin(Time.time * 19f + a.root.GetInstanceID() * 0.37f) * 1.6f : 0f;
            // talking in the circle (before the TV comes on, the owner, 2026-10-20): whoever's turn it is talks - the head moves with the words and glances round the others - and everybody else looks at them and now and then nods
            float look = 0f, nod = 0f, lean = 0f;
            if (mood == -1)
            {
                int talker = IntroRules.TalkerAt(t, actors.Count);
                if (index == talker)
                {
                    look = Mathf.Sin(t * 1.2f + a.phase) * 24f;
                    nod = (0.5f + 0.5f * Mathf.Sin(t * 9f + a.phase * 3f)) * 4.5f;
                    lean = 3f;
                }
                else
                {
                    var toTalker = actors[talker].foot - a.foot; toTalker.y = 0f;
                    look = toTalker.sqrMagnitude > 0.01f ? Mathf.Clamp(Mathf.DeltaAngle(a.yaw, Quaternion.LookRotation(toTalker, Vector3.up).eulerAngles.y), -70f, 70f) : 0f;
                    nod = Mathf.Max(0f, Mathf.Sin(t * 2.6f + a.phase * 2.1f)) * 6f;
                    lean = Mathf.Sin(t * 0.6f + a.phase) * 1.5f;
                }
            }
            a.lookYaw = Mathf.MoveTowards(a.lookYaw, look, 260f * dt);
            var right = a.root.right;
            if (a.spine) a.spine.rotation = Quaternion.AngleAxis(a.spinePitch + lean, right) * a.spine.rotation;
            if (a.head) a.head.rotation = Quaternion.AngleAxis(shake + a.lookYaw, Vector3.up) * Quaternion.AngleAxis(a.headPitch + nod, right) * a.head.rotation;
        }

        /// <summary>The players' own bodies stand in the elevator and are not in the picture (a camera across the docks could see them through its open door): hidden for the Intro.</summary>
        void HidePlayers()
        {
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p) continue;
                foreach (var r in p.GetComponentsInChildren<Renderer>())
                    if (r && r.enabled) { r.enabled = false; hiddenPlayers.Add(r); }
            }
        }

        void UpdateActors(float dt)
        {
            if (actors.Count == 0) return;
            bool talking = t >= IntroRules.Beats[WorkerBeat].start + TurnAfter && speakerAt != listenerAt && speakerAt >= 0 && listenerAt >= 0;
            float faceTv = Quaternion.LookRotation(-tv.Forward, Vector3.up).eulerAngles.y;
            // how they look: talking in the circle while the TV is dark, as they are while the boss is friendly, crushed once he takes the bonuses, angry when it is over
            // (the TV is dark and they talk, then they are as they are until the bonus line has been HEARD, then crushed, then angry: IntroRules.MoodAt)
            int mood = IntroRules.MoodAt(t);
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                // where they are: in the circle until the TV comes on, then on the walk to their place in the line, then in it (all from the clock: every machine shows the same, and a skip forward in the tests lands right)
                float walked = IntroRules.WalkProgress(t, a.departAt, a.metres);
                bool walking = walked > 0f && walked < 1f;
                a.foot = Vector3.Lerp(a.ringFoot, a.lineFoot, walked);
                a.root.position = a.foot + Vector3.up;
                // where they face: in to the middle of the circle while they talk; round to the TV when it comes on (the nearest one first); the way they walk; the TV again in the line; the two who speak at the end turn to each other
                float target;
                bool turning = false;
                if (t < IntroRules.TvOnAt + a.noticeDelay) target = a.ringYaw;
                else if (walking) target = a.walkYaw;
                else if (walked >= 1f) target = faceTv;
                else { var toTv = tv.Origin - a.foot; toTv.y = 0f; target = toTv.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toTv, Vector3.up).eulerAngles.y : faceTv; }
                if (talking && (i == speakerAt || i == listenerAt))
                {
                    turning = true;
                    var toOther = actors[i == speakerAt ? listenerAt : speakerAt].foot - a.foot; toOther.y = 0f;
                    if (toOther.sqrMagnitude > 0.01f) target = Quaternion.LookRotation(toOther, Vector3.up).eulerAngles.y;
                }
                int face = mood <= 0 ? a.ownFace : mood == 1 ? 3 : 2;
                if (a.party && a.shownFace != face) { a.shownFace = face; PartyCharacterFactory.SetFace(a.anim, face); }
                a.yaw = Mathf.MoveTowardsAngle(a.yaw, target, (turning ? 300f : walking ? 420f : walked >= 1f ? 300f : 240f) * dt);
                a.root.rotation = Quaternion.Euler(0f, a.yaw, 0f);
                if (!a.party) Pose(a, i, mood, dt);
                if (a.anim) a.anim.Locomote(walking, 0.85f);
            }
            if (CircleVoices) Murmur();
        }

        /// <summary>The murmur of the circle: soft blips for whoever's turn it is to talk (a pitch of their own), with a breath between the turns, until a moment before the TV clicks on.</summary>
        void Murmur()
        {
            if (!voice || blips == null || t < 0.3f || t > IntroRules.TvOnAt - 0.25f || Time.unscaledTime < nextMurmur) return;
            float into = t - Mathf.Floor(t / IntroRules.TalkTurnSeconds) * IntroRules.TalkTurnSeconds;
            if (into > IntroRules.TalkTurnSeconds - 0.25f) { nextMurmur = Time.unscaledTime + 0.2f; return; }
            nextMurmur = Time.unscaledTime + Random.Range(0.13f, 0.24f);
            if (Random.value < 0.2f) return;                                                    // (not a blip every time: it should sound like words)
            voice.pitch = (1.0f + 0.11f * IntroRules.TalkerAt(t, actors.Count)) * Random.Range(0.94f, 1.08f);
            voice.PlayOneShot(blips[Random.Range(0, blips.Length)], 0.11f);
        }

        // ------------------------------------------------------------------ each frame

        /// <summary>16:9 asks for a different vertical angle than 2.39:1 to show the same width.</summary>
        float TvFov(float fovForFilm) => tv == null ? fovForFilm : 2f * Mathf.Atan(Mathf.Tan(fovForFilm * 0.5f * Mathf.Deg2Rad) * (Aspect / (16f / 9f))) * Mathf.Rad2Deg;

        void LateUpdate()
        {
            if (state != State.Playing || office == null || !officeCam) return;
            int bi = t < 0f ? 0 : IntroRules.BeatAt(t);
            if (bi < 0) bi = IntroRules.Beats.Length - 1;
            var beat = IntroRules.Beats[bi];
            float p = IntroRules.Progress(beat, t);

            // ---- the TV: dark while the workers hang out, static (and a click) for a moment when it comes on, then the boss's office while the monologue runs, then static again
            if (beat.shot == IntroShot.Off)
            {
                office.SetJet(-1f);
                if (tv != null) { tv.ShowOff(); StopHiss(); }
            }
            else if (beat.shot != IntroShot.None)
            {
                var rig = Rigs[(int)beat.shot];
                var o = office.root.position;
                officeCam.transform.position = o + Vector3.Lerp(rig.posA, rig.posB, p);
                officeCam.transform.rotation = Quaternion.LookRotation(o + Vector3.Lerp(rig.lookA, rig.lookB, p) - officeCam.transform.position, Vector3.up);
                officeCam.fieldOfView = TvFov(Mathf.Lerp(rig.fovA, rig.fovB, p));
                float jet = (t - IntroRules.JetStart) / IntroRules.JetSeconds;
                office.SetJet(jet);
                if (jet >= 0f && !whooshed && voice) { whooshed = true; voice.pitch = 1f; voice.PlayOneShot(whoosh, 0.22f); }          // (the plane: it was 0.8, too loud; the owner, 2026-10-20)
                RenderOffice();
                if (tv != null)
                {
                    if (t < IntroRules.TvOnAt + IntroRules.TvStaticSeconds)
                    {
                        if (!tvClicked && voice && blips != null) { tvClicked = true; voice.pitch = 0.5f; voice.PlayOneShot(blips[0], 0.6f); }          // (the click of a TV coming on)
                        tv.ShowStatic(); StartHiss();
                    }
                    else { StopHiss(); if (!tv.OnProgram) tv.ShowProgram(tvRt); }
                }
            }
            else
            {
                office.SetJet(-1f);
                if (tv != null) { tv.ShowStatic(); StartHiss(); }
            }

            // ---- the vendor shows up: after the workers' line the roller door of his bay rolls up (the owner, 2026-10-19 night)
            if (beat.dock == DockShot.Bay && t >= IntroRules.BayOpensAt) OpenBay();

            // ---- the words: the boss's head and shoulders move while a line is being typed, and the voices blip
            int chars = t < 0f ? 0 : IntroRules.VisibleChars(beat.line, t - beat.start);
            bool speaking = !string.IsNullOrEmpty(beat.line) && chars > 0 && chars < beat.line.Length;
            bool bossSpeaking = speaking && beat.speaker == Speaker.Boss;
            if (bi != lastBeat) { lastBeat = bi; lastChars = 0; }
            if (speaking && chars != lastChars && voice)
            {
                char c = beat.line[Mathf.Clamp(chars - 1, 0, beat.line.Length - 1)];
                if (c != ' ' && chars % 2 == 0)
                {
                    voice.pitch = beat.speaker == Speaker.Worker ? Random.Range(1.3f, 1.65f) : beat.speaker == Speaker.Vendor ? Random.Range(0.72f, 0.86f) : Random.Range(0.92f, 1.12f);
                    voice.PlayOneShot(blips[Random.Range(0, blips.Length)], beat.speaker == Speaker.Worker ? 0.45f : 0.4f);
                }
            }
            lastChars = chars;
            float a = bossSpeaking ? 1f : 0.25f, s = Time.time;
            if (office.head) office.head.localRotation *= Quaternion.Euler(Mathf.Sin(s * 9f) * 2.2f * a + Mathf.Sin(s * 2.1f) * 1.5f, Mathf.Sin(s * 1.7f) * 5f * a, Mathf.Sin(s * 1.3f) * 2.5f * a);
            if (office.spine) office.spine.localRotation *= Quaternion.Euler(Mathf.Sin(s * 1.1f) * 1.2f, 0f, Mathf.Sin(s * 0.8f) * 1.5f);

            // ---- the docks: the workers and the camera
            if (tv != null && dockCam)
            {
                UpdateActors(Time.unscaledDeltaTime);
                PlaceDockCamera(beat, p);
            }
        }

        /// <summary>Films the office into the TV's texture with the office's own light (the docks' ambient light is the game's; it is put back at once).</summary>
        void RenderOffice()
        {
            var mode = RenderSettings.ambientMode; var colour = RenderSettings.ambientLight; float intensity = RenderSettings.ambientIntensity; bool fog = RenderSettings.fog;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.50f, 0.50f, 0.54f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            officeCam.Render();
            RenderSettings.ambientMode = mode; RenderSettings.ambientLight = colour; RenderSettings.ambientIntensity = intensity; RenderSettings.fog = fog;
        }

        void PlaceDockCamera(in IntroBeat beat, float p)
        {
            Vector3 pos, look; float fov;
            var rig = DockRigs[(int)beat.dock];
            if (beat.dock == DockShot.TwoShot && speakerAt >= 0 && listenerAt >= 0 && speakerAt != listenerAt)
            {
                // the two who talk, from the TV's side of them (so their faces are seen as they turn to each other), a little way off the middle line
                var mid = (actors[speakerAt].foot + actors[listenerAt].foot) * 0.5f;
                var between = actors[speakerAt].foot - actors[listenerAt].foot; between.y = 0f;
                var side = between.sqrMagnitude > 0.01f ? between.normalized : tv.Right;
                pos = mid - tv.Forward * Mathf.Lerp(1.8f, 1.4f, p) + side * 0.35f + Vector3.up * 1.65f;
                look = mid + Vector3.up * 1.5f;
                fov = Mathf.Lerp(rig.fovA, rig.fovB, p);
            }
            else if (beat.dock == DockShot.Bay && dockGen != null)
            {
                // the vendor's bay from inside the docks, pushing in slowly: the roller door in the middle of the picture, the trailer behind it, the vendor at its far end (the owner, 2026-10-19 night)
                var e = dockGen.DockTrailerEntrance;
                float pb = Mathf.Clamp01((t - IntroRules.Beats[IntroRules.DoorBeat].start) / (IntroRules.Total - IntroRules.Beats[IntroRules.DoorBeat].start));          // (one slow push-in over the whole bay scene: the door, then the vendor's four lines)
                pos = e + new Vector3(Mathf.Lerp(-0.9f, -0.3f, pb), Mathf.Lerp(1.6f, 1.55f, pb), Mathf.Lerp(-3.2f, -1.4f, pb));
                look = e + new Vector3(0f, 1.35f, 9.8f);
                fov = Mathf.Lerp(rig.fovA, rig.fovB, pb);
            }
            else
            {
                var a = rig;
                if (beat.dock == DockShot.Faces && actors.Count > 0) a.fovA = a.fovB = 30f + 3.2f * actors.Count;        // (more of them, a wider picture)
                if (beat.dock == DockShot.Establish) p = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(IntroRules.TvOnAt - 1.2f, EstablishSeconds, t));          // (it holds on the circle, then eases over to the Behind set-up from a second before the TV comes on to the end of the TV's first beat)
                pos = tv.Point(Mathf.Lerp(a.posA.x, a.posB.x, p), Mathf.Lerp(a.posA.y, a.posB.y, p), Mathf.Lerp(a.posA.z, a.posB.z, p));
                look = tv.Point(Mathf.Lerp(a.lookA.x, a.lookB.x, p), Mathf.Lerp(a.lookA.y, a.lookB.y, p), Mathf.Lerp(a.lookA.z, a.lookB.z, p));
                fov = Mathf.Lerp(a.fovA, a.fovB, p);
            }
            dockCam.transform.position = pos;
            dockCam.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
            dockCam.fieldOfView = fov;
        }

        void StopHiss() { if (hiss && hiss.isPlaying) hiss.Pause(); }

        void StartHiss()
        {
            if (hiss) { if (!hiss.isPlaying) hiss.UnPause(); return; }
            hiss = gameObject.AddComponent<AudioSource>();
            hiss.spatialBlend = 0f; hiss.loop = true; hiss.volume = 0.1f;
            hissClip = MakeHiss();
            hiss.clip = hissClip;
            hiss.Play();
        }

        // ------------------------------------------------------------------ the screen

        void OnGUI()
        {
            var picture = rt ? rt : tvRt;
            if (state != State.Playing || !picture) return;
            GUI.depth = -10000;                                        // above every other screen
            if (Event.current.type != EventType.Repaint) return;
            float W = Screen.width, H = Screen.height;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            float ph = Mathf.Min(H, W / Aspect);
            var pic = new Rect(0f, (H - ph) * 0.5f, W, ph);
            GUI.color = Color.white;
            GUI.DrawTexture(pic, picture, ScaleMode.StretchToFill, false);
            float fade = IntroRules.Fade(t);
            if (fade < 1f) { GUI.color = new Color(0f, 0f, 0f, 1f - fade); GUI.DrawTexture(pic, Texture2D.whiteTexture); GUI.color = Color.white; }

            // the letterbox bars: the subtitles sit in the lower one and the skip meter in the upper one when the bars are tall enough (so nothing covers the
            // picture); on a screen so wide that there are no bars they lie over the picture, on a dark plate
            float barH = (H - ph) * 0.5f, plateH = H * 0.125f;
            bool lowerBar = barH >= plateH;

            // subtitles: who speaks, then the line typed out
            int bi = IntroRules.BeatAt(t);
            if (bi >= 0 && !string.IsNullOrEmpty(IntroRules.Beats[bi].line))
            {
                var beat = IntroRules.Beats[bi];
                string typed = beat.line.Substring(0, IntroRules.VisibleChars(beat.line, t - beat.start));
                bool worker = beat.speaker == Speaker.Worker;
                bool vendor = beat.speaker == Speaker.Vendor;
                string who = vendor ? IntroContent.VendorLabel : worker && speakerAt >= 0 && speakerAt < actors.Count ? actors[speakerAt].label : worker ? IntroContent.CoworkerLabel : IntroContent.BossLabel;
                var plate = new Rect(pic.x + W * 0.10f, lowerBar ? pic.yMax + (barH - plateH) * 0.5f : pic.yMax - plateH - H * 0.03f, W * 0.80f, plateH);
                if (!lowerBar) { GUI.color = new Color(0f, 0f, 0f, 0.55f * Mathf.Min(1f, fade * 2f)); GUI.DrawTexture(plate, Texture2D.whiteTexture); GUI.color = Color.white; }
                int big = Mathf.Max(14, Mathf.RoundToInt(H * 0.036f)), small = Mathf.Max(12, Mathf.RoundToInt(H * 0.022f));
                HudText.Label(new Rect(plate.x + 18f, plate.y + 6f, plate.width - 36f, small * 1.5f), who, HudStyles.Label(small, TextAnchor.UpperLeft, FontStyle.Bold, worker ? HudTheme.Info : vendor ? HudTheme.Cash : HudTheme.Quota));
                var body = new Rect(plate.x + 18f, plate.y + small * 1.5f + 8f, plate.width - 36f, plate.height - small * 1.5f - 14f);
                HudText.Label(new Rect(body.x + 2f, body.y + 2f, body.width, body.height), typed, HudStyles.Label(big, TextAnchor.UpperLeft, FontStyle.Bold, Color.black));
                HudText.Label(body, typed, HudStyles.Label(big, TextAnchor.UpperLeft, FontStyle.Bold, HudTheme.Cream));
            }

            // the skip meter: HOLD SPACE TO SKIP, a bar that fills while it is held, and in a party how many are holding
            var sync = RunSync.Instance;
            int party = sync != null ? sync.IntroParty : 1;
            int needed = IntroRules.NeededToSkip(party);
            float boxH = H * 0.075f;
            var box = new Rect(pic.xMax - W * 0.235f, barH >= boxH * 1.25f ? (barH - boxH) * 0.5f : pic.y + H * 0.03f, W * 0.215f, boxH);
            GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(box, Texture2D.whiteTexture); GUI.color = Color.white;
            int hint = Mathf.Max(12, Mathf.RoundToInt(H * 0.021f));
            string hintText = voting ? IntroContent.SavedHint : IntroContent.SkipHint;
            string line = party > 1 && sync != null ? $"{hintText}   {Mathf.Min(sync.IntroHolders, needed)} / {needed}" : hintText;
            HudText.Label(new Rect(box.x, box.y + 4f, box.width, box.height * 0.55f), line, HudStyles.Label(hint, TextAnchor.MiddleCenter, FontStyle.Bold, HudTheme.Cream));
            var bar = new Rect(box.x + 12f, box.yMax - box.height * 0.28f, box.width - 24f, box.height * 0.14f);
            GUI.color = new Color(1f, 1f, 1f, 0.25f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = voting ? HudTheme.Cash : HudTheme.Quota; GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * (voting ? 1f : Mathf.Clamp01(held / IntroRules.HoldSeconds)), bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void Finish()
        {
            if (state == State.Done) return;
            state = State.Done;
            Cleanup();
            if (Current == this) Current = null;
            Destroy(gameObject);
        }

        bool bayShut;
        FloorGenerator dockGen;

        /// <summary>The roller door of the vendor's bay comes down for the cutscene (the owner, 2026-10-17: "the vendor should not be visible ... the loading dock door should still be shut").</summary>
        void CloseBay()
        {
            var bay = DockBayDoor.Current;
            if (bay) { bay.Close(); bayShut = true; }
        }

        void OpenBay()
        {
            if (!bayShut) return;
            bayShut = false;
            var bay = DockBayDoor.Current;
            if (bay) bay.Open();
        }

        void Cleanup()
        {
            OpenBay();
            if (voting)
            {
                voting = false;
                var sync = RunSync.Instance;
                if (sync != null && sync.IsSpawned) sync.RequestIntroHold(false);
            }
            if (shown) { shown = false; OutpostUi.Hide(OutpostUi.Kind.Intro); }
            foreach (var r in hiddenPlayers) if (r) r.enabled = true;
            hiddenPlayers.Clear();
            foreach (var a in actors) if (a.root) Destroy(a.root.gameObject);
            actors.Clear();
            if (tv != null) tv.ShowStandby();
            if (officeCam) Destroy(officeCam.gameObject);
            if (dockCam) Destroy(dockCam.gameObject);
            if (rt) { rt.Release(); Destroy(rt); rt = null; }
            if (tvRt) { tvRt.Release(); Destroy(tvRt); tvRt = null; }
            if (office != null) { office.Destroy(); office = null; }
            if (blips != null) { foreach (var b in blips) if (b) Destroy(b); blips = null; }
            if (whoosh) { Destroy(whoosh); whoosh = null; }
            if (hissClip) { Destroy(hissClip); hissClip = null; }
            if (hiss) { hiss.Stop(); Destroy(hiss); hiss = null; }
        }

        void OnDestroy()
        {
            Cleanup();
            if (Current == this) Current = null;
        }

        // ------------------------------------------------------------------ the sounds, made from numbers (the boss "talks" in short blips; a jet whooshes past; the TV hisses)

        static AudioClip[] MakeBlips()
        {
            var freqs = new[] { 118f, 142f, 101f, 165f };
            var list = new AudioClip[freqs.Length];
            for (int k = 0; k < freqs.Length; k++)
            {
                int n = (int)(44100 * 0.06f);
                var d = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float x = i / 44100f, env = Mathf.Sin(Mathf.PI * i / n);
                    env *= env;
                    float s = Mathf.Sin(6.2831853f * freqs[k] * x) * 0.55f + Mathf.Sin(6.2831853f * freqs[k] * 2.02f * x) * 0.25f + Mathf.Sin(6.2831853f * freqs[k] * 3.1f * x) * 0.10f;
                    d[i] = s * env * 0.8f;
                }
                list[k] = AudioClip.Create("IntroBlip" + k, n, 1, 44100, false);
                list[k].SetData(d, 0);
            }
            return list;
        }

        static AudioClip MakeWhoosh()
        {
            const int rate = 44100;
            int n = rate * 22 / 10;
            var d = new float[n];
            var rnd = new System.Random(5);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n, env = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.7f)), cutoff = Mathf.Lerp(0.02f, 0.25f, Mathf.Sin(Mathf.PI * u));
                lp += ((float)(rnd.NextDouble() * 2.0 - 1.0) - lp) * cutoff;
                d[i] = Mathf.Clamp(lp * env * 2.4f, -1f, 1f);
            }
            var c = AudioClip.Create("IntroWhoosh", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }

        /// <summary>A second of the hiss of a TV with no signal (it loops).</summary>
        static AudioClip MakeHiss()
        {
            const int rate = 22050;
            var d = new float[rate];
            var rnd = new System.Random(9);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                lp += ((float)(rnd.NextDouble() * 2.0 - 1.0) - lp) * 0.6f;
                d[i] = lp * 0.5f;
            }
            var c = AudioClip.Create("TvHiss", d.Length, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
