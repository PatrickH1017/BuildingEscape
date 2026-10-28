using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The owner's combat list of 2026-10-27: set bullet damage by ammunition (light 25 / 40 head, medium 40 / 60, heavy 100, shotgun 75), headshots, the hit marker and its three sounds, hip fire and aiming. The pure rules are <c>NetDevTest.BulletDamageRulesTest</c> (PolicyTest); the Play half is the
    /// scenario <c>BulletDamage</c> (<c>qa_regress_bd.ps1 -Methods BulletDamage</c>; pictures need a real window: <c>ui.ps1 -Project C:\bd -Methods BulletDamage</c>, %TEMP%\be_ui_bd_*.png).
    /// </summary>
    public static partial class NetDevTest
    {
        public static void BulletDamageRulesTest()
        {
            var K = RangedWeapon.Kind.Pew;
            Check("bullet damage: light ammo 25 a hit and 40 a headshot (the pistol and the sub machine gun), medium 40 / 60 (the rifle), heavy 100 (the sniper rifle), a shotgun shot 75 (the owner's numbers)",
                BulletDamageRules.Damage(RangedWeapon.Kind.Pew, false) == 25f && BulletDamageRules.Damage(RangedWeapon.Kind.Pew, true) == 40f && BulletDamageRules.Damage(RangedWeapon.Kind.Smg, false) == 25f && BulletDamageRules.Damage(RangedWeapon.Kind.Smg, true) == 40f
                && BulletDamageRules.Damage(RangedWeapon.Kind.Rifle, false) == 40f && BulletDamageRules.Damage(RangedWeapon.Kind.Rifle, true) == 60f && BulletDamageRules.Damage(RangedWeapon.Kind.Sniper, false) == 100f && BulletDamageRules.Damage(RangedWeapon.Kind.Shotgun, false) == 75f);
            Check("bullet damage: the ammunition of each gun: pew and SMG light, rifle medium, sniper heavy, shotgun shells",
                BulletDamageRules.ClassOf(RangedWeapon.Kind.Pew) == AmmoClass.Light && BulletDamageRules.ClassOf(RangedWeapon.Kind.Smg) == AmmoClass.Light && BulletDamageRules.ClassOf(RangedWeapon.Kind.Rifle) == AmmoClass.Medium
                && BulletDamageRules.ClassOf(RangedWeapon.Kind.Sniper) == AmmoClass.Heavy && BulletDamageRules.ClassOf(RangedWeapon.Kind.Shotgun) == AmmoClass.Shotgun);
            bool headMore = true;
            foreach (AmmoClass c in System.Enum.GetValues(typeof(AmmoClass))) headMore &= BulletDamageRules.Damage(c, true) > BulletDamageRules.Damage(c, false);
            Check("bullet damage: a headshot does more than a body shot for every kind of ammunition (the heavy and shotgun headshot numbers are my placeholders)", headMore);
            Check("bullet damage: the gun table says the same: guardHits 2.5 / 2.5 / 4 / 7.5 / 10 hits of 10 HP, damage = the body shot", Mathf.Approximately(RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].guardHits, 2.5f) && Mathf.Approximately(RangedWeapon.Specs[(int)RangedWeapon.Kind.Smg].guardHits, 2.5f)
                && Mathf.Approximately(RangedWeapon.Specs[(int)RangedWeapon.Kind.Rifle].guardHits, 4f) && Mathf.Approximately(RangedWeapon.Specs[(int)RangedWeapon.Kind.Shotgun].guardHits, 7.5f) && Mathf.Approximately(RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper].guardHits, 10f)
                && RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].damage == 25f && RangedWeapon.Specs[(int)RangedWeapon.Kind.Rifle].damage == 40f && RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper].damage == 100f && RangedWeapon.Specs[(int)RangedWeapon.Kind.Shotgun].damage == 75f);
            Check("bullet damage: in the officers' own unit (hits of 10 HP) a pew body shot is 2.5 and a headshot 4, a rifle 4 / 6, a sniper 10 / 15, a shotgun shot 7.5 / 10",
                Mathf.Approximately(BulletDamageRules.Hits(K, false), 2.5f) && Mathf.Approximately(BulletDamageRules.Hits(K, true), 4f) && Mathf.Approximately(BulletDamageRules.Hits(RangedWeapon.Kind.Rifle, false), 4f) && Mathf.Approximately(BulletDamageRules.Hits(RangedWeapon.Kind.Rifle, true), 6f)
                && Mathf.Approximately(BulletDamageRules.Hits(RangedWeapon.Kind.Sniper, false), 10f) && Mathf.Approximately(BulletDamageRules.Hits(RangedWeapon.Kind.Sniper, true), 15f) && Mathf.Approximately(BulletDamageRules.Hits(RangedWeapon.Kind.Shotgun, false), 7.5f) && Mathf.Approximately(BulletDamageRules.Hits(RangedWeapon.Kind.Shotgun, true), 10f));
            Check("bullet damage: shots to knock out a 100 HP officer: pew 4 (3 headshots), rifle 3 (2 headshots), sniper 1, shotgun 2 (1 headshot)",
                BulletDamageRules.ShotsToKill(K, 100f, false) == 4 && BulletDamageRules.ShotsToKill(K, 100f, true) == 3 && BulletDamageRules.ShotsToKill(RangedWeapon.Kind.Rifle, 100f, false) == 3 && BulletDamageRules.ShotsToKill(RangedWeapon.Kind.Rifle, 100f, true) == 2
                && BulletDamageRules.ShotsToKill(RangedWeapon.Kind.Sniper, 100f, false) == 1 && BulletDamageRules.ShotsToKill(RangedWeapon.Kind.Shotgun, 100f, false) == 2 && BulletDamageRules.ShotsToKill(RangedWeapon.Kind.Shotgun, 100f, true) == 1);
            Check("bullet damage: a hit in the top fifth of a body is a headshot (feet 0, 2 m tall: from 1.6 m), anything lower is not, a body without height has no head",
                BulletDamageRules.IsHeadshot(1.61f, 0f, 2f) && BulletDamageRules.IsHeadshot(1.95f, 0f, 2f) && !BulletDamageRules.IsHeadshot(1.59f, 0f, 2f) && !BulletDamageRules.IsHeadshot(1f, 0f, 2f) && !BulletDamageRules.IsHeadshot(5f, 0f, 0f) && BulletDamageRules.IsHeadshot(2.61f, 1f, 2f) && !BulletDamageRules.IsHeadshot(2.5f, 1f, 2f));
            Check("hit feedback: an elimination wins over a headshot, a headshot over a plain hit; the stronger of two is kept",
                BulletDamageRules.Feedback(true, true) == HitKind.Kill && BulletDamageRules.Feedback(true, false) == HitKind.Kill && BulletDamageRules.Feedback(false, true) == HitKind.Head && BulletDamageRules.Feedback(false, false) == HitKind.Hit
                && BulletDamageRules.Stronger(HitKind.Hit, HitKind.Head) == HitKind.Head && BulletDamageRules.Stronger(HitKind.Kill, HitKind.Head) == HitKind.Kill && BulletDamageRules.Stronger(HitKind.None, HitKind.Hit) == HitKind.Hit);
            Check("hit feedback: each of the three has a sound of its own: the marker's tick, the headshot's ding, the elimination's thump",
                HitFeedback.SoundOf(HitKind.Hit) == SoundId.HitMarker && HitFeedback.SoundOf(HitKind.Head) == SoundId.HitHead && HitFeedback.SoundOf(HitKind.Kill) == SoundId.HitKill);
            var tick = OutpostSound.HitTick(); var ding = OutpostSound.HitHead(); var thump = OutpostSound.HitKill();
            float PeakOf(float[] a) { float p = 0f; foreach (var v in a) p = Mathf.Max(p, Mathf.Abs(v)); return p; }
            Check("hit feedback: the three generated sounds are real, different (lengths 0.1 / 0.3 / 0.4 s) and each one clip", tick.Length < ding.Length && ding.Length < thump.Length && PeakOf(tick) > 0.2f && PeakOf(ding) > 0.2f && PeakOf(thump) > 0.2f
                && SoundBank.Variants(SoundId.HitMarker) == 1 && SoundBank.Variants(SoundId.HitHead) == 1 && SoundBank.Variants(SoundId.HitKill) == 1, $"({tick.Length} / {ding.Length} / {thump.Length} samples)");

            // ---- one shot's bookkeeping: a shotgun's pellets deal the shot's damage once to each officer, a pellet in the head makes his share a headshot
            var tally = new ShotTally();
            var a1 = new object(); var a2 = new object();
            tally.Note(a1, false); tally.Note(a1, true); tally.Note(a1, false); tally.Note(a2, false);
            bool t1 = tally.Take(a1, out bool h1), t1b = tally.Take(a1, out bool h1b), t2 = tally.Take(a2, out bool h2);
            Check("shot tally: each officer takes a shot's damage once however many pellets hit him; one pellet in the head makes it a headshot; one who was not noted takes it once too", t1 && !t1b && h1 && h1b && t2 && !h2 && tally.Take(new object(), out _) && tally.HeadOf(a1) && !tally.HeadOf(a2));
            tally.Reset();
            Check("shot tally: a new shot starts clean", tally.Take(a1, out bool h3) && !h3);

            // ---- hip fire and aiming
            Check("aiming: from the hip a shot scatters 1.5 times wider than aimed; the gun is at half height at the hip (HipRaise 0.5) and takes about a sixth of a second to come up and to go down; the pose is eased at both ends",
                AimRules.SpreadFactor(false) == AimRules.HipSpread && AimRules.SpreadFactor(true) == 1f && Mathf.Abs(AimRules.HipSpread - 1.5f) < 1e-5f && Mathf.Abs(AimRules.Ease(0f, true, AimRules.AimSeconds * 0.5f) - 0.5f) < 1e-4f && AimRules.Ease(0.5f, true, 1f) == 1f && AimRules.Ease(1f, false, 1f) == 0f
                && Mathf.Abs(AimRules.Ease(1f, false, AimRules.AimSeconds * 0.5f) - 0.5f) < 1e-4f && Mathf.Abs(AimRules.Smooth(0.5f) - 0.5f) < 1e-4f && AimRules.Smooth(0f) == 0f && AimRules.Smooth(1f) == 1f && AimRules.Ease(0.3f, true, 0f) == 0.3f && AimRules.HipRaise == 0.5f && AimRules.RaiseFor(true) == 1f && AimRules.RaiseFor(false) == AimRules.HipRaise);
        }
    }

    public partial class NetDevRunner
    {
        int bdFails;

        void BdCheck(string name, bool ok, string detail = "")
        {
            if (!ok) bdFails++;
            Log($"[BulletDamage] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static readonly BindingFlags BdAny = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        IEnumerator BulletDamageScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            HeistManager.TestPartySize = 3;
            GunController.LimitedAmmoForTests = true;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(0.6f);
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var gc = player.GetComponent<GunController>();
            var rig = FindFirstObjectByType<CameraFollow>();
            if (rig) rig.enabled = false;
            var cam = Camera.main;
            var menu = new GameObject("TestModeForTest").AddComponent<TestMode>();
            var giveGun = typeof(TestMode).GetMethod("GiveGun", BdAny);
            var fState = typeof(GuardAI).GetField("state", BdAny);
            BdCheck("(test) the test menu can hand over guns and the pack, the gun controller and a guard to shoot are there", giveGun != null && pack != null && gc != null && fState != null && GuardAI.All.Count(g => g && !g.Dead && !g.IsBoss && !g.IsBackup && g.name != "Guard_Outpost") >= 4);
            if (giveGun == null || pack == null || gc == null) { Log($"[BulletDamage] DONE fails={bdFails}"); Quit(1); yield break; }

            // a guard on duty 6 m from a spot with a clear line, standing still, not looking for anybody
            System.Collections.Generic.List<GuardAI> candidates = null;
            GuardAI Pick(int skip, out Vector3 spotOut, float dist)
            {
                spotOut = default;
                for (int k = skip; k < candidates.Count; k++)
                {
                    var cand = candidates[k];
                    if (cand == null || cand.Dead) continue;
                    if (!OpenSpot(cand.transform.position + Vector3.up * 0.9f, dist, out var s, true)) continue;
                    fState.SetValue(cand, GuardState.Patrol);
                    GuardSightFreeze(cand);
                    cand.sightRange = 0.1f;                                                   // (he does not turn on the player)
                    spotOut = s;
                    return cand;
                }
                return null;
            }
            float HpLost(GuardAI g) => GuardHealthRules.HitsToHp(g.Damage);

            var kinds = new[] { RangedWeapon.Kind.Pew, RangedWeapon.Kind.Smg, RangedWeapon.Kind.Rifle, RangedWeapon.Kind.Shotgun, RangedWeapon.Kind.Sniper };
            int idx = 0;
            Pickup previous = null;
            foreach (var kind in kinds)
            {
                var spec = RangedWeapon.Specs[(int)kind];
                string tag = spec.name + ":";
                yield return LoadLevel(hm, 1);                                                  // (a fresh floor for each gun: new officers, none of them down)
                yield return new WaitForSeconds(0.6f);
                player = WobblyPlayerRegistry.All[0]; pack = player.GetComponent<Backpack>(); gc = player.GetComponent<GunController>(); cam = Camera.main;
                rig = FindFirstObjectByType<CameraFollow>(); if (rig) rig.enabled = false;
                candidates = GuardAI.All.Where(g => g && !g.Dead && !g.IsBoss && !g.IsBackup && g.name != "Guard_Outpost").ToList();
                var g = Pick(0, out var spot, kind == RangedWeapon.Kind.Shotgun ? 2.2f : kind == RangedWeapon.Kind.Sniper ? 6f : 2.5f);          // (the SMG's cone is 3.4 degrees: from 6 m a shot can miss a head; from 2.5 m it cannot)
                BdCheck($"{tag} (test) a guard to shoot, with a clear line", g != null);
                if (g == null) continue;
                if (previous) { pack.Drop(previous); Destroy(previous.gameObject); previous = null; yield return new WaitForSeconds(0.3f); }
                giveGun.Invoke(menu, new object[] { pack, kind });
                yield return new WaitForSeconds(0.9f);
                previous = pack.Items.FirstOrDefault(i => i && i.TryGetComponent(out RangedWeapon rw) && rw.kind == kind);
                player.TeleportTo(spot + Vector3.up * 1.1f, Quaternion.identity);
                yield return new WaitForSeconds(0.3f);
                GunController.TestAim = true;                                                 // (aiming: the narrowest cone)
                float s = Mathf.Abs(g.transform.lossyScale.y);
                System.Func<Vector3> bodyPt = () => g.transform.position + Vector3.up * (0.15f * s);          // (the chest: a body shot; where he stands NOW: a bullet shoves him back)
                System.Func<Vector3> headPt = () => g.transform.position + Vector3.up * (0.85f * s);          // (the head: a headshot, above 0.6 over his middle)
                BdCheck($"{tag} the guard's head is where the rule says (above 0.6 m over his middle) and his body is not", g.IsHeadHit(headPt().y) && !g.IsHeadHit(bodyPt().y));

                // ---- a body shot: the ammunition's damage
                float before = HpLost(g);
                int count0 = HitFeedback.Count;
                float took = 0f; bool fired = false, bodyLanded = false; int markersOfShot = 0; string bwhy = "";
                for (int attempt = 0; attempt < 4 && !bodyLanded; attempt++)
                {
                    float beforeTry = HpLost(g); int countTry = HitFeedback.Count;
                    GunController.TestAimPoint = bodyPt(); AimCamera(cam, player, bodyPt());
                    yield return null;
                    fired = gc.Fire();
                    yield return new WaitForSeconds(0.15f);
                    var bimp = gc.LastImpact;
                    bool landed = HpLost(g) - beforeTry > 0.01f;
                    bwhy += $"[try {attempt}: fired {fired}, bullet hit {(bimp.hit ? (bimp.guard != null ? bimp.guard.name : bimp.collider ? bimp.collider.name : "?") : "nothing")}, he took {HpLost(g) - beforeTry:0.0}] ";
                    if (landed) { bodyLanded = true; took = HpLost(g) - beforeTry; markersOfShot = HitFeedback.Count - countTry; }
                    else if (!g.Dead) yield return new WaitForSeconds(Mathf.Max(0.3f, spec.fireInterval + 0.2f));
                }
                float expectBody = BulletDamageRules.Damage(kind, false);
                if (kind == RangedWeapon.Kind.Sniper) BdCheck($"{tag} a body shot takes the heavy ammunition's 100 HP and eliminates the officer (the one shot)", bodyLanded && g.Dead && took >= 99.9f, $"(took {took:0.0}, dead {g.Dead}) {bwhy}");
                else BdCheck($"{tag} a body shot takes {expectBody} HP{(kind == RangedWeapon.Kind.Shotgun ? " once, though eight pellets were fired" : "")}", bodyLanded && Mathf.Abs(took - expectBody) < 0.01f && !g.Dead, $"(took {took:0.0}, fired {fired}) {bwhy}");
                BdCheck($"{tag} the hit marker flashed: ONE for the shot, a hit (or the elimination), with its sound", bodyLanded && markersOfShot == 1 && (HitFeedback.Last == (g.Dead ? HitKind.Kill : HitKind.Hit) || HitFeedback.Last == HitKind.Kill) && (HitFeedback.LastSound == (g.Dead ? SoundId.HitKill : SoundId.HitMarker) || HitFeedback.LastSound == SoundId.HitKill), $"({HitFeedback.Last}, {HitFeedback.LastSound}, markers {markersOfShot})");
                if (g.Dead) continue;

                // ---- a headshot on the same guard: more, with the headshot's own marker and sound
                yield return new WaitForSeconds(Mathf.Max(0.4f, spec.fireInterval + 0.3f));
                before = HpLost(g);
                count0 = HitFeedback.Count;
                string why = "";
                float headTook = -1f; bool headLanded = false;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    float beforeTry = HpLost(g);
                    GunController.TestAimPoint = headPt(); AimCamera(cam, player, headPt());
                    yield return null;
                    fired = gc.Fire();
                    yield return new WaitForSeconds(0.15f);
                    var imp = gc.LastImpact;
                    bool landed = fired && imp.hit && imp.guard == g;
                    bool headNow = landed && g.IsHeadHit(imp.point.y);
                    why += $"[try {attempt}: fired {fired}, landed {landed}, {(landed ? imp.point.y - g.transform.position.y : float.NaN):0.00} m over his middle, head {headNow}, took {HpLost(g) - beforeTry:0.0}, state {g.State}] ";
                    if (headNow) { headLanded = true; headTook = HpLost(g) - beforeTry; break; }
                    if (g.Dead) break;
                    yield return new WaitForSeconds(Mathf.Max(0.3f, spec.fireInterval + 0.2f));
                }
                took = HpLost(g) - before;
                float expectHead = BulletDamageRules.Damage(kind, true);
                if (kind == RangedWeapon.Kind.Shotgun) BdCheck($"{tag} a shot whose pellets reach the head takes 100 HP (a headshot) or the 75 of a body shot when none does", fired && (Mathf.Abs(took - 100f) < 0.01f || Mathf.Abs(took - 75f) < 0.01f || g.Dead), $"(took {took:0.0}, dead {g.Dead})");
                else BdCheck($"{tag} a bullet that lands in the head takes {expectHead} HP, more than the {expectBody} of a body shot", headLanded && (Mathf.Abs(headTook - expectHead) < 0.01f || g.Dead) && expectHead > expectBody, $"(the headshot took {headTook:0.0}, dead {g.Dead}) {why}");
                bool headFeedback = HitFeedback.Count >= count0 + 1 && (HitFeedback.Last == HitKind.Head || HitFeedback.Last == HitKind.Kill);          // (at least one: a retry or a second officer in the line of the pellets gives more; the exact count is the body shot's check)
                BdCheck($"{tag} the headshot has its marker and its sound (the ding; the thump when it eliminates)", headFeedback && (HitFeedback.LastSound == SoundId.HitHead || HitFeedback.LastSound == SoundId.HitKill), $"({HitFeedback.Last}, {HitFeedback.LastSound}, markers {HitFeedback.Count - count0})");

                // ---- keep shooting: the elimination has its own marker and sound
                int safety = 0;
                while (!g.Dead && safety++ < 8) { yield return new WaitForSeconds(Mathf.Max(0.12f, spec.fireInterval + 0.1f)); GunController.TestAimPoint = bodyPt(); AimCamera(cam, player, bodyPt()); gc.Fire(); yield return new WaitForSeconds(0.1f); }
                BdCheck($"{tag} the shots that eliminate him end with the elimination's marker and sound", g.Dead && HitFeedback.Last == HitKind.Kill && HitFeedback.LastSound == SoundId.HitKill, $"(dead {g.Dead}, {HitFeedback.Last}, {HitFeedback.LastSound})");
            }
            GunController.TestAimPoint = null; GunController.TestAim = null;

            // ---- a shot at nothing makes no marker
            int c1 = HitFeedback.Count;
            GunController.TestAim = true;
            GunController.TestAimPoint = player.transform.position + new Vector3(0f, 40f, 0f); AimCamera(cam, player, GunController.TestAimPoint.Value);
            yield return new WaitForSeconds(0.5f);
            gc.Fire();
            yield return new WaitForSeconds(0.2f);
            BdCheck("a shot at the sky makes no hit marker and no sound", HitFeedback.Count == c1);
            GunController.TestAimPoint = null; GunController.TestAim = null;
            GunController.LimitedAmmoForTests = false;

            // ---- a networked run: the guess and the host's answer make ONE marker for a plain hit, and the stronger one for a headshot or an elimination (the guess is forgotten after a second)
            int g0 = HitFeedback.Count;
            HitFeedback.Guess(); HitFeedback.Answer(HitKind.Hit);
            BdCheck("(net feedback) a guessed hit and the host's answer for it are ONE marker", HitFeedback.Count == g0 + 1 && HitFeedback.Last == HitKind.Hit, $"({HitFeedback.Count - g0} markers)");
            HitFeedback.Guess(); HitFeedback.Answer(HitKind.Kill);
            BdCheck("(net feedback) a guess and the host's answer that it was an elimination: the plain marker, then the elimination's", HitFeedback.Count == g0 + 3 && HitFeedback.Last == HitKind.Kill && HitFeedback.LastSound == SoundId.HitKill, $"({HitFeedback.Count - g0} markers, last {HitFeedback.Last})");
            HitFeedback.Answer(HitKind.Head);
            BdCheck("(net feedback) an answer with no guess before it plays as it is (a headshot on a foe the crosshair was not on)", HitFeedback.Count == g0 + 4 && HitFeedback.Last == HitKind.Head, $"({HitFeedback.Count - g0} markers, last {HitFeedback.Last})");
            HitFeedback.Guess();
            yield return new WaitForSecondsRealtime(HitFeedback.GuessLife + 0.2f);
            HitFeedback.Answer(HitKind.Hit);
            BdCheck("(net feedback) a guess the host never answered is forgotten after a second: a later plain hit is shown", HitFeedback.Count == g0 + 6 && HitFeedback.Last == HitKind.Hit, $"({HitFeedback.Count - g0} markers)");

            // ---- the dev menu's alarm buttons (the owner, 2026-10-27: "the dev menu buttons for getting rid of wanted level/backup don't work")
            HeistManager.TestPartySize = 0;
            var devPlayer = WobblyPlayerRegistry.All[0];
            bool bigNow = hm.MarkSpotted(devPlayer);
            hm.CallBackup(null);
            yield return new WaitForSeconds(1.5f);
            int backups = GuardAI.All.Count(g => g && !g.Dead && g.IsBackup);
            BdCheck("(dev alarm) a solo sighting: the Big Alarm is on, the player is marked and the backup is on with officers on their way", bigNow && hm.BigAlarmActive && hm.MarkedCount == 1 && hm.BackupActive && backups >= 1, $"(big {hm.BigAlarmActive}, marked {hm.MarkedCount}, backup {hm.BackupActive}, backup officers {backups})");
            BdCheck("(dev alarm) the guards' alarm panel rule is unchanged: StandDown cannot shut the Big Alarm off", !hm.StandDown() && hm.BigAlarmActive);
            bool cleared = hm.DevClearAlert();
            yield return null; yield return null;                                          // (the officers the backup called are destroyed at the end of the frame; nobody has had the time to notice the player again)
            int backupsLeft = GuardAI.All.Count(g => g && !g.Dead && g.IsBackup);
            BdCheck("(dev alarm) the dev menu's button works: the Big Alarm, the mark and the backup are gone, and so are the officers the backup called", cleared && !hm.BigAlarmActive && !hm.BackupActive && hm.MarkedCount == 0 && backupsLeft == 0, $"(cleared {cleared}, big {hm.BigAlarmActive}, marked {hm.MarkedCount}, backup {hm.BackupActive}, backup officers left {backupsLeft})");
            BdCheck("(dev alarm) nobody is still after anybody: no officer is chasing, investigating, calling or holding somebody down", GuardAI.All.All(g => !g || g.Dead || g.State == GuardState.Patrol || g.State == GuardState.Stunned), $"({string.Join(", ", GuardAI.All.Where(g => g && !g.Dead).Select(g => g.State.ToString()).Distinct())})");
            BdCheck("(dev alarm) pressed again with nothing going it says so", !hm.DevClearAlert());

            // ---- the picture of the marker (a real window)
            if (CanPicture)
            {
                HitFeedback.Play(HitKind.Head);
                yield return null;
                yield return Snap("bd_1_head");
                HitFeedback.Play(HitKind.Kill);
                yield return null;
                yield return Snap("bd_2_kill");
            }
            Log($"[BulletDamage] DONE fails={bdFails}");
            Quit(bdFails == 0 ? 0 : 1);
        }
    }
}
