# Round 12, batch G26 (the owner, 2026-10-20, after seeing the docks in the build: a picture of the hallway outside the break room with a red circle on its end wall; "The dart board and tape should be in this hallway ..., where it was
# originally, the hallway should just be cleaned up a bit (leave the shelves against the wall), also when a player presses to throw the darts they should get locked into the position at the center of the tape but there should be able
# to move left and right and turn a bit like Wii bowling"; "When a player is throwing darts it should show the keybinds on the screen for moving side to side and for turning the camera angle slightly"):
#  (1) the dart board hangs on the end wall of the hallway outside the break room (the wall between the two halves of the docks, straight across from the break room's doorway); the tape and the scoreboard go with it;
#  (2) the hallway's floor is cleared (no boxes, pallets, drums or forklift; the steel shelves against its walls stay);
#  (3) a game holds the player at the middle of the tape: A / D slide him along it (+-0.45 m), the mouse turns him +-25 degrees (and a little up / down), no walking, jumping, sprinting or crouching; E leaves;
#  (4) the keys are shown on the screen while the game is on; (5) protocol 25 (the docks are built differently). BE_qa (= C:\bqm) only.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'

# ---------------------------------------------------------------- the stance: numbers and the keys shown (pure)
Open-Src "$A\Darts\DartsRules.cs"
Rep @'
        /// <summary>The regulation distance from the board's face to the throw line (the oche): 7 ft 9.25 in = 2.37 m (the owner, 2026-10-19 night: a line on the ground that looks like tape, where the player stands to throw).</summary>
'@ @'
        // the stance at the line (the owner, 2026-10-20: "when a player presses to throw the darts they should get locked into the position at the center of the tape but there should be able to move left and right and turn a bit like Wii bowling")
        /// <summary>The player is held at the middle of the tape, this far behind it, and can slide this far to each side of the middle (metres; the tape is a metre long) at this speed (m/s); the aim can turn this many degrees either side of the board and tilt between these.</summary>
        public const float StandBehind = 0.3f, LateralHalf = 0.45f, LateralSpeed = 1.6f, TurnHalf = 25f, PitchMin = -8f, PitchMax = 24f;

        /// <summary>The slide: where the player is along the tape after <paramref name="dt"/> seconds of <paramref name="input"/> (-1 left .. 1 right).</summary>
        public static float Lateral(float current, float input, float dt) =>
            Math.Max(-LateralHalf, Math.Min(LateralHalf, current + Math.Max(-1f, Math.Min(1f, input)) * LateralSpeed * dt));

        /// <summary>The turn: <paramref name="yaw"/> kept within <see cref="TurnHalf"/> degrees of <paramref name="centreYaw"/> (the way to the board).</summary>
        public static float ClampAim(float centreYaw, float yaw)
        {
            float d = ((yaw - centreYaw + 540f) % 360f + 360f) % 360f - 180f;
            return centreYaw + Math.Max(-TurnHalf, Math.Min(TurnHalf, d));
        }

        /// <summary>The keys shown on the screen while a game is on (the owner, 2026-10-20: "it should show the keybinds on the screen for moving side to side and for turning the camera angle slightly"). The words are placeholders: the owner writes the text.</summary>
        public static readonly (string key, string label)[] Hints =
        {
            ("A  D", "move side to side"),
            ("MOUSE", "turn the camera slightly"),
            ("LMB", "hold to wind up, let go to throw"),
            ("E", "leave"),
        };

        /// <summary>The regulation distance from the board's face to the throw line (the oche): 7 ft 9.25 in = 2.37 m (the owner, 2026-10-19 night: a line on the ground that looks like tape, where the player stands to throw).</summary>
'@
Save-Src

# ---------------------------------------------------------------- the camera: a limit on the aim
Open-Src "$A\CameraFollow.cs"
Rep @'
        public bool FreeLooking { get; private set; }
