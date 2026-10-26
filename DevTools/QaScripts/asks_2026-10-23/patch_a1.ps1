# Batch A1 of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63): T19 elevator buttons, T20 weapon weights, T21 scan delay, T10 vendor weapon lines, T15 computer tabs, T18 every safe's code,
# T8 drop all loot, T17 floor 1 crowbar, T14 guard count. Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }          # a dry run checks every exact match and saves nothing
$A = 'C:\bd\Assets\'

# ---- T19 ElevatorPanel
$SC = $A + 'Scripts\ElevatorPanel.cs'; Open-Src $SC
Rep ('            // floor buttons: 13 at the top left down to 1, then the docks' + "`n" + '            for (int k = 0; k < 14; k++)' + "`n" + '            {' + "`n" + '                int floor = k < 13 ? 13 - k : 0;' + "`n" + '                int row = k / Columns, col = k % Columns;' + "`n" + '                var pos = new Vector3((col - 1) * Pitch, 0.5f - row * Pitch, FaceZ);') (
  '            // floor buttons rising from left to right and from the bottom row up, like a real panel (the owner, 2026-10-23, with a photograph: "in order from left to right ascending"): the docks, 1, 2 / 3 4 5 / ... / 12 13 at the top (ElevatorPanelRules)' + "`n" +
  '            for (int k = 0; k < ElevatorPanelRules.Buttons; k++)' + "`n" + '            {' + "`n" + '                int floor = ElevatorPanelRules.FloorOf(k);' + "`n" +
  '                ElevatorPanelRules.Position(k, 0.5f, out float bx, out float by);' + "`n" + '                var pos = new Vector3(bx, by, FaceZ);')
Save-Src

# ---- T20 weights (half)
$SC = $A + 'Scripts\MeleeWeapon.cs'; Open-Src $SC
Rep '"Crowbar",      weight = 3f, length = 0.85f,' '"Crowbar",      weight = 1.5f, length = 0.85f,'
Rep '"Baseball Bat", weight = 2f, length = 1.00f,' '"Baseball Bat", weight = 1f, length = 1.00f,'
Rep '"Hatchet",      weight = 3f, length = 0.70f,' '"Hatchet",      weight = 1.5f, length = 0.70f,'
Save-Src
$SC = $A + 'Scripts\Guns\RangedWeapon.cs'; Open-Src $SC
Rep 'name = "Pew", weight = 1.2f, length = 0.30f,' 'name = "Pew", weight = 0.6f, length = 0.30f,'
Rep 'name = "SMG", weight = 1.9f, length = 0.50f,' 'name = "SMG", weight = 0.95f, length = 0.50f,'
Rep 'name = "Rifle", weight = 3.4f, length = 0.90f,' 'name = "Rifle", weight = 1.7f, length = 0.90f,'
Rep 'name = "Shotgun", weight = 3.6f, length = 1.00f,' 'name = "Shotgun", weight = 1.8f, length = 1.00f,'
Rep 'name = "Sniper", weight = 4.8f, length = 1.15f,' 'name = "Sniper", weight = 2.4f, length = 1.15f,'
Save-Src
$SC = $A + 'Editor\NetDev\NetDevDemoTest.cs'; Open-Src $SC
RepRx 'its weight is the pew''s \(2\.6 lbs\)' 'its weight is the pew''s (1.3 lbs, half of what it was: the owner, 2026-10-23)'
RepRx 'RangedWeapon\.Specs\[\(int\)c\[7\]\.gun\]\.weight, 1\.2f\)' 'RangedWeapon.Specs[(int)c[7].gun].weight, 0.6f)'
Save-Src

