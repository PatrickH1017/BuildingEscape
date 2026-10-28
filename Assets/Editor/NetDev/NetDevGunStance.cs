using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The owner's gun stances (2026-10-27: "I will now provide plenty of animations for hip-fire while standing/moving and aiming while standing/moving. Please implement them properly to the matching weapon"): scenario <c>GunStance</c>.
    /// The real player in a plain arena far from the building, as Dock worker 1 (and Dock worker 5 for the held-and-aimed checks), is given each gun in turn: it must be held with its own four loops (<c>Resources/Animations/&lt;Gun&gt;_HipFire_Stand ...</c>),
    /// at the hip unless the right mouse button is held, standing and running, the barrel on the crosshair (level, down, up) with the left hand on the gun where the Blender file has it, a shot must kick and come back, a reload must
    /// lower the long guns to the low ready, and the first-person view must show the hip (the gun in the picture, no sight) and the sights (the red dot, the scope). Needs graphics: <c>qa_regress.ps1 -Methods GunStance:gfx</c>.
    /// Pictures: %TEMP%\be_shot_gunstance_{gun}_{what}.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int stanceFails;

        void StanceCheck(string name, bool ok, string detail = "")
        {
            if (!ok) stanceFails++;
            Log($"[GunStance] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        // where the wrists are on each gun in the stance files (measure.py at frame 5 of the aimed standing file: the wrist in the gun model's own space, Blender's units; Unity's model is the same with its x mirrored and a hundredth of the size);
        // the guns are in the same hands in all twenty files (checked), so the same numbers hold at the hip, aimed, standing and running
        static readonly Vector3[] StanceRightWrist =
        {
            new Vector3(-2.6016f, -0.6178f, -11.1311f), new Vector3(-3.5675f, -0.4409f, -8.5897f), new Vector3(-3.9746f, -2.9083f, -11.9228f), new Vector3(-4.7581f, -0.5732f, -10.8616f), new Vector3(-4.8716f, -2.7305f, -15.7731f),
        };
        static readonly Vector3[] StanceLeftWrist =
        {
            new Vector3(5.857f, -11.4536f, -7.8092f), new Vector3(3.556f, -5.4926f, 12.1899f), new Vector3(9.1795f, 2.5018f, 25.9205f), new Vector3(8.1208f, -1.1403f, 33.488f), new Vector3(10.5196f, -2.6905f, 41.5047f),
        };

        /// <summary>How many cycles a second a signal makes (it crosses its mean, with a margin of a fifth of its swing so that jitter does not count, twice a cycle); -1 when it is too short, 0 when it hardly moves.</summary>
        static float StanceCycles(List<float> times, List<float> values)
        {
            if (values.Count < 8) return -1f;
            float mean = values.Average(), lo = values.Min(), hi = values.Max(), amp = (hi - lo) * 0.5f;
            if (amp < 1e-4f) return 0f;
            int crossings = 0, state = 0;
            foreach (var v in values)
            {
                float d = v - mean;
                int s = d > 0.2f * amp ? 1 : d < -0.2f * amp ? -1 : state;
                if (state != 0 && s != state) crossings++;
                state = s;
            }
            float span = times[times.Count - 1] - times[0];
            return span > 0.1f ? crossings / 2f / span : -1f;
        }

        IEnumerator GunStanceScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            hm.godMode = true;
            GunController.TestCursorLocked = true;
            Time.captureDeltaTime = 1f / 30f;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var vis = player.GetComponent<PlayerVisual>();
            var gc = player.GetComponent<GunController>();
            var rig = FindFirstObjectByType<CameraFollow>();
            if (!rig || !pack || !vis || !gc) { Log("[GunStance] no camera rig, pack, look or gun controller"); Quit(1); yield break; }
            pack.capacity = 100f;
            var kinds = new[] { RangedWeapon.Kind.Pew, RangedWeapon.Kind.Smg, RangedWeapon.Kind.Rifle, RangedWeapon.Kind.Shotgun, RangedWeapon.Kind.Sniper };
            StanceCheck("the twenty stance loops are in the build (Resources/Animations: hip fire / aimed, standing / running; humanoid)",
                kinds.All(k => { var c = k == RangedWeapon.Kind.Pew ? PistolAnimation.Load() : WeaponAnimation.Gun(k); return c != null && c.Stances; }),
                $"({string.Join(", ", kinds.Select(k => { var c = k == RangedWeapon.Kind.Pew ? PistolAnimation.Load() : WeaponAnimation.Gun(k); return WeaponAnimation.GunName(k) + " " + (c != null && c.Stances); }))})");

            // a plain arena far from the building: ground, a few boxes to rest the eye on, a sun
            var root = new GameObject("GunStance");
            root.transform.position = new Vector3(0f, 0f, 220f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            Material Flat(Color c) => new Material(Shader.Find("Standard")) { color = c };
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.1f, 8f);
            ground.transform.localScale = new Vector3(40f, 0.2f, 120f);
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
            var side = new GameObject("GunStanceCam").AddComponent<Camera>();
            side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = new Color(0.55f, 0.66f, 0.82f); side.enabled = false; side.nearClipPlane = 0.02f;
            void SideShot(Transform t, string name) { side.fieldOfView = 40f; side.transform.position = t.position + t.right * 2.0f + t.up * 0.55f + t.forward * 0.9f; side.transform.LookAt(t.position + t.up * 0.45f + t.forward * 0.35f); ShotCam(side, name + "_side"); }
            void FrontShot(Transform t, string name) { side.fieldOfView = 40f; side.transform.position = t.position + t.forward * 2.6f + t.right * 1.3f + t.up * 0.7f; side.transform.LookAt(t.position + t.up * 0.35f); ShotCam(side, name + "_front"); }
            void RunShot(Transform t, string name) { side.fieldOfView = 40f; side.transform.position = t.position + t.right * 2.6f + t.up * 0.4f + t.forward * 0.8f; side.transform.LookAt(t.position + t.forward * 0.3f); ShotCam(side, name + "_run"); }

            var cat = CharacterCatalog.Instance;
            var ids = PlayerAppearance.DefaultToons;
            Pickup item = null;
            for (int who = 0; who < 2; who++)
            {
                string id = who == 0 ? ids.First() : ids.Last();
                var ch = cat.Find(id);
                if (ch == null) { StanceCheck($"{id}: in the catalog", false); continue; }
                string label = cat.DockLabel(ch).Replace(" ", "");
                vis.Apply(new CharacterLook(id, Color.white, 1, null, 1f, 1f, id));
                foreach (var kind in kinds)
                {
                    var spec = RangedWeapon.Specs[(int)kind];
                    string gname = spec.name;
                    string tag = $"gunstance_{label}_{gname}";
                    string pre = $"{label} {gname}:";
                    if (item) { pack.Drop(item); Destroy(item.gameObject); item = null; yield return new WaitForSeconds(0.2f); }
                    GunController.TestAim = false;
                    player.testMove = null;
                    player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                    rig.SetFirstPerson(false);
                    yield return WaitFor(() => rig.Zoom <= 0.001f, 30f, "third person");
                    rig.TurnAim(-rig.AimYaw);
                    rig.TiltAim(-rig.AimPitch + 8f);
                    yield return new WaitForSeconds(0.4f);
                    item = RangedWeapon.Create(kind, player.transform.position + Vector3.up * 0.5f, null, 0f, 2).GetComponent<Pickup>();
                    pack.TryStore(item);
                    yield return null;
                    pack.SelectSlot(0);
                    yield return new WaitForSeconds(1.4f);
                    player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                    yield return new WaitForSeconds(0.5f);
                    var hg = gc.Held;
                    var anim = vis.Anim;
                    var t = player.transform;
                    bool held = hg != null && hg.Animated && hg.Clips != null && hg.Clips.Stances && anim && anim.StanceActive && anim.GunWeight > 0.99f && (kind == RangedWeapon.Kind.Pew ? !hg.TwoHanded : hg.TwoHanded);
                    StanceCheck($"{pre} held with its stance loops, the upper body theirs", held, hg != null ? $"(animated {hg.Animated}, stances {(hg.Clips != null && hg.Clips.Stances)}, upper body {(anim ? anim.GunWeight : -1f):0.00})" : "(no gun)");
                    if (!held) continue;
                    var animator = anim.GetComponent<Animator>();
                    var rWrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    var lWrist = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    var model = hg.Grip.Find(gname + "_Model");
                    var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var hips = animator.GetBoneTransform(HumanBodyBones.Hips);

                    // ---------------- third person, at the hip (no right mouse button), standing
                    StanceCheck($"{pre} at the hip: the gun is half raised, the player is not aiming, the run share is nil", !gc.Aiming && !player.IsAiming && Mathf.Abs(hg.RaiseNow - AimRules.HipRaise) < 0.03f && anim.RunShare < 0.05f, $"(raise {hg.RaiseNow:0.00}, run {anim.RunShare:0.00})");
                    float err = BarrelError(hg, CrosshairPoint(t));
                    StanceCheck($"{pre} at the hip, looking level: the barrel points at the crosshair (within 3 degrees)", err < 3f, $"({err:0.00} deg)");
                    float rOff = WristOffGun(model, rWrist, StanceRightWrist[(int)kind]), lOff = WristOffGun(model, lWrist, StanceLeftWrist[(int)kind]);
                    StanceCheck($"{pre} at the hip: both hands on the gun where the Blender file has them (the right wrist within 1.5 cm, the left within 4 cm)", rOff < 0.015f && lOff < 0.04f && hg.LeftHandGap < 0.012f, $"(right {rOff * 100f:0.0} cm, left {lOff * 100f:0.0} cm, left hand off the gun {hg.LeftHandGap * 100f:0.0} cm)");
                    SideShot(t, tag + "_hip"); FrontShot(t, tag + "_hip");
                    ShotCam(Camera.main, tag + "_hip_tp");
                    rig.TiltAim(25f);
                    yield return new WaitForSeconds(0.4f);
                    err = BarrelError(hg, CrosshairPoint(t));
                    StanceCheck($"{pre} at the hip, looking down: the barrel points at the crosshair (within 3 degrees), the left hand on the gun", err < 3f && hg.LeftHandGap < 0.012f, $"({err:0.00} deg, left hand off the gun {hg.LeftHandGap * 100f:0.0} cm)");
                    SideShot(t, tag + "_hipdown");
                    rig.TiltAim(-50f);
                    yield return new WaitForSeconds(0.4f);
                    err = BarrelError(hg, CrosshairPoint(t));
                    StanceCheck($"{pre} at the hip, looking up: the barrel points at the crosshair (within 3 degrees), the left hand on the gun", err < 3f && hg.LeftHandGap < 0.012f, $"({err:0.00} deg, left hand off the gun {hg.LeftHandGap * 100f:0.0} cm)");
                    SideShot(t, tag + "_hipup");
                    rig.TiltAim(-rig.AimPitch + 8f);
                    yield return new WaitForSeconds(0.4f);
                    if (who == 1) continue;           // (the second character: held, hands and barrel at the hip are enough)

                    // ---------------- a shot from the hip: the barrel kicks up and comes back
                    float before = BarrelError(hg, CrosshairPoint(t));
                    bool fired = gc.Fire();
                    yield return null;
                    float kicked = BarrelError(hg, CrosshairPoint(t));
                    ShotCam(Camera.main, tag + "_kick");
                    yield return new WaitForSeconds(0.9f);
                    float after = BarrelError(hg, CrosshairPoint(t));
                    StanceCheck($"{pre} a shot from the hip kicks the barrel up ({spec.KickDegrees:0} degrees at most) and it comes back", fired && kicked > before + 0.5f * spec.KickDegrees && kicked < spec.KickDegrees * 1.3f + 1f && after < 2f, $"(fired {fired}; off the crosshair {before:0.0} -> {kicked:0.0} -> {after:0.0} deg)");

                    // ---------------- third person, aimed (the right mouse button held), standing
                    GunController.TestAim = true;
                    yield return new WaitForSeconds(0.15f);
                    float midRaise = hg.RaiseNow;
                    yield return new WaitForSeconds(0.9f);
                    StanceCheck($"{pre} the right mouse button brings the gun up to the sights (in transit {midRaise:0.00}, then 1) and the teammates are told", midRaise > AimRules.HipRaise + 0.02f && midRaise < 0.99f && hg.RaiseNow > 0.97f && gc.Aiming && player.IsAiming, $"(raise {hg.RaiseNow:0.00})");
                    err = BarrelError(hg, CrosshairPoint(t));
                    rOff = WristOffGun(model, rWrist, StanceRightWrist[(int)kind]); lOff = WristOffGun(model, lWrist, StanceLeftWrist[(int)kind]);
                    StanceCheck($"{pre} aimed: the barrel points at the crosshair (within 3 degrees), both hands on the gun", err < 3f && rOff < 0.015f && lOff < 0.04f && hg.LeftHandGap < 0.012f, $"({err:0.00} deg, right {rOff * 100f:0.0} cm, left {lOff * 100f:0.0} cm, left hand off the gun {hg.LeftHandGap * 100f:0.0} cm)");
                    SideShot(t, tag + "_aim"); FrontShot(t, tag + "_aim");
                    ShotCam(Camera.main, tag + "_aim_tp");
                    rig.TiltAim(25f);
                    yield return new WaitForSeconds(0.4f);
                    err = BarrelError(hg, CrosshairPoint(t));
                    StanceCheck($"{pre} aimed, looking down: the barrel points at the crosshair (within 3 degrees)", err < 3f && hg.LeftHandGap < 0.012f, $"({err:0.00} deg, left hand off the gun {hg.LeftHandGap * 100f:0.0} cm)");
                    rig.TiltAim(-50f);
                    yield return new WaitForSeconds(0.4f);
                    err = BarrelError(hg, CrosshairPoint(t));
                    StanceCheck($"{pre} aimed, looking up: the barrel points at the crosshair (within 3 degrees)", err < 3f && hg.LeftHandGap < 0.012f, $"({err:0.00} deg, left hand off the gun {hg.LeftHandGap * 100f:0.0} cm)");
                    rig.TiltAim(-rig.AimPitch + 8f);
                    yield return new WaitForSeconds(0.3f);
                    GunController.TestAim = false;
                    yield return new WaitForSeconds(0.9f);
                    StanceCheck($"{pre} letting go of the right mouse button brings the gun back to the hip", Mathf.Abs(hg.RaiseNow - AimRules.HipRaise) < 0.03f && !player.IsAiming, $"(raise {hg.RaiseNow:0.00})");

                    // ---------------- third person, running: the legs run under the stance, the upper body shows the run loop
                    foreach (bool aimed in new[] { false, true })
                    {
                        string st = aimed ? "aimed" : "at the hip";
                        GunController.TestAim = aimed;
                        yield return new WaitForSeconds(0.9f);
                        var handR = rWrist;
                        float standLo = float.MaxValue, standHi = float.MinValue;
                        for (float tm = 0f; tm < 1.0f; tm += Time.deltaTime)
                        {
                            yield return null;
                            float hy = handR.position.y - t.position.y;
                            standLo = Mathf.Min(standLo, hy); standHi = Mathf.Max(standHi, hy);
                        }
                        player.testMove = Vector3.forward;
                        var times = new List<float>(); var footZ = new List<float>(); var handY = new List<float>();
                        float footMin = float.MaxValue, footMax = float.MinValue, layerMin = 1f, errMax = 0f, gapMax = 0f, runMin = 1f;
                        float runLo = float.MaxValue, runHi = float.MinValue;
                        float t0 = Time.time;
                        for (float tm = 0f; tm < 2.4f; tm += Time.deltaTime)
                        {
                            yield return null;
                            if (tm < 0.7f) continue;
                            float z = Vector3.Dot(foot.position - hips.position, t.forward);
                            footMin = Mathf.Min(footMin, z); footMax = Mathf.Max(footMax, z);
                            layerMin = Mathf.Min(layerMin, anim.GunWeight);
                            errMax = Mathf.Max(errMax, BarrelError(hg, CrosshairPoint(t)));
                            gapMax = Mathf.Max(gapMax, hg.LeftHandGap);
                            runMin = Mathf.Min(runMin, anim.RunShare);
                            float hy = handR.position.y - t.position.y;
                            runLo = Mathf.Min(runLo, hy); runHi = Mathf.Max(runHi, hy);
                            times.Add(Time.time - t0); footZ.Add(z); handY.Add(handR.position.y - hips.position.y);
                        }
                        if (aimed) RunShot(t, tag + "_aim"); else RunShot(t, tag + "_hip");
                        player.testMove = null;
                        StanceCheck($"{pre} running {st}: the run loop is shown (run share {runMin:0.00} or more), the legs run (the foot swings {footMax - footMin:0.00} m) and the gun layer stays on ({layerMin:0.00})", runMin > 0.9f && footMax - footMin > 0.15f && layerMin > 0.99f, $"(run share {runMin:0.00}, foot {footMax - footMin:0.00} m, layer {layerMin:0.00})");
                        StanceCheck($"{pre} running {st}: the barrel stays on the crosshair (within 4 degrees), both hands on the gun (within 1.5 cm)", errMax < 4f && gapMax < 0.015f, $"(worst {errMax:0.00} deg, left hand off the gun at most {gapMax * 100f:0.0} cm)");
                        StanceCheck($"{pre} running {st}: the gun moves with the run (the hand rises and falls {(runHi - runLo) * 100f:0.0} cm; standing {(standHi - standLo) * 100f:0.0} cm)", runHi - runLo > (standHi - standLo) + 0.004f, $"(run {(runHi - runLo) * 100f:0.0} cm, standing {(standHi - standLo) * 100f:0.0} cm)");
                        float legHz = StanceCycles(times, footZ), handHz = StanceCycles(times, handY);
                        float hzRatio = legHz > 0f ? handHz / legHz : -1f;
                        StanceCheck($"{pre} running {st}: the gun's bob keeps the step of the legs (the legs {legHz:0.00} strides a second, the hand's bob {handHz:0.00} a second: the same, or two or three times as often: the loop is stretched to the legs' cycle, it does not run at its own rate or stand still)", legHz > 0f && handHz > 0f && hzRatio > 0.85f && hzRatio < 3.4f, $"(ratio {hzRatio:0.00})");
                        yield return new WaitForSeconds(0.8f);
                        player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                        yield return new WaitForSeconds(0.6f);
                    }
                    GunController.TestAim = false;
                    yield return new WaitForSeconds(0.5f);

                    // ---------------- a reload in third person: a long gun goes down to the low ready, a pistol plays its reload; the gun is back at the hip after it
                    GunController.LimitedAmmoForTests = true;
                    yield return null;
                    var mag = gc.Gun.Magazine;
                    for (int i = 0; i < mag.Capacity && mag.Rounds > 0; i++) mag.TryFire(Time.time + i * mag.FireInterval);
                    bool started = mag.StartReload(Time.time);
                    yield return new WaitForSeconds(Mathf.Min(0.7f, mag.ReloadSeconds * 0.4f));
                    float pitch = 90f - Vector3.Angle(hg.Grip.forward, Vector3.up);
                    if (kind == RangedWeapon.Kind.Pew)
                        StanceCheck($"{pre} a reload: the reload clip plays over the stance (the magazine change)", started && anim.GunReloadWeight > 0.9f, $"(reload layer {anim.GunReloadWeight:0.00})");
                    else
                        StanceCheck($"{pre} a reload: the gun goes down to the low ready (the barrel {pitch:0} degrees below level)", started && hg.RaiseNow < 0.1f && pitch < -15f && pitch > -60f, $"(raise {hg.RaiseNow:0.00}, barrel {pitch:0} deg)");
                    ShotCam(Camera.main, tag + "_reload_tp");
                    SideShot(t, tag + "_reload");
                    float rest = mag.ReloadEndsAt + 0.9f - Time.time;
                    if (rest > 0f) yield return new WaitForSeconds(rest);
                    GunController.LimitedAmmoForTests = false;
                    err = BarrelError(hg, CrosshairPoint(t));
                    StanceCheck($"{pre} after the reload the gun is back at the hip on the crosshair, both hands on it", Mathf.Abs(hg.RaiseNow - AimRules.HipRaise) < 0.03f && err < 3f && hg.LeftHandGap < 0.012f, $"(raise {hg.RaiseNow:0.00}, {err:0.00} deg, left hand off the gun {hg.LeftHandGap * 100f:0.0} cm)");

                    // ---------------- first person: the hip (the gun in the picture, no sight), the sights (the red dot, the scope), running
                    rig.SetFirstPerson(true);
                    yield return WaitFor(() => rig.Zoom >= 0.999f, 30f, "first person");
                    GunController.TestAim = false;
                    yield return new WaitForSeconds(1.0f);
                    var cam = Camera.main;
                    var vpGrip = cam.WorldToViewportPoint(hg.Grip.position);
                    err = BarrelError(hg, CrosshairPoint(t));
                    var vpMuzzle = cam.WorldToViewportPoint(hg.Muzzle.position);
                    StanceCheck($"{pre} first person at the hip: the gun is in the picture at the lower right (its muzzle at {vpMuzzle.x:0.00}, {vpMuzzle.y:0.00}; the grip is under the bottom edge, as in the Blender file), no red dot is looked through and no scope is up, the barrel on the crosshair",
                        vpMuzzle.z > 0.1f && vpMuzzle.x > 0.3f && vpMuzzle.x < 1.0f && vpMuzzle.y > 0f && vpMuzzle.y < 0.75f && HeldGun.LocalDotShare < 0.05f && HeldGun.LocalScope < 0.05f && err < 4f, $"(muzzle at {vpMuzzle:F2}, grip at {vpGrip:F2}, dot {HeldGun.LocalDotShare:0.00}, scope {HeldGun.LocalScope:0.00}, {err:0.00} deg, arm mode {HeldGun.LocalArmMode})");
                    ShotCam(cam, tag + "_fp_hip");
                    GunController.TestAim = true;
                    yield return new WaitForSeconds(1.3f);
                    err = BarrelError(hg, CrosshairPoint(t));
                    bool hasDot = spec.CanSight && RangedWeapon.Catalog != null && RangedWeapon.Catalog.Gun((int)kind).sightAt != Vector3.zero;
                    if (kind == RangedWeapon.Kind.Sniper) StanceCheck($"{pre} first person aimed: the scope comes up", hg.RaiseNow > 0.97f && HeldGun.LocalScope > 0.9f, $"(raise {hg.RaiseNow:0.00}, scope {HeldGun.LocalScope:0.00})");
                    else if (hasDot) StanceCheck($"{pre} first person aimed: the view is through the red dot", hg.RaiseNow > 0.97f && HeldGun.LocalDotShare > 0.9f && err < 3f, $"(raise {hg.RaiseNow:0.00}, dot {HeldGun.LocalDotShare:0.00}, {err:0.00} deg)");
                    else StanceCheck($"{pre} first person aimed: the gun is all the way up, the barrel on the crosshair, in the picture", hg.RaiseNow > 0.97f && err < 3f && cam.WorldToViewportPoint(hg.Grip.position).z > 0.1f, $"(raise {hg.RaiseNow:0.00}, {err:0.00} deg, arm mode {HeldGun.LocalArmMode})");
                    ShotCam(cam, tag + "_fp_aim");
                    // running aimed in first person: the sight stays on the line of sight
                    var probe = new GameObject("GunStanceProbe").AddComponent<PistolAnimProbe>();
                    probe.gun = hg; probe.player = t;
                    yield return null; yield return null;
                    player.testMove = Vector3.forward;
                    float worst = 0f, dotMin = 1f, scopeMin = 1f;
                    for (float tm = 0f; tm < 2.0f; tm += Time.deltaTime)
                    {
                        yield return null;
                        if (tm < 0.6f) continue;
                        worst = Mathf.Max(worst, probe.barrelError);
                        dotMin = Mathf.Min(dotMin, HeldGun.LocalDotShare); scopeMin = Mathf.Min(scopeMin, HeldGun.LocalScope);
                    }
                    ShotCam(cam, tag + "_fp_aimrun");
                    player.testMove = null;
                    Destroy(probe.gameObject);
                    bool sightHeld = kind == RangedWeapon.Kind.Sniper ? scopeMin > 0.9f : !hasDot || dotMin > 0.9f;
                    StanceCheck($"{pre} first person, running aimed: the barrel stays on the crosshair (within 3 degrees) and the sight stays up (dot {dotMin:0.00}, scope {scopeMin:0.00})", worst < 3f && sightHeld, $"(worst {worst:0.00} deg, dot {dotMin:0.00}, scope {scopeMin:0.00})");
                    GunController.TestAim = false;
                    rig.SetFirstPerson(false);
                    yield return WaitFor(() => rig.Zoom <= 0.001f, 30f, "third person");
                    player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                    yield return new WaitForSeconds(0.5f);
                }
            }
            if (item) { pack.Drop(item); Destroy(item.gameObject); item = null; }

            // the officers' pistol and a gun without its loops are held the way they were
            WeaponAnimation.StancesEnabled = false;
            var offAnim = PartyCharacterFactory.Attach(new GameObject("GunStanceGuard"), new CharacterLook("guard", Color.white, 1, null, 1f, 1f, "syn_security_male_01"));
            yield return null;
            var gHeld = offAnim ? HeldGun.Attach(offAnim, RangedWeapon.Kind.Pew, false, true, true) : null;
            StanceCheck("an officer's pistol has no stances: the raise / aim / fire clips of before (or the code's hold)", gHeld != null && (gHeld.Clips == null || !gHeld.Clips.Stances), gHeld != null ? "" : "(no gun)");
            WeaponAnimation.StancesEnabled = true;
            GunController.TestAim = null;
            GunController.TestCursorLocked = null;
            Time.captureDeltaTime = 0f;
            Log($"[GunStance] DONE fails={stanceFails}");
            Quit(stanceFails == 0 ? 0 : 1);
        }
    }
}