'@ @'
        public bool FreeLooking { get; private set; }

        // the dart game (the owner, 2026-10-20): at the throw line the aim can turn only a little, like Wii bowling
        bool aimLimited; float limitCentre, limitHalf, limitPitchMin, limitPitchMax;
        public bool AimLimited => aimLimited;
        /// <summary>
        /// The aim may turn <paramref name="halfRange"/> degrees either side of <paramref name="centreYaw"/> and tilt between the two pitches; the view is put on the centre (and tilted at <paramref name="lookAt"/>, if there is one).
        /// Free look is off meanwhile. <see cref="ClearAimLimit"/> lifts it.
        /// </summary>
        public void LimitAim(float centreYaw, float halfRange, float pitchMin, float pitchMax, Vector3? lookAt = null)
        {
            aimLimited = true; limitCentre = centreYaw; limitHalf = halfRange; limitPitchMin = pitchMin; limitPitchMax = pitchMax;
            aimYaw = centreYaw; lookYaw = 0f; lookPitch = 0f;
            if (lookAt.HasValue && (spectate ? spectate : target))
            {
                var d = lookAt.Value - Pivot();                                       // (the line of sight runs through the pivot: the camera sits behind it)
                pitch = Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            }
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        }
        public void ClearAimLimit() => aimLimited = false;
        /// <summary>The mouse turned the aim by this many degrees (the tests call it too): kept within the limit, if there is one.</summary>
        public void TurnAim(float dyaw)
        {
            aimYaw += dyaw;
            if (aimLimited) aimYaw = limitCentre + Mathf.Clamp(Mathf.DeltaAngle(limitCentre, aimYaw), -limitHalf, limitHalf);
        }
'@
Rep 'FreeLooking = Input.GetKey(freeLookKey) || Input.GetKey(altFreeLookKey);' 'FreeLooking = !aimLimited && (Input.GetKey(freeLookKey) || Input.GetKey(altFreeLookKey));'
Rep @'
                aimYaw += mx;
                pitch = Mathf.Clamp(pitch - my, minPitch, maxPitch);
'@ @'
                TurnAim(mx);
                pitch = aimLimited ? Mathf.Clamp(pitch - my, limitPitchMin, limitPitchMax) : Mathf.Clamp(pitch - my, minPitch, maxPitch);
'@
Save-Src

# ---------------------------------------------------------------- the body: no walking while held
Open-Src "$A\WobblyPlayer.cs"
Rep @'
        public bool IsStumbling { get; private set; }
'@ @'
        public bool IsStumbling { get; private set; }
        /// <summary>The body is held in place by something else (the dart game holds the player at the throw line): the keys do not walk, jump, sprint or crouch it.</summary>
        [System.NonSerialized] public bool inputLocked;
'@
Rep @'
            crouchKey |= testCrouch;
#endif
'@ @'
            crouchKey |= testCrouch;
#endif
            if (inputLocked) { raw = Vector3.zero; sprintKey = false; crouchKey = false; }
'@
Rep 'if (Input.GetKeyDown(KeyCode.Space) && grounded && !IsStumbling && !IsCrouching && !InCustody && !OutpostUi.Open)' 'if (Input.GetKeyDown(KeyCode.Space) && grounded && !IsStumbling && !IsCrouching && !InCustody && !OutpostUi.Open && !inputLocked)'
Save-Src

# ---------------------------------------------------------------- the board: the stance, E to leave, the keys on the screen
Open-Src "$A\Darts\DartBoard.cs"
Rep @'
        bool atLine;
        int lastToggleFrame = -1;
'@ @'
        bool atLine;
        int lastToggleFrame = -1;
        // the stance: the player held at the middle of the tape while a game is on (the owner, 2026-10-20)
        WobblyPlayer locked;
        Rigidbody lockedBody;
        float lateral;
        /// <summary>The body may be this far off its stance (a push, a teleport) before the game lets go of it.</summary>
        const float StanceBreak = 1.5f;
'@
Rep @'
        public bool AtLine => atLine;
'@ @'
        public bool AtLine => atLine;
        /// <summary>The player held at the line while a game is on, or null.</summary>
        public WobblyPlayer LockedBody => locked;
        /// <summary>How far along the tape (m, positive to the thrower's right as he faces the board) the player has slid from its middle.</summary>
        public float LateralOffset => lateral;
'@
Rep @'
            state = State.Playing;
            Current = this;
            charging = false; held = 0f; lastText = null;
            OutpostUi.Hide(OutpostUi.Kind.Darts);
        }
