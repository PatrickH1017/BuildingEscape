using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Round 4 (A7, "remake the floor layouts properly"): a picture from straight above of the rooms of the floors, without the ceiling, to judge the furnishing by eye (and to compare before and after).
    /// Run with graphics: <c>NetDevHarness.RoomShots</c>. Environment: BE_ROOM_THEMES (comma list, default every theme), BE_ROOM_FLOORS (default 1,2,3,4,5,6), BE_ROOM_PER_THEME (per floor, default 2),
    /// BE_ROOM_TAG (file name prefix, default "room"). Pictures: %TEMP%\be_<tag>_<theme>_f<floor>_<n>.png (640 x 640, one room each).
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator RoomShotsScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            string Env(string n, string d) { var v = System.Environment.GetEnvironmentVariable(n); return string.IsNullOrEmpty(v) ? d : v; }
            var themes = Env("BE_ROOM_THEMES", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            var floors = Env("BE_ROOM_FLOORS", "1,2,3,4,5,6").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x.Trim())).ToList();
            int perTheme = int.Parse(Env("BE_ROOM_PER_THEME", "2"));
            string tag = Env("BE_ROOM_TAG", "room");

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);
            RenderSettings.fog = false;
            var sun = new GameObject("RoomShotSun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.0f; sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(78f, 25f, 0f);
            var cam = new GameObject("RoomShotCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
            cam.orthographic = true; cam.nearClipPlane = 0.1f; cam.farClipPlane = 80f; cam.enabled = false;
            var rt = new RenderTexture(640, 640, 24);
            var tex = new Texture2D(640, 640, TextureFormat.RGB24, false);
            int shots = 0;
            foreach (int floor in floors)
            {
                TowerManager.Instance.GoToLevel(floor);
                yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == floor);
                yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == floor, 90, $"floor {floor}");
                yield return new WaitForSeconds(1.5f);
                var layout = gen.CurrentLayout;
                if (layout == null) continue;
                var interior = gen.Current.Find("Interior");
                if (interior)
                {
                    var culler = interior.GetComponent<PropCuller>();
                    if (culler) culler.enabled = false;
                    foreach (var rr in interior.GetComponentsInChildren<Renderer>(true)) if (rr) rr.enabled = true;
                }
                foreach (Transform ch in gen.Current.GetComponentsInChildren<Transform>(true))
                    if (ch && (ch.name == "Ceiling" || ch.name.StartsWith("CeilingLights") || ch.name.StartsWith("CeilingPanels"))) ch.gameObject.SetActive(false);
                yield return null;
                var perCount = new Dictionary<string, int>();
                foreach (var room in layout.rooms)
                {
                    if (room.theme == null) continue;
                    if (themes.Count > 0 && !themes.Contains(room.theme)) continue;
                    if (room.theme == "hub" || room.theme == "hall") { if (themes.Count == 0) continue; }
                    perCount.TryGetValue(room.theme, out int have);
                    if (have >= perTheme) continue;
                    perCount[room.theme] = have + 1;
                    var b = room.bounds;
                    float half = Mathf.Max(b.W, b.D) * 0.5f + 0.7f;
                    cam.transform.position = new Vector3(b.Cx, 40f, b.Cz);
                    cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    cam.orthographicSize = half;
                    cam.targetTexture = rt; cam.Render();
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); tex.Apply();
                    RenderTexture.active = prev; cam.targetTexture = null;
                    string path = Path.Combine(Path.GetTempPath(), $"be_{tag}_{room.theme}_f{floor}_{have}.png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    int pieces = layout.pieces.Count(p => p.room != null && room.bounds.Contains(p.x, p.z));
                    Log($"[Rooms] saved {Path.GetFileName(path)}: {room.theme} {b.W:0.0} x {b.D:0.0} m, {pieces} pieces, role {room.role ?? "-"}");
                    shots++;
                }
                Log($"[Rooms] floor {floor}: {layout.rooms.Count} rooms; themes {string.Join(", ", layout.rooms.GroupBy(r => r.theme).Select(g => g.Key + " x" + g.Count()))}");
            }
            Object.Destroy(rt); Object.Destroy(tex);
            Log($"[Rooms] DONE pictures={shots}");
            Quit(0);
        }
    }
}
