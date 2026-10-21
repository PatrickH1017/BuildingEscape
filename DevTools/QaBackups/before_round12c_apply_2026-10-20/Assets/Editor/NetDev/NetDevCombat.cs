using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode check of the combat model (the owner's queue of 2026-10-19): who carries what on which floor, the vest of each shield tier, what bullets do to a guard through each shield (damage, never a knock-down, a look at where it came from),
    /// what a melee blow does (knocks over for the weapon's own time, never knocks out), that a shot guard lets go of the player he pins, the clip an armed guard drops and the player's taking of it, a rifle's hit that only knocks the player down
    /// and a shotgun's and a sniper rifle's that end the player's floor with a blast.
    /// </summary>
    public partial class NetDevRunner
    {
        int combatFails;

        void CombatCheck(string name, bool ok, string detail = "")
        {
            if (!ok) combatFails++;
            Log($"[Combat] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static bool HasVest(GuardAI g) => g.GetComponentsInChildren<Transform>(true).Any(t => t.name == "BodyArmour");
        static int VestParts(GuardAI g) { var v = g.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BodyArmour"); return v ? v.childCount : 0; }
        static bool Carries(GuardAI g, RangedWeapon.Kind k) => g.Loadout.Armed && g.Loadout.Kind == k;
        static string Mix(IEnumerable<GuardAI> gs) => string.Join(", ", gs.Select(g => g.Loadout.ToString()).GroupBy(s => s).Select(x => x.Count() + " x " + x.Key));
        static List<GuardAI> Patrolling() => GuardAI.All.Where(g => g && g.enabled && !g.IsPuppet && !g.IsBoss && g.name != "Guard_Outpost" && !g.Dead).ToList();

        // results of a duel (CombatDuel); a guard who has fought one is not used again (he may still be shooting at the player, in god mode, when the next starts)
        bool duelStumbled, duelBusted; int duelHits, duelBlasts; float duelStumbleSeconds;
        readonly HashSet<GuardAI> duelShooters = new HashSet<GuardAI>();

        /// <summary>An armed guard of this kind shoots at the player from <paramref name="dist"/> metres (god mode off, nobody else sees him): did the hit knock him down, did it blow him up?</summary>
        IEnumerator CombatDuel(RangedWeapon.Kind kind, float dist, string label, bool oneShot = false)
        {
            var hm = HeistManager.Instance;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            duelStumbled = duelBusted = false; duelHits = 0; duelBlasts = 0;
            var fState = typeof(GuardAI).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
            var fTarget = typeof(GuardAI).GetField("target", BindingFlags.NonPublic | BindingFlags.Instance);
            var fUnseen = typeof(GuardAI).GetField("unseenFor", BindingFlags.NonPublic | BindingFlags.Instance);
            GuardAI shooter = null; Vector3 spot = default;
            foreach (var g in Patrolling())
                if (!duelShooters.Contains(g) && OpenSpot(g.transform.position, dist, out spot, false)) { shooter = g; break; }
            CombatCheck($"{label}: found a guard with a clear {dist:0} m line to the player", shooter != null);
            if (shooter == null) yield break;
            duelShooters.Add(shooter);
            shooter.SetLoadout(GuardLoadout.Of(kind));
            shooter.SyncGun();
            yield return new WaitForSeconds(0.7f);
            // (he walked on while his gun was made: the spot is worked out again from where he stands now, a partition may be between him and the first one)
            Vector3 spotNow = spot; bool clearNow = false;
            for (int retry = 0; retry < 6 && !clearNow; retry++)
            {
                clearNow = OpenSpot(shooter.transform.position, dist, out spotNow, false);
                if (!clearNow) yield return new WaitForSeconds(0.3f);
            }
            if (clearNow) spot = spotNow; else Log($"[Combat] {label}: {shooter.name} has no clear line at {dist:0} m from where he stands now: the first spot is used");
            var gg = shooter.Gun;
            CombatCheck($"{label}: he carries a {kind} in his hand", gg != null && gg.kind == kind && gg.HasModel);
            if (gg == null) yield break;
            gg.baseSpreadDegrees = 0.3f; gg.spreadPerMetre = 0f;                       // (so that the test does not depend on luck: a shotgun's pellets keep their own spread)
            shooter.sightRange = 14f;
            hm.godMode = false;
            player.PinImmuneUntil = Time.time + 600f;                                  // (so a pin from another guard cannot spoil it)
            player.TeleportTo(spot + Vector3.up * 1.1f, player.transform.rotation);
            Face(player, shooter.transform.position, rb);
            var to = player.transform.position - shooter.transform.position; to.y = 0f;
            shooter.transform.rotation = Quaternion.LookRotation(to.normalized);
            fTarget.SetValue(shooter, player); fUnseen.SetValue(shooter, 0f); fState.SetValue(shooter, GuardState.Chase);
            shooter.GetComponent<NavMeshAgent>().speed = shooter.chaseSpeed;
            int blasts0 = BlastFx.Played, hits0 = gg.PlayerHits;
            float t0 = Time.time;
            while (Time.time - t0 < 10f && gg.PlayerHits <= hits0) { duelStumbled |= player.IsStumbling; yield return null; }
            if (oneShot) { shooter.SetLoadout(GuardLoadout.Unarmed); shooter.SyncGun(); }        // (one bullet only: the time of its knock-down is what is measured)
            // how long the knock-down lasts (armour shortens it): from the hit until he is up again
            float tHit = Time.time, lastDown = -1f;
            while (Time.time - tHit < 4f)
            {
                if (player.IsStumbling) { duelStumbled = true; lastDown = Time.time; }
                else if (lastDown >= 0f && Time.time - lastDown > 0.1f) break;                 // (he is up again)
                else if (lastDown < 0f && Time.time - tHit > 0.5f) break;                      // (he never went down)
                yield return null;
            }
            duelStumbleSeconds = lastDown >= 0f ? lastDown - tHit : 0f;
            yield return new WaitForSeconds(0.2f);
            duelHits = gg.PlayerHits - hits0;
            duelBusted = player.Custody == CustodyState.Busted;
            duelBlasts = BlastFx.Played - blasts0;
            hm.godMode = true;
        }

        IEnumerator CombatScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            bool gfx = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var pack = player.GetComponent<Backpack>();
            var cam = Camera.main;
            var rig = FindFirstObjectByType<CameraFollow>();
            int pewBullet = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].guardHits;
            var fStun = typeof(GuardAI).GetField("stunUntil", BindingFlags.NonPublic | BindingFlags.Instance);
            CombatCheck("(test) the guard's stun time is where the test expects it", fStun != null);

            // ================= 1. floor 3: the first armed guards - a pistol and a light shield
            yield return R5LoadFloor(3);
            player = WobblyPlayerRegistry.All[0]; rb = player.GetComponent<Rigidbody>(); pack = player.GetComponent<Backpack>(); cam = Camera.main; rig = FindFirstObjectByType<CameraFollow>();
            var all3 = Patrolling();
            var armed3 = all3.Where(g => g.Loadout.Armed).ToList();
            CombatCheck("floor 3: some of the patrolling guards are armed, and not all of them", armed3.Count >= 1 && armed3.Count < all3.Count, $"({armed3.Count} of {all3.Count}: {Mix(all3)})");
            CombatCheck("floor 3: the armed ones carry the pew and wear a light shield - the gun in the hand, the vest on the chest", armed3.All(g => Carries(g, RangedWeapon.Kind.Pew) && g.Shield == ShieldTier.Light && HasVest(g) && VestParts(g) == 1 && g.Gun != null && g.Gun.kind == RangedWeapon.Kind.Pew && g.Gun.HasModel));
            CombatCheck("floor 3: the others carry nothing and wear nothing", all3.Where(g => !g.Loadout.Armed).All(g => g.Shield == ShieldTier.None && !HasVest(g) && g.Gun == null));
            CombatCheck("floor 3: the loadout is the plan of the floor (the same pew guards the plan says)", armed3.Count >= 1 && armed3.Count <= GuardLoadouts.ArmedShare(3) * GuardRules.PatrolGuards(3, 3) + 1.01f, $"(share {GuardLoadouts.ArmedShare(3)})");

            // ================= 2. what bullets do through each shield; the vests; no clip from a guard with no gun
            var bare = all3.Where(g => !g.Loadout.Armed && !g.Dead).ToList();
            var tiers = new[] { ShieldTier.None, ShieldTier.Light, ShieldTier.Medium, ShieldTier.Heavy };
            var expectBullets = new[] { 3, 4, 5, 9 };
            var expectParts = new[] { 0, 1, 3, 5 };
            int dropped0 = GuardDrops.Dropped;
            if (bare.Count >= 4)
            {
                for (int i = 0; i < 4; i++) bare[i].SetLoadout(new GuardLoadout(false, RangedWeapon.Kind.Pew, tiers[i]));
                yield return null; yield return null;                              // (GuardVisual puts the vests on)
                for (int i = 0; i < 4; i++)
                {
                    var g = bare[i];
                    CombatCheck($"{tiers[i]} shield: the vest on his chest has {expectParts[i]} part(s)", VestParts(g) == expectParts[i] && HasVest(g) == (expectParts[i] > 0), $"({VestParts(g)})");
                    int n = 0;
                    while (!g.Dead && n < 40) { g.BulletHitHost(pewBullet, false, null); n++; }
                    CombatCheck($"{tiers[i]} shield: {expectBullets[i]} pew bullets put the guard down for good", g.Dead && n == expectBullets[i], $"({n} bullets, hits {g.Hits})");
                }
                CombatCheck("a guard who carries no gun drops no clip", GuardDrops.Dropped == dropped0);
            }
            else CombatCheck("floor 3 has four unarmed guards to shoot at", false, $"({bare.Count})");

            var victim = armed3.FirstOrDefault(g => !g.Dead);
            if (victim != null)
            {
                var vpos = victim.transform.position;
                int n = 0;
                while (!victim.Dead && n < 40) { victim.BulletHitHost(pewBullet, false, null); n++; }
                yield return null;
                var clip = FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).OrderBy(c => Vector3.Distance(c.transform.position, vpos)).FirstOrDefault();
                CombatCheck("an armed guard (pistol, light shield) goes down after four bullets and drops one clip of pistol ammunition where he fell",
                    victim.Dead && n == 4 && GuardDrops.Dropped == dropped0 + 1 && clip != null && clip.kind == RangedWeapon.Kind.Pew && clip.rounds == RangedWeapon.Specs[0].magazine && Vector3.Distance(clip.transform.position, vpos) < 2.5f,
                    clip != null ? $"({n} bullets, clip of {clip.rounds} for {clip.kind} {Vector3.Distance(clip.transform.position, vpos):0.0} m away)" : $"({n} bullets, no clip)");
                var menu = new GameObject("TestModeForTest").AddComponent<TestMode>();
                typeof(TestMode).GetMethod("GiveGun", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(menu, new object[] { pack, RangedWeapon.Kind.Pew });
                yield return new WaitForSeconds(0.6f);
                var pewGun = pack.Equipped ? pack.Equipped.GetComponent<RangedWeapon>() : null;
                CombatCheck("(test) the player has a pew in his hand", pewGun != null);
                if (pewGun != null && clip != null)
                {
                    pewGun.Magazine.SetReserve(0);
                    bool taken = pack.TryStore(clip.GetComponent<Pickup>());
                    yield return null;
                    CombatCheck("the dropped clip is taken like any spare clip: its eight rounds go into the pew's reserve and the clip is gone", pewGun.Magazine.Reserve == RangedWeapon.Specs[0].magazine && clip == null, $"(taken {taken}, reserve {pewGun.Magazine.Reserve})");
                }
            }
            else CombatCheck("floor 3 has an armed guard to shoot at", false);

            // ================= 3. floor 4: a bullet does not knock over, a shot guard lets go, a blow knocks over and never knocks out
            yield return R5LoadFloor(4);
            player = WobblyPlayerRegistry.All[0]; rb = player.GetComponent<Rigidbody>(); pack = player.GetComponent<Backpack>(); cam = Camera.main;
            var all4 = Patrolling();
            var bare4 = all4.Where(g => !g.Loadout.Armed).ToList();
            CombatCheck("floor 4: some guards armed with the pew and a light shield, at least five unarmed ones to try things on", all4.Count(g => g.Loadout.Armed) >= 2 && bare4.Count >= 5 && bare4.All(g => g.Shield == ShieldTier.None), $"({Mix(all4)})");
            if (bare4.Count >= 5)
            {
                // ---- a bullet: damage, no knock-over, he goes to look where it came from
                var a = bare4[0];
                var before = a.State;
                a.BulletHitHost(pewBullet, false, player.gameObject);
                bool lookedAround = before == GuardState.Chase || before == GuardState.Stunned || a.State == GuardState.Investigate;
                CombatCheck("a bullet does its four hits to a guard, does not knock him over and sends him to look where it came from", a.Hits == pewBullet && a.State != GuardState.Stunned && !a.Dead && lookedAround, $"(was {before}, now {a.State}, hits {a.Hits})");

                // ---- shot while he holds a player down: the player is free and he has let go
                a.SetLoadout(a.Loadout);                                           // (no change)
                player.PinnedBy = a;
                player.ApplyCustody(CustodyState.Pinned, 12f, 0);
                a.BeginHold(player);
                yield return null;
                CombatCheck("(test) the guard holds the player down", a.State == GuardState.Pinning && a.Holding == player && player.Custody == CustodyState.Pinned);
                a.BulletHitHost(pewBullet, false, player.gameObject);
                yield return null;
                CombatCheck("shot while he pins a player: the player is free and he is not pinning any more (and not knocked over, not down)", player.Custody == CustodyState.Free && a.Holding == null && a.State != GuardState.Pinning && a.State != GuardState.Stunned && !a.Dead,
                    $"(player {player.Custody}, guard {a.State}, hits {a.Hits})");

                // ---- a blow while he holds a player down: free, and he is knocked over
                var b = bare4[1];
                player.PinImmuneUntil = 0f;
                player.PinnedBy = b;
                player.ApplyCustody(CustodyState.Pinned, 12f, 0);
                b.BeginHold(player);
                yield return null;
                b.MeleeHitHost(4f, player.gameObject);
                yield return null;
                CombatCheck("a blow while he pins a player: the player is free and he is knocked over", player.Custody == CustodyState.Free && b.Holding == null && b.State == GuardState.Stunned && !b.Dead, $"(player {player.Custody}, guard {b.State})");

                // ---- a blow knocks over for the weapon's own time (bat 3 s, crowbar 4 s, hatchet 6 s) and counts for nothing
                var specs = MeleeWeapon.Specs;
                bool timesOk = true; string timesText = "";
                for (int i = 0; i < specs.Length && i + 2 < bare4.Count; i++)
                {
                    var g = bare4[i + 2];
                    g.MeleeHitHost(specs[i].stun, player.gameObject);
                    float left = (float)fStun.GetValue(g) - Time.time;
                    timesOk &= g.State == GuardState.Stunned && Mathf.Abs(left - specs[i].stun) < 0.3f && g.Hits == 0 && !g.Dead;
                    timesText += $"{specs[i].name} {left:0.0} s; ";
                }
                CombatCheck("a blow knocks the guard over for the weapon's own time and does not count towards knocking him out", timesOk, $"({timesText})");
                var s0 = specs.OrderBy(s => s.stun).First(); var s2 = specs.OrderBy(s => s.stun).Last();
                CombatCheck("the better the weapon the longer the guard stays down (bat 3 s < crowbar 4 s < hatchet 6 s)", s0.stun < s2.stun && specs.Length == 3 && specs.Select(s => s.stun).Distinct().Count() == 3);
                var c = bare4[Mathf.Min(bare4.Count - 1, 4)];
                int hitsBefore = c.Hits;
                for (int i = 0; i < 12; i++) c.MeleeHitHost(0.05f, player.gameObject);
                CombatCheck("twelve blows never knock a guard out: no damage, still alive", !c.Dead && c.Hits == hitsBefore, $"(hits {hitsBefore} -> {c.Hits})");
            }

            // ================= 4. the loadouts of the higher floors
            foreach (int floor in new[] { 5, 6, 8 })
            {
                yield return R5LoadFloor(floor);
                var gs = Patrolling();
                int pew = gs.Count(g => Carries(g, RangedWeapon.Kind.Pew)), smg = gs.Count(g => Carries(g, RangedWeapon.Kind.Smg)), rifle = gs.Count(g => Carries(g, RangedWeapon.Kind.Rifle)), shotgun = gs.Count(g => Carries(g, RangedWeapon.Kind.Shotgun)), sniper = gs.Count(g => Carries(g, RangedWeapon.Kind.Sniper));
                bool shields = gs.All(g => g.Loadout.Armed ? g.Shield == GuardLoadouts.ShieldOf(g.Loadout.Kind) && g.Gun != null && g.Gun.kind == g.Loadout.Kind : g.Shield == ShieldTier.None && g.Gun == null);
                string f = $"floor {floor}:";
                if (floor == 5) CombatCheck($"{f} SMG and pistol guards, nothing heavier", smg >= 1 && pew >= 1 && rifle + shotgun + sniper == 0 && shields, $"({Mix(gs)})");
                if (floor == 6) CombatCheck($"{f} one shotgun guard besides the SMGs and the pistols", shotgun == 1 && smg >= 1 && rifle + sniper == 0 && shields, $"({Mix(gs)})");
                if (floor == 8) CombatCheck($"{f} only assault rifles and two shotguns - no pistols, no SMGs, no snipers", rifle >= 3 && shotgun >= 1 && shotgun <= 2 && pew + smg + sniper == 0 && shields, $"({Mix(gs)})");
                CombatCheck($"{f} every gun is the kind of his loadout and every shield the one that goes with it", shields);
            }

            // ================= 5. floor 10: snipers; every gun's look; the shots at the player
            yield return R5LoadFloor(10);
            player = WobblyPlayerRegistry.All[0]; rb = player.GetComponent<Rigidbody>(); pack = player.GetComponent<Backpack>(); cam = Camera.main; rig = FindFirstObjectByType<CameraFollow>();
            var all10 = Patrolling();
            CombatCheck("floor 10: two snipers (no shield) with the rifles and the two shotguns - no pistols, no SMGs",
                all10.Count(g => Carries(g, RangedWeapon.Kind.Sniper) && g.Shield == ShieldTier.None) >= 1 && all10.Count(g => Carries(g, RangedWeapon.Kind.Sniper)) <= 2 && all10.Count(g => Carries(g, RangedWeapon.Kind.Shotgun)) <= 2
                && all10.Count(g => Carries(g, RangedWeapon.Kind.Rifle)) >= 3 && all10.Count(g => Carries(g, RangedWeapon.Kind.Pew) || Carries(g, RangedWeapon.Kind.Smg)) == 0, $"({Mix(all10)})");

            if (gfx)
            {
                // ---- every kind of guard, one picture each (the guards stand still)
                if (rig) rig.enabled = false;
                var look = new[] { GuardLoadout.Unarmed, GuardLoadout.Of(RangedWeapon.Kind.Pew), GuardLoadout.Of(RangedWeapon.Kind.Smg), GuardLoadout.Of(RangedWeapon.Kind.Rifle), GuardLoadout.Of(RangedWeapon.Kind.Shotgun), GuardLoadout.Of(RangedWeapon.Kind.Sniper) };
                var names = new[] { "unarmed", "pistol_light", "smg_light", "rifle_medium", "shotgun_heavy", "sniper_none" };
                var pool = Patrolling();
                for (int i = 0; i < look.Length && i < pool.Count; i++)
                {
                    var g = pool[i];
                    g.SetLoadout(look[i]); g.SyncGun();
                    g.enabled = false;
                    var ag = g.GetComponent<NavMeshAgent>(); if (ag && ag.isOnNavMesh) { ag.isStopped = true; ag.velocity = Vector3.zero; }
                }
                yield return new WaitForSeconds(0.9f);
                for (int i = 0; i < look.Length && i < pool.Count; i++)
                {
                    var g = pool[i];
                    Vector3 spot = default;
                    if (!(OpenSpot(g.transform.position, 3.2f, out spot, false) || OpenSpot(g.transform.position, 2.6f, out spot, false) || OpenSpot(g.transform.position, 2.0f, out spot, false))) { Log($"SKIP picture of {names[i]}: no clear spot"); continue; }
                    var eye = new GameObject("CombatEye").AddComponent<Camera>();
                    eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = new Color(0.2f, 0.22f, 0.26f); eye.fieldOfView = 36f; eye.enabled = false;
                    eye.transform.position = spot + Vector3.up * 1.25f;                              // (a body's pivot is its middle, about a metre up: the chest is a little above it)
                    eye.transform.LookAt(g.transform.position + Vector3.up * 0.3f);
                    var face = g.transform.position - spot; face.y = 0f;
                    if (face.sqrMagnitude > 0.01f) g.transform.rotation = Quaternion.LookRotation(-face.normalized);          // (he faces the camera)
                    yield return null; yield return null;
                    ShotCam(eye, "combat_guard_" + names[i]);
                    Destroy(eye.gameObject);
                }
            }

            // ---- the blast (pictures, with graphics only): on the player, who stands in the open; the camera looks at him from a clear spot 5 m away
            if (gfx)
            {
                if (rig) rig.enabled = false;
                Vector3 spotB = default, camSpot = default;
                var anyG = Patrolling().FirstOrDefault();
                if (anyG != null && OpenSpot(anyG.transform.position, 6f, out spotB, false))
                {
                    player.TeleportTo(spotB + Vector3.up * 1.1f, player.transform.rotation);
                    yield return new WaitForSeconds(0.6f);
                    var at = player.transform.position + new Vector3(0f, 0.3f, 0f);
                    if (OpenSpot(at, 5f, out camSpot, false) || OpenSpot(at, 4f, out camSpot, false))
                    {
                        var eye = new GameObject("BlastEye").AddComponent<Camera>();
                        eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = new Color(0.2f, 0.22f, 0.26f); eye.fieldOfView = 55f; eye.enabled = false;
                        eye.transform.position = camSpot + Vector3.up * 1.5f;
                        eye.transform.LookAt(at);
                        int played0 = BlastFx.Played;
                        var fx = BlastFx.Play(at, HudTheme.PlayerColour(1));
                        CombatCheck("the blast plays", BlastFx.Played == played0 + 1 && fx != null, $"(at {at}, the camera {Vector3.Distance(eye.transform.position, at):0.0} m away)");
                        foreach (var step in new[] { (0.1f, "a"), (0.2f, "b"), (0.3f, "c"), (0.7f, "d") })
                        {
                            yield return new WaitForSeconds(step.Item1);
                            ShotCam(eye, "combat_blast_" + step.Item2);
                        }
                        yield return new WaitForSeconds(BlastFx.Seconds);
                        CombatCheck("the blast is gone after its three seconds", fx == null);
                        Destroy(eye.gameObject);
                    }
                    else Log("SKIP the blast pictures: no clear spot for the camera");
                }
            }
            if (rig) rig.enabled = false;

            // ---- shots at the player: a rifle only knocks him down, a shotgun blows him up
            yield return CombatDuel(RangedWeapon.Kind.Rifle, 7f, "rifle guard");
            CombatCheck("a rifle guard's bullet knocks the player down and does not end his floor", duelHits >= 1 && duelStumbled && !duelBusted && duelBlasts == 0, $"(hits {duelHits}, stumbled {duelStumbled}, busted {duelBusted}, blasts {duelBlasts})");
            yield return CombatDuel(RangedWeapon.Kind.Smg, 6f, "SMG guard");
            CombatCheck("an SMG guard's bullet only knocks the player down", duelHits >= 1 && duelStumbled && !duelBusted && duelBlasts == 0, $"(hits {duelHits}, stumbled {duelStumbled}, busted {duelBusted}, blasts {duelBlasts})");
            yield return CombatDuel(RangedWeapon.Kind.Shotgun, 5f, "shotgun guard");
            CombatCheck("a shotgun guard's hit ends the player's floor: he is busted at once and there is a blast", duelHits >= 1 && duelBusted && duelBlasts == 1 && player.Custody == CustodyState.Busted, $"(hits {duelHits}, busted {duelBusted}, blasts {duelBlasts}, phase {hm.Phase})");
            hm.ReleaseBusted(player);
            yield return R5LoadFloor(11);
            player = WobblyPlayerRegistry.All[0]; rb = player.GetComponent<Rigidbody>(); pack = player.GetComponent<Backpack>(); cam = Camera.main;
            CombatCheck("(test) a new floor: the player is free again", player.Custody == CustodyState.Free && hm.Phase == HeistPhase.Heist, $"({player.Custody}, {hm.Phase})");
            yield return CombatDuel(RangedWeapon.Kind.Sniper, 9f, "sniper guard");
            CombatCheck("a sniper guard's hit ends the player's floor too", duelHits >= 1 && duelBusted && duelBlasts == 1, $"(hits {duelHits}, busted {duelBusted}, blasts {duelBlasts})");

            Log($"[Combat] DONE fails={combatFails}");
            Quit(combatFails == 0 ? 0 : 1);
        }
    }
}