'@ @'
            state = State.Playing;
            Current = this;
            charging = false; held = 0f; lastText = null;
            OutpostUi.Hide(OutpostUi.Kind.Darts);
            if (!locked) LockStance();                                                       // (R for another game keeps him where he stands)
        }

        Vector3 StancePosition(float lat, float y) => lineCentre + flatNormal * DartsRules.StandBehind + viewerRight * lat + Vector3.up * y;

        /// <summary>
        /// Holds the player at the middle of the tape, a step behind it and facing the board; he can then slide along it (<see cref="FixedUpdate"/>) and turn a little (<see cref="CameraFollow.LimitAim"/>), like Wii bowling
        /// (the owner, 2026-10-20: "when a player presses to throw the darts they should get locked into the position at the center of the tape but there should be able to move left and right and turn a bit").
        /// </summary>
        void LockStance()
        {
            ReleaseStance();
            if (flatNormal.sqrMagnitude < 0.5f) return;                                      // (the board has not started yet)
            var body = Body();
            if (!body || !body.TryGetComponent(out WobblyPlayer wob) || !body.TryGetComponent(out Rigidbody rb)) return;
            locked = wob; lockedBody = rb; lateral = 0f;
            wob.inputLocked = true;
            var at = StancePosition(0f, rb.position.y);
            var facing = Quaternion.LookRotation(-flatNormal, Vector3.up);
            if (body.TryGetComponent(out Net.NetPlayer np)) np.PlaceAt(at, facing); else wob.TeleportTo(at, facing);
            if (rig) rig.LimitAim(Mathf.Atan2(-flatNormal.x, -flatNormal.z) * Mathf.Rad2Deg, DartsRules.TurnHalf, DartsRules.PitchMin, DartsRules.PitchMax, faceCentre);
        }

        void ReleaseStance()
        {
            if (locked) locked.inputLocked = false;
            if (rig) rig.ClearAimLimit();
            locked = null; lockedBody = null;
        }

        /// <summary>While a game is on the body is held on the tape: A / D slide it along (<see cref="DartsRules.Lateral"/>), nothing else moves it; a knock-over, a pin-down or a teleport (a run starting) lets it go.</summary>
        void FixedUpdate()
        {
            if (!locked || !lockedBody || state != State.Playing) return;
            var want = StancePosition(lateral, lockedBody.position.y);
            var gap = lockedBody.position - want; gap.y = 0f;
            if (locked.IsStumbling || locked.InCustody || gap.sqrMagnitude > StanceBreak * StanceBreak) { Leave(); return; }
            float input = 0f;
            bool blocked = EscapeMenu.Open || Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || TestMode.PanelOpen || OutpostUi.Open;
            if (!blocked)
            {
                input = Input.GetAxisRaw("Horizontal");
#if UNITY_EDITOR
                if (locked.testMove.HasValue) input = locked.testMove.Value.x;
#endif
            }
            lateral = DartsRules.Lateral(lateral, input, Time.fixedDeltaTime);
            lockedBody.MovePosition(StancePosition(lateral, lockedBody.position.y));
            var v = lockedBody.linearVelocity;
            lockedBody.linearVelocity = new Vector3(0f, v.y, 0f);
        }
