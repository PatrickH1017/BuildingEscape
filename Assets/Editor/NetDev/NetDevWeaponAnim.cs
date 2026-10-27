using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The other weapons' Blender animations (the owner, 2026-10-27: "guards pistol shots/reloads, the hatchet swing, crowbar swing, bat swing, sniper fire, shotgun fire, rife fire and smg fire ... do the same thing with these files and
    /// attach the animations"). In the PistolAnim scenario's plain arena the real player is dressed as Dock worker 1 and Dock worker 5 (the office developer, other proportions) and given each long gun and each melee weapon in turn:
    /// a long gun must be held with its clips in both hands where the Blender file holds it (both wrists where Blender has them on the gun), point at the crosshair level, down and up, kick on a shot and come back, work its bolt
    /// or pump, go down to the low ready for a reload in third person and keep the sight picture in first person; a melee weapon must be held ready, swing with its clip and land the blow (on a training dummy) when the clip's
    /// weapon is at its fastest, as often as before. Then two officers get the pistol (their own clips: raised from the side, aimed, a shot, the reload) and a rifle. Needs graphics: <c>qa_regress.ps1 -Methods WeaponAnim:gfx</c>.
    /// Pictures: %TEMP%\be_shot_weaponanim_{who}_{weapon}_{what}.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int weaponAnimFails;

        void WeaponAnimCheck(string name, bool ok, string detail = "")
        {
            if (!ok) weaponAnimFails++;
            Log($"[WeaponAnim] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        // where the wrists are on each long gun in the Blender files' aim (measure.py: the wrist in the gun model's own space, Blender's units; Unity's model is the same with its x mirrored and a hundredth of the size)
        static readonly Vector3[] BlenderRightWrist = { Vector3.zero, new Vector3(-3.5675f, -0.441f, -8.5897f), new Vector3(-3.9746f, -2.9083f, -11.9227f), new Vector3(-4.7581f, -0.5732f, -10.8616f), new Vector3(-4.8716f, -2.7305f, -15.7731f) };
        static readonly Vector3[] BlenderLeftWrist = { Vector3.zero, new Vector3(3.556f, -5.4926f, 12.1899f), new Vector3(9.1795f, 2.5018f, 25.9206f), new Vector3(8.1208f, -1.1403f, 33.488f), new Vector3(10.5196f, -2.6905f, 41.5047f) };

        /// <summary>How far (m) a wrist is from where the Blender file has it on the gun.</summary>
        static float WristOffGun(Transform model, Transform wrist, Vector3 blender)
        {
            var want = model.TransformPoint(new Vector3(-blender.x, blender.y, blender.z) * 0.01f);
            return (wrist.position - want).magnitude;
        }

        /// <summary>The barrel's angle above the level (degrees; negative: down).</summary>
        static float BarrelPitch(HeldGun hg) => 90f - Vector3.Angle(hg.Grip.forward, Vector3.up);

        IEnumerator WeaponAnimScenario()
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
            var mc = player.GetComponent<MeleeController>();
            var rig = FindFirstObjectByType<CameraFollow>();
            if (!rig || !pack || !vis || !gc || !mc) { Log("[WeaponAnim] no camera rig, pack, look, gun or melee controller"); Quit(1); yield break; }
            pack.capacity = 100f;
            var longGuns = new[] { RangedWeapon.Kind.Smg, RangedWeapon.Kind.Rifle, RangedWeapon.Kind.Shotgun, RangedWeapon.Kind.Sniper };
            var meleeKinds = new[] { MeleeWeapon.Kind.Crowbar, MeleeWeapon.Kind.Bat, MeleeWeapon.Kind.Hatchet };
            WeaponAnimCheck("the clips are in the build (Resources/Animations; humanoid)", longGuns.All(k => WeaponAnimation.Gun(k) != null) && meleeKinds.All(k => WeaponAnimation.Melee(k) != null) && WeaponAnimation.Guard() != null,
                $"(guns {string.Join(",", longGuns.Select(k => WeaponAnimation.Gun(k) != null))}, melee {string.Join(",", meleeKinds.Select(k => WeaponAnimation.Melee(k) != null))}, officers {WeaponAnimation.Guard() != null})");

            // the arena (as PistolAnim's) and a training dummy two metres in front, for the blows
            var root = new GameObject("WeaponAnim");
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
            var side = new GameObject("WeaponAnimCam").AddComponent<Camera>();
            side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = new Color(0.55f, 0.66f, 0.82f); side.enabled = false; side.nearClipPlane = 0.02f;
            void SideShot(Transform t, string name)
            {
                side.fieldOfView = 40f;
                side.transform.position = t.position + t.right * 2.0f + t.up * 0.55f + t.forward * 0.9f;
                side.transform.LookAt(t.position + t.up * 0.45f + t.forward * 0.35f);
                ShotCam(side, name + "_side");
            }
            void FrontShot(Transform t, string name)
            {
                side.fieldOfView = 40f;
                side.transform.position = t.position + t.forward * 2.6f + t.right * 1.3f + t.up * 0.7f;
                side.transform.LookAt(t.position + t.up * 0.35f);
                ShotCam(side, name + "_front");
            }

            var cat = CharacterCatalog.Instance;
            Pickup item = null;
            foreach (var id in new[] { PlayerAppearance.DefaultToons[0], PlayerAppearance.DefaultToons[4] })
            {
                var ch = cat.Find(id);
                if (ch == null) { WeaponAnimCheck($"{id}: in the catalog", false); continue; }
                string who = cat.DockLabel(ch).Replace(" ", "");
                vis.Apply(new CharacterLook(id, Color.white, 1, null, 1f, 1f, id));
                yield return new WaitForSeconds(0.3f);

                // ---------------------------------------------------------------- the long guns
                foreach (var kind in longGuns)
                {
                    string gname = WeaponAnimation.GunName(kind);
                    string tag = $"weaponanim_{who}_{gname}";
                    if (item) { pack.Drop(item); Destroy(item.gameObject); item = null; yield return new WaitForSeconds(0.2f); }
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
                    yield return new WaitForSeconds(1.2f);
                    player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                    yield return new WaitForSeconds(0.5f);
                    var hg = gc.Held;
                    var anim = vis.Anim;
                    var t = player.transform;
                    bool animated = hg != null && hg.Animated && hg.TwoHanded && hg.Clips == WeaponAnimation.Gun(kind) && anim && anim.GunWeight > 0.99f;
                    WeaponAnimCheck($"{who} {gname}: held in both hands with its Blender animation", animated, hg != null ? $"(animated {hg.Animated}, two hands {hg.TwoHanded}, clips {(hg.Clips != null ? hg.Clips.name : "none")}, upper body {(anim ? anim.GunWeight : -1f):0.00})" : "(no gun)");
                    if (!animated) continue;
                    var animator = anim.GetComponent<Animator>();
                    var rWrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    var lWrist = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    var model = hg.Grip.Find(RangedWeapon.Specs[(int)kind].name + "_Model");

                    float err = BarrelError(hg, CrosshairPoint(t));
                    WeaponAnimCheck($"{who} {gname}: third person - the barrel points at the crosshair (within 3 degrees)", err < 3f, $"({err:0.00} deg)");
                    float rOff = WristOffGun(model, rWrist, BlenderRightWrist[(int)kind]), lOff = WristOffGun(model, lWrist, BlenderLeftWrist[(int)kind]);
                    WeaponAnimCheck($"{who} {gname}: both hands on the gun where the Blender file has them (the right wrist within 1 cm, the left within 4 cm)", rOff < 0.01f && lOff < 0.04f && hg.LeftHandGap < 0.01f, $"(right {rOff * 100f:0.0} cm, left {lOff * 100f:0.0} cm, left hand left behind by the turn {hg.LeftHandGap * 100f:0.0} cm, gun model scale {model.lossyScale.x:0.000})");
                    SideShot(t, tag);
                    FrontShot(t, tag);
                    side.fieldOfView = 30f;
                    side.transform.position = hg.Grip.position + t.right * 0.55f + t.up * 0.15f - t.forward * 0.05f;
                    side.transform.LookAt(hg.Grip.position + t.forward * 0.2f);
                    ShotCam(side, tag + "_hand");
                    side.transform.position = hg.Grip.position - t.right * 0.55f + t.up * 0.15f + t.forward * 0.1f;
                    side.transform.LookAt(hg.Grip.position + t.forward * 0.2f);
                    ShotCam(side, tag + "_handleft");
                    ShotCam(Camera.main, tag + "_tp");

                    // a shot: the kick of the shot clip, then back on the aim; the bolt / the pump
                    var bolt = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.Contains(WeaponAnimation.BoltPart));
                    var boltRest = bolt ? bolt.localPosition : Vector3.zero;
                    gc.Fire();
                    yield return null;
                    float boltBack = bolt ? (boltRest - bolt.localPosition).z : -1f;
                    yield return new WaitForSeconds(1f / 30f);
                    float kickAngle = Vector3.Angle(hg.Grip.forward, CrosshairPoint(t) - hg.Grip.position);
                    SideShot(t, tag + "_kick");
                    float pump = 0f;
                    if (kind == RangedWeapon.Kind.Shotgun)
                    {
                        yield return new WaitForSeconds(0.32f - 2f / 30f);
                        pump = hg.PumpBack;
                        side.fieldOfView = 30f;
                        side.transform.position = hg.Grip.position + t.right * 0.6f + t.up * 0.1f + t.forward * 0.25f;
                        side.transform.LookAt(hg.Grip.position + t.forward * 0.3f);
                        ShotCam(side, tag + "_pump");
                    }
                    yield return new WaitForSeconds(kind == RangedWeapon.Kind.Sniper ? 1.5f : 0.9f);
                    float after = BarrelError(hg, CrosshairPoint(t));
                    float minKick = kind == RangedWeapon.Kind.Smg ? 1.2f : kind == RangedWeapon.Kind.Rifle ? 1.5f : 6f;
                    bool parts = kind == RangedWeapon.Kind.Shotgun ? pump > 0.9f : boltBack > 0.01f;
                    WeaponAnimCheck($"{who} {gname}: a shot kicks the muzzle up (the shot clip), works the {(kind == RangedWeapon.Kind.Shotgun ? "pump" : "bolt")} and the gun comes back on the crosshair", kickAngle > minKick && after < 3f && parts,
                        $"(kick {kickAngle:0.0} deg, after {after:0.00} deg, bolt back {boltBack * 100f:0.0} cm of the model, pump {pump:0.00}, shots {gc.ShotsFired})");

                    // looking down and up
                    rig.TiltAim(25f);
                    yield return new WaitForSeconds(0.5f);
                    err = BarrelError(hg, CrosshairPoint(t));
                    WeaponAnimCheck($"{who} {gname}: third person, looking down - the barrel on the crosshair, both hands on the gun", err < 3f && hg.LeftHandGap < 0.02f, $"({err:0.00} deg, left hand off by {hg.LeftHandGap * 100f:0.0} cm, pitch {rig.AimPitch:0})");
                    SideShot(t, tag + "_down");
                    rig.TiltAim(-50f);
                    yield return new WaitForSeconds(0.5f);
                    err = BarrelError(hg, CrosshairPoint(t));
                    WeaponAnimCheck($"{who} {gname}: third person, looking up - the barrel on the crosshair, both hands on the gun", err < 3f && hg.LeftHandGap < 0.02f, $"({err:0.00} deg, left hand off by {hg.LeftHandGap * 100f:0.0} cm, pitch {rig.AimPitch:0})");
                    SideShot(t, tag + "_up");
                    rig.TiltAim(-rig.AimPitch + 8f);
                    yield return new WaitForSeconds(0.4f);

                    // walking: the legs walk under the gun held up
                    var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    float footMin = float.MaxValue, footMax = float.MinValue, layerMin = 1f, worstWalk = 0f;
                    player.testMove = Vector3.forward;
                    for (float tm = 0f; tm < 1.5f; tm += Time.deltaTime)
                    {
                        yield return null;
                        if (tm < 0.5f) continue;
                        float z = Vector3.Dot(foot.position - t.position, t.forward);
                        footMin = Mathf.Min(footMin, z); footMax = Mathf.Max(footMax, z);
                        layerMin = Mathf.Min(layerMin, anim.GunWeight);
                        worstWalk = Mathf.Max(worstWalk, hg.LeftHandGap);
                    }
                    side.fieldOfView = 40f;
                    side.transform.position = t.position + t.right * 2.6f + t.up * 0.4f + t.forward * 0.8f;
                    side.transform.LookAt(t.position + t.forward * 0.3f);
                    ShotCam(side, tag + "_walk");
                    player.testMove = null;
                    WeaponAnimCheck($"{who} {gname}: walking - the legs walk while the gun stays up in both hands", footMax - footMin > 0.15f && layerMin > 0.99f && worstWalk < 0.02f, $"(the foot swings {footMax - footMin:0.00} m, gun layer at least {layerMin:0.00}, left hand off by up to {worstWalk * 100f:0.0} cm)");
                    yield return new WaitForSeconds(0.5f);
                    player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                    yield return new WaitForSeconds(0.4f);

                    // the reload in third person: down to the low ready (the file has no reload) and up again after
                    GunController.LimitedAmmoForTests = true;
                    yield return null;
                    var mag = gc.Gun.Magazine;
                    for (int i = 0; i < mag.Capacity && mag.Rounds > 0; i++) mag.TryFire(Time.time + i * mag.FireInterval);
                    mag.StartReload(Time.time);
                    yield return new WaitForSeconds(Mathf.Min(0.9f, mag.ReloadSeconds * 0.5f));
                    float lowPitch = BarrelPitch(hg) - (90f - Vector3.Angle(t.forward, Vector3.up));
                    WeaponAnimCheck($"{who} {gname}: a reload in third person - the gun goes down to the low ready of the file (the muzzle about 35 degrees down)", mag.Reloading && lowPitch < -25f && lowPitch > -50f, $"(the barrel {lowPitch:0.0} deg from level, reloading {mag.Reloading})");
                    SideShot(t, tag + "_lowready");
                    float rest = mag.ReloadEndsAt + 0.8f - Time.time;
                    if (rest > 0f) yield return new WaitForSeconds(rest);
                    err = BarrelError(hg, CrosshairPoint(t));
                    WeaponAnimCheck($"{who} {gname}: after the reload the gun is back on the crosshair", !mag.Reloading && err < 3f, $"({err:0.00} deg)");
                    GunController.LimitedAmmoForTests = false;

                    // first person: the sight picture of before (the pack's layer puts the sight on the line of sight)
                    rig.SetFirstPerson(true);
                    yield return WaitFor(() => rig.Zoom >= 0.999f, 30f, "first person");
                    yield return new WaitForSeconds(0.6f);
                    err = BarrelError(hg, CrosshairPoint(t));
                    var vpMuzzle = Camera.main.WorldToViewportPoint(hg.Muzzle.position);
                    bool scoped = kind == RangedWeapon.Kind.Sniper && HeldGun.LocalScope > 0.9f;
                    WeaponAnimCheck($"{who} {gname}: first person - the sight picture as before (the barrel on the crosshair, the muzzle in the middle of the picture or the scope up)", err < 3f && (scoped || (vpMuzzle.z > 0f && Mathf.Abs(vpMuzzle.x - 0.5f) < 0.12f && Mathf.Abs(vpMuzzle.y - 0.5f) < 0.2f)),
                        $"({err:0.00} deg, muzzle at {vpMuzzle:F2}, aligned by the layer {hg.AlignerUsed}, scope {HeldGun.LocalScope:0.00}, dot {HeldGun.LocalDotShare:0.00})");
                    ShotCam(Camera.main, tag + "_fp");
                    gc.Fire();
                    yield return new WaitForSeconds(2f / 30f);
                    ShotCam(Camera.main, tag + "_fpshot");
                    yield return new WaitForSeconds(0.8f);
                    rig.SetFirstPerson(false);
                    yield return WaitFor(() => rig.Zoom <= 0.001f, 30f, "third person");
                    yield return new WaitForSeconds(0.3f);
                }
                if (item) { pack.Drop(item); Destroy(item.gameObject); item = null; yield return new WaitForSeconds(0.3f); }

                // ---------------------------------------------------------------- the melee weapons
                var swingMethod = typeof(MeleeController).GetMethod("Swing", BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var kind in meleeKinds)
                {
                    string mname = WeaponAnimation.MeleeName(kind);
                    string tag = $"weaponanim_{who}_{mname}";
                    if (item) { pack.Drop(item); Destroy(item.gameObject); item = null; yield return new WaitForSeconds(0.2f); }
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
                    yield return new WaitForSeconds(1.0f);
                    player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
                    yield return new WaitForSeconds(0.5f);
                    var anim = vis.Anim;
                    var t = player.transform;
                    bool animated = mc.Animated && anim && anim.MeleeWeight > 0.99f && anim.MeleeClipsNow == WeaponAnimation.Melee(kind);
                    WeaponAnimCheck($"{who} {mname}: held ready in front with its Blender animation", animated, $"(animated {mc.Animated}, upper body {(anim ? anim.MeleeWeight : -1f):0.00})");
                    if (!animated) continue;
                    var weapon = mc.HeldModel.transform;
                    float headUp = Vector3.Dot(weapon.up, Vector3.up);
                    WeaponAnimCheck($"{who} {mname}: the ready pose holds the weapon's head up", headUp > 0.4f, $"(the weapon's head {Mathf.Asin(Mathf.Clamp(headUp, -1f, 1f)) * Mathf.Rad2Deg:0} deg above the level)");
                    SideShot(t, tag);
                    FrontShot(t, tag);
                    side.fieldOfView = 30f;
                    side.transform.position = weapon.position + t.right * 0.6f + t.up * 0.1f;
                    side.transform.LookAt(weapon.position);
                    ShotCam(side, tag + "_hand");

                    // the swing at a training dummy in front: the blow lands when the clip's weapon is at its fastest
                    var dummy = TrainingDummy.Create(root.transform, t.position + t.forward * 1.3f - Vector3.up * 1.1f, 180f, Flat(new Color(0.7f, 0.6f, 0.45f)), Flat(new Color(0.45f, 0.3f, 0.2f)), Flat(Color.red));
                    yield return new WaitForSeconds(0.3f);
                    float health0 = dummy.Health, start = Time.time, hitAt = -1f, fastest = 0f, fastestAt = -1f;
                    var tip = weapon.TransformPoint(Vector3.up * 0.5f);
                    var mw = item.GetComponent<MeleeWeapon>();
                    mc.StartCoroutine((IEnumerator)swingMethod.Invoke(mc, new object[] { mw }));
                    float expectHit = mc.HitDelayNow, cooldown = mw.Data.cooldown;
                    bool slashing = false;
                    var shotsAt = new[] { 0.3f, 0.5f, 0.75f };
                    int shotIdx = 0;
                    while (Time.time - start < cooldown + 0.3f)
                    {
                        yield return null;
                        float e = Time.time - start;
                        slashing |= anim.Slashing;
                        if (hitAt < 0f && dummy.Health < health0) hitAt = e;
                        var tipNow = weapon.TransformPoint(Vector3.up * 0.5f);
                        float speed = (tipNow - tip).magnitude / Mathf.Max(1e-4f, Time.deltaTime);
                        tip = tipNow;
                        if (e > 0.05f && speed > fastest) { fastest = speed; fastestAt = e; }
                        if (shotIdx < shotsAt.Length && e >= shotsAt[shotIdx] * cooldown) { SideShot(t, $"{tag}_swing{(int)(shotsAt[shotIdx] * 100)}"); shotIdx++; }
                    }
                    WeaponAnimCheck($"{who} {mname}: the swing plays its clip (the attack is shown) and lands on the dummy when the weapon is at its fastest (within 0.07 s)", slashing && hitAt > 0f && Mathf.Abs(hitAt - expectHit) < 0.05f && Mathf.Abs(fastestAt - expectHit) < 0.07f,
                        $"(blow at {hitAt:0.00} s, expected {expectHit:0.00} s, the weapon fastest at {fastestAt:0.00} s ({fastest:0.0} m/s), dummy {health0:0} -> {dummy.Health:0})");
                    WeaponAnimCheck($"{who} {mname}: the swing takes the weapon's cooldown ({cooldown:0.00} s) and ends on the ready", !anim.Slashing && anim.MeleeWeight > 0.99f && Vector3.Dot(weapon.up, Vector3.up) > 0.4f, $"(slashing {anim.Slashing}, layer {anim.MeleeWeight:0.00})");
                    Destroy(dummy.gameObject);

                    // first person: the swing goes through the picture
                    rig.SetFirstPerson(true);
                    yield return WaitFor(() => rig.Zoom >= 0.999f, 30f, "first person");
                    yield return new WaitForSeconds(0.4f);
                    ShotCam(Camera.main, tag + "_fp");
                    start = Time.time;
                    mc.StartCoroutine((IEnumerator)swingMethod.Invoke(mc, new object[] { mw }));
                    bool seen = false;
                    int fpShot = 1, inView = 0, frames = 0;
                    while (Time.time - start < cooldown + 0.2f)
                    {
                        yield return null;
                        var v = Camera.main.WorldToViewportPoint(weapon.TransformPoint(Vector3.up * 0.4f));
                        bool here = v.z > 0.05f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
                        seen |= here;
                        float e = Time.time - start;
                        if (e > expectHit && e < cooldown - 0.15f) { frames++; if (here) inView++; }
                        if (fpShot < 10 && Time.time - start >= fpShot * 0.1f * cooldown) { ShotCam(Camera.main, $"{tag}_fpswing{fpShot * 10}"); fpShot++; }
                    }
                    WeaponAnimCheck($"{who} {mname}: first person - the weapon's head goes through the picture during the swing, and stays in it from the blow through the follow-through", seen && frames > 0 && inView >= frames - 1, $"(in the picture {inView} of {frames} frames after the blow)");
                    if (who == "Dockworker1")
                    {
                        // the same without the Blender animation, to compare (the hold and the slash clip of before 2026-10-27)
                        WeaponAnimation.MeleeEnabled = false;
                        pack.SetEquipped(null); yield return new WaitForSeconds(0.3f); pack.SetEquipped(item); yield return new WaitForSeconds(0.6f);
                        ShotCam(Camera.main, tag + "_fp_before");
                        rig.SetFirstPerson(false);
                        yield return WaitFor(() => rig.Zoom <= 0.001f, 30f, "third person");
                        yield return new WaitForSeconds(0.4f);
                        SideShot(t, tag + "_before");
                        WeaponAnimation.MeleeEnabled = true;
                        pack.SetEquipped(null); yield return new WaitForSeconds(0.3f); pack.SetEquipped(item); yield return new WaitForSeconds(0.6f);
                    }
                    rig.SetFirstPerson(false);
                    yield return WaitFor(() => rig.Zoom <= 0.001f, 30f, "third person");
                    yield return new WaitForSeconds(0.3f);
                }
                if (item) { pack.Drop(item); Destroy(item.gameObject); item = null; yield return new WaitForSeconds(0.3f); }
            }

            // ---------------------------------------------------------------- the officers: their pistol (raised from the side, aimed, a shot, the reload) and a rifle
            int n = 0;
            foreach (var gid in new[] { "syn_security_male_01", "syn_security_female_01" })
            {
                var g = new GameObject("WeaponAnimOfficer" + n);
                g.transform.position = root.transform.position + new Vector3(5f + 3f * n, 1f, 0f);          // (the middle of a guard's capsule: his feet on the ground, away from the player)
                n++;
                var gAnim = PartyCharacterFactory.Attach(g, new CharacterLook("guard", Color.white, 1, null, 1f, 1f, gid));
                yield return null;
                var gHeld = gAnim ? HeldGun.Attach(gAnim, RangedWeapon.Kind.Pew, false, true, true) : null;
                bool ok = gHeld != null && gHeld.Animated && gHeld.Clips == WeaponAnimation.Guard();
                WeaponAnimCheck($"officer {gid}: the pistol is held with the officers' Blender animation", ok, gHeld != null ? $"(animated {gHeld.Animated}, clips {(gHeld.Clips != null ? gHeld.Clips.name : "none")})" : "(no gun)");
                if (!ok) continue;
                var gt = g.transform;
                string tag = $"weaponanim_officer{n}";
                gHeld.Raise = 0f; gHeld.AimDirection = gt.forward;
                yield return new WaitForSeconds(0.8f);
                float lowered = BarrelPitch(gHeld);
                SideShot(gt, tag + "_lowered");
                gHeld.Raise = 1f;
                var target = gt.position + gt.forward * 8f + Vector3.up * 1.0f;
                for (float tm = 0f; tm < 1.0f; tm += Time.deltaTime) { gHeld.AimDirection = (target - gHeld.Grip.position).normalized; yield return null; }
                float gErr = Vector3.Angle(gHeld.Grip.forward, target - gHeld.Grip.position);
                WeaponAnimCheck($"officer {gid}: lowered at his side, raised onto the target", lowered < -45f && gErr < 3f, $"(lowered {lowered:0} deg from level, aimed {gErr:0.00} deg off)");
                SideShot(gt, tag + "_aim");
                FrontShot(gt, tag + "_aim");
                gHeld.Kick();
                yield return new WaitForSeconds(2f / 30f);
                float gKick = Vector3.Angle(gHeld.Grip.forward, target - gHeld.Grip.position);
                SideShot(gt, tag + "_kick");
                yield return new WaitForSeconds(0.5f);
                gErr = Vector3.Angle(gHeld.Grip.forward, target - gHeld.Grip.position);
                WeaponAnimCheck($"officer {gid}: a shot kicks the muzzle up and the gun comes back on the target", gKick > 5f && gErr < 3f, $"(kick {gKick:0.0} deg, after {gErr:0.00} deg)");
                // the reload: the clip at its progress, the magazine out of the grip and in the left hand
                var gAnimator = gAnim.GetComponent<Animator>();
                var gLeft = gAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                var gMag = gHeld.Grip.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.Contains("_Mag"));
                float magToLeft = -1f, reloadLayer = 0f;
                for (float p = 0.02f; p <= 1.0f; p += 1f / 66f)
                {
                    gHeld.ReloadProgress = p;
                    yield return null;
                    if (Mathf.Abs(p - 0.45f) < 0.5f / 66f) { magToLeft = gMag && gMag.gameObject.activeInHierarchy && gLeft ? (gMag.position - gLeft.position).magnitude : -1f; reloadLayer = gAnim.GunReloadWeight; SideShot(gt, tag + "_reload45"); }
                    if (Mathf.Abs(p - 0.7f) < 0.5f / 66f) SideShot(gt, tag + "_reload70");
                }
                gHeld.ReloadProgress = 0f;
                WeaponAnimCheck($"officer {gid}: the reload clip plays - the new magazine in the left hand", magToLeft > 0f && magToLeft < 0.16f && reloadLayer > 0.95f, $"(new magazine {magToLeft:0.000} m from the left wrist, reload layer {reloadLayer:0.00})");
                yield return new WaitForSeconds(0.4f);
                gHeld.Remove();
                yield return null;
                // a rifle: the long gun's clips on an officer too
                var gRifle = HeldGun.Attach(gAnim, RangedWeapon.Kind.Rifle, false, true, true);
                gRifle.Raise = 1f;
                for (float tm = 0f; tm < 1.0f; tm += Time.deltaTime) { gRifle.AimDirection = (target - gRifle.Grip.position).normalized; yield return null; }
                gErr = Vector3.Angle(gRifle.Grip.forward, target - gRifle.Grip.position);
                WeaponAnimCheck($"officer {gid}: a rifle is held in both hands with its Blender animation, on the target", gRifle.Animated && gRifle.TwoHanded && gErr < 3f && gRifle.LeftHandGap < 0.02f, $"(animated {gRifle.Animated}, {gErr:0.00} deg off, left hand off by {gRifle.LeftHandGap * 100f:0.0} cm)");
                SideShot(gt, tag + "_rifle");
                gRifle.Raise = 0f;
                yield return new WaitForSeconds(0.8f);
                SideShot(gt, tag + "_rifle_lowready");
                Destroy(g);
                yield return null;
            }

            GunController.TestCursorLocked = null;
            Time.captureDeltaTime = 0f;
            Log($"[WeaponAnim] DONE fails={weaponAnimFails}");
            Quit(weaponAnimFails == 0 ? 0 : 1);
        }
    }
}
