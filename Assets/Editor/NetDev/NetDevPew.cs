using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>Batch entry that does what the editor does by itself on load: puts the pew and its sounds in the two catalogs.</summary>
    public static class NetDevPewSetup
    {
        public static void FillCatalogs()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var wc = AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/Resources/WeaponCatalog.asset");
            var ac = AssetDatabase.LoadAssetAtPath<AudioCatalog>("Assets/Resources/AudioCatalog.asset");
            if (!wc || !ac) { Debug.Log("[PewSetup] FAIL a catalog is missing"); EditorApplication.Exit(1); return; }
            typeof(WeaponCatalogBuilder).GetMethod("FillGuns", flags).Invoke(null, new object[] { wc, true });
            typeof(AudioCatalogBuilder).GetMethod("FillGuns", flags).Invoke(null, new object[] { ac });
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PewSetup] pew={wc.pew} bullet={wc.pewBullet} shots={ac.gunShot.Length} tails={ac.gunTail.Length} reload={ac.gunReload.Length} equip={ac.gunEquip.Length} aim={ac.gunAim.Length}");
            EditorApplication.Exit(0);
        }
    }

    /// <summary>Play Mode check of the pew: the dev menu, the magazine and reload on the dummy and on guards, armed guards, the floor rule.</summary>
    public partial class NetDevRunner
    {
        int pewFails;

        void PewCheck(string name, bool ok, string detail = "")
        {
            if (!ok) pewFails++;
            Log($"[Pew] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static bool AllSet(AudioClip[] a)
        {
            if (a == null || a.Length == 0) return false;
            foreach (var c in a) if (!c) return false;
            return true;
        }

        static readonly BindingFlags AnyInst = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        /// <summary>A spot <paramref name="dist"/> metres from <paramref name="target"/> with nothing solid between the two (knee, chest and head height).</summary>
        static bool OpenSpot(Vector3 target, float dist, out Vector3 spot, bool onNavMesh)
        {
            // (the floors are at y = 0; a body's pivot is its middle, so "the floor" is not under the target's feet by its own height)
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f * Mathf.Deg2Rad;
                var p = target + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * dist;
                spot = default;
                // the floor itself, not the top of a desk or a partition: the lowest solid thing under the point
                var downs = Physics.RaycastAll(new Vector3(p.x, 2.5f, p.z), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
                bool floorFound = false; float lowest = float.MaxValue;
                foreach (var d in downs) if (d.point.y < lowest) { lowest = d.point.y; floorFound = true; }
                if (!floorFound || lowest > 0.15f) continue;
                spot = new Vector3(p.x, lowest, p.z);
                if (onNavMesh && !NavMesh.SamplePosition(spot, out _, 0.5f, NavMesh.AllAreas)) continue;
                bool clear = true;
                var flat = spot - new Vector3(target.x, spot.y, target.z);
                var dirTo = flat.normalized;
                foreach (float h in new[] { 0.7f, 1.3f })
                {
                    var from = new Vector3(target.x, spot.y + h, target.z) + dirTo * 0.8f;     // (start outside the target's own body)
                    if (Physics.SphereCast(from, 0.3f, dirTo, out _, flat.magnitude - 0.8f, ~0, QueryTriggerInteraction.Ignore)) { clear = false; break; }
                }
                if (clear) return true;
            }
            spot = default;
            return false;
        }

        /// <summary>The game camera held still, looking over the player's shoulder at <paramref name="at"/> (the rig is switched off for the test).</summary>
        static void AimCamera(Camera cam, WobblyPlayer player, Vector3 at)
        {
            var flat = at - player.transform.position; flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = player.transform.forward;
            flat.Normalize();
            var right = Vector3.Cross(Vector3.up, flat);
            cam.transform.position = player.transform.position - flat * 2.6f + right * 0.45f + Vector3.up * 1.35f;     // (the game camera: 1.35 above the middle of the body)
            cam.transform.rotation = Quaternion.LookRotation(at - cam.transform.position, Vector3.up);
        }

        static void Face(WobblyPlayer player, Vector3 at, Rigidbody rb)
        {
            var d = at - player.transform.position; d.y = 0f;
            player.TeleportTo(player.transform.position, Quaternion.LookRotation(d.normalized));
            rb.linearVelocity = Vector3.zero;
        }

        IEnumerator PewScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            bool gfx = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var pack = player.GetComponent<Backpack>();
            var cam = Camera.main;
            var rig = FindFirstObjectByType<CameraFollow>();
            var noiseHeard = new List<NoiseEvent>();
            void OnNoise(NoiseEvent n) { noiseHeard.Add(n); }
            NoiseBus.OnNoise += OnNoise;

            // ================= 1. the catalogs: the pew model and the handgun sounds are wired in
            PewCheck("catalog: the weapon catalog holds the pew and its cartridge", RangedWeapon.Available && RangedWeapon.Catalog.pewBullet != null);
            var ac = GameAudio.Catalog;
            PewCheck("catalog: the audio catalog holds the handgun sounds (3 shots, 2 tails, 5 reload steps, draw, aim)",
                ac != null && AllSet(ac.gunShot) && AllSet(ac.gunTail) && AllSet(ac.gunReload) && AllSet(ac.gunEquip) && AllSet(ac.gunAim)
                && ac.gunShot.Length == 3 && ac.gunTail.Length == 2 && ac.gunReload.Length == 5,
                ac == null ? "(no catalog)" : $"(shots {ac.gunShot?.Length}, tails {ac.gunTail?.Length}, reload {ac.gunReload?.Length}, equip {ac.gunEquip?.Length}, aim {ac.gunAim?.Length})");
            PewCheck("catalog: the reload clips are the five steps in order",
                AllSet(ac.gunReload) && ac.gunReload[0].name.Contains("slideLock") && ac.gunReload[1].name.Contains("magOut") && ac.gunReload[2].name.Contains("magDraw")
                && ac.gunReload[3].name.Contains("magIn") && ac.gunReload[4].name.Contains("charge"));
            PewCheck("nobody is armed by default (the game as it is today)", ArmedGuards.FirstArmedFloor == ArmedRules.Never && !ArmedGuards.Everywhere);

            // ================= 2. the dev menu hands out the pew
            PewCheck("the pew is not in the pack before the menu gives it", !pack.Items.Any(i => i && i.TryGetComponent(out RangedWeapon _)));
            var menu = new GameObject("TestModeForTest").AddComponent<TestMode>();
            typeof(TestMode).GetMethod("GivePew", AnyInst).Invoke(menu, new object[] { pack });
            yield return new WaitForSeconds(0.8f);
            var pewItem = pack.Items.FirstOrDefault(i => i && i.TryGetComponent(out RangedWeapon _));
            PewCheck("menu: the Pew button puts a pew in the pack", pewItem != null && pewItem.Label == "Pew");
            PewCheck("menu: and equips it", pewItem != null && pack.Equipped == pewItem);
            var gc = player.GetComponent<GunController>();
            PewCheck("the player has a gun controller that holds the pew", gc != null && gc.Gun != null && gc.Gun.kind == RangedWeapon.Kind.Pew);
            PewCheck("the pew model is in the player's hand", gc != null && gc.Held != null && gc.Held.Grip != null && gc.Held.Muzzle != null);
            var vis = player.GetComponent<PlayerVisual>();
            PewCheck("the pew sits on the right-hand bone", gc != null && gc.Held != null && vis != null && vis.Anim != null && gc.Held.Grip.IsChildOf(vis.Anim.RightHand != null ? vis.Anim.RightHand : vis.Anim.transform));
            PewCheck("the pew starts with a full magazine of 10", pewItem != null && pewItem.GetComponent<RangedWeapon>().Magazine.Rounds == 10 && pewItem.GetComponent<RangedWeapon>().Magazine.Capacity == 10);
            PewCheck("the draw sound played when it was equipped", GunSounds.EquipsPlayed >= 1);
            var pewGun = pewItem.GetComponent<RangedWeapon>();
            PewCheck("melee does not swing the pew (the melee controller sees no melee weapon)", pewItem.GetComponent<MeleeWeapon>() == null);

            // ================= 3. shooting the training dummy from the camera's crosshair
            if (rig) rig.enabled = false;
            var dummy = TrainingDummy.Current;
            PewCheck("the training dummy stands in the dock", dummy != null);
            Vector3 spot = default;
            bool open = dummy != null && OpenSpot(dummy.transform.position, 8f, out spot, false);
            PewCheck("found a clear spot 8 m from the dummy", open);
            if (open)
            {
                player.TeleportTo(spot + Vector3.up * 1.1f, player.transform.rotation);
                Face(player, dummy.transform.position, rb);
                var aimAt = dummy.transform.position + Vector3.up * 1.0f;
                yield return new WaitForSeconds(0.6f);                      // (the player lands)
                Face(player, dummy.transform.position, rb);
                AimCamera(cam, player, aimAt);
                yield return null; yield return null;
                AimCamera(cam, player, aimAt);
                var aimPoint = typeof(GunController).GetMethod("AimPoint", AnyInst);
                var args = new object[] { false };
                var ap = (Vector3)aimPoint.Invoke(gc, args);
                PewCheck("the crosshair on the dummy is a foe (the reticle goes red)", (bool)args[0] && Vector3.Distance(ap, dummy.transform.position) < 1.2f, $"(aim point {ap}, dummy {dummy.transform.position})");
                // away from the dummy: not a foe
                var wall = player.transform.position - (dummy.transform.position - player.transform.position).normalized * 3f + Vector3.up;
                AimCamera(cam, player, wall);
                args = new object[] { true };
                aimPoint.Invoke(gc, args);
                PewCheck("the crosshair on a wall or the empty floor is not a foe", !(bool)args[0]);
                AimCamera(cam, player, aimAt);

                int flashes0 = GunFx.Get().FlashesShown, tracers0 = GunFx.Get().TracersShown, shots0 = GunSounds.ShotsPlayed, tails0 = GunSounds.TailsPlayed;
                noiseHeard.Clear();
                float h0 = dummy.Health;
                bool fired1 = gc.Fire();
                PewCheck("the first shot leaves the gun", fired1 && pewGun.Magazine.Rounds == 9);
                yield return null;
                PewCheck("a bullet takes 60 health off the dummy", Mathf.Approximately(h0 - dummy.Health, RangedWeapon.Specs[0].damage), $"({h0} -> {dummy.Health})");
                PewCheck("the shot makes a flash, a tracer, a gunshot and its tail", GunFx.Get().FlashesShown == flashes0 + 1 && GunFx.Get().TracersShown == tracers0 + 1
                    && GunSounds.ShotsPlayed == shots0 + 1 && GunSounds.TailsPlayed == tails0 + 1);
                PewCheck("the shot is a loud noise guards can hear (45 m)", noiseHeard.Any(n => n.Source == player.gameObject && Mathf.Approximately(n.Loudness, 45f)));
                PewCheck("a second pull at once is too soon", !gc.Fire() && pewGun.Magazine.Rounds == 9);
                if (gfx) ShotCam(cam, "pew_player_shot");

                // the rest of the magazine, 0.32 s apart
                for (int i = 2; i <= 10; i++)
                {
                    yield return new WaitForSeconds(0.32f);
                    AimCamera(cam, player, aimAt);
                    gc.Fire();
                }
                PewCheck("ten shots empty the magazine", pewGun.Magazine.Rounds == 0 && pewGun.Magazine.Fired == 10 && gc.ShotsFired == 10, $"(rounds {pewGun.Magazine.Rounds}, fired {pewGun.Magazine.Fired})");
                PewCheck("ten flashes, ten tracers, ten gunshots with tails", GunFx.Get().FlashesShown - flashes0 == 10 && GunFx.Get().TracersShown - tracers0 == 10
                    && GunSounds.ShotsPlayed - shots0 == 10 && GunSounds.TailsPlayed - tails0 == 10);
                PewCheck("the bullets sparked where they landed and threw cases", GunFx.Get().SparksShown >= 10 && GunFx.Get().CasingsShown >= 10, $"(sparks {GunFx.Get().SparksShown}, cases {GunFx.Get().CasingsShown})");
                PewCheck("the dummy went down after two bullets", dummy.Down || dummy.Health <= 0f, $"(health {dummy.Health}, down {dummy.Down})");

                // the reload starts by itself and takes its time; the five reload sounds play in order
                int steps0 = GunSounds.ReloadStepsPlayed;
                yield return new WaitForSeconds(0.5f);
                PewCheck("the empty gun reloads by itself", pewGun.Magazine.Reloading);
                PewCheck("nothing can be fired while it reloads", !gc.Fire());
                yield return new WaitForSeconds(0.3f);
                PewCheck("the first reload sounds have played (slide, magazine out)", GunSounds.ReloadStepsPlayed - steps0 >= 2, $"({GunSounds.ReloadStepsPlayed - steps0} steps)");
                if (gfx) ShotCam(cam, "pew_player_reloading");
                yield return new WaitForSeconds(RangedWeapon.Specs[0].reloadSeconds);
                PewCheck("the reload ends with ten rounds", !pewGun.Magazine.Reloading && pewGun.Magazine.Rounds == 10 && pewGun.Magazine.Reloads == 1);
                PewCheck("all five steps of the reload were heard", GunSounds.ReloadStepsPlayed - steps0 == 5, $"({GunSounds.ReloadStepsPlayed - steps0})");

                // the trigger of an empty gun only clicks, and starts the reload (pulled in the quarter second before it starts by itself)
                pewGun.Magazine.Fill();
                for (int i = 0; i < 9; i++) { AimCamera(cam, player, aimAt); gc.Fire(); yield return new WaitForSeconds(0.31f); }
                AimCamera(cam, player, aimAt);
                gc.Fire();                                              // the tenth
                yield return new WaitForSeconds(0.1f);
                int clicks0 = GunSounds.DryClicksPlayed;
                bool clicked = !gc.Fire();
                PewCheck("pulling the trigger of the empty gun only clicks, and starts the reload", clicked && GunSounds.DryClicksPlayed == clicks0 + 1 && pewGun.Magazine.Reloading && pewGun.Magazine.Rounds == 0);
                yield return new WaitForSeconds(RangedWeapon.Specs[0].reloadSeconds + 0.3f);
                PewCheck("full again after the second reload", pewGun.Magazine.Rounds == 10 && pewGun.Magazine.Reloads >= 2, $"(reloads {pewGun.Magazine.Reloads})");
                // reload by hand after a few shots, then put the gun away mid-reload
                gc.Fire(); yield return new WaitForSeconds(0.31f); gc.Fire(); yield return new WaitForSeconds(0.31f);
                PewCheck("the gun can be reloaded by hand when it is partly used", pewGun.Magazine.Rounds == 8 && pewGun.Magazine.StartReload(Time.time));
                pack.SetEquipped(null);
                yield return new WaitForSeconds(0.4f);
                PewCheck("putting the pew away takes it out of the hand and stops the reload", gc.Gun == null && gc.Held == null && !pewGun.Magazine.Reloading && pewGun.Magazine.Rounds == 8);
                pack.SetEquipped(pewItem);
                yield return new WaitForSeconds(0.4f);
                PewCheck("and getting it out again puts it back, with the rounds it had", gc.Gun == pewGun && gc.Held != null && pewGun.Magazine.Rounds == 8);
                pewGun.Magazine.Fill();
            }

            // ================= 4. the pew lying on the floor can be dropped and picked up (a normal item)
            pack.Drop(pewItem);
            yield return new WaitForSeconds(0.5f);
            PewCheck("dropped, the pew is a loose item again, nothing in the hand", !pack.Contains(pewItem) && pewItem.gameObject.activeSelf && pack.Equipped == null && gc.Held == null);
            if (gfx)
            {
                var eyeD = new GameObject("PewDropEye").AddComponent<Camera>();
                eyeD.clearFlags = CameraClearFlags.SolidColor; eyeD.backgroundColor = new Color(0.2f, 0.22f, 0.26f); eyeD.fieldOfView = 30f; eyeD.enabled = false;
                eyeD.transform.position = pewItem.transform.position + new Vector3(0.5f, 0.45f, -0.5f);
                eyeD.transform.LookAt(pewItem.transform.position);
                yield return null;
                ShotCam(eyeD, "pew_on_floor");
            }
            pack.TryStore(pewItem);
            yield return new WaitForSeconds(0.3f);
            PewCheck("picking it up again puts it in the pack and in the hand", pack.Contains(pewItem) && pack.Equipped == pewItem);

            // ================= 5. a floor with guards
            TowerManager.Instance.GoToLevel(1);
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == 1);
            yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == 1 && hm.Phase == HeistPhase.Heist, 60, "floor 1");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(1.0f);
            if (rig) rig.enabled = false;
            cam = Camera.main;
            gc = player.GetComponent<GunController>();
            var guards = GuardAI.All.Where(g => g && !g.Dead && !g.IsPuppet).ToList();
            PewCheck("floor 1 has guards", guards.Count >= 2, $"({guards.Count})");
            PewCheck("none of them is armed (the floor rule is off)", guards.All(g => !g.Armed && g.Gun == null));
            PewCheck("the pew came up with the player and is still in the hand", pack.Equipped == pewItem && gc.Gun == pewGun && gc.Held != null);
            foreach (var g in guards) g.sightRange = 0.1f;                  // blind, so they only stand and walk about
            var fState = typeof(GuardAI).GetField("state", AnyInst);
            var fTarget = typeof(GuardAI).GetField("target", AnyInst);
            var fUnseen = typeof(GuardAI).GetField("unseenFor", AnyInst);
            PewCheck("(test) the guard's private fields are where the test expects them", fState != null && fTarget != null && fUnseen != null);

            // ---- 5a. the player's pew against a guard: two bullets, where ten swings were needed
            GuardAI victim = null; Vector3 vspot = default;
            foreach (var g in guards)
            {
                if (OpenSpot(g.transform.position, 7f, out vspot, false) || OpenSpot(g.transform.position, 5f, out vspot, false)) { victim = g; break; }
            }
            PewCheck("found a guard with a clear 5-7 m line to the player", victim != null);
            if (victim != null)
            {
                pewGun.Magazine.Fill();
                player.TeleportTo(vspot + Vector3.up * 1.1f, player.transform.rotation);
                yield return new WaitForSeconds(0.6f);                      // (the player lands)
                Face(player, victim.transform.position, rb);
                var chest = victim.transform.position + Vector3.up * 0.3f;
                AimCamera(cam, player, chest); yield return null;
                var aimPoint = typeof(GunController).GetMethod("AimPoint", AnyInst);
                var args = new object[] { false };
                AimCamera(cam, player, victim.transform.position + Vector3.up * 0.3f);
                aimPoint.Invoke(gc, args);
                PewCheck("the crosshair on a standing guard is a foe", (bool)args[0]);
                int hits0 = victim.Hits;
                AimCamera(cam, player, victim.transform.position + Vector3.up * 0.3f);
                bool firedAtVictim = gc.Fire();
                var li = gc.LastImpact;
                yield return null;
                PewCheck("one bullet counts as five hits on a guard and stuns him", victim.Hits == hits0 + 5 && victim.State == GuardState.Stunned && !victim.Dead,
                    $"(fired {firedAtVictim}, hits {victim.Hits}, {victim.State}; bullet from {gc.LastMuzzle} dir {gc.LastDirection}, hit {(li.hit ? li.collider.name : "nothing")} at {li.distance:0.0} m, guard at {victim.transform.position}, player {player.transform.position})");
                yield return new WaitForSeconds(0.32f);
                AimCamera(cam, player, victim.transform.position + Vector3.up * 0.3f);
                bool second = gc.Fire();
                yield return null;
                PewCheck("the second bullet puts him down for good", second && victim.Dead, $"(hits {victim.Hits}, dead {victim.Dead})");
                args = new object[] { true };
                AimCamera(cam, player, victim.transform.position + Vector3.up * 0.3f);
                aimPoint.Invoke(gc, args);
                PewCheck("a guard who is down is not a foe any more", !(bool)args[0]);
                guards.Remove(victim);
            }
            pewGun.Magazine.Fill();

            // ---- 5b. a bullet stops at a wall
            {
                var at = player.transform.position + Vector3.up * 1.2f;
                Impact wallHit = default; bool found = false;
                for (int i = 0; i < 24 && !found; i++)
                {
                    var dir = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
                    var imp = Ballistics.Trace(at, dir, 45f, player.transform, false);
                    if (imp.hit && imp.guard == null && imp.player == null && imp.distance > 0.5f && imp.distance < 30f) { wallHit = imp; found = true; }
                }
                PewCheck("a bullet that meets a wall or furniture stops there", found && wallHit.distance < 45f,
                    found ? $"({wallHit.collider.name} at {wallHit.distance:0.0} m, from {at}, bounds {wallHit.collider.bounds})" : "");
            }

            // ================= 6. armed guards
            var armTargets = guards.Where(g => g && !g.Dead).ToList();
            ArmedGuards.Everywhere = true;
            int newly = ArmedGuards.ArmAllNow();
            PewCheck("test menu: 'every guard carries a pew' arms the guards on the floor", newly == armTargets.Count && armTargets.All(g => g.Armed), $"({newly} of {armTargets.Count})");
            yield return new WaitForSeconds(0.6f);
            PewCheck("each armed guard holds the pew model in his right hand", armTargets.All(g => g.Gun != null && g.Gun.HasModel && g.Gun.Muzzle != null));
            PewCheck("an armed guard's magazine is full (ten)", armTargets.All(g => g.Gun.Magazine.Rounds == 10 && g.Gun.Magazine.Capacity == 10));
            var shooter = armTargets[0];
            Vector3 pspot = default;
            bool placed = false;
            foreach (var g in armTargets)
            {
                if (OpenSpot(g.transform.position, 8f, out pspot, false) || OpenSpot(g.transform.position, 6f, out pspot, false) || OpenSpot(g.transform.position, 5f, out pspot, false))
                { shooter = g; placed = true; break; }
            }
            var flashlight = shooter.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Flashlight");
            yield return null;
            PewCheck("his flashlight is on the front of his gun", flashlight != null && Vector3.Distance(flashlight.position, shooter.Gun.Muzzle.position) < 0.05f,
                flashlight ? $"({Vector3.Distance(flashlight.position, shooter.Gun.Muzzle.position):0.000} m)" : "(no flashlight)");

            // the shooter sees the player at 8 m: he stops, aims, fires ten shots, reloads
            shooter.sightRange = 14f;
            var gg = shooter.Gun;
            gg.baseSpreadDegrees = 0.3f; gg.spreadPerMetre = 0f;            // (so the test does not depend on luck)
            PewCheck("found a clear spot 5-8 m from an armed guard", placed);
            if (placed)
            {
                noiseHeard.Clear();
                int shotsSeen0 = GunSounds.ShotsPlayed, aims0 = GunSounds.AimsPlayed, flashes0 = GunFx.Get().FlashesShown;
                player.TeleportTo(pspot + Vector3.up * 1.1f, player.transform.rotation);
                Face(player, shooter.transform.position, rb);
                var to = player.transform.position - shooter.transform.position; to.y = 0f;
                shooter.transform.rotation = Quaternion.LookRotation(to.normalized);
                fTarget.SetValue(shooter, player); fUnseen.SetValue(shooter, 0f); fState.SetValue(shooter, GuardState.Chase);
                shooter.GetComponent<NavMeshAgent>().speed = shooter.chaseSpeed;
                float t0 = Time.time;
                bool stumbled = false;
                yield return null;
                if (gfx) { yield return new WaitForSeconds(0.3f); AimCamera2(cam, shooter, player, 0); ShotCam(cam, "pew_guard_aiming"); }
                while (gg.ShotsFired < 1 && Time.time - t0 < 4f) yield return null;
                float firstShotAt = Time.time - t0;
                PewCheck("he stands, aims for half a second and fires", gg.ShotsFired >= 1 && firstShotAt >= gg.aimSeconds - 0.1f && firstShotAt < gg.aimSeconds + 1.2f, $"(first shot after {firstShotAt:0.00} s)");
                if (gfx) { AimCamera2(cam, shooter, player, 0); ShotCam(cam, "pew_guard_shot"); }
                while (gg.Magazine.Fired < 10 && Time.time - t0 < 16f) { stumbled |= player.IsStumbling; yield return null; }
                PewCheck("ten shots are fired at the fire interval", gg.Magazine.Fired == 10 && Time.time - t0 > gg.aimSeconds + 9f * gg.fireInterval - 0.3f, $"(10 shots after {Time.time - t0:0.0} s)");
                PewCheck("the shots hit the player", gg.PlayerHits >= 8, $"({gg.PlayerHits} of {gg.ShotsFired})");
                PewCheck("god mode: the hits do not knock the player down", !stumbled);
                PewCheck("every shot made a flash and a gunshot on this machine", GunFx.Get().FlashesShown - flashes0 >= 10 && GunSounds.ShotsPlayed - shotsSeen0 >= 10 && gg.ShotsSeen >= 10);
                PewCheck("he rustled his gun up when he started to aim", GunSounds.AimsPlayed - aims0 >= 1);
                PewCheck("his shots are loud noises (45 m) the other guards hear", noiseHeard.Count(n => n.Source == shooter.gameObject && Mathf.Approximately(n.Loudness, 45f)) >= 10);
                yield return new WaitForSeconds(gg.Magazine.AutoReloadDelay + 0.3f);
                PewCheck("then he reloads, standing still", gg.Magazine.Reloading || gg.Reloads >= 1);
                if (gfx) { AimCamera2(cam, shooter, player, 0); ShotCam(cam, "pew_guard_reloading"); }
                yield return new WaitForSeconds(gg.reloadSeconds + 0.4f);
                PewCheck("and is ready with ten rounds again", gg.Reloads >= 1 && gg.Magazine.Rounds >= 9, $"(reloads {gg.Reloads}, rounds {gg.Magazine.Rounds})");

                // hits without god mode knock the player down
                hm.godMode = false;
                player.PinImmuneUntil = Time.time + 600f;              // (so a pin from another guard cannot spoil the check)
                float tk = Time.time;
                bool knocked = false;
                var before = player.transform.position;
                while (!knocked && Time.time - tk < 10f) { knocked = player.IsStumbling; yield return null; }
                PewCheck("without god mode a bullet knocks the player down", knocked, $"(after {Time.time - tk:0.0} s)");
                hm.godMode = true;
                player.PinImmuneUntil = 0f;
                yield return new WaitForSeconds(2.2f);

                // at arm's reach he does not shoot
                int fired1 = gg.Magazine.Fired;
                var close = shooter.transform.position + shooter.transform.forward * 2.0f;
                if (Physics.Raycast(close + Vector3.up * 2f, Vector3.down, out var gh2, 4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    player.TeleportTo(gh2.point + Vector3.up * 1.1f, player.transform.rotation);
                    rb.linearVelocity = Vector3.zero;
                    fUnseen.SetValue(shooter, 0f); fState.SetValue(shooter, GuardState.Chase); fTarget.SetValue(shooter, player);
                    yield return new WaitForSeconds(1.8f);
                    PewCheck("at 2 m he does not shoot (he goes for the grab)", gg.Magazine.Fired == fired1, $"(fired {gg.Magazine.Fired - fired1} more)");
                }
            }

            // ---- disarming
            int taken = ArmedGuards.DisarmAll();
            ArmedGuards.Everywhere = false;
            yield return new WaitForSeconds(0.5f);
            PewCheck("test menu: disarm takes the pew from every guard", taken == armTargets.Count && armTargets.All(g => g && !g.Armed && g.GetComponent<GuardGun>() == null), $"({taken})");
            PewCheck("the pew model is gone from their hands and the flashlight is back", armTargets.All(g => g.GetComponentsInChildren<Transform>(true).All(t => t.name != "Pew_Held")));

            // ================= 7. the floor rule (future: "armed from floor N")
            ArmedGuards.FirstArmedFloor = 2;
            TowerManager.Instance.GoToLevel(2);
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == 2);
            yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == 2 && hm.Phase == HeistPhase.Heist, 60, "floor 2");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(1.2f);
            var g2 = GuardAI.All.Where(g => g && !g.Dead && !g.IsPuppet).ToList();
            PewCheck("armed from floor 2: every guard on floor 2 carries a pew", g2.Count > 0 && g2.All(g => g.Armed), $"({g2.Count(g => g.Armed)} of {g2.Count})");
            int before2 = g2.Count;
            hm.CallBackup(null);
            yield return new WaitForSeconds(4f);
            var g2b = GuardAI.All.Where(g => g && !g.Dead && !g.IsPuppet).ToList();
            PewCheck("backup guards on that floor are armed too", g2b.Count > before2 && g2b.All(g => g.Armed), $"({before2} -> {g2b.Count}, armed {g2b.Count(g => g.Armed)})");
            if (gfx)
            {
                var sg = g2b.FirstOrDefault(g => g.Gun != null && g.Gun.HasModel);
                if (sg != null)
                {
                    var eye = new GameObject("ArmedEye").AddComponent<Camera>();
                    eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = new Color(0.2f, 0.22f, 0.26f); eye.fieldOfView = 40f; eye.enabled = false;
                    eye.transform.position = sg.transform.position + sg.transform.right * 2.4f + sg.transform.forward * 1.2f + Vector3.up * 1.5f;
                    eye.transform.LookAt(sg.transform.position + Vector3.up * 1.2f);
                    yield return null;
                    ShotCam(eye, "pew_guard_idle");
                }
            }
            ArmedGuards.FirstArmedFloor = ArmedRules.Never;
            TowerManager.Instance.GoToLevel(1);
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == 1);
            yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == 1 && hm.Phase == HeistPhase.Heist, 60, "floor 1 again");
            yield return new WaitForSeconds(1.0f);
            var g1 = GuardAI.All.Where(g => g && !g.Dead && !g.IsPuppet).ToList();
            PewCheck("with the rule off again floor 1 has nobody armed", g1.Count > 0 && g1.All(g => !g.Armed));

            NoiseBus.OnNoise -= OnNoise;
            if (rig) rig.enabled = true;
            Log($"[Pew] DONE fails={pewFails}");
            Quit(pewFails == 0 ? 0 : 1);
        }

        /// <summary>A camera beside the armed guard, looking across at him with the player in the background.</summary>
        static void AimCamera2(Camera cam, GuardAI guard, WobblyPlayer player, int variant)
        {
            var toPlayer = player.transform.position - guard.transform.position; toPlayer.y = 0f;
            var fwd = toPlayer.normalized;
            var right = Vector3.Cross(Vector3.up, fwd);
            cam.transform.position = guard.transform.position + right * 2.8f - fwd * 0.6f + Vector3.up * 1.5f;
            cam.transform.rotation = Quaternion.LookRotation((guard.transform.position + fwd * 1.0f + Vector3.up * 1.25f) - cam.transform.position, Vector3.up);
        }
    }
}
