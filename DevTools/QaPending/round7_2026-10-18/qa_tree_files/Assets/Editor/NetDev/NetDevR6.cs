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

        static bool ShowThenHideAll()
        {
            OutpostUi.Show(OutpostUi.Kind.Intro);
            OutpostUi.HideAll();
            bool stays = OutpostUi.Current == OutpostUi.Kind.Intro;
            OutpostUi.Hide(OutpostUi.Kind.Intro);
            return stays && !OutpostUi.Open;
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

            // ---- (1) accepting an invite closes every open menu
            {
                var inbox = BuildingEscape.Net.InviteInbox.Instance;
                R6Check("the invite inbox is there", inbox != null);
                EscapeMenu.OpenForTests(true);
                OutpostUi.Show(OutpostUi.Kind.Journal);
                R6Check("the Esc menu (on its Settings page) and an outpost screen are up", EscapeMenu.Open && EscapeMenu.SettingsShowing && OutpostUi.Open && MenuRegistry.AnyOpen);
                if (inbox != null)
                {
                    int closed0 = MenuRegistry.Closed;
                    inbox.Enqueue(new Steamworks.CSteamID(76561198000000123UL), "A friend");
                    yield return new WaitUntil(() => inbox.Showing);
                    R6Check("the invite is showing", inbox.Showing);
                    inbox.Accept();
                    yield return null;
                    R6Check("accepting it closed every menu: the Esc menu with its settings page and the outpost screen are gone", !EscapeMenu.Open && !EscapeMenu.SettingsShowing && !OutpostUi.Open && !MenuRegistry.AnyOpen && MenuRegistry.Closed == closed0 + 1);
                }
                OutpostUi.Hide(OutpostUi.Kind.Journal);
                R6Check("the intro cinematic is not one of the menus: HideAll leaves it", ShowThenHideAll());
                EscapeMenu.OpenForTests(false);
                R6Check("the Esc menu opens on its main page for the tests and closes with CloseAll", EscapeMenu.Open && !EscapeMenu.SettingsShowing);
                MenuRegistry.CloseAll();
                R6Check("...closed", !EscapeMenu.Open);
            }

            // ---- (2) punching: a punch stuns an officer for a moment but does not count towards knocking him out
            {
                var melee = player.GetComponent<MeleeController>();
                var pack = player.GetComponent<Backpack>();
                R6Check("the player can punch: a melee controller and nothing equipped", melee != null && pack != null && !pack.Equipped);
                var agent = guard.GetComponent<UnityEngine.AI.NavMeshAgent>();
                bool placed = false;
                var home = guard.transform.position;
                for (int k = 0; k < 8 && !placed && melee != null && agent != null; k++)
                {
                    player.TeleportTo(home + Vector3.up * 0.1f, Quaternion.Euler(0f, k * 45f, 0f));
                    yield return new WaitForSeconds(0.2f);
                    var ahead = Crosshair.SwingForward(player.transform); ahead.y = 0f;
                    var spot = player.transform.position + ahead.normalized * PunchRules.Reach; spot.y = home.y - agent.baseOffset;       // (his transform is his middle: the NavMesh is baseOffset lower)
                    placed = UnityEngine.AI.NavMesh.SamplePosition(spot, out var navHit, 0.6f, UnityEngine.AI.NavMesh.AllAreas) && agent.Warp(navHit.position);
                }
                R6Check("an officer was put right in front of the fists", placed);
                if (placed)
                {
                    yield return new WaitForSeconds(0.3f);
                    int before = guard.Hits;
                    bool thrown = melee.TryPunch();
                    R6Check("a punch is thrown with empty hands", thrown && melee.PunchesThrown == 1);
                    R6Check("...and a second one at once is not (the blow is not over, the cooldown)", !melee.TryPunch());
                    yield return new WaitForSeconds(0.7f);
                    R6Check("it landed: the officer is stunned, and the blow did not count towards knocking him out", melee.PunchesLanded >= 1 && guard.State == GuardState.Stunned && guard.Hits == before && !guard.Dead, $"(state {guard.State}, hits {before} -> {guard.Hits}, landed {melee.PunchesLanded})");
                    yield return new WaitForSeconds(PunchRules.StunSeconds + 0.8f);
                    R6Check("a moment later he is up again", guard.State != GuardState.Stunned && !guard.Dead, $"({guard.State})");
                    R6Check("another punch can be thrown after the cooldown", melee.TryPunch());
                    yield return new WaitForSeconds(0.8f);
                }
                // what teammates hear: the remote calls count (the sound itself falls silent with the distance)
                int swings0 = melee.RemoteSwingsHeard, hits0 = melee.RemoteHitsHeard;
                melee.PlayRemoteSwing(MeleeWeapon.PunchKind); melee.PlayRemoteSwing(0);
                melee.PlayRemoteHit(MeleeWeapon.PunchKind, false, player.transform.position); melee.PlayRemoteHit(1, true, player.transform.position);
                R6Check("a teammate's swings and hits are played here (the remote calls ran: 2 swings, 2 hits)", melee.RemoteSwingsHeard == swings0 + 2 && melee.RemoteHitsHeard == hits0 + 2);
            }

            Log($"[R6] DONE fails={r6Fails}");
            Quit(r6Fails == 0 ? 0 : 1);
        }
    }
}