'@
Rep @'
        public void Leave()
        {
            lastToggleFrame = Time.frameCount;
            state = State.Idle;
'@ @'
        public void Leave()
        {
            lastToggleFrame = Time.frameCount;
            ReleaseStance();
            state = State.Idle;
'@
Rep @'
            if (Instance == this) Instance = null;
            if (Current == this) { Current = null; OutpostUi.Hide(OutpostUi.Kind.Darts); }
'@ @'
            if (Instance == this) Instance = null;
            ReleaseStance();
            if (Current == this) { Current = null; OutpostUi.Hide(OutpostUi.Kind.Darts); }
'@
Rep @'
        public override void Use(WobblyPlayer who)
        {
            lastToggleFrame = Time.frameCount;
'@ @'
        public override void Use(WobblyPlayer who)
        {
            if (lastToggleFrame == Time.frameCount) return;                      // (E has already been worked this frame: the line and the crosshair must not both act)
            lastToggleFrame = Time.frameCount;
'@
Rep @'
            if (blocked) { charging = false; return; }
            if (match.Won)
'@ @'
            if (blocked) { charging = false; return; }
            if (Input.GetKeyDown(KeyCode.E) && lastToggleFrame != Time.frameCount) { Leave(); return; }          // (held at the line, E leaves from anywhere on it)
            if (match.Won)
'@
Rep @'
                HudTheme.Bar(bar, p, Color.Lerp(HudTheme.Good, HudTheme.Danger, p), 2.5f);
            }
        }
'@ @'
                HudTheme.Bar(bar, p, Color.Lerp(HudTheme.Good, HudTheme.Danger, p), 2.5f);
            }
            DrawHints();
        }

        static float CapWidth(string key, float s) => Mathf.Max(28f * s, HudTheme.TextWidthPx(key, 15f * s) + 28f * s * 0.55f);          // (HudTheme.KeyCap's own width)

        /// <summary>The keys of the stance on a plate at the bottom of the screen, above the weapon bar (<see cref="DartsRules.Hints"/>): slide, turn, throw, leave.</summary>
        void DrawHints()
        {
            float s = HudTheme.S, capH = 28f * s, gap = 22f * s, pad = 14f * s, plateH = 48f * s;
            var hints = DartsRules.Hints;
            float total = 0f;
            for (int i = 0; i < hints.Length; i++)
                total += CapWidth(hints[i].key, s) + 8f * s + HudTheme.TextWidthPx(hints[i].label, 16f * s) + (i > 0 ? gap : 0f);
            var r = new Rect((Screen.width - total) * 0.5f - pad, Screen.height - 184f * s, total + pad * 2f, plateH);
            HudTheme.Plate(r);
            float x = r.x + pad;
            for (int i = 0; i < hints.Length; i++)
            {
                float kw = HudTheme.KeyCap(x, r.y + (plateH - capH) * 0.5f - 2f * s, hints[i].key, capH, 15f);
                float lw = HudTheme.TextWidthPx(hints[i].label, 16f * s);
                HudTheme.Text(new Rect(x + kw + 8f * s, r.y, lw + 6f * s, plateH - 6f * s), hints[i].label, 16f, HudTheme.Bright, TextAnchor.MiddleLeft);
                x += kw + 8f * s + lw + gap;
            }
        }
'@
Save-Src

# ---------------------------------------------------------------- the docks: the board at the end of the hallway, the hallway cleared
Open-Src "$A\FloorGenerator.Dock.cs"
RepRx '(?m)^[ \t]*float eastInner = cols \* cellSize - 0\.2f;[ \t]*// \(the inner face of the east wall\)\r?\n[ \t]*var go = PlaceKey\("dartboard", eastInner - def\.D \* 0\.5f, BreakRoomDoor\.z, 270f, parent, 1\.7f - def\.H \* 0\.5f, false\);' @'
            // the end wall of the hallway outside the break room: the west face of the wall between the two halves of the docks, straight across from the break room's doorway (the owner, 2026-10-20: "The dart board and tape should be in
            // this hallway ..., where it was originally")
            float faceX = cellSize - 0.2f;
            var go = PlaceKey("dartboard", faceX - def.D * 0.5f, BreakRoomDoor.z, 270f, parent, 1.7f - def.H * 0.5f, false);
'@
Rep 'the Synty board on the east wall, straight across from the break room''s doorway, its middle 1.7 m up and its face to the west; the room to throw from is kept clear' 'the Synty board on the end wall of the hallway outside the break room (the wall between the two halves of the docks), straight across from the break room''s doorway, its middle 1.7 m up and its face to the west; the hallway is kept clear of boxes, pallets, drums and forklifts (the steel shelves along its walls stay)'
RepRx '(?m)^[ \t]*new Vector4\(width - 5\.2f, BreakRoomDoor\.z - 2\.4f, width, BreakRoomDoor\.z \+ 2\.4f\),[^\r\n]*\r?\n[ \t]*\r?\n' ''
Rep 'var placed = new List<Vector3>(); // x, z, radius' @'
// the hallway outside the break room, from its doorway to the wall between the two halves of the docks, where the dart board hangs (the owner, 2026-10-20: "the hallway should just be cleaned up a bit (leave the shelves
            // against the wall)"): no boxes, pallets, drums or forklift stand on its floor; the steel shelves against the walls may (those are placed against a wall, which is not checked against this list)
            var floorClear = new List<Vector4> { new Vector4(BreakRoomDoor.x, 0f, cellSize - 0.2f, (cellSize - doorWidth) / 2f) };
            var placed = new List<Vector3>(); // x, z, radius
'@
Rep @'
                foreach (var c in clear)
                    if (x > c.x - r && x < c.z + r && z > c.y - r && z < c.w + r) return false;
                foreach (var p in placed)
