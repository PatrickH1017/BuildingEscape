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
            Object.FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None).Where(r => r && r.GetComponent<Pickup>()).Select(r => r.GetComponent<Pickup>()).ToList();

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

            foreach (int floor in new[] { 1, 2, 3, 4, 5, 6 })
            {
                yield return LoadLevel(hm, floor);
                string f = $"floor {floor}:";
                var pews = PewPickups();
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

                var guards = GuardAI.All.Where(g => g && !g.Dead && !g.IsPuppet).ToList();
                bool armed = floor >= ArmedGuards.DefaultFirstArmedFloor;
                PewLootCheck($"{f} {(armed ? "every guard carries a pew" : "nobody carries a pew")}", guards.Count > 0 && guards.All(g => g.Armed == armed), $"({guards.Count(g => g.Armed)} of {guards.Count})");
                var duty = GuardAI.All.FirstOrDefault(g => g && g.name == "Guard_Outpost");
                PewLootCheck($"{f} the guard on duty in the outpost {(armed ? "is armed too" : "is not armed")}", duty != null && duty.Armed == armed);

                if (floor == OutpostLoot.PewFloor)
                {
                    // ---- the backup is armed as well
                    int before = guards.Count;
                    hm.CallBackup(null);
                    yield return new WaitForSeconds(3.5f);
                    var after = GuardAI.All.Where(g => g && !g.Dead && !g.IsPuppet).ToList();
                    PewLootCheck($"{f} the backup that comes is armed", after.Count > before && after.All(g => g.Armed), $"({before} -> {after.Count}, armed {after.Count(g => g.Armed)})");

                    // ---- the pew can be picked up and ends up in the hand
                    var pew = pews.Count == 1 ? pews[0] : null;
                    if (pew != null)
                    {
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

            tm.GoToLevel(OutpostLoot.PewFloor);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == OutpostLoot.PewFloor);
            yield return FloorReady(OutpostLoot.PewFloor, 120);
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(3f);

            var gen = FindFirstObjectByType<FloorGenerator>();
            var pews = PewPickups();
            GunNetCheck("host: exactly one pew lies on floor 4, in the outpost", pews.Count == 1 && gen.TryGetOutpostRoom(out var room) && room.Grow(0.4f).Contains(pews[0].transform.position.x, pews[0].transform.position.z), $"({pews.Count})");
            var pew = pews.Count == 1 ? pews[0] : null;
            var duty = GuardAI.All.FirstOrDefault(g => g && g.name == "Guard_Outpost");
            var live = GuardAI.All.Where(g => g && !g.Dead && !g.IsPuppet).ToList();
            GunNetCheck("host: every guard carries a pew (no test switch: the floor rule)", live.Count > 0 && live.All(g => g.Armed) && duty != null, $"({live.Count(g => g.Armed)} of {live.Count})");
            if (pew == null || duty == null) { Log($"[GunNet] host DONE fails={gunNetFails}"); Quit(1); yield break; }
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
            GunNetCheck("host: it accepted both shots of the client", RunSync.Instance.GunShotsAccepted - accepted0 == 2, $"({RunSync.Instance.GunShotsAccepted - accepted0})");
            GunNetCheck("host: the guard on duty is down for good (two bullets, five hits each)", duty.Dead && duty.Hits >= 10, $"(dead {duty.Dead}, hits {duty.Hits})");
            GunNetCheck("host: the client's two shots were shown on this machine", cgc != null && cgc.ShotsSeen == 2, $"({(cgc != null ? cgc.ShotsSeen : -1)})");
            Signal("h_shot_ok");

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
                GunNetCheck("client: both bullets left the gun", f1 && f2 && gc.ShotsFired == 2 && gc.Gun.Magazine.Rounds == 8, $"({f1}, {f2}, fired {gc.ShotsFired})");
            }
            yield return new WaitForSeconds(0.5f);
            Signal("c_shot");
            yield return WaitSig("h_shot_ok", 60);
            yield return new WaitForSeconds(1.0f);
            GunNetCheck("client: the guard is down here too (the host's flag in the snapshot)", duty != null && duty.Dead, duty != null ? $"(dead {duty.Dead})" : "");

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
