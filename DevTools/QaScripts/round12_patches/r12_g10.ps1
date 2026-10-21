# Round 12, batch G10 (the owner's asks of 2026-10-19 night, all "before the build"): the vendor shows up in the intro after the workers' line; floor 3 holds no weapon or ammunition in its safes; a weapon bar of three slots
# (keys 1, 2, 3); first / third person (P, the wheel, Ctrl + Up / Down); the forklift out of the PVP room's doorway; the dart board outside the break room and its game. The scripts (the tests follow in G11).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$SC = 'C:\bqm\Assets\Scripts'

# the new files are written with CRLF like the rest of the project
foreach ($f in @("$SC\Darts\DartsRules.cs", "$SC\Darts\DartBoard.cs", "$SC\Hud\WeaponBarRules.cs", "$SC\ViewRules.cs", 'C:\bqm\Assets\Editor\NetDev\NetDevRound12.cs')) {
  if (-not $env:R2_DRY) { $t = [IO.File]::ReadAllText($f); [IO.File]::WriteAllText($f, ($t -replace "`r?`n", "`r`n"), (New-Object Text.UTF8Encoding($false))) }
}

# ====================================================================== the intro: the vendor shows up
Open-Src "$SC\Intro\IntroRules.cs"
Rep @'
        /// <summary>The two who talk, side by side, turned to each other.</summary>
        TwoShot,
'@ @'
        /// <summary>The two who talk, side by side, turned to each other.</summary>
        TwoShot,
        /// <summary>The vendor's bay from inside the docks: after the workers' line its roller door rolls up on him (the owner, 2026-10-19 night: "the Vendor showing up after they say they are going to call").</summary>
        Bay,
'@
RepRx 'public const float Total = 35f;' 'public const float Total = 37.8f;'
RepRx '/// <summary>The length of the Intro: the monologue on the TV \(27 s\), a moment of static, the workers. line, and the way out\.</summary>' '/// <summary>The length of the Intro: the monologue on the TV (27 s), a moment of static, the workers'' line, and the vendor''s bay door rolling up on him.</summary>'
Rep 'public const float JetStart = 18.4f, JetSeconds = 4.6f;' @'
public const float JetStart = 18.4f, JetSeconds = 4.6f;
        /// <summary>The vendor's bay door starts to roll up (it takes <see cref="DockBayDoor.RollSeconds"/>) a little after the workers' line has ended (the owner, 2026-10-19 night: the vendor shows up after they say they are going to call).</summary>
        public const float BayOpensAt = 34.2f;
'@
Rep 'new IntroBeat(33.4f, 35.0f, IntroShot.None, DockShot.Behind, null, Speaker.None),' 'new IntroBeat(33.4f, 37.8f, IntroShot.None, DockShot.Bay, null, Speaker.None),'
Rep 'The timeline: ten beats, 35 s.' 'The timeline: ten beats, 37.8 s.'
Save-Src

Open-Src "$SC\Intro\IntroCinematic.cs"
Rep '/* TwoShot   */ new Rig(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 36f, 33f),     // (made from where the two of them stand: see PlaceDockCamera)' @'
/* TwoShot   */ new Rig(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 36f, 33f),     // (made from where the two of them stand: see PlaceDockCamera)
            /* Bay       */ new Rig(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 40f, 34f),     // (made from the vendor's bay: see PlaceDockCamera)
'@
Rep 'bool bayShut;' @'
bool bayShut;
        FloorGenerator dockGen;
'@
Rep 'state = State.Playing;' @'
state = State.Playing;
            dockGen = FindFirstObjectByType<FloorGenerator>();
