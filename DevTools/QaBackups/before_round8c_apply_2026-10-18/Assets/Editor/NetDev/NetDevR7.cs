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

        IEnumerator R7Scenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var cam = Camera.main;
            var rig = FindFirstObjectByType<CameraFollow>();

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
                st.DrainMultiplier = 0.4f;                                                   // (an energy drink: less stamina drain)
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

            Log($"[R7] DONE fails={r7Fails}");
            Quit(r7Fails == 0 ? 0 : 1);
        }
    }
}
