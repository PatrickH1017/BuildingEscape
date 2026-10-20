using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Round 2 (2026-10-17): pictures and checks of what makes a floor look like a place - the carpets in the rooms and the tile on the path, the painted accent walls, the windows in the
    /// outside walls and the city beyond them. Run: <c>NetDevHarness.Looks</c> (graphics needed: qa_regress.ps1 -Methods Looks:gfx). Pictures: %TEMP%\be_shot_look{floor}_{n}_{what}.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int looksFails;

        void LookCheck(string name, bool ok, string detail = "")
        {
            if (!ok) looksFails++;
            Log($"[Looks] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>Which sides of the building a room touches (west, east, south, north), as outward unit vectors.</summary>
        static List<Vector3> ExteriorSides(Rc b, float width, float depth)
        {
            var sides = new List<Vector3>();
            if (b.x0 < 0.7f) sides.Add(Vector3.left);
            if (b.x1 > width - 0.7f) sides.Add(Vector3.right);
            if (b.z0 < 0.7f) sides.Add(Vector3.back);
            if (b.z1 > depth - 0.7f) sides.Add(Vector3.forward);
            return sides;
        }

        IEnumerator LooksScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            var eye = new GameObject("LooksEye").AddComponent<Camera>();
            eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = new Color(0.2f, 0.22f, 0.26f);
            eye.fieldOfView = 70f; eye.nearClipPlane = 0.05f; eye.farClipPlane = 1000f; eye.enabled = false;

            foreach (int floor in new[] { 1, 4 })
            {
                TowerManager.Instance.GoToLevel(floor);
                yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == floor);
                yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == floor, 90, $"floor {floor}");
                yield return new WaitForSeconds(1.5f);
                var info = TowerManager.Instance.CurrentInfo;
                float cs = gen.cellSize, width = info.cols * cs, depth = info.rows * cs;
                var layout = gen.CurrentLayout;
                string f = $"floor {floor}:";
                LookCheck($"{f} there is a plan to look at", layout != null && layout.rooms.Count > 4);
                if (layout == null) continue;

                // ---- the numbers
                LookCheck($"{f} tile on the path and carpet in the rooms", gen.TileArea > 100f && gen.CarpetArea > 300f, $"(tile {gen.TileArea:0} m2, carpet {gen.CarpetArea:0} m2, {gen.CarpetKinds} kinds, owner's carpets {gen.UsingOwnerCarpets})");
                LookCheck($"{f} one carpet colour for the whole floor", gen.CarpetTintCount == 1, $"({gen.CarpetTintCount} colour, the floor's own is {FloorGenerator.CarpetColourOf(floor)})");
                LookCheck($"{f} the rooms have accent walls", gen.AccentWalls >= 4, $"({gen.AccentWalls})");
                float overlap = 0f;
                foreach (var tl in gen.LaidTiles)
                    foreach (var cp in gen.LaidCarpets)
                    {
                        float ow = Mathf.Min(tl.x1, cp.x1) - Mathf.Max(tl.x0, cp.x0), od = Mathf.Min(tl.z1, cp.z1) - Mathf.Max(tl.z0, cp.z0);
                        if (ow > 0.001f && od > 0.001f) overlap += ow * od;
                    }
                LookCheck($"{f} no tile lies over a carpet and no carpet over tile", overlap < 0.01f && gen.LaidTiles.Count > 5 && gen.LaidCarpets.Count > 5, $"(overlap {overlap:0.000} m2 of {gen.LaidTiles.Count} tile and {gen.LaidCarpets.Count} carpet rectangles)");
                LookCheck($"{f} the outside walls have windows", gen.WindowCount >= 10, $"({gen.WindowCount})");
                LookCheck($"{f} some offices have a glass front", gen.GlassOffices >= 3 && gen.GlassPanes >= 6 && gen.GlassFronts.Count >= 3, $"({gen.GlassOffices} offices, {gen.GlassPanes} panes, {gen.CornerOffices} in a corner of the building)");
                int glassWalls = 0, glassSolid = 0;
                foreach (var t in gen.Current.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "Partition") continue;
                    var pr = t.GetComponent<MeshRenderer>();
                    var pc = t.GetComponent<BoxCollider>();
                    if (pr && !pr.enabled) { glassWalls++; if (pc && pc.enabled) glassSolid++; }
                }
                int suitesN = 0, suitesGlass = 0, serverN = 0;
                foreach (var rr in layout.rooms)
                {
                    if (rr.role == "server") serverN++;
                    if (rr.role != "suite") continue;
                    suitesN++;
                    foreach (var gb in gen.GlassRoomBounds) if (Mathf.Abs(gb.x0 - rr.bounds.x0) < 0.01f && Mathf.Abs(gb.z0 - rr.bounds.z0) < 0.01f) { suitesGlass++; break; }
                }
                LookCheck($"{f} every executive suite has a glass front, and the floor has one server room", suitesN >= 1 && suitesGlass == suitesN && serverN == 1, $"({suitesGlass} of {suitesN} suites with glass, {serverN} server room)");
                LookCheck($"{f} the doors of offices with a glass front are glass", gen.GlassDoors >= 2, $"({gen.GlassDoors} glass doors for {gen.GlassOffices} offices)");
                LookCheck($"{f} a wall with a glass front keeps its collider (nothing about walking or sight changes)", glassWalls == gen.GlassWallCount && glassSolid == glassWalls && glassWalls > 0, $"({glassWalls} walls, {glassSolid} with a collider)");
                LookCheck($"{f} the city is there", gen.CityTowers > 500 && gen.CityMeshes >= 6 && gen.CityReach > 600f && gen.Scenery != null, $"({gen.CityTowers} towers in {gen.CityMeshes} meshes, out to {gen.CityReach:0} m, built in {gen.SceneryMs} ms)");
                if (gen.Scenery != null)
                    LookCheck($"{f} the scenery has no collider (it costs the physics and the NavMesh nothing)", gen.Scenery.GetComponentsInChildren<Collider>(true).Length == 0);

                // ---- the outside walls: the renderers are off, the colliders stay (nobody leaves the building)
                int hidden = 0, solid = 0;
                foreach (var t in gen.Current.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "Wall") continue;
                    var mr = t.GetComponent<MeshRenderer>();
                    var bc = t.GetComponent<BoxCollider>();
                    if (mr && !mr.enabled) { hidden++; if (bc && bc.enabled) solid++; }
                }
                int expected = 2 * info.cols + 2 * info.rows;
                LookCheck($"{f} every outside wall segment ({expected}) is replaced by a window wall but keeps its collider", hidden == expected && solid == expected, $"(hidden {hidden}, with collider {solid})");

                // ---- pictures
                var rooms = layout.rooms.Where(r => r.kind == 0 && (r.theme == "office" || r.theme == "conference" || r.theme == "training" || r.theme == "lounge" || r.theme == "break")).ToList();
                var perimeter = rooms.Where(r => ExteriorSides(r.bounds, width, depth).Count > 0).OrderByDescending(r => ExteriorSides(r.bounds, width, depth).Count).ThenBy(r => r.bounds.x0 + r.bounds.z0).ToList();
                var inner = rooms.Where(r => ExteriorSides(r.bounds, width, depth).Count == 0).OrderBy(r => r.bounds.x0 * 3 + r.bounds.z0).ToList();
                var halls = layout.rooms.Where(r => r.kind == 2 && r.theme == "hall" && Mathf.Max(r.bounds.W, r.bounds.D) > 6f).OrderBy(r => r.bounds.Cx + r.bounds.Cz).ToList();
                var wanted = new List<(string what, Vector3 from, Vector3 to)>();
                int n = 0;
                foreach (var r in perimeter.Take(3))
                {
                    var sides = ExteriorSides(r.bounds, width, depth);
                    var dir = sides.Aggregate(Vector3.zero, (a, b) => a + b).normalized;
                    var c = new Vector3(r.bounds.Cx, 0f, r.bounds.Cz);
                    float half = Mathf.Max(Mathf.Abs(dir.x) * r.bounds.W, Mathf.Abs(dir.z) * r.bounds.D) * 0.5f;
                    wanted.Add(($"office{n++}", c - dir * Mathf.Max(0.5f, half - 0.7f) + Vector3.up * 1.65f, c + dir * (half + 4f) + Vector3.up * 1.5f));
                }
                foreach (var r in inner.Take(2))
                {
                    var c = new Vector3(r.bounds.Cx, 0f, r.bounds.Cz);
                    var from = new Vector3(r.bounds.x0 + 0.8f, 1.65f, r.bounds.z0 + 0.8f);
                    wanted.Add(($"inner{n++}", from, c + Vector3.up * 0.8f));
                }
                foreach (var r in halls.Take(2))
                {
                    bool alongX = r.bounds.W >= r.bounds.D;
                    var c = new Vector3(r.bounds.Cx, 0f, r.bounds.Cz);
                    var along = alongX ? Vector3.right : Vector3.forward;
                    float len = (alongX ? r.bounds.W : r.bounds.D) * 0.5f;
                    wanted.Add(($"path{n++}", c - along * (len - 0.4f) + Vector3.up * 1.65f, c + along * len + Vector3.up * 1.2f));
                }
                foreach (var gf in gen.GlassFronts.Take(2)) wanted.Add(($"glass{n++}", gf.centre + gf.outward * 2.8f + Vector3.up * 0.05f, gf.centre + Vector3.down * 0.1f));
                int shots = 0;
                foreach (var (what, from, to) in wanted)
                {
                    eye.transform.position = from;
                    eye.transform.LookAt(to);
                    yield return null;
                    ShotCam(eye, $"look{floor}_{shots++}_{what}");
                }
                // the outpost's computer, from where the player stands to use it, and the post-it on its monitor
                var computer = FindFirstObjectByType<ComputerProp>();
                LookCheck($"{f} the outpost has its computer", computer != null);
                if (computer != null)
                {
                    var front = computer.transform.rotation * Vector3.forward;
                    eye.transform.position = computer.transform.position + front * 0.85f + Vector3.up * 0.62f;
                    eye.transform.LookAt(computer.transform.position + Vector3.up * 0.26f + front * -0.1f);
                    yield return null;
                    ShotCam(eye, $"look{floor}_{shots++}_computer");
                }
                LookCheck($"{f} pictures taken", shots >= 5, $"({shots})");
            }
            Log($"[Looks] DONE fails={looksFails}");
            Quit(looksFails == 0 ? 0 : 1);
        }
    }
}
