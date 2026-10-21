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
                bool ok = lifts >= 1 && drums >= 2 && blocks >= 6 && stacks >= 10 && shelves >= 4 && way.Length == 0 && truck.Length == 0 && dummy.Length == 0 && hall.Count == 0;
                if (!ok) fails++;
                Log($"[DockSeeds] {(ok ? "PASS" : "FAIL")} seed {seed}: {lifts} forklifts, {drums} drums, {blocks} blocks, {stacks} stacks, {shelves} shelves" + (ok ? "" : $"; way [{way}] truck [{truck}] dummy [{dummy}] hallway [{string.Join(",", hall)}]"));
            }
            Log($"[DockSeeds] minimum over {runs} seeds: {minLifts} forklifts, {minDrums} drums, {minBlocks} blocks, {minStacks} stacks, {minShelves} shelves");
            Log($"[DockSeeds] DONE fails={fails}");
            Quit(fails == 0 ? 0 : 1);
        }
    }
}