# ---- T21 the scan's wait
$SC = $A + 'Scripts\LootScanRules.cs'; Open-Src $SC
Rep 'public static bool NextPulse(bool holding, float sinceLast) => holding && (sinceLast < 0f || sinceLast >= PulseGap);' (
  'public static bool NextPulse(bool holding, float sinceLast) => holding && (sinceLast < 0f || sinceLast >= PulseGap);' + "`n" + "`n" +
  '        /// <summary>The scan goes off only after the key has been held this long (the owner, 2026-10-23: "Scanning should have a 3 second delay"); letting go starts the wait over.</summary>' + "`n" +
  '        public const float ChargeSeconds = 3f;' + "`n" +
  '        public static bool Charged(float heldSeconds) => heldSeconds >= ChargeSeconds;' + "`n" + "`n" +
  '        /// <summary>How far the wait has got, 0 to 1 (the bar over the scan''s status line).</summary>' + "`n" +
  '        public static float ChargeFraction(float heldSeconds) => ChargeSeconds <= 0f ? 1f : Math.Min(1f, Math.Max(0f, heldSeconds / ChargeSeconds));')
Save-Src
$SC = $A + 'Scripts\LootScanner.cs'; Open-Src $SC
Rep '        public static bool TestHold;' (
  '        public static bool TestHold;' + "`n" +
  '        /// <summary>Tests: the scan key is held like the real key - the scan then waits its <see cref="LootScanRules.ChargeSeconds"/> (<see cref="TestHold"/> is a hold that has been held long enough already).</summary>' + "`n" +
  '        public static bool TestKeyDown;' + "`n" +
  '        /// <summary>How far the held key has got towards the scan, 0 to 1 (1 once the scan has gone off and while the key stays down); 0 when the key is up.</summary>' + "`n" +
  '        public static float ScanCharge { get; private set; }')
Rep 'static void ResetStatics() { Instance = null; TestHold = false; Scanning = false; }' 'static void ResetStatics() { Instance = null; TestHold = false; TestKeyDown = false; Scanning = false; ScanCharge = 0f; }'
Rep '        float pulseAge = -1f, sinceLast = -1f, reach, nextRoomCheck;' '        float pulseAge = -1f, sinceLast = -1f, reach, nextRoomCheck, chargeHeld;'
Rep ('            bool holding = can && (Input.GetKey(ScanKey) || TestHold);' + "`n" + '            Scanning = holding;') (
  '            float dt = Time.unscaledDeltaTime;' + "`n" +
  '            // the scan goes off only after the key has been held for LootScanRules.ChargeSeconds (the owner, 2026-10-23: "Scanning should have a 3 second delay"); letting go starts the wait over; a scripted TestHold counts as held long enough' + "`n" +
  '            bool pressed = can && (Input.GetKey(ScanKey) || TestKeyDown);' + "`n" +
  '            chargeHeld = pressed ? chargeHeld + dt : 0f;' + "`n" +
  '            bool holding = can && (pressed || TestHold);' + "`n" +
  '            bool charged = TestHold || LootScanRules.Charged(chargeHeld);' + "`n" +
  '            ScanCharge = !holding ? 0f : TestHold ? 1f : LootScanRules.ChargeFraction(chargeHeld);' + "`n" +
  '            Scanning = holding;')
Rep ('            float dt = Time.unscaledDeltaTime;' + "`n" + '            if (sinceLast >= 0f) sinceLast += dt;' + "`n" + '            if (LootScanRules.NextPulse(holding, sinceLast)) StartPulse(me);') (
  '            if (sinceLast >= 0f) sinceLast += dt;' + "`n" + '            if (LootScanRules.NextPulse(holding && charged, sinceLast)) StartPulse(me);')
Rep 'if (can && (Time.time >= nextRoomCheck || holding && sinceLast < 0f)) {' 'if (can && (Time.time >= nextRoomCheck || holding && charged && sinceLast < 0f)) {'
Rep 'HudTheme.Text(r, line, 17f, HudTheme.Bright, TextAnchor.MiddleCenter, false);' (
  'HudTheme.Text(r, line, 17f, HudTheme.Bright, TextAnchor.MiddleCenter, false);' + "`n" +
  '                if (Scanning && ScanCharge < 1f) HudTheme.Bar(new Rect(r.x, r.y - 12f * s, r.width, 8f * s), ScanCharge, new Color(0.35f, 0.85f, 1f), 2f);          // (the scan''s wait of 3 s: the bar fills over the status line)')
