# Round 4, batch 15: NetDevHarness.CoreLooks = eye-level pictures of what A9 / A10 / A11 built: the restroom doors with their signs, the break room's door and inside, a stairwell's door with its safety kit. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void RoomShots()' '        public static void CoreLooks()
        {
            SessionState.SetString("netdev.mode", "corelooks");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void RoomShots()'
Rep 'mode != "roomshots"' 'mode != "roomshots" && mode != "corelooks"'
Rep '                case "roomshots": yield return RoomShotsScenario(); break;' '                case "roomshots": yield return RoomShotsScenario(); break;
                case "corelooks": yield return CoreLooksScenario(); break;'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRoomShots.cs')
Rep '    public partial class NetDevRunner
    {' '    public partial class NetDevRunner
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
            foreach (int floor in new[] { 1, 4 })
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
                Log($"[Rooms] core looks of floor {floor}: {layout.deadDoors.Count} restroom doors, break room {(brk != null ? "yes" : "NO")}, {gen.StairCount} stairwells");
            }
            Log("[Rooms] DONE core looks");
            Quit(0);
        }
'
Save-Src
Write-Output 'round 4 batch 15 patched'
