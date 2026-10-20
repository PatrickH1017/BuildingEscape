using System.Collections;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pictures of the candidate long guns of the Synty Battle Royale pack in side view (the owner, 2026-10-18: "Yes, add the SMG, rifle, shotgun and sniper guns from the Synty pack"), stacked like a wall rack at the
    /// pack's own scale, barrel to the right, so one model can be chosen for each kind. Needs graphics: <c>ui.ps1 -Methods GunSheet</c>. Pictures: %TEMP%\be_shot_gunsheet_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator GunSheetScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var root = new GameObject("GunSheet");
            root.transform.position = new Vector3(0f, 0f, 90f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.25f;
            sun.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            sun.transform.SetParent(root.transform, false);
            var camGo = new GameObject("GunSheetCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.70f, 0.82f); cam.enabled = false;
            cam.orthographic = true;

            const string Pack = "Assets/Synty/PolygonBattleRoyale/Prefabs/Weapons/";
            var groups = new (string tag, string[] names)[]
            {
                ("smg", new[] { "SM_Wep_SubMGun_01", "SM_Wep_SubMGun_Lite_01", "SM_Wep_MachineGun_01" }),
                ("rifles", new[] { "SM_Wep_Rifle_Assault_01", "SM_Wep_Rifle_Assault_02", "SM_Wep_Rifle_Assault_03", "SM_Wep_Rifle_Assault_04", "SM_Wep_Rifle_Assault_05" }),
                ("long", new[] { "SM_Wep_Shotgun_01", "SM_Wep_Sniper_01", "SM_Wep_Sniper_Lite_01", "SM_Wep_Sniper_Mil_01", "SM_Wep_Sniper_Heavy_01" }),
            };
            foreach (var (tag, names) in groups)
            {
                var spawned = new System.Collections.Generic.List<GameObject>();
                for (int i = 0; i < names.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + names[i] + ".prefab");
                    if (!prefab) { Log($"[GunSheet] {names[i]} not found"); continue; }
                    var g = Object.Instantiate(prefab, root.transform);
                    foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = false;
                    g.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);                // the barrel (+Z) along +X
                    g.transform.localPosition = new Vector3(0f, -i * 0.46f, 0f);
                    spawned.Add(g);
                }
                yield return null;
                float rows = names.Length;
                cam.orthographicSize = Mathf.Max(0.5f, rows * 0.23f + 0.1f);
                var mid = root.transform.position + new Vector3(0.1f, -(rows - 1f) * 0.23f, 0f);
                cam.transform.position = mid + new Vector3(0f, 0f, -4f);
                cam.transform.rotation = Quaternion.identity;                                // looking along +Z: +X is to the right of the picture, so the barrels point right
                yield return null;
                ShotCam(cam, "gunsheet_" + tag);
                foreach (var g in spawned) Destroy(g);
                yield return null;
            }
            Log("[GunSheet] DONE");
            Quit(0);
        }

        /// <summary>
        /// The five guns as the game makes them (<see cref="RangedWeapon.Create"/>), lying on the ground in a row: from above, each from the side, and the ones that take a silencer end-on along the barrel (the silencer must sit on the
        /// barrel's own line, in the middle). Needs graphics: <c>ui.ps1 -Methods GunItems</c>. Pictures: %TEMP%\be_shot_gunitems_*.png.
        /// </summary>
        IEnumerator GunItemsScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var root = new GameObject("GunItems");
            root.transform.position = new Vector3(0f, 0f, 90f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(8f, 0.1f, 8f);
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.42f, 0.44f, 0.48f) };
            var camGo = new GameObject("GunItemsCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f); cam.enabled = false;

            var at = root.transform.position;
            var kinds = (RangedWeapon.Kind[])System.Enum.GetValues(typeof(RangedWeapon.Kind));
            var guns = new System.Collections.Generic.List<GameObject>();
            float z = -1.5f;
            foreach (var k in kinds)
            {
                var g = RangedWeapon.Has(k) ? RangedWeapon.Create(k, at + new Vector3(0f, 0.03f, z), root.transform, 0f) : null;
                Log($"[GunItems] {k}: {(g ? "made" : "MISSING")}");
                if (g)
                {
                    if (g.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
                    guns.Add(g);
                }
                z += 0.75f;
            }
            yield return null;
            cam.fieldOfView = 36f;
            cam.transform.position = at + new Vector3(0f, 5.2f, 0.0f);
            cam.transform.LookAt(at + new Vector3(0f, 0f, 0.0f), Vector3.forward);               // (from above, +x to the right: the barrels point right, the first kind is at the bottom)
            yield return null;
            ShotCam(cam, "gunitems_top");
            foreach (var g0 in guns) Destroy(g0);
            yield return null;
            for (int i = 0; i < kinds.Length; i++)
            {
                var k = kinds[i];
                if (!RangedWeapon.Has(k)) continue;
                var g = RangedWeapon.Create(k, at + new Vector3(0f, 0.03f, 0f), root.transform, 0f);       // (alone, so that nothing hides it)
                if (g.TryGetComponent(out Rigidbody rb1)) rb1.isKinematic = true;
                yield return null;
                var spec = RangedWeapon.Specs[(int)k];
                var lay = RangedWeapon.LayoutOf(k);
                float len = lay.fullBounds.size.x * lay.scale;
                // from the side, low: the whole gun in the picture
                cam.fieldOfView = 30f;
                cam.transform.position = g.transform.position + new Vector3(0f, 0.16f, -Mathf.Max(1.0f, len * 1.9f));
                cam.transform.LookAt(g.transform.position + new Vector3(0f, 0.02f, 0f));
                yield return null;
                ShotCam(cam, $"gunitems_{k}_side");
                var sil = g.transform.Find("Silencer");
                Log($"[GunItems] {k}: class {spec.weaponClass}, takes a silencer {spec.CanSilence}, silencer on the item {(sil ? "yes" : "no")}, scale {lay.scale:0.000}, length {len:0.000} m, foregrip {lay.hasForegrip}");
                if (sil)
                {
                    cam.fieldOfView = 24f;
                    cam.transform.position = sil.position + sil.forward * 1.0f + Vector3.up * 0.01f;
                    cam.transform.LookAt(sil.position);
                    yield return null;
                    ShotCam(cam, $"gunitems_{k}_end");
                }
                Destroy(g);
                yield return null;
            }
            Log("[GunItems] DONE");
            Quit(0);
        }

        /// <summary>
        /// Every gun in the hands of a toon person, a Synty office person and a Party character (needs graphics: <c>ui.ps1 -Methods GunLook</c>): from behind (the way the game's camera sees them), from the side and from the front,
        /// aiming level, then up and down. For tuning the two-handed hold. Pictures: %TEMP%\be_shot_gunlook_{kind}_{who}_{view}.png.
        /// </summary>
        IEnumerator GunLookScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var cat = CharacterCatalog.Instance;
            var root = new GameObject("GunLookLineup");
            root.transform.position = new Vector3(0f, 0f, 90f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -1.05f, 0f);
            ground.transform.localScale = new Vector3(60f, 0.1f, 20f);
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.38f, 0.42f) };

            var whoNames = new[] { "toon", "synty", "party" };
            var lookIds = new[] { "male_a", "syn_boss_male_01", null };
            var xs = new[] { -4f, 0f, 5f };
            var anims = new System.Collections.Generic.List<CharacterAnimator>();
            var bodies = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < whoNames.Length; i++)
            {
                var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                host.name = "Host_" + whoNames[i];
                host.transform.SetParent(root.transform, false);
                host.transform.localPosition = new Vector3(xs[i], 0f, 0f);
                host.transform.localRotation = Quaternion.identity;                          // facing +z, away from the camera at -z: the way the game's camera sees a player
                Object.Destroy(host.GetComponent<Collider>());
                CharacterLook look;
                if (lookIds[i] != null) look = new CharacterLook(lookIds[i], Color.white, 1, null, 1f, 1f, lookIds[i]);
                else look = PartyCharacterFactory.PlayerLooks[0];
                anims.Add(PartyCharacterFactory.Attach(host, look));
                bodies.Add(host);
            }
            foreach (var an in anims) if (an) { var ar = an.GetComponent<Animator>(); if (ar) ar.cullingMode = AnimatorCullingMode.AlwaysAnimate; foreach (var smr in an.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true; }
            yield return new WaitForSeconds(1.0f);
            var camGo = new GameObject("GunLookCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f); cam.enabled = false;

            var kinds = (RangedWeapon.Kind[])System.Enum.GetValues(typeof(RangedWeapon.Kind));
            var aims = new[] { ("level", Vector3.forward), ("up", new Vector3(0f, 0.34f, 0.94f)), ("down", new Vector3(0f, -0.26f, 0.97f)) };
            foreach (var k in kinds)
            {
                if (!RangedWeapon.Has(k)) continue;
                var spec = RangedWeapon.Specs[(int)k];
                var helds = new System.Collections.Generic.List<HeldGun>();
                for (int i = 0; i < anims.Count; i++)
                {
                    var held = anims[i] ? HeldGun.Attach(anims[i], k, spec.CanSilence) : null;
                    helds.Add(held);
                    Log($"[GunLook] {k} on {whoNames[i]}: {(held != null ? held.ArmInfo() + ", " + held.FitInfo() + ", two-handed " + held.TwoHanded : "NO HAND")}");
                }
                foreach (var (label, dir) in aims)
                {
                    foreach (var h in helds) if (h) { h.Raise = 1f; h.AimDirection = dir; }
                    yield return new WaitForSeconds(0.9f);
                    for (int i = 0; i < anims.Count; i++)
                    {
                        if (!anims[i] || helds[i] == null) continue;
                        var rs = anims[i].GetComponentsInChildren<SkinnedMeshRenderer>();
                        var b = rs.Length > 0 ? rs[0].bounds : new Bounds(bodies[i].transform.position, Vector3.one);
                        for (int r = 1; r < rs.Length; r++) b.Encapsulate(rs[r].bounds);
                        float s = Mathf.Max(0.6f, b.size.y);
                        var c = b.center;
                        cam.fieldOfView = 32f;
                        // from behind and to the right (the game's camera), then the side (the right), then the front
                        var views = label == "level"
                            ? new[] { ("behind", new Vector3(0.40f * s, 0.10f * s, -1.9f * s), new Vector3(0f, 0.05f * s, 0.30f * s)),
                                      ("side", new Vector3(2.0f * s, 0.05f * s, 0.15f * s), new Vector3(0f, 0.0f, 0.30f * s)),
                                      ("front", new Vector3(0.9f * s, 0.15f * s, 1.9f * s), new Vector3(0f, 0.0f, 0.15f * s)) }
                            : new[] { ("behind", new Vector3(0.40f * s, 0.10f * s, -1.9f * s), new Vector3(0f, 0.05f * s, 0.30f * s)) };
                        foreach (var (vname, offs, look) in views)
                        {
                            cam.transform.position = c + offs;
                            cam.transform.LookAt(c + look);
                            yield return null;
                            ShotCam(cam, $"gunlook_{k}_{whoNames[i]}_{vname}_{label}");
                        }
                        if (label == "level") Log($"[GunLook] {k} on {whoNames[i]}: left hand {helds[i].LeftHandGap:0.000} m from the foregrip (two-handed {helds[i].TwoHanded})");
                    }
                }
                foreach (var h in helds) if (h) h.Remove();
                yield return null;
            }
            Log("[GunLook] DONE");
            Quit(0);
        }
        /// <summary>
        /// A weapon safe holding each of the five guns in turn (the owner's ladder of unlocks, 2026-10-19), open, with its spare clip, a bit of cash and a gold watch: from the front and from above. A long gun is longer than the hollow is deep:
        /// it lies slanted across it with its barrel out through the open door. Needs graphics: <c>ui.ps1 -Methods GunSafes</c>. Pictures: %TEMP%\be_shot_gunsafes_{kind}_{front|top}.png.
        /// </summary>
        IEnumerator GunSafesScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var root = new GameObject("GunSafes");
            root.transform.position = new Vector3(0f, 0f, 90f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            Material Flat(Color c) => new Material(Shader.Find("Standard")) { color = c };
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(8f, 0.1f, 8f);
            ground.GetComponent<Renderer>().sharedMaterial = Flat(new Color(0.42f, 0.44f, 0.48f));
            var camGo = new GameObject("GunSafesCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f); cam.enabled = false;
            var mats = new FloorSafe.Mats { steel = Flat(new Color(0.5f, 0.52f, 0.55f)), dark = Flat(new Color(0.12f, 0.12f, 0.13f)), brass = Flat(new Color(0.8f, 0.65f, 0.25f)), lining = Flat(new Color(0.2f, 0.2f, 0.22f)), led = Flat(new Color(0.2f, 1f, 0.3f)) };
            var gen = FindFirstObjectByType<FloorGenerator>();
            var build = typeof(FloorGenerator).GetMethod("BuildValuable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var at = root.transform.position;

            foreach (var kind in (RangedWeapon.Kind[])System.Enum.GetValues(typeof(RangedWeapon.Kind)))
            {
                if (!RangedWeapon.Has(kind)) continue;
                var holder = new GameObject("Safe_" + kind).transform;
                holder.SetParent(root.transform, false);
                var safe = FloorSafe.Build(holder, 0f, at.z + 2.6f, 180f, 7, 0, 1234, mats);
                safe.FillWithGuns(holder, new[] { SafeRules.GunItem(kind) });
                safe.FillWithExtras(holder, 450, true, Flat(new Color(0.35f, 0.62f, 0.35f)), Flat(new Color(0.9f, 0.9f, 0.85f)), Flat(new Color(0.15f, 0.15f, 0.17f)), Flat(new Color(0.8f, 0.65f, 0.25f)), kind);
                if (gen && build != null)
                {
                    var watchAt = safe.WatchSpot(out float watchYaw);
                    safe.AddWatch((GameObject)build.Invoke(gen, new object[] { ValuableKind.Watch, SafeRules.SideWatchValue(7), watchAt, watchYaw, holder }));
                }
                safe.ApplyOpen(true);
                yield return new WaitForSeconds(1.4f);
                var st = safe.transform;
                var gunObj = FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None);
                int guns = 0; foreach (var g in gunObj) if (g && g.isActiveAndEnabled && g.transform.IsChildOf(holder)) guns++;
                Log($"[GunSafes] {kind}: {guns} gun(s) lying in the open safe, {FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length} clip(s) about");
                cam.fieldOfView = 38f;
                cam.transform.position = st.TransformPoint(new Vector3(0.85f, 1.25f, 1.45f));
                cam.transform.LookAt(st.TransformPoint(new Vector3(0f, 0.30f, 0.1f)));
                yield return null;
                ShotCam(cam, $"gunsafes_{kind}_front");
                cam.fieldOfView = 34f;
                cam.transform.position = st.TransformPoint(new Vector3(0.10f, 1.55f, 0.95f));
                cam.transform.LookAt(st.TransformPoint(new Vector3(0f, 0.12f, 0.0f)));
                yield return null;
                ShotCam(cam, $"gunsafes_{kind}_top");
                Destroy(holder.gameObject);
                yield return null;
            }
            Log("[GunSafes] DONE");
            Quit(0);
        }    }
}
