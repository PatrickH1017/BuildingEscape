using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The owner's playtest of 2026-10-23: "Plants also should not spawn in the middle of rooms (along the walls are good) and floating items such as fire extinguishers should not be floating randomly". Builds floors and looks at every prop the planner placed:
    /// a plant standing on the floor must be near a wall (<see cref="PlantWallReach"/>), a prop of the wall-mounted kind must have a wall right behind it, and nothing meant for the floor may hang in the air. Run: <c>qa_regress_bd.ps1 -Methods PropScan</c>;
    /// the log lists the offenders ([PropScan] lines) and ends with <c>[PropScan] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        const float PlantWallReach = 1.1f;

        static bool IsFloorPlant(string key) => key.StartsWith("plant_") && key != "plant_desk" && key != "plant_tiny" || key == "cs_pot";

        static string nearestAny = ""; static float nearestAnyDistance;

        static bool PropSolid(Collider c, Transform self) => c && !c.isTrigger && c.attachedRigidbody == null && !c.transform.IsChildOf(self);

        /// <summary>The distance to the nearest wall in the eight directions at knee height (the plant's own box left out), or <paramref name="max"/> when there is none.</summary>
        static float WallDistance(Vector3 at, Transform self, float max)
        {
            float best = max;
            nearestAny = "nothing"; nearestAnyDistance = max;
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                foreach (var h in Physics.RaycastAll(new Vector3(at.x, 0.6f, at.z), dir, max, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!PropSolid(h.collider, self)) continue;
                    var n = h.collider.transform; bool wall = false;
                    if (h.distance < nearestAnyDistance && h.distance > 0.05f) { nearestAnyDistance = h.distance; nearestAny = n.parent != null ? n.parent.name + "/" + n.name : n.name; }
                    for (var t = n; t != null && !wall; t = t.parent) if (t.name.StartsWith("Wall") || t.name.StartsWith("Partition")) wall = true;
                    if (!wall) continue;
                    best = Mathf.Min(best, h.distance);
                }
            }
            return best;
        }

        IEnumerator PropScanScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            int fails = 0, plants = 0, midRoom = 0, mounts = 0, floatingMounts = 0, floorProps = 0, floatingFloor = 0;
            for (int floor = 1; floor <= 4; floor++)
            {
                yield return LoadLevel(hm, floor);
                yield return new WaitForSeconds(1.5f);
                Physics.SyncTransforms();
                var gen = FindFirstObjectByType<FloorGenerator>();
                var root = gen != null ? gen.Current : null;
                if (root == null) { Log($"[PropScan] FAIL floor {floor} has no root"); fails++; continue; }
                var offenders = new List<string>();
                foreach (var t in root.GetComponentsInChildren<Transform>())
                {
                    if (!PropTable.TryGet(t.name, out var def) || !t.gameObject.activeInHierarchy) continue;
                    var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    if (IsFloorPlant(def.key) && b.min.y < 0.3f)
                    {
                        plants++;
                        float d = WallDistance(new Vector3(b.center.x, 0f, b.center.z), t, 3f);
                        if (d > PlantWallReach) { midRoom++; offenders.Add($"plant '{def.key}' in the middle of a room at ({b.center.x:0.0}, {b.center.z:0.0}), {d:0.0} m from a wall (nearest solid: {nearestAny} at {nearestAnyDistance:0.0} m)"); }
                    }
                    else if (def.cat == PropCat.Mount)
                    {
                        mounts++;
                        var back = -t.forward; back.y = 0f;
                        bool held = false;
                        foreach (var h in Physics.RaycastAll(b.center, back.normalized, 0.8f, ~0, QueryTriggerInteraction.Ignore))
                            if (PropSolid(h.collider, t) && h.distance <= def.D * 0.5f + 0.25f) { held = true; break; }
                        if (!held) { floatingMounts++; offenders.Add($"wall prop '{def.key}' hangs in the air at ({b.center.x:0.0}, {b.center.y:0.0}, {b.center.z:0.0}) facing {t.forward.x:0.0}, {t.forward.z:0.0}"); }
                    }
                    else if (def.cat == PropCat.Furniture || def.cat == PropCat.Appliance || def.cat == PropCat.Chair || def.cat == PropCat.Deco && def.solid && IsFloorPlant(def.key) == false)
                    {
                        floorProps++;
                        if (b.min.y > 0.2f)
                        {
                            bool resting = false;
                            foreach (var h in Physics.RaycastAll(new Vector3(b.center.x, b.min.y + 0.05f, b.center.z), Vector3.down, 0.4f, ~0, QueryTriggerInteraction.Ignore))
                                if (PropSolid(h.collider, t)) { resting = true; break; }
                            if (!resting) { floatingFloor++; offenders.Add($"'{def.key}' floats {b.min.y:0.00} m above the floor at ({b.center.x:0.0}, {b.center.z:0.0})"); }
                        }
                    }
                }
                Log($"[PropScan] floor {floor}: {offenders.Count} offender(s)");
                foreach (var o in offenders.Take(25)) Log("[PropScan]    " + o);
            }
            fails = midRoom + floatingMounts + floatingFloor;
            Log($"[PropScan] plants {plants} (in the middle of a room: {midRoom}); wall props {mounts} (hanging in the air: {floatingMounts}); floor props {floorProps} (floating: {floatingFloor})");
            Log($"[PropScan] DONE fails={fails}");
            Quit(fails == 0 ? 0 : 1);
        }
    }
}
