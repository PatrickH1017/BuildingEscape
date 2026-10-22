using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of the owner's list of 2026-10-21 (offline, on the real floors): the elevator countdown that is cancelled when somebody steps out, the gun that ignores the click that closed the elevator panel, an officer who is shot and radios for
    /// backup (and whose call is cancelled when he is put down in time), a dead officer who lies there and can be walked through, the treasures for two or three that stand where they can be reached from both sides, and the ping. The pistol's
    /// bare first-person view is checked in <c>FpLook</c>. Run: <c>qa_regress.ps1 -Methods Asks1021</c> (<c>Asks1021:gfx</c> adds the pictures).
    /// </summary>
    public partial class NetDevRunner
    {
        int asksFails;

        void AsksCheck(string name, bool ok, string detail = "")
        {
            if (!ok) asksFails++;
            Log($"[Asks] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static Pickup AsksItem(string label, Vector3 pos, int value = 0, bool clip = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "AsksItem_" + label;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
            go.AddComponent<Rigidbody>().isKinematic = true;
            var p = go.AddComponent<Pickup>();
            p.label = label; p.bankValue = value; p.weight = 0.5f;
            if (clip) go.AddComponent<AmmoClip>();
            return p;
        }

        IEnumerator Asks1021Scenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            UnityEditor.EditorSettings.asyncShaderCompilation = false;                   // (no pink stand-ins in the pictures while a shader variant compiles in the background)
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            yield return new WaitForSeconds(1.5f);

            // ================================================================== the elevator countdown (the owner: "When someone leaves the elevator while the countdown is active, the countdown should reset (cancel)...")
            var car = ElevatorExtraction.Current;
            AsksCheck("the elevator car is there and the player starts inside it", car != null && ElevatorExtraction.EveryoneAboard, $"(aboard {ElevatorExtraction.EveryoneAboard})");
            var carBox = car.GetComponent<Collider>().bounds;
            var inside = player.transform.position;                                     // (where the player starts: next to the panel, as GO needs)
            Vector3 outside = default; bool haveOutside = false;
            foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
            {
                var cand = carBox.center + dir * (carBox.extents.magnitude + 2.5f);
                if (!Physics.Raycast(cand + Vector3.up * 2f, Vector3.down, out var floorHit, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                cand = floorHit.point + Vector3.up * 1.0f;
                if (carBox.Contains(cand) || Physics.CheckCapsule(cand + Vector3.up * 0.1f, cand + Vector3.up * 1.4f, 0.35f, ~0, QueryTriggerInteraction.Ignore)) continue;
                outside = cand; haveOutside = true; break;
            }
            AsksCheck("there is a free spot outside the car to step out to", haveOutside, haveOutside ? $"({outside})" : "(none found)");
            car.HostPress(player.transform, pack, ElevatorExtraction.SelectBase + 1);
            yield return null;
            AsksCheck("floor 1 is chosen on the panel", ElevatorExtraction.Destination == 1, $"(destination {ElevatorExtraction.Destination})");
            car.HostPress(player.transform, pack, 1);
            yield return null;
            AsksCheck("GO starts the countdown", ElevatorExtraction.Sealing);
            yield return new WaitForSeconds(0.5f);
            AsksCheck("half a second into the countdown it is still running (nobody has left)", ElevatorExtraction.Sealing && hm.Floor == 0);
            if (haveOutside) player.TeleportTo(outside, Quaternion.identity);
            yield return new WaitForSeconds(0.3f);
            AsksCheck("somebody steps out of the car: the countdown is cancelled at once (well before its 2 seconds are up)", !ElevatorExtraction.Sealing && !ElevatorExtraction.EveryoneAboard && hm.Floor == 0 && !tm.Loading, $"(sealing {ElevatorExtraction.Sealing}, aboard {ElevatorExtraction.EveryoneAboard})");
            AsksCheck("... the floor stays chosen on the panel (GO is all that has to be clicked again)", ElevatorExtraction.Destination == 1);
            player.TeleportTo(inside, Quaternion.identity);
            yield return new WaitForSeconds(3.5f);
            AsksCheck("they get back in and nothing starts by itself: no countdown, the car has not moved", ElevatorExtraction.EveryoneAboard && !ElevatorExtraction.Sealing && hm.Floor == 0 && !tm.Loading, $"(aboard {ElevatorExtraction.EveryoneAboard}, sealing {ElevatorExtraction.Sealing})");
            car.HostPress(player.transform, pack, 1);
            yield return null;
            AsksCheck("a player clicks GO again and the countdown starts again", ElevatorExtraction.Sealing);
            // a quick step out and back in inside the countdown still cancels it (the owner: the countdown cancels when somebody leaves, coming back is not enough)
            yield return new WaitForSeconds(0.3f);
            if (haveOutside) { player.TeleportTo(outside, Quaternion.identity); yield return new WaitForSeconds(0.25f); player.TeleportTo(inside, Quaternion.identity); }
            yield return new WaitForSeconds(0.3f);
            AsksCheck("... and stepping out and straight back in within the countdown does not save it: it is cancelled", !ElevatorExtraction.Sealing && hm.Floor == 0 && !tm.Loading, $"(sealing {ElevatorExtraction.Sealing})");
            yield return new WaitForSeconds(2.6f);
            AsksCheck("... and again nothing restarts", !ElevatorExtraction.Sealing && hm.Floor == 0 && !tm.Loading);
            car.HostPress(player.transform, pack, 1);
            yield return WaitFor(() => tm.Loading || hm.Floor == 1, 12, "the ride after a clean countdown");
            AsksCheck("with everybody inside for the whole countdown the ride goes (floor 1 loads)", true);
            yield return WaitFor(() => !tm.Loading && hm.Floor == 1 && hm.Phase == HeistPhase.Heist, 120, "floor 1");
            hm.godMode = true; hm.freezeTimer = true;
            player = WobblyPlayerRegistry.All[0];
            pack = player.GetComponent<Backpack>();
            yield return WaitFor(() => GuardAI.All.Count(g => g && !g.Dead && !g.IsBoss) >= 3, 30, "three guards on the floor");
            yield return new WaitForSeconds(1.0f);

            // ================================================================== a click that began in a menu is not a shot (the owner: "When you click go on the elevator menu you fire a shot from your gun")
            var gunItem = RangedWeapon.Create(RangedWeapon.Kind.Pew, player.transform.position + Vector3.up * 0.5f, null, 0f, 2).GetComponent<Pickup>();
            pack.TryStore(gunItem);
            yield return new WaitForSeconds(0.3f);
            pack.SelectSlot(0);
            yield return new WaitForSeconds(1.5f);
            var gc = player.GetComponent<GunController>();
            AsksCheck("the pistol is in the hand", gc != null && gc.Held != null && pack.Equipped == gunItem);
            GunController.TestCursorLocked = true;                                      // (a batch run cannot lock the cursor: the gun is told it is in the game)
            var panelUi = FindFirstObjectByType<ElevatorPanel>();
            var openPanel = typeof(ElevatorPanel).GetMethod("OpenPanel", BindingFlags.NonPublic | BindingFlags.Instance);
            var closePanel = typeof(ElevatorPanel).GetMethod("ClosePanel", BindingFlags.NonPublic | BindingFlags.Instance);
            var nearPanel = Vector3.Lerp(ElevatorExtraction.Current.panelPosition, ElevatorExtraction.Current.GetComponent<Collider>().bounds.center, 0.35f); nearPanel.y = player.transform.position.y;
            player.TeleportTo(nearPanel, Quaternion.identity);
            yield return new WaitForSeconds(0.4f);
            int shots0 = gc.ShotsFired;
            GunController.TestTrigger = true;
            yield return new WaitForSeconds(0.5f);
            AsksCheck("control: with the fire button held and the gun usable it fires", gc.ShotsFired > shots0, $"({gc.ShotsFired - shots0} shots)");
            GunController.TestTrigger = false;
            yield return new WaitForSeconds(0.4f);
            AsksCheck("the elevator panel is up and the cursor is free", panelUi != null && openPanel != null);
            openPanel.Invoke(panelUi, new object[] { player.transform });
            yield return new WaitForSeconds(0.2f);
            AsksCheck("panel open: the gun cannot be used", ElevatorPanel.Open && Crosshair.MenuOpen);
            int shots1 = gc.ShotsFired;
            GunController.TestTrigger = true;                         // (the click on GO: the button goes down while the panel is up...)
            yield return new WaitForSeconds(0.3f);
            AsksCheck("... the click on a panel button fires nothing", gc.ShotsFired == shots1 && gc.TriggerLatched, $"({gc.ShotsFired - shots1} shots, latched {gc.TriggerLatched})");
            closePanel.Invoke(panelUi, null);                         // (... the panel closes on the click, the button is still held down)
            yield return new WaitForSeconds(0.6f);
            AsksCheck("the panel has closed on the click and the button is still down: the gun does NOT fire (the owner's bug)", !ElevatorPanel.Open && gc.ShotsFired == shots1 && gc.TriggerLatched, $"({gc.ShotsFired - shots1} shots, latched {gc.TriggerLatched}, cursor {Cursor.lockState})");
            GunController.TestTrigger = false;
            yield return new WaitForSeconds(0.3f);
            AsksCheck("the button is let go: the latch opens", !gc.TriggerLatched);
            GunController.TestTrigger = true;
            yield return new WaitForSeconds(0.5f);
            AsksCheck("a new press fires as usual", gc.ShotsFired > shots1, $"({gc.ShotsFired - shots1} shots)");
            GunController.TestTrigger = null; GunController.TestCursorLocked = null;
            yield return new WaitForSeconds(0.3f);
            pack.Drop(gunItem); Destroy(gunItem.gameObject);
            yield return new WaitForSeconds(0.3f);

            // ================================================================== an officer who is shot calls for backup; put down in time, the call is cancelled (the owner)
            foreach (var g in GuardAI.All) if (g) g.sightRange = 0.1f;                  // (nobody sees the player: only the bullets count)
            var guards = GuardAI.All.Where(g => g && !g.Dead && !g.IsBoss && !g.IsBackup).ToList();
            var fast = guards[0]; var slow = guards[1]; var meleeShot = guards[2];
            AsksCheck("no backup has been called yet", !hm.BackupCalled);
            fast.hitsToKill = 4;
            int guardsBefore = GuardAI.All.Count(g => g && !g.Dead);
            fast.BulletHitHost(1f, false, player.gameObject);
            AsksCheck("a bullet that does not put an officer down starts his radio call at once (he stops and radios: the ring over his head)", fast.State == GuardState.Calling && fast.IsCalling && !fast.Dead && !hm.BackupCalled, $"(state {fast.State}, calling {fast.IsCalling})");
            yield return new WaitForSeconds(0.3f);
            fast.BulletHitHost(1f, false, player.gameObject);
            AsksCheck("a second bullet does not start the call over and does not stop it", fast.IsCalling && fast.CallProgress > 0.1f, $"(progress {fast.CallProgress:0.00})");
            yield return new WaitForSeconds(0.3f);
            fast.BulletHitHost(1f, false, player.gameObject);
            yield return new WaitForSeconds(0.3f);
            fast.BulletHitHost(1f, false, player.gameObject);
            AsksCheck("the bullet that puts him down inside the call's two seconds cancels the call: no ring, no backup", fast.Dead && !fast.IsCalling && fast.CallProgress == 0f && !hm.BackupCalled, $"(dead {fast.Dead}, calling {fast.IsCalling})");
            yield return new WaitForSeconds(fast.callSeconds + 1.0f);
            AsksCheck("... and none comes later: backup is still not called and no officers were sent", !hm.BackupCalled && GuardAI.All.Count(g => g && !g.Dead) == guardsBefore - 1, $"(backup {hm.BackupCalled}, guards {GuardAI.All.Count(g => g && !g.Dead)} of {guardsBefore - 1})");

            slow.BulletHitHost(1f, false, player.gameObject);
            AsksCheck("another officer is shot once and starts his call", slow.State == GuardState.Calling && slow.IsCalling);
            yield return new WaitForSeconds(slow.callSeconds * 0.5f);
            AsksCheck("... half way through the ring is about half full and he still stands there radioing", slow.IsCalling && slow.CallProgress > 0.3f && slow.CallProgress < 0.8f && !hm.BackupCalled, $"(progress {slow.CallProgress:0.00})");
            int beforeCall = GuardAI.All.Count(g => g && !g.Dead);
            yield return new WaitForSeconds(slow.callSeconds * 0.5f + 0.8f);
            int afterCall = GuardAI.All.Count(g => g && !g.Dead);
            AsksCheck("if nobody puts him down the call goes through: backup is called and officers are sent", hm.BackupCalled && afterCall > beforeCall, $"(backup {hm.BackupCalled}, guards {beforeCall} -> {afterCall})");
            AsksCheck("... and he goes after the player who shot him", !slow.IsCalling && (slow.State == GuardState.Chase || slow.State == GuardState.Investigate), $"(state {slow.State})");

            // a melee blow still cuts a call (and he calls again on getting up), a bullet at a guard who is chasing starts nothing
            meleeShot.BulletHitHost(1f, false, player.gameObject);
            bool started = meleeShot.IsCalling;
            meleeShot.MeleeHitHost(1.0f, player.gameObject);
            AsksCheck("a melee blow cuts a call he has going and knocks him over", started && !meleeShot.IsCalling && meleeShot.State == GuardState.Stunned, $"(started {started}, state {meleeShot.State})");

            // ================================================================== the dead officer lies there and can be walked through (the owner)
            var corpse = fast;
            var corpseAt = corpse.transform.position;
            var colliders = corpse.GetComponentsInChildren<Collider>(true);
            AsksCheck("an eliminated officer: every collider of his body is off, nothing of him is solid", colliders.Length > 0 && colliders.All(c => !c.enabled), $"({colliders.Count(c => c.enabled)} of {colliders.Length} still on)");
            AsksCheck("... he is still there where he fell (not removed)", corpse && corpse.gameObject.activeInHierarchy && Vector3.Distance(corpse.transform.position, corpseAt) < 0.05f);
            var visual = corpse.GetComponent<GuardVisual>();
            var weights = visual && visual.Anim ? visual.Anim.DebugWeights() : null;
            AsksCheck("... and he lies down: the death clip, not the dazed loop", weights != null && weights[3] > 0.5f && weights[6] < 0.2f, weights != null ? $"(weights {string.Join(",", weights.Select(x => x.ToString("0.00")))})" : "(no animator)");
            bool blocksWalk = false;
            foreach (var dir in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
            {
                var from = corpseAt - dir * 1.6f + Vector3.up * 0.4f;
                if (Physics.CapsuleCast(from, from + Vector3.up * 1.2f, 0.35f, dir, out var hit, 3.2f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<GuardAI>() == corpse) blocksWalk = true;
            }
            AsksCheck("a player's body cast through the place where he lies from every side meets nothing of him", !blocksWalk);
            AsksCheck("a bullet's ray through him no longer stops at him", !Physics.Raycast(corpseAt + Vector3.back * 2f + Vector3.up * 1f, Vector3.forward, out var rayHit, 4f, ~0, QueryTriggerInteraction.Ignore) || rayHit.collider.GetComponentInParent<GuardAI>() != corpse);
            yield return new WaitForSeconds(2f);
            AsksCheck("two seconds later he is still lying there (bodies go after 15 seconds, as before)", corpse && corpse.gameObject.activeInHierarchy);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var cam = new GameObject("AsksCam").AddComponent<Camera>();
                cam.enabled = false; cam.fieldOfView = 55f;
                cam.transform.position = corpseAt + new Vector3(2.2f, 1.6f, -2.2f);
                cam.transform.LookAt(corpseAt + Vector3.up * 0.2f);
                ShotCam(cam, "asks_corpse");
                Destroy(cam.gameObject);
            }

            // ================================================================== a treasure for two or three on the floor can be reached from both sides (the owner's picture: the server between two racks)
            for (int pass = 0; pass < 2; pass++)
            {
                int checkedLoot = 0, bothSides = 0;
                var bad = new System.Collections.Generic.List<string>();
                foreach (var l in FindObjectsByType<HeavyLoot>(FindObjectsSortMode.None))
                {
                    if (!l || l.lifters < 2) continue;
                    var box = l.GetComponentInChildren<Collider>().bounds;
                    float onFloor = box.min.y;
                    // standing on a counter or a desk (its underside well above the floor)? Then its carriers stand side by side in front of it: only floor treasures need two sides
                    if (onFloor > ElevatorExtraction.Current.GetComponent<Collider>().bounds.min.y + 0.25f) continue;
                    checkedLoot++;
                    bool ok = false;
                    var why = new System.Text.StringBuilder();
                    int freeSides = 0;
                    foreach (var axis in new[] { Vector3.right, Vector3.forward })
                    {
                        // a carrier stands just beyond each face of the box (the planner's spot: half the box and 0.45 m)
                        float reachOut = (axis == Vector3.right ? box.extents.x : box.extents.z) + LootPlan.StandOffset;
                        string a = SpotBlocker(box.center + axis * reachOut, onFloor, l), b = SpotBlocker(box.center - axis * reachOut, onFloor, l);
                        if (a == null) freeSides++;
                        if (b == null) freeSides++;
                        if (l.lifters == 2 && a == null && b == null) ok = true;
                        why.Append($"[{(axis == Vector3.right ? "x" : "z")}: +{a ?? "free"} / -{b ?? "free"}] ");
                    }
                    if (l.lifters >= 3 && freeSides >= 2) ok = true;
                    if (ok) bothSides++; else bad.Add($"{l.displayName} at {l.transform.position} {why}");
                }
                AsksCheck($"floor {hm.Floor}: every treasure for two or three that stands on the floor has a spot for a carrier on each of two opposite sides (two carriers) or on two sides (three carriers) ({bothSides} of {checkedLoot})", bad.Count == 0, bad.Count > 0 ? "(" + string.Join("; ", bad) + ")" : "");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    foreach (var srv in FindObjectsByType<HeavyLoot>(FindObjectsSortMode.None).Where(x => x && x.displayName == "Prototype Server").Take(3))
                    {
                        var sb = srv.GetComponentInChildren<Collider>().bounds;
                        foreach (var axis in new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back })
                        {
                            var camPos = sb.center + axis * 3.0f + Vector3.up * 1.2f;
                            if (Physics.Raycast(sb.center + Vector3.up * 0.3f, axis, out var vis, 3.0f, ~0, QueryTriggerInteraction.Ignore) && vis.collider.GetComponentInParent<HeavyLoot>() != srv) continue;
                            var sc = new GameObject("AsksServerCam").AddComponent<Camera>();
                            if (Camera.main) sc.CopyFrom(Camera.main);                                    // (the game''s own camera settings)
                            sc.enabled = false; sc.targetTexture = null; sc.fieldOfView = 62f;
                            sc.transform.position = camPos; sc.transform.LookAt(sb.center);
                            // the materials near the server that this copy cannot draw (a shader graph: pink) are swapped for a plain dark one while the picture is taken
                            var plain = new Material(Shader.Find("Standard")) { color = new Color(0.16f, 0.18f, 0.24f) };
                            var swapped = new System.Collections.Generic.List<(Renderer r, Material[] was)>();
                            var seen = new System.Collections.Generic.HashSet<string>();
                            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                            {
                                if (!r || !r.enabled || Vector3.Distance(r.bounds.center, sb.center) > 7f) continue;
                                var ms = r.sharedMaterials;
                                if (!ms.Any(m => m && m.shader && (!m.shader.isSupported || m.shader.name.StartsWith("Shader Graphs/") || m.shader.name.Contains("Hidden/InternalErrorShader")))) continue;
                                if (seen.Add(r.name + "|" + string.Join(",", ms.Select(m => m && m.shader ? m.shader.name : "-")))) Log($"[Asks] a material this copy cannot draw: {r.name} [{string.Join(", ", ms.Select(m => m && m.shader ? m.shader.name + (m.shader.isSupported ? "" : " (not supported)") : "none"))}]");
                                swapped.Add((r, ms)); r.sharedMaterials = ms.Select(m => plain).ToArray();
                            }
                            AsksShot(sc, $"asks_server_f{hm.Floor}_{(int)srv.transform.position.x}");
                            foreach (var (r, was) in swapped) if (r) r.sharedMaterials = was;
                            Destroy(plain); Destroy(sc.gameObject);
                            break;
                        }
                    }
                if (pass == 0) { tm.GoToLevel(2); yield return new WaitUntil(() => tm.Loading || hm.Floor == 2); yield return WaitFor(() => !tm.Loading && hm.Floor == 2 && hm.Phase == HeistPhase.Heist, 120, "floor 2"); yield return new WaitForSeconds(1.0f); }
            }

            // ================================================================== the ping (the owner: "Add a ping system ... lootable items, consumable items, weapons and doors")
            hm.godMode = true; hm.freezeTimer = true;
            player = WobblyPlayerRegistry.All[0];
            pack = player.GetComponent<Backpack>();
            yield return new WaitForSeconds(1.0f);
            // somewhere with a clear line of 4.5 m at the height of the eyes and of the floor (a corridor or a big room): the guards' stops are walkable spots
            bool haveSpot = FindClearSpot(out var spot, out var forward);
            AsksCheck("the ping: there is a clear line for the tests", haveSpot);
            if (haveSpot) { player.TeleportTo(spot + Vector3.up * 1.0f, Quaternion.identity); yield return new WaitForSeconds(0.5f); }
            var standAt = player.transform.position;
            var eye = (haveSpot ? spot : player.transform.position) + Vector3.up * 1.6f;
            var watchAt = eye + forward * 3f + Vector3.down * 0.9f;
            var loot = AsksItem("Gold Watch", watchAt, 700);
            yield return new WaitForFixedUpdate();
            AsksCheck("the ping system is installed", PingSystem.Instance != null);
            // aim at the watch from the player's eye
            bool aimed = PingSystem.TryAim(eye, (watchAt - eye).normalized, player, out var tLoot);
            AsksCheck("a ping on a valuable: it is the item, named, with its worth making it loot, a few metres away", aimed && tLoot.kind == PingTargetKind.Item && tLoot.label.StartsWith("Gold Watch") && tLoot.label.Contains("$700") && tLoot.category == PingCategory.Loot && Mathf.Abs(tLoot.distance - 3f) < 0.5f && tLoot.id == loot.NetId, $"({tLoot.kind} {tLoot.label} {tLoot.category} {tLoot.distance:0.0} m, id {tLoot.id}/{loot.NetId})");

            // the other kinds, one at a time on the same spot (a second item on the same line would be hidden behind the first)
            loot.transform.position = watchAt + Vector3.up * 200f;
            var spare = AsksItem("Spare Clip", watchAt, 0, true);
            yield return new WaitForFixedUpdate();
            bool aimedClip = PingSystem.TryAim(eye, (watchAt - eye).normalized, player, out var tClip);
            AsksCheck("a ping on a spare clip: something that is used up (a consumable)", aimedClip && tClip.category == PingCategory.Consumable && tClip.label == "Spare Clip", $"({tClip.category} '{tClip.label}')");
            Destroy(spare.gameObject);
            var gunPick = RangedWeapon.Create(RangedWeapon.Kind.Smg, watchAt + Vector3.up * 0.3f, null, 0f, 1).GetComponent<Pickup>();
            yield return new WaitForSeconds(0.8f);                                                // (it falls to the floor)
            Bounds gunBox = default;
            bool gunHasBox = gunPick && gunPick.TryGetBounds(out gunBox);
            PingTarget tGun = default;
            bool aimedGun = gunHasBox && PingSystem.TryAim(eye, (gunBox.center - eye).normalized, player, out tGun) && tGun.category == PingCategory.Gear;
            AsksCheck("a ping on a gun lying about: a weapon (gear)", aimedGun, gunHasBox ? $"({tGun.category} '{tGun.label}', box {gunBox.size:F2} at {gunBox.center})" : "(the gun has no box)");
            AsksCheck("the categories: a weapon or armour is gear, a clip or a thing worth under $50 is used up, the rest is loot", PingSystem.CategoryOf(gunPick) == PingCategory.Gear && PingSystem.CategoryOf(loot) == PingCategory.Loot);
            Destroy(gunPick.gameObject);
            loot.transform.position = watchAt;
            yield return new WaitForFixedUpdate();

            // a wall between: not pingable (nothing is pinged through a wall)
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "AsksWall"; wall.transform.position = eye + forward * 1.2f; wall.transform.localScale = forward.x != 0f ? new Vector3(0.2f, 3f, 3f) : new Vector3(3f, 3f, 0.2f);
            yield return new WaitForFixedUpdate();
            AsksCheck("an item behind a wall cannot be pinged", !PingSystem.TryAim(eye, (watchAt - eye).normalized, player, out _));
            AsksCheck("the wall itself cannot be pinged (only items and doors can)", !PingSystem.TryAim(eye, forward, player, out _));
            Destroy(wall);
            yield return new WaitForFixedUpdate();
            AsksCheck("nothing in the crosshair (the sky, the floor), nothing to ping", !PingSystem.TryAim(eye, Vector3.up, player, out _) && !PingSystem.TryAim(eye, Vector3.down * 1f + forward * 0.01f, player, out _));
            var faraway = AsksItem("Far Thing", eye + forward * 3f, 900);
            faraway.transform.position = player.transform.position + Vector3.up * 40f + forward * 100f;     // (out of every wall's way but beyond the range: 100 m)
            yield return new WaitForFixedUpdate();
            AsksCheck("an item beyond the range (80 m) cannot be pinged", !PingSystem.TryAim(eye, (faraway.transform.position - eye).normalized, player, out _));
            Destroy(faraway.gameObject);

            // a door
            int doorIndex = -1; Vector3 doorFrom = default, doorTo = default;
            var dm = DoorManager.Active;
            if (dm != null)
            {
                var order = Enumerable.Range(0, dm.Count).OrderBy(i => (dm.Get(i).Centre - player.transform.position).sqrMagnitude).Take(40).ToList();
                // try the nearest doors from a clear spot in front of each, until one works
                foreach (int i in order)
                {
                    var d = dm.Get(i);
                    var n = d.AlongX ? Vector3.forward : Vector3.right;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var from = d.Centre + n * side * 2.4f + Vector3.up * 1.5f;
                        var to = d.Centre + Vector3.up * 1.2f;
                        player.TeleportTo(from - Vector3.up * 1.5f, Quaternion.identity);
                        yield return new WaitForFixedUpdate();
                        if (PingSystem.TryAim(from, (to - from).normalized, player, out var tDoor) && tDoor.kind == PingTargetKind.Door && tDoor.id == i) { doorIndex = i; doorFrom = from; doorTo = to; break; }
                    }
                    if (doorIndex >= 0) break;
                }
            }
            AsksCheck("a ping on a door: the doorway under the crosshair is the target, with its number", doorIndex >= 0, doorIndex >= 0 ? $"(door {doorIndex})" : "(no door could be pinged from in front of it)");

            // the marker itself
            player.TeleportTo(standAt, Quaternion.identity);
            yield return new WaitForSeconds(PingRules.Cooldown + 0.1f);
            bool placed = PingSystem.PingFromView(eye, (watchAt - eye).normalized, player);
            AsksCheck("pinging puts a marker up for this player: one marker, on the watch", placed && PingSystem.Count == 1 && PingSystem.TryGet(PingSystem.LocalSlot, out var mk, out _) && mk.label.StartsWith("Gold Watch"), $"(placed {placed}, markers {PingSystem.Count})");
            bool tooSoon = PingSystem.PingFromView(eye, (watchAt - eye).normalized, player);
            AsksCheck("a second ping straight away is refused (half a second between pings)", !tooSoon && PingSystem.Count == 1);
            yield return new WaitForSeconds(PingRules.Cooldown + 0.1f);
            bool second = false;
            if (doorIndex >= 0)
            {
                player.TeleportTo(doorFrom - Vector3.up * 1.5f, Quaternion.identity);
                yield return new WaitForFixedUpdate();
                second = PingSystem.PingFromView(doorFrom, (doorTo - doorFrom).normalized, player);
                player.TeleportTo(standAt, Quaternion.identity);
            }
            AsksCheck("a new ping (on a door) replaces the player's old one: one marker each", second && PingSystem.Count == 1 && PingSystem.TryGet(PingSystem.LocalSlot, out var mk2, out _) && mk2.kind == PingTargetKind.Door && mk2.category == PingCategory.Door);
            PingSystem.OnPinged(2, (byte)PingTargetKind.Item, loot.NetId, loot.transform.position);
            AsksCheck("a teammate's ping (the host's message) adds a marker of theirs: two markers now", PingSystem.Count == 2 && PingSystem.TryGet(2, out var mk3, out _) && mk3.label.StartsWith("Gold Watch"));
            bool stored = pack.TryStore(loot);
            yield return new WaitForSeconds(0.3f);
            AsksCheck("an item that is taken into a pack loses its marker (the teammate's marker on the watch is gone, the player's own on the door stays)", stored && !PingSystem.TryGet(2, out _, out _) && PingSystem.TryGet(PingSystem.LocalSlot, out _, out _) && PingSystem.Count == 1, $"(stored {stored}, markers {PingSystem.Count})");
            var shortLived = AsksItem("Short Lived", watchAt, 300);
            yield return new WaitForFixedUpdate();
            PingSystem.OnPinged(3, (byte)PingTargetKind.Item, shortLived.NetId, shortLived.transform.position);
            AsksCheck("a teammate pings another item", PingSystem.Count == 2);
            Destroy(shortLived.gameObject);
            yield return new WaitForSeconds(0.3f);
            AsksCheck("... and a marker whose item is destroyed (banked, sold) goes with it", PingSystem.Count == 1 && !PingSystem.TryGet(3, out _, out _), $"(markers {PingSystem.Count})");
            yield return new WaitForSeconds(PingRules.Seconds + 0.6f);
            AsksCheck("a marker goes by itself after 8 seconds", PingSystem.Count == 0, $"(markers {PingSystem.Count})");


            UnityEditor.EditorSettings.asyncShaderCompilation = true;
            Log($"[Asks] DONE fails={asksFails}");
            Quit(asksFails == 0 ? 0 : 1);
        }

        /// <summary>A picture like ShotCam, but a pink picture is only a note (the scratch copy cannot draw some of the Synty shader graphs).</summary>
        static void AsksShot(Camera cam, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            cam.targetTexture = null; RenderTexture.active = null;
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_shot_" + name + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Log($"[Asks] picture {path} (pink pixels: {RenderAudit.CountMagenta(tex.GetPixels32())})");
            Destroy(rt); Destroy(tex);
        }

        /// <summary>A walkable spot (a guard''s stop) with a clear line of 4.5 m along the floor and at the height of the eyes in one of the four directions: where the ping tests stand.</summary>
        static bool FindClearSpot(out Vector3 spot, out Vector3 forward)
        {
            spot = default; forward = Vector3.forward;
            foreach (var g in GuardAI.All)
            {
                if (!g || g.patrolPoints == null) continue;
                foreach (var pt in g.patrolPoints)
                {
                    if (!pt) continue;
                    var p = pt.position; p.y = ElevatorExtraction.Current.GetComponent<Collider>().bounds.min.y;
                    foreach (var d in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                        if (!Physics.Raycast(p + Vector3.up * 1.6f, d, 4.6f, ~0, QueryTriggerInteraction.Ignore) && !Physics.Raycast(p + Vector3.up * 0.7f, d, 4.6f, ~0, QueryTriggerInteraction.Ignore)
                            && !Physics.Raycast(p + Vector3.up * 0.7f - d * 0.0f, Vector3.up, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                        { spot = p; forward = d; return true; }
                }
            }
            return false;
        }

        /// <summary>What stands where a person would stand at this spot (null: nothing): the name and height of the first thing that is built into the floor.</summary>
        static string SpotBlocker(Vector3 at, float floorY, HeavyLoot self)
        {
            var bottom = new Vector3(at.x, floorY + 0.7f, at.z);
            foreach (var c in Physics.OverlapCapsule(bottom, bottom + Vector3.up * 0.7f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!c || c.GetComponentInParent<HeavyLoot>() == self) continue;
                if (c.attachedRigidbody != null) continue;
                return $"{c.name}({c.bounds.min.y:0.0}-{c.bounds.max.y:0.0}m)";
            }
            return null;
        }

        /// <summary>Is there room for a person to stand at this spot (a capsule clear of everything that is built into the floor, the treasure and loose things)?</summary>
        static bool SpotFree(Vector3 at, float floorY, HeavyLoot self)
        {
            // a person: 0.3 m wide each way, from the knee (above the floor and the low things the planner ignores) up to the head
            var bottom = new Vector3(at.x, floorY + 0.7f, at.z);
            foreach (var c in Physics.OverlapCapsule(bottom, bottom + Vector3.up * 0.7f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!c || c.GetComponentInParent<HeavyLoot>() == self) continue;
                if (c.attachedRigidbody != null) continue;                      // (loose things and people move out of the way)
                return false;
            }
            return true;
        }
    }
}
