using System.Collections;
using System.Linq;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The ping over the network (the owner, 2026-10-21): two machines on the loopback. The client pings an item on floor 1: his marker goes up at once, the host checks the message and every other machine shows the client's marker (slot, name, kind);
    /// then the host pings another item and the client sees the host's marker; a burst of pings from the client is cut by the host's limit (a burst of 3). Run: <c>runmp.ps1 -Method PingNet -Roles host,c1</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int pingNetFails;

        void PingNetCheck(string name, bool ok, string detail = "")
        {
            if (!ok) pingNetFails++;
            Log($"[PingNet] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>
        /// Two pickups worth money on this floor, the two nearest to the elevator where the players stand (the same two on every machine). Not the two with the lowest ids: the host takes a ping from a player only within
        /// <see cref="PingRules.MaxRange"/> + <see cref="PingRules.HostSlack"/> of his body, and a floor is longer than that, so an item at the far end is (rightly) not pinged from the elevator.
        /// </summary>
        static Pickup[] TwoValuables()
        {
            var tm = TowerManager.Instance;
            var from = tm != null ? tm.CurrentInfo.spawn : Vector3.zero;
            return NetIds.All.Select(kv => kv.Value as Pickup).Where(p => p && p.isActiveAndEnabled && p.Value >= PingRules.LootMinValue && !p.IsHeld && p.Loot == null)
                .OrderBy(p => (p.transform.position - from).sqrMagnitude).ThenBy(p => p.NetId).Take(2).ToArray();
        }

        IEnumerator PingNetHostScenario()
        {
            var net = NetSession.Instance;
            ClearSignals();
            net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "host lobby");
            yield return WaitFor(() => net.Roster.Count >= 2, 200, "a client");
            net.StartRun();
            yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
            yield return FloorReady(0, 90);
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            hm.godMode = true; hm.freezeTimer = true;
            Signal("h_ready");
            yield return WaitSig("c_ready", 90);
            tm.RideTo(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return FloorReady(1, 150);
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(2f);
            var two = TwoValuables();
            PingNetCheck("host: there are two valuables on the floor to ping", two.Length == 2);
            Signal("ids", two.Length == 2 ? $"{two[0].NetId},{two[1].NetId}" : "-1,-1");
            var client = NetPlayer.All.FirstOrDefault(p => !p.IsOwner);
            int clientSlot = client ? client.Slot : 1;

            // ---- the client pings the first one: the host shows his marker
            yield return WaitFor(() => PingSystem.TryGet(clientSlot, out _, out _), 25, "the client's ping");
            bool got = PingSystem.TryGet(clientSlot, out var t1, out float age1);
            PingNetCheck("host: the client's ping arrives here as his marker (an item, the right one, named)", got && t1.kind == PingTargetKind.Item && two.Length == 2 && t1.id == two[0].NetId && t1.label.StartsWith(two[0].Label), $"({t1.kind} id {t1.id}, '{t1.label}', {age1:0.0} s old)");
            Signal("h_saw1");

            // ---- the client's burst of six: the host lets three through
            yield return WaitSig("c_burst", 60);
            yield return new WaitForSeconds(1.0f);
            int burst = PingSystem.ShownFrom(clientSlot) - 1;                 // (the first ping was the one before)
            PingNetCheck("host: of the client's burst of six pings the host passed no more than the burst of three (the rest were cut by its rate limit)", burst >= 2 && burst <= 4, $"({burst} of the burst of 6 came through)");

            // ---- the host pings the second one: the client shows it
            yield return new WaitForSeconds(0.7f);
            if (two.Length == 2) PingSystem.Send(PingSystem.ItemTarget(two[1]));
            PingNetCheck("host: the host's own marker is up at once", PingSystem.TryGet(Mine.Slot, out var own, out _) && two.Length == 2 && own.id == two[1].NetId);
            Signal("h_pinged");
            yield return WaitSig("c_saw2", 40);
            PingNetCheck("host: the host's ping did not come back to the host as a second marker (own pings are shown once)", PingSystem.ShownFrom(Mine.Slot) == 1, $"({PingSystem.ShownFrom(Mine.Slot)} markers made for the host's slot)");
            Signal("h_done");
            yield return WaitSig("c_done", 60);
            Log($"[PingNet] host DONE fails={pingNetFails}");
            Log("DONE pingnethost");
            Quit(pingNetFails == 0 ? 0 : 1);
        }

        IEnumerator PingNetClientScenario()
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
            yield return WaitSig("h_ready", 120);
            Signal("c_ready");
            yield return FloorReady(1, 240);
            yield return WaitSig("ids", 90);
            var ids = ReadSig("ids").Split(',').Select(int.Parse).ToArray();
            var a = NetIds.Get<Pickup>(ids[0]); var b = NetIds.Get<Pickup>(ids[1]);
            PingNetCheck("client: the two valuables exist here under the same ids", a != null && b != null && ids[0] >= 0);
            if (a == null || b == null) { Signal("c_done"); Quit(1); yield break; }
            yield return new WaitForSeconds(1.0f);

            // ---- the client pings the first one
            PingSystem.Send(PingSystem.ItemTarget(a));
            PingNetCheck("client: his own marker is up at once", PingSystem.TryGet(Mine.Slot, out var mine1, out _) && mine1.id == ids[0]);
            yield return WaitSig("h_saw1", 40);

            // ---- a burst of six straight after each other (the host's limit lets three through)
            for (int i = 0; i < 6; i++) RunSync.Instance.RequestPing((byte)PingTargetKind.Item, (ushort)ids[1], b.transform.position);
            Signal("c_burst");

            // ---- the host pings the second one: it comes here as the host's marker (slot 0)
            yield return WaitSig("h_pinged", 60);
            yield return WaitFor(() => PingSystem.TryGet(0, out _, out _), 15, "the host's ping");
            bool got = PingSystem.TryGet(0, out var t2, out _);
            PingNetCheck("client: the host's ping arrives here as his marker (an item, the right one, named)", got && t2.kind == PingTargetKind.Item && t2.id == ids[1] && t2.label.StartsWith(b.Label), $"({t2.kind} id {t2.id}, '{t2.label}')");
            PingNetCheck("client: the client's own pings were shown once each here (the host's announcement of them did not make a second marker)", PingSystem.ShownFrom(Mine.Slot) == 1, $"({PingSystem.ShownFrom(Mine.Slot)} markers made for his slot)");
            Signal("c_saw2");
            yield return WaitSig("h_done", 60);
            Signal("c_done");
            Log($"[PingNet] client DONE fails={pingNetFails}");
            Log("DONE pingnetclient");
            Quit(pingNetFails == 0 ? 0 : 1);
        }
    }
}
