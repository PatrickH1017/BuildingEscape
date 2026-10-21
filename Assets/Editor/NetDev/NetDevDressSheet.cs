using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Candidate sheets of the Synty Construction pack's head attachments (hats, helmets, goggles, beards ...) on a bare builder (the owner, 2026-10-20: the five characters of his pictures wear hard hats, caps, goggles and beards that the pack's
    /// character prefabs do not carry): each attachment is parented to the Head bone with no offset (that is how the pack's pivots are made) and photographed, six to a picture (two rows of three heads). The pack's four colour themes
    /// (Textures 01 yellow, 02 orange, 03 grey / white, 04 green; the A / B / C are skin tones) decide the colour of a hat, so the second sheet shows a hat in every theme. Logs where each mesh sits.
    /// Needs graphics: <c>qa_regress.ps1 -Methods DressSheet:gfx</c> or <c>ThemeSheet:gfx</c>. Pictures: %TEMP%\be_shot_{dresssheet|themesheet}_{n}.png (the names are logged in picture order, row by row).
    /// </summary>
    public partial class NetDevRunner
    {
        const string ConstructionAttachments = "Assets/Synty/PolygonConstruction/Prefabs/Characters/Attachments/";
        const string ConstructionTextures = "Assets/Synty/PolygonConstruction/Textures/PolygonConstruction_Texture_0{0}_A.png";

        IEnumerator DressSheetScenario()
        {
            var names = new[]
            {
                "SM_Chr_Attach_Hat_01", "SM_Chr_Attach_Helmet_01", "SM_Chr_Attach_Helmet_02", "SM_Chr_Attach_Helmet_Backwards_01", "SM_Chr_Attach_Helmet_Goggles_01", "SM_Chr_Attach_Helmet_Hat_01",
                "SM_Chr_Attach_Helmet_Earmuffs_01", "SM_Chr_Attach_Helmet_Earmuffs_02", "SM_Chr_Attach_Earmuffs_01", "SM_Chr_Attach_Goggles_01", "SM_Chr_Attach_Goggles_OnHead_01", "SM_Chr_Attach_SafteyGlasses_01",
                "SM_Chr_Attach_SunGlasses_01", "SM_Chr_Attach_FilterMask_01", "SM_Chr_Attach_Beard_01", "SM_Chr_Attach_Beard_02", "SM_Chr_Attach_Beard_03", "SM_Chr_Attach_Beard_Grey_01",
                "SM_Chr_Attach_Beard_Grey_02", "SM_Chr_Attach_Goatee_01", "SM_Chr_Attach_Mustache_01", "SM_Chr_Attach_WelderMask_01", "SM_Chr_Attach_FaceShield_Mesh_01", "SM_Chr_Attach_FaceShield_Plastic_01",
            };
            yield return DressPictures("dresssheet", names.Select(n => (n, 1)).ToList());
        }

        /// <summary>A cap, a hard hat and goggles in each of the four colour themes.</summary>
        IEnumerator ThemeSheetScenario()
        {
            var items = new List<(string, int)>();
            foreach (var n in new[] { "SM_Chr_Attach_Hat_01", "SM_Chr_Attach_Helmet_01", "SM_Chr_Attach_Goggles_OnHead_01" })
                for (int t = 1; t <= 4; t++) items.Add((n, t));
            yield return DressPictures("themesheet", items);
        }

        IEnumerator DressPictures(string tag, List<(string file, int theme)> items)
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var cat = CharacterCatalog.Instance;
            var mannequin = cat ? cat.Find("syn_builder_male_01") : null;
            if (mannequin == null) { Log("[DressSheet] no builder in the catalog"); Quit(1); yield break; }
            var root = new GameObject("DressSheet");
            root.transform.position = new Vector3(0f, 0f, 150f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
            sun.transform.SetParent(root.transform, false);
            var camGo = new GameObject("DressSheetCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.60f, 0.68f, 0.80f); cam.enabled = false;

            for (int n = 0; n * 6 < items.Count; n++)
            {
                var chunk = items.Skip(n * 6).Take(6).ToList();
                var hosts = new List<GameObject>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    host.name = "Host_" + chunk[i].file;
                    host.transform.SetParent(root.transform, false);
                    host.transform.localPosition = new Vector3((i % 3 - 1) * 0.9f, i < 3 ? 0f : -0.9f, 0f);
                    host.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    Object.Destroy(host.GetComponent<Collider>());
                    var anim = PartyCharacterFactory.Attach(host, new CharacterLook(mannequin.id, Color.white, 1, null, 1f, 1f, mannequin.id));
                    var head = anim ? anim.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head) : null;
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ConstructionAttachments + chunk[i].file + ".prefab");
                    if (!head || !prefab) { Log($"[DressSheet] {chunk[i].file}: head {(head ? "ok" : "MISSING")}, prefab {(prefab ? "ok" : "MISSING")}"); hosts.Add(host); continue; }
                    var atlas = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(ConstructionTextures, chunk[i].theme)) ?? mannequin.atlas;
                    var mat = atlas ? ToonCharacterFactory.AtlasMaterialFor(atlas) : null;
                    var item = Object.Instantiate(prefab, head, false);
                    item.transform.localPosition = Vector3.zero; item.transform.localRotation = Quaternion.identity;
                    foreach (var r in item.GetComponentsInChildren<Renderer>(true)) if (mat) r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                    var mf = item.GetComponentInChildren<MeshFilter>();
                    var b = mf && mf.sharedMesh ? mf.sharedMesh.bounds : default;
                    Log($"[DressSheet] {chunk[i].file} (theme {chunk[i].theme}): {(mf ? "static mesh" : "no mesh")}, mesh box centre {b.center:F3} size {b.size:F3}");
                    hosts.Add(host);
                }
                yield return new WaitForSeconds(0.8f);
                cam.fieldOfView = 15f;
                cam.transform.localPosition = new Vector3(0f, 0.27f, -7f);
                cam.transform.LookAt(root.transform.position + new Vector3(0f, 0.27f, 0f));
                yield return null;
                ShotCam(cam, $"{tag}_{n + 1}");
                Log($"[DressSheet] picture {tag}_{n + 1}: top row {string.Join(" | ", chunk.Take(3).Select(c => c.file + "#" + c.theme))}; bottom row {string.Join(" | ", chunk.Skip(3).Select(c => c.file + "#" + c.theme))}");
                foreach (var h in hosts) Destroy(h);
                yield return null;
            }
            Log("[DressSheet] DONE");
            Quit(0);
        }
    }
}
