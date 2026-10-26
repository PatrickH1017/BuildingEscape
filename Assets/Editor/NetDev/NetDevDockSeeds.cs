using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The docks' scatter over many seeds (round 12, 2026-10-20): the floor of the docks is mostly rooms and the hallway outside the break room is cleared, so what is scattered over the rest (forklifts, pallet blocks, drums, box stacks) fits in a few
    /// places only, and its count must not be left to luck. 24 floors are generated, each must hold a forklift, two drums, six pallet blocks (five stand in the PVP warehouse), ten box stacks and four shelves, nothing on the hallway's floor, and the way to
    /// the bay, the front of the truck and the room round the dummy clear. Run: <c>qa_regress.ps1 -Methods DockSeeds</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator DockSeedsScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var gen = FindFirstObjectByType<FloorGenerator>();
            yield return new WaitForSeconds(0.5f);
            int fails = 0, runs = 24;
            int minLifts = 99, minDrums = 99, minBlocks = 99, minStacks = 99, minShelves = 99;
            for (int i = 0; i < runs; i++)
            {
                int seed = 1000 + i * 7919;
                gen.Clear();
                gen.Generate(0, seed);
                yield return null; yield return null;
                Physics.SyncTransforms();
                var vendor = gen.Current ? gen.Current.Find("Vendor") : null;
                var holder = vendor ? vendor.Find("DockDressing") : null;
                if (holder == null) { fails++; Log($"[DockSeeds] FAIL seed {seed}: no dock root"); continue; }
                var roots = holder.Cast<Transform>().ToList();
                int Count(string n) => roots.Count(t => t.name == n);
                int lifts = Count("Forklift"), drums = Count("Drum"), blocks = Count("BoxBlock"), stacks = Count("BoxStack"), shelves = Count("Shelf");
                minLifts = Mathf.Min(minLifts, lifts); minDrums = Mathf.Min(minDrums, drums); minBlocks = Mathf.Min(minBlocks, blocks); minStacks = Mathf.Min(minStacks, stacks); minShelves = Mathf.Min(minShelves, shelves);
                var elev = gen.CellCentre(Vector2Int.zero);
                var c = vendor.position;
                float tx = c.x + 5.8f, zWall = c.z + gen.cellSize * 0.5f;
                string Blocking(Vector3 centre, Vector3 size) => string.Join(",", Physics.OverlapBox(centre, size * 0.5f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .Where(col => col.transform.IsChildOf(holder) && col.transform.name != "Bollard").Select(col => col.transform.name + "@" + col.bounds.center.ToString("0.0")));
                string way = Blocking(new Vector3((elev.x + 3f + tx + 3.4f) * 0.5f, 1.2f, c.z), new Vector3(tx + 3.4f - (elev.x + 3f), 2.2f, 6f));
                string truck = Blocking(new Vector3(tx, 1.2f, zWall - 4.25f), new Vector3(7.2f, 2.2f, 8.5f));
                string dummy = Blocking(gen.DummyAt + Vector3.up * 1.2f, new Vector3(4.4f, 2.2f, 4.4f));
                var hall = new List<string>();
                foreach (var pt in roots)
                {
                    if (pt.name != "BoxStack" && pt.name != "BoxBlock" && pt.name != "Forklift" && pt.name != "Drum") continue;
                    var prs = pt.GetComponentsInChildren<Renderer>();
                    if (prs.Length == 0) continue;
                    var pb = prs[0].bounds; foreach (var pr in prs) pb.Encapsulate(pr.bounds);
                    if (pb.min.y < 2.2f && pb.max.x > gen.BreakRoomDoor.x + 0.2f && pb.min.x < 17.8f && pb.max.z > 0.2f && pb.min.z < 7f) hall.Add(pt.name + "@" + pb.center.ToString("0.0"));
                }
                string wayIn = PvpWayIn(gen);                                                                    // (the owner, 2026-10-20: players must be able to get into the PVP room, whatever the seed)
                bool ok = lifts >= 1 && drums >= 2 && blocks >= (PvpBattlefieldRules.On ? 4 : 6) /* two cardboard blocks of the PVP room made way for the battlefield (T7) */ && stacks >= 10 && shelves >= 4 && way.Length == 0 && truck.Length == 0 && dummy.Length == 0 && hall.Count == 0 && wayIn.Length == 0;
                if (!ok) fails++;
                Log($"[DockSeeds] {(ok ? "PASS" : "FAIL")} seed {seed}: {lifts} forklifts, {drums} drums, {blocks} blocks, {stacks} stacks, {shelves} shelves" + (ok ? "" : $"; way [{way}] truck [{truck}] dummy [{dummy}] hallway [{string.Join(",", hall)}] PVP room [{wayIn}]"));
            }
            Log($"[DockSeeds] minimum over {runs} seeds: {minLifts} forklifts, {minDrums} drums, {minBlocks} blocks, {minStacks} stacks, {minShelves} shelves");
            Log($"[DockSeeds] DONE fails={fails}");
            Quit(fails == 0 ? 0 : 1);
        }

        const float GridStep = 0.2f;

        /// <summary>The docks' floor plan (x 0..36, z 0..24.4: the hall and the PVP room's north part) as a grid of 0.2 m cells: a cell is free when a capsule of this radius, from the floor up to <paramref name="height"/>, stands in it clear of everything.</summary>
        static bool[,] FreeGrid(float radius, float height)
        {
            int nx = (int)(36f / GridStep), nz = (int)(24.4f / GridStep);
            var free = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var p = new Vector3((i + 0.5f) * GridStep, 0f, (j + 0.5f) * GridStep);
                    free[i, j] = !Physics.CheckCapsule(p + Vector3.up * (0.05f + radius), p + Vector3.up * Mathf.Max(0.05f + radius, height - radius), radius, ~0, QueryTriggerInteraction.Ignore);
                }
            return free;
        }

        /// <summary>The cells that can be walked to from <paramref name="start"/> (steps between free cells that share an edge).</summary>
        static bool[,] Flood(bool[,] free, Vector3 start)
        {
            int nx = free.GetLength(0), nz = free.GetLength(1);
            var seen = new bool[nx, nz];
            int si = Mathf.Clamp((int)(start.x / GridStep), 0, nx - 1), sj = Mathf.Clamp((int)(start.z / GridStep), 0, nz - 1);
            if (!free[si, sj]) return seen;
            var queue = new Queue<(int i, int j)>();
            queue.Enqueue((si, sj)); seen[si, sj] = true;
            int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                var (ci, cj) = queue.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int ni = ci + di[k], nj = cj + dj[k];
                    if (ni < 0 || nj < 0 || ni >= nx || nj >= nz || seen[ni, nj] || !free[ni, nj]) continue;
                    seen[ni, nj] = true; queue.Enqueue((ni, nj));
                }
            }
            return seen;
        }

        /// <summary>
        /// Can players get into the PVP room (the owner, 2026-10-20: "the Forklift should not be blocking the PvP room doorway ... players need to be able to get into the PvP room")? From just outside the elevator's door a player (a capsule 0.8 m across, 1.8 m high)
        /// has to reach EVERY free spot of the PVP room (the hall, its north part and the pocket), and a forklift-sized body (1.3 m across, 2.3 m high) the doorway and the first metres inside it. Returns "" when all is well, else what is wrong.
        /// </summary>
        static string PvpWayIn(FloorGenerator gen)
        {
            var problems = new List<string>();
            var start = new Vector3(14.2f, 0f, 9.0f);                                      // just outside the elevator's east door, in the way through to the bay
            var room = gen.PvpRoomArea; var annex = gen.PvpRoomAnnex;
            Physics.SyncTransforms();
            var free = FreeGrid(0.4f, 1.8f);
            if (!free[(int)(start.x / GridStep), (int)(start.z / GridStep)]) problems.Add("the spot outside the elevator's door is blocked");
            var reach = Flood(free, start);
            int total = 0, cut = 0; string firstCut = "";
            for (int i = 0; i < free.GetLength(0); i++)
                for (int j = 0; j < free.GetLength(1); j++)
                {
                    var p = new Vector2((i + 0.5f) * GridStep, (j + 0.5f) * GridStep);
                    if (!free[i, j] || !(room.Contains(p) || annex.Contains(p))) continue;
                    total++;
                    if (!reach[i, j]) { cut++; if (firstCut.Length == 0) firstCut = $"({p.x:0.0}, {p.y:0.0})"; }
                }
            if (total < 300) problems.Add($"only {total} free cells in the PVP room");
            if (cut > 0) problems.Add($"{cut} of {total} free spots of the PVP room cannot be walked to from the elevator's door (the first at {firstCut})");
            var wide = FreeGrid(0.65f, 2.3f);
            var wideReach = Flood(wide, start);
            foreach (var (label, x) in new[] { ("the plaza outside the doorway", gen.PvpRoomDoor.x + 2.5f), ("the doorway", gen.PvpRoomDoor.x), ("just inside the doorway", gen.PvpRoomDoor.x - 1.8f) })
                if (!wideReach[(int)(x / GridStep), (int)(gen.PvpRoomDoor.z / GridStep)]) problems.Add($"a forklift cannot get to {label}");
            return string.Join("; ", problems);
        }
    }
}