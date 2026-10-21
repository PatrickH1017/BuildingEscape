# Round 12, batch G53: the Intro's opening (the owner, 2026-10-20: "In the beginning of the cinematic when the characters are all there it should have them more spread out they don't all need to be right by the tv ...
# or better yet have them all in the break room in a circle talking then the tv plays and then they all line up to watch it"): the five workers stand in a CIRCLE in the east half of the break room and talk (a different one
# every 1.4 s, the others look at them and nod, soft blips for voices); when the TV comes on they look round at it, walk to their places in the line in front of it (the walks end one after the other, the shortest first, all
# five in the line as the boss starts) and watch. The opening is 2.2 s longer (the TV comes on at 5.4 s, not 3.2 s): every later time moves by 2.2 s (IntroRules). Pure rules: IntroRules (TalkerAt, DepartAt, WalkProgress).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
# a region replace whose anchors may span lines (they are made to match the file's line ends)
function RepRegionN([string]$startAnchor, [string]$endAnchor, [string]$new) {
  $sa = $startAnchor.Replace("`r`n", "`n").Replace("`n", $script:eol)
  $ea = $endAnchor.Replace("`r`n", "`n").Replace("`n", $script:eol)
  RepRegion $sa $ea $new
}

# ---------------------------------------------------------------- the pure rules
Open-Src 'C:\bqm\Assets\Scripts\Intro\IntroRules.cs'
Rep '/// <summary>The whole docks from across the room: the workers in the corner, the TV on the wall.</summary>' '/// <summary>The break room from its north-west corner: the workers in a circle talking, the TV dark on the wall beyond them; the same shot goes on (one slow move) while the TV comes on and they walk to their places in the line in front of it (the owner, 2026-10-20).</summary>'
Rep 'hang out in the break room with the wall-mounted TV dark; it comes on for the announcement, they turn to it and watch the' 'stand in a circle in the break room talking, with the wall-mounted TV dark; it comes on for the announcement, they look round at it, line up in front of it and watch the'
Rep 'the workers hanging out (3.2 s), the TV coming on, the monologue on it (27 s)' 'the workers talking in a circle (5.4 s), the TV coming on and the workers lining up in front of it, the monologue on it (27 s)'
Rep 'public const float Total = 55.4f;' 'public const float Total = 57.6f;'
Rep '(before it the workers hang out and it is dark), shows static for <see cref="TvStaticSeconds"/> and then the picture; the workers turn to it, one after the other.</summary>' '(before it the workers stand in a circle talking and it is dark), shows static for <see cref="TvStaticSeconds"/> and then the picture; the workers look round at it, one after the other, and walk to their places in the line in front of it (the owner, 2026-10-20).</summary>'
Rep 'public const float TvOnAt = 3.2f;' 'public const float TvOnAt = 5.4f;'
Rep 'public const float ProgramEnd = 30.2f;' 'public const float ProgramEnd = 32.4f;'
Rep 'public const float JetStart = 21.6f, JetSeconds = 4.6f;' 'public const float JetStart = 23.8f, JetSeconds = 4.6f;'
Rep 'public const float BayOpensAt = 37.7f;' 'public const float BayOpensAt = 39.9f;'
Rep 'The timeline: fifteen beats, 55.4 s.' 'The timeline: fifteen beats, 57.6 s.'
Rep '-1 hanging out (the TV is not on yet)' '-1 talking in the circle (the TV is not on yet)'
RepRegion '            new IntroBeat(0.0f, 3.2f, IntroShot.Off, DockShot.Establish, null, Speaker.None),' '            new IntroBeat(51.4f, 55.4f, IntroShot.None, DockShot.Bay, IntroContent.VendorLine4, Speaker.Vendor),' @'
            new IntroBeat(0.0f, 5.4f, IntroShot.Off, DockShot.Establish, null, Speaker.None),
            new IntroBeat(5.4f, 7.4f, IntroShot.Wide, DockShot.Establish, null, Speaker.None),
            new IntroBeat(7.4f, 10.9f, IntroShot.Medium, DockShot.Behind, IntroContent.Line1, Speaker.Boss),
            new IntroBeat(10.9f, 15.9f, IntroShot.MediumClose, DockShot.TvClose, IntroContent.Line2, Speaker.Boss),
            new IntroBeat(15.9f, 20.4f, IntroShot.Desk, DockShot.Faces, IntroContent.Line3, Speaker.Boss),
            new IntroBeat(20.4f, 23.4f, IntroShot.Medium, DockShot.TvClose, IntroContent.Line4, Speaker.Boss),
            new IntroBeat(23.4f, 28.4f, IntroShot.Window, DockShot.TvClose, IntroContent.Line5, Speaker.Boss),
            new IntroBeat(28.4f, 32.4f, IntroShot.Low, DockShot.Faces, IntroContent.Line6, Speaker.Boss),
            new IntroBeat(32.4f, 34.2f, IntroShot.None, DockShot.Behind, null, Speaker.None),
            new IntroBeat(34.2f, 39.4f, IntroShot.None, DockShot.TwoShot, IntroContent.FinalLine, Speaker.Worker),
            new IntroBeat(39.4f, 41.8f, IntroShot.None, DockShot.Bay, null, Speaker.None),
            new IntroBeat(41.8f, 44.3f, IntroShot.None, DockShot.Bay, IntroContent.VendorLine1, Speaker.Vendor),
            new IntroBeat(44.3f, 47.4f, IntroShot.None, DockShot.Bay, IntroContent.VendorLine2, Speaker.Vendor),
            new IntroBeat(47.4f, 53.6f, IntroShot.None, DockShot.Bay, IntroContent.VendorLine3, Speaker.Vendor),
            new IntroBeat(53.6f, 57.6f, IntroShot.None, DockShot.Bay, IntroContent.VendorLine4, Speaker.Vendor),
