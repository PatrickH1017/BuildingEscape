using System.Collections;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pictures of the weapon items the way the game makes them (the owner, 2026-10-18: "I added more Synty assets for the weapons, please utilize and implement them and make sure the silencer is on the exit barrel of the
    /// pew"): the three melee weapons and the silenced pew lying side by side, the pew from above, end-on along the barrel and from the side, the spare clip beside it, and a weapon safe standing open with its pistol,
    /// its cash and its clip. Needs graphics: <c>ui.ps1 -Methods WeaponSheet</c>. Pictures: %TEMP%\be_shot_weaponsheet_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator WeaponSheetScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var root = new GameObject("WeaponSheet");
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
            var camGo = new GameObject("WeaponSheetCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f); cam.enabled = false;

            IEnumerator Shot(string name, Vector3 lookAt, Vector3 from, float fov)
            {
                cam.fieldOfView = fov;
                cam.transform.position = from;
                cam.transform.LookAt(lookAt);
                yield return null;
                ShotCam(cam, "weaponsheet_" + name);
            }
            void Pin(GameObject go) { if (go && go.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true; }

            // ---- the items as the game makes them, lying on the ground in a row: crowbar, bat, hatchet, pew with a spare clip beside it
            var at = root.transform.position;
            var crowbar = MeleeWeapon.Create(MeleeWeapon.Kind.Crowbar, at + new Vector3(0f, 0.04f, -1.05f), root.transform, 0f);
            var bat = MeleeWeapon.Create(MeleeWeapon.Kind.Bat, at + new Vector3(0f, 0.04f, -0.55f), root.transform, 0f);
            var axe = MeleeWeapon.Create(MeleeWeapon.Kind.Hatchet, at + new Vector3(0f, 0.04f, 0.0f), root.transform, 0f);
            var pew = RangedWeapon.Create(RangedWeapon.Kind.Pew, at + new Vector3(0f, 0.025f, 0.55f), root.transform, 0f);
            var clip = new GameObject("ClipItem");
            clip.transform.SetParent(root.transform, false);
            var clipModel = RangedWeapon.CreateClipModel(clip.transform, out var clipSize);
            clip.transform.position = at + new Vector3(0.0f, 0.02f, 0.95f);
            Log($"[WeaponSheet] clip model {(clipModel ? "built" : "MISSING")}, size {clipSize.x:0.000} x {clipSize.y:0.000} x {clipSize.z:0.000}");
            foreach (var g in new[] { crowbar, bat, axe, pew }) Pin(g);
            yield return null;
            yield return Shot("items_top", at + new Vector3(0f, 0f, -0.1f), at + new Vector3(0f, 3.6f, -0.9f), 32f);
            yield return Shot("pew_top", pew.transform.position, pew.transform.position + new Vector3(0.02f, 0.75f, -0.08f), 24f);
            var sil = pew.transform.Find("Silencer");
            if (sil)
            {
                // end-on along the barrel, from the front: the silencer must sit centred on the slide behind it
                yield return Shot("pew_end", sil.position, sil.position + sil.forward * 0.9f + Vector3.up * 0.01f, 24f);
                // from the side, low: the thin way
                yield return Shot("pew_side", pew.transform.position, pew.transform.position + new Vector3(0.02f, 0.10f, -0.7f), 24f);
            }
            else Log("[WeaponSheet] the pew item has no Silencer child");
            Destroy(crowbar); Destroy(bat); Destroy(axe); Destroy(pew); Destroy(clip);

            // ---- a weapon safe, open: the pistol in the middle, the spare clip on the right, the cash at the back left
            var mats = new FloorSafe.Mats { steel = Flat(new Color(0.5f, 0.52f, 0.55f)), dark = Flat(new Color(0.12f, 0.12f, 0.13f)), brass = Flat(new Color(0.8f, 0.65f, 0.25f)), lining = Flat(new Color(0.2f, 0.2f, 0.22f)), led = Flat(new Color(0.2f, 1f, 0.3f)) };
            var safe = FloorSafe.Build(root.transform, 0f, at.z + 2.6f, 180f, 7, 0, 1234, mats);
            safe.FillWithGuns(root.transform, new[] { SafeRules.PewItem });
            safe.FillWithExtras(root.transform, 450, true, Flat(new Color(0.35f, 0.62f, 0.35f)), Flat(new Color(0.9f, 0.9f, 0.85f)), Flat(new Color(0.15f, 0.15f, 0.17f)), Flat(new Color(0.8f, 0.65f, 0.25f)));
            // the gold watch the rules give some safes: the generator's own builder (the same watch as the desks'), put in at the safe's own spot
            var gen = FindFirstObjectByType<FloorGenerator>();
            var build = typeof(FloorGenerator).GetMethod("BuildValuable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (gen && build != null)
            {
                var watchAt = safe.WatchSpot(out float watchYaw);
                safe.AddWatch((GameObject)build.Invoke(gen, new object[] { ValuableKind.Watch, SafeRules.SideWatchValue(7), watchAt, watchYaw, root.transform }));
            }
            else Log("[WeaponSheet] no way to make a watch");
            safe.ApplyOpen(true);
            yield return new WaitForSeconds(1.6f);
            var st = safe.transform;
            yield return Shot("safe_open", st.TransformPoint(new Vector3(0f, 0.30f, 0f)), st.TransformPoint(new Vector3(0.75f, 1.25f, 1.15f)), 38f);
            yield return Shot("safe_inside", st.TransformPoint(new Vector3(0f, 0.12f, -0.02f)), st.TransformPoint(new Vector3(0.12f, 0.95f, 0.55f)), 40f);
            Log("[WeaponSheet] DONE");
            Quit(0);
        }

        /// <summary>
        /// The three melee weapons (the Synty crowbar, bat and fire axe) in the right hand of a Tiny Toon person and of a Party character: close up on the hand at rest, and the whole body in the middle of the swing.
        /// Needs graphics: <c>ui.ps1 -Methods MeleeLook</c>. Pictures: %TEMP%\be_shot_meleelook_{kind}_{character}_{rest|swing}.png.
        /// </summary>
        IEnumerator MeleeLookScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var catalog = CharacterCatalog.Instance;
            var root = new GameObject("MeleeLookLineup");
            root.transform.position = new Vector3(0f, 0f, 90f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -1.05f, 0f);
            ground.transform.localScale = new Vector3(40f, 0.1f, 12f);
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.38f, 0.42f) };

            // two bodies: the first Tiny Toon person and the Party character
            var names = new System.Collections.Generic.List<string>();
            var anims = new System.Collections.Generic.List<CharacterAnimator>();
            var first = catalog.characters[0];
            for (int i = 0; i < 2; i++)
            {
                var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                host.name = i == 0 ? "Host_" + first.id : "Host_party";
                host.transform.SetParent(root.transform, false);
                host.transform.localPosition = new Vector3((i - 0.5f) * 3.2f, 0f, 0f);
                host.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);        // facing the camera, which is at -z
                Object.Destroy(host.GetComponent<Collider>());
                var look = i == 0 ? new CharacterLook(first.id, Color.white, 1, null, 1f, 1f, first.id) : PartyCharacterFactory.PlayerLooks[0];
                anims.Add(PartyCharacterFactory.Attach(host, look));
                names.Add(i == 0 ? "toon" : "party");
            }
            foreach (var an in anims) if (an) { var ar = an.GetComponent<Animator>(); if (ar) ar.cullingMode = AnimatorCullingMode.AlwaysAnimate; foreach (var smr in an.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true; }
            yield return new WaitForSeconds(1.0f);
            var camGo = new GameObject("MeleeLookCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f); cam.enabled = false;

            var clip = MeleeWeapon.Catalog ? MeleeWeapon.Catalog.slash : null;
            foreach (var kind in new[] { MeleeWeapon.Kind.Crowbar, MeleeWeapon.Kind.Bat, MeleeWeapon.Kind.Hatchet })
            {
                var helds = new System.Collections.Generic.List<GameObject>();
                for (int i = 0; i < anims.Count; i++)
                {
                    var hand = anims[i] ? anims[i].RightHand : null;
                    helds.Add(hand ? MeleeWeapon.BuildHeld(kind, hand) : null);
                    if (!hand) Log($"[MeleeLook] no right hand on {names[i]}");
                }
                yield return new WaitForSeconds(0.7f);
                // the person with the weapon in the right hand (the Party characters have no hand bone for one): at rest, up close with the whole weapon in the picture, then the swing frame by frame from the front right
                if (anims[0] && anims[0].RightHand)
                {
                    var body = root.transform.position + new Vector3(-0.5f * 3.2f, 0f, 0f);
                    var hand = anims[0].RightHand.position;
                    cam.fieldOfView = 36f;
                    cam.transform.position = hand + new Vector3(-0.9f, 0.45f, -2.3f);
                    cam.transform.LookAt(hand + new Vector3(0f, 0.4f, 0f));
                    yield return null;
                    ShotCam(cam, $"meleelook_{kind}_{names[0]}_rest");
                    anims[0].PlaySlash(clip, 1f);
                    float began = Time.time;
                    int frame = 0;
                    foreach (float at in new[] { 0.14f, 0.26f, 0.40f, 0.56f })
                    {
                        while (Time.time - began < at) yield return null;
                        cam.fieldOfView = 40f;
                        cam.transform.position = body + new Vector3(-2.0f, 0.7f, -2.6f);
                        cam.transform.LookAt(body + new Vector3(-0.2f, 0.25f, 0f));
                        ShotCam(cam, $"meleelook_{kind}_{names[0]}_swing{++frame}");
                    }
                }
                yield return new WaitForSeconds(1.4f);
                foreach (var h in helds) if (h) Destroy(h);
                yield return null;
            }
            Log("[MeleeLook] DONE");
            Quit(0);
        }
    }
}