'@ @'
                foreach (var c in clear)
                    if (x > c.x - r && x < c.z + r && z > c.y - r && z < c.w + r) return false;
                if (!againstWall)
                    foreach (var c in floorClear)
                        if (x > c.x - r && x < c.z + r && z > c.y - r && z < c.w + r) return false;
                foreach (var p in placed)
'@
Save-Src

# ---------------------------------------------------------------- the protocol: the docks are built differently now
Open-Src "$A\Net\NetConst.cs"
Rep 'public const string ProtocolVersion = "24";   // 24: the dart board''s scoreboard' 'public const string ProtocolVersion = "25";   // 25: the dart board hangs at the end of the hallway outside the break room and the hallway is cleared (the docks are built differently, so old and new cannot share a run), a game holds the player at the tape; 24: the dart board''s scoreboard'
Save-Src

# ---------------------------------------------------------------- the tests
Open-Src "$E\NetDevRound12.cs"
Rep @'
            // the throw: how long the button is held, the speed, the drop-off, the spread
'@ @'
            // the stance at the line (the owner, 2026-10-20): held at the middle of the tape, slides along it, turns a little
            Check("darts: the slide along the tape is 1.6 m/s, stops 0.45 m either side of the middle, a key held both ways cancels; the turn stops 25 degrees either side of the board (across the 0 / 360 line too)",
                Mathf.Abs(DartsRules.Lateral(0f, 1f, 0.1f) - 0.16f) < 0.001f && DartsRules.Lateral(0.4f, 1f, 1f) == DartsRules.LateralHalf && DartsRules.Lateral(-0.4f, -1f, 1f) == -DartsRules.LateralHalf
                && DartsRules.Lateral(0.2f, 0f, 1f) == 0.2f && DartsRules.Lateral(0f, 5f, 0.1f) == DartsRules.Lateral(0f, 1f, 0.1f)
                && Mathf.Abs(DartsRules.ClampAim(90f, 100f) - 100f) < 0.001f && Mathf.Abs(DartsRules.ClampAim(90f, 200f) - 115f) < 0.001f && Mathf.Abs(DartsRules.ClampAim(90f, -50f) - 65f) < 0.001f
                && Mathf.Abs(Mathf.DeltaAngle(DartsRules.ClampAim(350f, 20f), 15f)) < 0.001f && Mathf.Abs(Mathf.DeltaAngle(DartsRules.ClampAim(10f, 330f), 345f)) < 0.001f);
            Check("darts: the keys shown while a game is on are the slide, the turn, the throw and leaving; the stance fits inside the spot where \"throw darts\" is offered",
                DartsRules.Hints.Length == 4 && DartsRules.Hints[0].key == "A  D" && DartsRules.Hints[1].key == "MOUSE" && DartsRules.Hints[2].key == "LMB" && DartsRules.Hints[3].key == "E" && DartsRules.Hints.All(h => h.label.Length > 0)
                && DartsRules.StandBehind > 0f && DartsRules.StandBehind < DartsRules.LineZoneBehind && DartsRules.LateralHalf < DartsRules.LineZoneHalfWidth && DartsRules.PitchMin < DartsRules.PitchMax);
            // the throw: how long the button is held, the speed, the drop-off, the spread
'@
RepRegion 'float width = gen.CellColumns * 18f;' 'doorway z {gen.BreakRoomDoor.z})");' @'
float wallFace = 18f - 0.2f;                                                       // (the west face of the wall between the two halves of the docks)
                R12Check("it hangs at the end of the hallway outside the break room (on the wall between the two halves of the docks), straight across from the break room's doorway, facing west, its middle 1.7 m up",
                    Mathf.Abs(board.FaceCentre.x - wallFace) < 0.5f && Mathf.Abs(board.FaceCentre.z - gen.BreakRoomDoor.z) < 0.3f && board.Normal.x < -0.99f && Mathf.Abs(board.FaceCentre.y - 1.7f) < 0.25f,
                    $"(face at {board.FaceCentre}, faces {board.Normal}, wall face at {wallFace}, doorway z {gen.BreakRoomDoor.z})");
                // the hallway outside the break room is cleaned up (the owner, 2026-10-20: "the hallway should just be cleaned up a bit (leave the shelves against the wall)"): nothing but the shelves stands on its floor
                var hallProps = new List<string>();
                foreach (var pt in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (pt.name != "BoxStack" && pt.name != "BoxBlock" && pt.name != "Forklift" && pt.name != "Drum") continue;
                    var prs = pt.GetComponentsInChildren<Renderer>();
                    if (prs.Length == 0) continue;
                    var pb = prs[0].bounds; foreach (var pr in prs) pb.Encapsulate(pr.bounds);
                    if (pb.min.y < 2.2f && pb.max.x > gen.BreakRoomDoor.x + 0.2f && pb.min.x < wallFace && pb.max.z > 0.2f && pb.min.z < 7f) hallProps.Add(pt.name + " at " + pb.center.ToString("0.0"));
                }
                R12Check("the hallway outside the break room is cleaned up: no box, pallet, drum or forklift stands on its floor between the doorway and the board's wall (the steel shelves along the walls stay)", hallProps.Count == 0, $"(in the way: {string.Join("; ", hallProps)})");
