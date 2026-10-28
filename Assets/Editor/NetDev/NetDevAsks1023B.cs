using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of batch A2 of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63; offline, on the real floors): the guard that needs 2 seconds of contact to pin (T4), the 0 to 3 bullets an eliminated guard drops and the player who takes them by
    /// running over them (T25, T24) and the second set of armour that is very heavy in the pack (T28). Run: <c>qa_regress_bd.ps1 -Methods Asks1023B</c>; the log ends with <c>[Asks1023B] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int a23bFails;

        void A23B(string name, bool ok, string detail = "")
        {
            if (!ok) a23bFails++;
            Log($"[Asks1023B] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>The nearest bullets (an <see cref="AmmoClip"/> a guard dropped) to a point, or null.</summary>
        static AmmoClip BulletsNear(Vector3 at, float within = 3f) =>
            FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Where(c => c && c.isActiveAndEnabled && Vector3.Distance(c.transform.position, at) < within).OrderBy(c => Vector3.Distance(c.transform.position, at)).FirstOrDefault();

        /// <summary>Shoots a guard down with the given drop roll (<see cref="GuardDrops.TestRoll"/>) and returns where he stood.</summary>
        Vector3 KillWithRoll(GuardAI g, int roll, float hitsPerBullet)
        {
            var at = g.transform.position;
            GuardDrops.TestRoll = roll;
            int n = 0;
            while (!g.Dead && n < 60) { g.BulletHitHost(hitsPerBullet, false, null); n++; }
            return at;
        }

        IEnumerator Asks1023BScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var fState = typeof(GuardAI).GetField("state", any);
            var fTarget = typeof(GuardAI).GetField("target", any);
            var fUnseen = typeof(GuardAI).GetField("unseenFor", any);
            A23B("(test) the guard's fields are where the test expects them", fState != null && fTarget != null && fUnseen != null);
            if (fState == null || fTarget == null || fUnseen == null) { Quit(1); yield break; }

            // ================================================================== floor 2
            yield return LoadLevel(hm, 2);
            yield return new WaitForSeconds(1.0f);
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var pack = player.GetComponent<Backpack>();
            float pewHits = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].guardHits;
            var guards = GuardAI.All.Where(g => g && g.enabled && !g.IsPuppet && !g.IsBoss && g.name != "Guard_Outpost" && !g.Dead).ToList();
            A23B("(test) floor 2 has guards to try things on", guards.Count >= 5, $"({guards.Count})");
            if (guards.Count < 5) { Quit(1); yield break; }

            // ---- T4: a guard needs 1 second of contact to pin (god mode on: the pin itself would end the floor for the only player, so the clock is read: GrabsDone counts the times it ran out)
            {
                var grabber = guards[0];
                var switchedOff = new List<GuardAI>();
                foreach (var g in GuardAI.All.ToList()) if (g && g != grabber) { g.enabled = false; switchedOff.Add(g); }          // (nobody else bothers the player; they are switched on again below)
                bool open = OpenSpot(grabber.transform.position + Vector3.up * 0.9f, 1.1f, out var spot, true);
                A23B("(test) a clear spot a metre from the guard", open);
                if (open)
                {
                    int grabs0 = grabber.GrabsDone;
                    player.PinImmuneUntil = 0f;
                    player.TeleportTo(spot + Vector3.up * 1.1f, player.transform.rotation);
                    rb.linearVelocity = Vector3.zero;
                    fTarget.SetValue(grabber, player); fUnseen.SetValue(grabber, 0f); fState.SetValue(grabber, GuardState.Chase);
                    grabber.GetComponent<NavMeshAgent>().speed = grabber.chaseSpeed;
                    float contactStart = -1f, heldAt06 = -1f; int grabsAt06 = -1, grabsAt13 = -1;
                    float t0 = Time.time;
                    while (Time.time - t0 < 8f)
                    {
                        rb.linearVelocity = Vector3.zero;                                                              // (the player stands still)
                        if (contactStart < 0f && grabber.GrabHeld > 0.01f) contactStart = Time.time;
                        if (contactStart >= 0f)
                        {
                            float since = Time.time - contactStart;
                            if (heldAt06 < 0f && since >= 0.6f) { heldAt06 = grabber.GrabHeld; grabsAt06 = grabber.GrabsDone - grabs0; }
                            if (grabsAt13 < 0 && since >= 1.3f) { grabsAt13 = grabber.GrabsDone - grabs0; break; }
                        }
                        yield return null;
                    }
                    A23B("T4: the guard reached the player and the contact clock started", contactStart >= 0f, $"(contact after {contactStart - t0:0.0} s)");
                    A23B("T4: after 0.6 s of contact the clock is at about 0.6 s and he has not grabbed yet", heldAt06 > 0.4f && heldAt06 < 0.85f && grabsAt06 == 0, $"(clock {heldAt06:0.00} s, grabs {grabsAt06})");
                    A23B("T4: after 1.3 s of contact the grab has gone off once (the clock ran out)", grabsAt13 == 1, $"(grabs {grabsAt13})");

                    // getting away drains the clock: out of reach it falls twice as fast
                    fState.SetValue(grabber, GuardState.Chase); fTarget.SetValue(grabber, player); fUnseen.SetValue(grabber, 0f);
                    player.TeleportTo(spot + Vector3.up * 1.1f, player.transform.rotation);
                    rb.linearVelocity = Vector3.zero;
                    float t1 = Time.time;
                    while (Time.time - t1 < 4f && grabber.GrabHeld < 0.6f) { rb.linearVelocity = Vector3.zero; yield return null; }
                    float before = grabber.GrabHeld;
                    player.TeleportTo(spot + Vector3.up * 1.1f + Vector3.right * 40f, player.transform.rotation);          // (far out of reach)
                    rb.linearVelocity = Vector3.zero;
                    yield return new WaitForSeconds(0.3f);
                    float after = grabber.GrabHeld;
                    A23B("T4: out of reach the clock falls (a player who gets away loses the grab)", before >= 0.5f && after < before - 0.3f, $"(clock {before:0.00} -> {after:0.00} s in 0.3 s)");
                }
                foreach (var g in switchedOff) if (g) g.enabled = true;
                player.TeleportTo(spot + Vector3.up * 1.1f, player.transform.rotation);
                fState.SetValue(grabber, GuardState.Patrol);
                yield return new WaitForSeconds(0.2f);
            }

            // ---- T25 + T24: an eliminated guard drops 0 to 3 bullets (his gun's kind; pistol bullets for an unarmed guard); a player takes them by running over them
            {
                var victims = GuardAI.All.Where(g => g && !g.IsBoss && g.name != "Guard_Outpost" && !g.Dead).ToList();
                A23B("(test) enough guards left to try the drops on", victims.Count >= 4, $"({victims.Count})");
                if (victims.Count >= 4)
                {
                    int dropped0 = GuardDrops.Dropped, bullets0 = GuardDrops.DroppedBullets;
                    var none = victims[0]; none.SetLoadout(GuardLoadout.Unarmed);
                    var noneAt = KillWithRoll(none, 0, pewHits);
                    yield return null;
                    var leftNone = BulletsNear(noneAt);
                    A23B("T25: a roll of 0 (the most likely) drops nothing", none.Dead && GuardDrops.Dropped == dropped0 && leftNone == null, $"(dropped {GuardDrops.Dropped - dropped0})");

                    var one = victims[1]; one.SetLoadout(GuardLoadout.Unarmed);
                    var oneAt = KillWithRoll(one, 40, pewHits);
                    yield return null;
                    var leftOne = BulletsNear(oneAt);
                    A23B("T25: a roll of 40 drops one bullet - of the pistol, because an unarmed guard's bullets are pistol bullets", one.Dead && GuardDrops.Dropped == dropped0 + 1 && GuardDrops.DroppedBullets == bullets0 + 1 && leftOne != null && leftOne.rounds == 1 && leftOne.kind == RangedWeapon.Kind.Pew, $"(rounds {(leftOne ? leftOne.rounds : -1)})");

                    var two = victims[2]; two.SetLoadout(new GuardLoadout(true, RangedWeapon.Kind.Smg, ShieldTier.None));
                    var twoAt = KillWithRoll(two, 70, pewHits);
                    yield return null;
                    var leftTwo = BulletsNear(twoAt);
                    A23B("T25: a roll of 70 drops two bullets, of the kind of gun the guard carried (SMG bullets)", two.Dead && leftTwo != null && leftTwo.rounds == 2 && leftTwo.kind == RangedWeapon.Kind.Smg, $"(rounds {(leftTwo ? leftTwo.rounds : -1)}, kind {(leftTwo ? leftTwo.kind.ToString() : "-")})");

                    var three = victims[3]; three.SetLoadout(new GuardLoadout(true, RangedWeapon.Kind.Pew, ShieldTier.None));
                    var threeAt = KillWithRoll(three, 99, pewHits);
                    yield return null;
                    var leftThree = BulletsNear(threeAt);
                    A23B("T25: a roll of 99 (the least likely) drops three bullets, close to where he fell", three.Dead && leftThree != null && leftThree.rounds == 3 && leftThree.kind == RangedWeapon.Kind.Pew && Vector3.Distance(leftThree.transform.position, threeAt) < 2.5f, $"(rounds {(leftThree ? leftThree.rounds : -1)})");
                    GuardDrops.TestRoll = -1;
                    if (leftThree != null)
                    {
                        var pk = leftThree.GetComponent<Pickup>();
                        var seenByCrosshair = new List<Pickup>();
                        Pickup.ScanAround(leftThree.transform.position + Vector3.up * 0.1f, 1.5f, seenByCrosshair);
                        A23B("T25: the bullets are no item for the crosshair and E: the scan does not list them and the pack refuses them", pk.runOverOnly && !seenByCrosshair.Contains(pk) && !pack.TryStore(pk) && leftThree != null);

                        // a player without a pistol runs over them and nothing happens
                        var barePack = pack.Items.Count(i => i && i.GetComponent<RangedWeapon>()) == 0;
                        player.TeleportTo(leftThree.transform.position + Vector3.up * 1.1f, player.transform.rotation);
                        rb.linearVelocity = Vector3.zero;
                        yield return new WaitForSeconds(0.5f);
                        A23B("T25: a player who carries no pistol runs over the bullets and they stay where they lie", barePack && leftThree != null && leftThree.isActiveAndEnabled);

                        // the pistol is bought / given; he stands 2 m away: nothing; then he steps on them
                        var menu = new GameObject("TestModeForAsks").AddComponent<TestMode>();
                        player.TeleportTo(leftThree.transform.position + Vector3.up * 1.1f + Vector3.right * 2.2f, player.transform.rotation);
                        rb.linearVelocity = Vector3.zero;
                        typeof(TestMode).GetMethod("GiveGun", any).Invoke(menu, new object[] { pack, RangedWeapon.Kind.Pew });
                        yield return new WaitForSeconds(0.6f);
                        var pewGun = pack.Equipped ? pack.Equipped.GetComponent<RangedWeapon>() : null;
                        A23B("(test) the player has a pistol in hand", pewGun != null);
                        if (pewGun != null)
                        {
                            pewGun.Magazine.SetReserve(0);
                            yield return new WaitForSeconds(0.4f);
                            A23B("T25: standing 2 m away he gets nothing", leftThree != null && pewGun.Magazine.Reserve == 0);
                            player.TeleportTo(leftThree.transform.position + Vector3.up * 1.1f, player.transform.rotation);
                            rb.linearVelocity = Vector3.zero;
                            yield return new WaitForSeconds(0.5f);
                            A23B("T25: running over them puts the three bullets in the pistol's reserve and they are gone", pewGun.Magazine.Reserve == 3 && leftThree == null, $"(reserve {pewGun.Magazine.Reserve})");

                            // bullets for a gun he does not carry stay (SMG bullets, a pistol only)
                            if (leftTwo != null)
                            {
                                player.TeleportTo(leftTwo.transform.position + Vector3.up * 1.1f, player.transform.rotation);
                                rb.linearVelocity = Vector3.zero;
                                yield return new WaitForSeconds(0.5f);
                                A23B("T25: SMG bullets are left lying for a player who carries only a pistol", leftTwo != null && leftTwo.isActiveAndEnabled && pewGun.Magazine.Reserve == 3);
                            }

                            // T24: no limit on the bullets carried
                            pewGun.Magazine.AddReserve(5000);
                            A23B("T24: the pistol's reserve has no upper limit (5003 bullets carried)", pewGun.Magazine.Reserve == 5003, $"(reserve {pewGun.Magazine.Reserve})");
                        }
                        Destroy(menu.gameObject);
                    }
                    GuardDrops.TestRoll = -1;
                }
            }

            // ---- T28: a second set of armour, to bring to a teammate; very heavy in the pack
            {
                foreach (var held in pack.Snapshot()) if (held && held.TryGetComponent(out ArmourItem _)) { pack.Drop(held); Destroy(held.gameObject); }
                yield return null;
                var at = player.transform.position + Vector3.up * 1.0f + player.transform.forward * 0.8f;
                Pickup Make(ShieldTier t, float dy) => ArmourItem.Create(t, at + Vector3.up * dy, null, 0f).GetComponent<Pickup>();
                float w0 = pack.Weight;
                var light = Make(ShieldTier.Light, 0f); var medium = Make(ShieldTier.Medium, 0.4f); var heavy = Make(ShieldTier.Heavy, 0.8f);
                bool first = pack.TryStore(light);
                A23B("T28: the first piece is taken and worn at its normal weight", first && PlayerArmour.TierOf(pack) == ShieldTier.Light && Mathf.Abs(pack.Weight - w0 - ArmourRules.Weight(ShieldTier.Light)) < 0.01f, $"(weight {w0} -> {pack.Weight})");
                bool second = pack.TryStore(medium);
                A23B("T28: a second piece is taken as well: the better one (Medium) is the one worn, the Light one is the spare and weighs its spare weight",
                    second && pack.Contains(medium) && pack.Contains(light) && PlayerArmour.TierOf(pack) == ShieldTier.Medium && pack.WornArmour == medium
                    && Mathf.Abs(pack.WeightOf(light) - ArmourRules.SpareWeight(ShieldTier.Light)) < 0.01f && Mathf.Abs(pack.Weight - w0 - ArmourRules.PackWeight(ShieldTier.Light, ShieldTier.Medium)) < 0.01f, $"(weight {w0} -> {pack.Weight})");
                bool fitsThird = pack.CanFit(heavy, out string why);
                bool third = pack.TryStore(heavy);
                A23B("T28: a third piece is refused and the pack is as it was", !fitsThird && !third && why == ArmourRules.TooMuchArmour && !pack.Contains(heavy) && pack.CountArmour() == 2, $"(\"{why}\")");
                pack.Drop(medium);
                yield return null;
                A23B("T28: putting down the worn piece makes the spare the one worn, at its normal weight", PlayerArmour.TierOf(pack) == ShieldTier.Light && pack.WornArmour == light && Mathf.Abs(pack.Weight - w0 - ArmourRules.Weight(ShieldTier.Light)) < 0.01f, $"(weight {pack.Weight})");

                // very heavy: with ten kilograms of other things in the pack the second piece no longer fits
                var ballast = AsksItem("Ballast", at + Vector3.up * 1.2f); ballast.weight = 10f;
                pack.TryStore(ballast);
                bool fitsHeavy = pack.CanFit(medium, out string whyHeavy);
                A23B("T28: with the Light piece worn and 10 kg of other things in the pack a second piece (Medium) is too heavy to take", !fitsHeavy && whyHeavy != null && whyHeavy.Contains("Too heavy") && !pack.TryStore(medium), $"(\"{whyHeavy}\")");
                pack.Drop(ballast);
                yield return null;
                A23B("T28: with the ballast gone the same piece fits again", pack.CanFit(medium, out _));
                pack.TryStore(medium);
                pack.Drop(light); pack.Drop(medium);
                yield return null;
                A23B("T28: with both pieces put down the pack holds no armour and weighs what it did", PlayerArmour.TierOf(pack) == ShieldTier.None && Mathf.Abs(pack.Weight - w0) < 0.01f, $"(weight {pack.Weight})");
                foreach (var p in new[] { light, medium, heavy, ballast }) if (p) Destroy(p.gameObject);
            }

            Log($"[Asks1023B] DONE fails={a23bFails}");
            Quit(a23bFails == 0 ? 0 : 1);
        }

        /// <summary>
        /// Pictures of the ammunition panel while a gun reloads (T27: the ring, and the shotgun's counter of the shells going in): a real window is needed, IMGUI is not drawn in batch mode (<c>ui.ps1 -Methods Asks1023BUi</c>).
        /// Pictures: %TEMP%\be_ui_ring_*.png.
        /// </summary>
        IEnumerator Asks1023BUiScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return LoadLevel(hm, 2);
            yield return new WaitForSeconds(1.0f);
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var menu = new GameObject("TestModeForAsksUi").AddComponent<TestMode>();
            const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            foreach (var kind in new[] { RangedWeapon.Kind.Pew, RangedWeapon.Kind.Shotgun })
            {
                typeof(TestMode).GetMethod("GiveGun", any).Invoke(menu, new object[] { pack, kind });
                yield return new WaitForSeconds(0.8f);
                var gc = player.GetComponent<GunController>();
                A23B($"(test) the {kind} is in the player's hand", gc != null && gc.Gun != null && gc.Gun.kind == kind);
                if (gc == null || gc.Gun == null) continue;
                var mag = gc.Gun.Magazine;
                mag.SetReserve(130);
                yield return Snap("ring_" + kind + "_full");
                for (int i = 0; i < mag.Capacity && mag.Rounds > 0; i++) { gc.Fire(); yield return new WaitForSeconds(mag.FireInterval + 0.05f); }
                float t0 = Time.time;
                while (!mag.Reloading && Time.time - t0 < 3f) yield return null;
                A23B($"{kind}: emptied, the gun starts its reload by itself and the ring is up", mag.Reloading);
                float start = mag.ReloadStartedAt;
                foreach (float frac in new[] { 0.35f, 0.7f })
                {
                    while (mag.Reloading && Time.time - start < mag.ReloadSeconds * frac) yield return null;
                    yield return Snap($"ring_{kind}_{(int)(frac * 100)}");
                }
                while (mag.Reloading) yield return null;
                yield return Snap("ring_" + kind + "_loaded");
            }
            Destroy(menu.gameObject);
            Log($"[Asks1023B] DONE fails={a23bFails}");
            Quit(a23bFails == 0 ? 0 : 1);
        }
    }
}