'@
Rep @'
            else
            {
                var a = rig;
'@ @'
            else if (beat.dock == DockShot.Bay && dockGen != null)
            {
                // the vendor's bay from inside the docks, pushing in slowly: the roller door in the middle of the picture, the trailer behind it, the vendor at its far end (the owner, 2026-10-19 night)
                var e = dockGen.DockTrailerEntrance;
                pos = e + new Vector3(Mathf.Lerp(-0.9f, -0.3f, p), Mathf.Lerp(1.6f, 1.55f, p), Mathf.Lerp(-3.2f, -1.9f, p));
                look = e + new Vector3(0f, 1.35f, 9.8f);
                fov = Mathf.Lerp(rig.fovA, rig.fovB, p);
            }
            else
            {
                var a = rig;
'@
Rep @'
            // ---- the words: the boss's head and shoulders move while a line is being typed, and the voices blip
'@ @'
            // ---- the vendor shows up: after the workers' line the roller door of his bay rolls up (the owner, 2026-10-19 night)
            if (beat.dock == DockShot.Bay && t >= IntroRules.BayOpensAt) OpenBay();

            // ---- the words: the boss's head and shoulders move while a line is being typed, and the voices blip
'@
Save-Src

# ====================================================================== floor 3: no weapons or ammunition in the safes
Open-Src "$SC\Outpost\SafeRules.cs"
Rep 'from floor 3 on the safes may hold a gun (by luck)' 'from floor 3 on the safes may hold a gun (by luck; not on floor 3 itself: see NoGunFloor)'
Rep 'public const int WeaponFirstFloor = 3;' @'
public const int WeaponFirstFloor = 3;
        /// <summary>The floor whose safes hold no gun and no spare clip: the one pistol of floor 3 is in the guard outpost's gun safe (the owner, 2026-10-19 night: "there should only be one pistol on floor 3 (the one inside the outpost). Floor 3 should have no weapons or ammo in the safes"). Armour, cash and watches can still be in them.</summary>
        public const int NoGunFloor = 3;
        /// <summary>Do the safes of this floor hold guns and spare clips (every weapon floor but <see cref="NoGunFloor"/>)?</summary>
        public static bool SafesHoldGuns(int floor) => floor != NoGunFloor;
'@
Rep 'if (!WeaponSafeExists(runSeed, floor, index) || HoldsArmour(runSeed, floor, index, slot)) return false;' 'if (!SafesHoldGuns(floor) || !WeaponSafeExists(runSeed, floor, index) || HoldsArmour(runSeed, floor, index, slot)) return false;'
Rep 'WeaponSafeExists(runSeed, floor, index) && !HoldsArmour(runSeed, floor, index) && Hash(runSeed, floor, SaltSideClip' 'WeaponSafeExists(runSeed, floor, index) && SafesHoldGuns(floor) && !HoldsArmour(runSeed, floor, index) && Hash(runSeed, floor, SaltSideClip'
Save-Src

# ====================================================================== the weapon bar (keys 1, 2, 3)
Open-Src "$SC\Backpack.cs"
Rep 'public IReadOnlyList<Pickup> Items => items;' @'
public IReadOnlyList<Pickup> Items => items;

        // ---- the weapon bar (the owner, 2026-10-19 night: "a tool bar (hot bar) of 3 items so players can click 1, 2, 3 to switch between weapons"): the first three weapons of the pack, in the order they came into it
        readonly List<bool> barFlags = new List<bool>();
        readonly int[] barSlots = new int[WeaponBarRules.Slots];
        readonly string[] barNames = new string[WeaponBarRules.Slots];

        void RefreshBar()
        {
            barFlags.Clear();
            for (int i = 0; i < items.Count; i++) barFlags.Add(items[i] && RangedWeapon.IsWeapon(items[i]));
            WeaponBarRules.Fill(barFlags, barSlots);
        }

        /// <summary>The weapon of a bar slot (0 to 2), or null for an empty slot.</summary>
        public Pickup BarWeapon(int slot)
        {
            RefreshBar();
            return slot >= 0 && slot < barSlots.Length && barSlots[slot] >= 0 && barSlots[slot] < items.Count ? items[barSlots[slot]] : null;
        }

        /// <summary>Takes the weapon of a bar slot in hand (the keys 1, 2, 3); false for an empty slot, which changes nothing.</summary>
        public bool SelectSlot(int slot)
        {
            var p = BarWeapon(slot);
            if (!p) return false;
            if (p != Equipped) SetEquipped(p);
            return true;
        }
'@
Rep 'if (Input.GetKeyDown(inventoryKey) && !Vendor.ShopOpen && !ElevatorPanel.Open && !OutpostUi.Open) { UiOpen = !UiOpen; scroll = Vector2.zero; }' @'
if (Input.GetKeyDown(inventoryKey) && !Vendor.ShopOpen && !ElevatorPanel.Open && !OutpostUi.Open) { UiOpen = !UiOpen; scroll = Vector2.zero; }
            if (!UiOpen && !talk && !EscapeMenu.Open && !TestMode.PanelOpen)
                for (int d = 1; d <= WeaponBarRules.Slots; d++)
                    if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d)) SelectSlot(WeaponBarRules.SlotOfKey(d));
