using System.Collections;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63; offline, on the real floors): the checks of each finished task, one section per task, in the order they were built. Batch A1: the scan that waits 3 seconds (T21), "drop all loot"
    /// that drops every piece of loot (T8), the one random crowbar on floor 1 outside the outpost (T17), the weights of the weapons (T20) and the guards that keep growing in number with the floor (T14). Run: <c>qa_regress.ps1 -Methods Asks1023</c>;
    /// the log ends with <c>[Asks1023] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int a23Fails;

        void A23(string name, bool ok, string detail = "")
        {
            if (!ok) a23Fails++;
            Log($"[Asks1023] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator Asks1023Scenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var gen = FindFirstObjectByType<FloorGenerator>();

            // ================================================================== floor 1
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(1.0f);
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();

            // ---- T17: one random crowbar on floor 1, outside the outpost
            {
                Rc op = default; bool hasOp = gen.TryGetOutpostRoom(out op);
                var crowbars = FindObjectsByType<MeleeWeapon>(FindObjectsSortMode.None).Where(m => m && m.isActiveAndEnabled && m.kind == MeleeWeapon.Kind.Crowbar).ToList();
                var outside = crowbars.Where(m => !(hasOp && op.Grow(1.5f).Contains(m.transform.position.x, m.transform.position.z))).ToList();
                A23("T17: floor 1 has exactly one crowbar lying outside the outpost", outside.Count == 1, $"({outside.Count} outside, {crowbars.Count} crowbars in all, outpost {hasOp})");
                if (outside.Count == 1)
                {
                    var c = outside[0];
                    bool onMesh = UnityEngine.AI.NavMesh.SamplePosition(c.transform.position, out var hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas);
                    A23("T17: ... it lies on walkable floor, with a pickup that weighs what the spec says (T20: half of 3 kg)", onMesh && c.TryGetComponent(out Pickup pk) && Mathf.Approximately(pk.weight, MeleeWeapon.Specs[(int)MeleeWeapon.Kind.Crowbar].weight) && Mathf.Approximately(pk.weight, 1.5f), $"(on the NavMesh {onMesh})");
                }
            }

            // ---- T20: a pistol made on the floor weighs what the spec says (half of 1.2 kg)
            {
                var pewGo = RangedWeapon.Create(RangedWeapon.Kind.Pew, player.transform.position + Vector3.up * 0.5f + player.transform.forward * 2f, null, 0f, 0);
                var pew = pewGo.GetComponent<Pickup>();
                A23("T20: a pistol weighs 0.6 kg (half of the 1.2 it did)", Mathf.Approximately(pew.weight, 0.6f), $"({pew.weight} kg)");
                Destroy(pewGo);
            }

            // ---- stand in a room for the rest (the elevator car banks anything with a cash value that lies in it, so loot dropped at the arrival point would be gone)
            var rooms = gen.CurrentLayout.rooms.Where(r => r.kind == 0 && r.bounds.W > 3f && r.bounds.D > 3f).ToList();
            var room = rooms.FirstOrDefault();
            A23("(test) a room to stand in", room != null);
            if (room != null)
            {
                player.TeleportTo(new Vector3(room.bounds.Cx, 1.1f, room.bounds.Cz), Quaternion.Euler(0f, 90f, 0f));
                yield return new WaitForSeconds(1.0f);
            }

            // ---- T8: drop all loot drops every piece of loot and keeps the equipment
            {
                var at = player.transform.position + player.transform.forward * 2f + Vector3.up * 0.6f;
                var watch = AsksItem("Watch", at, 300);                                        // (a valuable: a bank value and no HeavyLoot, like the real ones)
                var bundle = AsksItem("Cash bundle", at + Vector3.right * 0.4f, 500);          // (cash: the same)
                var heavyGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                heavyGo.name = "AsksHeavy"; heavyGo.transform.position = at + Vector3.left * 0.4f; heavyGo.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
                heavyGo.AddComponent<Rigidbody>().isKinematic = true;
                var heavy = heavyGo.AddComponent<Pickup>(); heavy.label = "Prototype"; heavy.weight = 0.5f;
                var hl = heavyGo.AddComponent<HeavyLoot>(); hl.displayName = "Prototype"; hl.baseValue = 400; hl.lifters = 1;
                var crowbarItem = MeleeWeapon.Create(MeleeWeapon.Kind.Crowbar, at + Vector3.up * 0.3f, gen.Current, 0f).GetComponent<Pickup>();
                foreach (var p in new[] { watch, bundle, heavy, crowbarItem }) pack.TryStore(p);
                yield return null;
                int before = pack.Items.Count;
                A23("T8: the pack holds the watch, the cash bundle, the heavy piece and the crowbar", before >= 4 && pack.Contains(watch) && pack.Contains(bundle) && pack.Contains(heavy) && pack.Contains(crowbarItem), $"({before} items)");
                A23("T8: the watch, the cash and the heavy piece count as loot, the crowbar does not", watch.CountsAsLoot && bundle.CountsAsLoot && heavy.CountsAsLoot && !crowbarItem.CountsAsLoot);
                pack.DropAll();
                yield return new WaitForSeconds(1.0f);
                bool lootOut = !pack.Contains(watch) && !pack.Contains(bundle) && !pack.Contains(heavy) && watch && watch.gameObject.activeInHierarchy && bundle && bundle.gameObject.activeInHierarchy && heavy && heavy.gameObject.activeInHierarchy;
                A23("T8: drop all loot drops the valuable, the cash and the heavy piece (they are in the world again)", lootOut, $"(watch in pack {pack.Contains(watch)}, bundle {pack.Contains(bundle)}, heavy {pack.Contains(heavy)})");
                A23("T8: ... and the weapon stays in the pack", pack.Contains(crowbarItem));
                bool near = new[] { watch, bundle, heavy }.All(p => p && Vector3.Distance(p.transform.position, player.transform.position) < 3.5f && p.transform.position.y > player.transform.position.y - 1.5f);
                A23("T8: ... the dropped things lie close to the player, none fallen through the floor", near);
                var spread = Enumerable.Range(0, 20).Select(Backpack.DropSpread).ToList();
                A23("T8: ... a drop of twenty is laid out in rings: the first on the drop point, no two on the same spot, none farther than 1.5 m", spread[0] == Vector3.zero && spread.Select(v => (Mathf.Round(v.x * 100f), Mathf.Round(v.z * 100f))).Distinct().Count() == 20 && spread.All(v => v.magnitude < 1.5f));
                foreach (var p in new[] { watch, bundle, heavy }) if (p) Destroy(p.gameObject);
                pack.Drop(crowbarItem);
                yield return null;
                if (crowbarItem) Destroy(crowbarItem.gameObject);
            }

            // ---- T21 (changed 2026-10-23, after the playtest): the scan goes off at once and has a 2 second cooldown
            {
                var scanner = LootScanner.Instance;
                A23("T21: the scanner exists and there is a room to stand in", scanner != null && room != null);
                if (scanner != null && room != null)
                {
                    player.TeleportTo(new Vector3(room.bounds.Cx, 1.1f, room.bounds.Cz), Quaternion.Euler(0f, 90f, 0f));
                    yield return new WaitForSeconds(1.2f);
                    int pulses0 = scanner.Pulses;
                    LootScanner.TestKeyDown = true;
                    yield return new WaitForSeconds(0.4f);
                    A23("T21: the scan goes off at once when the key goes down (no charge-up)", scanner.Pulses == pulses0 + 1 && LootScanner.Scanning, $"(pulses {scanner.Pulses - pulses0})");
                    yield return new WaitForSeconds(1.0f);
                    A23("T21: 1.4 s after it the key is still held but the cooldown holds the next scan back (the bar is not full)", scanner.Pulses == pulses0 + 1 && LootScanner.ScanReady < 0.9f, $"(pulses {scanner.Pulses - pulses0}, ready {LootScanner.ScanReady:0.00})");
                    yield return new WaitForSeconds(1.0f);
                    A23("T21: 2.4 s after the first scan, still held, the second leaves", scanner.Pulses == pulses0 + 2, $"(pulses {scanner.Pulses - pulses0})");
                    LootScanner.TestKeyDown = false;
                    yield return new WaitForSeconds(0.3f);
                    A23("T21: letting go stops the scanning", !LootScanner.Scanning);
                    int pulses1 = scanner.Pulses;
                    LootScanner.TestKeyDown = true;
                    yield return new WaitForSeconds(0.5f);
                    A23("T21: pressing again straight after a scan does not skip the cooldown", scanner.Pulses == pulses1, $"(pulses {scanner.Pulses - pulses1}, ready {LootScanner.ScanReady:0.00})");
                    yield return new WaitForSeconds(1.6f);
                    A23("T21: once the cooldown is over the held key scans again", scanner.Pulses == pulses1 + 1, $"(pulses {scanner.Pulses - pulses1})");
                    LootScanner.TestKeyDown = false;
                    yield return new WaitForSeconds(0.2f);
                }
            }

            // ---- T26: the free look (Alt) unlocks the view only while it is held, and only after a quarter of a second
            {
                var rig = CameraFollow.Current;
                A23("T26: the camera rig is there", rig != null);
                if (rig != null)
                {
                    CameraFollow.TestFreeLookKey = true;
                    yield return new WaitForSecondsRealtime(0.1f);
                    bool tapStayedLocked = !rig.FreeLooking;
                    CameraFollow.TestFreeLookKey = false;
                    yield return new WaitForSecondsRealtime(0.2f);
                    A23("T26: a tap on the key (0.1 s) does not unlock the view", tapStayedLocked && !rig.FreeLooking);
                    CameraFollow.TestFreeLookKey = true;
                    yield return new WaitForSecondsRealtime(0.6f);
                    bool heldUnlocked = rig.FreeLooking;
                    CameraFollow.TestFreeLookKey = false;
                    yield return new WaitForSecondsRealtime(0.15f);
                    A23("T26: held for 0.6 s the view is unlocked, and it is locked again as soon as the key is let go", heldUnlocked && !rig.FreeLooking, $"(unlocked while held {heldUnlocked}, after letting go {rig.FreeLooking})");
                }
            }

            // ================================================================== floor 7: the guards (T14)
            yield return LoadLevel(hm, 7);
            yield return new WaitForSeconds(1.0f);
            {
                int guards = GuardAI.All.Count(g => g && !g.IsBoss);
                int want = GuardRules.PatrolCount(7, gen.baseGuards);
                A23("T14: floor 7 has its patrol guards by the rule (12) and the outpost's one", guards >= want && guards <= want + 2 && want == 12, $"({guards} guards, the rule says {want} patrolling)");
            }
            yield return LoadLevel(hm, 11);
            yield return new WaitForSeconds(1.0f);
            {
                int guards = GuardAI.All.Count(g => g && !g.IsBoss);
                int want = GuardRules.PatrolCount(11, gen.baseGuards);
                A23("T14: floor 11 has more (16, not the 12 it was cut at) and still leaves room under the alarm cap of 24", guards >= want && guards <= want + 2 && want == 16 && guards + 6 <= GuardRules.MaxGuardsAlive, $"({guards} guards, the rule says {want} patrolling, cap {GuardRules.MaxGuardsAlive})");
            }

            Log($"[Asks1023] DONE fails={a23Fails}");
            Quit(a23Fails == 0 ? 0 : 1);
        }
    }
}
