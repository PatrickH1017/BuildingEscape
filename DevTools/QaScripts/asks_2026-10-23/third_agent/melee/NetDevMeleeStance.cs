using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The owner's melee holds (2026-10-28, Melee_Hold_Animations: "Idle is the melee hip fire, Ready is the melee aimed"): scenario <c>MeleeStance</c>. The real player in a plain arena far from the building, as Dock worker 1, is given the crowbar, the bat and
    /// the hatchet in turn: each must be held with its four loops (<c>Resources/Animations/&lt;Weapon&gt;_Idle_Stand ...</c>), idle unless the right mouse button is held (then at the ready; the teammates are told), standing and running (the legs'
    /// step), where the Blender files hold it (the wrist from the head, the weapon's pitch), and a swing must start from either and come back to it. First person: the idle is the code's hold at the lower right, the ready is the clip's.
    /// Needs graphics: <c>qa_regress.ps1 -Methods MeleeStance:gfx</c>. Pictures: %TEMP%\be_shot_meleestance_{weapon}_{what}.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int meleeStanceFails;

        void MeleeStanceCheck(string name, bool ok, string detail = "")
        {
            if (!ok) meleeStanceFails++;
            Log($"[MeleeStance] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        // the Blender files at their first frame (weaponpose.py): the right wrist from the head in the character's frame (to his right, forward, up; metres) and the weapon's +Y axis above the horizon (degrees)
        static readonly Vector3 MeleeIdleWrist = new Vector3(0.5317f, 0f, -0.1455f), MeleeReadyWrist = new Vector3(0.5172f, 0f, 0.0245f);          // (horizontal distance, 0, height: the Unity body is turned 14-23 degrees from the Blender character, so the heading is left out)
        static readonly float MeleeIdleDist = 0.5513f, MeleeReadyDist = 0.5178f;          // (the wrist from the head, in 3-D: what the Blender files keep within 4 cm all through the runs)
        static readonly float[] MeleeIdlePitch = { 60.6f, 51.0f, 49.5f }, MeleeReadyPitch = { 47.3f, 55.4f, 56.6f };       // crowbar, bat, hatchet

        /// <summary>The wrist from the head: the horizontal distance, 0, the height over it (the heading is left out: the Unity body is turned 14-23 degrees from the Blender character).</summary>
        static Vector3 WristFromHead(Transform body, Transform wrist, Transform head)
        {
            var d = wrist.position - head.position;
            return new Vector3(new Vector2(d.x, d.z).magnitude, 0f, d.y);
        }

        /// <summary>Is any part of this weapon's model in the picture?</summary>
        static bool WeaponInPicture(Camera cam, Transform weapon)
        {
            foreach (var r in weapon.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var v = cam.WorldToViewportPoint(corner);
                    if (v.z > 0.1f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f) return true;
                }
            }
            return false;
        }

        IEnumerator MeleeStanceScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            hm.godMode = true;
            GunController.TestCursorLocked = true;
            Time.captureDeltaTime = 1f / 30f;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var vis = player.GetComponent<PlayerVisual>();
            var mc = player.GetComponent<MeleeController>();
            var rig = FindFirstObjectByType<CameraFollow>();
            if (!rig || !pack || !vis || !mc) { Log("[MeleeStance] no camera rig, pack, look or melee controller"); Quit(1); yield break; }
            pack.capacity = 100f;
            var kinds = new[] { MeleeWeapon.Kind.Crowbar, MeleeWeapon.Kind.Bat, MeleeWeapon.Kind.Hatchet };
            MeleeStanceCheck("the twelve hold loops are in the build (Resources/Animations: idle / ready, standing / running; humanoid)", kinds.All(k => { var c = WeaponAnimation.Melee(k); return c != null && c.Stances; }),
                $"({string.Join(", ", kinds.Select(k => { var c = WeaponAnimation.Melee(k); return WeaponAnimation.MeleeName(k) + " " + (c != null && c.Stances); }))})");

            var root = new GameObject("MeleeStance");
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
            var side = new GameObject("MeleeStanceCam").AddComponent<Camera>();
            side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = new Color(0.55f, 0.66f, 0.82f); side.enabled = false; side.nearClipPlane = 0.02f;
            void SideShot(Transform t, string name) { side.fieldOfView = 40f; side.transform.position = t.position + t.right * 2.0f + t.up * 0.55f + t.forward * 0.9f; side.transform.LookAt(t.position + t.up * 0.45f + t.forward * 0.35f); ShotCam(side, name + "_side"); }
            void FrontShot(Transform t, string name) { side.fieldOfView = 40f; side.transform.position = t.position + t.forward * 2.6f + t.right * 1.3f + t.up * 0.7f; side.transform.LookAt(t.position + t.up * 0.35f); ShotCam(side, name + "_front"); }
            void RunShot(Transform t, string name) { side.fieldOfView = 40f; side.transform.position = t.position + t.right * 2.6f + t.up * 0.4f + t.forward * 0.8f; side.transform.LookAt(t.position + t.forward * 0.3f); ShotCam(side, name + "_run"); }

            var cat = CharacterCatalog.Instance;
            var firstId = PlayerAppearance.DefaultToons.First();
            vis.Apply(new CharacterLook(firstId, Color.white, 1, null, 1f, 1f, firstId));
            var swingMethod = typeof(MeleeController).GetMethod("Swing", BindingFlags.NonPublic | BindingFlags.Instance);
            Pickup item = null;
            for (int ki = 0; ki < kinds.Length; ki++)
            {
                var kind = kinds[ki];
                string wname = WeaponAnimation.MeleeName(kind);
                string tag = $"meleestance_{wname}";
                string pre = $"{wname}:";
                if (item) { pack.Drop(item); Destroy(item.gameObject); item = null; yield return new WaitForSeconds(0.2f); }
                GunController.TestAim = false;
                player.testMove = null;
                player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                rig.SetFirstPerson(false);
                yield return WaitFor(() => rig.Zoom <= 0.001f, 30f, "third person");
                rig.TurnAim(-rig.AimYaw);
                rig.TiltAim(-rig.AimPitch + 8f);
                yield return new WaitForSeconds(0.3f);
                item = MeleeWeapon.Create(kind, player.transform.position + Vector3.up * 0.5f, null, 0f).GetComponent<Pickup>();
                pack.TryStore(item);
                yield return null;
                pack.SelectSlot(0);
                yield return new WaitForSeconds(1.4f);
                player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                yield return new WaitForSeconds(0.5f);
                var anim = vis.Anim;
                var t = player.transform;
                bool held = mc.Animated && anim && anim.MeleeStanceActive && anim.MeleeWeight > 0.99f && anim.MeleeClipsNow == WeaponAnimation.Melee(kind);
                MeleeStanceCheck($"{pre} held with its hold loops, the upper body theirs", held, $"(animated {mc.Animated}, stances {(anim != null && anim.MeleeStanceActive)}, upper body {(anim ? anim.MeleeWeight : -1f):0.00})");
                if (!held) continue;
                var animator = anim.GetComponent<Animator>();
                var rWrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var weapon = mc.HeldModel.transform;
                float Pitch() => Mathf.Asin(Mathf.Clamp(Vector3.Dot(weapon.up, Vector3.up), -1f, 1f)) * Mathf.Rad2Deg;

                // ---------------- idle, standing
                var wrist = WristFromHead(t, rWrist, head);
                float off = (wrist - MeleeIdleWrist).magnitude;
                MeleeStanceCheck($"{pre} idle (the right mouse button up): the weapon is not at the ready, the run share is nil, the wrist is where the Blender file has it from the head (within 6 cm) and the weapon points up at {MeleeIdlePitch[ki]:0} degrees (within 8)",
                    !mc.Ready && !player.IsAiming && mc.ReadyShare < 0.05f && anim.RunShare < 0.05f && off < 0.06f && Mathf.Abs(Pitch() - MeleeIdlePitch[ki]) < 8f, $"(wrist {wrist:F2} wanted {MeleeIdleWrist:F2}, {off * 100f:0.0} cm off; pitch {Pitch():0.0}, ready {mc.ReadyShare:0.00}, run {anim.RunShare:0.00})");
                SideShot(t, tag + "_idle"); FrontShot(t, tag + "_idle");
                ShotCam(Camera.main, tag + "_idle_tp");

                // ---------------- the ready: the right mouse button
                GunController.TestAim = true;
                yield return new WaitForSeconds(0.1f);
                float mid = mc.ReadyShare;
                yield return new WaitForSeconds(0.9f);
                wrist = WristFromHead(t, rWrist, head);
                off = (wrist - MeleeReadyWrist).magnitude;
                MeleeStanceCheck($"{pre} the right mouse button raises the weapon to the ready (in transit {mid:0.00}, then 1), the teammates are told, the wrist is where the Blender file has it (within 6 cm), the weapon points up at {MeleeReadyPitch[ki]:0} degrees (within 8)",
                    mid > 0.05f && mid < 0.99f && mc.Ready && player.IsAiming && mc.ReadyShare > 0.97f && off < 0.06f && Mathf.Abs(Pitch() - MeleeReadyPitch[ki]) < 8f, $"(wrist {wrist:F2} wanted {MeleeReadyWrist:F2}, {off * 100f:0.0} cm off; pitch {Pitch():0.0}, ready {mc.ReadyShare:0.00})");
                SideShot(t, tag + "_ready"); FrontShot(t, tag + "_ready");
                ShotCam(Camera.main, tag + "_ready_tp");
                GunController.TestAim = false;
                yield return new WaitForSeconds(0.9f);
                MeleeStanceCheck($"{pre} letting go brings it back to idle", !mc.Ready && !player.IsAiming && mc.ReadyShare < 0.03f, $"(ready {mc.ReadyShare:0.00})");

                // ---------------- running, idle and at the ready: the run loop is shown, the legs run, the weapon stays in the hand
                foreach (bool ready in new[] { false, true })
                {
                    string st = ready ? "at the ready" : "idle";
                    GunController.TestAim = ready;
                    yield return new WaitForSeconds(0.9f);
                    float standLo = float.MaxValue, standHi = float.MinValue;
                    for (float tm = 0f; tm < 1.0f; tm += Time.deltaTime) { yield return null; float hy = rWrist.position.y - t.position.y; standLo = Mathf.Min(standLo, hy); standHi = Mathf.Max(standHi, hy); }
                    player.testMove = Vector3.forward;
                    float footMin = float.MaxValue, footMax = float.MinValue, layerMin = 1f, runMin = 1f, runLo = float.MaxValue, runHi = float.MinValue, worstOff = 0f;
                    for (float tm = 0f; tm < 2.4f; tm += Time.deltaTime)
                    {
                        yield return null;
                        if (tm < 0.7f) continue;
                        float z = Vector3.Dot(foot.position - hips.position, t.forward);
                        footMin = Mathf.Min(footMin, z); footMax = Mathf.Max(footMax, z);
                        layerMin = Mathf.Min(layerMin, anim.MeleeWeight);
                        runMin = Mathf.Min(runMin, anim.RunShare);
                        float hy = rWrist.position.y - t.position.y;
                        runLo = Mathf.Min(runLo, hy); runHi = Mathf.Max(runHi, hy);
                        worstOff = Mathf.Max(worstOff, Mathf.Abs((rWrist.position - head.position).magnitude - (ready ? MeleeReadyDist : MeleeIdleDist)));          // (the distance from the head: the body leans and wobbles while running, which turns the vector but not its length; Blender's own run keeps it within 4 cm)
                    }
                    RunShot(t, tag + (ready ? "_ready" : "_idle"));
                    player.testMove = null;
                    MeleeStanceCheck($"{pre} running {st}: the run loop is shown (run share {runMin:0.00}), the legs run (the foot swings {footMax - footMin:0.00} m), the weapon layer stays on ({layerMin:0.00}) and the weapon stays in the hand where the file has it (the wrist within 12 cm of it at its worst)",
                        runMin > 0.9f && footMax - footMin > 0.15f && layerMin > 0.99f && worstOff < 0.06f, $"(run {runMin:0.00}, foot {footMax - footMin:0.00} m, layer {layerMin:0.00}, wrist off at most {worstOff * 100f:0.0} cm)");
                    MeleeStanceCheck($"{pre} running {st}: the weapon moves with the run (the hand rises and falls {(runHi - runLo) * 100f:0.0} cm; standing {(standHi - standLo) * 100f:0.0} cm)", runHi - runLo > (standHi - standLo) + 0.004f, $"(run {(runHi - runLo) * 100f:0.0} cm, standing {(standHi - standLo) * 100f:0.0} cm)");
                    yield return new WaitForSeconds(0.8f);
                    player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                    yield return new WaitForSeconds(0.6f);
                }

                // ---------------- a swing from the idle and from the ready: it plays, the weapon comes back to the hold
                var mw = item.GetComponent<MeleeWeapon>();
                foreach (bool ready in new[] { false, true })
                {
                    string st = ready ? "the ready" : "the idle";
                    GunController.TestAim = ready;
                    yield return new WaitForSeconds(1.0f);
                    float start = Time.time; bool slashed = false;
                    mc.StartCoroutine((IEnumerator)swingMethod.Invoke(mc, new object[] { mw }));
                    float cooldown = mw.Data.cooldown;
                    int pic = 0;
                    while (Time.time - start < cooldown + 0.3f)
                    {
                        yield return null;
                        slashed |= anim.Slashing;
                        float e = Time.time - start;
                        if (pic == 0 && e >= 0.35f * cooldown) { SideShot(t, tag + (ready ? "_readyswing" : "_idleswing") + "35"); pic++; }
                        else if (pic == 1 && e >= 0.6f * cooldown) { SideShot(t, tag + (ready ? "_readyswing" : "_idleswing") + "60"); pic++; }
                    }
                    yield return new WaitForSeconds(0.5f);
                    wrist = WristFromHead(t, rWrist, head);
                    off = (wrist - (ready ? MeleeReadyWrist : MeleeIdleWrist)).magnitude;
                    MeleeStanceCheck($"{pre} a swing from {st} plays (the attack is shown) and the weapon is back in the hold after it (the wrist within 6 cm, the hold weights back)", slashed && !anim.Slashing && anim.MeleeWeight > 0.99f && off < 0.06f && Mathf.Abs(mc.ReadyShare - (ready ? 1f : 0f)) < 0.05f, $"(slashed {slashed}, wrist {off * 100f:0.0} cm off, ready {mc.ReadyShare:0.00})");
                }
                GunController.TestAim = false;
                yield return new WaitForSeconds(0.8f);

                // ---------------- first person: the idle is the code's hold at the lower right of the view, the ready is the clip's (the fist and the handle at the right)
                rig.SetFirstPerson(true);
                yield return WaitFor(() => rig.Zoom >= 0.999f, 30f, "first person");
                yield return new WaitForSeconds(1.0f);
                var cam = Camera.main;
                var vpTip = cam.WorldToViewportPoint(weapon.TransformPoint(Vector3.up * 0.4f));
                MeleeStanceCheck($"{pre} first person, idle: the weapon is in the picture at the lower right (at {vpTip.x:0.00}, {vpTip.y:0.00})", vpTip.z > 0.1f && vpTip.x > 0.45f && vpTip.x < 1.0f && vpTip.y > 0f && vpTip.y < 1f && mc.ReadyShare < 0.05f, $"(at {vpTip:F2})");
                ShotCam(cam, tag + "_fp_idle");
                GunController.TestAim = true;
                yield return new WaitForSeconds(1.2f);
                var vpHand = cam.WorldToViewportPoint(rWrist.position);
                var vpTip2 = cam.WorldToViewportPoint(weapon.TransformPoint(Vector3.up * 0.4f));
                MeleeStanceCheck($"{pre} first person, ready: the fist is at the right of the picture and the weapon is in it (fist at {vpHand.x:0.00}, {vpHand.y:0.00})", vpHand.z > 0.1f && vpHand.x > 0.6f && mc.ReadyShare > 0.97f && WeaponInPicture(cam, weapon), $"(fist {vpHand:F2} - at or past the right edge, the arm is out of the picture; weapon in the picture {WeaponInPicture(cam, weapon)}, ready {mc.ReadyShare:0.00})");
                ShotCam(cam, tag + "_fp_ready");
                // running at the ready in first person: the weapon stays in the picture
                player.testMove = Vector3.forward;
                int inView = 0, frames = 0;
                for (float tm = 0f; tm < 2.0f; tm += Time.deltaTime)
                {
                    yield return null;
                    if (tm < 0.6f) continue;
                    var v = cam.WorldToViewportPoint(rWrist.position);
                    frames++; if (v.z > 0.1f && v.x > 0.3f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f) inView++;
                }
                ShotCam(cam, tag + "_fp_readyrun");
                player.testMove = null;
                MeleeStanceCheck($"{pre} first person, running at the ready: the fist stays in the picture", frames > 0 && inView >= frames * 0.9f, $"({inView} of {frames} frames)");
                GunController.TestAim = false;
                rig.SetFirstPerson(false);
                yield return WaitFor(() => rig.Zoom <= 0.001f, 30f, "third person");
                player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                yield return new WaitForSeconds(0.5f);
            }
            if (item) { pack.Drop(item); Destroy(item.gameObject); item = null; }

            // the holds off: the weapon is held the way it was before them
            WeaponAnimation.StancesEnabled = false;
            var c0 = WeaponAnimation.Melee(MeleeWeapon.Kind.Crowbar);
            MeleeStanceCheck("with the holds switched off (WeaponAnimation.StancesEnabled) the crowbar's clips have no stances: the swing file's own ready pose", c0 != null && !c0.Stances);
            WeaponAnimation.StancesEnabled = true;
            GunController.TestAim = null;
            GunController.TestCursorLocked = null;
            Time.captureDeltaTime = 0f;
            Log($"[MeleeStance] DONE fails={meleeStanceFails}");
            Quit(meleeStanceFails == 0 ? 0 : 1);
        }
    }
}
