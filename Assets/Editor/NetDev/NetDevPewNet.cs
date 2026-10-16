using System.Collections;
using System.Linq;
using BuildingEscape.Net;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Two processes over the local transport: the host's guards carry pews, and the client has to see it - the guns in their hands,
    /// the shots (flash, tracer, sound), the guns going away again, and the backup guards being armed too.
    /// </summary>
    public partial class NetDevRunner
    {
        int pewNetFails;

        void NetPewCheck(string name, bool ok, string detail = "")
        {
            if (!ok) pewNetFails++;
            Log($"[PewNet] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator ArmedHostScenario()
        {
            var net = NetSession.Instance;
            ClearSignals();
            ArmedGuards.ForceForTests = true;                       // the guards of this run are armed when they are made
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
            yield return WaitSig("c_ready0", 150);                  // (the client has built the basement too: it must not be mid-build when the floor changes)

            tm.GoToLevel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return FloorReady(1, 120);
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(3f);
            var guards = GuardAI.All.Where(g => g && !g.IsPuppet && !g.Dead).ToList();
            NetPewCheck("host: every guard of the floor carries a pew", guards.Count >= 2 && guards.All(g => g.Armed), $"({guards.Count(g => g.Armed)} of {guards.Count})");
            NetPewCheck("host: and holds it in his hand", guards.All(g => g.Gun != null && g.Gun.HasModel), $"({guards.Count(g => g.Gun != null && g.Gun.HasModel)})");
            Signal("h_floor1", guards.Count.ToString());
            yield return WaitSig("c_sees_armed", 90);

            // one of them shoots at the host's player
            var me = NetPlayer.Local.GetComponent<WobblyPlayer>();
            var shooter = guards[0];
            foreach (var g in guards) g.sightRange = 0.1f;
            shooter.sightRange = 14f;
            var gg = shooter.Gun;
            gg.baseSpreadDegrees = 0.3f; gg.spreadPerMetre = 0f;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            var fState = typeof(GuardAI).GetField("state", flags);
            var fTarget = typeof(GuardAI).GetField("target", flags);
            var fUnseen = typeof(GuardAI).GetField("unseenFor", flags);
            Vector3 spot = default;
            bool placed = OpenSpot(shooter.transform.position, 8f, out spot, false) || OpenSpot(shooter.transform.position, 6f, out spot, false);
            NetPewCheck("host: found a clear spot 6-8 m from the shooter", placed);
            if (placed)
            {
                Teleport(spot + Vector3.up * 1.1f);
                yield return new WaitForSeconds(0.8f);
                var to = me.transform.position - shooter.transform.position; to.y = 0f;
                shooter.transform.rotation = Quaternion.LookRotation(to.normalized);
                fTarget.SetValue(shooter, me); fUnseen.SetValue(shooter, 0f); fState.SetValue(shooter, GuardState.Chase);
                shooter.GetComponent<NavMeshAgent>().speed = shooter.chaseSpeed;
                float t0 = Time.time;
                while (gg.ShotsFired < 4 && Time.time - t0 < 12f) yield return null;
                NetPewCheck("host: the guard shoots four times at the player", gg.ShotsFired >= 4, $"({gg.ShotsFired} after {Time.time - t0:0.0} s)");
                shooter.Stun(60f, null);                            // stop him so the count is final
                yield return new WaitForSeconds(1.5f);
                Signal("h_shots", $"{shooter.NetId} {gg.ShotsFired}");
                yield return WaitSig("c_saw_shots", 60);
            }
            else { Signal("h_shots", $"{shooter.NetId} 0"); yield return WaitSig("c_saw_shots", 60); }

            // the guns are taken away again
            ArmedGuards.ForceForTests = false;
            int taken = ArmedGuards.DisarmAll();
            NetPewCheck("host: disarming takes the pew from every guard", taken >= guards.Count && GuardAI.All.All(g => !g || !g.Armed), $"({taken} disarmed)");
            Signal("h_disarmed");
            yield return WaitSig("c_sees_unarmed", 60);

            // backup on a floor where the rule says armed: the new guards carry pews, here and there
            ArmedGuards.ForceForTests = true;
            var known = GuardAI.All.Where(g => g).ToList();
            int before = known.Count;
            hm.CallBackup(guards.FirstOrDefault(g => g && !g.Dead));
            yield return new WaitForSeconds(3f);
            var fresh = GuardAI.All.Where(g => g && !g.IsPuppet && !g.Dead && !known.Contains(g)).ToList();
            NetPewCheck("host: backup brings new guards", GuardAI.All.Count > before && fresh.Count > 0, $"({before} -> {GuardAI.All.Count})");
            NetPewCheck("host: and they are armed", fresh.Count > 0 && fresh.All(g => g.Armed), $"({fresh.Count(g => g.Armed)} of {fresh.Count})");
            Signal("h_backup", fresh.Count.ToString());
            yield return WaitSig("c_done", 60);
            Log($"[PewNet] host DONE fails={pewNetFails}");
            Log("DONE armedhost");
            Quit(pewNetFails == 0 ? 0 : 1);
        }

        IEnumerator ArmedClientScenario()
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
            yield return WaitSig("h_floor1", 150);
            var hm = HeistManager.Instance;
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == 1);
            yield return FloorReady(1, 120);
            yield return WaitFor(() => RunSync.SyncChecked, 60, "floor checked against the host");
            yield return new WaitForSeconds(3f);

            int n = int.Parse(ReadSig("h_floor1").Trim());
            var guards = GuardAI.All.Where(g => g && g.IsPuppet && !g.Dead).ToList();
            NetPewCheck("client: sees the same number of guards as the host", guards.Count == n, $"({guards.Count} vs {n})");
            NetPewCheck("client: every one of them carries a pew (from the snapshot flag)", guards.Count > 0 && guards.All(g => g.Armed), $"({guards.Count(g => g.Armed)} of {guards.Count})");
            NetPewCheck("client: and holds it in his hand", guards.Count > 0 && guards.All(g => g.Gun != null && g.Gun.HasModel), $"({guards.Count(g => g.Gun != null && g.Gun.HasModel)})");
            NetPewCheck("client: nothing of the host's gun logic runs here (the magazine is never used)", guards.All(g => g.Gun == null || g.Gun.ShotsFired == 0));
            Signal("c_sees_armed");

            // the shots the host's guard fires
            yield return WaitSig("h_shots", 120);
            var parts = ReadSig("h_shots").Trim().Split(' ');
            int sid = int.Parse(parts[0]), count = int.Parse(parts[1]);
            var sg = NetIds.Get<GuardAI>(sid);
            NetPewCheck("client: the shooting guard is there with his gun", sg != null && sg.Gun != null);
            if (sg != null && sg.Gun != null)
            {
                NetPewCheck("client: saw every shot the host announced", sg.Gun.ShotsSeen == count && count >= 4, $"({sg.Gun.ShotsSeen} seen, host fired {count})");
                NetPewCheck("client: and played their flash and sound", GunFx.Get().FlashesShown >= count && GunSounds.ShotsPlayed >= count, $"(flashes {GunFx.Get().FlashesShown}, sounds {GunSounds.ShotsPlayed})");
            }
            Signal("c_saw_shots");

            yield return WaitSig("h_disarmed", 90);
            yield return new WaitForSeconds(3f);
            var after = GuardAI.All.Where(g => g && g.IsPuppet && !g.Dead).ToList();
            NetPewCheck("client: when the host disarms the guards their pews are gone here too", after.Count > 0 && after.All(g => !g.Armed), $"({after.Count(g => g.Armed)} still armed)");
            NetPewCheck("client: the models are gone from their hands", after.All(g => g.GetComponentsInChildren<Transform>(true).All(t => t.name != "Pew_Held")));
            var knownHere = new System.Collections.Generic.HashSet<int>();
            foreach (var g in GuardAI.All) if (g) knownHere.Add(g.NetId);
            Signal("c_sees_unarmed");

            yield return WaitSig("h_backup", 90);
            int freshCount = int.Parse(ReadSig("h_backup").Trim());
            yield return new WaitForSeconds(3f);
            var fresh = GuardAI.All.Where(g => g && g.IsPuppet && !g.Dead && !knownHere.Contains(g.NetId)).ToList();
            NetPewCheck("client: the backup guards are here and armed", fresh.Count >= freshCount && freshCount > 0 && fresh.All(g => g.Armed && g.Gun.HasModel), $"({fresh.Count} new, {fresh.Count(g => g.Armed)} armed, the host made {freshCount})");
            Signal("c_done");
            Log($"[PewNet] client DONE fails={pewNetFails}");
            Log("DONE armedclient");
            Quit(pewNetFails == 0 ? 0 : 1);
        }
    }
}