'@
Rep @'
            if (UiOpen) return;

            string prompt = null;
'@ @'
            RefreshBar();
            int inHand = -1;
            for (int i = 0; i < barSlots.Length; i++)
            {
                var slotItem = barSlots[i] >= 0 && barSlots[i] < items.Count ? items[barSlots[i]] : null;
                barNames[i] = slotItem ? slotItem.Label : null;
                if (slotItem && slotItem == Equipped) inHand = i;
            }
            HudWidgets.WeaponBar(new HudWidgets.WeaponBarModel { names = barNames, equipped = inHand });
            if (UiOpen) return;

            string prompt = null;
'@
Save-Src

Open-Src "$SC\Hud\HudWidgets.cs"
RepRx '// =+ the pew.s ammunition \(bottom right\)' @'
// ====================================================================== the weapon bar (bottom centre)

        /// <summary>What the weapon bar shows: the name of the weapon in each slot (null = empty) and which slot is in hand (-1 for none).</summary>
        public struct WeaponBarModel
        {
            public string[] names;
            public int equipped;
        }

        /// <summary>A chip with its left edge at <paramref name="x"/> (for the other screens that draw one); returns its width.</summary>
        public static float ChipAt(float x, float y, float h, string text, Color fill, float size = 13f) => Chip(x, y, h, text, fill, size);

        /// <summary>
        /// The weapon bar (the owner, 2026-10-19 night): three plates above the stamina bar, each with its key (1, 2, 3) and the weapon in that slot (<see cref="WeaponBarRules"/>); the one in hand is bright, with a bar under it.
        /// </summary>
        public static void WeaponBar(in WeaponBarModel m)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = S, sw = 176f * s, sh = 48f * s, gap = 8f * s, pad = 8f * s;
            float total = WeaponBarRules.Slots * sw + (WeaponBarRules.Slots - 1) * gap;
            float x0 = (Screen.width - total) * 0.5f, y = Screen.height - 74f * s - sh;
            for (int i = 0; i < WeaponBarRules.Slots; i++)
            {
                var r = new Rect(x0 + i * (sw + gap), y, sw, sh);
                HudTheme.Plate(r);
                bool on = m.equipped == i, has = m.names != null && i < m.names.Length && !string.IsNullOrEmpty(m.names[i]);
                float kw = HudTheme.KeyCap(r.x + pad, r.y + (sh - 28f * s) * 0.5f - 2f * s, (i + 1).ToString(), 28f * s, 15f);
                if (has)
                {
                    float tx = r.x + pad + kw + 8f * s, tw = r.xMax - pad - tx;
                    HudTheme.Text(new Rect(tx, r.y, tw, sh - 6f * s), MenuKit.Fit(m.names[i], 15f * s, tw), 15f, on ? HudTheme.Bright : HudTheme.Muted, TextAnchor.MiddleLeft);
                }
                if (on) HudTheme.Bar(new Rect(r.x + pad, r.yMax - 12f * s, r.width - pad * 2f, 6f * s), 1f, HudTheme.Info, 1.5f);
            }
        }

        // ====================================================================== the pew's ammunition (bottom right)
'@
Save-Src

# ====================================================================== first / third person
Open-Src "$SC\CameraFollow.cs"
Rep 'using UnityEngine;' @'
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
'@
Rep 'public float followSmooth = 25f;' @'
public float followSmooth = 25f;
        [Header("View (the owner: wheel up = first person, wheel down = third; Ctrl + Up / Down and P for those without a mouse)")]
        public KeyCode viewKey = KeyCode.P;
        public float eyeHeight = 0.62f;       // first person: the eyes above the capsule centre
