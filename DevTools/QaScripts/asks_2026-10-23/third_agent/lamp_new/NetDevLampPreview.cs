using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The lamps the owner added (2026-10-24, "PBR Lamps Pack" by New Solution Studio): each prefab in six views (from +x, -x, +z, -z, above, below) in one picture, its size, renderers and materials in the log, so that the one that is the red beacon of his
    /// reference picture can be told. Needs a real window: <c>ui.ps1 -Project C:\bd -Methods LampPreview</c>; pictures %TEMP%\be_shot_preview_lamp_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator LampViews(Transform root, Camera cam, string path, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) { Log("[Preview] no lamp prefab at " + path); yield break; }
            var go = Instantiate(prefab, root);
            go.transform.position = root.position; go.transform.rotation = Quaternion.identity;
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            Log($"[Preview] {name}: size {b.size.x:0.000} x {b.size.y:0.000} x {b.size.z:0.000} m, centre {(b.center - root.position)}, {rs.Length} renderers");
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                Log($"[Preview]   renderer {r.name}: {r.sharedMaterials.Length} materials ({string.Join(", ", r.sharedMaterials.Select(m => m ? m.name + " [" + m.shader.name + "]" : "null"))}), mesh {(mf && mf.sharedMesh ? mf.sharedMesh.name + " " + mf.sharedMesh.subMeshCount + " submeshes" : "none")}, bounds {r.bounds.size.x:0.000} x {r.bounds.size.y:0.000} x {r.bounds.size.z:0.000}");
            }
            foreach (var l in go.GetComponentsInChildren<Light>(true)) Log($"[Preview]   light on {l.name}: {l.type}");
            const int T = 384;
            var sheet = new Texture2D(T * 3, T * 2, TextureFormat.RGB24, false);
            var rt = new RenderTexture(T, T, 24);
            cam.orthographic = true; cam.orthographicSize = b.extents.magnitude * 1.1f;
            var dirs = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.up, Vector3.down };
            var ups = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.forward, Vector3.forward };
            for (int i = 0; i < 6; i++)
            {
                cam.transform.position = b.center + dirs[i] * 20f;
                cam.transform.rotation = Quaternion.LookRotation(-dirs[i], ups[i]);
                yield return null;
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                var tile = new Texture2D(T, T, TextureFormat.RGB24, false);
                tile.ReadPixels(new Rect(0, 0, T, T), 0, 0); tile.Apply();
                RenderTexture.active = null; cam.targetTexture = null;
                sheet.SetPixels((i % 3) * T, (1 - i / 3) * T, T, T, tile.GetPixels());
                Destroy(tile);
            }
            sheet.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_shot_preview_lamp_" + name + ".png"), sheet.EncodeToPNG());
            cam.orthographic = false;
            Destroy(go);
            yield return null;
        }

        IEnumerator LampPreviewScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            var root = new GameObject("LampPreview").transform;
            root.position = new Vector3(0f, 0f, 800f);
            var sun = new GameObject("PreviewSun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            var cam = new GameObject("PreviewCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.45f, 0.5f, 0.6f); cam.nearClipPlane = 0.05f; cam.farClipPlane = 200f; cam.enabled = false;
            foreach (var path in PreviewPaths("Assets/New Solution Studio/PBR Lamps Pack/Prefabs"))
                yield return LampViews(root, cam, path, System.IO.Path.GetFileNameWithoutExtension(path));
            Log("[Preview] DONE lamps");
            Quit(0);
        }
    }
}
