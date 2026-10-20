using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// Round 7 (the owner's list of the first two real playtests, 2026-10-18): the pure rules of the items that have one. Footsteps of teammates have a radius, quotas are higher, a safe on every floor,
        /// the backup comes faster on the higher floors, floor 3 has more guards, from floor 4 the guards wear body armour; (later batches add their checks here).
        /// </summary>
        static bool MunchSoundOk()
        {
            for (int v = 0; v < OutpostSound.MunchVariants; v++)
            {
                var data = OutpostSound.Munch(v);
                float peak = 0f; foreach (var x in data) peak = Mathf.Max(peak, Mathf.Abs(x));
                if (data.Length < Dsp.Samples(0.2f) || data.Length > Dsp.Samples(0.8f) || peak < 0.3f || peak > 1.001f) return false;
            }
            return true;
        }

        static bool SamePath(Vector2[] a, Vector2[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if ((a[i] - b[i]).sqrMagnitude > 1e-8f) return false;
            return true;
        }

        public static void Round7Test()
        {
            // ---- footsteps: the player's own steps are flat, a teammate's and a guard's are 3D (heard near, silent far away)
            Check("footsteps: the player the camera follows hears his own steps flat; a teammate's and a guard's are in 3D", FootstepPlayer.IsFlat(false, true) && !FootstepPlayer.IsFlat(false, false) && !FootstepPlayer.IsFlat(true, false) && !FootstepPlayer.IsFlat(true, true));
            Check("footsteps: a teammate's steps carry a short way (under the guards' 30 m, over his own metre)", FootstepPlayer.TeammateRange > 8f && FootstepPlayer.TeammateRange < GameAudio.DefaultMaxDistance && FootstepPlayer.TeammateNear < FootstepPlayer.TeammateRange);

            // ---- quotas: higher than the old ones by the multiplier, the loot's calibration unchanged
            Check("quota: floor 1 asks for the old quota times the multiplier", TowerRules.QuotaFor(1) == (int)(System.Math.Round(TowerRules.FirstQuota * TowerRules.QuotaMultiplier / 100.0) * 100.0) && TowerRules.QuotaMultiplier > 1.0);
            bool climbs = true; for (int f = 1; f < 12; f++) climbs &= TowerRules.QuotaFor(f + 1) > TowerRules.QuotaFor(f);
            Check("quota: still rising with the floor, none on the docks and the boss floor", climbs && TowerRules.QuotaFor(0) == 0 && TowerRules.QuotaFor(13) == 0);
            Check("quota: the loot's scale of a floor is as it was calibrated (floor 1: 1.0, the same numbers as before the multiplier)", System.Math.Abs(Valuables.Scale(1) - 1.0) < 1e-9 && Valuables.Scale(4) > 1.0 && Valuables.Scale(4) < 3.0);

            // ---- a safe on every floor (floors 1-12), none on the docks or the boss floor
            bool all = true; for (int seed = 0; seed < 50; seed++) for (int f = 1; f <= 12; f++) all &= SafeRules.HasSafe(seed * 977 + 3, f);
            Check("safes: every office floor from 1 to 12 has a safe, whatever the run seed", all && !SafeRules.HasSafe(1, 0) && !SafeRules.HasSafe(1, 13) && SafeRules.SafeFloors(5).Length == 12);
            bool notes = true; for (int f = 1; f <= 12; f++) notes &= SafeRules.NoteFor(11, f) >= 1;
            Check("safes: every guard outpost holds a note about a safe now (each outpost holds one)", notes);

            // ---- the backup comes faster on the higher floors
            bool faster = true; float last = 1000f;
            for (int f = 1; f <= 14; f++) { float i = CustodyRules.BackupInterval(f); if (i > last + 1e-4f) faster = false; last = i; }
            Check("backup: the interval shrinks with the floor (never longer)", faster);
            Check("backup: 30 s on floor 1 as before, 12 s on floor 12 and above, in between on the floors between", Mathf.Approximately(CustodyRules.BackupInterval(1), 30f) && Mathf.Approximately(CustodyRules.BackupInterval(12), 12f) && Mathf.Approximately(CustodyRules.BackupInterval(13), 12f)
                  && CustodyRules.BackupInterval(4) < 30f && CustodyRules.BackupInterval(4) > 12f && CustodyRules.BackupInterval(0) == 30f);
            var clock = new BackupClock();
            clock.Start(CustodyRules.BackupInterval(12));
            bool ran = Mathf.Approximately(clock.Interval, 12f) && !clock.Tick(11f) && clock.Tick(1.1f);
            clock.Start(30f);                                      // (already running: changes nothing)
            Check("backup: the clock runs with the interval it was started with, and a later start does not change it", ran && Mathf.Approximately(clock.Interval, 12f));

            // ---- floor 3 has a decent step more guards than floor 2; every floor has more than the one before
            bool more = true; for (int f = 1; f < 9; f++) more &= GuardRules.PatrolGuards(f + 1, 3) > GuardRules.PatrolGuards(f, 3);
            Check("guards: floor 2 has 5, floor 3 has 8 (three more: a decent step), floor 4 has 9; each floor has more than the one before", GuardRules.PatrolGuards(2, 3) == 5 && GuardRules.PatrolGuards(3, 3) == 8 && GuardRules.PatrolGuards(4, 3) == 9 && GuardRules.PatrolGuards(1, 3) == 4 && more);

            // ---- body armour from floor 4 on: a bullet counts for half the hits, so the pew needs five bullets instead of three
            Check("armour: floors 1-3 have none, floors 4-12 have it, the boss floor does not", !GuardRules.Armoured(1) && !GuardRules.Armoured(3) && GuardRules.Armoured(4) && GuardRules.Armoured(12) && !GuardRules.Armoured(13) && !GuardRules.Armoured(0));
            var pew = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew];
            Check("armour: an armoured officer takes five pew bullets where an unarmoured one takes three", GunRules.BulletsToKill(10, pew.guardHits) == 3 && GunRules.BulletsToKill(10, GuardRules.ArmouredHits(pew.guardHits)) == 5, $"({pew.guardHits} hits a bullet, {GuardRules.ArmouredHits(pew.guardHits)} against armour)");
            Check("armour: a bullet always counts for at least one hit", GuardRules.ArmouredHits(1) == 1 && GuardRules.ArmouredHits(0) == 1 && GuardRules.ArmouredHits(2) == 1 && GuardRules.ArmouredHits(8) == 4);

            // ---- a body goes away (the owner: "Guards bodies should de-spawn"): 15 s, then a second of sinking
            Check("corpse: a body lies there for 15 s, sinks away in one more second and is gone", GuardRules.CorpseSink(0f) == 0f && GuardRules.CorpseSink(14.9f) == 0f && GuardRules.CorpseSink(15.5f) > 0.4f && GuardRules.CorpseSink(15.5f) < 0.6f && GuardRules.CorpseSink(16f) >= 1f && GuardRules.CorpseSink(60f) == 1f);
            Check("corpse: with a shorter wait (the tests) it is the same curve", GuardRules.CorpseSink(1.2f, 1.5f) == 0f && GuardRules.CorpseSink(2.0f, 1.5f) > 0.4f && GuardRules.CorpseSink(2.6f, 1.5f) == 1f);

            // ---- stamina (the owner: "There should be stamina with a stamina bar on the HUD")
            var sm = new StaminaMeter();
            Check("stamina: a new meter is full and the player can sprint", Mathf.Approximately(sm.Fraction, 1f) && sm.CanSprint && !sm.Exhausted);
            sm.Step(3f, true);
            Check("stamina: three seconds of sprinting take 48 of 100", Mathf.Abs(sm.Value - 52f) < 0.01f && sm.CanSprint);
            sm.Step(4f, true);
            Check("stamina: a little over six seconds of sprinting run it dry: winded, no sprint", sm.Value <= 0.001f && sm.Exhausted && !sm.CanSprint);
            sm.Step(0.9f, false);
            Check("stamina: a breather of a second first, nothing comes back in it", sm.Value <= 0.001f && sm.Exhausted);
            sm.Step(1.1f, false);
            Check("stamina: then twelve a second come back (still winded)", Mathf.Abs(sm.Value - 13.2f) < 0.01f && sm.Exhausted && !sm.CanSprint);
            sm.Step(1.1f, false);
            Check("stamina: a quarter of the bar ends the winded spell", sm.Value >= 25f && !sm.Exhausted && sm.CanSprint);
            var m2 = new StaminaMeter(); var m3 = new StaminaMeter();
            m2.Step(2f, true); m3.Step(2f, true, 0.4f);
            Check("stamina: with the drain turned down to 40 % (a buff) the same two seconds cost 40 % as much", Mathf.Abs((100f - m3.Value) - 0.4f * (100f - m2.Value)) < 0.01f);
            var m4 = new StaminaMeter(); m4.Step(5f, false);
            Check("stamina: walking and standing cost nothing", Mathf.Approximately(m4.Value, 100f));
            m2.Refill();
            Check("stamina: a refill (a new floor, a test) makes it full at once", Mathf.Approximately(m2.Value, 100f) && !m2.Exhausted);

            // ---- the silencer: the player's pew is quiet and the officers' stays loud
            var pewSpec = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew];
            Check("silencer: a silenced shot is heard from 12 m, an officer's shot from 45 m", Mathf.Approximately(pewSpec.silencedNoise, 12f) && Mathf.Approximately(pewSpec.noise, 45f) && pewSpec.silencerLength > 0.05f && pewSpec.silencerLength < 0.3f);
            Check("silencer: the shot and its tail are quieter (the flash is smaller, in GunFx)", GunSounds.SilencedShotVolume < 0.6f && GunSounds.SilencedTailVolume < GunSounds.SilencedShotVolume);

            // ---- the terminal: OUTPOST LOCK / OUTPOST UNLOCK work the door of the outpost itself (the owner, 2026-10-18)
            FloorDirectory.ParseTerminal("Outpost Lock", out var tr1, out var tv1);
            FloorDirectory.ParseTerminal("outpost UNLOCK", out var tr2, out var tv2);
            FloorDirectory.ParseTerminal("lock outpost", out var tr3, out var tv3);
            Check("terminal: OUTPOST LOCK and OUTPOST UNLOCK (any capitals, either order) name the outpost's own door and the verb",
                FloorDirectory.IsOutpostName(tr1) && string.Equals(tv1, "lock", System.StringComparison.OrdinalIgnoreCase)
                && FloorDirectory.IsOutpostName(tr2) && string.Equals(tv2, "unlock", System.StringComparison.OrdinalIgnoreCase)
                && FloorDirectory.IsOutpostName(tr3) && string.Equals(tv3, "lock", System.StringComparison.OrdinalIgnoreCase));

            // ---- the hallway loudspeakers (the owner, 2026-10-18)
            var corridors = new System.Collections.Generic.List<Rc> { new Rc(0f, 0f, 40f, 3f), new Rc(10f, 3f, 13f, 30f), new Rc(20f, 0f, 24f, 4f) };
            var plan = PaRules.Plan(corridors);
            Check("speakers: one every 13 m along the middle of a hallway (40 m: three, 27 m: two), none in a piece that is too short", plan.Count == 3 + 2 && Mathf.Abs(plan[0].y - 1.5f) < 0.01f && Mathf.Abs(plan[3].x - 11.5f) < 0.01f, $"({plan.Count})");
            var big = new System.Collections.Generic.List<Rc>(); for (int i = 0; i < 30; i++) big.Add(new Rc(0f, i * 4f, 60f, i * 4f + 3f));
            Check("speakers: never more than 48 (a bit each in a mask), and the plan is the same twice", PaRules.Plan(big).Count <= PaRules.MaxSpeakers && PaRules.Plan(big).Count == PaRules.Plan(big).Count && PaRules.Plan(corridors).Count == plan.Count);
            ulong mask5 = PaRules.MaskOf(5);
            Check("speakers: masks: all on, one toggled off and back, a count", PaRules.IsOn(mask5, 0) && PaRules.IsOn(mask5, 4) && !PaRules.IsOn(mask5, 5) && !PaRules.IsOn(PaRules.Toggle(mask5, 2), 2) && PaRules.IsOn(PaRules.Toggle(PaRules.Toggle(mask5, 2), 2), 2) && PaRules.CountOn(PaRules.Toggle(mask5, 2), 5) == 4 && PaRules.MaskOf(0) == 0UL);
            var two = new System.Collections.Generic.List<Vector2> { new Vector2(10f, 10f), new Vector2(60f, 10f) };
            Check("speakers: an officer within 22 m of a speaker that is on is in earshot; one near a speaker that is switched off, or too far, is not",
                PaRules.InEarshot(new Vector3(20f, 0f, 10f), two, 3UL) && !PaRules.InEarshot(new Vector3(35f, 0f, 10f), two, 3UL) && !PaRules.InEarshot(new Vector3(20f, 0f, 10f), two, 2UL) && PaRules.InEarshot(new Vector3(70f, 0f, 10f), two, 2UL));
            Check("speakers: the PA is full to 6 m, silent from 28 m, and falls away in between; the nearest speaker that is on carries it",
                PaRules.GainAt(3f) == 1f && PaRules.GainAt(30f) == 0f && PaRules.GainAt(17f) > 0.1f && PaRules.GainAt(17f) < 0.5f && PaRules.GainAt(new Vector3(14f, 0f, 10f), two, 3UL, out int nearA) > 0.9f && nearA == 0 && PaRules.GainAt(new Vector3(14f, 0f, 10f), two, 2UL, out int nearB) < 0.01f && nearB == 1);

            Check("speakers: a listener hears the louder of the voice in the air and the PA out of the nearest speaker (a little under the voice at full volume)", Mathf.Approximately(PaRules.Mix(0.2f, 1f), PaRules.PaVolume) && PaRules.Mix(1f, 0.3f) == 1f && PaRules.Mix(0f, 0f) == 0f && PaRules.PaVolume < 1f && PaRules.PaVolume > 0.5f);

            // ---- the microphone's arm: a two-bone reach for the button
            {
                var chain = new GameObject("TestArm").transform;
                var up = new GameObject("U").transform; up.SetParent(chain, false); up.localPosition = Vector3.zero;
                var lo = new GameObject("L").transform; lo.SetParent(up, false); lo.localPosition = new Vector3(0.3f, 0f, 0f);
                var hd = new GameObject("H").transform; hd.SetParent(lo, false); hd.localPosition = new Vector3(0.3f, 0f, 0f);
                var near = new Vector3(0.3f, 0.3f, 0.1f);
                ArmReach.TwoBone(up, lo, hd, near, 1f);
                bool reached = Vector3.Distance(hd.position, near) < 0.03f;
                ArmReach.TwoBone(up, lo, hd, new Vector3(2f, 0f, 0f), 1f);
                bool pointed = Vector3.Distance(hd.position, up.position) > 0.55f && Mathf.Abs(Vector3.Dot((hd.position - up.position).normalized, Vector3.right)) > 0.98f;
                var before = hd.position;
                ArmReach.TwoBone(up, lo, hd, new Vector3(0f, 1f, 0f), 0f);
                bool untouched = Vector3.Distance(hd.position, before) < 1e-4f;
                Object.DestroyImmediate(chain.gameObject);
                Check("microphone: an arm turns so that the hand reaches a point within its reach, points at one beyond it, and a weight of nothing leaves it alone", reached && pointed && untouched, $"(reached {reached}, pointed {pointed}, untouched {untouched})");
            }

            // ---- the test version of the computer's map: the view, the ping tracker, what a typed number points at (the owner, 2026-10-18)
            {
                var nmExt = new Rc(0f, 0f, 40f, 24f);
                var nmV0 = NavView.Fit(nmExt, new Vector2(800f, 400f), 14f);
                bool nmFit = Mathf.Abs(nmV0.K - 15.5f) < 0.01f;
                var nmMid = nmV0.ToScreen(20f, 12f);
                var nmNw = nmV0.ToScreen(0f, 24f);
                bool nmCentre = Mathf.Abs(nmMid.x - 400f) < 0.01f && Mathf.Abs(nmMid.y - 200f) < 0.01f && nmNw.x < nmMid.x && nmNw.y < nmMid.y;
                var nmBack = nmV0.ToWorld(nmV0.ToScreen(7.5f, 3.25f));
                bool nmRound = Mathf.Abs(nmBack.x - 7.5f) < 1e-3f && Mathf.Abs(nmBack.y - 3.25f) < 1e-3f;
                var nmAnchor = new Vector2(430f, 210f);
                var nmZ2 = nmV0.ZoomAt(2f, nmAnchor);
                bool nmZoom = Mathf.Approximately(nmZ2.zoom, 2f) && Vector2.Distance(nmV0.ToWorld(nmAnchor), nmZ2.ToWorld(nmAnchor)) < 0.01f;
                bool nmLimits = Mathf.Approximately(nmV0.ZoomAt(0.1f, nmAnchor).zoom, NavView.MinZoom) && Mathf.Approximately(nmV0.ZoomAt(1000f, nmAnchor).zoom, NavView.MaxZoom) && nmV0.ZoomAt(0.5f, nmAnchor).pan == Vector2.zero;
                var nmDrag = nmZ2.Dragged(nmZ2.pan, new Vector2(100000f, -100000f));
                bool nmClamp = Mathf.Abs(nmDrag.pan.x) < 1000f && Mathf.Abs(nmDrag.pan.y) < 1000f;
                var nmC = nmZ2.CentreOn(28f, 9f);
                bool nmCentreOn = Vector2.Distance(nmC.ToScreen(28f, 9f), new Vector2(400f, 200f)) < 1f;
                Check("nav map: the plan keeps the floor centred with north up, converts both ways, zooms about the point under the mouse, stays inside its limits and can be centred on a spot", nmFit && nmCentre && nmRound && nmZoom && nmLimits && nmClamp && nmCentreOn,
                      $"(fit {nmFit}, centre {nmCentre}, round {nmRound}, zoom {nmZoom}, limits {nmLimits}, clamp {nmClamp}, centreOn {nmCentreOn})");

                var nmM = new NavMotion();
                for (int i = 0; i <= 60; i++) nmM.Step(1, 5f, 5f, i * 0.05f);                                  // stands still for 3 s
                bool nmStill = nmM.Get(1) != null && !nmM.Get(1).IsMoving && nmM.Get(1).emitted == 0 && nmM.Get(1).pings.Count == 0 && nmM.Get(1).trail.Count == 0;
                for (int i = 0; i <= 56; i++) nmM.Step(2, 2f * i * 0.05f, 0f, i * 0.05f);                      // walks at 2 m/s for 2.8 s
                var nmT = nmM.Get(2);
                bool nmWalk = nmT.IsMoving && nmT.emitted == 3 && nmT.pings.Count >= 1 && nmT.pings.Count <= 2 && nmT.trail.Count >= 8 && nmT.trail.Count <= 12;
                float nmX = 2f * 56 * 0.05f;
                for (int i = 57; i <= 100; i++) nmM.Step(2, nmX, 0f, i * 0.05f);                               // then stops for 2.2 s
                bool nmStops = !nmT.IsMoving && nmT.emitted == 3 && nmT.pings.Count == 0;
                nmM.Step(3, 0f, 0f, 0f); nmM.Step(3, 30f, 0f, 0.05f);
                bool nmJump = !nmM.Get(3).IsMoving;                                                              // 30 m in one frame is a jump, not walking
                nmM.Sweep(10f);
                bool nmSweep = nmM.Count == 0;
                Check("nav map: an icon that stands still never pings, one that walks pings about every 0.9 s and leaves a trail, the pings and the movement stop with it, a jump is not walking, and what is gone is forgotten",
                      nmStill && nmWalk && nmStops && nmJump && nmSweep, $"(still {nmStill}, walk {nmWalk} ({nmT.emitted} pings, {nmT.trail.Count} dots), stops {nmStops}, jump {nmJump}, sweep {nmSweep})");

                var nmLabels = new System.Collections.Generic.List<string> { "101A", "101", "1010", "201", "102" };
                var nmF = NavMapRules.Matches(nmLabels, " 101 ");
                bool nmFind = nmF.Count == 3 && nmF[0] == 1 && nmF[1] == 0 && nmF[2] == 2 && NavMapRules.Matches(nmLabels, "101a").Count == 1 && NavMapRules.Matches(nmLabels, "").Count == 0
                              && NavMapRules.Matches(nmLabels, "9").Count == 0 && NavMapRules.Matches(nmLabels, "1", 2).Count == 2;
                bool nmKinds = NavMapRules.KindOf("outpost", null, 2) == NavMapRules.RoomKind.Outpost && NavMapRules.KindOf("office", "suite", 0) == NavMapRules.RoomKind.Suite && NavMapRules.KindOf("office", "server", 0) == NavMapRules.RoomKind.Server
                               && NavMapRules.KindOf("hall", null, 2) == NavMapRules.RoomKind.Corridor && NavMapRules.KindOf("open", null, 1) == NavMapRules.RoomKind.Zone && NavMapRules.KindOf("office", null, 0) == NavMapRules.RoomKind.Room;
                bool nmPingMath = NavMapRules.PingRadius(0f, 10f) >= 10f && NavMapRules.PingRadius(0f, 10f) < NavMapRules.PingRadius(0.5f, 10f) && NavMapRules.PingRadius(0.5f, 10f) < NavMapRules.PingRadius(1f, 10f) && NavMapRules.PingRadius(1f, 10f) <= 33.01f
                                  && NavMapRules.PingAlpha(0f) <= 0.6001f && NavMapRules.PingAlpha(1f) == 0f && NavMapRules.PingAlpha(0.3f) > NavMapRules.PingAlpha(0.6f) && Mathf.Approximately(NavMapRules.PingT(5f, 5f + NavMotion.PingLife * 0.5f), 0.5f);
                Check("nav map: a typed number points at the room with exactly that number first and then the rooms that start with it, every kind of room has its look, and a ping spreads to 3.3 times its icon and fades (never above 0.6)",
                      nmFind && nmKinds && nmPingMath, $"(find {nmFind}, kinds {nmKinds}, ping {nmPingMath})");
            }

            // ---- the door terminal: a room number on its own switches the doors the other way (the owner, 2026-10-18)
            {
                bool trmOpen = ComputerRules.WantLocked(null, false, out bool trmL1) && trmL1;                 // (open doors: lock)
                bool trmShut = ComputerRules.WantLocked(null, true, out bool trmL2) && !trmL2;                 // (locked doors: unlock)
                bool trmLock = ComputerRules.WantLocked("lock", true, out bool trmL3) && trmL3 && ComputerRules.WantLocked(" LoCk ", false, out bool trmL3b) && trmL3b;
                bool trmUnlock = ComputerRules.WantLocked("UNLOCK", false, out bool trmL4) && !trmL4 && ComputerRules.WantLocked("unlock", true, out bool trmL4b) && !trmL4b;
                bool trmBad = !ComputerRules.WantLocked("foo", false, out _) && !ComputerRules.WantLocked("", true, out _) && !ComputerRules.WantLocked("lock 507", false, out _);
                Check("terminal: a room number on its own switches the doors the other way, LOCK and UNLOCK still say which way, any other word is refused", trmOpen && trmShut && trmLock && trmUnlock && trmBad, $"(open {trmOpen}, locked {trmShut}, lock {trmLock}, unlock {trmUnlock}, refused {trmBad})");
            }

            // ---- the speaker bends over the desk, and slides towards it, when the button is further than his arm
            {
                var pivot = Vector3.zero; var shoulder = new Vector3(0f, 0.5f, 0f);
                bool planNear = ArmReach.PlanLean(pivot, shoulder, 0.55f, new Vector3(0.3f, 0.4f, 0f), out float dN, out float sN) && dN == 0f && sN == 0f;
                bool planLean = ArmReach.PlanLean(pivot, shoulder, 0.55f, new Vector3(0.65f, 0.3f, 0f), out float dL, out float sL) && dL > 0f && dL <= 35f && sL == 0f;
                bool planSlide = ArmReach.PlanLean(pivot, shoulder, 0.55f, new Vector3(0.95f, 0.3f, 0f), out float dS, out float sS) && Mathf.Approximately(dS, 35f) && sS > 0f && sS <= 0.55f;
                bool planNone = !ArmReach.PlanLean(pivot, shoulder, 0.55f, new Vector3(3f, 0.3f, 0f), out float dX, out float sX) && dX == 0f && sX == 0f;
                var body = new GameObject("TestBody").transform;
                var waist = new GameObject("W").transform; waist.SetParent(body, false);
                var sh = new GameObject("S").transform; sh.SetParent(waist, false); sh.localPosition = shoulder;
                ArmReach.Bend(waist, new Vector3(2f, 0f, 0f), 30f, 1f);
                bool bent = Mathf.Abs(sh.position.x - 0.25f) < 0.01f && Mathf.Abs(sh.position.y - 0.433f) < 0.01f;
                var rotBefore = waist.rotation;
                ArmReach.Bend(waist, new Vector3(2f, 0f, 0f), 30f, 0f);
                ArmReach.Bend(waist, new Vector3(2f, 0f, 0f), 0f, 1f);
                bool still = Quaternion.Angle(waist.rotation, rotBefore) < 0.01f;
                Object.DestroyImmediate(body.gameObject);
                Check("microphone: a body within reach stays as it is, one a little too far bends over from the waist, one further slides towards the target as well, one out of reach does nothing", planNear && planLean && planSlide && planNone, $"(near {planNear}, lean {planLean} {dL:0} deg, slide {planSlide} {dS:0} deg + {sS:0.00} m, none {planNone})");
                Check("microphone: the waist bends towards the target by the angle asked, and a weight or an angle of nothing leaves it alone", bent && still, $"(bent {bent}, still {still})");
            }

            // ---- the gun safe's hack (the owner, 2026-10-18)
            var gsPath = GunSafeHackRules.MakePath(3);
            bool inPanel = true; foreach (var p in gsPath) inPanel &= p.x > 0.5f && p.x < GunSafeHackRules.PanelWidth - 0.5f && p.y > 0.5f && p.y < GunSafeHackRules.PanelHeight - 0.5f;
            Check("gun safe: the corridor is seven points from the left of the panel to the right, inside the panel, the same for the same seed and different for another", gsPath.Length == GunSafeHackRules.Points && gsPath[0].x < 2f && gsPath[gsPath.Length - 1].x > 14f && inPanel && SamePath(gsPath, GunSafeHackRules.MakePath(3)) && !SamePath(gsPath, GunSafeHackRules.MakePath(4)));
            var gsWin = new GunSafeHackSession(3);
            gsWin.Press(gsWin.Path[0]);
            for (int i = 0; i + 1 < gsWin.Path.Length && !gsWin.Finished; i++)
            {
                float segLen = (gsWin.Path[i + 1] - gsWin.Path[i]).magnitude;
                int steps = Mathf.CeilToInt(segLen / 0.2f);
                for (int k = 1; k <= steps && !gsWin.Finished; k++) gsWin.Move(Vector2.Lerp(gsWin.Path[i], gsWin.Path[i + 1], k / (float)steps));
            }
            Check("gun safe: following the corridor from the green circle to the red one wins, with no wall touched", gsWin.Won && !gsWin.Hit && gsWin.Finished);
            var gsLine = new GunSafeHackSession(3);
            gsLine.Press(gsLine.Path[0]);
            for (int k = 1; k <= 12; k++) gsLine.Move(Vector2.Lerp(gsLine.Path[0], gsLine.Path[gsLine.Path.Length - 1], k / 12f));
            Check("gun safe: a straight line across the corridor's walls hits one (the alarm)", gsLine.Hit && !gsLine.Won);
            var gsAway = new GunSafeHackSession(3);
            gsAway.Press(gsAway.Path[0] + new Vector2(0f, 3f)); gsAway.Move(gsAway.Path[0] + new Vector2(0.5f, 3f));
            Check("gun safe: pressing away from the green circle starts nothing, and nothing can hit a wall then", !gsAway.Drawing && !gsAway.Hit);
            var gsLet = new GunSafeHackSession(3);
            gsLet.Press(gsLet.Path[0]); gsLet.Move(Vector2.Lerp(gsLet.Path[0], gsLet.Path[1], 0.5f)); gsLet.Release();
            bool restarted = false; gsLet.Press(gsLet.Path[0]); restarted = gsLet.Drawing;
            Check("gun safe: letting go before the end is only giving the stroke up (no alarm, and a new one can start)", !gsLet.Hit && !gsLet.Won && restarted && gsLet.Along == 0f);
            float gsAlong; float gsOff = GunSafeHackRules.DistanceToPath(gsPath[2] + new Vector2(0f, 0.3f), gsPath, out gsAlong);
            Check("gun safe: the distance to the corridor's middle line and the way along it are measured", Mathf.Abs(gsOff - 0.3f) < 0.01f && gsAlong > 0f && gsAlong < GunSafeHackRules.Length(gsPath));

            // ---- the PVP room (the owner, 2026-10-17)
            Check("pvp: health 100, a fist takes 8, a bullet 34 (three bullets knock a player out, two do not), a knock-out lasts a few seconds", Mathf.Approximately(PvpRules.MaxHealth, 100f) && PvpRules.FistDamage == 8f && PvpRules.BulletDamage == 34f && 3 * PvpRules.BulletDamage > PvpRules.MaxHealth && 2 * PvpRules.BulletDamage < PvpRules.MaxHealth && PvpRules.KnockoutSeconds >= 3f && PvpRules.KnockoutSeconds <= 6f);
            Check("pvp: health never goes below nothing, and knocked out is at nothing", PvpRules.After(10f, 34f) == 0f && PvpRules.KnockedOut(0f) && !PvpRules.KnockedOut(0.1f) && PvpRules.After(50f, -5f) == 50f);
            Check("pvp: a hit counts only when both are inside the room, neither is down and they are two players", PvpRules.CanHurt(true, true, false, false, false) && !PvpRules.CanHurt(false, true, false, false, false) && !PvpRules.CanHurt(true, false, false, false, false) && !PvpRules.CanHurt(true, true, true, false, false) && !PvpRules.CanHurt(true, true, false, true, false) && !PvpRules.CanHurt(true, true, false, false, true));
            var pvpRoom = new Rect(0.2f, 13.2f, 9.1f, 4.6f);
            Check("pvp: inside the room means inside its rectangle (a hand's breadth of the doorway counts); an empty room holds nobody", PvpRules.Inside(pvpRoom, 5f, 15f) && PvpRules.Inside(pvpRoom, 9.35f, 15f) && !PvpRules.Inside(pvpRoom, 12f, 15f) && !PvpRules.Inside(pvpRoom, 5f, 8f) && !PvpRules.Inside(default, 0f, 0f));

            // ---- spectating (the owner, 2026-10-18)
            Check("spectate: only a teammate who is still free can be watched (not oneself, not a pinned or a busted one)", SpectateRules.Watchable(false, CustodyState.Free) && !SpectateRules.Watchable(true, CustodyState.Free) && !SpectateRules.Watchable(false, CustodyState.Pinned) && !SpectateRules.Watchable(false, CustodyState.Busted));
            Check("spectate: a busted player with a free teammate spectates; a free or pinned one does not; with nobody left to watch nobody does", SpectateRules.Spectating(CustodyState.Busted, 1) && !SpectateRules.Spectating(CustodyState.Free, 2) && !SpectateRules.Spectating(CustodyState.Pinned, 2) && !SpectateRules.Spectating(CustodyState.Busted, 0));
            Check("spectate: D / the left button step forward, A / the right button step back, round a ring of players", SpectateRules.Step(0, 3, 1) == 1 && SpectateRules.Step(2, 3, 1) == 0 && SpectateRules.Step(0, 3, -1) == 2 && SpectateRules.Step(1, 3, -1) == 0 && SpectateRules.Step(0, 0, 1) == 0 && SpectateRules.Step(0, 1, 1) == 0);

            // ---- a donut break
            Check("donut: an unarmed patrolling guard has a small chance; an armed guard, the boss and a guard on a post never stop for one",
                GuardRules.DonutBreak(0f, false, false, false) && !GuardRules.DonutBreak(0.5f, false, false, false) && GuardRules.DonutBreak(0.0149f, false, false, false) && !GuardRules.DonutBreak(0.0151f, false, false, false)
                && !GuardRules.DonutBreak(0f, true, false, false) && !GuardRules.DonutBreak(0f, false, true, false) && !GuardRules.DonutBreak(0f, false, false, true));
            Check("donut: a break lasts a few seconds (long enough to be seen, short enough not to be a nap), and the munch is a real clip", GuardRules.DonutSeconds >= 4f && GuardRules.DonutSeconds <= 10f && MunchSoundOk());
        }
    }
}
