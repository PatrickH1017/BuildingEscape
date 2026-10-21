using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Candidate sheet of the Synty people (the owner, 2026-10-20: "the attached images show the 5 characters that should be playable and present in the intro cinematic"): the Construction pack's builders and inspectors and the office people, three
    /// to a picture, standing in a row facing the camera, so that the owner's five can be picked by eye. Needs graphics: <c>qa_regress.ps1 -Methods CharSheet:gfx</c>. Pictures: %TEMP%\be_shot_charsheet_{group}{n}.png (the ids are logged in picture order).
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator CharSheetScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var cat = CharacterCatalog.Instance;
            if (!cat) { Log("[CharSheet] no character catalog"); Quit(1); yield break; }
            var root = new GameObject("CharSheet");
            root.transform.position = new Vector3(0f, 0f, 120f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.15f;
            sun.transform.rotation = Quaternion.Euler(40f, 160f, 0f);                      // (from the camera's side: the faces are lit)
            sun.transform.SetParent(root.transform, false);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -1.05f, 0f);
            ground.transform.localScale = new Vector3(12f, 0.1f, 6f);
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.45f, 0.47f, 0.5f) };
            var camGo = new GameObject("CharSheetCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.60f, 0.68f, 0.80f); cam.enabled = false;

            var construction = cat.characters.Where(c => c != null && c.prefab && (c.id.StartsWith("syn_builder_") || c.id.StartsWith("syn_inspector_"))).Select(c => c.id).OrderBy(i => i).ToList();
            var office = cat.characters.Where(c => c != null && c.prefab && c.id.StartsWith("syn_") && !construction.Contains(c.id) && !c.id.Contains("_security_") && !c.id.Contains("_boss_male")).Select(c => c.id).OrderBy(i => i).ToList();
            Log($"[CharSheet] construction people: {string.Join(", ", construction)}");
            Log($"[CharSheet] office people: {string.Join(", ", office)}");
            foreach (var (tag, ids) in new[] { ("con", construction), ("off", office) })
            {
                for (int n = 0; n * 3 < ids.Count; n++)
                {
                    var chunk = ids.Skip(n * 3).Take(3).ToList();
                    var hosts = new List<GameObject>();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        host.name = "Host_" + chunk[i];
                        host.transform.SetParent(root.transform, false);
                        host.transform.localPosition = new Vector3((i - (chunk.Count - 1) / 2f) * 1.25f, 0f, 0f);
                        host.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);        // facing the camera
                        Object.Destroy(host.GetComponent<Collider>());
                        var anim = PartyCharacterFactory.Attach(host, new CharacterLook(chunk[i], Color.white, 1, null, 1f, 1f, chunk[i]));
                        hosts.Add(host);
                        if (!anim) Log($"[CharSheet] {chunk[i]} could not be dressed");
                    }
                    yield return new WaitForSeconds(0.9f);
                    cam.fieldOfView = 30f;
                    cam.transform.localPosition = new Vector3(0f, 0.35f, -4.3f);
                    cam.transform.LookAt(root.transform.position + new Vector3(0f, 0.05f, 0f));
                    yield return null;
                    ShotCam(cam, $"charsheet_{tag}{n + 1}");
                    Log($"[CharSheet] picture charsheet_{tag}{n + 1}: left to right {string.Join(" | ", chunk)}");
                    foreach (var h in hosts) Destroy(h);
                    yield return null;
                }
            }
            Log("[CharSheet] DONE");
            Quit(0);
        }
    }
}