'@
Rep 'if (pt.name != "BoxStack" && pt.name != "BoxBlock" && pt.name != "Shelf" && pt.name != "StockShelf" && pt.name != "Forklift" && pt.name != "Drum" && pt.name != "Bollard") continue;' 'if (pt.name != "BoxStack" && pt.name != "BoxBlock" && pt.name != "Forklift" && pt.name != "Drum" && pt.name != "Bollard") continue;          // (the shelves against the wall may stay: the owner, 2026-10-20)'
Rep 'are clear of boxes, pallets, shelves, forklifts and drums"' 'are clear of boxes, pallets, forklifts and drums (the steel shelves against the wall may stay)"'
Rep @'
                // standing at the line it says "throw darts", and E brings up the game's screen
                board.Leave();
'@ @'
                // the stance (the owner, 2026-10-20: "when a player presses to throw the darts they should get locked into the position at the center of the tape but there should be able to move left and right and turn a bit like Wii bowling"):
                // the game holds the player at the middle of the tape, A / D slide him along it, the mouse turns him a little
                R12Check("the tape lies in the hallway outside the break room: east of its doorway, a few metres short of the board's wall, across the way to the board",
                    tape != null && tape.position.x > gen.BreakRoomDoor.x + 2f && tape.position.x < 18f - 2f && tape.position.z > 0.5f && tape.position.z < 5f, $"(tape at {(tape ? tape.position.ToString("0.00") : "none")})");
                var wobLock = board.LockedBody;
                float centreYaw = Mathf.Atan2(-board.Normal.x, -board.Normal.z) * Mathf.Rad2Deg;
                var sideways = Vector3.Cross(Vector3.up, -board.Normal);
                var rel0 = player.transform.position - board.LineCentre; rel0.y = 0f;
                R12Check("a game holds the player at the middle of the tape, a step behind it, facing the board, and the keys do not walk him",
                    wobLock == player && player.inputLocked && Mathf.Abs(Vector3.Dot(rel0, board.Normal) - DartsRules.StandBehind) < 0.08f && Mathf.Abs(Vector3.Dot(rel0, sideways)) < 0.08f && rig != null && rig.AimLimited,
                    $"(locked {wobLock != null}, behind {Vector3.Dot(rel0, board.Normal):0.00}, aside {Vector3.Dot(rel0, sideways):0.00})");
                if (gfx)
                {
                    var scam = new GameObject("R12StanceCam").AddComponent<Camera>();
                    scam.fieldOfView = 60f; scam.nearClipPlane = 0.05f; scam.enabled = false;
                    scam.transform.position = player.transform.position + board.Normal * 2.6f + Vector3.up * 1.3f + sideways * 0.45f;
                    scam.transform.LookAt(board.FaceCentre);
                    ShotCam(scam, "round12_stance");
                    Destroy(scam.gameObject);
                }
                player.testMove = new Vector3(1f, 0f, 0f);
                yield return new WaitForSeconds(1.2f);
                var rel1 = player.transform.position - board.LineCentre; rel1.y = 0f;
                R12Check("holding D slides him to the right along the tape, as far as its end and no further (0.45 m)", Mathf.Abs(Vector3.Dot(rel1, sideways) - DartsRules.LateralHalf) < 0.06f && Mathf.Abs(board.LateralOffset - DartsRules.LateralHalf) < 0.02f,
                    $"(aside {Vector3.Dot(rel1, sideways):0.00}, offset {board.LateralOffset:0.00})");
                player.testMove = new Vector3(-1f, 0f, 0f);
                yield return new WaitForSeconds(2.4f);
                var rel2 = player.transform.position - board.LineCentre; rel2.y = 0f;
                R12Check("holding A slides him back across the middle to the other end", Mathf.Abs(Vector3.Dot(rel2, sideways) + DartsRules.LateralHalf) < 0.06f, $"(aside {Vector3.Dot(rel2, sideways):0.00})");
                player.testMove = new Vector3(0f, 0f, 1f);
                yield return new WaitForSeconds(1.0f);
                var rel3 = player.transform.position - board.LineCentre; rel3.y = 0f;
                R12Check("holding W does not walk him off the line (he stays a step behind the tape)", Mathf.Abs(Vector3.Dot(rel3, board.Normal) - DartsRules.StandBehind) < 0.1f, $"(behind {Vector3.Dot(rel3, board.Normal):0.00})");
                player.testMove = null;
                rig.TurnAim(90f);
                bool turnedRight = Mathf.Abs(Mathf.DeltaAngle(centreYaw, rig.AimYaw)) <= DartsRules.TurnHalf + 0.01f && Mathf.Abs(Mathf.DeltaAngle(centreYaw, rig.AimYaw)) > DartsRules.TurnHalf - 1f;
                rig.TurnAim(-200f);
                bool turnedLeft = Mathf.Abs(Mathf.DeltaAngle(centreYaw, rig.AimYaw)) <= DartsRules.TurnHalf + 0.01f && Mathf.Abs(Mathf.DeltaAngle(centreYaw, rig.AimYaw)) > DartsRules.TurnHalf - 1f;
                R12Check("the mouse turns him only a little: 90 degrees to one side or 200 to the other stop at 25 degrees either side of the board", turnedRight && turnedLeft, $"(yaw {rig.AimYaw:0}, centre {centreYaw:0})");
                board.Leave();
                R12Check("leaving lets him go: the keys walk him again and the aim turns freely", !player.inputLocked && !rig.AimLimited && board.LockedBody == null);

                // standing at the line it says "throw darts", and E brings up the game's screen
                board.Leave();
