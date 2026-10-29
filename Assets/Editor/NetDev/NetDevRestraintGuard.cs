using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The owner's restraint, drag and rescue clips (2026-10-28), scenario <c>RestraintGuard</c> (stages 2 and 3, one process): on floor 1 an officer pins the player, the pin runs out (the real <c>BustPlayer</c>), and the officer drags him by the head to the
    /// guards' outpost: the two halves of the drag clip, the pace, the struggle (the held button slows the officer, then he shoves), a hit officer lets go (the drop), a drag that ends at the outpost. Needs graphics: <c>qa_regress_bd.ps1 -Methods RestraintGuard:gfx</c>.
    /// Pictures: %TEMP%\be_shot_restraintg_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int rgFails;

        void RgCheck(string name, bool ok, string detail = "")
        {
            if (!ok) rgFails++;
            Log($"[RestraintGuard] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>A navigation-mesh point 8..25 m of walking from <paramref name="to"/> on a ring round it, or false.</summary>
        static bool RgStartPoint(Vector3 to, float ringFrom, float ringTo, out Vector3 start)
        {
            start = default;
            var path = new NavMeshPath();
            for (float r = ringFrom; r <= ringTo; r += 1.5f)
                for (int a = 0; a < 360; a += 20)
                {
                    var p = to + Quaternion.Euler(0f, a, 0f) * Vector3.forward * r;
                    if (!NavMesh.SamplePosition(p, out var hit, 1.2f, NavMesh.AllAreas)) continue;
                    if (!NavMesh.CalculatePath(hit.position, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    float len = 0f;
                    for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    if (len < ringFrom * 0.8f || len > 25f) continue;
                    start = hit.position;
                    return true;
                }
            return false;
        }

        IEnumerator RestraintGuardScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            hm.godMode = true;
            hm.freezeTimer = true;
            GunController.TestCursorLocked = true;
            Time.captureDeltaTime = 1f / 30f;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(1.0f);
            var player = WobblyPlayerRegistry.All[0];
            var rig = FindFirstObjectByType<CameraFollow>();
            var rv = player.GetComponent<RestraintVisual>();
            var gen = FindFirstObjectByType<FloorGenerator>();
            if (!rig || rv == null || !gen) { Log("[RestraintGuard] no camera rig, restraint visual or floor generator"); Quit(1); yield break; }
            rig.SetFirstPerson(false);
            hm.godMode = true;
            RgCheck("the floor has a guards' outpost to take a captive to", gen.TryGetOutpostRoom(out var room));
            var middle = new Vector3(room.Cx, 0f, room.Cz);
            var guards = GuardAI.All.Where(x => x && !x.Dead && !x.IsBoss && !x.IsBackup && x.name != "Guard_Outpost").ToList();
            RgCheck("(test) two plain officers on the floor", guards.Count >= 2, $"({guards.Count})");
            if (guards.Count < 2) { Quit(1); yield break; }
            var g = guards[0]; var g2 = guards[1];
            NavMesh.SamplePosition(middle, out var dest, 8f, NavMesh.AllAreas);
            RgCheck("(test) a start point of the officer 10-14 m of walking from the outpost", RgStartPoint(dest.position, 10f, 14f, out var start), $"(outpost at {middle})");
            RgStartPoint(dest.position, 10f, 14f, out start);
            var side = new GameObject("RestraintGuardCam").AddComponent<Camera>();
            side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = new Color(0.55f, 0.66f, 0.82f); side.enabled = false; side.nearClipPlane = 0.02f;
            void Shot(string name, Transform around, float away = 3.2f, float high = 1.4f, float fov = 45f)
            {
                var focus = around.position + Vector3.up * 0.4f;
                side.fieldOfView = fov;
                foreach (var ang in new[] { 90f, -90f, 135f, -135f, 45f, -45f, 180f, 0f })
                {
                    var pos = focus + Quaternion.Euler(0f, ang, 0f) * around.forward * away + Vector3.up * high;
                    if (Physics.Linecast(focus, pos, ~0, QueryTriggerInteraction.Ignore)) continue;
                    side.transform.position = pos; side.transform.LookAt(focus);
                    ShotCam(side, "restraintg_" + name);
                    return;
                }
                side.transform.position = focus + Vector3.up * 3f - around.forward * 0.5f; side.transform.LookAt(focus);
                ShotCam(side, "restraintg_" + name);
            }

            // ---------------- set the scene: the officer holds the player down, the pin runs out (the real BustPlayer)
            foreach (var other in guards) { var oa = other.GetComponent<NavMeshAgent>(); if (other != g && other != g2 && oa && oa.isOnNavMesh) oa.Warp(middle + new Vector3(0f, 0f, 40f)); }
            var ga = g.GetComponent<NavMeshAgent>();
            ga.Warp(start);
            g.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(dest.position - start, Vector3.up).normalized);
            player.TeleportTo(start + g.transform.forward * -0.6f + Vector3.up * 1.1f, g.transform.rotation);
            yield return new WaitForSeconds(0.4f);
            player.PinImmuneUntil = 0f;
            player.PinnedBy = g;
            player.ApplyCustody(CustodyState.Pinned, 12f, 0);
            g.BeginHold(player);
            yield return null;
            RgCheck("(test) the officer holds the player down", g.State == GuardState.Pinning && g.Holding == player);
            var gv = g.GetComponent<GuardVisual>();
            var grv = g.GetComponent<RestraintVisual>();
            RgCheck("(test) the officer's model can play a full-body clip", gv != null && gv.Anim != null && gv.Anim.CanPlayBody && grv != null);
            yield return new WaitForSeconds(1.0f);
            typeof(HeistManager).GetMethod("BustPlayer", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hm, new object[] { player });
            yield return null; yield return null;
            RgCheck("the pin ran out: he is busted and the officer who held him drags him by the head (state Dragging)", player.Custody == CustodyState.Busted && g.State == GuardState.Dragging && g.Dragging == player && player.DraggedByGuard == g && g.Holding == null, $"(custody {player.Custody}, officer {g.State})");
            RgCheck("the officer is taking him to the outpost, at the drag pace", Vector3.Distance(g.DragTarget, dest.position) < 0.5f && Mathf.Abs(ga.speed - RestraintRules.GuardSpeed(false)) < 0.01f, $"(target {g.DragTarget}, speed {ga.speed})");
            yield return new WaitForSeconds(0.5f);
            RgCheck("the two halves of the drag clip: the officer plays his, the captive his (the head in the officer's hands)", grv.Current == RestraintVisual.Look.GuardDragGuard && rv.Current == RestraintVisual.Look.GuardDragDragged && gv.Anim.BodyActive && rv.Anim.BodyActive, $"(officer {grv.Current}, captive {rv.Current})");

            // ---------------- the drag: pace, trail, the pair together
            var p0 = g.transform.position;
            float d0 = Vector3.Distance(p0, dest.position);
            var pair = new GameObject("RestraintPairProbe").AddComponent<RestraintPairProbe>();
            pair.a = grv; pair.b = rv;
            for (float tm = 0f; tm < 2.0f; tm += Time.deltaTime) yield return null;
            float worstGap = pair.worst;
            float walked = Vector3.Distance(p0, g.transform.position);
            RgCheck("dragging: the officer walks at about 1.2 m/s towards the outpost", walked > 1.8f && walked < 2.8f && Vector3.Distance(g.transform.position, dest.position) < d0 - 1.0f, $"({walked / 2f:0.00} m/s, {d0:0.0} m -> {Vector3.Distance(g.transform.position, dest.position):0.0} m)");
            RgCheck("dragging: the captive's model is put where the officer's is (the clips are two halves of one)", worstGap < 0.02f, $"({worstGap * 100f:0.0} cm)");
            RgCheck("dragging: every step of the drag is a scuff on the floor and a mark behind the body (the captive's machine plays them)", rv.Scuffs >= 2 && DragMarks.Count >= 2, $"(scuffs {rv.Scuffs}, marks {DragMarks.Count})");
            Shot("drag_side", g.transform, 3.4f, 1.3f);
            Shot("drag_high", g.transform, 1.2f, 3.2f, 55f);

            // ---------------- the struggle: the button held
            player.testStruggle = true;
            yield return new WaitForSeconds(0.4f);
            RgCheck("struggling (the button held): he struggles, the officer slows to half and the captive plays the struggle clip", player.StruggleHeld && player.StruggleActive && rv.Current == RestraintVisual.Look.GuardDragStruggleDragged && Mathf.Abs(ga.speed - RestraintRules.GuardSpeed(true)) < 0.01f && g.Struggle.Stamina < 1f, $"(held {player.StruggleHeld}, active {player.StruggleActive}, look {rv.Current}, speed {ga.speed}, stamina {g.Struggle.Stamina:0.00})");
            var p1 = g.transform.position;
            yield return new WaitForSeconds(1.0f);
            float slow = Vector3.Distance(p1, g.transform.position);
            RgCheck("struggling: the officer walks at about half pace (0.6 m/s)", slow > 0.3f && slow < 0.85f, $"({slow:0.00} m in 1 s)");
            RgCheck("struggling: the captive strains (a grunt) and nothing he does makes a noise the officers hear", rv.Strains >= 1);
            Shot("struggle_side", g.transform, 3.4f, 1.3f);
            for (float tm = 0f; tm < 3.0f && !g.DragShoving; tm += Time.deltaTime) yield return null;
            RgCheck("after 2.5 s of struggling the officer stops and shoves him: the stamina is full again and the struggle has ended", g.DragShoving && g.Shoves == 1 && g.Struggle.Stamina >= 0.99f && !player.StruggleActive, $"(shoving {g.DragShoving}, shoves {g.Shoves}, stamina {g.Struggle.Stamina:0.00}, active {player.StruggleActive})");
            yield return null; yield return null;
            RgCheck("the shove is heard on the captive's machine (a thud: the player's own shove count went up by one)", player.ShoveCount == 1 && rv.Shoves == 1, $"(count {player.ShoveCount}, heard {rv.Shoves})");
            var p2 = g.transform.position;
            yield return new WaitForSeconds(1.0f);
            RgCheck("shoving: the officer stands still for 1.5 s (he does not walk)", Vector3.Distance(p2, g.transform.position) < 0.1f && ga.isStopped);
            yield return new WaitForSeconds(0.8f);
            RgCheck("after the shove the drag goes on (the struggle button is still held: it starts again)", !g.DragShoving && !ga.isStopped && g.State == GuardState.Dragging, $"(state {g.State}, shoving {g.DragShoving}, stopped {ga.isStopped})");
            player.testStruggle = false;
            yield return new WaitForSeconds(0.4f);
            RgCheck("the button let go: he drags at the full pace again", !player.StruggleActive && Mathf.Abs(ga.speed - RestraintRules.GuardSpeed(false)) < 0.01f && rv.Current == RestraintVisual.Look.GuardDragDragged, $"(active {player.StruggleActive}, speed {ga.speed}, look {rv.Current})");

            // ---------------- a teammate shoots the officer: he lets go
            var at = g.transform.position;
            g.BulletHitHost(2.5f, false, null);
            yield return null; yield return null;
            RgCheck("the officer is hit: he lets go of the captive where he is (no longer Dragging, nobody dragged), he is not down", g.Dragging == null && player.DraggedByGuard == null && g.State != GuardState.Dragging && !g.Dead && g.Dropped == 1 && player.Custody == CustodyState.Busted, $"(state {g.State}, drops {g.Dropped})");
            RgCheck("the drop is shown: the officer plays Guard_Shot_DropHead and the captive its other half", player.DroppedBy == g && grv.Current == RestraintVisual.Look.GuardShotGuard && rv.Current == RestraintVisual.Look.GuardShotDragged, $"(officer {grv.Current}, captive {rv.Current})");
            yield return new WaitForSeconds(0.9f);
            Shot("drop_09", g.transform, 3.4f, 1.3f);
            yield return new WaitForSeconds(RestraintRules.DropSeconds);
            RgCheck("after the drop he lies hogtied where he was let go, the officer is back to his own animation", rv.Current == RestraintVisual.Look.Hogtied && grv.Current == RestraintVisual.Look.None && Vector3.Distance(player.transform.position, at) < 4f, $"(captive {rv.Current}, officer {grv.Current})");
            Shot("hogtied_after_drop", player.transform, 3f, 1.4f);

            // ---------------- a drag that ends at the outpost
            player.PinImmuneUntil = 0f;
            var g2a = g2.GetComponent<NavMeshAgent>();
            RgCheck("(test) a start point of the second officer 3-6 m from the outpost", RgStartPoint(dest.position, 3f, 6f, out var near));
            RgStartPoint(dest.position, 3f, 6f, out near);
            g2a.Warp(near);
            player.TeleportTo(near + Vector3.up * 1.1f, Quaternion.identity);
            player.PinnedBy = g2;
            player.ApplyCustody(CustodyState.Pinned, 12f, 0);
            g2.BeginHold(player);
            yield return null;
            hm.godMode = true;
            typeof(HeistManager).GetMethod("BustPlayer", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hm, new object[] { player });
            yield return null;
            RgCheck("(test) the second officer drags him", g2.State == GuardState.Dragging && g2.Dragging == player);
            for (float tm = 0f; tm < 12f && g2.State == GuardState.Dragging; tm += Time.deltaTime) yield return null;
            RgCheck("at the outpost the officer lets go: delivered, back to his round, the captive still busted and lying there", g2.Delivered == 1 && g2.State != GuardState.Dragging && g2.Dragging == null && player.DraggedByGuard == null && player.Custody == CustodyState.Busted, $"(delivered {g2.Delivered}, state {g2.State}, custody {player.Custody})");
            yield return new WaitForSeconds(0.6f);
            RgCheck("(after) the captive lies hogtied in the outpost", rv.Current == RestraintVisual.Look.Hogtied && Vector3.Distance(player.transform.position, dest.position) < 6f, $"(look {rv.Current}, {Vector3.Distance(player.transform.position, dest.position):0.0} m from the middle)");
            Shot("delivered", player.transform, 3f, 1.4f);

            GunController.TestCursorLocked = null;
            Time.captureDeltaTime = 0f;
            Log($"[RestraintGuard] DONE fails={rgFails}");
            Quit(rgFails == 0 ? 0 : 1);
        }
    }
}

namespace BuildingEscape.EditorTools
{
    /// <summary>The RestraintGuard scenario's eye on a dragger and the dragged body: the distance between their model roots, measured after every other late update has run (the dragged model is placed in one), so it is the picture of the frame.</summary>
    [UnityEngine.DefaultExecutionOrder(5000)]
    public class RestraintPairProbe : UnityEngine.MonoBehaviour
    {
        public RestraintVisual a, b;
        public float worst;

        void LateUpdate()
        {
            if (a && b && a.ModelRoot && b.ModelRoot && a.Current == RestraintVisual.Look.GuardDragGuard && (b.Current == RestraintVisual.Look.GuardDragDragged || b.Current == RestraintVisual.Look.GuardDragStruggleDragged))
                worst = UnityEngine.Mathf.Max(worst, (a.ModelRoot.position - b.ModelRoot.position).magnitude);
        }
    }
}
