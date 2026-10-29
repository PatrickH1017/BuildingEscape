using System.Collections;
using System.Linq;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// A client knocks a guard down with a crowbar over the network (the one thing the players must do to the guard on duty in an outpost before he
    /// alerts them): the client buys the crowbar at the docks, the team rides to floor 1, the client stands next to a patrolling guard and swings;
    /// the HOST decides (<c>RunSync.HostGuardHit</c>), so both machines must show the guard stunned a moment later, and a second hit must count.
    /// Run: <c>runmp.ps1 -Method MeleeNet -Roles host,c1</c> (qa_mp.ps1 'MeleeNet:host,c1'). Looks for "[MeleeNet] ... FAIL".
    /// </summary>
    public partial class NetDevRunner
    {
        int meleeFails;

        void MeleeCheck(string name, bool ok, string detail = "")
        {
            if (!ok) meleeFails++;
            Log($"[MeleeNet] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static GuardAI PatrolGuard() => GuardAI.All.FirstOrDefault(g => g && g.isActiveAndEnabled && !g.Dead && g.name != "Guard_Outpost" && !g.name.StartsWith("Backup"));

        IEnumerator MeleeHostScenario()
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
            hm.AddCash(2000);
            Signal("h_ready");
            yield return WaitSig("c_bought", 90);
            tm.RideTo(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return FloorReady(1, 150);
            hm.godMode = true; hm.freezeTimer = true;
            foreach (var gg in GuardAI.All) if (gg) { gg.sightRange = 0.1f; gg.hearingMultiplier = 0f; }       // (they only stand about)
            yield return new WaitForSeconds(2f);
            var g = PatrolGuard();
            MeleeCheck("host: there is a patrolling guard on floor 1", g != null);
            Signal("g_id", g != null ? g.NetId.ToString() : "-1");
            var clientPlayer = NetPlayer.All.FirstOrDefault(p => !p.IsOwner);
            yield return WaitSig("c_hit1", 90);
            yield return new WaitForSeconds(1.2f);
            float dist = clientPlayer && g ? Vector3.Distance(clientPlayer.transform.position, g.transform.position) : -1f;
            var cpack = clientPlayer ? clientPlayer.GetComponent<Backpack>() : null;
            MeleeCheck("host: the client's crowbar is in his hand here", cpack != null && cpack.Equipped != null && cpack.Equipped.GetComponent<MeleeWeapon>() != null);
            float crowbarHp = MeleeWeapon.Specs[(int)MeleeWeapon.Kind.Crowbar].guardDamage;           // (the owner, 2026-10-22: a blow takes the weapon's damage off the guard's 100 HP; before that it only knocked him over)
            MeleeCheck("host: the first blow knocked the guard over and took the crowbar's damage (15) off his 100 HP", g != null && g.State == GuardState.Stunned && !g.Dead && Mathf.Abs(g.Health - (g.MaxHealth - crowbarHp)) < 0.01f, $"(state {g?.State}, health {g?.Health} of {g?.MaxHealth}, the client was {dist:0.0} m away)");
            Signal("h_saw1");
            yield return WaitSig("c_hit2", 90);
            yield return new WaitForSeconds(1.0f);
            MeleeCheck("host: a second blow takes the same again (70 of 100 HP left) and he is still up to be hit", g != null && !g.Dead && Mathf.Abs(g.Health - (g.MaxHealth - 2f * crowbarHp)) < 0.01f, $"(health {g?.Health} of {g?.MaxHealth})");
            // ---- the client holds the right mouse button: the host sees his crowbar at the ready, and at the idle again when he lets go
            var cmc = clientPlayer ? clientPlayer.GetComponent<MeleeController>() : null;
            yield return WaitSig("c_ready_on", 90);
            yield return new WaitForSeconds(1.2f);
            MeleeCheck("host: the client holds the right mouse button: his crowbar is at the ready here (the flag came over)", cmc != null && cmc.Ready && cmc.ReadyShare > 0.95f, cmc != null ? $"(ready {cmc.Ready}, share {cmc.ReadyShare:0.00}, animated {cmc.Animated})" : "(no melee controller)");
            Signal("h_ready_on_ok");
            yield return WaitSig("c_ready_off", 90);
            yield return new WaitForSeconds(1.2f);
            MeleeCheck("host: the client lets go: his crowbar is at the idle here", cmc != null && !cmc.Ready && cmc.ReadyShare < 0.05f, cmc != null ? $"(ready {cmc.Ready}, share {cmc.ReadyShare:0.00})" : "(no melee controller)");
            Signal("h_ready_off_ok");
            Signal("h_done");
            yield return WaitSig("c_done", 60);
            Log($"[MeleeNet] host DONE fails={meleeFails}");
            Log("DONE meleenethost");
            Quit(meleeFails == 0 ? 0 : 1);
        }

        IEnumerator MeleeClientScenario()
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
            var pack = MyPack;
            var counter = Vendor.Current.GetComponent<Collider>();
            Teleport(counter.bounds.center + Vector3.up * 0.2f);       // (the host sells only to somebody at the counter)
            yield return new WaitForSeconds(1.5f);
            RunSync.Instance.RequestBuy(4);            // the crowbar
            yield return WaitFor(() => pack.Items.Any(i => i && i.GetComponent<MeleeWeapon>()), 30, "the crowbar in the pack");
            var crowbar = pack.Items.First(i => i && i.GetComponent<MeleeWeapon>());
            pack.SetEquipped(crowbar);
            yield return WaitFor(() => pack.Equipped == crowbar, 30, "the crowbar in the hand");
            Signal("c_bought");
            yield return FloorReady(1, 240);
            yield return WaitSig("g_id", 90);
            var g = NetIds.Get<GuardAI>(int.Parse(ReadSig("g_id")));
            MeleeCheck("client: the guard exists here as a puppet", g != null && g.IsPuppet);
            if (g != null)
            {
                Teleport(g.transform.position + new Vector3(1.4f, 0.5f, 0f));
                yield return new WaitForSeconds(1.0f);
                g = NetIds.Get<GuardAI>(g.NetId);
                g.WeaponHit(4f, Mine.gameObject);
                Signal("c_hit1");
                yield return WaitSig("h_saw1", 60);
                yield return new WaitForSeconds(0.6f);
                MeleeCheck("client: the guard is stunned here too", g.State == GuardState.Stunned, $"(state {g.State})");
                yield return new WaitForSeconds(0.5f);
                g.WeaponHit(4f, Mine.gameObject);
                Signal("c_hit2");
            }
            GunController.TestAim = true;
            Signal("c_ready_on");
            yield return WaitSig("h_ready_on_ok", 60);
            GunController.TestAim = false;
            Signal("c_ready_off");
            yield return WaitSig("h_ready_off_ok", 60);
            GunController.TestAim = null;
            yield return WaitSig("h_done", 60);
            Signal("c_done");
            Log($"[MeleeNet] client DONE fails={meleeFails}");
            Log("DONE meleenetclient");
            Quit(meleeFails == 0 ? 0 : 1);
        }
    }
}
