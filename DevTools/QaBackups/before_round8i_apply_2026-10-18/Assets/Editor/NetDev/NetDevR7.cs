using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode checks (offline, Greybox scene, batch mode is fine) of the owner's list of the first two real playtests and of the task list of 2026-10-18, round 8: stamina, a guard's vision cone
    /// that only shows when the guard can be seen, body armour from floor 4, more guards on floor 3, the backup that comes faster, bodies that go away, a safe on every floor.
    /// Run: Unity -batchmode -executeMethod BuildingEscape.EditorTools.NetDevHarness.R7   (look for "[R7] DONE fails=0").
    /// </summary>
    public partial class NetDevRunner
    {
        int r7Fails;

        void R7Check(string name, bool ok, string detail = "")
        {
            if (!ok) r7Fails++;
            Log($"[R7] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>The patrolling guards on the floor (the outpost's own guard and the backup come on top).</summary>
        static int R7Guards() => GuardAI.All.Count(g => g && !g.IsPuppet);

        /// <summary>Turns the camera rig's aim towards a direction on the floor (the body follows the aim, so a teleport's rotation alone does not last); the rig's field is private.</summary>
        static void R7AimAt(CameraFollow rig, Vector3 dir)
        {
            dir.y = 0f;
            if (!rig || dir.sqrMagnitude < 1e-4f) return;
            var f = typeof(CameraFollow).GetField("aimYaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(rig, Quaternion.LookRotation(dir).eulerAngles.y);
        }

        /// <summary>Pictures of the basement's rooms and of the boxes in the main room (needs graphics: <c>ui.ps1 -Methods R7Look</c>; the files are %TEMP%\be_shot_r7_*.png).</summary>
        IEnumerator R7LookScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var gen = FindFirstObjectByType<FloorGenerator>();
            var rig = FindFirstObjectByType<CameraFollow>(); if (rig) rig.enabled = false;
            var cam = Camera.main;
            yield return new WaitForSeconds(1.0f);
            void Put(Vector3 pos, Vector3 at, float fov) { cam.transform.position = pos; cam.transform.rotation = Quaternion.LookRotation(at - pos, Vector3.up); cam.fieldOfView = fov; }
            Put(new Vector3(9.15f, 1.75f, 2.4f), new Vector3(3.5f, 1.0f, 2.4f), 78f); yield return null; yield return null; Shot("r7_break_in");
            Put(new Vector3(1.4f, 1.9f, 3.9f), new Vector3(5.8f, 1.7f, 0.5f), 70f); yield return null; yield return null; Shot("r7_break_tv");
            Put(new Vector3(9.15f, 1.8f, gen.PvpRoomDoor.z), new Vector3(3.5f, 0.6f, gen.PvpRoomDoor.z), 80f); yield return null; yield return null; Shot("r7_pvp_in");
            Put(new Vector3(15.5f, 3.0f, 1.6f), new Vector3(24f, 0.6f, 9.5f), 70f); yield return null; yield return null; Shot("r7_main");
            Put(new Vector3(30f, 3.0f, 16.5f), new Vector3(21f, 0.5f, 6f), 70f); yield return null; yield return null; Shot("r7_main_b");
            // the microphone on the outpost's desk: idle, and with the player at the computer speaking (the arm out to the button)
            yield return R5LoadFloor(1);
            yield return new WaitForSeconds(1.0f);
            rig = FindFirstObjectByType<CameraFollow>(); if (rig) rig.enabled = false;
            cam = Camera.main;
            var micL = MicProp.Active; var compL = FindFirstObjectByType<ComputerProp>();
            if (micL != null && compL != null)
            {
                var frL = compL.transform.rotation * Vector3.forward;
                Put(micL.transform.position + frL * 0.55f + Vector3.up * 0.40f, micL.transform.position + Vector3.up * 0.07f, 50f); yield return null; yield return null; Shot("r7_mic_idle");
                var meL = WobblyPlayerRegistry.All[0];
                var faceL = -frL;
                meL.TeleportTo(compL.transform.position + frL * 0.65f + Vector3.up * 0.2f, Quaternion.LookRotation(faceL));
                R7AimAt(rig, faceL);
                yield return new WaitForSeconds(0.8f);
                PaState.LiveBy = 0;
                yield return new WaitForSeconds(0.8f);
                var rightL = Vector3.Cross(Vector3.up, faceL);
                var midL = (meL.transform.position + micL.transform.position) * 0.5f;
                Put(meL.transform.position + frL * 1.8f + rightL * 1.0f + Vector3.up * 1.0f, midL + Vector3.up * 0.55f, 55f); yield return null; yield return null; Shot("r7_mic_live_back");
                Put(meL.transform.position + rightL * 1.5f + Vector3.up * 0.9f, midL + Vector3.up * 0.5f, 50f); yield return null; yield return null; Shot("r7_mic_live_side");
                PaState.LiveBy = -1;
            }
            // and on floor 2 (a chair in front of the desk keeps the speaker a step back): his model slides up to the desk
            yield return R5LoadFloor(2);
            yield return new WaitForSeconds(1.0f);
            rig = FindFirstObjectByType<CameraFollow>(); if (rig) rig.enabled = false;
            cam = Camera.main;
            micL = MicProp.Active; compL = FindFirstObjectByType<ComputerProp>();
            if (micL != null && compL != null)
            {
                var frF = compL.transform.rotation * Vector3.forward;
                var meF = WobblyPlayerRegistry.All[0];
                meF.TeleportTo(compL.transform.position + frF * 1.05f + Vector3.up * 0.2f, Quaternion.LookRotation(-frF));
                R7AimAt(rig, -frF);
                yield return new WaitForSeconds(0.8f);
                PaState.LiveBy = 0;
                yield return new WaitForSeconds(0.8f);
                var rightF = Vector3.Cross(Vector3.up, -frF);
                var midF = (meF.transform.position + micL.transform.position) * 0.5f;
                Put(meF.transform.position + frF * 1.6f + rightF * 1.2f + Vector3.up * 1.0f, midF + Vector3.up * 0.5f, 55f); yield return null; yield return null; Shot("r7_mic_live_far");
                Put(meF.transform.position + rightF * 1.8f + Vector3.up * 0.9f, midF + Vector3.up * 0.4f, 50f); yield return null; yield return null; Shot("r7_mic_live_far_side");
                PaState.LiveBy = -1;
            }
            Log("[R7] DONE look");
            Quit(0);
        }

        IEnumerator R7Scenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var cam = Camera.main;
            var rig = FindFirstObjectByType<CameraFollow>();

            // ================= the basement's rooms (the owner, 2026-10-17 / 18): the break room with the TV in it, the PVP room, boxes that stand square and rest on something
            {
                var gen0 = FindFirstObjectByType<FloorGenerator>();
                var tv0 = DockTv.Current;
                R7Check("the break room is in the south-west corner and the PVP room in the north-west, 9 x 4.6 m each", gen0 != null && gen0.BreakRoom.xMin < 0.5f && gen0.BreakRoom.yMax < 5f && gen0.BreakRoom.width > 8.5f && gen0.PvpRoomArea.yMin > 13f && gen0.PvpRoomArea.xMin < 0.5f && gen0.PvpRoomArea.width > 8.5f);
                R7Check("the TV hangs in the break room and nowhere else (the old TV of the main room is gone)", tv0 != null && FindObjectsByType<DockTv>(FindObjectsSortMode.None).Length == 1 && gen0.BreakRoom.Contains(new Vector2(tv0.Origin.x, tv0.Origin.z + 0.5f)));
                var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
                var inBreak = all.Where(t => t && t.parent != null && gen0.BreakRoom.Contains(new Vector2(t.position.x, t.position.z))).Select(t => t.name).ToList();
                R7Check("the break room holds a fridge, counters with a microwave, a desk, a few chairs and a door with a restroom sign", inBreak.Contains("fridge") && inBreak.Contains("counter_a2") && inBreak.Contains("microwave") && inBreak.Contains("desk_1") && inBreak.Count(n => n == "chair_cafe") >= 3 && inBreak.Contains("sign_wc") && inBreak.Contains("RestroomDoor"), $"({string.Join(",", inBreak.Distinct().Take(24))})");
                int pvpBoxes = all.Count(t => t && t.name == "box" && gen0.PvpRoomArea.Contains(new Vector2(t.position.x, t.position.z)));
                R7Check("the PVP room has a few boxes in it", pvpBoxes >= 8 && pvpBoxes <= 30, $"({pvpBoxes} boxes)");
                bool breakOpen = !Physics.CheckCapsule(gen0.BreakRoomDoor + Vector3.up * 0.5f, gen0.BreakRoomDoor + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                bool pvpOpen = !Physics.CheckCapsule(gen0.PvpRoomDoor + Vector3.up * 0.5f, gen0.PvpRoomDoor + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                R7Check("both doorways are open (a player fits through)", breakOpen && pvpOpen);
                var boxes = all.Where(t => t && t.name == "box" && t.parent != null).Select(t => t.GetComponentInChildren<Renderer>()).Where(r => r).Select(r => r.bounds).ToList();
                var pallets = all.Where(t => t && t.name == "pallet").Select(t => t.GetComponentInChildren<Renderer>()).Where(r => r).Select(r => r.bounds).ToList();
                var racks = all.Where(t => t && t.parent != null && (t.name == "kshelve" || t.name == "shelf" || t.name == "bookshelf" || t.name.StartsWith("cabinet"))).Select(t => t.GetComponentInChildren<Renderer>()).Where(r => r).Select(r => r.bounds).ToList();
                int floating = 0, crooked = 0;
                foreach (var b in boxes)
                {
                    bool rests = b.min.y < 0.03f;
                    foreach (var o in boxes) if (!rests && o.center != b.center && Mathf.Abs(o.max.y - b.min.y) < 0.04f && Mathf.Abs(o.center.x - b.center.x) < 0.3f && Mathf.Abs(o.center.z - b.center.z) < 0.3f) rests = true;
                    foreach (var o in pallets) if (!rests && Mathf.Abs(o.max.y - b.min.y) < 0.04f && Mathf.Abs(o.center.x - b.center.x) < 0.9f && Mathf.Abs(o.center.z - b.center.z) < 0.9f) rests = true;
                    foreach (var o in racks) if (!rests && Mathf.Abs(o.max.y - b.min.y) < 0.08f && Mathf.Abs(o.center.x - b.center.x) < 1.3f && Mathf.Abs(o.center.z - b.center.z) < 1.3f) rests = true;
                    if (!rests) floating++;
                }
                foreach (var t in all) if (t && t.name == "box" && t.parent != null && Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.y, Mathf.Round(t.eulerAngles.y / 90f) * 90f)) > 1.5f) crooked++;
                R7Check("the basement's boxes rest on the floor, on a pallet or on another box (none floats) and stand square to the room", boxes.Count >= 20 && floating == 0 && crooked == 0, $"({boxes.Count} boxes, {floating} floating, {crooked} crooked)");
            }

            // ================= the PVP room (the owner, 2026-10-17): health bars, hurting a rival, a brief knock-out, getting up again
            {
                var gen1 = FindFirstObjectByType<FloorGenerator>();
                var me1 = WobblyPlayerRegistry.All[0];
                var rivalGo = Object.Instantiate(me1.gameObject); rivalGo.name = "FakeRival";
                var fa = me1.GetComponent<PvpFighter>(); var fb = rivalGo.GetComponent<PvpFighter>();
                var wb = rivalGo.GetComponent<WobblyPlayer>();
                R7Check("every player has a PVP health meter, full", fa != null && fb != null && fa.Health >= PvpRules.MaxHealth - 0.01f && fb.Health >= PvpRules.MaxHealth - 0.01f);
                var mid = new Vector3((gen1.PvpRoomArea.xMin + gen1.PvpRoomArea.xMax) * 0.5f, 0.1f, (gen1.PvpRoomArea.yMin + gen1.PvpRoomArea.yMax) * 0.5f);
                me1.TeleportTo(mid + Vector3.left * 0.8f + Vector3.up, Quaternion.identity);
                wb.TeleportTo(mid + Vector3.right * 0.8f + Vector3.up, Quaternion.identity);
                yield return new WaitForSeconds(0.6f);
                R7Check("both are inside the PVP room", fa.Inside && fb.Inside);
                bool hit1 = PvpFighter.TryStrike(me1, wb, PvpRules.FistDamage);
                R7Check("a fist inside the room takes health off the rival and nothing off the one who threw it", hit1 && Mathf.Abs(fb.Health - (PvpRules.MaxHealth - PvpRules.FistDamage)) < 0.01f && fa.Health >= PvpRules.MaxHealth - 0.01f, $"(rival {fb.Health})");
                for (int i = 0; i < 2; i++) PvpFighter.TryStrike(me1, wb, PvpRules.BulletDamage);
                R7Check("two bullets more leave him on 24 health, still up", fb.Health > 20f && fb.Health < 28f && !fb.Down, $"({fb.Health})");
                PvpFighter.TryStrike(me1, wb, PvpRules.BulletDamage);
                yield return new WaitForSeconds(0.3f);
                R7Check("the third bullet knocks him out: no health, down, and his body stumbles", fb.Health <= 0.01f && fb.Down && wb.IsStumbling, $"(health {fb.Health}, down {fb.Down}, stumbling {wb.IsStumbling})");
                bool hitDown = PvpFighter.TryStrike(me1, wb, PvpRules.BulletDamage);
                R7Check("nothing can hurt him while he is down", !hitDown && fb.Health <= 0.01f);
                yield return new WaitForSeconds(PvpRules.KnockoutSeconds + 0.6f);
                R7Check("a few seconds later he is up again with full health", !fb.Down && fb.Health >= PvpRules.MaxHealth - 0.01f, $"(health {fb.Health}, down {fb.Down})");
                me1.TeleportTo(mid + Vector3.left * 0.8f + Vector3.up, Quaternion.identity);
                wb.TeleportTo(new Vector3(20f, 1.1f, 9f), Quaternion.identity);
                yield return new WaitForSeconds(0.5f);
                R7Check("a rival outside the room cannot be hurt, and nobody outside can hurt one inside", !PvpFighter.TryStrike(me1, wb, PvpRules.FistDamage) && !PvpFighter.TryStrike(wb, me1, PvpRules.FistDamage) && fb.Health >= PvpRules.MaxHealth - 0.01f && fa.Health >= PvpRules.MaxHealth - 0.01f);
                Object.Destroy(rivalGo);
                me1.TeleportTo(new Vector3(20f, 1.1f, 9f), Quaternion.identity);
                yield return null;
            }

            // ================= stamina (the owner, 2026-10-18: "There should be stamina with a stamina bar on the HUD")
            var st = player.GetComponent<Stamina>();
            R7Check("the player has a stamina meter and it is full", st != null && st.Meter.Fraction > 0.999f && !st.Meter.Exhausted);
            if (st != null)
            {
                player.testMove = new Vector3(0f, 0f, 1f); player.testSprint = true;       // (the same as holding W and Shift)
                yield return new WaitForSeconds(3f);
                float f3 = st.Meter.Fraction;
                R7Check("sprinting for three seconds takes about half of the bar, and the player sprints", f3 > 0.3f && f3 < 0.7f && player.IsSprinting && !st.Meter.Exhausted, $"({f3:0.00})");
                yield return new WaitForSeconds(5f);
                R7Check("sprinting on runs the bar dry: winded, and with the keys still held the player no longer sprints", st.Meter.Exhausted && !player.IsSprinting && st.Meter.Fraction < 0.3f, $"({st.Meter.Fraction:0.00}, exhausted {st.Meter.Exhausted}, sprinting {player.IsSprinting})");
                player.testSprint = false; player.testMove = null;
                float low = st.Meter.Fraction;
                yield return new WaitForSeconds(1.6f);
                R7Check("resting gives it back (after a breather), and a quarter of the bar ends the winded spell", st.Meter.Fraction > low + 0.05f, $"({low:0.00} -> {st.Meter.Fraction:0.00})");
                yield return WaitFor(() => !st.Meter.Exhausted, 6, "the winded spell to end");
                player.testMove = new Vector3(0f, 0f, 1f); player.testSprint = true;
                yield return new WaitForSeconds(0.4f);
                R7Check("and then the player can sprint again", player.IsSprinting);
                player.testSprint = false; player.testMove = null;
                st.Meter.Refill();
                st.DrainMultiplier = 0.4f;                                                   // (a buff: nothing gives one yet)
                player.testMove = new Vector3(0f, 0f, 1f); player.testSprint = true;
                yield return new WaitForSeconds(2f);
                float fd = st.Meter.Fraction;
                R7Check("with the drain turned down to 40 % two seconds of sprinting cost about 13 % of the bar", fd > 0.82f && fd < 0.93f, $"({fd:0.00})");
                player.testSprint = false; player.testMove = null;
                st.DrainMultiplier = 1f; st.Meter.Refill();
            }

            // ================= floor 1: no armour, the vision cone shows when the guard can be seen and only then, a body goes away
            yield return R5LoadFloor(1);
            player = WobblyPlayerRegistry.All[0]; rb = player.GetComponent<Rigidbody>(); cam = Camera.main; rig = FindFirstObjectByType<CameraFollow>();
            int g1 = R7Guards();
            R7Check("floor 1: the patrolling guards and the outpost's own (base 3 + 1, and one more)", g1 >= GuardRules.PatrolGuards(1, 3), $"({g1} guards, the rule says {GuardRules.PatrolGuards(1, 3)} patrolling)");
            R7Check("floor 1: nobody wears armour", GuardAI.All.All(g => !g || !g.Armoured));
            R7Check("floor 1: a safe", FloorSafe.Active != null);
            {
                GuardAI target = null; Vector3 spot = default;
                foreach (var g in GuardAI.All)
                {
                    if (!g || g.Dead || g.IsPuppet) continue;
                    if (OpenSpot(g.transform.position, 6f, out spot, false)) { target = g; break; }
                }
                R7Check("found a guard with a clear 6 m line to stand at", target != null);
                if (target != null)
                {
                    // the guard stands still for the test
                    var agent = target.GetComponent<NavMeshAgent>();
                    target.enabled = false;
                    if (agent && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
                    var vis = target.GetComponent<GuardVision>();
                    R7Check("the guard has a vision cone component", vis != null);
                    if (rig) rig.enabled = false;
                    player.TeleportTo(spot + Vector3.up * 1.1f, player.transform.rotation);
                    yield return new WaitForSeconds(0.6f);
                    var at = target.transform.position + Vector3.up * 0.9f;
                    Face(player, target.transform.position, rb);
                    AimCamera(cam, player, at);
                    yield return new WaitForSeconds(0.9f);
                    AimCamera(cam, player, at);
                    R7Check("in plain view of the camera the cone and the beam are shown", vis != null && vis.Shown > 0.95f, vis != null ? $"(shown {vis.Shown:0.00})" : "");

                    var mid = (target.transform.position + cam.transform.position) * 0.5f;
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "R7Wall";
                    var toGuard = target.transform.position - cam.transform.position; toGuard.y = 0f;
                    wall.transform.position = new Vector3(mid.x, 1.5f, mid.z);
                    wall.transform.rotation = Quaternion.LookRotation(toGuard.normalized);
                    wall.transform.localScale = new Vector3(8f, 4f, 0.2f);                 // (a partition right across the line, wider than the picture)
                    yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                    AimCamera(cam, player, at);
                    yield return new WaitForSeconds(0.9f);
                    R7Check("with a wall between the camera and the guard the cone and the beam are gone (nobody can tell he is round the corner)", vis != null && vis.Shown < 0.05f, vis != null ? $"(shown {vis.Shown:0.00})" : "");
                    Object.Destroy(wall);
                    yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                    yield return new WaitForSeconds(0.9f);
                    R7Check("the wall gone, he is seen again and the cone comes back", vis != null && vis.Shown > 0.95f, vis != null ? $"(shown {vis.Shown:0.00})" : "");
                    cam.transform.rotation = Quaternion.LookRotation(-(at - cam.transform.position).normalized, Vector3.up);          // (turned the other way: he is behind the camera)
                    yield return new WaitForSeconds(0.9f);
                    R7Check("with the camera turned away from him (he is behind it) the cone is not shown either", vis != null && vis.Shown < 0.05f, vis != null ? $"(shown {vis.Shown:0.00})" : "");
                    GuardVision.AlwaysShownForTests = true;
                    yield return new WaitForSeconds(0.9f);
                    R7Check("(test hook) the cone can be forced on for the tests that read it", vis != null && vis.Shown > 0.95f);
                    GuardVision.AlwaysShownForTests = false;

                    // ---- a body goes away: 15 s in the game, a moment in the test
                    target.enabled = true;                                                   // (his AI runs again: the body is handled from its LateUpdate)
                    GuardCorpse.WaitOverride = 1.5f;
                    int gone0 = GuardCorpse.Gone;
                    target.WeaponHitHost(8f, null, 10);
                    R7Check("a guard who took ten hits is down for good", target.Dead);
                    yield return new WaitForSeconds(0.8f);
                    R7Check("a moment later his body is still there", target != null);
                    yield return new WaitForSeconds(2.4f);
                    R7Check("...and after the wait it has sunk away and is gone (the owner: guards bodies should de-spawn)", target == null && GuardCorpse.Gone == gone0 + 1, $"(gone {GuardCorpse.Gone - gone0})");
                    GuardCorpse.WaitOverride = -1f;
                }
            }

            // ================= more guards on floor 3, body armour on floor 4, the backup faster on the higher floors
            yield return R5LoadFloor(2);
            int g2 = R7Guards();
            R7Check("floor 2: no armour, a safe", GuardAI.All.All(g => !g || !g.Armoured) && FloorSafe.Active != null);
            yield return R5LoadFloor(3);
            int g3 = R7Guards();
            R7Check("floor 3 has a decent step more guards than floor 2 (three more patrolling, the owner: wave 3 should have more guards than 2)", g3 >= g2 + 2 && g3 >= GuardRules.PatrolGuards(3, 3), $"(floor 2: {g2}, floor 3: {g3})");
            R7Check("floor 3: still no armour, a safe", GuardAI.All.All(g => !g || !g.Armoured) && FloorSafe.Active != null);
            yield return R5LoadFloor(4);
            var armoured = GuardAI.All.Where(g => g && !g.IsPuppet && !g.IsBoss).ToList();
            R7Check("floor 4: every guard wears body armour (a vest on his chest)", armoured.Count > 0 && armoured.All(g => g.Armoured && g.GetComponentsInChildren<Transform>(true).Any(t => t.name == "BodyArmour")), $"({armoured.Count(g => g.Armoured)} of {armoured.Count})");
            R7Check("floor 4: a safe", FloorSafe.Active != null);
            var victim = armoured.FirstOrDefault(g => !g.Dead);
            if (victim != null)
            {
                int bullet = RangedWeapon.Specs[0].guardHits;
                for (int i = 0; i < 4; i++) victim.WeaponHitHost(8f, null, bullet);
                R7Check("four pew bullets do not knock an armoured guard down for good (two hits each, eight of ten)", !victim.Dead && victim.Hits == 8, $"(hits {victim.Hits}, dead {victim.Dead})");
                victim.WeaponHitHost(8f, null, bullet);
                R7Check("the fifth one does (five bullets where an unarmoured guard takes three)", victim.Dead, $"(hits {victim.Hits})");
            }
            var other = armoured.FirstOrDefault(g => g && !g.Dead);
            if (other != null)
            {
                for (int i = 0; i < 9; i++) other.WeaponHitHost(1f, null, 1);
                bool alive9 = !other.Dead;
                other.WeaponHitHost(1f, null, 1);
                R7Check("a melee blow is still one hit against armour (ten blows knock him down)", alive9 && other.Dead, $"(hits {other.Hits})");
            }
            // the backup: the clock runs on the floor's interval
            var extra = armoured.FirstOrDefault(g => g && !g.Dead);
            hm.CallBackupAt(player.transform.position);
            yield return new WaitForSeconds(0.6f);
            float expect = CustodyRules.BackupInterval(4);
            R7Check("floor 4: the backup clock counts down from the floor's interval (shorter than on floor 1)", hm.BackupActive && hm.BackupIn >= Mathf.FloorToInt(expect) - 2 && hm.BackupIn <= Mathf.CeilToInt(expect) && expect < CustodyRules.BackupIntervalSeconds, $"(shown {hm.BackupIn} s, the rule says {expect:0.0} s)");
            hm.StandDown();

            // ================= the gun safe in floor 3's outpost: shut, a wall is the alarm, a clean stroke opens it (the owner, 2026-10-18)
            yield return R5LoadFloor(3);
            {
                var gs = GunSafeProp.Active;
                var ctl = OutpostController.Active;
                R7Check("floor 3: the pistol is in a gun safe in the outpost, shut, and the pistol is out of the world", gs != null && !gs.IsOpen && gs.Pew != null && !gs.Pew.gameObject.activeSelf && ctl != null && ctl.GunSafe == gs);
                if (gs != null && ctl != null)
                {
                    var me2 = WobblyPlayerRegistry.All[0];
                    me2.TeleportTo(gs.transform.position + gs.transform.forward * 1.0f + Vector3.up * 0.2f, Quaternion.identity);
                    yield return new WaitForSeconds(0.5f);
                    Vector2 ptr = default; bool btn = false;
                    GunSafeHackPanel.TestPointer = () => ptr; GunSafeHackPanel.TestButton = () => btn;
                    GunSafeHackPanel.Begin(gs, me2);
                    yield return null;
                    R7Check("E on the gun safe brings the hack screen up: the cursor is free and the player stands still", GunSafeHackPanel.Running && OutpostUi.Current == OutpostUi.Kind.GunSafe && OutpostUi.FreeCursor);
                    var path = GunSafeHackPanel.Instance.Session.Path;
                    ptr = path[0] + new Vector2(0f, 3f); btn = true; yield return null; yield return null;
                    R7Check("pressing away from the green circle does not start a stroke", !GunSafeHackPanel.Instance.Session.Drawing);
                    btn = false; yield return null;
                    ptr = path[0]; btn = true; yield return null; yield return null;
                    R7Check("pressing on the green circle starts one", GunSafeHackPanel.Instance.Session.Drawing);
                    ptr = Vector2.Lerp(path[0], path[1], 0.5f); yield return null; yield return null;
                    btn = false; yield return null; yield return null;
                    R7Check("letting go before the end gives the stroke up with no alarm", !GunSafeHackPanel.Instance.Session.Drawing && !ctl.AlarmOn && !gs.IsOpen);
                    ptr = path[0]; btn = true; yield return null; yield return null;
                    for (int i = 1; i <= 10; i++) { ptr = Vector2.Lerp(path[0], path[path.Length - 1], i / 10f); yield return null; }
                    yield return new WaitForSeconds(0.4f);
                    R7Check("a straight line across the corridor's walls hits one: the siren sounds, backup is called, the safe is jammed and stays shut", ctl.AlarmOn && hm.BackupActive && gs.Jammed && !gs.IsOpen && !GunSafeHackPanel.Running);
                    btn = false; yield return null;
                    ctl.HostStandDown();
                    yield return new WaitForSeconds(0.5f);
                    R7Check("the alarm panel calms it down and the safe can be tried again", !ctl.AlarmOn && !gs.Jammed);
                    GunSafeHackPanel.Begin(gs, me2);
                    yield return null;
                    path = GunSafeHackPanel.Instance.Session.Path;
                    ptr = path[0]; btn = true; yield return null; yield return null;
                    for (int i = 0; i + 1 < path.Length && GunSafeHackPanel.Running; i++)
                    {
                        float segLen = (path[i + 1] - path[i]).magnitude;
                        int steps = Mathf.CeilToInt(segLen / 0.3f);
                        for (int k = 1; k <= steps && GunSafeHackPanel.Running; k++) { ptr = Vector2.Lerp(path[i], path[i + 1], k / (float)steps); yield return null; }
                    }
                    yield return new WaitForSeconds(0.9f);
                    R7Check("a clean stroke along the corridor opens the safe, and the pistol is in the world", gs.IsOpen && gs.Pew.gameObject.activeSelf && !ctl.AlarmOn && (ctl.Bits & 8) != 0);
                    R7Check("the screen is gone and the player is free again", !GunSafeHackPanel.Running && !OutpostUi.Open);
                    GunSafeHackPanel.TestPointer = null; GunSafeHackPanel.TestButton = null;
                    hm.StandDown();
                }
            }

            // ================= the hallway loudspeakers: speakers in the corridors; with the PA live an officer within earshot of one that is on rushes to the outpost (the owner, 2026-10-18)
            yield return R5LoadFloor(2);
            {
                var spk = FindObjectsByType<LoudSpeaker>(FindObjectsSortMode.None);
                R7Check("the corridors have loudspeakers, as many as the plan says, all on, nobody speaking", spk.Length >= 6 && spk.Length == PaState.Speakers.Count && PaState.Mask == PaRules.MaskOf(spk.Length) && !PaState.Live, $"({spk.Length} speakers)");
                R7Check("they hang under the ceiling", spk.All(x => Mathf.Abs(x.transform.position.y - PaRules.Height) < 0.01f));
                var ctlp = OutpostController.Active;
                var guardsP = GuardAI.All.Where(g => g && !g.Dead && !g.IsPuppet && g.name != "Guard_Outpost").ToList();
                R7Check("floor 2 has the officers and the outpost to try it on", ctlp != null && guardsP.Count >= 2 && PaState.Speakers.Count > 0);
                if (ctlp != null && guardsP.Count >= 2 && PaState.Speakers.Count > 0)
                {
                    var spot = PaState.Speakers[0];
                    UnityEngine.AI.NavMesh.SamplePosition(new Vector3(spot.x + 2f, 0f, spot.y), out var near, 8f, UnityEngine.AI.NavMesh.AllAreas);
                    var a1 = guardsP[0].GetComponent<UnityEngine.AI.NavMeshAgent>(); var a2 = guardsP[1].GetComponent<UnityEngine.AI.NavMeshAgent>();
                    PaState.Mask = 0UL;                                                   // (every speaker switched off at the computer)
                    a1.Warp(near.position);
                    PaState.LiveBy = 0;                                                    // (somebody speaks)
                    yield return new WaitForSeconds(1.6f);
                    R7Check("with every speaker switched off nobody hears the PA: the officer next to one carries on with his round", !guardsP[0].PaAlerted && guardsP[0].State == GuardState.Patrol, $"(alerted {guardsP[0].PaAlerted}, {guardsP[0].State})");
                    PaState.Mask = PaRules.MaskOf(PaState.Speakers.Count);                // (switched on again)
                    yield return new WaitForSeconds(1.6f);
                    R7Check("with the speakers on, the officer within earshot of one rushes to the outpost's door", guardsP[0].PaAlerted && guardsP[0].State == GuardState.Investigate && Vector3.Distance(a1.destination, ctlp.KeypadPosition) < 3f, $"(alerted {guardsP[0].PaAlerted}, {guardsP[0].State}, goes to {a1.destination})");
                    PaState.LiveBy = -1;
                    guardsP[0].WeaponHitHost(1f, null, 10);                               // (a guard who is down is not called)
                    PaState.LiveBy = 0;
                    yield return new WaitForSeconds(1.2f);
                    R7Check("an officer who is down is not called", guardsP[0].Dead && !guardsP[0].PaAlerted);
                    PaState.LiveBy = -1;
                    PaState.Mask = PaRules.MaskOf(PaState.Speakers.Count);

                    // the microphone next to the computer: the ring lights while the PA is live, and the speaker's right arm goes out to its button
                    var mic = MicProp.Active;
                    var comp = FindFirstObjectByType<ComputerProp>();
                    R7Check("a microphone stands on the desk next to the computer, its ring dark while nobody speaks", mic != null && comp != null && Vector3.Distance(mic.transform.position, comp.transform.position) < 0.8f && !mic.LedOn);
                    if (mic != null && comp != null)
                    {
                        var me4 = WobblyPlayerRegistry.All[0];
                        var pv4 = me4.GetComponent<PlayerVisual>();
                        var hand = pv4 != null && pv4.Anim != null ? pv4.Anim.RightHand : null;
                        var front4 = comp.transform.rotation * Vector3.forward;                  // (the user's side: where the keyboard is)
                        me4.TeleportTo(comp.transform.position + front4 * 1.1f + Vector3.up * 0.2f, Quaternion.LookRotation(-front4));
                        R7AimAt(rig, -front4);
                        yield return new WaitForSeconds(0.8f);
                        R7Check("the player is in the outpost, at the computer, with a right hand", ctlp.IsInside(me4.transform.position) && hand != null);
                        if (hand != null)
                        {
                            float far0 = Vector3.Distance(hand.position, mic.ButtonWorld);
                            PaState.LiveBy = 0;
                            yield return new WaitForSeconds(1.0f);
                            float far1 = Vector3.Distance(hand.position, mic.ButtonWorld);
                            R7Check("speaking through the PA lights the microphone's ring, pushes its button in and puts the speaker's right hand towards it", mic.LedOn && mic.Pressed && far1 < far0 - 0.12f, $"(hand to button {far0:0.00} m -> {far1:0.00} m)");
                            PaState.LiveBy = -1;
                            yield return new WaitForSeconds(1.0f);
                            float far2 = Vector3.Distance(hand.position, mic.ButtonWorld);
                            R7Check("letting go of V lets the arm come back, the button up and the ring go dark", !mic.LedOn && !mic.Pressed && far2 > far1 + 0.08f, $"(hand to button {far1:0.00} m -> {far2:0.00} m)");

                            // at the desk itself the speaker bends over it and his hand is on the button
                            me4.TeleportTo(comp.transform.position + front4 * 0.65f + Vector3.up * 0.2f, Quaternion.LookRotation(-front4));
                            R7AimAt(rig, -front4);
                            yield return new WaitForSeconds(0.8f);
                            var rest4 = pv4.Anim.transform.localPosition;
                            PaState.LiveBy = 0;
                            yield return new WaitForSeconds(1.0f);
                            float close1 = Vector3.Distance(hand.position, mic.ButtonWorld);
                            Log($"[R7] near the desk: the player stands at {comp.transform.InverseTransformPoint(me4.transform.position)} of the computer and his model has slid {(pv4.Anim.transform.localPosition - rest4).magnitude:0.00} m towards it");
                            R7Check("standing at the desk the speaker bends over it (his model slides up to it if a chair keeps him back) and his right hand is on the microphone's button", close1 < 0.15f, $"(hand to button {close1:0.00} m)");
                            PaState.LiveBy = -1;
                            yield return new WaitForSeconds(0.8f);
                            R7Check("when he lets go his model is back on his body and the hand is off the button", (pv4.Anim.transform.localPosition - rest4).sqrMagnitude < 1e-6f && Vector3.Distance(hand.position, mic.ButtonWorld) > close1 + 0.05f, $"(model {(pv4.Anim.transform.localPosition - rest4).magnitude:0.000} m off, hand {Vector3.Distance(hand.position, mic.ButtonWorld):0.00} m from the button)");

                            // with his back to the desk he does not reach for it
                            R7AimAt(rig, front4);
                            me4.TeleportTo(me4.transform.position, Quaternion.LookRotation(front4));
                            yield return new WaitForSeconds(0.8f);
                            PaState.LiveBy = 0;
                            yield return new WaitForSeconds(1.0f);
                            R7Check("a speaker with his back to the desk does not reach for the button: his model stays on his body and the hand stays well away from it", (pv4.Anim.transform.localPosition - rest4).sqrMagnitude < 1e-6f && Vector3.Distance(hand.position, mic.ButtonWorld) > 0.6f, $"(model {(pv4.Anim.transform.localPosition - rest4).magnitude:0.000} m off, hand {Vector3.Distance(hand.position, mic.ButtonWorld):0.00} m from the button)");
                            PaState.LiveBy = -1;
                            yield return new WaitForSeconds(0.5f);
                        }
                    }
                }
            }

            // ================= the door terminal under the computer's Nav map: a room number on its own switches its doors the other way (the owner, 2026-10-18)
            {
                var compT = FindFirstObjectByType<ComputerProp>();
                var dirT = FloorDirectory.Current(2);
                var roomT = dirT != null ? dirT.rooms.FirstOrDefault(r => r.doors.Count > 0) : null;
                var plT = WobblyPlayerRegistry.All[0];
                var ocT = OutpostController.Active;
                R7Check("floor 2 has a computer, a room with doors and the outpost for the terminal to work on", compT != null && roomT != null && DoorManager.Active != null && ocT != null && ocT.Door != null);
                if (compT != null && roomT != null && DoorManager.Active != null && ocT != null && ocT.Door != null)
                {
                    var frT = compT.transform.rotation * Vector3.forward;
                    plT.TeleportTo(compT.transform.position + frT * 1.1f + Vector3.up * 0.2f, Quaternion.LookRotation(-frT));
                    R7AimAt(rig, -frT);
                    yield return new WaitForSeconds(0.6f);
                    compT.Use(plT);
                    yield return null;
                    R7Check("the computer's screen is up", ComputerScreen.Showing);
                    string labelT = roomT.label;
                    bool LockedT() => roomT.doors.All(i => DoorManager.Active.Get(i).ComputerLocked);
                    bool OpenT() => roomT.doors.All(i => !DoorManager.Active.Get(i).ComputerLocked);
                    if (!ComputerScreen.LoggedIn)
                    {
                        ComputerScreen.TypeTerminal(labelT);
                        R7Check("a line typed before the log-in does nothing", OpenT() && !ComputerScreen.LoggedIn);
                        var crT = compT.Credentials;
                        ComputerScreen.TryLogin(crT.user, crT.pass);
                    }
                    ComputerScreen.SelectTab(ComputerScreen.TabNav);
                    yield return null;
                    ComputerScreen.TypeTerminal(labelT);
                    R7Check("on the Nav page a room number on its own locks the room's doors", LockedT());
                    ComputerScreen.TypeTerminal(labelT);
                    R7Check("the same number again unlocks them", OpenT());
                    ComputerScreen.TypeTerminal("  " + labelT.ToLowerInvariant() + " ");
                    R7Check("capitals and spaces do not matter", LockedT());
                    ComputerScreen.TypeTerminal(labelT + " lock");
                    R7Check("LOCK after the number still works, and leaves locked doors locked", LockedT());
                    ComputerScreen.TypeTerminal("unlock " + labelT);
                    R7Check("UNLOCK before the number still works", OpenT());
                    ComputerScreen.TypeTerminal(labelT + " unlock");
                    R7Check("UNLOCK leaves doors that are open open", OpenT());
                    ComputerScreen.TypeTerminal("9999");
                    ComputerScreen.TypeTerminal(labelT + " foo");
                    var logT = ComputerScreen.TerminalLog;
                    R7Check("a room that does not exist and a word the terminal does not know are refused in the log, and nothing changes", logT != null && logT.Any(t => t == "NO SUCH ROOM") && logT.Any(t => t.StartsWith("UNKNOWN COMMAND")) && OpenT());
                    ComputerScreen.TypeTerminal(labelT);
                    ComputerScreen.TypeTerminal(labelT);
                    R7Check("the log says what happened", logT != null && logT.Any(t => t == "ROOM " + labelT.ToUpperInvariant() + " LOCKED") && logT.Any(t => t == "ROOM " + labelT.ToUpperInvariant() + " UNLOCKED"));
                    bool lockedBefore = ocT.Door.ComputerLocked;
                    ComputerScreen.TypeTerminal("SECURITY");
                    R7Check("SECURITY on its own locks this office's own door with the code", !lockedBefore && ocT.Door.ComputerLocked && !ocT.Unlocked);
                    ComputerScreen.TypeTerminal("outpost");
                    R7Check("...and typed again unlocks it", !ocT.Door.ComputerLocked && ocT.Unlocked);
                    ComputerScreen.SelectTab(ComputerScreen.TabHacked);
                    ComputerScreen.TypeTerminal(labelT);
                    R7Check("the Hacked tab is the same terminal", LockedT());
                    ComputerScreen.TypeTerminal(labelT);
                    R7Check("...and the number again unlocks there too", OpenT());
                    ComputerScreen.SelectTab(ComputerScreen.TabSafes);
                    ComputerScreen.TypeTerminal(labelT);
                    R7Check("the Safes tab has no terminal", OpenT());
                    ComputerScreen.SelectTab(ComputerScreen.TabMapTest);
                    R7Check("the test map is a tab of its own", ComputerScreen.Tab == ComputerScreen.TabMapTest);
                    ComputerScreen.TypeTerminal(labelT);
                    R7Check("the test map has the same terminal: a number locks", LockedT());
                    ComputerScreen.TypeTerminal(labelT);
                    R7Check("...and the number again unlocks there too", OpenT());
                    OutpostUi.Hide(OutpostUi.Kind.Computer);
                    yield return null;
                }
            }

            // ================= a donut break (floors 1-3: unarmed guards only)
            yield return R5LoadFloor(1);
            GuardAI.DonutRollOverride = 0f;                                                  // (every patrol stop is a donut break)
            GuardAI eater = null;
            float donutUntil = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < donutUntil && eater == null) { eater = GuardAI.All.FirstOrDefault(g => g && !g.IsPuppet && g.Eating); yield return new WaitForSeconds(0.5f); }
            R7Check("with the dice rigged an unarmed guard stops at a patrol stop to eat a donut", eater != null);
            if (eater != null)
            {
                var p0 = eater.transform.position;
                yield return new WaitForSeconds(0.6f);
                var donutT = eater.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Donut");
                R7Check("he holds a donut in his hand while he eats", donutT != null && donutT.gameObject.activeInHierarchy);
                yield return new WaitForSeconds(2.5f);
                R7Check("and he stays put while he does", eater.Eating && (eater.transform.position - p0).sqrMagnitude < 0.25f, $"(moved {(eater.transform.position - p0).magnitude:0.00} m)");
                GuardAI.DonutRollOverride = 1f;                                              // (no more breaks)
                yield return WaitFor(() => !eater.Eating, GuardRules.DonutSeconds + 4f, "the donut to be finished");
                yield return new WaitForSeconds(0.5f);
                R7Check("when it is eaten the donut is gone from his hand", donutT == null || !donutT.gameObject.activeInHierarchy);
            }
            GuardAI.DonutRollOverride = -1f;
            yield return R5LoadFloor(4);
            GuardAI.DonutRollOverride = 0f;
            yield return new WaitForSeconds(12f);
            R7Check("floor 4: the armed guards never stop for a donut, however the dice fall", GuardAI.All.Any(g => g && !g.IsPuppet && g.Armed) && GuardAI.All.Where(g => g && !g.IsPuppet && g.Armed).All(g => !g.Eating));
            GuardAI.DonutRollOverride = -1f;

            // ================= spectating: a busted player watches the teammates who are still going
            {
                var rig2 = FindFirstObjectByType<CameraFollow>();
                var me = WobblyPlayerRegistry.All[0];
                R7Check("free and alone: nobody is watched, the camera follows the player's own body", !Spectator.Active && rig2 != null && rig2.spectate == null);
                var mateA = Object.Instantiate(me.gameObject); mateA.name = "FakeTeammateA";
                var mateB = Object.Instantiate(me.gameObject); mateB.name = "FakeTeammateB";
                mateA.transform.position = me.transform.position + new Vector3(3f, 0f, 0f);
                mateB.transform.position = me.transform.position + new Vector3(-3f, 0f, 0f);
                yield return new WaitForSeconds(0.4f);
                R7Check("free, with teammates about: still nobody is watched (only a knocked-out player spectates)", !Spectator.Active && rig2.spectate == null);
                me.ApplyCustody(CustodyState.Busted, 0f, 1);
                yield return new WaitForSeconds(0.4f);
                var first = Spectator.Watching;
                R7Check("busted with free teammates: the camera watches one of them, and the camera's own target (the pack, the keys) stays the player's body", Spectator.Active && first != null && first.gameObject != me.gameObject && rig2.spectate == first.transform && rig2.target == me.transform);
                Spectator.TestStep = 1; yield return new WaitForSeconds(0.2f);
                var second = Spectator.Watching;
                R7Check("the next key moves the camera to the other teammate", second != null && second != first && rig2.spectate == second.transform);
                Spectator.TestStep = -1; yield return new WaitForSeconds(0.2f);
                R7Check("the key back returns to the first", Spectator.Watching == first);
                first.ApplyCustody(CustodyState.Busted, 0f, 1);
                yield return new WaitForSeconds(0.3f);
                R7Check("when the one watched is knocked out too, the camera moves to the one who is still free", Spectator.Watching == second && rig2.spectate == second.transform);
                second.ApplyCustody(CustodyState.Busted, 0f, 1);
                yield return new WaitForSeconds(0.3f);
                R7Check("when nobody is free any more the camera is back on the player's own body", !Spectator.Active && rig2.spectate == null);
                first.ApplyCustody(CustodyState.Free, 0f, 0);
                yield return new WaitForSeconds(0.3f);
                R7Check("a teammate who is free again can be watched again", Spectator.Active && Spectator.Watching == first);
                me.ApplyCustody(CustodyState.Free, 0f, 0);
                yield return new WaitForSeconds(0.3f);
                R7Check("and when the player is free again (a new floor) the camera comes back", !Spectator.Active && rig2.spectate == null);
                Object.Destroy(mateA); Object.Destroy(mateB);
                yield return null;
            }

            Log($"[R7] DONE fails={r7Fails}");
            Quit(r7Fails == 0 ? 0 : 1);
        }
    }
}
