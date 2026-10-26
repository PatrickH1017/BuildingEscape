using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (<see cref="NetDevTest.PvpBattlefieldTest"/>) and the Play scenarios of T7, the PVP room as a battlefield (the owner, 2026-10-23: "The PvP room should use the battle royale assets from SYNTY to make it seem more like a battlefield";
    /// DEVELOPER_NOTES 2.63): <c>PvpBattlefield</c> (<c>qa_regress_bd.ps1 -Methods PvpBattlefield</c>: every piece stands where the list says, nothing overlaps, the room's numbers are the rules', the way into the room is open) and <c>PvpBattlefieldUi</c>
    /// (pictures from five places in a real window: <c>ui.ps1 -Project C:\bd -Methods PvpBattlefieldUi</c>, %TEMP%\be_ui_pvp_field_*.png).
    /// </summary>
    public static partial class NetDevTest
    {
        public static void PvpBattlefieldTest()
        {
            string problems = PvpBattlefieldRules.Problems();
            Check("battlefield (T7): every piece is in the prop table, square to the room, inside it, not on the mat, not in the doorway's lane and not on another piece" + (problems.Length == 0 ? "" : " [" + problems + "]"), problems.Length == 0);
            var kinds = PvpBattlefieldRules.Pieces.Select(p => p.key).Distinct().ToList();
            Check("battlefield (T7): ten pieces or more of at least six kinds (the Battle Royale pack's barriers, walls, tank traps, spool and drums) [" + PvpBattlefieldRules.Pieces.Length + " pieces, " + kinds.Count + " kinds]", PvpBattlefieldRules.Pieces.Length >= 10 && kinds.Count >= 6);
            Check("battlefield (T7): the long barrier wall is the largest piece, lying north to south on the east side; its prop is 5.5 x 1.4 x 2.1 m",
                PropTable.TryGet("hesco_wall", out var w) && System.Math.Abs(w.W - 5.5f) < 0.05f && System.Math.Abs(w.D - 1.39f) < 0.05f && System.Math.Abs(w.H - 2.13f) < 0.05f
                && PvpBattlefieldRules.Pieces.Any(p => p.key == "hesco_wall" && p.yaw == 90f && p.x > 7.5f));
        }
    }

    public partial class NetDevRunner
    {
        int bfFails;
        void BfCheck(string name, bool ok, string detail = "") { if (!ok) bfFails++; Log($"[PvpBattlefield] {(ok ? "PASS" : "FAIL")} {name} {detail}"); }

        IEnumerator PvpBattlefieldScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            yield return new WaitForSeconds(0.5f);
            gen.Clear(); gen.Generate(0, 1000);
            yield return null; yield return null;
            Physics.SyncTransforms();

            // the room is the one the rules were written for
            var room = gen.PvpRoomArea; var annex = gen.PvpRoomAnnex; var door = gen.PvpRoomDoor;
            bool same = Mathf.Abs(room.xMin - PvpBattlefieldRules.HallX0) < 0.02f && Mathf.Abs(room.xMax - PvpBattlefieldRules.HallX1) < 0.02f && Mathf.Abs(room.yMin - PvpBattlefieldRules.HallZ0) < 0.02f && Mathf.Abs(room.yMax - PvpBattlefieldRules.HallZ1) < 0.02f
                && Mathf.Abs(annex.xMin - PvpBattlefieldRules.PocketX0) < 0.02f && Mathf.Abs(annex.xMax - PvpBattlefieldRules.PocketX1) < 0.02f && Mathf.Abs(annex.yMin - PvpBattlefieldRules.PocketZ0) < 0.02f && Mathf.Abs(annex.yMax - PvpBattlefieldRules.PocketZ1) < 0.02f;
            BfCheck("T7: the room's numbers (hall, pocket) are the ones the battlefield's rules were written for", same, $"(hall {room}, pocket {annex})");
            BfCheck("T7: the doorway's lane of the rules is at the doorway (its z is the middle of the lane)", Mathf.Abs(door.z - (PvpBattlefieldRules.LaneZ0 + PvpBattlefieldRules.LaneZ1) * 0.5f) < 0.05f, $"(doorway z {door.z:0.00})");

            // every piece stands where the list says; none of its box overlaps anything else solid
            var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int missing = 0, collided = 0; string firstMissing = "", firstCollision = "";
            foreach (var p in PvpBattlefieldRules.Pieces)
            {
                GameObject found = null;
                foreach (var t in all)
                {
                    if (t.name != p.key || !t.gameObject.activeInHierarchy) continue;
                    var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    if (Mathf.Abs(b.center.x - p.x) < 0.12f && Mathf.Abs(b.center.z - p.z) < 0.12f) { found = t.gameObject; break; }
                }
                if (found == null) { missing++; if (firstMissing.Length == 0) firstMissing = $"{p.key} at ({p.x:0.00}, {p.z:0.00})"; continue; }
                var bc = found.GetComponent<BoxCollider>();
                if (bc == null) { missing++; if (firstMissing.Length == 0) firstMissing = $"{p.key} has no collider"; continue; }
                var wb = bc.bounds;
                var ext = wb.extents - new Vector3(0.05f, 0.05f, 0.05f);
                foreach (var col in Physics.OverlapBox(wb.center + Vector3.up * 0.02f, ext, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (col.transform.IsChildOf(found.transform)) continue;
                    collided++; if (firstCollision.Length == 0) firstCollision = $"{p.key} at ({p.x:0.00}, {p.z:0.00}) with {col.name}";
                    break;
                }
            }
            BfCheck("T7: every piece of the list stands in the room, with a collider", missing == 0, missing == 0 ? $"({PvpBattlefieldRules.Pieces.Length} pieces)" : $"({missing} missing, first: {firstMissing})");
            BfCheck("T7: no piece's box overlaps a shelf, a crate, a wall or another piece", collided == 0, collided == 0 ? "" : $"({collided} overlap, first: {firstCollision})");
            string wayIn = PvpWayIn(gen);
            BfCheck("T7: players can walk to every free spot of the room from the elevator's door, and a forklift gets to the doorway", wayIn.Length == 0, wayIn.Length == 0 ? "" : $"({wayIn})");

            Log($"[PvpBattlefield] DONE fails={bfFails}");
            Quit(bfFails == 0 ? 0 : 1);
        }

        IEnumerator PvpBattlefieldUiScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(1.0f);
            var player = WobblyPlayerRegistry.All[0];
            var views = new (string name, Vector3 at, Vector3 look)[]
            {
                ("door", new Vector3(8.9f, 0f, 15.6f), new Vector3(3.0f, 0f, 19.0f)),
                ("mat", new Vector3(4.75f, 0f, 18.7f), new Vector3(4.75f, 0f, 24.0f)),
                ("south_west", new Vector3(1.9f, 0f, 14.3f), new Vector3(6.0f, 0f, 19.0f)),
                ("pocket", new Vector3(2.0f, 0f, 12.6f), new Vector3(2.5f, 0f, 6.0f)),
                ("north_east", new Vector3(6.8f, 0f, 22.6f), new Vector3(2.5f, 0f, 17.5f)),
            };
            foreach (var v in views)
            {
                var d = v.look - v.at; d.y = 0f;
                player.TeleportTo(v.at + Vector3.up * 1.1f, Quaternion.LookRotation(d.normalized));
                yield return new WaitForSeconds(1.2f);
                yield return Snap("pvp_field_" + v.name);
            }
            Log("[PvpBattlefield] DONE pictures");
            Quit(0);
        }
    }
}