Save-Src

# ---- T10 Vendor cards: Damage / Stun Time
$SC = $A + 'Scripts\Vendor.cs'; Open-Src $SC
Rep 'MenuKit.Wrapped(new Rect(cr.x + 12f * S, cr.y + 62f * S, cr.width - 24f * S, cr.height - 66f * S), p.blurb, 14f * S, HudTheme.Muted);' (
  'if (p.isWeapon || p.isGun)' + "`n" + '                    {' + "`n" +
  '                        // a weapon''s card says what a hit does (the owner, 2026-10-23: "Damage: x" and "Stun Time: x"): from the weapon''s own numbers' + "`n" +
  '                        float dmg = p.isGun ? WeaponCardRules.GunDamage(RangedWeapon.Specs[(int)p.gun].guardHits) : MeleeWeapon.Specs[(int)p.kind].guardDamage;' + "`n" +
  '                        float stunTime = p.isGun ? RangedWeapon.Specs[(int)p.gun].stun : MeleeWeapon.Specs[(int)p.kind].stun;' + "`n" +
  '                        HudTheme.Text(new Rect(cr.x + 12f * S, cr.y + 62f * S, cr.width - 24f * S, 22f * S), WeaponCardRules.DamageLine(dmg), 16f, HudTheme.Cream, TextAnchor.MiddleLeft, false);' + "`n" +
  '                        HudTheme.Text(new Rect(cr.x + 12f * S, cr.y + 84f * S, cr.width - 24f * S, 22f * S), WeaponCardRules.StunLine(stunTime), 16f, HudTheme.Cream, TextAnchor.MiddleLeft, false);' + "`n" +
  '                    }' + "`n" +
  '                    else MenuKit.Wrapped(new Rect(cr.x + 12f * S, cr.y + 62f * S, cr.width - 24f * S, cr.height - 66f * S), p.blurb, 14f * S, HudTheme.Muted);')
Save-Src

# ---- T15 computer tabs
$SC = $A + 'Scripts\Outpost\ComputerScreen.cs'; Open-Src $SC
Rep 'public const int TabNone = 0, TabSafes = 1, TabNav = 2, TabHacked = 3;' (
  'public const int TabNone = 0, TabSafes = 1, TabNav = 2, TabHacked = 3;' + "`n" +
  '        /// <summary>The tabs left to right (the owner, 2026-10-23: the Nav tab first, then Safes, and a third tab called Commands - the door terminal that used to be called Hacked): each keeps its id, so only this order and these names say where it stands.</summary>' + "`n" +
  '        public static readonly int[] TabOrder = { TabNav, TabSafes, TabHacked };' + "`n" +
  '        public static readonly string[] TabNames = { "Nav", "Safes", "Commands" };')
Rep 'Beep(1.1f, 0.35f);' 'if (beep) Beep(1.1f, 0.35f);'
Rep ('        void ChooseTab(int t)' + "`n" + '        {' + "`n" + '            tab = t;') (
  '        void ChooseTab(int t) => ShowTab(t);' + "`n" + "`n" +
  '        /// <summary>What a tab shows once it is up: the Safes tab writes its record into the journal, the Nav tab and the Commands tab (the door terminal) get their first lines and the keyboard. A click on a tab beeps as well; the tab that opens by itself does not.</summary>' + "`n" +
  '        void ShowTab(int t, bool beep = true)' + "`n" + '        {' + "`n" + '            tab = t;')
RepRx 'loggedIn = SafeIntel\.IsLoggedIn\(p\.outpostFloor\);[^\r\n]*' (
  'loggedIn = SafeIntel.IsLoggedIn(p.outpostFloor);          // (a player who logged in on this floor stays logged in until the run ends)' + "`n" +
  '            if (loggedIn) ShowTab(TabNav, false);                      // (the Nav tab is what opens first: the owner, 2026-10-23)')
