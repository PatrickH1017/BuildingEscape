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
        /// <summary>Eye-level pictures (1280 x 720, %TEMP%\be_shot_core_*.png) of the restroom doors, the break room and a stairwell on floors 1 and 4.</summary>
        IEnumerator CoreLooksScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            var cam = new GameObject("CoreLooksCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.22f, 0.26f); cam.fieldOfView = 62f; cam.nearClipPlane = 0.1f; cam.enabled = false;
            var lamp = new GameObject("CoreLooksLamp").AddComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 12f; lamp.intensity = 1.6f;
            foreach (int floor in new[] { 1, 3, 4 })
            {
                TowerManager.Instance.GoToLevel(floor);
                yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == floor);
                yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == floor, 90, $"floor {floor}");
                yield return new WaitForSeconds(1.5f);
                var layout = gen.CurrentLayout;
                if (layout == null) continue;
                var interior = gen.Current.Find("Interior");
                if (interior) { var culler = interior.GetComponent<PropCuller>(); if (culler) culler.enabled = false; foreach (var rr in interior.GetComponentsInChildren<Renderer>(true)) if (rr) rr.enabled = true; }
                int n = 0;
                void Look(string name, Vector3 at, Vector3 from)
                {
                    cam.transform.position = from; cam.transform.LookAt(at);
                    lamp.transform.position = from + Vector3.up * 0.8f;
                    ShotCam(cam, $"core_f{floor}_{name}");
                }
                foreach (var d in layout.deadDoors)
                {
                    var room = layout.rooms.FirstOrDefault(r => r.doors.Any(dd => dd.Overlaps(d.Grow(0.05f))));
                    if (room == null) continue;
                    bool alongX = d.W >= d.D;
                    var away = alongX ? new Vector3(0f, 0f, d.Cz > room.bounds.Cz ? 1f : -1f) : new Vector3(d.Cx > room.bounds.Cx ? 1f : -1f, 0f, 0f);
                    var c = new Vector3(d.Cx, 1.3f, d.Cz);
                    Look("restroom" + n++, c, c + away * 3.4f + Vector3.up * 0.3f);
                }
                var brk = layout.rooms.FirstOrDefault(r => r.theme == "break" && r.kind == 0 && r.doors.Count > 0);
                if (brk != null)
                {
                    var d = brk.doors[0];
                    bool alongX = d.W >= d.D;
                    var away = alongX ? new Vector3(0f, 0f, d.Cz > brk.bounds.Cz ? 1f : -1f) : new Vector3(d.Cx > brk.bounds.Cx ? 1f : -1f, 0f, 0f);
                    var c = new Vector3(d.Cx, 1.3f, d.Cz);
                    Look("break_door", c, c + away * 3.2f + Vector3.up * 0.3f);
                    Look("break_inside", new Vector3(brk.bounds.Cx, 0.8f, brk.bounds.Cz), c - away * 0.5f + Vector3.up * 0.4f);
                }
                for (int i = 0; i < gen.StairCount; i++)
                {
                    var centre = gen.StairCentre(i); var outDir = gen.StairOut(i);
                    Look("stair" + i, centre + outDir * 2.4f + Vector3.up * 1.3f, centre + outDir * 6.2f + Vector3.up * 1.6f);
                }
                int axeNo = 0, axeAll = 0;
                foreach (var axe in layout.pieces.Where(pc => pc.key == "emergency_axe"))
                {
                    axeAll++;
                    if (axeNo >= 8) continue;
                    var front = new Vector3(Mathf.Sin(axe.yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(axe.yaw * Mathf.Deg2Rad));
                    var at = new Vector3(axe.x, axe.y + 0.45f, axe.z);
                    var inRoom = layout.rooms.FirstOrDefault(rm => rm.bounds.Contains(axe.x, axe.z));
                    Look("axe" + axeNo, at, at + front * 2.8f + Vector3.up * 0.95f);
                    Log($"[Rooms] axe {axeNo} of floor {floor}: in {(inRoom != null ? inRoom.theme : "?")} at ({axe.x:0.0}, {axe.z:0.0}), {axe.y:0.00} m up");
                    axeNo++;
                }
                var pewCase = GameObject.Find("PewCase");
                if (pewCase)
                {
                    var pc = pewCase.transform.position + Vector3.up * 0.07f;
                    var pf = pewCase.transform.forward;
                    Look("pewcase_close", pc, pc + pf * 0.75f + Vector3.up * 0.5f);
                    Look("pewcase_desk", pc, pc + pf * 2.2f + Vector3.up * 1.3f);
                    Look("pewcase_top", pc, pc + pf * 0.15f + Vector3.up * 0.9f);
                    var pistol = Object.FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None).FirstOrDefault();
                    Log($"[Rooms] pew case of floor {floor} at {pc}; pistol {(pistol ? (pistol.transform.position - pc).ToString("0.00") : "none")} from the middle of the case");
                }
                Log($"[Rooms] core looks of floor {floor}: {layout.deadDoors.Count} restroom doors, break room {(brk != null ? "yes" : "NO")}, {gen.StairCount} stairwells, {axeAll} emergency axes");
            }
            Log("[Rooms] DONE core looks");
            Quit(0);
        }

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
                    foreach (var culler in interior.GetComponentsInChildren<PropCuller>(true)) culler.enabled = false;          // (the furniture's, and the Floor 7 wall skin's)
                    foreach (var rr in interior.GetComponentsInChildren<Renderer>(true)) if (rr) rr.enabled = true;
                }
                foreach (Transform ch in gen.Current.GetComponentsInChildren<Transform>(true))
                    if (ch && (ch.name == "Ceiling" || ch.name == "CeilingTiles" || ch.name.StartsWith("CeilingLights") || ch.name.StartsWith("CeilingPanels"))) ch.gameObject.SetActive(false);
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