'@
Rep 'float aimYaw, pitch = 12f, lookYaw, lookPitch;' @'
float aimYaw, pitch = 12f, lookYaw, lookPitch;
        float zoom, zoomTarget;               // 0 = third person .. 1 = first person (ViewRules)
        readonly List<Renderer> hiddenBody = new List<Renderer>();
        readonly Dictionary<Renderer, ShadowCastingMode> bodyModes = new Dictionary<Renderer, ShadowCastingMode>();
        bool bodyHidden; float nextBodyScan;

        /// <summary>Is the view at the eyes (the body is out of the picture)?</summary>
        public bool IsFirstPerson => ViewRules.IsFirstPerson(zoom);
        public float Zoom => zoom;
        public void SetFirstPerson(bool on) => zoomTarget = on ? ViewRules.FirstPerson : ViewRules.ThirdPerson;
        public void ToggleView() => zoomTarget = zoomTarget > 0.5f ? ViewRules.ThirdPerson : ViewRules.FirstPerson;
'@
Rep 'return body.position + Vector3.up * (headHeight - 0.5f * crouchDrop);' 'return body.position + Vector3.up * (Mathf.Lerp(headHeight, eyeHeight, ViewRules.Smooth(zoom)) - 0.5f * crouchDrop);'
Rep 'if (!locked) return;' @'
if (!locked) return;

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            zoomTarget = ViewRules.Target(zoomTarget, Input.mouseScrollDelta.y, ctrl && Input.GetKeyDown(KeyCode.UpArrow), ctrl && Input.GetKeyDown(KeyCode.DownArrow), Input.GetKeyDown(viewKey));
'@
Rep 'void LateUpdate()' @'
/// <summary>The body goes out of the picture in first person (its meshes only cast their shadow; what is in the hands stays) and comes back as it was.</summary>
        void ShowBody(bool show)
        {
            var body = spectate ? spectate : target;
            if (!body) return;
            if (show)
            {
                if (!bodyHidden) return;
                foreach (var r in hiddenBody) if (r && bodyModes.TryGetValue(r, out var mode)) r.shadowCastingMode = mode;
                hiddenBody.Clear(); bodyModes.Clear();
                bodyHidden = false;
                return;
            }
            if (bodyHidden && Time.unscaledTime < nextBodyScan) return;
            nextBodyScan = Time.unscaledTime + 0.5f;                                  // (a hat or another look can come later: looked at again twice a second)
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (!r || bodyModes.ContainsKey(r) || r.GetComponentInParent<HeldGun>() != null || r.GetComponentInParent<MeleeWeapon>() != null) continue;
                bodyModes[r] = r.shadowCastingMode;
                hiddenBody.Add(r);
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            bodyHidden = true;
        }

        void LateUpdate()
