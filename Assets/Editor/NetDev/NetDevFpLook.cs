using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// What first person looks like with each gun in the hands (the owner, 2026-10-20: "When in first person with a weapon the character is somewhat visible blocking the screen", with a picture of a first-person shooter: the gun, its sight in the middle of
    /// the screen, a forearm reaching in and nothing else of the body). The player stands in a plain arena far from the building (ground, a few boxes to rest the eye on), the camera level, and the real camera is photographed with each gun in turn
    /// (and with the crowbar and with empty hands). Needs graphics: <c>qa_regress.ps1 -Methods FpLook:gfx</c>. Pictures: %TEMP%\be_shot_fplook_{what}.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int fpLookFails;

        void FpLookCheck(string name, bool ok, string detail = "")
        {
            if (!ok) fpLookFails++;
            Log($"[FpLook] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>
        /// Aiming and reloading through the Kinemation pack (the owner, 2026-10-23: "make it so the fps shooting in game when you zoom in uses this asset pack for shooting aiming and reloading"): the sight is where the old solve put it
        /// (so the picture of the aimed gun did not change), the pack's layer is what aims it, and a reload takes the gun down out of the sight line with the reload's progress and brings it back (pictures at four moments of it).
        /// </summary>
        IEnumerator FpLookAimAndReload(GunController gc, RangedWeapon.Kind kind, Vector3 vpMuzzleAimed)
        {
            var hg = gc ? gc.Held : null;
            if (hg == null || gc.Gun == null) { FpLookCheck($"{kind}: reload - the gun is in the hand", false); yield break; }
            var cam = Camera.main;
            FpLookCheck($"{kind}: aimed through the pack's layer - the arms are solved by the Kinemation solver (HeldGun.UseSightsAligner {HeldGun.UseSightsAligner}), not the direct solve", HeldGun.UseSightsAligner && hg.AlignerUsed && hg.AimLayer != null && !hg.AimLayer.Failed, $"(aligner used {hg.AlignerUsed}, sight gap {(hg.AimLayer != null ? hg.AimLayer.Gap : -1f):0.0000} m)");
            // the old solve put the muzzle here: the pack's layer must not move the picture of the aimed gun
            HeldGun.UseSightsAligner = false;
            yield return new WaitForSeconds(0.5f);
            var vpOld = cam.WorldToViewportPoint(hg.Muzzle.position);
            bool oldUsed = hg.AlignerUsed;
            HeldGun.UseSightsAligner = true;
            yield return new WaitForSeconds(0.5f);
            var vpNew = cam.WorldToViewportPoint(hg.Muzzle.position);
            FpLookCheck($"{kind}: the aimed gun is where it was with the direct solve - the muzzle is within 2% of the screen of the old place", !oldUsed && hg.AlignerUsed && Mathf.Abs(vpNew.x - vpOld.x) < 0.02f && Mathf.Abs(vpNew.y - vpOld.y) < 0.02f, $"(old {vpOld:F3}, now {vpNew:F3})");

            // one round fired, so that there is something to reload (the basement never runs short: the test asks for real ammunition)
            GunController.LimitedAmmoForTests = true;
            yield return null; yield return null;
            var mag = gc.Gun.Magazine;
            mag.TryFire(Time.time);
            bool started = mag.StartReload(Time.time);
            FpLookCheck($"{kind}: a reload starts", started && mag.Reloading, $"(reload {mag.ReloadSeconds:0.0} s)");
            float lowestDot = 1f, loweredAt = 0f, muzzleDrop = 0f; bool alignedAll = true, tiltedUp = false; int frames = 0; var vpLow = Vector3.zero;
            foreach (var frac in new[] { 0.10f, 0.30f, 0.55f, 0.90f })
            {
                float wait = mag.ReloadStartedAt + frac * mag.ReloadSeconds - Time.time;
                if (wait > 0f) yield return new WaitForSeconds(wait);
                var vp = cam.WorldToViewportPoint(hg.Muzzle.position);
                frames++;
                alignedAll &= hg.AlignerUsed;
                if (frac == 0.30f)
                {
                    loweredAt = hg.ReloadLowered; lowestDot = HeldGun.LocalDotShare; muzzleDrop = vpMuzzleAimed.y - vp.y; vpLow = vp;
                    tiltedUp = hg.RaiseNow < 0.95f;
                }
                ShotCam(cam, "fplook_reload_" + kind + "_" + (int)(frac * 100));
            }
            FpLookCheck($"{kind}: in the middle of the reload the gun is down out of the sight line (lowered {loweredAt:0.00}) and the crosshair is back (dot share {lowestDot:0.00})", loweredAt > 0.95f && lowestDot < 0.1f, $"(muzzle moved {muzzleDrop:0.000} of the screen height)");
            FpLookCheck($"{kind}: the reload is shown by the gun moving, not by tilting the arm up as in third person (the arm stays raised), and the pack's layer holds it at every moment", !tiltedUp && alignedAll && hg.FirstPersonReload, $"(raise {hg.RaiseNow:0.00}, {frames} moments)");
            FpLookCheck($"{kind}: the gun is still in the picture while it is down - its muzzle is on the screen, lower than when aimed", vpLow.z > 0.3f && vpLow.x > 0.15f && vpLow.x < 0.9f && vpLow.y > 0.02f && vpLow.y < 0.62f && muzzleDrop > 0.01f, $"(muzzle at {vpLow:F2}, {muzzleDrop:0.000} lower than aimed)");
            float rest = mag.ReloadEndsAt + 0.6f - Time.time;
            if (rest > 0f) yield return new WaitForSeconds(rest);
            var vpEnd = cam.WorldToViewportPoint(hg.Muzzle.position);
            FpLookCheck($"{kind}: after the reload the gun is back on the sight line where it was (within 1.5% of the screen) and the dot is back", !mag.Reloading && hg.ReloadLowered < 0.01f && Mathf.Abs(vpEnd.x - vpMuzzleAimed.x) < 0.015f && Mathf.Abs(vpEnd.y - vpMuzzleAimed.y) < 0.015f && (kind != RangedWeapon.Kind.Smg || HeldGun.LocalDotShare > 0.9f), $"(muzzle {vpEnd:F3} from {vpMuzzleAimed:F3}, lowered {hg.ReloadLowered:0.00}, dot share {HeldGun.LocalDotShare:0.00})");
            GunController.LimitedAmmoForTests = false;
        }

        IEnumerator FpLookScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            hm.godMode = true;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var rig = FindFirstObjectByType<CameraFollow>();
            if (!rig || !pack) { Log("[FpLook] no camera rig or pack"); Quit(1); yield break; }

            var root = new GameObject("FpLook");
            root.transform.position = new Vector3(0f, 0f, 220f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            Material Flat(Color c) => new Material(Shader.Find("Standard")) { color = c };
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.1f, 8f);
            ground.transform.localScale = new Vector3(40f, 0.2f, 40f);
            ground.GetComponent<Renderer>().sharedMaterial = Flat(new Color(0.55f, 0.57f, 0.60f));
            var colours = new[] { new Color(0.8f, 0.3f, 0.25f), new Color(0.3f, 0.7f, 0.35f), new Color(0.3f, 0.45f, 0.8f), new Color(0.85f, 0.75f, 0.3f) };
            for (int i = 0; i < 4; i++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition = new Vector3(-4.5f + i * 3f, 0.8f, 9f + (i % 2) * 3f);
                box.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
                box.GetComponent<Renderer>().sharedMaterial = Flat(colours[i]);
            }
            player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
            rig.SetFirstPerson(true);
            yield return new WaitForSeconds(0.8f);
            rig.TurnAim(-rig.AimYaw);                                                           // (looking along +z, at the boxes)
            rig.TiltAim(-rig.AimPitch + 2f);                                                    // (and level)
            yield return new WaitForSeconds(0.8f);
            player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
            Log($"[FpLook] first person {rig.IsFirstPerson}, arms shown {rig.ArmsShown}, yaw {rig.AimYaw:0.0}, pitch {rig.AimPitch:0.0}");
            ShotCam(Camera.main, "fplook_empty_hands");

            float fovBefore = Camera.main.fieldOfView;                                             // (the field of view with no scope: the sniper rifle's check compares with it)
            Pickup previous = null;
            var at = player.transform.position + Vector3.up * 0.5f;
            foreach (var kind in new[] { RangedWeapon.Kind.Smg, RangedWeapon.Kind.Rifle, RangedWeapon.Kind.Shotgun, RangedWeapon.Kind.Sniper, RangedWeapon.Kind.Pew })
            {
                if (previous) { pack.Drop(previous); Destroy(previous.gameObject); previous = null; yield return new WaitForSeconds(0.3f); }
                var item = RangedWeapon.Create(kind, at, null, 0f, 2).GetComponent<Pickup>();
                pack.TryStore(item);
                yield return new WaitForSeconds(0.3f);
                pack.SelectSlot(0);
                previous = item;
                yield return new WaitForSeconds(1.3f);
                Log($"[FpLook] {kind}: equipped {(pack.Equipped ? pack.Equipped.Label : "nothing")}, arms shown {rig.ArmsShown}");
                ShotCam(Camera.main, "fplook_" + kind);
                var camNow = Camera.main;
                var gcn = player.GetComponent<GunController>();
                var hg = gcn ? gcn.Held : null;
                var vpMuzzle = hg ? camNow.WorldToViewportPoint(hg.Muzzle.position) : Vector3.zero;
                if (kind == RangedWeapon.Kind.Sniper)
                {
                    // the scope (the owner, 2026-10-20 night): the view is narrowed, the rifle is not drawn, the lens is up
                    bool gunDrawn = hg != null && hg.Grip.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.enabled);
                    FpLookCheck("Sniper: looking down the scope - the lens is up, the view is zoomed in and the rifle itself is not drawn", hg != null && HeldGun.LocalScope > 0.95f && camNow.fieldOfView < fovBefore * 0.6f && !gunDrawn, $"(scope {HeldGun.LocalScope:0.00}, field of view {camNow.fieldOfView:0.0} from {fovBefore:0.0}, rifle drawn {gunDrawn})");
                }
                else
                    FpLookCheck($"{kind}: the gun is in the picture - its muzzle is on the screen, in the lower half and not at the very edge, shouldered in front of the eyes (the owner, 2026-10-20)", hg != null && vpMuzzle.z > 0.3f && vpMuzzle.x > 0.25f && vpMuzzle.x < 0.8f && vpMuzzle.y > 0.12f && vpMuzzle.y < 0.62f, $"(muzzle at {vpMuzzle:F2})");
                if (RangedWeapon.TakesRedDot(RangedWeapon.Specs[(int)kind].weaponClass))
                {
                    var dotT = hg ? hg.Grip.Find("RedDot/Dot") : null;
                    var vpDot = dotT ? camNow.WorldToViewportPoint(dotT.position) : Vector3.zero;
                    FpLookCheck($"{kind}: the view is through the red dot sight - the dot is in the very middle of the screen, a hand's width in front of the camera (the owner, 2026-10-20 night: \"I should be looking out the red dot sight\")", dotT != null && vpDot.z > 0.1f && vpDot.z < 0.4f && Mathf.Abs(vpDot.x - 0.5f) < 0.04f && Mathf.Abs(vpDot.y - 0.5f) < 0.04f && HeldGun.LocalDotShare > 0.9f, $"(dot at {vpDot:F2}, dot share {HeldGun.LocalDotShare:0.00})");
                }
                var bodyBits = player.GetComponentsInChildren<Renderer>(true).Where(r => r && r.enabled && r.gameObject.activeInHierarchy && !CameraFollow.IsHeldModel(r.transform, player.transform)).ToList();
                if (kind == RangedWeapon.Kind.Smg || kind == RangedWeapon.Kind.Sniper)
                {
                    var side = new GameObject("FpSideCam").AddComponent<Camera>();
                    side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = new Color(0.55f, 0.66f, 0.82f); side.enabled = false; side.fieldOfView = 55f;
                    side.transform.position = camNow.transform.position + camNow.transform.right * 1.1f + camNow.transform.forward * 0.45f + camNow.transform.up * 0.15f;
                    side.transform.LookAt(camNow.transform.position + camNow.transform.forward * 0.35f - camNow.transform.up * 0.2f);
                    ShotCam(side, "fplook_side_" + kind);
                    side.transform.position = camNow.transform.position + camNow.transform.right * -0.9f + camNow.transform.forward * 0.8f + camNow.transform.up * 0.2f;
                    side.transform.LookAt(camNow.transform.position + camNow.transform.forward * 0.35f - camNow.transform.up * 0.2f);
                    ShotCam(side, "fplook_leftside_" + kind);
                    Destroy(side.gameObject);
                }
                bool pewAnimated = kind == RangedWeapon.Kind.Pew && hg != null && hg.Animated;          // (the dock worker's Blender animation, 2026-10-27: the PistolAnim scenario checks it in full)
                if (pewAnimated)
                    FpLookCheck("Pew: held with the dock worker's Blender animation - the right arm holds it out in the picture and the view does not go through the sights (the owner, 2026-10-27)", HeldGun.LocalArmMode == 1 && rig.ArmsShown && HeldGun.LocalDotShare == 0f && !hg.AlignerUsed, $"(arm mode {HeldGun.LocalArmMode}, arms shown {rig.ArmsShown})");
                else if (kind == RangedWeapon.Kind.Pew)
                    FpLookCheck("Pew: no hands in the picture - the pistol is seen alone like every other gun, no arm is drawn (the owner, 2026-10-21)", !HeldGun.ShowPistolArm && HeldGun.LocalArmMode == 0 && !rig.ArmsShown && bodyBits.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly), $"(arm mode {HeldGun.LocalArmMode}, arms shown {rig.ArmsShown}, {bodyBits.Count} parts)");
                FpLookCheck($"{kind}: nothing of the body but its arms is drawn", rig.ArmsShown == (CameraFollow.ShowArms || (kind == RangedWeapon.Kind.Pew && (HeldGun.ShowPistolArm || pewAnimated))) && bodyBits.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly || r.sharedMaterials.All(m => m && m.shader && m.shader.name == "BuildingEscape/FirstPersonArms")), $"({bodyBits.Count} parts)");
                if (kind == RangedWeapon.Kind.Smg || (kind == RangedWeapon.Kind.Pew && !pewAnimated))
                    yield return FpLookAimAndReload(gcn, kind, vpMuzzle);
            }
            if (previous) { pack.Drop(previous); Destroy(previous.gameObject); previous = null; yield return new WaitForSeconds(0.3f); }
            var crowbar = MeleeWeapon.Create(MeleeWeapon.Kind.Crowbar, at, null).GetComponent<Pickup>();
            pack.TryStore(crowbar);
            yield return new WaitForSeconds(0.3f);
            pack.SelectSlot(0);
            yield return new WaitForSeconds(1.3f);
            Log($"[FpLook] crowbar: equipped {(pack.Equipped ? pack.Equipped.Label : "nothing")}");
            ShotCam(Camera.main, "fplook_crowbar");
            var crowbarHeld = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("_Held") && t.name.StartsWith("Crowbar"));
            var crowbarRenderer = crowbarHeld ? crowbarHeld.GetComponentInChildren<Renderer>() : null;
            var vpCrowbar = crowbarRenderer ? Camera.main.WorldToViewportPoint(crowbarRenderer.bounds.center) : Vector3.zero;
            FpLookCheck("crowbar: the weapon in the hand is in the picture in first person - raised to the lower right, up and forward (the owner, 2026-10-20: it was out of the picture below the eyes)", crowbarRenderer != null && crowbarRenderer.enabled && crowbarRenderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly && vpCrowbar.z > 0.3f && vpCrowbar.x > 0.5f && vpCrowbar.x < 0.97f && vpCrowbar.y > 0.08f && vpCrowbar.y < 0.7f, $"(weapon at {vpCrowbar:F2})");
            Log($"[FpLook] DONE fails={fpLookFails}");
            Quit(fpLookFails == 0 ? 0 : 1);
        }
    }
}