'@
Rep 'public static float Spacing(int cast) => cast <= 3 ? 1.2f : 1.0f;' @'
public static float Spacing(int cast) => cast <= 3 ? 1.2f : 1.0f;

        // ------------------------------------------------------------------ the opening: a circle talking, then a line in front of the TV

        /// <summary>
        /// The opening (the owner, 2026-10-20: "have them all in the break room in a circle talking then the tv plays and then they all line up to watch it"): the workers stand in a RING of this radius (m), facing its middle, and talk - a
        /// different one every <see cref="TalkTurnSeconds"/>, the others looking at whoever talks. When the TV comes on they look round at it and set off for their places in the line in front of it.
        /// </summary>
        public const float RingRadius = 0.9f, TalkTurnSeconds = 1.4f;
        /// <summary>
        /// How fast they walk to the line (m/s), how long after the TV comes on the first of them sets off (they look round at it first), and how far apart the walks END (the shortest ends first, from <see cref="FirstArrival"/> on, so the
        /// longest set off first and they come into the line one after the other as the boss starts).
        /// </summary>
        public const float WalkSpeed = 1.6f, NoticeSeconds = 0.3f, ArrivalGap = 0.15f;
        public const float FirstArrival = TvOnAt + 1.5f;

        /// <summary>Who is talking in the circle at this time (an index into the workers): each takes a turn, in an order that goes round all of them before it comes back to the first.</summary>
        public static int TalkerAt(float t, int workers)
        {
            if (workers <= 1) return 0;
            int step = 2;
            while (Gcd(step, workers) != 1) step++;
            int turn = (int)Math.Floor(Math.Max(0f, t) / TalkTurnSeconds);
            return (int)(((long)turn * step) % workers);
        }

        static int Gcd(int a, int b) { while (b != 0) { int r = a % b; a = b; b = r; } return a; }

        /// <summary>How long a walk of <paramref name="metres"/> takes (s).</summary>
        public static float WalkSeconds(float metres) => metres / WalkSpeed;

        /// <summary>
        /// When a worker whose walk to the line is <paramref name="metres"/> long sets off, given how many of the others have a shorter walk than his (<paramref name="shorter"/>): the walks end one after the other from
        /// <see cref="FirstArrival"/> on, the shortest first; nobody sets off before he has had a moment (<see cref="NoticeSeconds"/>) to look round at the TV.
        /// </summary>
        public static float DepartAt(float metres, int shorter) => Math.Max(TvOnAt + NoticeSeconds, FirstArrival + shorter * ArrivalGap - WalkSeconds(metres));

        /// <summary>How far along his walk a worker is at this time: 0 = in the circle, 1 = in his place in the line (a steady walk).</summary>
        public static float WalkProgress(float t, float departAt, float metres) =>
            metres <= 0.001f ? (t >= departAt ? 1f : 0f) : Math.Max(0f, Math.Min(1f, (t - departAt) / WalkSeconds(metres)));
'@
Save-Src

