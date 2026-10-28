using System.Collections;
using System.Linq;
using System.Reflection;
using BuildingEscape.Net;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The owner's request of 2026-10-16: the game is a demo up to floor 4 (the elevator never goes higher) and, once floor 4 is finished, the
    /// vendor sells the pew pistol for nothing, so the team can try it on the dummy in the Loading Docks.
    /// <c>Demo</c> (offline, Greybox) climbs the real way (the elevator's own <c>RideTo</c>): floors 1 to 6 (the demo's last: TowerRules.DemoLastFloor), each finished and left for the docks;
    /// the elevator stops at floor 4 and at the docks afterwards; the shop has no pew until then, then one free pew per player, which is fired at
    /// the training dummy. <c>DemoNet</c> (two processes: <c>runmp.ps1 -Method DemoNet -Roles host,c1</c>) does the shopping and the shooting over the network:
    /// the client buys the free pew (the host spawns it, it is in his pack and in his hand on both machines), fires it at the dummy, and the host's
    /// health for the dummy (published to the client) goes down.
    /// Run: Unity -batchmode -nographics -executeMethod BuildingEscape.EditorTools.NetDevHarness.Demo   (look for "[Demo] DONE fails=0").
    /// </summary>
    public partial class NetDevRunner
    {
        int demoFails, demoNetFails;

        void DemoCheck(string name, bool ok, string detail = "")
        {
            if (!ok) demoFails++;
            Log($"[Demo] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        void DemoNetCheck(string name, bool ok, string detail = "")
        {
            if (!ok) demoNetFails++;
            Log($"[DemoNet] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static readonly BindingFlags NonPublicInst = BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>The vendor's own purchase rules (offline): the message the buyer gets.</summary>
        static string BuyFrom(Vendor v, HeistManager hm, Backpack pack, int index) =>
            (string)typeof(Vendor).GetMethod("BuyFor", NonPublicInst).Invoke(v, new object[] { hm, pack, index });

        /// <summary>Ride the service elevator the way the panel does, and wait until the floor is built.</summary>
        IEnumerator Ride(HeistManager hm, TowerManager tm, int level)
        {
            tm.RideTo(level);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == level);
            yield return WaitFor(() => !tm.Loading && hm.Floor == level, 90, $"level {level}");
            yield return new WaitForSeconds(1f);
        }

        // ------------------------------------------------------------------ offline

        IEnumerator DemoScenario()
        {
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var pack = player.GetComponent<Backpack>();
            var vendor = Vendor.Current;
            DemoCheck("the docks have a vendor and a training dummy", vendor != null && TrainingDummy.Current != null);
            DemoCheck("at the start the elevator would resume at floor 1 and the demo is not over", tm.ResumeLevel == 1 && !TowerRules.DemoOver(tm.ResumeLevel));
            string refused = vendor != null ? BuyFrom(vendor, hm, pack, Vendor.PewIndex) : "";
            DemoCheck("at the start the vendor will not sell the pew", !Vendor.PewForSale(tm.ResumeLevel) && refused.Contains("nothing like that") && !pack.Items.Any(i => i && i.GetComponent<RangedWeapon>()), $"(\"{refused}\")");

            // ---- floors 1 to the demo's last (6): finish each one and go down to the docks
            for (int floor = 1; floor <= TowerRules.DemoLastFloor; floor++)
            {
                yield return Ride(hm, tm, floor);
                DemoCheck($"floor {floor}: the elevator took the team there", hm.Floor == floor && hm.Phase == HeistPhase.Heist);
                hm.DebugCompleteFloor();
                DemoCheck($"floor {floor}: the quota is met", hm.Phase == HeistPhase.FloorComplete);
                int up = TowerRules.UpFrom(floor, true, tm.ResumeLevel);
                bool expectUp = !TowerRules.IsGroupEnd(floor) && floor < TowerRules.DemoLastFloor;          // (floors 3 and 6 end their group, floor 6 ends the demo)
                DemoCheck($"floor {floor}: from here the elevator goes {(expectUp ? "up to floor " + (floor + 1) : "nowhere but down")}", expectUp ? up == floor + 1 : up == -1, $"(up {up})");
                if (floor == TowerRules.DemoLastFloor)
                {
                    tm.RideTo(TowerRules.DemoLastFloor + 1);
                    yield return new WaitForSeconds(1.5f);
                    DemoCheck($"floor {floor}: the elevator refuses floor {floor + 1} (nothing is built, the team stays)", hm.Floor == floor && !tm.Loading);
                    tm.RideTo(13);
                    yield return new WaitForSeconds(0.5f);
                    DemoCheck($"floor {floor}: and the boss floor", hm.Floor == floor && !tm.Loading);
                }
                yield return Ride(hm, tm, 0);
                DemoCheck($"floor {floor}: back at the docks, the elevator would resume at floor {floor + 1}", hm.Floor == 0 && tm.ResumeLevel == floor + 1, $"(resume {tm.ResumeLevel})");
                if (floor < Vendor.PewAfterFloor)
                    DemoCheck($"floor {floor}: the shop still has no pew", !Vendor.PewForSale(tm.ResumeLevel) && BuyFrom(Vendor.Current, hm, pack, Vendor.PewIndex).Contains("nothing like that"));
                else
                    DemoCheck($"floor {floor} done: the shop has the pew now", Vendor.PewForSale(tm.ResumeLevel));
            }

            // ---- the demo is over: the docks have nothing to go up to, and the vendor has a free pew
            vendor = Vendor.Current;
            DemoCheck("floor 6 done: the demo is over", TowerRules.DemoOver(tm.ResumeLevel) && tm.ResumeLevel == TowerRules.DemoLastFloor + 1);
            DemoCheck("floor 6 done: from the docks the elevator goes nowhere", TowerRules.UpFrom(0, false, tm.ResumeLevel) == -1);
            tm.RideElevator();
            tm.RideTo(TowerRules.DemoLastFloor + 1);
            yield return new WaitForSeconds(1.5f);
            DemoCheck("floor 6 done: neither the elevator's own up nor a ride to floor 7 builds a floor", hm.Floor == 0 && hm.Phase == HeistPhase.Hub && !tm.Loading);

            DemoCheck("floor 6 done: the pew is for sale", Vendor.PewForSale(tm.ResumeLevel));
            hm.TrySpend(hm.Cash); hm.AddCash(Vendor.PewPrice - 1);                      // (one dollar short)
            string noClip = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("without a pew the vendor will not sell a clip", noClip.Contains("no pew") && hm.Cash == Vendor.PewPrice - 1, $"(\"{noClip}\")");
            string poor = BuyFrom(vendor, hm, pack, Vendor.PewIndex);
            DemoCheck("the pew is not free any more: one dollar short, the vendor refuses", poor.Contains("Not enough cash") && !pack.Items.Any(i => i && i.GetComponent<RangedWeapon>()) && hm.Cash == Vendor.PewPrice - 1, $"(\"{poor}\")");
            hm.AddCash(1 + Vendor.AmmoClipPrice * 2);                                   // (the pew's price, and two clips)
            int cash0 = hm.Cash;
            string bought = BuyFrom(vendor, hm, pack, Vendor.PewIndex);
            yield return new WaitForSeconds(0.8f);
            var pew = pack.Items.FirstOrDefault(i => i && i.GetComponent<RangedWeapon>());
            DemoCheck("the vendor sells it for its price: it is in the pack and in the hand", bought.Contains("Bought Pew Pistol") && pew != null && pack.Equipped == pew && cash0 - hm.Cash == Vendor.PewPrice, $"(\"{bought}\", cash {cash0} -> {hm.Cash})");
            var gc = player.GetComponent<GunController>();
            DemoCheck("the pew is in his hand with a full magazine of eight and two spare clips", gc != null && gc.Gun != null && gc.Held != null && gc.Gun.Magazine.Rounds == 8 && gc.Gun.Magazine.Reserve == 16);
            string again = BuyFrom(vendor, hm, pack, Vendor.PewIndex);
            DemoCheck("a second pew is refused (one each)", again.Contains("already") && pack.Items.Count(i => i && i.GetComponent<RangedWeapon>()) == 1, $"(\"{again}\")");
            gc.Gun.Magazine.SetReserve(88);                                                      // (a big reserve: there is no limit on the bullets a player carries any more; the owner, 2026-10-23)
            int cashClip = hm.Cash;
            string bigPack = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("a pew that already carries 88 bullets still takes eight more for the price: there is no limit on the bullets carried", bigPack.Contains("Bought bullets") && cashClip - hm.Cash == Vendor.AmmoClipPrice && gc.Gun.Magazine.Reserve == 96, $"(\"{bigPack}\", reserve {gc.Gun.Magazine.Reserve})");
            gc.Gun.Magazine.SetReserve(8);
            cashClip = hm.Cash;
            string clip = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("bullets cost their price and put eight rounds in the reserve of the pew", clip.Contains("Bought bullets") && cashClip - hm.Cash == Vendor.AmmoClipPrice && gc.Gun.Magazine.Reserve == 16, $"(\"{clip}\", cash {cashClip} -> {hm.Cash}, reserve {gc.Gun.Magazine.Reserve})");
            hm.TrySpend(hm.Cash);
            gc.Gun.Magazine.SetReserve(8);
            string brokeClip = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("with no cash left no clip is sold, and the reserve stays", brokeClip.Contains("Not enough cash") && gc.Gun.Magazine.Reserve == 8, $"(\"{brokeClip}\")");

            // ---- try it on the dummy
            var rig = FindFirstObjectByType<CameraFollow>();
            if (rig) rig.enabled = false;
            var dummy = TrainingDummy.Current;
            Vector3 spot = default;
            bool open = dummy != null && OpenSpot(dummy.transform.position, 7f, out spot, false);
            DemoCheck("a clear spot 7 m from the dummy", open);
            if (open && pew != null)
            {
                player.TeleportTo(spot + Vector3.up * 1.1f, player.transform.rotation);
                rb.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(0.7f);
                Face(player, dummy.transform.position, rb);
                var aimAt = dummy.transform.position + Vector3.up * 1.0f;
                AimCamera(Camera.main, player, aimAt);
                yield return null; yield return null;
                AimCamera(Camera.main, player, aimAt);
                float h0 = dummy.Health;
                bool fired = gc.Fire();
                yield return null;
                DemoCheck("a bullet takes 25 health off the dummy (light ammo, the owner, 2026-10-27)", fired && Mathf.Approximately(h0 - dummy.Health, 25f), $"({h0} -> {dummy.Health})");
                yield return new WaitForSeconds(0.35f);
                AimCamera(Camera.main, player, aimAt);
                gc.Fire();
                yield return null;
                DemoCheck("two bullets leave it standing (50 of 100 health)", !dummy.Down && Mathf.Approximately(dummy.Health, 50f), $"(health {dummy.Health})");
                for (int more = 0; more < 2; more++)
                {
                    yield return new WaitForSeconds(0.35f);
                    AimCamera(Camera.main, player, aimAt);
                    gc.Fire();
                    yield return null;
                }
                DemoCheck("four bullets bring it down", dummy.Down || dummy.Health <= 0f, $"(health {dummy.Health})");
            }
            if (rig) rig.enabled = true;

            Log($"[Demo] DONE fails={demoFails}");
            Quit(demoFails == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ two processes

        IEnumerator DemoHostScenario()
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
            DemoNetCheck("host: at the docks with the demo not over, the vendor has no pew for sale", !Vendor.PewForSale(tm.ResumeLevel));
            // the demo's last floor is finished (the same state the elevator leaves behind): the next floor to climb to is the one after it (7)
            typeof(TowerManager).GetProperty("ResumeLevel").SetValue(tm, TowerRules.DemoLastFloor + 1);
            yield return new WaitForSeconds(1.5f);
            DemoNetCheck("host: now the demo is over and the elevator has nothing to go up to", TowerRules.DemoOver(tm.ResumeLevel) && TowerRules.UpFrom(0, false, tm.ResumeLevel) == -1);
            Signal("h_ready");
            yield return WaitSig("c_ready", 150);

            hm.TrySpend(hm.Cash); hm.AddCash(Vendor.PewPrice + Vendor.AmmoClipPrice + 300);        // (what the team has to spend: the pew costs money now)
            Signal("h_cash");

            // ---- the client buys the pew
            yield return WaitSig("c_bought", 120);
            var clientPlayer = NetPlayer.All.FirstOrDefault(p => !p.IsOwner);
            var cpack = clientPlayer ? clientPlayer.GetComponent<Backpack>() : null;
            var pew = cpack != null ? cpack.Items.FirstOrDefault(i => i && i.GetComponent<RangedWeapon>()) : null;
            DemoNetCheck("host: the client's pack holds a pew and has it equipped", pew != null && cpack.Equipped == pew);
            DemoNetCheck("host: it cost the team the pew's price", hm.Cash == Vendor.AmmoClipPrice + 300, $"(cash {hm.Cash})");
            yield return new WaitForSeconds(0.5f);
            var cgc = clientPlayer ? clientPlayer.GetComponent<GunController>() : null;
            DemoNetCheck("host: the pew is in the client's hand here", cgc != null && cgc.Gun != null && cgc.Held != null);
            var dummy = TrainingDummy.Current;
            float h0 = dummy != null ? dummy.Health : -1f;
            int accepted0 = RunSync.Instance.GunShotsAccepted;
            Signal("h_bought_ok");

            // ---- the client buys a clip: the host takes the money and every machine adds the rounds to the reserve of his pew
            yield return WaitSig("c_clip", 120);
            DemoNetCheck("host: the clip cost its price and the client's pew has eight more spare rounds here too", hm.Cash == 300 && cgc != null && cgc.Gun != null && cgc.Gun.Magazine.Reserve == 24, $"(cash {hm.Cash}, reserve {(cgc != null && cgc.Gun != null ? cgc.Gun.Magazine.Reserve : -1)})");
            Signal("h_clip_ok");

            // ---- the client fires two bullets at the dummy
            yield return WaitSig("c_shot", 120);
            yield return new WaitForSeconds(0.8f);
            DemoNetCheck("host: it accepted both shots", RunSync.Instance.GunShotsAccepted - accepted0 == 2, $"({RunSync.Instance.GunShotsAccepted - accepted0})");
            DemoNetCheck("host: the dummy is down after two bullets (60 health each)", dummy != null && h0 - dummy.Health >= 99f && dummy.Down, dummy != null ? $"({h0} -> {dummy.Health}, down {dummy.Down})" : "");
            DemoNetCheck("host: the client's two shots were shown here", cgc != null && cgc.ShotsSeen == 2, $"({(cgc != null ? cgc.ShotsSeen : -1)})");
            Signal("h_shot_ok");
            yield return WaitSig("c_done", 90);
            Log($"[DemoNet] host DONE fails={demoNetFails}");
            Log("DONE demonethost");
            Quit(demoNetFails == 0 ? 0 : 1);
        }

        IEnumerator DemoClientScenario()
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
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => tm.ResumeLevel == TowerRules.DemoLastFloor + 1, 30, "the host's resume level");
            DemoNetCheck("client: the host's state reaches here: the demo is over and the pew is for sale", TowerRules.DemoOver(tm.ResumeLevel) && Vendor.PewForSale(tm.ResumeLevel));
            Signal("c_ready");
            yield return WaitSig("h_cash", 60);
            yield return WaitFor(() => hm.Cash == Vendor.PewPrice + Vendor.AmmoClipPrice + 300, 30, "the team's cash");

            // ---- stand at the counter and buy it
            var me = NetPlayer.Local;
            var wob = me.GetComponent<WobblyPlayer>();
            var rb = wob.GetComponent<Rigidbody>();
            var vendor = Vendor.Current;
            var zone = vendor.GetComponent<Collider>();
            wob.TeleportTo(zone.bounds.center + Vector3.up * 0.2f, wob.transform.rotation);
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(1.5f);
            var pack = MyPack;
            RunSync.Instance.RequestBuy(Vendor.PewIndex);
            yield return WaitFor(() => pack.Items.Any(i => i && i.GetComponent<RangedWeapon>()) && pack.Equipped != null, 20, "the pew in the pack");
            yield return new WaitForSeconds(0.6f);
            var pew = pack.Items.First(i => i && i.GetComponent<RangedWeapon>());
            var gc = me.GetComponent<GunController>();
            DemoNetCheck("client: bought it: it is in the pack and in the hand, and the team paid the pew's price", pack.Equipped == pew && gc != null && gc.Gun != null && gc.Held != null && hm.Cash == Vendor.AmmoClipPrice + 300 && gc.Gun.Magazine.Reserve == 16, $"(cash {hm.Cash})");
            Signal("c_bought");
            yield return WaitSig("h_bought_ok", 60);
            RunSync.Instance.RequestBuy(Vendor.AmmoIndex);
            yield return WaitFor(() => gc.Gun != null && gc.Gun.Magazine.Reserve == 24 && hm.Cash == 300, 20, "the clip in the reserve and its price off the team's cash");          // (the cash comes with the host's next state, a frame or more after the ammunition)
            DemoNetCheck("client: the clip he bought added eight rounds to the reserve, for its price", gc.Gun.Magazine.Reserve == 24 && hm.Cash == 300, $"(reserve {gc.Gun.Magazine.Reserve}, cash {hm.Cash})");
            Signal("c_clip");
            yield return WaitSig("h_clip_ok", 60);

            // ---- the dummy
            var dummy = TrainingDummy.Current;
            Vector3 spot = default;
            bool open = dummy != null && OpenSpot(dummy.transform.position, 6f, out spot, false);
            DemoNetCheck("client: a clear spot 6 m from the dummy", open);
            var rig = FindFirstObjectByType<CameraFollow>();
            if (rig) rig.enabled = false;
            if (open)
            {
                wob.TeleportTo(spot + Vector3.up * 1.1f, wob.transform.rotation);
                rb.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(1.0f);
                var d = dummy.transform.position - wob.transform.position; d.y = 0f;
                wob.TeleportTo(wob.transform.position, Quaternion.LookRotation(d.normalized));
                rb.linearVelocity = Vector3.zero;
                var aimAt = dummy.transform.position + Vector3.up * 1.0f;
                AimCamera(Camera.main, wob, aimAt);
                yield return null; yield return null;
                AimCamera(Camera.main, wob, aimAt);
                float h0 = RunSync.Instance.DummyHealth;
                bool f1 = gc.Fire();
                yield return new WaitForSeconds(0.4f);
                AimCamera(Camera.main, wob, aimAt);
                bool f2 = gc.Fire();
                DemoNetCheck("client: both bullets left the gun", f1 && f2 && gc.ShotsFired == 2, $"({f1}, {f2})");
                yield return new WaitForSeconds(1.0f);
                float h1 = RunSync.Instance.DummyHealth;
                DemoNetCheck("client: the host's health of the dummy (published here) went down to nothing", h0 - h1 >= 99f || h1 <= 0f, $"({h0} -> {h1})");
            }
            Signal("c_shot");
            yield return WaitSig("h_shot_ok", 60);
            Signal("c_done");
            Log($"[DemoNet] client DONE fails={demoNetFails}");
            Log("DONE demonetclient");
            Quit(demoNetFails == 0 ? 0 : 1);
        }
    }
}
