using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The vending machines of the Loading Dock (the owner, 2026-10-22: "add them both to the main room of the loading dock and add the smaller black one to the break room ... In the main room I want them on the wall across from the vendor"),
    /// with the real props and the real physics, over several floors: the black and the red machine stand side by side on the main room's south wall, centred on the vendor, the second black one in the break room; each stands on the floor with its
    /// back to the wall, faces the room, has a collider that fits it, overlaps nothing, has room in front of it and can be walked up to from the elevator's door; the red one's small parts cast no shadow; the doorway of the break room is free;
    /// and the dock's scatter (forklift, drums, pallet blocks, box stacks, shelves) is still all there. Run: <c>qa_regress.ps1 -Methods Vending</c>; it ends with <c>[Vending] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        static Bounds VendingBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>The spot 0.6 m in front of a machine's front face, on the floor, in the middle of its width.</summary>
        static Vector3 VendingFront(Bounds b, DockVendingRules.Spot spot)
        {
            spot.Front(out int fx, out int fz);
            return new Vector3(fx > 0 ? b.max.x + 0.6f : fx < 0 ? b.min.x - 0.6f : b.center.x, 0f, fz > 0 ? b.max.z + 0.6f : fz < 0 ? b.min.z - 0.6f : b.center.z);
        }

        /// <summary>What is wrong with one machine (empty when all is well): its place, its size, its back to the wall, its way round, its collider, what overlaps it and the room in front of it.</summary>
        static string VendingProblems(string label, GameObject go, DockVendingRules.Spot spot, PropDef def, float wall)
        {
            var p = new List<string>();
            var b = VendingBounds(go);
            spot.Front(out int fx, out int fz);
            bool alongZ = fz != 0;                                                      // (facing north or south: its width runs along x)
            float width = alongZ ? b.size.x : b.size.z, depth = alongZ ? b.size.z : b.size.x;
            if (Mathf.Abs(b.min.y) > 0.03f) p.Add($"{label} does not stand on the floor (its underside is at y {b.min.y:0.00})");
            if (Mathf.Abs(b.size.y - def.H) > def.H * 0.15f) p.Add($"{label} is {b.size.y:0.00} m high, the prop table says {def.H:0.00}");
            if (Mathf.Abs(width - def.W) > def.W * 0.15f || Mathf.Abs(depth - def.D) > def.D * 0.15f) p.Add($"{label} measures {width:0.00} wide x {depth:0.00} deep, the prop table says {def.W:0.00} x {def.D:0.00}");
            float across = alongZ ? Mathf.Abs(b.center.x - spot.x) : Mathf.Abs(b.center.z - spot.z);
            float along = alongZ ? Mathf.Abs(b.center.z - spot.z) : Mathf.Abs(b.center.x - spot.x);
            if (across > 0.1f) p.Add($"{label} is {across:0.00} m off its place across the wall");
            if (along > 0.3f) p.Add($"{label} is {along:0.00} m off its place along its facing");
            float back = fz > 0 ? b.min.z : fz < 0 ? b.max.z : fx > 0 ? b.min.x : b.max.x;
            float wantBack = (fz > 0 || fx > 0) ? wall + DockVendingRules.WallGap : wall - DockVendingRules.WallGap;
            if (Mathf.Abs(back - wantBack) > 0.03f) p.Add($"{label}'s back is at {back:0.000}, it should be {wantBack:0.000} (the wall's face is at {wall:0.0})");
            float turn = Mathf.DeltaAngle(go.transform.eulerAngles.y, spot.yaw + def.FrontYaw);
            if (Mathf.Abs(turn) > 1f) p.Add($"{label} is turned {go.transform.eulerAngles.y:0} degrees, expected {Mathf.Repeat(spot.yaw + def.FrontYaw, 360f):0}");
            var bc = go.GetComponent<BoxCollider>();
            if (bc == null) p.Add($"{label} has no box collider");
            else if (Vector3.Distance(bc.bounds.center, b.center) > 0.15f || bc.bounds.size.y < def.H * 0.7f || bc.bounds.size.y > def.H * 1.15f) p.Add($"{label}'s collider (centre {bc.bounds.center.ToString("0.00")}, {bc.bounds.size.ToString("0.00")}) does not fit it (centre {b.center.ToString("0.00")}, {b.size.ToString("0.00")})");
            var hits = Physics.OverlapBox(b.center, b.extents * 0.9f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => !c.transform.IsChildOf(go.transform)).Select(c => c.transform.name + "@" + c.bounds.center.ToString("0.0")).Distinct().ToList();
            if (hits.Count > 0) p.Add($"{label} overlaps {string.Join(", ", hits)}");
            var front = VendingFront(b, spot);
            if (Physics.CheckCapsule(front + Vector3.up * 0.45f, front + Vector3.up * 1.4f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) p.Add($"there is no room for a person in front of {label} (at {front.ToString("0.0")})");
            return string.Join("; ", p);
        }

        IEnumerator VendingScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var gen = FindFirstObjectByType<FloorGenerator>();
            yield return new WaitForSeconds(0.5f);
            int fails = 0, runs = 10;
            var black = PropTable.Get("vending"); var red = PropTable.Get("vending_red");
            for (int i = 0; i < runs; i++)
            {
                int seed = 3100 + i * 7919;
                gen.Clear();
                gen.Generate(0, seed);
                yield return null; yield return null;
                Physics.SyncTransforms();
                var problems = new List<string>();
                var vendor = gen.Current ? gen.Current.Find("Vendor") : null;
                var holder = vendor ? vendor.Find("DockDressing") : null;
                if (vendor == null || holder == null) { fails++; Log($"[Vending] FAIL seed {seed}: no dock root"); continue; }
                var spots = gen.DockVendingSpots;
                var blacks = vendor.Cast<Transform>().Where(t => t.name == "vending").Select(t => t.gameObject).ToList();
                var reds = vendor.Cast<Transform>().Where(t => t.name == "vending_red").Select(t => t.gameObject).ToList();
                if (spots.Length != 3) problems.Add($"{spots.Length} places were laid out (3 expected: the pack's prefab or its catalog entry may be missing, see Employees Take Revenge > Setup Office Pack)");
                if (blacks.Count != 2) problems.Add($"{blacks.Count} black machines in the dock (2 expected)");
                if (reds.Count != 1) problems.Add($"{reds.Count} red machines in the dock (1 expected)");
                string detail = "";
                if (problems.Count == 0)
                {
                    var mainBlack = blacks.OrderBy(g => (VendingBounds(g).center - new Vector3(spots[0].x, 0f, spots[0].z)).sqrMagnitude).First();
                    var breakBlack = blacks.First(g => g != mainBlack);
                    var theRed = reds[0];
                    string a = VendingProblems("the black machine", mainBlack, spots[0], black, 0.2f);
                    string b = VendingProblems("the red machine", theRed, spots[1], red, 0.2f);
                    string c = VendingProblems("the break room's machine", breakBlack, spots[2], black, gen.BreakRoom.xMax);
                    foreach (var s in new[] { a, b, c }) if (s.Length > 0) problems.Add(s);

                    // the pair: side by side, west to east black then red, no overlap, centred on the vendor (whose trailer is backed into the north wall at his x)
                    var bb = VendingBounds(mainBlack); var rb = VendingBounds(theRed);
                    float tx = vendor.position.x + 5.8f;
                    if (bb.center.x >= rb.center.x) problems.Add("the black machine is not west of the red one");
                    if (bb.max.x > rb.min.x + 0.005f) problems.Add($"the two machines of the main room overlap ({bb.max.x:0.00} > {rb.min.x:0.00})");
                    float mid = (Mathf.Min(bb.min.x, rb.min.x) + Mathf.Max(bb.max.x, rb.max.x)) * 0.5f;
                    if (Mathf.Abs(mid - tx) > 0.35f) problems.Add($"the pair is centred on x {mid:0.00}, the vendor stands at {tx:0.00}");

                    // the break room's machine: inside the room, clear of the doorway, and the doorway itself walkable
                    var kb = VendingBounds(breakBlack); var br = gen.BreakRoom;
                    if (kb.min.x < br.xMin - 0.01f || kb.max.x > br.xMax + 0.01f || kb.min.z < br.yMin - 0.01f || kb.max.z > br.yMax + 0.01f) problems.Add($"the break room's machine ({kb.min.ToString("0.00")} to {kb.max.ToString("0.00")}) is not inside the room ({br})");
                    if (kb.max.z > gen.BreakRoomDoor.z - 0.8f + 0.02f) problems.Add($"the break room's machine reaches z {kb.max.z:0.00}, into the doorway (it starts at {gen.BreakRoomDoor.z - 0.8f:0.00})");
                    var door = new Vector3(gen.BreakRoomDoor.x, 0f, gen.BreakRoomDoor.z);
                    if (Physics.CheckCapsule(door + Vector3.up * 0.45f, door + Vector3.up * 1.4f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) problems.Add("the break room's doorway is blocked");

                    // the red machine is heavy (hundreds of parts): the small ones cast no shadow
                    var rs = theRed.GetComponentsInChildren<Renderer>(true);
                    int smallCasting = rs.Count(r => r.bounds.size.magnitude < 0.45f && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off);
                    int casting = rs.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off);
                    int verts = theRed.GetComponentsInChildren<MeshFilter>(true).Sum(mf => mf.sharedMesh ? mf.sharedMesh.vertexCount : 0);
                    if (smallCasting > 0) problems.Add($"{smallCasting} small parts of the red machine still cast a shadow");
                    detail = $" black ({bb.center.x:0.00}, {bb.center.z:0.00}) red ({rb.center.x:0.00}, {rb.center.z:0.00}) break room ({kb.center.x:0.00}, {kb.center.z:0.00}); the red machine: {rs.Length} parts, {verts:N0} vertices, {casting} cast a shadow";

                    // and they can be walked up to, from just outside the elevator's door (a person: a capsule 0.8 m across, 1.8 m high)
                    var free = FreeGrid(0.4f, 1.8f);
                    var reach = Flood(free, new Vector3(14.2f, 0f, 9.0f));
                    foreach (var (label, go, spot) in new[] { ("the black machine", mainBlack, spots[0]), ("the red machine", theRed, spots[1]), ("the break room's machine", breakBlack, spots[2]) })
                    {
                        var f = VendingFront(VendingBounds(go), spot);
                        int fi = Mathf.Clamp((int)(f.x / GridStep), 0, free.GetLength(0) - 1), fj = Mathf.Clamp((int)(f.z / GridStep), 0, free.GetLength(1) - 1);
                        if (!reach[fi, fj]) problems.Add($"{label} cannot be walked up to from the elevator's door (the spot in front of it, {f.ToString("0.0")}, is {(free[fi, fj] ? "cut off" : "blocked")})");
                    }
                }

                // the scatter is still all there (the machines and their room in front of them are kept clear of it): the same minimums as the DockSeeds scenario
                var roots = holder.Cast<Transform>().ToList();
                int Count(string n) => roots.Count(t => t.name == n);
                int lifts = Count("Forklift"), drums = Count("Drum"), blocks = Count("BoxBlock"), stacks = Count("BoxStack"), shelves = Count("Shelf");
                if (!(lifts >= 1 && drums >= 2 && blocks >= 6 && stacks >= 10 && shelves >= 4)) problems.Add($"the scatter came out thin: {lifts} forklifts, {drums} drums, {blocks} blocks, {stacks} stacks, {shelves} shelves (at least 1, 2, 6, 10 and 4 are needed)");

                bool ok = problems.Count == 0;
                if (!ok) fails++;
                Log($"[Vending] {(ok ? "PASS" : "FAIL")} seed {seed}:" + (ok ? detail : " " + string.Join(" | ", problems)));
            }
            Log($"[Vending] DONE fails={fails}");
            Quit(fails == 0 ? 0 : 1);
        }
    }
}
