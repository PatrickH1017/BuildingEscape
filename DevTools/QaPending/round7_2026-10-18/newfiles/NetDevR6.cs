using System.Collections;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode checks (offline, Greybox scene, batch mode is fine) of the owner's task list of 2026-10-18: an officer cannot pin a player through a wall, (later: punching, menus closing on an invite, the Esc menu's Settings button).
    /// Run: Unity -batchmode -executeMethod BuildingEscape.EditorTools.NetDevHarness.R6   (look for "[R6] DONE fails=0").
    /// </summary>
    public partial class NetDevRunner
    {
        int r6Fails;

        void R6Check(string name, bool ok, string detail = "")
        {
            if (!ok) r6Fails++;
            Log($"[R6] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator R6Scenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return R5LoadFloor(1);
            var player = WobblyPlayerRegistry.All.Count > 0 ? WobblyPlayerRegistry.All[0] : null;
            var guard = GuardAI.All.FirstOrDefault(g => g && !g.Dead && !g.IsPuppet);
            R6Check("floor 1 has a player and an officer", player != null && guard != null);
            if (player == null || guard == null) { Quit(1); yield break; }

            // ---- (4) pinning: nothing solid and fixed between the two of them, or no catch
            // (find a spot beside the officer where the line to the player is clear, as it is out in the open)
            Vector3 gpos = guard.transform.position, dir = Vector3.right;
            bool found = false;
            foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                player.TeleportTo(gpos + d * 1.2f + Vector3.up * 0.1f, Quaternion.identity);
                yield return new WaitForSeconds(0.2f);
                if (guard.HasReach(player)) { dir = d; found = true; break; }
            }
            R6Check("a spot beside the officer with a clear line to the player was found", found);
            if (found)
            {
                yield return new WaitForSeconds(0.1f);
                R6Check("with nothing between them he can lay hands on the player (reach)", guard.HasReach(player));
                var mid = (gpos + player.transform.position) * 0.5f + Vector3.up * 0.9f;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "R6Wall";
                wall.transform.position = mid;
                wall.transform.rotation = Quaternion.LookRotation(dir);
                wall.transform.localScale = new Vector3(3f, 3f, 0.15f);              // (a thin partition across the line)
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                R6Check("a wall between them: no reach", !guard.HasReach(player));
                hm.godMode = false;                                                    // (so that only the wall can be what stops the catch)
                bool caught = guard.TryCatch(player);
                R6Check("...so he cannot pin the player through it (TryCatch is false, the player is still free)", !caught && player.Custody == CustodyState.Free && guard.State != GuardState.Pinning);
                hm.godMode = true;
                Object.Destroy(wall);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                R6Check("the wall gone: reach again", guard.HasReach(player));
                // a crate is not a wall: it moves
                var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = "R6Crate";
                crate.transform.position = mid;
                crate.transform.rotation = Quaternion.LookRotation(dir);
                crate.transform.localScale = new Vector3(0.5f, 1.5f, 0.15f);
                crate.AddComponent<Rigidbody>().isKinematic = true;
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                R6Check("a crate (a rigid body) between them is not a wall: he still has reach", guard.HasReach(player));
                Object.Destroy(crate);
            }

            Log($"[R6] DONE fails={r6Fails}");
            Quit(r6Fails == 0 ? 0 : 1);
        }
    }
}