# ---------------------------------------------------------------- the director
Open-Src 'C:\bqm\Assets\Scripts\Intro\IntroCinematic.cs'
Rep 'stand in the corner of the Loading Docks and watch the boss tell them' 'talk in a circle in the break room of the Loading Docks until the TV comes on, then line up in front of it and watch the boss tell them'
Rep 'public float WorkerYaw(int i) => i >= 0 && i < actors.Count ? actors[i].yaw : 0f;' @'
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
'@
Rep '/* Establish */ new Rig(new Vector3(-3.0f, 2.7f, 4.0f), new Vector3(-2.4f, 2.6f, 3.7f), new Vector3(0.4f, 1.5f, 0.9f), new Vector3(0.3f, 1.6f, 0.8f), 66f, 62f),' '/* Establish */ new Rig(new Vector3(-3.2f, 2.3f, 3.9f), new Vector3(-2.7f, 2.2f, 3.7f), new Vector3(1.1f, 1.2f, 2.3f), new Vector3(1.0f, 1.25f, 2.2f), 62f, 56f),          // (the break room from its north-west corner: the circle on the right, the TV on the wall beyond; one move over the two beats of the opening)'
Rep 'const float DockWorkerOut = 2.0f;' @'
const float DockWorkerOut = 2.0f;

        /// <summary>
        /// Where the circle of workers stands, in the TV's frame (the owner, 2026-10-20: "have them all in the break room in a circle talking then the tv plays and then they all line up to watch it"): 1.3 m east of the TV's middle and 3.2 m
        /// out from its wall, in the free east half of the break room (the counters, the fridge and the desk are on its west side).
        /// </summary>
        const float RingSide = 1.3f, RingOut = 3.2f;
        /// <summary>The Establish shot lasts the dark TV and the TV coming on (two beats): the camera makes one move across both.</summary>
        static readonly float EstablishSeconds = IntroRules.Beats[IntroRules.TvOnBeat].end;
        /// <summary>The circle talks in soft blips (the Intro's voices, quietly: whoever's turn it is, in a pitch of their own) until a moment before the TV comes on; false = silent.</summary>
        static readonly bool CircleVoices = true;
'@
RepRegionN @'
        sealed class Actor
'@ @'
            public float headPitch, spinePitch;
        }
'@ @'
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
'@
Rep 'int speakerAt = -1, listenerAt = -1;' @'
int speakerAt = -1, listenerAt = -1;
        Vector3 ringCentre;
        float nextMurmur;
'@
Rep 'Stand-ins dressed as the players'' characters, in a row in front of the TV: the same on every machine' 'Stand-ins dressed as the players'' characters, in a circle talking and then in a row in front of the TV: the same on every machine'
RepRegionN '            var row = IntroRules.RowOrder(cast.Count, speaker, listener);' @'
                a.root.rotation = Quaternion.Euler(0f, a.yaw, 0f);
            }
        }
'@ @'
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
'@
RepRegionN '        void Pose(Actor a, int mood, float dt)' @'
            if (a.head) a.head.rotation = Quaternion.AngleAxis(shake + sway, Vector3.up) * Quaternion.AngleAxis(a.headPitch + nod, right) * a.head.rotation;
        }
'@ @'
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
'@
RepRegionN '        void UpdateActors(float dt)' @'
                if (a.anim) a.anim.Locomote(false);
            }
        }
'@ @'
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
                if (a.anim) a.anim.Locomote(walking, 0.75f);
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
'@
Rep 'if (beat.dock == DockShot.Faces && actors.Count > 0) a.fovA = a.fovB = 30f + 3.2f * actors.Count;        // (more of them, a wider picture)' @'
if (beat.dock == DockShot.Faces && actors.Count > 0) a.fovA = a.fovB = 30f + 3.2f * actors.Count;        // (more of them, a wider picture)
                if (beat.dock == DockShot.Establish) p = Mathf.Clamp01(t / EstablishSeconds);                           // (one move across the dark TV and the TV coming on)
'@
Save-Src

# ---------------------------------------------------------------- the tests
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevIntroTest.cs'
Rep 'the beats follow one another from 0 to the total (55.4 s) with no gap or overlap", contiguous && longEnough && IntroRules.Total == 55.4f && beats.Length == 15' 'the beats follow one another from 0 to the total (57.6 s) with no gap or overlap", contiguous && longEnough && IntroRules.Total == 57.6f && beats.Length == 15'
Rep 'the TV is dark for 3.2 s while the five workers hang out, then it comes on' 'the TV is dark for 5.4 s while the five workers talk in a circle, then it comes on'
Rep 'until 30.2 s, then static' 'until 32.4 s, then static'
Rep 'IntroRules.ProgramEnd == 30.2f' 'IntroRules.ProgramEnd == 32.4f'
Rep 'IntroRules.BeatAt(3.2f) == 1 && IntroRules.BeatAt(5.2f) == 2 && IntroRules.BeatAt(55.39f) == beats.Length - 1' 'IntroRules.BeatAt(5.4f) == 1 && IntroRules.BeatAt(7.4f) == 2 && IntroRules.BeatAt(57.59f) == beats.Length - 1'
Rep 'IntroRules.BeatAt(IntroRules.Total) == -1 && IntroRules.BeatAt(32.1f) == 9);' 'IntroRules.BeatAt(IntroRules.Total) == -1 && IntroRules.BeatAt(34.3f) == 9);'
Rep @'
                && (5 - 1) * IntroRules.Spacing(5) <= 4.5f);
