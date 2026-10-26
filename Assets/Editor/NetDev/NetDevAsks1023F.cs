using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of batch D, T12, of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63; offline, on the real floors): the body of an eliminated guard is taken hold of with the grab key, trails behind the player on the floor plan, does not sink away while
    /// it is dragged and is let go with the key or when the player gets too far from it. The key itself cannot be pressed in a test: <c>PlayerGrab.TryGrabBody</c> is what the key calls. Run: <c>qa_regress_bd.ps1 -Methods Asks1023F</c>; the log ends with <c>[Asks1023F] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int a23fFails;

        void A23F(string name, bool ok, string detail = "")
        {
            if (!ok) a23fFails++;
            Log($"[Asks1023F] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator Asks1023FScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return LoadLevel(hm, 2);
            yield return new WaitForSeconds(1.0f);
            var player = WobblyPlayerRegistry.All[0];
            var body0 = player.GetComponent<Rigidbody>();
            var pg = player.GetComponent<PlayerGrab>();
            GuardDrops.TestRoll = 0;                                           // (no bullets lie about)
            GuardCorpse.WaitOverride = 4f;                                     // (a body lies 4 s, not 15)
            var guards = GuardAI.All.Where(g => g && !g.IsPuppet && !g.IsBoss && !g.Dead && g.name != "Guard_Outpost").ToList();
            A23F("(test) floor 2 has guards to try things on", guards.Count >= 3, $"({guards.Count})");
            if (guards.Count < 3) { Quit(1); yield break; }

            var victim = guards[0];
            A23F("T12: a guard who is alive has no body to drag", DraggableBody.Nearest(player.transform, 100f) == null);
            int n = 0; while (!victim.Dead && n < 20) { victim.BulletHitHost(100f, false, null); n++; }
            yield return null; yield return null;
            var body = victim.GetComponent<DraggableBody>();
            A23F("T12: a guard who is down for good has a body that can be dragged", victim.Dead && body != null && !body.IsDragged);
            if (body == null) { Quit(1); yield break; }
            var bodyAt = body.transform.position;
            bool open = OpenSpot(bodyAt + Vector3.up * 0.9f, 5f, out var far, true);
            A23F("(test) an open spot 5 m from the body", open);
            if (!open) { Quit(1); yield break; }

            // far from it nothing can be taken hold of; next to it the grab key takes it
            player.TeleportTo(far + Vector3.up * 1.1f, Quaternion.LookRotation((far - bodyAt).normalized));
            body0.linearVelocity = Vector3.zero;
            yield return null;
            A23F("T12: five metres away the grab key finds nothing to drag", !pg.TryGrabBody() && pg.Dragging == null);
            var dir = new Vector3(far.x - bodyAt.x, 0f, far.z - bodyAt.z).normalized;
            var near = bodyAt + dir * 1.0f;
            player.TeleportTo(new Vector3(near.x, far.y + 1.1f, near.z), Quaternion.LookRotation(dir));
            body0.linearVelocity = Vector3.zero;
            yield return null;
            A23F("T12: a metre from it the grab key takes hold of the body", pg.TryGrabBody() && pg.Dragging == body && body.IsDragged && body.Dragger == player);
            A23F("T12: the player drags slower while he holds a body", Mathf.Approximately(player.DragFactor, DraggableBodyRules.DraggerSpeedShare), $"({player.DragFactor})");

            // he walks away facing away from it: the body trails behind him, on the floor plan
            float t0 = Time.time;
            for (int i = 1; i <= 25; i++)
            {
                var p = Vector3.Lerp(near, far, i / 25f);
                player.TeleportTo(new Vector3(p.x, player.transform.position.y, p.z), Quaternion.LookRotation(dir));
                body0.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(2.5f);
            var gap = body.transform.position - player.transform.position; gap.y = 0f;
            bool onPlan = NavMesh.SamplePosition(body.transform.position, out var hit, 1.5f, NavMesh.AllAreas);
            float behind = Vector3.Dot(gap.normalized, dir);          // (a player faces where the camera looks, not where he walks: the body follows his PATH, so behind = against the way he WALKED)
            A23F("T12: the body followed him along his path: it trails a little more than a metre behind, on the floor plan", gap.magnitude > 0.6f && gap.magnitude < 2.2f && behind < -0.5f && onPlan, $"(gap {gap.magnitude:0.00} m, along his walk {behind:0.00}, on the plan {onPlan})");
            A23F("T12: the body is where the player took it, not where it lay", Vector3.Distance(body.transform.position, bodyAt) > 2.5f);

            // it does not sink away under his hands: a body lies 4 s and sinks in 1 s more, and it has been dragged for longer than that
            A23F("T12: the body is still there after longer than a body lies (it waits while it is dragged)", body != null && victim != null && victim.Dead && Time.time - t0 > 5.2f, $"(dragged for {Time.time - t0:0.0} s)");

            // let go with the key: it stays, the player is as fast as before, and then it sinks away after its time
            pg.ApplyDropBody();
            yield return null;
            var dropAt = body.transform.position;
            A23F("T12: let go, the body stays where it was left and the player walks at his own speed again", !body.IsDragged && pg.Dragging == null && Mathf.Approximately(player.DragFactor, 1f));
            yield return new WaitForSeconds(5.5f);
            A23F("T12: let go, the body goes the way every body goes (it lay 4 s)", body == null || !body.gameObject.activeInHierarchy || victim == null);

            // a player who gets too far from the body lets go of it
            var second = guards.Skip(1).FirstOrDefault(g => g && !g.Dead);
            if (second != null)
            {
                n = 0; while (!second.Dead && n < 20) { second.BulletHitHost(100f, false, null); n++; }
                yield return null; yield return null;
                var body2 = second.GetComponent<DraggableBody>();
                if (body2 != null)
                {
                    GuardCorpse.WaitOverride = 30f;
                    var at2 = body2.transform.position;
                    player.TeleportTo(at2 + new Vector3(0.8f, 1.1f, 0f), Quaternion.identity);
                    body0.linearVelocity = Vector3.zero;
                    yield return null;
                    bool took = pg.TryGrabBody();
                    A23F("(test) a second body is taken hold of", took && body2.IsDragged);
                    player.TeleportTo(at2 + new Vector3(0f, 1.1f, 9f), Quaternion.identity);               // (a lift to somewhere far: teleported, pinned, the floor changed)
                    body0.linearVelocity = Vector3.zero;
                    yield return new WaitForSeconds(0.5f);
                    A23F("T12: a player who is more than 4.5 m from the body lets go of it", !body2.IsDragged && pg.Dragging == null && Mathf.Approximately(player.DragFactor, 1f), $"(dragged {body2.IsDragged})");
                }
            }

            // ---- T12, the other half: the body of a BUSTED teammate (a copy of the player stands for him)
            {
                GuardCorpse.WaitOverride = 30f;
                bool gotSpotA = false; Vector3 spotA = default;
                foreach (float reachA in new[] { 8f, 7f, 6f, 5.5f }) { if (OpenSpot(player.transform.position + Vector3.up * 0.9f, reachA, out spotA, true)) { gotSpotA = true; break; } }
                if (gotSpotA)
                {
                    var mate = Object.Instantiate(player.gameObject);
                    mate.name = "Teammate_test";
                    var mw = mate.GetComponent<WobblyPlayer>(); var mrb = mate.GetComponent<Rigidbody>();
                    var dir2 = new Vector3(spotA.x - player.transform.position.x, 0f, spotA.z - player.transform.position.z).normalized;
                    player.TeleportTo(spotA + Vector3.up * 1.1f, Quaternion.LookRotation(dir2));
                    body0.linearVelocity = Vector3.zero;
                    mw.TeleportTo(spotA + dir2 * 1.0f + Vector3.up * 1.1f, Quaternion.identity);
                    mrb.linearVelocity = Vector3.zero;
                    yield return new WaitForSeconds(0.3f);
                    A23F("T12 (players): a teammate who is free has no body to drag", !pg.TryGrabBody() && !pg.IsDragging);
                    mw.ApplyCustody(CustodyState.Busted, 0f, 2);
                    yield return new WaitForSeconds(1.0f);
                    var mateAt = mate.transform.position;
                    A23F("T12 (players): the body of a busted teammate within reach is taken hold of with the grab key", pg.TryGrabBody() && pg.DraggingPlayer == mw && mw.DraggedBy == player, $"(dragging {(pg.DraggingPlayer ? pg.DraggingPlayer.name : "nobody")})");
                    A23F("T12 (players): the player drags slower while he holds the body", Mathf.Approximately(player.DragFactor, DraggableBodyRules.DraggerSpeedShare));
                    // he walks away: the body trails behind him
                    var farSpot = player.transform.position - dir2 * 5f;
                    for (int i = 1; i <= 25; i++)
                    {
                        var p = Vector3.Lerp(spotA, farSpot, i / 25f);
                        player.TeleportTo(new Vector3(p.x, player.transform.position.y, p.z), Quaternion.LookRotation(-dir2));
                        body0.linearVelocity = Vector3.zero;
                        yield return new WaitForSeconds(0.12f);
                    }
                    yield return new WaitForSeconds(2.5f);
                    var gap2 = mate.transform.position - player.transform.position; gap2.y = 0f;
                    A23F("T12 (players): the busted body followed him: it moved with him and lies within a few metres of him", Vector3.Distance(mate.transform.position, mateAt) > 2.5f && gap2.magnitude < 3.2f, $"(moved {Vector3.Distance(mate.transform.position, mateAt):0.0} m, now {gap2.magnitude:0.0} m from him)");
                    pg.ApplyDropBody();
                    yield return null;
                    A23F("T12 (players): let go, nobody drags it and the player is as fast as before", mw.DraggedBy == null && !pg.IsDragging && Mathf.Approximately(player.DragFactor, 1f));
                    // bought back while dragged: let go at once
                    player.TeleportTo(mate.transform.position + Vector3.right * 1.0f + Vector3.up * 1.1f, Quaternion.identity);
                    body0.linearVelocity = Vector3.zero;
                    yield return null;
                    bool again = pg.TryGrabBody();
                    mw.ApplyCustody(CustodyState.Free, 0f, 0);
                    yield return new WaitForSeconds(0.3f);
                    A23F("T12 (players): a teammate who is bought back while he is dragged is let go of", again && !pg.IsDragging && mw.DraggedBy == null && Mathf.Approximately(player.DragFactor, 1f), $"(took hold {again}, dragging {pg.IsDragging})");
                    Object.Destroy(mate);
                }
                else A23F("(test) an open spot for the teammate", false);
                GuardCorpse.WaitOverride = -1f;
            }

            GuardCorpse.WaitOverride = -1f; GuardDrops.TestRoll = -1;
            Log($"[Asks1023F] DONE fails={a23fFails}");
            Quit(a23fFails == 0 ? 0 : 1);
        }
    }
}