Rep ('                user = pass = message = "";' + "`n" + '                tab = TabNone;' + "`n" + '                Beep(1.3f, 0.5f);') (
  '                user = pass = message = "";' + "`n" + '                ShowTab(TabNav, false);                                   // (signed in: the Nav tab opens first, the owner, 2026-10-23)' + "`n" + '                Beep(1.3f, 0.5f);')
Rep ('            string[] names = { "Safes", "Nav", "Hacked" };' + "`n" + '            for (int i = 0; i < 3; i++)' + "`n" + '            {' + "`n" + '                var b = new Rect(strip.x + i * (strip.width + 14f * s), strip.y, strip.width, strip.height);' + "`n" + '                if (MenuKit.Pick(b, names[i].ToUpperInvariant(), tab == i + 1, HudTheme.Quota, 22f)) ChooseTab(i + 1);') (
  '            for (int i = 0; i < TabOrder.Length; i++)' + "`n" + '            {' + "`n" + '                var b = new Rect(strip.x + i * (strip.width + 14f * s), strip.y, strip.width, strip.height);' + "`n" + '                if (MenuKit.Pick(b, TabNames[i].ToUpperInvariant(), tab == TabOrder[i], HudTheme.Quota, 22f)) ChooseTab(TabOrder[i]);')
Save-Src

# the How to Play page names the tab: the only words changed (the tab's new name, the owner's own: "commands")
$SC = $A + 'Scripts\HowToPlayContent.cs'; Open-Src $SC
Rep '(under the Nav map, or on the Hacked tab)' '(under the Nav map, or on the Commands tab)'
Rep 'the Hacked tab is the same terminal, bigger' 'the Commands tab is the same terminal, bigger'
Save-Src

# ---- T18 every safe has its code on record
$SC = $A + 'Scripts\Outpost\SafeRules.cs'; Open-Src $SC
Rep 'public const int NoteGivesCodePercent = 62;' (
  'public const int NoteGivesCodePercent = 62;' + "`n" +
  '        /// <summary>Every safe''s code is on record in the computer''s Safes tab (the owner, 2026-10-23: "All Safes should have a code in the Safes tab"). Set it to false for the old rule: a share of the records (<see cref="NoteGivesCodePercent"/>) left the code out and the safe had to be pried open.</summary>' + "`n" +
  '        public static bool EveryRecordGivesCode = true;')
Rep ('        public static bool NoteGivesCode(int runSeed, int floor) =>' + "`n" + '            Hash(runSeed, floor, SaltNoteCode) % 100U < (uint)NoteGivesCodePercent;') (
  '        public static bool NoteGivesCode(int runSeed, int floor) =>' + "`n" + '            EveryRecordGivesCode || Hash(runSeed, floor, SaltNoteCode) % 100U < (uint)NoteGivesCodePercent;')
Rep ('            if (index < 0 || index >= SafeCount(runSeed, floor)) return false;' + "`n" + '            if (!IsWeaponFloor(floor)) return NoteGivesCode(runSeed, floor);') (
  '            if (index < 0 || index >= SafeCount(runSeed, floor)) return false;' + "`n" + '            if (EveryRecordGivesCode) return true;' + "`n" + '            if (!IsWeaponFloor(floor)) return NoteGivesCode(runSeed, floor);')
Save-Src
$SC = $A + 'Editor\NetDev\NetDevR7Test.cs'; Open-Src $SC
Rep 'given * 100 >= others * 50 && given * 100 <= others * 75' 'given == others'
Rep 'the first safe''s code on a weapon floor every time and the others'' about 62 % of the time (a cash floor''s as the note had it), and a safe that is not there has none' 'the code of every safe (the owner, 2026-10-23: all of them), and a safe that is not there has none'
Save-Src

