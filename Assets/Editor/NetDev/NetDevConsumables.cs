using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The consumables of the vending machines (the owner, 2026-10-25: speed burst, endurance laster, silent steps, invisible, accuracy cola). The pure rules are <c>NetDevTest.ConsumablesRulesTest</c> (PolicyTest); the Play half is the scenario <c>Consumables</c> (one player:
    /// <c>qa_regress_bd.ps1 -Methods Consumables</c>; pictures need a real window: <c>ui.ps1 -Project C:\bd -Methods Consumables</c>, %TEMP%\be_ui_cons_*.png) and the two-machine pair <c>ConsumablesNet</c> (<c>qa_mp_bd.ps1 -Method ConsumablesNet</c>).
    /// </summary>
    public static partial class NetDevTest
    {
        public static void ConsumablesRulesTest()
        {
            var d = ConsumableRules.Defs;
            Check("consumables: five of them in the owner's order and under his names", d.Length == ConsumableRules.Count && ConsumableRules.Count == 5
                && d[0].name == "Speed Burst" && d[1].name == "Endurance Laster" && d[2].name == "Silent Steps" && d[3].name == "Invisible" && d[4].name == "Accuracy Cola");
            Check("consumables: the owner's numbers: limits 3 / 3 / 2 / 1, effects 10 s / 60 s / 15 s / 10 s, cooldowns 60 s / 30 s / 60 s, invisible once a round",
                d[0].limit == 3 && d[1].limit == 3 && d[2].limit == 2 && d[3].limit == 1 && d[0].effectSeconds == 10f && d[1].effectSeconds == 60f && d[2].effectSeconds == 15f && d[3].effectSeconds == 10f
                && d[0].cooldownSeconds == 60f && d[1].cooldownSeconds == 30f && d[2].cooldownSeconds == 60f && d[3].oncePerRound && !d[0].oncePerRound && !d[1].oncePerRound && !d[2].oncePerRound);
            Check("consumables: every price and weight is positive, every limit at least 1, every effect has a length", d.All(x => x.price > 0 && x.weight > 0f && x.weight < 2f && x.limit >= 1 && x.effectSeconds > 0f && !string.IsNullOrEmpty(x.effectText)));
            Check("consumables: the keys are 7 8 9 0 and minus, five different ones, none of the weapon bar's 1 to 6", ConsumableItem.Keys.Length == 5 && ConsumableItem.KeyNames.Length == 5 && ConsumableItem.Keys.Distinct().Count() == 5
                && !ConsumableItem.Keys.Any(k => k >= KeyCode.Alpha1 && k <= KeyCode.Alpha6) && ConsumableItem.Keys[0] == KeyCode.Alpha7 && ConsumableItem.Keys[3] == KeyCode.Alpha0 && ConsumableItem.Keys[4] == KeyCode.Minus);

            // ---- the timers
            var set = new BuffSet();
            const float t0 = 100f;
            Check("buffs: nothing runs and everything can be used at the start", !set.Active(ConsumableKind.SpeedBurst, t0) && set.Check(ConsumableKind.SpeedBurst, t0, 1) == UseCheck.Ok && set.SpeedFactor(t0) == 1f && set.DrainFactor(t0) == 1f && set.SpreadFactor(t0) == 1f && !set.StepsSilent(t0) && !set.Invisible(t0));
            set.Start(ConsumableKind.SpeedBurst, t0, 1);
            Check("buffs: the speed burst runs for 10 s at its factor, and is back to 1 after", set.Active(ConsumableKind.SpeedBurst, t0 + 9.9f) && set.SpeedFactor(t0 + 9.9f) == ConsumableRules.Of(ConsumableKind.SpeedBurst).factor && set.SpeedFactor(t0 + 9.9f) > 1f && !set.Active(ConsumableKind.SpeedBurst, t0 + 10.1f) && set.SpeedFactor(t0 + 10.1f) == 1f);
            float ready = ConsumableRules.CooldownFromEnd ? t0 + 10f + 60f : t0 + 60f;
            Check("buffs: the speed burst cools down for 60 s (counted from the end of the effect), and then can be used again", set.Check(ConsumableKind.SpeedBurst, t0 + 5f, 1) == UseCheck.OnCooldown && set.Check(ConsumableKind.SpeedBurst, ready - 0.5f, 1) == UseCheck.OnCooldown && set.Check(ConsumableKind.SpeedBurst, ready + 0.5f, 1) == UseCheck.Ok
                && set.CooldownLeft(ConsumableKind.SpeedBurst, t0 + 5f) > 55f && set.CooldownLeft(ConsumableKind.SpeedBurst, ready + 1f) == 0f);
            set.Start(ConsumableKind.EnduranceLaster, t0, 1);
            Check("buffs: the endurance laster slows the stamina drain for 60 s and the next one waits another 30 s after that", set.DrainFactor(t0 + 59f) < 1f && set.DrainFactor(t0 + 61f) == 1f && set.Check(ConsumableKind.EnduranceLaster, t0 + 61f, 1) == (ConsumableRules.CooldownFromEnd ? UseCheck.OnCooldown : UseCheck.Ok)
                && set.Check(ConsumableKind.EnduranceLaster, t0 + 91f, 1) == UseCheck.Ok);
            set.Start(ConsumableKind.SilentSteps, t0, 1);
            Check("buffs: silent steps are silent for 15 s only", set.StepsSilent(t0 + 14.9f) && !set.StepsSilent(t0 + 15.1f));
            set.Start(ConsumableKind.AccuracyCola, t0, 1);
            Check("buffs: the accuracy cola tightens the spread while it runs and not after", set.SpreadFactor(t0 + 1f) < 1f && set.SpreadFactor(t0 + 1f) > 0f && set.SpreadFactor(t0 + ConsumableRules.Of(ConsumableKind.AccuracyCola).effectSeconds + 1f) == 1f);
            set.Start(ConsumableKind.Invisible, t0, 3);
            Check("buffs: invisible lasts 10 s, and cannot be used again in the same round even when its time is up; in the next round it can", set.Invisible(t0 + 9.9f) && !set.Invisible(t0 + 10.1f)
                && set.Check(ConsumableKind.Invisible, t0 + 500f, 3) == UseCheck.UsedThisRound && set.Check(ConsumableKind.Invisible, t0 + 500f, 4) == UseCheck.Ok && set.Check(ConsumableKind.Invisible, t0 + 5f, 4) == UseCheck.OnCooldown);
            set.Start(ConsumableKind.EnduranceLaster, t0 + 100f, 2);
            Check("buffs: a second one used while one runs starts the effect over, it does not stack", set.DrainFactor(t0 + 100f + 59f) == ConsumableRules.Of(ConsumableKind.EnduranceLaster).factor);
            set.Clear();
            Check("buffs: clearing ends everything and forgets the rounds", !set.Active(ConsumableKind.SpeedBurst, t0) && set.Check(ConsumableKind.Invisible, t0, 3) == UseCheck.Ok && set.CooldownLeft(ConsumableKind.SpeedBurst, t0) == 0f);

            // ---- the words (placeholders) and the machines' list
            Check("consumables: the machine's line says what it does, for how long, when it can be used again and the limit, from the numbers", ConsumableRules.Blurb(ConsumableKind.SpeedBurst).Contains("10 s") && ConsumableRules.Blurb(ConsumableKind.SpeedBurst).Contains("cooldown 1 min") && ConsumableRules.Blurb(ConsumableKind.SpeedBurst).Contains("carry up to 3")
                && ConsumableRules.Blurb(ConsumableKind.EnduranceLaster).Contains("1 min") && ConsumableRules.Blurb(ConsumableKind.EnduranceLaster).Contains("cooldown 30 s") && ConsumableRules.Blurb(ConsumableKind.Invisible).Contains("once a round") && ConsumableRules.Blurb(ConsumableKind.Invisible).Contains("carry up to 1"));
            Check("consumables: seconds are written as seconds, whole minutes as minutes", ConsumableRules.Seconds(10f) == "10 s" && ConsumableRules.Seconds(60f) == "1 min" && ConsumableRules.Seconds(90f) == "90 s" && ConsumableRules.Seconds(120f) == "2 min");
            Check("consumables: the refusals name the kind (the limit, the round, the cooldown)", ConsumableRules.AtLimit(ConsumableKind.SilentSteps).Contains("2") && ConsumableRules.AtLimit(ConsumableKind.SilentSteps).Contains("Silent Steps")
                && ConsumableRules.Refusal(ConsumableKind.Invisible, UseCheck.UsedThisRound, 0f).Contains("round") && ConsumableRules.Refusal(ConsumableKind.SpeedBurst, UseCheck.OnCooldown, 41.2f).Contains("42 s"));
            var cat = Vendor.Catalog;
            var sold = Vendor.MachineProducts;
            bool listed = true;
            for (int k = 0; k < ConsumableRules.Count; k++)
            {
                int idx = System.Array.FindIndex(cat, p => p.isConsumable && p.consumable == (ConsumableKind)k);
                var dk = ConsumableRules.Of((ConsumableKind)k);
                listed &= idx > Vendor.WalkieIndex && Vendor.OnMachine(idx) && !Vendor.OnOffer(idx, 1) && !Vendor.OnOffer(idx, 13) && cat[idx].name == dk.name && cat[idx].price == dk.price && cat[idx].machine && sold.Contains(idx);
            }
            Check("consumables: the vending machines list each of the five once (after the walkie talkie: the dealer's places are unchanged), at the rules' price, and the dealer sells none", listed && cat.Count(p => p.isConsumable) == 5 && sold.Length == ConsumableRules.Count);          // (only the consumables: the mug, stapler, toy and extinguisher left the machines, the owner 2026-10-27)
        }
    }

    public partial class NetDevRunner
    {
        int coFails;

        void CoCheck(string name, bool ok, string detail = "")
        {
            if (!ok) coFails++;
            Log($"[Consumables] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static int CoIndex(ConsumableKind kind) => System.Array.FindIndex(Vendor.Catalog, p => p.isConsumable && p.consumable == kind);

        IEnumerator ConsumablesScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            HeistManager.TestPartySize = 0;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var buffs = player.GetComponent<PlayerBuffs>();
            var stamina = player.GetComponent<Stamina>();
            var vendor = Vendor.Current;
            CoCheck("(test) the player has a pack, a buffs component and a stamina meter, and the dealer is there", pack != null && buffs != null && stamina != null && vendor != null && VendingMachine.Machines.Count > 0, $"({VendingMachine.Machines.Count} machines)");
            if (pack == null || buffs == null || vendor == null || VendingMachine.Machines.Count == 0) { Log($"[Consumables] DONE fails={coFails}"); Quit(1); yield break; }
            hm.AddCash(100000);
            var tray = VendingMachine.Machines[0].TrayPoint;
            float weight0 = pack.Weight;

            // ---- buying: each of the five is bought at a machine and goes into the pack; the limit stops a sixth purchase
            bool allBought = true, limitsHold = true; string limitSaid = "";
            for (int k = 0; k < ConsumableRules.Count; k++)
            {
                var kind = (ConsumableKind)k; var def = ConsumableRules.Of(kind);
                int cash0 = hm.Cash;
                string said = vendor.BuyAtMachine(hm, pack, CoIndex(kind), tray);
                yield return null;
                allBought &= said.Contains("Bought") && pack.CountConsumable(kind) == 1 && hm.Cash == cash0 - def.price;
                for (int n = 1; n < def.limit; n++) vendor.BuyAtMachine(hm, pack, CoIndex(kind), tray);
                yield return null;
                int cashFull = hm.Cash;
                string refused = vendor.BuyAtMachine(hm, pack, CoIndex(kind), tray);
                limitsHold &= pack.CountConsumable(kind) == def.limit && refused == ConsumableRules.AtLimit(kind) && hm.Cash == cashFull;
                limitSaid = refused;
            }
            CoCheck("each of the five is bought at a vending machine for its price and is in the pack", allBought);
            CoCheck("the limits hold: 3 speed bursts, 3 endurance lasters, 2 silent steps, 1 invisible, 3 accuracy colas - one more is refused with the limit and costs nothing", limitsHold, $"('{limitSaid}')");
            var spare = ConsumableItem.Create(ConsumableKind.SilentSteps, player.transform.position + Vector3.forward * 3f, null, 0f).GetComponent<Pickup>();
            yield return null;
            CoCheck("a can lying about cannot be picked up either when the pack is at its limit", !pack.CanFit(spare, out string why) && why == ConsumableRules.AtLimit(ConsumableKind.SilentSteps), $"('{why}')");
            Destroy(spare.gameObject);
            float w = pack.Weight - weight0;
            CoCheck("the cans weigh what the rules say (12 of them: 3.6)", Mathf.Abs(w - 12f * 0.3f) < 0.01f, $"({w})");

            // ---- using: the key / button path (UseKind) takes one can out of the pack and starts the effect
            int steps0 = 0; NoiseBus.OnNoise += OnCoNoise;
            void OnCoNoise(NoiseEvent n) { if (n.Source == player.gameObject) steps0++; }
            player.testMove = new Vector3(0f, 0f, 1f);
            yield return new WaitForSeconds(2.2f);
            player.testMove = null;
            int stepsBefore = steps0;
            CoCheck("(control) walking makes footstep noise the guards can hear", stepsBefore >= 2, $"({stepsBefore} noises in 2 s)");

            pack.UseKind(ConsumableKind.SilentSteps);
            yield return null;
            CoCheck("silent steps: one can is used up, the effect runs, the footstep noise is zero", pack.CountConsumable(ConsumableKind.SilentSteps) == 1 && buffs.Set.StepsSilent(Time.time) && player.StepsSilent && player.CurrentFootstepNoise == 0f);
            steps0 = 0;
            player.testMove = new Vector3(0f, 0f, 1f);
            yield return new WaitForSeconds(2.2f);
            player.testMove = null;
            CoCheck("silent steps: two seconds of walking make no noise at all", steps0 == 0, $"({steps0} noises)");
            NoiseBus.OnNoise -= OnCoNoise;
            pack.UseKind(ConsumableKind.SilentSteps);
            yield return null;
            CoCheck("silent steps: a second can while the first is running or cooling down is refused (still 1 in the pack)", pack.CountConsumable(ConsumableKind.SilentSteps) == 1 && buffs.Check(ConsumableKind.SilentSteps) == UseCheck.OnCooldown);

            pack.UseKind(ConsumableKind.SpeedBurst);
            yield return null;
            CoCheck("speed burst: one can is used up and the player's speed is up by the factor", pack.CountConsumable(ConsumableKind.SpeedBurst) == 2 && Mathf.Abs(player.SpeedBuff - ConsumableRules.Of(ConsumableKind.SpeedBurst).factor) < 1e-4f && player.SpeedBuff > 1f, $"(factor {player.SpeedBuff})");
            pack.UseKind(ConsumableKind.SpeedBurst);
            yield return null;
            CoCheck("speed burst: a second one straight after is refused (cooling down), the can stays", pack.CountConsumable(ConsumableKind.SpeedBurst) == 2);

            pack.UseKind(ConsumableKind.EnduranceLaster);
            yield return null;
            float before = stamina.Meter.Value;
            stamina.Meter.Step(1f, true, stamina.DrainMultiplier);
            float drained = before - stamina.Meter.Value;
            CoCheck("endurance laster: one can is used up and a second of sprinting drains half of what it did", pack.CountConsumable(ConsumableKind.EnduranceLaster) == 2 && Mathf.Abs(stamina.DrainMultiplier - ConsumableRules.Of(ConsumableKind.EnduranceLaster).factor) < 1e-4f && Mathf.Abs(drained - StaminaMeter.DrainPerSecond * stamina.DrainMultiplier) < 0.01f, $"(drained {drained:0.0})");
            stamina.Meter.Refill();

            pack.UseKind(ConsumableKind.AccuracyCola);
            yield return null;
            CoCheck("accuracy cola: one can is used up and the gun's spread is multiplied by the factor (tighter)", pack.CountConsumable(ConsumableKind.AccuracyCola) == 2 && Mathf.Abs(PlayerBuffs.SpreadFactor(player.gameObject) - ConsumableRules.Of(ConsumableKind.AccuracyCola).factor) < 1e-4f && PlayerBuffs.SpreadFactor(player.gameObject) < 1f);
            pack.UseKind(ConsumableKind.Invisible);
            yield return null;
            CoCheck("invisible: the only can is used up and the guards' eyes skip the player", pack.CountConsumable(ConsumableKind.Invisible) == 0 && PlayerBuffs.IsInvisible(player));
            vendor.BuyAtMachine(hm, pack, CoIndex(ConsumableKind.Invisible), tray);
            yield return null;
            pack.UseKind(ConsumableKind.Invisible);
            yield return null;
            CoCheck("invisible: a second can bought in the same round cannot be used (once a round), and stays in the pack", pack.CountConsumable(ConsumableKind.Invisible) == 1 && buffs.Check(ConsumableKind.Invisible) == UseCheck.UsedThisRound);
            pack.UseKind(ConsumableKind.EnduranceLaster);
            yield return null;
            CoCheck("(control) using a kind that is cooling down changes nothing", pack.CountConsumable(ConsumableKind.EnduranceLaster) == 2);

            if (CanPicture)
            {
                var machine = VendingMachine.Machines[0];
                yield return new WaitForSeconds(0.4f);
                yield return Snap("cons_1_hud");
                CoCheck("the HUD shows a chip for each kind carried or running", ConsumableHud.LastChips >= 4, $"({ConsumableHud.LastChips} chips)");
                player.TeleportTo(machine.TrayPoint + Vector3.up * 0.6f, player.transform.rotation);          // (the screen closes itself when the player is away from the machine)
                yield return new WaitForSeconds(0.6f);
                VendingScreen.Show(machine);
                yield return new WaitForSeconds(0.4f);
                yield return Snap("cons_2_machine");
                VendingScreen.Close();
                SetStaticProp(typeof(Backpack), "UiOpen", true);
                yield return new WaitForSeconds(0.6f);
                yield return Snap("cons_3_tab");
                SetStaticProp(typeof(Backpack), "UiOpen", false);
            }

            // ---- on a floor: an invisible player is not seen by a guard who looks straight at him; one who is not invisible is
            HeistManager.TestPartySize = 3;                                  // (a party of three: one sighting is no Big Alarm)
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(0.5f);
            player = WobblyPlayerRegistry.All[0];
            buffs = player.GetComponent<PlayerBuffs>();
            buffs.Set.Clear();                                               // (the invisible can of the docks would still hide him)
            const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            var fState = typeof(GuardAI).GetField("state", any);
            GuardAI watcher = null; GuardState state0 = GuardState.Patrol;
            foreach (var cand in GuardAI.All.Where(g => g && !g.Dead && !g.IsBoss && !g.IsBackup && g.name != "Guard_Outpost").Take(6))
            {
                if (!OpenSpot(cand.transform.position + Vector3.up * 0.9f, 6f, out var spot, true)) continue;
                fState.SetValue(cand, GuardState.Patrol);
                var to = spot - cand.transform.position; to.y = 0f;
                GuardSightFreeze(cand);
                cand.transform.rotation = Quaternion.LookRotation(to.normalized);
                player.TeleportTo(spot + Vector3.up * 1.1f, Quaternion.identity);
                yield return null;
                yield return null;
                if (GsCanSee(cand, player)) { watcher = cand; state0 = cand.State; break; }
            }
            CoCheck("(test) a guard on duty looks at the player 6 m away and sees him", watcher != null);
            if (watcher != null)
            {
                int round = PlayerBuffs.Round;
                watcher.ForgetNotices();
                buffs.StartLocal(ConsumableKind.Invisible);                  // (at once: the guard has had two frames to look at him)
                bool blind = !GsCanSee(watcher, player);
                yield return new WaitForSeconds(2.5f);
                CoCheck("invisible: the same guard, the same spot: he does not see him, his notice meter stays empty for 2.5 s, nobody is marked, the guard is still on his round", blind && watcher.NoticeProgress(player) == 0f && !hm.IsMarked(player) && watcher.State == state0, $"(blind {blind}, meter {watcher.NoticeProgress(player):0.00}, marked {hm.IsMarked(player)}, state {watcher.State})");
                CoCheck("invisible: used in this round, so the next can has to wait for the next floor", buffs.Check(ConsumableKind.Invisible) == UseCheck.UsedThisRound && PlayerBuffs.Round == round);
                float waited = 0f;
                while (PlayerBuffs.IsInvisible(player) && waited < 14f) { waited += Time.deltaTime; yield return null; }
                CoCheck("invisible: after its 10 s the effect is over", !PlayerBuffs.IsInvisible(player) && waited > 6f, $"(waited {waited:0.0} s)");
                bool seesAgain = GsCanSee(watcher, player);
                watcher.ForgetNotices();
                float t1 = Time.time;
                while (Time.time - t1 < 4f && !(watcher.NoticeProgress(player) > 0f || hm.IsMarked(player) || watcher.State != state0)) yield return null;
                CoCheck("invisible: and then the same guard sees him again and starts to notice him (control)", seesAgain && (watcher.NoticeProgress(player) > 0f || hm.IsMarked(player) || watcher.State != state0), $"(sees {seesAgain}, meter {watcher.NoticeProgress(player):0.00}, marked {hm.IsMarked(player)}, state {watcher.State})");
                yield return LoadLevel(hm, 2);
                yield return new WaitForSeconds(0.5f);
                var buffs2 = WobblyPlayerRegistry.All[0].GetComponent<PlayerBuffs>();
                CoCheck("a new floor is a new round: invisible can be used again once its 10 s are over", PlayerBuffs.Round > round && buffs2.Check(ConsumableKind.Invisible) == UseCheck.Ok, $"(round {PlayerBuffs.Round}, was {round}, check {buffs2.Check(ConsumableKind.Invisible)})");
            }

            Log($"[Consumables] DONE fails={coFails}");
            Quit(coFails == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ two machines: a client buys and uses, the host sees the same

        int conFails;

        void ConCheck(string name, bool ok, string detail = "")
        {
            if (!ok) conFails++;
            Log($"[ConsumablesNet] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator ConsumablesHostScenario()
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
            hm.godMode = true; hm.freezeTimer = true;
            hm.AddCash(20000);
            Signal("h_ready");
            yield return WaitSig("c_bought", 120);
            yield return new WaitForSeconds(1.0f);
            var cp = NetPlayer.All.FirstOrDefault(p => !p.IsOwner);
            var cpack = cp ? cp.GetComponent<Backpack>() : null;
            var cbuffs = cp ? cp.GetComponent<PlayerBuffs>() : null;
            ConCheck("host: the client's purchases are in his pack here: 2 silent steps (the limit stopped the third), 1 invisible (the second was refused), 1 accuracy cola",
                cpack != null && cpack.CountConsumable(ConsumableKind.SilentSteps) == 2 && cpack.CountConsumable(ConsumableKind.Invisible) == 1 && cpack.CountConsumable(ConsumableKind.AccuracyCola) == 1,
                cpack != null ? $"({cpack.CountConsumable(ConsumableKind.SilentSteps)} / {cpack.CountConsumable(ConsumableKind.Invisible)} / {cpack.CountConsumable(ConsumableKind.AccuracyCola)})" : "(no pack)");
            Signal("h_saw_bought");
            yield return WaitSig("c_used", 120);
            yield return new WaitForSeconds(1.0f);
            var cw = cp ? cp.GetComponent<WobblyPlayer>() : null;
            ConCheck("host: the cans the client drank are gone from his pack here, one of each is left or none (silent steps 1, invisible 0, accuracy cola 0)",
                cpack != null && cpack.CountConsumable(ConsumableKind.SilentSteps) == 1 && cpack.CountConsumable(ConsumableKind.Invisible) == 0 && cpack.CountConsumable(ConsumableKind.AccuracyCola) == 0);
            ConCheck("host: the client is invisible HERE (where the guards are), his steps are silent here, his shots are tighter here", cbuffs != null && PlayerBuffs.IsInvisible(cw) && cw.StepsSilent && cw.CurrentFootstepNoise == 0f && PlayerBuffs.SpreadFactor(cp.gameObject) < 1f);
            ConCheck("host: the host accepted exactly three uses", RunSync.Instance.ConsumablesUsed == 3, $"({RunSync.Instance.ConsumablesUsed})");
            Signal("h_done");
            yield return WaitSig("c_done", 60);
            Log($"[ConsumablesNet] host DONE fails={conFails}");
            Log("DONE consumablesnethost");
            Quit(conFails == 0 ? 0 : 1);
        }

        IEnumerator ConsumablesClientScenario()
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
            var machine = VendingMachine.Machines.Count > 0 ? VendingMachine.Machines[0] : null;
            ConCheck("client: the docks have a vending machine here", machine != null);
            if (machine == null) { Log($"[ConsumablesNet] client DONE fails={conFails}"); Quit(1); yield break; }
            Teleport(machine.TrayPoint + Vector3.up * 0.6f);
            yield return new WaitForSeconds(1.5f);
            for (int i = 0; i < 3; i++) { RunSync.Instance.RequestMachineBuy(machine.Number, CoIndex(ConsumableKind.SilentSteps)); yield return new WaitForSeconds(0.5f); }
            for (int i = 0; i < 2; i++) { RunSync.Instance.RequestMachineBuy(machine.Number, CoIndex(ConsumableKind.Invisible)); yield return new WaitForSeconds(0.5f); }
            RunSync.Instance.RequestMachineBuy(machine.Number, CoIndex(ConsumableKind.AccuracyCola));
            yield return WaitFor(() => pack.CountConsumable(ConsumableKind.AccuracyCola) == 1 && pack.CountConsumable(ConsumableKind.Invisible) >= 1, 30, "the cans in the pack");
            yield return new WaitForSeconds(1.0f);
            ConCheck("client: 2 silent steps (the third was refused), 1 invisible (the second was refused), 1 accuracy cola in the pack", pack.CountConsumable(ConsumableKind.SilentSteps) == 2 && pack.CountConsumable(ConsumableKind.Invisible) == 1 && pack.CountConsumable(ConsumableKind.AccuracyCola) == 1,
                $"({pack.CountConsumable(ConsumableKind.SilentSteps)} / {pack.CountConsumable(ConsumableKind.Invisible)} / {pack.CountConsumable(ConsumableKind.AccuracyCola)})");
            Signal("c_bought");
            yield return WaitSig("h_saw_bought", 60);
            var buffs = Mine.GetComponent<PlayerBuffs>();
            pack.UseKind(ConsumableKind.SilentSteps);
            pack.UseKind(ConsumableKind.Invisible);
            pack.UseKind(ConsumableKind.AccuracyCola);
            pack.UseKind(ConsumableKind.SilentSteps);                       // (the second is refused: it cools down)
            yield return WaitFor(() => buffs.Set.Invisible(Time.time) && buffs.Set.StepsSilent(Time.time) && buffs.Set.SpreadFactor(Time.time) < 1f, 30, "the effects here");
            yield return new WaitForSeconds(1.0f);
            ConCheck("client: the effects run here too, the cans are gone, and the refused second silent steps stayed in the pack", pack.CountConsumable(ConsumableKind.Invisible) == 0 && pack.CountConsumable(ConsumableKind.AccuracyCola) == 0 && pack.CountConsumable(ConsumableKind.SilentSteps) == 1);
            Signal("c_used");
            yield return WaitSig("h_done", 60);
            Signal("c_done");
            Log($"[ConsumablesNet] client DONE fails={conFails}");
            Log("DONE consumablesnetclient");
            Quit(conFails == 0 ? 0 : 1);
        }
    }
}