'@
RepRegion 'smoothedPivot = Vector3.Lerp(smoothedPivot, Pivot(), 1f - Mathf.Exp(-followSmooth * Time.deltaTime));' 'transform.rotation = rot;' @'
zoom = ViewRules.Step(zoom, zoomTarget, Time.unscaledDeltaTime);
            float zs = ViewRules.Smooth(zoom);
            smoothedPivot = Vector3.Lerp(smoothedPivot, Pivot(), 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
            var rot = Quaternion.Euler(pitch + lookPitch, aimYaw + lookYaw, 0f);
            var dir = rot * new Vector3(Mathf.Lerp(shoulderOffset, 0f, zs), 0f, -ViewRules.Distance(distance, zoom));
            float len = dir.magnitude;
            if (len < 0.02f)
            {
                transform.SetPositionAndRotation(smoothedPivot, rot);                 // (first person: the camera is at the eyes)
            }
            else
            {
                var ndir = dir / len;
                // pull the camera in front of walls (ignore rigidbodies like loot, mugs, players)
                int n = Physics.SphereCastNonAlloc(smoothedPivot, collisionRadius, ndir, hits, len, ~0, QueryTriggerInteraction.Ignore);
                float best = len;
                for (int i = 0; i < n; i++)
                {
                    if (hits[i].rigidbody != null || hits[i].distance <= 0f) continue;
                    if (hits[i].distance < best) best = hits[i].distance;
                }
                transform.position = smoothedPivot + ndir * Mathf.Max(Mathf.Lerp(0.3f, 0f, zs), best);
                transform.rotation = rot;
            }
            ShowBody(!ViewRules.IsFirstPerson(zoom));
'@
Save-Src

Open-Src "$SC\WobblyPlayer.cs"
Rep 'bool crouchKey = !OutpostUi.Open && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C));' 'bool crouchKey = !OutpostUi.Open && ((Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.UpArrow) && !Input.GetKey(KeyCode.DownArrow)) || Input.GetKey(KeyCode.C));          // (Ctrl + Up / Down is the view, not a crouch)'
Save-Src

# ====================================================================== the dart board: the screen kind, and the left button throws while a game is on
Open-Src "$SC\Outpost\OutpostUi.cs"
Rep 'public enum Kind { None, Hack, Safe, Journal, Intro, Computer, GunSafe }' 'public enum Kind { None, Hack, Safe, Journal, Intro, Computer, GunSafe, Darts }'
Rep 'Current == Kind.GunSafe;' 'Current == Kind.GunSafe || Current == Kind.Darts;'
Save-Src

Open-Src "$SC\Guns\GunController.cs"
Rep 'if (Input.GetMouseButton(0)) Fire();' 'if (Input.GetMouseButton(0) && !DartBoard.Playing) Fire();'
Save-Src

Open-Src "$SC\MeleeController.cs"
Rep 'if (Input.GetMouseButtonDown(0)) TryPunch();' 'if (Input.GetMouseButtonDown(0) && !DartBoard.Playing) TryPunch();'
Rep 'if (Input.GetMouseButtonDown(0)) StartCoroutine(Swing(' 'if (Input.GetMouseButtonDown(0) && !DartBoard.Playing) StartCoroutine(Swing('
Save-Src

Open-Src "$SC\Interior\PropTable.cs"
Rep 'Add("clock", Syn + "Props/Wall Props/SM_Prop_Clock_01", .39f, .37f, .05f, solid: false, cat: PropCat.Mount);' @'
Add("dartboard", Syn + "Props/Wall Props/SM_Prop_Darkboard_01", .714f, .679f, .334f, solid: false, cat: PropCat.Mount);          // (the dart board: its name has a typo in the pack; the model stands on its back, its front +z, the board 0.7 m across with a dart stuck in it)
            Add("clock", Syn + "Props/Wall Props/SM_Prop_Clock_01", .39f, .37f, .05f, solid: false, cat: PropCat.Mount);
'@
Save-Src

# ====================================================================== the docks: the doorways stay clear, the dart board hangs across from the break room's doorway
Open-Src "$SC\FloorGenerator.Dock.cs"
Rep 'new Vector4(width - 1.6f, depth / 2f - 4f, width, depth / 2f + 4f),' @'
new Vector4(BreakRoomDoor.x, BreakRoomDoor.z - 2.4f, BreakRoomDoor.x + 4.6f, BreakRoomDoor.z + 2.4f),   // in front of the break room's doorway
                new Vector4(PvpRoomDoor.x, PvpRoomDoor.z - 2.4f, PvpRoomDoor.x + 4.6f, PvpRoomDoor.z + 2.4f),            // in front of the PVP room's doorway: a forklift stood across it (the owner, 2026-10-19 night)
                new Vector4(width - 3.6f, BreakRoomDoor.z - 2.0f, width, BreakRoomDoor.z + 2.0f),                          // the dart board on the east wall and the room to throw from
                new Vector4(width - 1.6f, depth / 2f - 4f, width, depth / 2f + 4f),
'@
Rep 'BuildDockRooms(root.transform);' @'
BuildDockRooms(root.transform);
            BuildDartBoard(root.transform);
'@
Rep @'
            if (solid && def.solid) AddBox(go, pr, Mathf.Max(def.H, 0.05f));
            return go;
        }
'@ @'
            if (solid && def.solid) AddBox(go, pr, Mathf.Max(def.H, 0.05f));
            return go;
        }

        /// <summary>
        /// The dart board (the owner, 2026-10-19 night): the Synty board on the east wall, straight across from the break room's doorway, its middle 1.7 m up and its face to the west; the room to throw from is kept clear
        /// (ScatterStorage). The game: <see cref="DartBoard"/>.
        /// </summary>
        void BuildDartBoard(Transform parent)
        {
            if (!Application.isPlaying || !PropTable.TryGet("dartboard", out var def)) return;
            float eastInner = cols * cellSize - 0.2f;                                // (the inner face of the east wall)
            var go = PlaceKey("dartboard", eastInner - def.D * 0.5f, BreakRoomDoor.z, 270f, parent, 1.7f - def.H * 0.5f, false);
            if (go != null) go.AddComponent<DartBoard>();
        }
'@
Save-Src
