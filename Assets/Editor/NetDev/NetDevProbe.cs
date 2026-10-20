using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    public partial class NetDevRunner
    {
        IEnumerator ProbeScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            foreach (int seed in new[] { 20240, 1, 7 })
            {
                tm.baseSeed = seed;
                for (int floor = 1; floor <= 4; floor++)
                {
                    tm.GoToLevel(floor);
                    yield return new WaitUntil(() => tm.Loading || hm.Floor == floor);
                    yield return WaitFor(() => !tm.Loading && hm.Floor == floor, 90, $"seed {seed} floor {floor}");
                    yield return new WaitForSeconds(0.6f);
                    var pieces = LootOnShow();
                    foreach (var g in GuardAI.All.Where(g => g && g.isActiveAndEnabled && !g.Dead))
                    {
                        NavMesh.SamplePosition(g.transform.position, out var hit, 4f, NavMesh.AllAreas);
                        int bad = 0;
                        foreach (var p in pieces) if (!WalkableNear(hit.position, p.transform.position)) bad++;
                        Log($"[Probe] seed {seed} floor {floor}: {g.name} at {g.transform.position} (navmesh {(hit.hit ? hit.position.ToString() : "NONE")}) state {g.State}: {bad} of {pieces.Count} pieces unreachable");
                        if (bad > pieces.Count / 2)
                        {
                            var lay = FindFirstObjectByType<FloorGenerator>().CurrentLayout;
                            string where(Vector3 pos) { var rm = lay?.rooms.FirstOrDefault(q => q.bounds.Contains(pos.x, pos.z)); return rm == null ? "no room" : $"{rm.theme} kind {rm.kind} role {rm.role} bounds ({rm.bounds.x0:0.0},{rm.bounds.z0:0.0})-({rm.bounds.x1:0.0},{rm.bounds.z1:0.0}) doors {rm.doors.Count}"; }
                            Log($"[Probe]    STUCK: he stands in: {where(g.transform.position)}");
                            var here = lay?.rooms.FirstOrDefault(q => q.bounds.Contains(g.transform.position.x, g.transform.position.z));
                            if (here != null)
                                foreach (var dr in here.doors)
                                {
                                    var dc = new Vector3(dr.Cx, 0.1f, dr.Cz);
                                    bool onMesh = NavMesh.SamplePosition(dc, out var dh, 1.5f, NavMesh.AllAreas);
                                    Log($"[Probe]    door rect ({dr.x0:0.00},{dr.z0:0.00})-({dr.x1:0.00},{dr.z1:0.00}) on the NavMesh: {onMesh} (moved {(onMesh ? Vector3.Distance(dh.position, dc) : -1f):0.00} m)");
                                    var zone = dr.Grow(1.0f);
                                    foreach (var pc in lay.pieces.Where(q => !q.mount && q.rc.Overlaps(zone, 0.01f)))
                                        Log($"[Probe]      near the door: {pc.key} rc ({pc.rc.x0:0.00},{pc.rc.z0:0.00})-({pc.rc.x1:0.00},{pc.rc.z1:0.00}) h {pc.h:0.00} solid {pc.solid} onTop {pc.onTop}");
                                    var pathA = new NavMeshPath(); NavMesh.CalculatePath(g.transform.position, dc, NavMesh.AllAreas, pathA);
                                    Log($"[Probe]      path from him to the doorway: {pathA.status}");
                                }
                            if (g.patrolPoints != null) foreach (var wp in g.patrolPoints) Log($"[Probe]    patrol point {wp.position}: {where(wp.position)}; reaches the outpost-guard-free floor: {WalkableNear(hit.position, wp.position)}");
                        }
                    }
                }
            }
            Log("[Probe] DONE");
            Quit(0);
        }
    }
}