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
                FpLookCheck($"{kind}: the gun is in the picture - its muzzle is on the screen, in the lower half and not at the very edge, shouldered in front of the eyes (the owner, 2026-10-20)", hg != null && vpMuzzle.z > 0.3f && vpMuzzle.x > 0.25f && vpMuzzle.x < 0.8f && vpMuzzle.y > 0.12f && vpMuzzle.y < 0.62f, $"(muzzle at {vpMuzzle:F2})");
                if (RangedWeapon.TakesRedDot(RangedWeapon.Specs[(int)kind].weaponClass))
                {
                    var dotT = hg ? hg.Grip.Find("RedDot/Dot") : null;
                    var vpDot = dotT ? camNow.WorldToViewportPoint(dotT.position) : Vector3.zero;
                    FpLookCheck($"{kind}: the red dot sight is near the middle of the screen, a little low and right of it, the way a shouldered gun's is", dotT != null && vpDot.z > 0.3f && vpDot.x > 0.42f && vpDot.x < 0.68f && vpDot.y > 0.30f && vpDot.y < 0.60f, $"(dot at {vpDot:F2})");
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
                FpLookCheck($"{kind}: nothing of the body but its arms is drawn", rig.ArmsShown && bodyBits.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly || r.sharedMaterials.All(m => m && m.shader && m.shader.name == "BuildingEscape/FirstPersonArms")), $"({bodyBits.Count} parts)");
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
