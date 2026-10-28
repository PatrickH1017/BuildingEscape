using System.Collections;
using System.Linq;
using BuildingEscape.Net;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The owner's request of 2026-10-16: "add the armed guards on wave 4 and the pistol should be in the outpost on wave 4 100% of the time".
    /// <c>PewLoot</c> (offline, Greybox, batch is fine) walks floors 1-6: a pew lies in the outpost of floor 4 and nowhere else, the guards of floors
    /// 4 and up carry one (the guard on duty and the backup included) and the guards of floors 1-3 do not; the pew can be picked up and ends up
    /// in the hand. <c>GunNet</c> (two processes: <c>runmp.ps1 -Method GunNet -Roles host,c1</c>) checks that it works for a team: both
    /// machines have the pew with the same id, the client's puppets of the guards are armed, the client picks the pew up (the host sees it in his pack
    /// and in his hand), fires it at the guard on duty (the host applies two bullets and the guard goes down on both machines), and a shot of the
    /// host is shown on the client.
    /// Run: Unity -batchmode -nographics -executeMethod BuildingEscape.EditorTools.NetDevHarness.PewLoot   (look for "[PewLoot] DONE fails=0").
    /// </summary>
    public partial class NetDevRunner
    {
        int pewLootFails, gunNetFails;

        void PewLootCheck(string name, bool ok, string detail = "")
        {
            if (!ok) pewLootFails++;
            Log($"[PewLoot] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        void GunNetCheck(string name, bool ok, string detail = "")
        {
            if (!ok) gunNetFails++;
            Log($"[GunNet] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static System.Collections.Generic.List<Pickup> PewPickups() =>
            Object.FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None).Where(r => r && r.kind == RangedWeapon.Kind.Pew && r.GetComponent<Pickup>() && !FloorSafe.HoldsGun(r.gameObject)).Select(r => r.GetComponent<Pickup>()).ToList();

        /// <summary>Every pew, also one that is out of the world (the one in the shut gun safe of floor 3, or one in a pack); the pistols that lie in the floor safes are not counted (the safe checks do that).</summary>
        static System.Collections.Generic.List<Pickup> PewPickupsAll() =>
            Object.FindObjectsByType<RangedWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r && r.kind == RangedWeapon.Kind.Pew && r.GetComponent<Pickup>() && !FloorSafe.HoldsGun(r.gameObject)).Select(r => r.GetComponent<Pickup>()).ToList();

        // ------------------------------------------------------------------ offline

        IEnumerator PewLootScenario()
        {
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var gc = player.GetComponent<GunController>();

            PewLootCheck("the docks: no pew lies about and nobody is armed", PewPickups().Count == 0 && !ArmedGuards.ShouldArm(0));

            bool gfx = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            foreach (int floor in new[] { 1, 2, 3, 4, 5, 6, 9, 10 })
            {
                yield return LoadLevel(hm, floor);
                string f = $"floor {floor}:";
                var pews = floor == OutpostLoot.PewFloor ? PewPickupsAll() : PewPickups();
                bool hasOutpost = gen.TryGetOutpostRoom(out var room);
                if (floor == OutpostLoot.PewFloor)
                {
                    PewLootCheck($"{f} exactly one pew lies on the floor", pews.Count == 1, $"({pews.Count})");
                    bool inRoom = pews.Count == 1 && hasOutpost && room.Grow(0.4f).Contains(pews[0].transform.position.x, pews[0].transform.position.z);
                    PewLootCheck($"{f} ... in the guard outpost", inRoom);
                    PewLootCheck($"{f} ... on a desk, not on the floor", pews.Count == 1 && pews[0].transform.position.y > 0.35f, pews.Count == 1 ? $"(y {pews[0].transform.position.y:0.00})" : "");
                    PewLootCheck($"{f} ... it is a pickup that is called Pew, weighs what the spec says and has a network id", pews.Count == 1 && pews[0].Label == "Pew"
                        && Mathf.Approximately(pews[0].weight, RangedWeapon.Specs[0].weight) && pews[0].NetId >= 0);
                }
                else PewLootCheck($"{f} no pew lies on the floor", pews.Count == 0, $"({pews.Count})");

                // ---- the sniper rifle lies in the outpost from floor 10 on, never in a safe (the owner, 2026-10-19: "The sniper and rocket launcher should be inside the outpost not in safes")
                var snipers = Object.FindObjectsByType<RangedWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r && r.kind == RangedWeapon.Kind.Sniper && r.GetComponent<Pickup>() && !FloorSafe.HoldsGun(r.gameObject)).Select(r => r.GetComponent<Pickup>()).ToList();
                if (floor >= OutpostLoot.SniperFloor)
                {
                    var sn = snipers.Count == 1 ? snipers[0] : null;
                    var snSpec = RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper];
                    bool snInRoom = sn != null && hasOutpost && room.Grow(0.4f).Contains(sn.transform.position.x, sn.transform.position.z);
                    PewLootCheck($"{f} exactly one sniper rifle lies on the floor: in the guard outpost, on a desk, in the open and in the world", sn != null && snInRoom && sn.transform.position.y > 0.35f && sn.gameObject.activeInHierarchy, sn != null ? $"({snipers.Count}, y {sn.transform.position.y:0.00})" : $"({snipers.Count})");
                    PewLootCheck($"{f} ... it is a pickup that is called as its spec says, weighs what the spec says and has a network id", sn != null && sn.Label == snSpec.name && Mathf.Approximately(sn.weight, snSpec.weight) && sn.NetId >= 0);
                    int snInSafes = Object.FindObjectsByType<RangedWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(r => r && r.kind == RangedWeapon.Kind.Sniper && FloorSafe.HoldsGun(r.gameObject));
                    PewLootCheck($"{f} ... and no safe holds one", snInSafes == 0, $"({snInSafes} in safes)");
                    if (gfx && sn != null)
                    {
                        // two pictures of it on its desk, from above at either end
                        foreach (var look in new[] { new Vector3(0.9f, 1.5f, 0.9f), new Vector3(-0.9f, 1.5f, -0.9f) })
                        {
                            var eyeS = new GameObject("SniperEye").AddComponent<Camera>();
                            eyeS.clearFlags = CameraClearFlags.SolidColor; eyeS.backgroundColor = new Color(0.2f, 0.22f, 0.26f); eyeS.fieldOfView = 55f; eyeS.enabled = false;
                            eyeS.transform.position = sn.transform.position + look;
                            eyeS.transform.LookAt(sn.transform.position);
                            var eyeLight = eyeS.gameObject.AddComponent<Light>();          // (the outpost is dim: a lamp on the camera)
                            eyeLight.type = LightType.Point; eyeLight.intensity = 2.5f; eyeLight.range = 7f;
                            yield return null;
                            ShotCam(eyeS, $"sniper_outpost_floor{floor}_{(look.x > 0f ? "a" : "b")}");
                            Destroy(eyeS.gameObject);
                        }
                    }
                    if (sn != null && pack != null && gc != null)
                    {
                        pack.TryStore(sn);
                        yield return new WaitForSeconds(0.6f);
                        PewLootCheck($"{f} picked up, the sniper rifle is in the pack (the pew taken on floor 3 is still in the hand)", pack.Contains(sn));
                        RunUnlocks.Reset();
                        int snUnlocked = RunUnlocks.CollectFrom(pack);
                        PewLootCheck($"{f} carried back to the docks it unlocks the sniper's ammunition for the party", snUnlocked >= 1 && RunUnlocks.Has(RangedWeapon.Kind.Sniper), $"({snUnlocked})");
                        RunUnlocks.Reset();
                    }
                }
                else PewLootCheck($"{f} no sniper rifle lies about", snipers.Count == 0, $"({snipers.Count})");

                var guards = GuardAI.All.Where(g => g && !g.Dead && !g.IsPuppet).ToList();
                bool armed = floor >= ArmedGuards.DefaultFirstArmedFloor;
                PewLootCheck($"{f} {(armed ? "some guards carry a gun, not all" : "nobody carries a gun")}", guards.Count > 0 && (armed ? guards.Any(g => g.Armed) && guards.Any(g => !g.Armed) : guards.All(g => !g.Armed)), $"({guards.Count(g => g.Armed)} of {guards.Count})");
                PewLootCheck($"{f} every gun is the kind of his loadout and every shield the one that goes with it", guards.All(g => g.Armed == g.Loadout.Armed && (!g.Armed || (g.Gun != null && g.Gun.kind == g.Loadout.Kind && g.Shield == GuardLoadouts.ShieldOf(g.Loadout.Kind))) && (g.Armed || g.Shield == ShieldTier.None)));
                var duty = GuardAI.All.FirstOrDefault(g => g && g.name == "Guard_Outpost");
                PewLootCheck($"{f} the guard on duty in the outpost is there", duty != null);

                if (floor == ArmedGuards.DefaultFirstArmedFloor)
                {
                    // ---- the backup is armed as well
                    int before = guards.Count;
                    hm.CallBackup(null);
                    yield return new WaitForSeconds(3.5f);
                    var after = GuardAI.All.Where(g => g && !g.Dead && !g.IsPuppet).ToList();
                    PewLootCheck($"{f} the backup that comes follows the same rule (a gun of his loadout's kind, or nothing)", after.Count > before && after.All(g => g.Armed == g.Loadout.Armed && (!g.Armed || g.Gun.kind == g.Loadout.Kind)), $"({before} -> {after.Count}, armed {after.Count(g => g.Armed)})");
                }

                if (floor == OutpostLoot.PewFloor)
                {
                    // ---- the pew can be picked up and ends up in the hand
                    var pew = pews.Count == 1 ? pews[0] : null;
                    if (pew != null)
                    {
                        PewLootCheck($"{f} the pew is out of the world while the gun safe is shut", !pew.gameObject.activeSelf && GunSafeProp.Active != null && !GunSafeProp.Active.IsOpen);
                        GunSafeProp.Active.HostResult(true);                                               // (the hack itself is played in the R7 scenario)
                        yield return new WaitForSeconds(0.8f);
                        PewLootCheck($"{f} the gun safe opened and the pew is in the world", GunSafeProp.Active.IsOpen && pew.gameObject.activeSelf);
                        pack.TryStore(pew);
                        yield return new WaitForSeconds(0.6f);
                        PewLootCheck($"{f} picked up, it is in the pack and in the hand", pack.Contains(pew) && pack.Equipped == pew);
                        PewLootCheck($"{f} the gun controller holds it, with a full magazine", gc != null && gc.Gun != null && gc.Gun.kind == RangedWeapon.Kind.Pew && gc.Gun.Magazine.Rounds == gc.Gun.Magazine.Capacity);
                        PewLootCheck($"{f} and the model is in the right hand", gc != null && gc.Held != null && gc.Held.Muzzle != null);
                    }
                }
            }

            // floors 7 to 13 are the same as 5 and 6: none has a pew (the stash is rolled from the run's seed and the floor only)
            Log($"[PewLoot] DONE fails={pewLootFails}");
            Quit(pewLootFails == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ two processes

        static Vector3 ParseV3(string x, string z) =>
            new Vector3(float.Parse(x, System.Globalization.CultureInfo.InvariantCulture), 0f, float.Parse(z, System.Globalization.CultureInfo.InvariantCulture));

        IEnumerator GunHostScenario()
        {
            var net = NetSession.Instance;
            ClearSignals();
            net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "host lobby");
            yield return WaitFor(() => net.Roster.Count >= 2, 200, "a client");
            net.StartRun();
            yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
            yield return FloorReady(0, 90);
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            hm.godMode = true; hm.freezeTimer = true;
            Signal("h_ready0");
            yield return WaitSig("c_ready0", 150);

            ArmedGuards.ForceForTests = true;      // (the guards are armed from floor 4 and the pew lies on floor 3: this test arms floor 3 too, to see the clients get the flag)
            tm.GoToLevel(OutpostLoot.PewFloor);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == OutpostLoot.PewFloor);
            yield return FloorReady(OutpostLoot.PewFloor, 120);
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(3f);

            var gen = FindFirstObjectByType<FloorGenerator>();
            var pews = PewPickupsAll();
            GunNetCheck($"host: exactly one pew lies on floor {OutpostLoot.PewFloor}, in the outpost", pews.Count == 1 && gen.TryGetOutpostRoom(out var room) && room.Grow(0.4f).Contains(pews[0].transform.position.x, pews[0].transform.position.z), $"({pews.Count})");
            var pew = pews.Count == 1 ? pews[0] : null;
            var duty = GuardAI.All.FirstOrDefault(g => g && g.name == "Guard_Outpost");
            var live = GuardAI.All.Where(g => g && !g.Dead && !g.IsPuppet).ToList();
            GunNetCheck("host: every guard carries a pew (the test switch arms this floor)", live.Count > 0 && live.All(g => g.Armed) && duty != null, $"({live.Count(g => g.Armed)} of {live.Count})");
            if (pew == null || duty == null) { Log($"[GunNet] host DONE fails={gunNetFails}"); Quit(1); yield break; }
            if (GunSafeProp.Active != null) GunSafeProp.Active.HostResult(true);          // (the pew lies in the gun safe since 2026-10-23: open it, as PewLoot does)
            yield return new WaitForSeconds(1.5f);
            GunNetCheck("host: the gun safe is open and the pew is in the world", GunSafeProp.Active != null && GunSafeProp.Active.IsOpen && pew.gameObject.activeSelf);
            var cult = System.Globalization.CultureInfo.InvariantCulture;
            Signal("h_floor4", string.Format(cult, "{0} {1} {2:0.###} {3:0.###} {4:0.###} {5:0.###}", pew.NetId, duty.NetId, duty.transform.position.x, duty.transform.position.z, pew.transform.position.x, pew.transform.position.z));
            yield return WaitSig("c_sees", 120);

            // ---- the client picks the pew up: the host sees it in his pack and in his hand
            yield return WaitSig("c_stored", 120);
            var clientPlayer = NetPlayer.All.FirstOrDefault(p => !p.IsOwner);
            var cpack = clientPlayer ? clientPlayer.GetComponent<Backpack>() : null;
            GunNetCheck("host: the client's pack holds the pew and has it equipped", cpack != null && cpack.Contains(pew) && cpack.Equipped == pew);
            yield return new WaitForSeconds(0.5f);
            var cgc = clientPlayer ? clientPlayer.GetComponent<GunController>() : null;
            GunNetCheck("host: the client's character has the pew in his hand here", cgc != null && cgc.Gun != null && cgc.Held != null && cgc.Held.Muzzle != null);
            Signal("h_stored_ok");

            // ---- the client fires two bullets at the guard on duty: the host applies them, the guard goes down, the shots are shown here
            int accepted0 = RunSync.Instance.GunShotsAccepted;
            yield return WaitSig("c_shot", 120);
            yield return new WaitForSeconds(0.8f);
            GunNetCheck("host: it accepted all four shots of the client", RunSync.Instance.GunShotsAccepted - accepted0 == 4, $"({RunSync.Instance.GunShotsAccepted - accepted0})");
            GunNetCheck("host: the guard on duty is down for good (four bullets, 25 HP each: light ammo)", duty.Dead && duty.Hits >= 10, $"(dead {duty.Dead}, hits {duty.Hits})");
            GunNetCheck("host: the client's four shots were shown on this machine", cgc != null && cgc.ShotsSeen == 4, $"({(cgc != null ? cgc.ShotsSeen : -1)})");
            Signal("h_shot_ok");

            // ---- the client aims: the host sees his gun come up to the sights, and go back to the hip when he lets go
            yield return WaitSig("c_aim_on", 90);
            yield return new WaitForSeconds(1.2f);
            GunNetCheck("host: the client holds the right mouse button: he is aiming here (the flag came over) and his gun is up at the sights", cgc != null && cgc.Aiming && cgc.Held != null && cgc.Held.RaiseNow > 0.95f, cgc != null && cgc.Held != null ? $"(aiming {cgc.Aiming}, raise {cgc.Held.RaiseNow:0.00}, stances {(cgc.Held.Clips != null && cgc.Held.Clips.Stances)})" : "(no gun)");
            Signal("h_aim_on_ok");
            yield return WaitSig("c_aim_off", 90);
            yield return new WaitForSeconds(1.2f);
            GunNetCheck("host: the client lets go: he is not aiming here and his gun is back at the hip", cgc != null && !cgc.Aiming && cgc.Held != null && Mathf.Abs(cgc.Held.RaiseNow - AimRules.HipRaise) < 0.04f, cgc != null && cgc.Held != null ? $"(aiming {cgc.Aiming}, raise {cgc.Held.RaiseNow:0.00})" : "(no gun)");
            Signal("h_aim_off_ok");

            // ---- the client drops it; the host picks it up and fires one shot: the client shows it
            yield return WaitSig("c_dropped", 90);
            var me = NetPlayer.Local;
            var wob = me.GetComponent<WobblyPlayer>();
            wob.TeleportTo(pew.transform.position + new Vector3(0.6f, 1.1f, 0f), wob.transform.rotation);
            yield return new WaitForSeconds(0.8f);
            RunSync.Instance.RequestStore(pew);
            yield return WaitFor(() => MyPack != null && MyPack.Equipped == pew, 15, "the host's pack holds the pew");
            yield return new WaitForSeconds(0.6f);
            var mgc = me.GetComponent<GunController>();
            GunNetCheck("host: it picked the pew up and holds it", mgc != null && mgc.Gun != null && mgc.Held != null);
            var cam = Camera.main; var rig = FindFirstObjectByType<CameraFollow>();
            if (rig) rig.enabled = false;
            if (cam) { cam.transform.position = wob.transform.position + Vector3.up * 1.2f; cam.transform.rotation = Quaternion.LookRotation(wob.transform.forward, Vector3.up); }
            yield return null;
            int accepted1 = RunSync.Instance.GunShotsAccepted;
            bool fired = mgc != null && mgc.Fire();
            yield return new WaitForSeconds(0.5f);
            GunNetCheck("host: its own shot left the gun and was accepted", fired && RunSync.Instance.GunShotsAccepted == accepted1 + 1, $"(fired {fired}, accepted +{RunSync.Instance.GunShotsAccepted - accepted1})");
            Signal("h_fired");
            yield return WaitSig("c_done", 90);
            Log($"[GunNet] host DONE fails={gunNetFails}");
            Log("DONE gunnethost");
            Quit(gunNetFails == 0 ? 0 : 1);
        }

        IEnumerator GunClientScenario()
        {
            var net = NetSession.Instance;
            float deadline = Time.realtimeSinceStartup + 150f;
            while (net.State != NetSessionState.InLobby && Time.realtimeSinceStartup < deadline)
            {
                net.JoinLocal("127.0.0.1");
                float t = 0;
                while (net.State == NetSessionState.Connecting && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
                if (net.State != NetSessionState.InLobby) yield return new WaitForSeconds(2f);
            }
            yield return FloorReady(0, 120);
            yield return WaitSig("h_ready0", 90);
            RunSync.SyncChecked = false;
            Signal("c_ready0");
            yield return WaitSig("h_floor4", 150);
            var hm = HeistManager.Instance;
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == OutpostLoot.PewFloor);
            yield return FloorReady(OutpostLoot.PewFloor, 120);
            yield return WaitFor(() => RunSync.SyncChecked, 60, "floor checked against the host");
            yield return new WaitForSeconds(3f);

            var p = ReadSig("h_floor4").Trim().Split(' ');
            int pewId = int.Parse(p[0]), dutyId = int.Parse(p[1]);
            var dutyPos = ParseV3(p[2], p[3]);
            var pew = NetIds.Get<Pickup>(pewId);
            GunNetCheck("client: the pew has the host's id here and is a pew", pew != null && pew.GetComponent<RangedWeapon>() != null && pew.Label == "Pew");
            var duty = NetIds.Get<GuardAI>(dutyId);
            GunNetCheck("client: the guard on duty is a puppet here", duty != null && duty.IsPuppet);
            float armedWait = 0f;
            while (armedWait < 6f && !GuardAI.All.Where(g => g && !g.Dead).All(g => g.Armed)) { armedWait += Time.deltaTime; yield return null; }
            var puppets = GuardAI.All.Where(g => g && !g.Dead).ToList();
            GunNetCheck("client: every guard puppet is armed (the host's flag in the snapshot)", puppets.Count > 0 && puppets.All(g => g.Armed), $"({puppets.Count(g => g.Armed)} of {puppets.Count})");
            Signal("c_sees");

            // ---- pick the pew up
            var me = NetPlayer.Local;
            var wob = me.GetComponent<WobblyPlayer>();
            var rb = wob.GetComponent<Rigidbody>();
            var pack = MyPack;
            yield return WaitFor(() => pew.gameObject.activeSelf, 20, "the gun safe open on this machine (the pew in the world)");
            wob.TeleportTo(pew.transform.position + new Vector3(0.6f, 1.1f, 0f), wob.transform.rotation);
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(1.2f);
            pack.TryStore(pew);
            yield return WaitFor(() => pack.Contains(pew) && pack.Equipped == pew, 15, "the pew in the pack");
            yield return new WaitForSeconds(0.6f);
            var gc = me.GetComponent<GunController>();
            GunNetCheck("client: the pew is in the pack, equipped, and the model is in his hand", pack.Contains(pew) && pack.Equipped == pew && gc != null && gc.Gun != null && gc.Held != null);
            Signal("c_stored");
            yield return WaitSig("h_stored_ok", 60);

            // ---- fire two bullets at the guard on duty, from inside the outpost
            var chest = dutyPos + Vector3.up * 1.0f;
            Vector3 spot = default;
            bool open = false;
            foreach (float d in new[] { 4.5f, 3.5f, 2.5f }) { open = OpenSpot(dutyPos, d, out spot, false); if (open) break; }
            GunNetCheck("client: found a clear spot a few metres from the guard", open);
            var cam = Camera.main; var rig = FindFirstObjectByType<CameraFollow>();
            if (rig) rig.enabled = false;
            if (open)
            {
                wob.TeleportTo(spot + Vector3.up * 1.1f, wob.transform.rotation);
                rb.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(0.7f);
                var d2 = dutyPos - wob.transform.position; d2.y = 0f;
                wob.TeleportTo(wob.transform.position, Quaternion.LookRotation(d2.normalized));
                rb.linearVelocity = Vector3.zero;
                var aimAt = duty != null ? duty.transform.position + Vector3.up * 0.3f : chest;
                AimCamera(cam, wob, aimAt);
                yield return null;
                AimCamera(cam, wob, aimAt);
                bool f1 = gc.Fire();
                yield return new WaitForSeconds(0.4f);
                AimCamera(cam, wob, duty != null ? duty.transform.position + Vector3.up * 0.3f : chest);
                bool f2 = gc.Fire();
                yield return new WaitForSeconds(0.4f);
                AimCamera(cam, wob, duty != null ? duty.transform.position + Vector3.up * 0.3f : chest);
                bool f3 = gc.Fire();
                yield return new WaitForSeconds(0.4f);
                AimCamera(cam, wob, duty != null ? duty.transform.position + Vector3.up * 0.3f : chest);
                bool f4 = gc.Fire();
                GunNetCheck("client: all four bullets left the gun", f1 && f2 && f3 && f4 && gc.ShotsFired == 4 && gc.Gun.Magazine.Rounds == 4, $"({f1}, {f2}, {f3}, {f4}, fired {gc.ShotsFired}, rounds {gc.Gun.Magazine.Rounds})");
            }
            yield return new WaitForSeconds(0.5f);
            Signal("c_shot");
            yield return WaitSig("h_shot_ok", 60);
            yield return new WaitForSeconds(0.8f);
            GunNetCheck("client: a hit marker flashed for the bullets that landed, and the last - the elimination - had its own marker and sound, which the host sent him (the owner, 2026-10-27)", HitFeedback.Count >= 4 && HitFeedback.Count <= 5 && HitFeedback.Last == HitKind.Kill && HitFeedback.LastSound == SoundId.HitKill, $"(markers {HitFeedback.Count}, last {HitFeedback.Last}, {HitFeedback.LastSound})");
            yield return new WaitForSeconds(1.0f);
            GunNetCheck("client: the guard is down here too (the host's flag in the snapshot)", duty != null && duty.Dead, duty != null ? $"(dead {duty.Dead})" : "");

            // ---- the hip and the sights: the host must see them (his checks are on the other side)
            GunController.TestAim = true;
            Signal("c_aim_on");
            yield return WaitSig("h_aim_on_ok", 60);
            GunController.TestAim = false;
            Signal("c_aim_off");
            yield return WaitSig("h_aim_off_ok", 60);
            GunController.TestAim = null;

            // ---- drop it for the host
            pack.Drop(pew);
            yield return WaitFor(() => !pack.Contains(pew), 15, "the pew dropped");
            Signal("c_dropped");
            yield return WaitSig("h_fired", 90);
            yield return new WaitForSeconds(0.8f);
            var hostPlayer = NetPlayer.All.FirstOrDefault(x => x.OwnerClientId == NetworkManager.ServerClientId);
            var hgc = hostPlayer ? hostPlayer.GetComponent<GunController>() : null;
            GunNetCheck("client: the host's shot was shown here, from the pew in his hand", hgc != null && hgc.Gun != null && hgc.ShotsSeen == 1, $"({(hgc != null ? hgc.ShotsSeen : -1)})");
            Signal("c_done");
            Log($"[GunNet] client DONE fails={gunNetFails}");
            Log("DONE gunnetclient");
            Quit(gunNetFails == 0 ? 0 : 1);
        }
    }
}