# ---- T8 drop all loot
$SC = $A + 'Scripts\Pickup.cs'; Open-Src $SC
Rep 'public int Value => Loot ? Loot.CurrentValue : bankValue;' (
  'public int Value => Loot ? Loot.CurrentValue : bankValue;' + "`n" +
  '        /// <summary>Is this a piece of loot (the owner, 2026-10-23: "Drop all loot is not dropping all loot properly")? A heavy treasure, or anything with a cash value to bank: the small valuables and the cash bundles have a bank value and no HeavyLoot. Weapons, armour, keys and ammunition have none.</summary>' + "`n" +
  '        public bool CountsAsLoot => Loot != null || bankValue > 0;')
Save-Src
$SC = $A + 'Scripts\Backpack.cs'; Open-Src $SC
RepRx 'if \(p && p\.Loot\) ApplyDropped\(p, DropPosition\(\)\);' 'if (p && p.CountsAsLoot) ApplyDropped(p, DropPosition());'
Save-Src
$SC = $A + 'Scripts\Net\RunSync.Requests.cs'; Open-Src $SC
Rep 'if (p && p.Loot && p.NetId >= 0) DroppedRpc((ushort)p.NetId, (byte)np.Slot, bp.DropPosition());' 'if (p && p.CountsAsLoot && p.NetId >= 0) DroppedRpc((ushort)p.NetId, (byte)np.Slot, bp.DropPosition());'
Save-Src

# ---- T17 floor 1's crowbar
$SC = $A + 'Scripts\FloorGenerator.Outpost.cs'; Open-Src $SC
Rep '/// <summary>How many loose melee weapons a floor has (the owner: floor 1 always one and sometimes two; floors 2-12 a chance of one, never two).</summary>' (
  '/// <summary>' + "`n" +
  '        /// The one loose crowbar of floor 1 (the owner, 2026-10-23: "One random crowbar spawn on the first floor outside of the outpost"): it lies on the floor at one of the floor''s walkable patrol spots outside the outpost, chosen from the run''s seed' + "`n" +
  '        /// (a new place every run, the same on every machine) and never from the floor''s own random numbers. Other floors get none.' + "`n" +
  '        /// </summary>' + "`n" +
  '        void FillFloorOneCrowbar(int floor)' + "`n" + '        {' + "`n" +
  '            if (floor != 1 || layout == null || !Application.isPlaying || !MeleeWeapon.Available) return;' + "`n" +
  '            var spots = new System.Collections.Generic.List<float[]>();' + "`n" +
  '            foreach (var w in layout.waypoints)' + "`n" + '            {' + "`n" +
  '                var at = new Vector3(w[0], 0f, w[1]);' + "`n" +
  '                if (!InOutpost(at)) spots.Add(w);                           // (the planner''s patrol stops are free floor in rooms and corridors: never the guards'' shafts, a restroom, a suite or the server room)' + "`n" + '            }' + "`n" +
  '            if (spots.Count == 0) return;' + "`n" +
  '            var pick = spots[(int)(SafeRules.Hash(intelNow, floor, 741) % (uint)spots.Count)];' + "`n" +
  '            float yaw = SafeRules.Hash(intelNow, floor, 742) % 360U;' + "`n" +
  '            MeleeWeapon.Create(MeleeWeapon.Kind.Crowbar, new Vector3(pick[0], 0.08f, pick[1]), current, yaw);' + "`n" + '        }' + "`n" + "`n" +
  '        /// <summary>How many loose melee weapons a floor has (the owner: floor 1 always one and sometimes two; floors 2-12 a chance of one, never two).</summary>')
Save-Src
$SC = $A + 'Scripts\FloorGenerator.cs'; Open-Src $SC
Rep 'OutpostStep("the hallway loudspeakers", floor, () => BuildSpeakers(floor));' (
  'OutpostStep("floor 1''s crowbar", floor, () => FillFloorOneCrowbar(floor));              // (the owner, 2026-10-23: one random crowbar on floor 1, outside the outpost; the run seed''s numbers only)' + "`n" +
  '                OutpostStep("the hallway loudspeakers", floor, () => BuildSpeakers(floor));')

