using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Q/A of the demo's game loop (floors 1 to 4) beyond the rules: for several run seeds every floor is built the way the game builds it and
    /// looked at the way a player would: is there loot enough for the quota (all of it, and what ONE player can carry alone), can every piece be
    /// walked to, are the guards on the floor and armed as they should be (none on floors 1-3, all from floor 4), is the pistol in floor 4's
    /// outpost, are the guards' elevator and the two stairwells there, does the player start sound. Then, on the offline run's own seed, floors 1
    /// to 4 are played through with the REAL banking (loot set down in the elevator car until the elevator's own AutoBank meets the quota) and
    /// left for the docks, where the surplus becomes cash.
    /// Run: Unity -batchmode -nographics -executeMethod BuildingEscape.EditorTools.NetDevHarness.LootQa   (look for "[LootQa] DONE fails=0").
    /// </summary>
    public partial class NetDevRunner
    {
        int lqFails;

        void LqCheck(string name, bool ok, string detail = "")
        {
            if (!ok) lqFails++;
            Log($"[LootQa] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>Every loose thing worth money that a player could carry off, lying on the floor on show.</summary>
        static List<Pickup> LootOnShow() =>
            Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => p && p.isActiveAndEnabled && p.Value > 0 && !p.IsHeld).ToList();

        static bool OnNavMesh(Vector3 p, float reach = 3.5f) => NavMesh.SamplePosition(p, out _, reach, NavMesh.AllAreas);

        /// <summary>
        /// Can a person standing on the floor walk to a spot within reach of <paramref name="item"/> (2 m)? A thing lying on a desk is matched
        /// to the floor around it (the desk top is a little NavMesh island of its own); floor-level NavMesh points on rings of 0, 0.8, 1.4 and 2 m
        /// round it are tried.
        /// </summary>
        static bool WalkableNear(Vector3 from, Vector3 item)
        {
            var path = new NavMeshPath();
            foreach (float r in new[] { 0f, 0.8f, 1.4f, 2.0f })        // (a player reaches 2.2 m)
                for (int i = 0; i < (r == 0f ? 1 : 12); i++)
                {
                    var q = new Vector3(item.x + Mathf.Cos(i * Mathf.PI / 6f) * r, 0.05f, item.z + Mathf.Sin(i * Mathf.PI / 6f) * r);
                    if (!NavMesh.SamplePosition(q, out var h, 0.4f, NavMesh.AllAreas) || h.position.y > 0.25f) continue;      // (floor level only)
                    if (NavMesh.CalculatePath(from, h.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) return true;
                }
            return false;
        }

        IEnumerator LootQaScenario()
        {
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var gen = FindFirstObjectByType<FloorGenerator>();
            var player = WobblyPlayerRegistry.All[0];
            int ownSeed = tm.baseSeed;

            // ---- part 1: what each floor is made of, for several seeds
            int[] seeds = { ownSeed, 1, 7, 99, 1234, 55555, 424242, 8675309 };
            int floorsLooked = 0;
            foreach (int seed in seeds)
            {
                tm.baseSeed = seed;
                for (int floor = 1; floor <= 4; floor++)
                {
                    tm.GoToLevel(floor);
                    yield return new WaitUntil(() => tm.Loading || hm.Floor == floor);
                    yield return WaitFor(() => !tm.Loading && hm.Floor == floor, 90, $"seed {seed} floor {floor}");
                    yield return new WaitForSeconds(0.6f);
                    floorsLooked++;
                    var info = tm.CurrentInfo;
                    string tag = $"seed {seed} floor {floor}";
                    var pieces = LootOnShow();
                    int total = pieces.Sum(p => p.Value);
                    int solo = pieces.Where(p => p.Loot == null || p.Loot.lifters <= 1).Sum(p => p.Value);
                    int heavy = pieces.Count(p => p.Loot != null && p.Loot.lifters > 1);
                    var spawn = info.spawn;
                    // (the players' elevator car is closed off from the NavMesh by design, so the walk is measured from where a guard stands: the guards patrol the whole floor)
                    var patrolling = GuardAI.All.Where(g => g && g.isActiveAndEnabled && !g.Dead && g.name != "Guard_Outpost").ToList();
                    NavMesh.SamplePosition(patrolling.Count > 0 ? patrolling[0].transform.position : spawn, out var spawnHit, 4f, NavMesh.AllAreas);
                    int unreachable = 0;
                    var lost = new List<string>();
                    foreach (var p in pieces)
                    {
                        bool ok = WalkableNear(spawnHit.position, p.transform.position);
                        if (!ok) { unreachable++; if (lost.Count < 4) lost.Add($"{p.Label} at {p.transform.position}"); }
                    }
                    Log($"[LootQa] {tag}: quota {info.quota}, loot value {info.lootValue} (x{info.lootValue / (float)Mathf.Max(1, info.quota):0.00}), {pieces.Count} pieces on show worth {total}, one player alone can carry {solo} (x{solo / (float)Mathf.Max(1, info.quota):0.00}), {heavy} heavy, {info.rooms} rooms, {info.guards} guards");
                    LqCheck($"{tag}: the floor holds at least the quota in loot", info.lootValue >= info.quota, $"({info.lootValue} vs {info.quota})");
                    LqCheck($"{tag}: the loot on show can make the quota even without the safe's cash", total >= info.quota * 0.9f, $"({total} vs {info.quota})");
                    // (a mug or a safe in a nook of furniture can fall between the NavMesh's holes: a few of 150 is the NavMesh, a room full is a bug)
                    LqCheck($"{tag}: the loot on show can be walked to (NavMesh path from a patrolling guard; at most 2 % in nooks)", unreachable <= Mathf.Max(3, pieces.Count / 50), $"({unreachable} of {pieces.Count} unreachable{(lost.Count > 0 ? ": " + string.Join("; ", lost) : "")})");
                    LqCheck($"{tag}: the player stands in the elevator, on the floor", Vector3.Distance(player.transform.position, info.spawn) < 6f && player.transform.position.y > -0.5f && player.transform.position.y < 2.5f, $"(at {player.transform.position})");

                    var guards = GuardAI.All.Where(g => g && g.isActiveAndEnabled && !g.Dead).ToList();
                    int offNav = guards.Count(g => !OnNavMesh(g.transform.position, 2f));
                    int armed = guards.Count(g => g.Armed);
                    int onDuty = guards.Count(g => g.name == "Guard_Outpost");
                    LqCheck($"{tag}: guards patrol the floor, all on the NavMesh", guards.Count >= 3 && offNav == 0, $"({guards.Count} guards, {offNav} off the NavMesh)");
                    LqCheck($"{tag}: exactly one guard is on duty in the outpost", onDuty == 1, $"({onDuty})");
                    LqCheck($"{tag}: guards are {(floor >= 4 ? "all armed" : "unarmed")}", floor >= 4 ? armed == guards.Count : armed == 0, $"({armed} of {guards.Count} armed)");
                    LqCheck($"{tag}: the guards' elevator and two stairwells are there", gen.HasGuardShaft && gen.StairCount == 2, $"(shaft {gen.HasGuardShaft}, stairs {gen.StairCount})");

                    bool hasRoom = gen.TryGetOutpostRoom(out var room);
                    var pews = FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None).Where(g => g && g.isActiveAndEnabled).ToList();
                    int pewsInOutpost = hasRoom ? pews.Count(g => g.transform.position.x > room.x0 - 0.5f && g.transform.position.x < room.x1 + 0.5f && g.transform.position.z > room.z0 - 0.5f && g.transform.position.z < room.z1 + 0.5f) : 0;
                    LqCheck($"{tag}: the outpost {(floor == 4 ? "holds exactly one pew, nothing else on the floor does" : "holds no pew")}", hasRoom && (floor == 4 ? pewsInOutpost == 1 && pews.Count == 1 : pews.Count == 0), $"(outpost {hasRoom}, pews on the floor {pews.Count}, in the outpost {pewsInOutpost})");
                }
            }
            Log($"[LootQa] part 1 looked at {floorsLooked} floors ({seeds.Length} seeds x 4)");

            // ---- part 2: the real loop on the offline run's own seed: bank loot in the car until the quota is met, then go down
            tm.baseSeed = ownSeed;
            hm.godMode = true;
            for (int floor = 1; floor <= 4; floor++)
            {
                tm.RideTo(floor);
                yield return new WaitUntil(() => tm.Loading || hm.Floor == floor);
                yield return WaitFor(() => !tm.Loading && hm.Floor == floor, 90, $"loop floor {floor}");
                yield return new WaitForSeconds(0.8f);
                hm.freezeTimer = true;
                string tag = $"loop floor {floor}";
                var car = FindObjectsByType<ElevatorExtraction>(FindObjectsSortMode.None).Select(e => e.GetComponent<Collider>()).FirstOrDefault(c => c && c.isTrigger);
                LqCheck($"{tag}: the elevator car has its banking zone", car != null && hm.Phase == HeistPhase.Heist);
                if (car == null) continue;
                var pieces = LootOnShow().Where(p => p.Loot == null || p.Loot.lifters <= 1).OrderByDescending(p => p.Value).ToList();
                int banked0 = hm.Banked, cash0 = hm.Cash, put = 0;
                var b = car.bounds;
                for (int batch = 0; batch * 8 < pieces.Count && hm.Phase == HeistPhase.Heist; batch++)
                {
                    for (int k = 0; k < 8 && batch * 8 + k < pieces.Count; k++)
                    {
                        var p = pieces[batch * 8 + k];
                        if (!p) continue;
                        var pos = new Vector3(b.min.x + 1.0f + (k % 4) * (b.size.x - 2f) / 3f, b.min.y + 0.7f, b.min.z + 1.0f + (k / 4) * (b.size.z - 2f));
                        if (p.TryGetComponent(out Rigidbody rb)) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                        p.transform.position = pos;
                        put++;
                    }
                    yield return new WaitForSeconds(2.2f);
                }
                yield return new WaitForSeconds(1.5f);
                LqCheck($"{tag}: banking the loose loot met the quota by itself (the elevator's own banking)", hm.Banked >= hm.Quota && hm.Phase == HeistPhase.FloorComplete, $"(banked {banked0} -> {hm.Banked} of {hm.Quota}, {put} pieces set down, phase {hm.Phase})");
                int surplus = hm.Surplus;
                yield return Ride(hm, tm, 0);
                LqCheck($"{tag}: at the docks the surplus became cash and the next floor is waiting", hm.Floor == 0 && hm.Cash >= cash0 + surplus && tm.ResumeLevel == floor + 1, $"(cash {cash0} -> {hm.Cash}, surplus {surplus}, resume {tm.ResumeLevel})");
            }
            LqCheck("after floor 4 the demo is over and the vendor has the pew", TowerRules.DemoOver(tm.ResumeLevel) && Vendor.PewForSale(tm.ResumeLevel));

            Log($"[LootQa] DONE fails={lqFails}");
            Quit(lqFails == 0 ? 0 : 1);
        }
    }
}