'@ @'
                && (5 - 1) * IntroRules.Spacing(5) <= 4.5f);

            // the opening: a circle talking, then a line in front of the TV (the owner, 2026-10-20)
            var talkers = new System.Collections.Generic.HashSet<int>();
            bool turnsChange = true;
            for (int turn = 0; turn < 10; turn++)
            {
                int tk = IntroRules.TalkerAt((turn + 0.5f) * IntroRules.TalkTurnSeconds, 5);
                talkers.Add(tk);
                if (turn > 0) turnsChange &= tk != IntroRules.TalkerAt((turn - 0.5f) * IntroRules.TalkTurnSeconds, 5);
            }
            Check("intro: in the circle everybody takes a turn to talk (a different one every 1.4 s, all five in five turns)",
                talkers.Count == 5 && turnsChange && IntroRules.TalkerAt(0f, 5) == IntroRules.TalkerAt(0.5f, 5) && IntroRules.TalkerAt(-3f, 5) == IntroRules.TalkerAt(0f, 5) && IntroRules.TalkerAt(7f, 1) == 0);
            bool walksOk = true; float lastArrive = 0f;
            for (int shorter = 0; shorter < 5; shorter++)
            {
                float metres = 0.5f + shorter * 0.6f;               // (0.5 to 2.9 m: the more walks are shorter than theirs, the longer theirs)
                float dep = IntroRules.DepartAt(metres, shorter), arrive = dep + IntroRules.WalkSeconds(metres), halfWay = IntroRules.WalkProgress((dep + arrive) * 0.5f, dep, metres);
                walksOk &= dep >= IntroRules.TvOnAt + IntroRules.NoticeSeconds - 1e-4f && IntroRules.WalkProgress(dep - 0.01f, dep, metres) == 0f && IntroRules.WalkProgress(arrive, dep, metres) > 0.999f && halfWay > 0.49f && halfWay < 0.51f;
                lastArrive = System.Math.Max(lastArrive, arrive);
            }
            Check("intro: when the TV comes on nobody sets off before they have looked round at it, a walk is steady from set-off to arrival, and all five are in the line within half a second of the boss's first word",
                walksOk && lastArrive < beats[IntroRules.FirstBossBeat].start + 0.5f, $"(the last one arrives at {lastArrive:0.00} s, the boss starts at {beats[IntroRules.FirstBossBeat].start:0.0} s)");
'@
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevEtr.cs'
RepRegion '            // the opening (the owner, 2026-10-20): the TV is dark and they hang out, some turned to each other, not to the wall' '            yield return Snap("intro_tv_opening_workers");' @'
            // the opening (the owner, 2026-10-20: "have them all in the break room in a circle talking then the tv plays and then they all line up to watch it"): the TV is dark and the five stand in a circle in the break room, turned in to its middle
            intro.TestSeek(3.0f);
            yield return new WaitForSeconds(0.5f);
            var ringMid = intro.RingCentre;
            bool ringOk = !tv.OnProgram && intro.WorkerCount == IntroRules.Workers;
            for (int i = 0; i < intro.WorkerCount; i++)
            {
                var f0 = intro.WorkerFoot(i);
                var inward = ringMid - f0; inward.y = 0f;
                ringOk &= Mathf.Abs(inward.magnitude - IntroRules.RingRadius) < 0.05f && Mathf.Abs(Mathf.DeltaAngle(intro.WorkerYaw(i), Quaternion.LookRotation(inward, Vector3.up).eulerAngles.y)) < 15f && gen.BreakRoom.Contains(new Vector2(f0.x, f0.z));
            }
            EtrCheck("tv: before the announcement the TV is dark and the five workers stand in a circle in the break room, turned in to its middle", ringOk);
            yield return Snap("intro_tv_opening_workers");
            intro.TestSeek(1.6f);
            yield return new WaitForSeconds(0.4f);
            yield return Snap("intro_tv_opening_talk");
            // the TV comes on: they look round at it and set off, each from its spot in the circle to its place in the line in front of the TV
            intro.TestSeek(IntroRules.TvOnAt + 0.8f);
            yield return new WaitForSeconds(0.25f);
            int onTheWay = 0;
            for (int i = 0; i < intro.WorkerCount; i++) if (intro.WorkerWalking(i)) onTheWay++;
            EtrCheck("tv: when the TV comes on they set off for the line in front of it: most of them are on the walk a moment later", tv.OnProgram && onTheWay >= 3, $"({onTheWay} of {intro.WorkerCount} walking)");
            yield return Snap("intro_tv_gather_walking");
            intro.TestSeek(IntroRules.TvOnAt + 1.5f);
            yield return new WaitForSeconds(0.25f);
            yield return Snap("intro_tv_gather_arriving");
'@
Save-Src
Write-Output 'round 12 batch G53 patched'