# ---- T14 guards by floor
Rep 'int count = Mathf.Min(maxGuards, GuardRules.PatrolGuards(floor, baseGuards));' 'int count = GuardRules.PatrolCount(floor, baseGuards);          // (rises with the floor to GuardRules.PatrolCap: the owner, 2026-10-23)'
Rep 'public int maxGuards = 12;' ('[Tooltip("Not used since 2026-10-23: the most patrolling guards is GuardRules.PatrolCap and it rises with the floor.")]' + "`n" + '        public int maxGuards = 12;')
Rep '[Tooltip("Most guards alive on a floor at once, counting backup that gets called in.")]' '[Tooltip("Not used since 2026-10-23: the most guards alive at once, backup included, is GuardRules.MaxGuardsAlive (24).")]'
Save-Src
$SC = $A + 'Scripts\GuardRules.cs'; Open-Src $SC
Rep 'public static int PatrolGuards(int floor, int baseGuards) => baseGuards + floor + (floor >= BigFloor ? BigFloorBonus : 0);' (
  'public static int PatrolGuards(int floor, int baseGuards) => baseGuards + floor + (floor >= BigFloor ? BigFloorBonus : 0);' + "`n" + "`n" +
  '        /// <summary>The most guards alive at once while the alarm is on (the owner, 2026-10-23: "the cap while the alarm is on should stay at 24 (this number may change later on)"): the patrols and the reinforcements together. The one place to change it.</summary>' + "`n" +
  '        public const int MaxGuardsAlive = 24;' + "`n" + "`n" +
  '        /// <summary>The most guards that PATROL a floor (the owner, 2026-10-23: the guards scale with the floor): the rule above rises by one a floor and is cut here (floor 13: 18), which leaves room under <see cref="MaxGuardsAlive"/> for the officers a backup call sends.</summary>' + "`n" +
  '        public const int PatrolCap = 18;' + "`n" + "`n" +
  '        /// <summary>The patrolling guards on a floor: <see cref="PatrolGuards"/>, never above <see cref="PatrolCap"/>.</summary>' + "`n" +
  '        public static int PatrolCount(int floor, int baseGuards) => Math.Min(PatrolCap, PatrolGuards(floor, baseGuards));')
Save-Src
$SC = $A + 'Scripts\HeistManager.cs'; Open-Src $SC
Rep 'int max = gen ? gen.maxGuardsAlive : 24;' 'int max = GuardRules.MaxGuardsAlive;'
Save-Src
$SC = $A + 'Editor\NetDev\NetDevGuardsTest.cs'; Open-Src $SC
Rep 'Mathf.Min(12, GuardRules.PatrolGuards(floor, 3))' 'GuardRules.PatrolCount(floor, 3)'
Save-Src

# ---- the test and the scenario are registered
$SC = $A + 'Editor\NetDev\NetDevTest.cs'; Open-Src $SC
Rep '            DoorSkinTest();' ('            DoorSkinTest();' + "`n" + '            Asks1023Test();')
Save-Src
$SC = $A + 'Editor\NetDev\NetDevHarness.cs'; Open-Src $SC
Rep '        public static void DoorLooks()' ('        public static void Asks1023()' + "`n" + '        {' + "`n" + '            SessionState.SetString("netdev.mode", "asks1023");' + "`n" + '            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");' + "`n" + '            EditorApplication.isPlaying = true;' + "`n" + '        }' + "`n" + '        public static void DoorLooks()')
Rep '&& mode != "doorlooks")' '&& mode != "doorlooks" && mode != "asks1023")'
Rep '                case "doorlooks": yield return DoorLooksScenario(); break;' ('                case "doorlooks": yield return DoorLooksScenario(); break;' + "`n" + '                case "asks1023": yield return Asks1023Scenario(); break;')
Save-Src
"A1 patched"