'@
Save-Src

# ---------------------------------------------------------------- a picture of the keys on the screen (a real Editor window: IMGUI is not drawn in batch mode)
Open-Src "$E\NetDevUi.cs"
Rep @'
            yield return Snap("hub");
            UiCheck("the pack holds the loot", pack.LootCount >= 1, $"loot={pack.LootCount}");
'@ @'
            yield return Snap("hub");
            UiCheck("the pack holds the loot", pack.LootCount >= 1, $"loot={pack.LootCount}");
            // ---- the dart game: the player held at the tape, the keys on the screen (the owner, 2026-10-20)
            var dartBoard = FindFirstObjectByType<DartBoard>();
            if (dartBoard != null)
            {
                player.TeleportTo(new Vector3(dartBoard.LineCentre.x + dartBoard.Normal.x * 0.8f, 1.1f, dartBoard.LineCentre.z + dartBoard.Normal.z * 0.8f), player.transform.rotation);
                yield return new WaitForSeconds(0.4f);
                dartBoard.Begin(DartsRules.Mode.Game301);
                yield return new WaitForSeconds(1.2f);
                yield return Snap("darts_stance");
                UiCheck("the dart game holds the player at the tape", dartBoard.LockedBody == player && player.inputLocked, $"locked={dartBoard.LockedBody != null}");
                dartBoard.Leave();
                yield return new WaitForSeconds(0.3f);
            }
'@
Save-Src

# ---------------------------------------------------------------- the words in the playtest change list
Open-Src 'C:\bqs\README_PLAYTEST.txt'
Rep @'
A dart board hangs on the Loading Docks' east wall across from the break room's doorway (E to play 301 or Cricket; hold the left mouse button to wind up a throw, let go to throw;
    the dart drops over a distance).
'@ @'
The PVP room is a big warehouse now, with shipping containers and crates to hide behind. A dart board hangs at the end of the hallway outside the break room, straight across from its doorway (the hallway is cleared; the steel shelves along its walls
    stay), with a strip of tape on the floor 2.37 m from it and a little scoreboard above it that everybody sees. Stand at the tape and press E to play 301 or Cricket: the game holds you at the middle of the tape - A / D slide you along it, the mouse
    turns you a little, hold the left mouse button to wind up a throw and let go to throw (the dart drops over a distance), E leaves; the keys are shown on the screen.
'@
Save-Src
Write-Output 'round 12 batch G26 patched'
