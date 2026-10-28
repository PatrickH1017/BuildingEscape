using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The dock worker's Blender pistol animation on every character a player can pick (the owner, 2026-10-27: "attach the blender animation for the silenced pistol for dock workers. I want all available character you pick to
    /// be able to use this animation ... when we are looking down in fps it looks more realistic as well as in third person"). The real player, in a plain arena far from the building, is dressed as each of the five dock
    /// workers in turn and given the pistol: it must be held with the animation (the clips on the upper body, the gun in the hand where the Blender files hold it), point at the crosshair in third and first person, looking
    /// level, down and up, and play the shot and the reload (the magazine drops out, the new one is in the left hand, then in the grip). Needs graphics: <c>qa_regress.ps1 -Methods PistolAnim:gfx</c>.
    /// Pictures: %TEMP%\be_shot_pistolanim_{worker}_{what}.png. The environment variable BE_PISTOLANIM_ONLY = one character's id (syn_builder_highvis_01 ...) runs that one alone.
    /// </summary>
    public partial class NetDevRunner
    {
        int pistolAnimFails;

        void PistolAnimCheck(string name, bool ok, string detail = "")
        {
            if (!ok) pistolAnimFails++;
            Log($"[PistolAnim] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>How far (degrees) the barrel is from pointing at where the crosshair is.</summary>
        static float BarrelError(HeldGun hg, Vector3 aimPoint)
        {
            var want = aimPoint - hg.Grip.position;
            return want.sqrMagnitude < 1e-4f ? 0f : Vector3.Angle(hg.Grip.forward, want);
        }

        /// <summary>Where the crosshair is: the camera's middle ray to the first solid thing (as the gun's own AimPoint, within its range).</summary>
        static Vector3 CrosshairPoint(Transform player)
        {
            var cam = Camera.main.transform;
            float t0 = AimMath.StartT(AimMath.Depth(cam.position.x, cam.position.y, cam.position.z, cam.forward.x, cam.forward.y, cam.forward.z, player.position.x, player.position.y, player.position.z));
            var imp = Ballistics.Trace(cam.position + cam.forward * t0, cam.forward, 45f, player, false);
            return imp.point;
        }

        IEnumerator PistolAnimScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            hm.godMode = true;
            GunController.TestCursorLocked = true;
            GunController.TestAim = true;              // (the right mouse button held: since 2026-10-27 a gun is at the hip without it, and these scenarios look at the aimed pose)
            WeaponAnimation.StancesEnabled = false;    // (these scenarios look at the raise / aim / fire clips: the officers' and the fallback of a gun whose stance loops are missing; the stances have the GunStance scenario)
            Time.captureDeltaTime = 1f / 30f;          // (every frame is a thirtieth of a second, however slow a batch run draws: the shot clip lasts seven frames)
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var vis = player.GetComponent<PlayerVisual>();
            var gc = player.GetComponent<GunController>();
            var rig = FindFirstObjectByType<CameraFollow>();
            if (!rig || !pack || !vis || !gc) { Log("[PistolAnim] no camera rig, pack, look or gun controller"); Quit(1); yield break; }
            PistolAnimCheck("the clips are in the build (Resources/Animations: raise, aim, shot, reload; humanoid)", PistolAnimation.Load() != null);

            // a plain arena far from the building: ground, a few boxes to rest the eye on, a sun
            var root = new GameObject("PistolAnim");
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
            var side = new GameObject("PistolAnimCam").AddComponent<Camera>();
            side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = new Color(0.55f, 0.66f, 0.82f); side.enabled = false; side.nearClipPlane = 0.02f;

            var cat = CharacterCatalog.Instance;
            Pickup pistol = null;
            var only = System.Environment.GetEnvironmentVariable("BE_PISTOLANIM_ONLY");
            foreach (var id in PlayerAppearance.DefaultToons)
            {
                if (!string.IsNullOrEmpty(only) && only != id) continue;
                var ch = cat.Find(id);
                if (ch == null) { PistolAnimCheck($"{id}: in the catalog", false); continue; }
                string who = cat.DockLabel(ch).Replace(" ", "");
                if (pistol) { pack.Drop(pistol); Destroy(pistol.gameObject); pistol = null; yield return new WaitForSeconds(0.2f); }
                vis.Apply(new CharacterLook(id, Color.white, 1, null, 1f, 1f, id));
                player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                rig.SetFirstPerson(false);
                yield return WaitFor(() => rig.Zoom <= 0.001f, 30f, "third person");
                rig.TurnAim(-rig.AimYaw);
                rig.TiltAim(-rig.AimPitch + 8f);
                yield return new WaitForSeconds(0.6f);
                pistol = RangedWeapon.Create(RangedWeapon.Kind.Pew, player.transform.position + Vector3.up * 0.5f, null, 0f, 2).GetComponent<Pickup>();
                pack.TryStore(pistol);
                yield return null;
                pack.SelectSlot(0);
                yield return new WaitForSeconds(1.2f);
                player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                yield return new WaitForSeconds(0.4f);
                var hg = gc.Held;
                var anim = vis.Anim;
                PistolAnimCheck($"{who} ({id}): the pistol is held with the Blender animation", hg != null && hg.Animated && anim && anim.GunWeight > 0.99f, hg != null ? $"(animated {hg.Animated}, upper body {(anim ? anim.GunWeight : -1f):0.00}, {hg.ArmInfo()})" : "(no gun)");
                if (hg == null || !hg.Animated) continue;

                // third person, looking level: the barrel points at the crosshair
                float err = BarrelError(hg, CrosshairPoint(player.transform));
                PistolAnimCheck($"{who}: third person - the barrel points at the crosshair (within 3 degrees)", err < 3f, $"({err:0.00} deg)");
                var t = player.transform;
                var gunAt = hg.Grip.position;
                side.fieldOfView = 40f;
                side.transform.position = t.position + t.right * 2.0f + t.up * 0.55f + t.forward * 0.9f;
                side.transform.LookAt(t.position + t.up * 0.45f + t.forward * 0.35f);
                ShotCam(side, $"pistolanim_{who}_side");
                side.transform.position = t.position + t.forward * 2.6f + t.right * 1.3f + t.up * 0.7f;
                side.transform.LookAt(t.position + t.up * 0.35f);
                ShotCam(side, $"pistolanim_{who}_front");
                side.fieldOfView = 30f;
                side.transform.position = gunAt + t.right * 0.42f + t.up * 0.12f - t.forward * 0.12f;
                side.transform.LookAt(gunAt);
                ShotCam(side, $"pistolanim_{who}_hand");
                side.transform.position = gunAt - t.right * 0.40f + t.up * 0.10f + t.forward * 0.05f;
                side.transform.LookAt(gunAt);
                ShotCam(side, $"pistolanim_{who}_handleft");
                ShotCam(Camera.main, $"pistolanim_{who}_tp");

                // a shot: the kick of the shot clip, then back on the aim
                gc.Fire();
                yield return new WaitForSeconds(3f / 30f);          // (the frame drawn two frames after the shot: the shot clip's kick at its height)
                var kickAngle = Vector3.Angle(hg.Grip.forward, CrosshairPoint(t) - hg.Grip.position);
                side.fieldOfView = 40f;
                side.transform.position = t.position + t.right * 2.0f + t.up * 0.55f + t.forward * 0.9f;
                side.transform.LookAt(t.position + t.up * 0.45f + t.forward * 0.35f);
                ShotCam(side, $"pistolanim_{who}_kick");
                yield return new WaitForSeconds(0.45f);
                float after = BarrelError(hg, CrosshairPoint(t));
                PistolAnimCheck($"{who}: a shot kicks the muzzle up (the shot clip) and the gun comes back on the crosshair", kickAngle > 5f && after < 3f, $"(kick {kickAngle:0.0} deg, after {after:0.00} deg, shots {gc.ShotsFired})");

                // looking up and down (third person)
                rig.TiltAim(25f);
                yield return new WaitForSeconds(0.5f);
                err = BarrelError(hg, CrosshairPoint(t));
                PistolAnimCheck($"{who}: third person, looking down - the barrel points at the crosshair", err < 3f, $"({err:0.00} deg, pitch {rig.AimPitch:0})");
                ShotCam(side, $"pistolanim_{who}_down");
                rig.TiltAim(-50f);
                yield return new WaitForSeconds(0.5f);
                err = BarrelError(hg, CrosshairPoint(t));
                PistolAnimCheck($"{who}: third person, looking up - the barrel points at the crosshair", err < 3f, $"({err:0.00} deg, pitch {rig.AimPitch:0})");
                ShotCam(side, $"pistolanim_{who}_up");
                rig.TiltAim(-rig.AimPitch + 8f);
                yield return new WaitForSeconds(0.4f);

                // walking in third person: the legs walk under the pistol held up (the pistol's layers take the upper body only)
                var animator = anim.GetComponent<Animator>();
                var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                float footMin = float.MaxValue, footMax = float.MinValue, layerMin = 1f;
                player.testMove = Vector3.forward;
                for (float tm = 0f; tm < 1.5f; tm += Time.deltaTime)
                {
                    yield return null;
                    if (tm < 0.5f) continue;
                    float z = Vector3.Dot(foot.position - t.position, t.forward);
                    footMin = Mathf.Min(footMin, z); footMax = Mathf.Max(footMax, z);
                    layerMin = Mathf.Min(layerMin, anim.GunWeight);
                }
                side.fieldOfView = 40f;
                side.transform.position = t.position + t.right * 2.6f + t.up * 0.4f + t.forward * 0.8f;
                side.transform.LookAt(t.position + t.forward * 0.3f);
                ShotCam(side, $"pistolanim_{who}_walk");
                player.testMove = null;
                PistolAnimCheck($"{who}: third person, walking - the legs walk (the left foot swings back and forth) while the pistol stays up", footMax - footMin > 0.15f && layerMin > 0.99f, $"(the foot swings {footMax - footMin:0.00} m, pistol layer at least {layerMin:0.00})");
                yield return new WaitForSeconds(0.6f);
                player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                yield return new WaitForSeconds(0.4f);

                // the reload (from empty: the slide is locked back until the left hand lets it go)
                GunController.LimitedAmmoForTests = true;
                yield return null;
                var mag = gc.Gun.Magazine;
                for (int i = 0; i < mag.Capacity && mag.Rounds > 0; i++) mag.TryFire(Time.time + i * mag.FireInterval);          // (the magazine emptied: the slide stays back)
                bool started = mag.StartReload(Time.time);
                PistolAnimCheck($"{who}: a reload starts", started && mag.Reloading, $"(reload {mag.ReloadSeconds:0.0} s)");
                side.fieldOfView = 40f;          // (the side view again: the walk moved the camera)
                side.transform.position = t.position + t.right * 2.0f + t.up * 0.55f + t.forward * 0.9f;
                side.transform.LookAt(t.position + t.up * 0.45f + t.forward * 0.35f);
                Transform magPart = hg.Grip.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.Contains("_Mag"));
                var lhand = anim.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftHand);
                float magToLeft = -1f; bool magGoneEarly = false; float reloadLayer = 0f; int dropped = 0;
                foreach (var frac in new[] { 0.10f, 0.27f, 0.45f, 0.62f, 0.70f, 0.90f })
                {
                    float wait = mag.ReloadStartedAt + frac * mag.ReloadSeconds - Time.time;
                    if (wait > 0f) yield return new WaitForSeconds(wait);
                    if (frac == 0.27f) { magGoneEarly = magPart && !magPart.gameObject.activeInHierarchy; dropped = FindObjectsByType<DroppedMagazine>(FindObjectsSortMode.None).Length; }
                    if (frac == 0.45f) { magToLeft = magPart && magPart.gameObject.activeInHierarchy && lhand ? (magPart.position - lhand.position).magnitude : -1f; reloadLayer = anim.GunReloadWeight; }
                    ShotCam(side, $"pistolanim_{who}_reload_{(int)(frac * 100)}");
                }
                PistolAnimCheck($"{who}: the reload plays its clip - the empty magazine leaves the grip and drops, the new one is in the left hand", magGoneEarly && dropped > 0 && magToLeft > 0f && magToLeft < 0.16f && reloadLayer > 0.95f, $"(gone from the grip {magGoneEarly}, dropped {dropped}, new one {magToLeft:0.000} m from the left wrist, reload layer {reloadLayer:0.00})");
                float rest = mag.ReloadEndsAt + 0.5f - Time.time;
                if (rest > 0f) yield return new WaitForSeconds(rest);
                err = BarrelError(hg, CrosshairPoint(t));
                PistolAnimCheck($"{who}: after the reload the magazine is in the grip and the gun is back on the crosshair", !mag.Reloading && magPart && magPart.gameObject.activeInHierarchy && magPart.localPosition.sqrMagnitude < 1f && err < 3f, $"({err:0.00} deg, rounds {mag.Rounds})");
                var droppedNow = FindObjectsByType<DroppedMagazine>(FindObjectsSortMode.None);
                if (droppedNow.Length > 0)
                {
                    float floorY = root.transform.position.y;
                    float lowest = droppedNow.Min(d => Mathf.Abs(d.transform.position.y - floorY));
                    PistolAnimCheck($"{who}: the dropped magazine lies on the floor", lowest < 0.05f, $"({lowest:0.000} m above it)");
                }
                GunController.LimitedAmmoForTests = false;

                // first person: the arm holds the pistol out, low and right of the crosshair, the barrel on the crosshair
                rig.SetFirstPerson(true);
                yield return WaitFor(() => rig.Zoom >= 0.999f, 30f, "first person");          // (the zoom runs on real time: a frame here is a thirtieth of a second of game time)
                yield return new WaitForSeconds(0.5f);
                var cam = Camera.main;
                var vp = cam.WorldToViewportPoint(hg.Muzzle.position);
                var vpGrip = cam.WorldToViewportPoint(hg.Grip.position);
                err = BarrelError(hg, CrosshairPoint(t));
                PistolAnimCheck($"{who}: first person - the pistol is in the picture, below and right of the crosshair, the right arm drawn, no sight picture", vp.z > 0.2f && vpGrip.x > 0.5f && vpGrip.x < 0.95f && vpGrip.y > 0.05f && vpGrip.y < 0.5f && HeldGun.LocalArmMode == 1 && rig.ArmsShown && HeldGun.LocalDotShare == 0f, $"(grip at {vpGrip:F2}, muzzle at {vp:F2}, arm mode {HeldGun.LocalArmMode}, arms shown {rig.ArmsShown})");
                PistolAnimCheck($"{who}: first person - the barrel points at the crosshair", err < 3f, $"({err:0.00} deg)");
                ShotCam(cam, $"pistolanim_{who}_fp");
                rig.TiltAim(30f);
                yield return new WaitForSeconds(0.5f);
                err = BarrelError(hg, CrosshairPoint(t));
                vpGrip = cam.WorldToViewportPoint(hg.Grip.position);
                PistolAnimCheck($"{who}: first person, looking down - the barrel on the crosshair, the gun in the picture", err < 3f && vpGrip.z > 0.1f && vpGrip.y > 0f && vpGrip.y < 1f, $"({err:0.00} deg, grip at {vpGrip:F2})");
                ShotCam(cam, $"pistolanim_{who}_fpdown");
                float pitchWas = rig.AimPitch;
                rig.TiltAim(60f);                                                                   // (as far down as the view goes)
                yield return new WaitForSeconds(0.5f);
                err = BarrelError(hg, CrosshairPoint(t));
                vpGrip = cam.WorldToViewportPoint(hg.Grip.position);
                PistolAnimCheck($"{who}: first person, looking as far down as the view goes - the barrel on the crosshair, the gun in the picture", err < 3f && vpGrip.z > 0.1f && vpGrip.y > 0f && vpGrip.y < 1f && vpGrip.x > 0f && vpGrip.x < 1f, $"({err:0.00} deg, pitch {rig.AimPitch:0}, grip at {vpGrip:F2})");
                ShotCam(cam, $"pistolanim_{who}_fpfloor");
                rig.TiltAim(pitchWas - rig.AimPitch);
                yield return new WaitForSeconds(0.3f);
                rig.TiltAim(-55f);
                yield return new WaitForSeconds(0.5f);
                err = BarrelError(hg, CrosshairPoint(t));
                vpGrip = cam.WorldToViewportPoint(hg.Grip.position);
                PistolAnimCheck($"{who}: first person, looking up - the barrel on the crosshair, the gun in the picture", err < 3f && vpGrip.z > 0.1f && vpGrip.y > 0f && vpGrip.y < 1f, $"({err:0.00} deg, grip at {vpGrip:F2})");
                ShotCam(cam, $"pistolanim_{who}_fpup");
                rig.TiltAim(-rig.AimPitch + 4f);
                yield return new WaitForSeconds(0.4f);
                // walking forward in first person: the gun stays on the crosshair and in its place in the picture
                var probe = new GameObject("PistolAnimProbe").AddComponent<PistolAnimProbe>();          // (measured after the camera and the gun have moved for the frame: a coroutine runs before the camera follows a moving body)
                probe.gun = hg; probe.player = t;
                yield return null; yield return null;
                var vpStill = probe.viewport;
                player.testMove = Vector3.forward;
                float worst = 0f, drift = 0f, loY = 1f, hiY = 0f;
                for (float tm = 0f; tm < 2f; tm += Time.deltaTime)
                {
                    yield return null;
                    worst = Mathf.Max(worst, probe.barrelError);
                    var v = probe.viewport;
                    drift = Mathf.Max(drift, new Vector2(v.x - vpStill.x, v.y - vpStill.y).magnitude);
                    if (tm > 0.8f) { loY = Mathf.Min(loY, v.y); hiY = Mathf.Max(hiY, v.y); }          // (the sway of the walk itself, once under way)
                }
                ShotCam(cam, $"pistolanim_{who}_fpwalk");
                player.testMove = null;
                Destroy(probe.gameObject);
                PistolAnimCheck($"{who}: first person, walking - the barrel stays on the crosshair and the gun near its place in the picture (a light bob of the walk, under 7% of the screen)", worst < 2f && drift < 0.08f && hiY - loY < 0.07f, $"(worst {worst:0.00} deg, the grip moved up to {drift:0.000} of the screen from where it stood, swaying {hiY - loY:0.000} up and down while walking)");
                // a reload in first person: the left hand comes in with the magazine
                GunController.LimitedAmmoForTests = true;
                yield return null;
                for (int i = 0; i < mag.Capacity && mag.Rounds > 0; i++) mag.TryFire(Time.time + i * mag.FireInterval);
                mag.StartReload(Time.time);
                float w45 = mag.ReloadStartedAt + 0.45f * mag.ReloadSeconds - Time.time;
                if (w45 > 0f) yield return new WaitForSeconds(w45);
                var vpLeft = lhand ? cam.WorldToViewportPoint(lhand.position) : Vector3.zero;
                var vpMag = magPart ? cam.WorldToViewportPoint(magPart.position) : Vector3.zero;
                PistolAnimCheck($"{who}: first person reload - both arms drawn, the left hand with the new magazine in the picture (the wrist may be at the bottom edge)", HeldGun.LocalArmMode == 2 && vpLeft.z > 0.1f && vpLeft.y > -0.05f && vpLeft.y < 0.9f && vpMag.y > 0.08f && vpMag.y < 0.9f, $"(arm mode {HeldGun.LocalArmMode}, left hand at {vpLeft:F2}, magazine at {vpMag:F2})");
                ShotCam(cam, $"pistolanim_{who}_fpreload");
                float w69 = mag.ReloadStartedAt + 0.69f * mag.ReloadSeconds - Time.time;
                if (w69 > 0f) yield return new WaitForSeconds(w69);
                ShotCam(cam, $"pistolanim_{who}_fprack");
                float rest2 = mag.ReloadEndsAt + 0.4f - Time.time;
                if (rest2 > 0f) yield return new WaitForSeconds(rest2);
                GunController.LimitedAmmoForTests = false;
                rig.SetFirstPerson(false);
                yield return WaitFor(() => rig.Zoom <= 0.001f, 30f, "third person");
                yield return new WaitForSeconds(0.3f);
            }

            // an officer's pistol is not the players': it stays posed by code
            var gAnim = PartyCharacterFactory.Attach(new GameObject("PistolAnimGuard"), new CharacterLook("guard", Color.white, 1, null, 1f, 1f, "syn_security_male_01"));
            yield return null;
            var gHeld = gAnim ? HeldGun.Attach(gAnim, RangedWeapon.Kind.Pew) : null;
            PistolAnimCheck("an officer's pistol is posed by code as before (not the players' animation)", gHeld != null && !gHeld.Animated, gHeld != null ? "" : "(no gun)");
            GunController.TestCursorLocked = null;
            Time.captureDeltaTime = 0f;
            Log($"[PistolAnim] DONE fails={pistolAnimFails}");
            Quit(pistolAnimFails == 0 ? 0 : 1);
        }
    }

    /// <summary>The PistolAnim scenario's eye on the first-person picture: where the gun is on the screen and how far its barrel is off the crosshair, measured after the camera and the gun have moved for the frame.</summary>
    [DefaultExecutionOrder(5000)]
    public class PistolAnimProbe : MonoBehaviour
    {
        public HeldGun gun;
        public Transform player;
        public Vector3 viewport;
        public float barrelError;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!gun || !gun.Grip || !cam || !player) return;
            viewport = cam.WorldToViewportPoint(gun.Grip.position);
            var c = cam.transform;
            float t0 = AimMath.StartT(AimMath.Depth(c.position.x, c.position.y, c.position.z, c.forward.x, c.forward.y, c.forward.z, player.position.x, player.position.y, player.position.z));
            var aimAt = Ballistics.Trace(c.position + c.forward * t0, c.forward, 45f, player, false).point;
            barrelError = Vector3.Angle(gun.Grip.forward, aimAt - gun.Grip.position);
        }
    }
}
