using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Hip fire and aiming (the owner, 2026-10-27: "There should now be hip fire and aiming in first and in third person. A weapon should be at hip-fire unless a player is aiming which is the right mouse button"): scenario <c>AimHip</c> in the docks, one player.
    /// <c>qa_regress_bd.ps1 -Methods AimHip</c>; pictures need a real window: <c>ui.ps1 -Project C:\bd -Methods AimHip</c> (%TEMP%\be_ui_aim_*.png). The right mouse button is a test's own (<see cref="GunController.TestAim"/>).
    /// </summary>
    public partial class NetDevRunner
    {
        int aimFails;

        void AimCheck(string name, bool ok, string detail = "")
        {
            if (!ok) aimFails++;
            Log($"[AimHip] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>The mean angle (degrees) between where the gun was told to shoot and where its bullets went, over <paramref name="n"/> shots at a point in the air.</summary>
        IEnumerator AimMeanError(GunController gc, Backpack pack, Vector3 target, int n, System.Action<float> done)
        {
            float sum = 0f; int shots = 0;
            GunController.TestAimPoint = target;
            AimCamera(Camera.main, gc.GetComponent<WobblyPlayer>(), target);
            for (int i = 0; i < n; i++)
            {
                if (gc.Fire())
                {
                    var want = (target - gc.LastMuzzle).normalized;
                    sum += Vector3.Angle(gc.LastDirection, want);
                    shots++;
                }
                yield return new WaitForSeconds(0.1f);
            }
            GunController.TestAimPoint = null;
            done(shots > 0 ? sum / shots : -1f);
        }

        IEnumerator AimHipScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var gc = player.GetComponent<GunController>();
            var rig = FindFirstObjectByType<CameraFollow>();
            var menu = new GameObject("TestModeForTest").AddComponent<TestMode>();
            var giveGun = typeof(TestMode).GetMethod("GiveGun", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            AimCheck("(test) the test menu can hand over guns, and the player, his pack, his gun controller and the camera are there", giveGun != null && pack != null && gc != null && rig != null);
            if (giveGun == null || pack == null || gc == null || rig == null) { Log($"[AimHip] DONE fails={aimFails}"); Quit(1); yield break; }
            Pickup previous = null;

            foreach (var kind in new[] { RangedWeapon.Kind.Pew, RangedWeapon.Kind.Smg, RangedWeapon.Kind.Rifle, RangedWeapon.Kind.Sniper })
            {
                string tag = RangedWeapon.Specs[(int)kind].name + ":";
                if (previous) { pack.Drop(previous); Destroy(previous.gameObject); previous = null; yield return new WaitForSeconds(0.3f); }
                giveGun.Invoke(menu, new object[] { pack, kind });
                yield return new WaitForSeconds(1.0f);
                previous = pack.Items.FirstOrDefault(i => i && i.TryGetComponent(out RangedWeapon rw) && rw.kind == kind);
                var held = gc.Held;
                AimCheck($"{tag} the gun is in the hand", held != null);
                if (held == null) continue;

                // ---- third person: the hip, then the sights
                rig.SetFirstPerson(false);
                yield return new WaitForSeconds(1.6f);
                GunController.TestAim = false;
                yield return new WaitForSeconds(0.8f);
                AimCheck($"{tag} third person: with the right mouse button up the gun is at the hip (raise {AimRules.HipRaise}) and the player is not aiming", !gc.Aiming && !player.IsAiming && Mathf.Abs(held.RaiseNow - AimRules.HipRaise) < 0.03f, $"(raise {held.RaiseNow:0.00})");
                if (CanPicture) yield return Snap("aim_" + kind.ToString().ToLower() + "_tp_hip");
                GunController.TestAim = true;
                yield return new WaitForSeconds(0.8f);
                AimCheck($"{tag} third person: with the right mouse button held the gun comes up to the sights (raise 1) and the player is aiming (teammates are told)", gc.Aiming && player.IsAiming && held.RaiseNow > 0.97f, $"(raise {held.RaiseNow:0.00})");
                if (CanPicture) yield return Snap("aim_" + kind.ToString().ToLower() + "_tp_aim");

                // ---- first person: the sight and the scope come on only while aiming
                rig.SetFirstPerson(true);
                yield return new WaitForSeconds(1.8f);
                GunController.TestAim = false;
                yield return new WaitForSeconds(0.9f);
                float dotHip = HeldGun.LocalDotShare, scopeHip = HeldGun.LocalScope;
                AimCheck($"{tag} first person at the hip: the gun is down at the hip, no red dot is looked through and no scope is up", Mathf.Abs(held.RaiseNow - AimRules.HipRaise) < 0.03f && dotHip < 0.05f && scopeHip < 0.05f, $"(raise {held.RaiseNow:0.00}, dot {dotHip:0.00}, scope {scopeHip:0.00})");
                if (CanPicture) yield return Snap("aim_" + kind.ToString().ToLower() + "_fp_hip");
                GunController.TestAim = true;
                yield return new WaitForSeconds(1.2f);
                bool hasDot = RangedWeapon.Specs[(int)kind].CanSight && RangedWeapon.Catalog != null && RangedWeapon.Catalog.Gun((int)kind).sightAt != Vector3.zero;
                if (kind == RangedWeapon.Kind.Sniper) AimCheck($"{tag} first person aiming: the scope comes up", held.RaiseNow > 0.97f && HeldGun.LocalScope > 0.9f, $"(raise {held.RaiseNow:0.00}, scope {HeldGun.LocalScope:0.00})");
                else if (hasDot) AimCheck($"{tag} first person aiming: the view is through the red dot", held.RaiseNow > 0.97f && HeldGun.LocalDotShare > 0.9f, $"(raise {held.RaiseNow:0.00}, dot {HeldGun.LocalDotShare:0.00})");
                else AimCheck($"{tag} first person aiming: the gun is all the way up", held.RaiseNow > 0.97f, $"(raise {held.RaiseNow:0.00})");
                if (CanPicture) yield return Snap("aim_" + kind.ToString().ToLower() + "_fp_aim");
                GunController.TestAim = null;
                rig.SetFirstPerson(false);
                yield return new WaitForSeconds(0.8f);
            }

            // ---- from the hip a shot scatters 1.5 times wider (the SMG: a 3.4 degree cone, many shots)
            if (previous) { pack.Drop(previous); Destroy(previous.gameObject); previous = null; yield return new WaitForSeconds(0.3f); }
            giveGun.Invoke(menu, new object[] { pack, RangedWeapon.Kind.Smg });
            yield return new WaitForSeconds(1.0f);
            rig.enabled = false;
            var sky = player.transform.position + new Vector3(0f, 40f, 60f);
            float hipErr = -1f, aimErr = -1f;
            GunController.TestAim = false;
            yield return AimMeanError(gc, pack, sky, 90, e => hipErr = e);
            GunController.TestAim = true;
            yield return AimMeanError(gc, pack, sky, 90, e => aimErr = e);
            GunController.TestAim = null;
            float cone = RangedWeapon.Specs[(int)RangedWeapon.Kind.Smg].spread;
            AimCheck("a shot from the hip scatters wider than an aimed one: the mean error is 2/3 of the cone aimed (3.4 degrees: 2.3) and 1.5 times that from the hip, within the noise of ninety shots",
                aimErr > 0f && hipErr > 0f && aimErr > cone * 0.5f && aimErr < cone * 0.85f && hipErr / aimErr > 1.25f && hipErr / aimErr < 1.8f, $"(aimed {aimErr:0.00} deg, hip {hipErr:0.00} deg, ratio {hipErr / Mathf.Max(0.001f, aimErr):0.00})");

            Log($"[AimHip] DONE fails={aimFails}");
            Quit(aimFails == 0 ? 0 : 1);
        }
    }
}
