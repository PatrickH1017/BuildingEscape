using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The doors of floor 7 (the owner, 2026-10-23: "update the doors on the 7th floor to match the new assets used around the floor such as the walls"): eye-level pictures of every kind of door the floor has (a room's
    /// double door, the wide hall door, an office with a glass front, a restroom, the guard outpost), shut and open, from both sides and from an angle, to compare before and after (<c>BE_DOOR_PACK=0</c> switches the pack's doors
    /// off in the same build, so "before" and "after" are one program), and the checks of the pack's doors in a real floor: which doors wear them, that every panel stands inside its leaf's box, that they show and hide with the
    /// leaf, that the guard outpost's door is left alone. Also contact sheets of the POLYGON Office pack's own doors and door walls. Run with graphics: <c>NetDevHarness.DoorLooks</c> (ui.ps1 -Methods DoorLooks).
    /// Pictures: %TEMP%\be_shot_&lt;tag&gt;_*.png. Environment: BE_DOOR_TAG (the file name part, default "doors"), BE_DOOR_SHEETS=1 adds the sheets of the pack, BE_DOOR_FLOOR (default 7), BE_DOOR_PACK=0 (the old doors),
    /// BE_DOOR_PICS=0 (checks only). The log ends with <c>[DoorLooks] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        const string DoorPackB = "Assets/Synty/PolygonOffice/Prefabs/Buildings/";

        /// <summary>A row of the pack's door pieces, left to right in the order of the log, seen from one side (the camera carries its own light, so the back shows as well as the front).</summary>
        IEnumerator DoorSheet(Transform root, Camera cam, string tag, string name, (string label, string[] prefabs)[] items, bool fromBack)
        {
            var holders = new List<GameObject>();
            var sizes = new List<Vector3>();
            float x = 0f, maxH = 0f;
            foreach (var it in items)
            {
                var holder = new GameObject(it.label);
                holder.transform.SetParent(root, false);
                foreach (var p in it.prefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPackB + p + ".prefab");
                    if (!prefab) { Log($"[DoorLooks] {name}: cannot load {p}"); continue; }
                    var go = Instantiate(prefab, holder.transform);
                    go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
                }
                var rs = holder.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) { Destroy(holder); continue; }
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                float w = Mathf.Max(b.size.x, 0.5f);
                x += w * 0.5f;
                holder.transform.position += new Vector3(x - b.center.x, -b.min.y, root.position.z - b.center.z);
                holders.Add(holder); sizes.Add(b.size);
                maxH = Mathf.Max(maxH, b.size.y);
                x += w * 0.5f + 0.55f;
            }
            if (holders.Count == 0) yield break;
            cam.fieldOfView = 30f;
            float dist = Mathf.Max(x * 0.5f / 0.46f, maxH * 0.5f / 0.26f) * 1.08f;
            float sign = fromBack ? 1f : -1f;
            cam.transform.position = new Vector3(x * 0.5f, maxH * 0.5f, root.position.z + sign * dist);
            cam.transform.LookAt(new Vector3(x * 0.5f, maxH * 0.5f, root.position.z));
            yield return null;
            ShotCam(cam, $"{tag}_sheet_{name}_{(fromBack ? "back" : "front")}");
            foreach (var h in holders) Destroy(h);
            yield return null;
        }

        string DoorKindOf(FloorGenerator gen, SlidingDoor d)
        {
            if (d.Permanent) return "restroom";
            if (d.Locked) return "outpost";
            if (d.Wide) return "hall";
            foreach (var r in gen.GlassRoomBounds) if (r.Grow(0.3f).Contains(d.Centre.x, d.Centre.z)) return "glass";
            return gen.TryGetPackLook(d, out var look) && look == DoorLook.Wood ? "wood" : "room";
        }

        /// <summary>The world box of a leaf (the unit cube SlidingDoor scales): the box of its eight corners.</summary>
        static Bounds LeafWorldBox(Transform leaf)
        {
            var b = new Bounds(leaf.TransformPoint(new Vector3(-0.5f, -0.5f, -0.5f)), Vector3.zero);
            for (int i = 1; i < 8; i++) b.Encapsulate(leaf.TransformPoint(new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f)));
            return b;
        }

        static IEnumerable<Transform> PackPanels(Transform leaf) { foreach (Transform c in leaf) if (c.name == "PackPanel") yield return c; }

        IEnumerator DoorLooksScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            string Env(string n, string d) { var v = System.Environment.GetEnvironmentVariable(n); return string.IsNullOrEmpty(v) ? d : v; }
            string tag = Env("BE_DOOR_TAG", "doors");
            int floor = int.Parse(Env("BE_DOOR_FLOOR", "7"));
            bool pics = Env("BE_DOOR_PICS", "1") != "0";
            int fails = 0;
            void Check(string name, bool ok, string detail = "") { if (!ok) fails++; Log($"[DoorLooks] {(ok ? "PASS" : "FAIL")} {name} {detail}"); }
            Log($"[DoorLooks] graphics {SystemInfo.graphicsDeviceType}, tag {tag}, floor {floor}, pack doors {(Env("BE_DOOR_PACK", "1") != "0" ? "ON" : "OFF")}");

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.50f, 0.50f, 0.52f);
            RenderSettings.fog = false;
            var cam = new GameObject("DoorLooksCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.30f, 0.34f, 0.40f); cam.fieldOfView = 62f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f; cam.enabled = false;
            var headlight = new GameObject("DoorLooksHeadlight").AddComponent<Light>();
            headlight.type = LightType.Directional; headlight.intensity = 0.9f; headlight.shadows = LightShadows.None;
            headlight.transform.SetParent(cam.transform, false);

            // ---------------- the pack's own doors (only when asked)
            if (Env("BE_DOOR_SHEETS", "0") == "1")
            {
                var root = new GameObject("DoorSheets").transform;
                root.position = new Vector3(0f, 0f, 900f);
                var singles = new (string, string[])[] { ("Door_01", new[] { "SM_Bld_Door_01" }), ("Door_02", new[] { "SM_Bld_Door_02" }), ("Door_03", new[] { "SM_Bld_Door_03" }) };
                var singles2 = new (string, string[])[] { ("Door_04", new[] { "SM_Bld_Door_04" }), ("Door_05", new[] { "SM_Bld_Door_05" }), ("Door_Large_01 L+R", new[] { "SM_Bld_Door_Large_01_L", "SM_Bld_Door_Large_01_R" }) };
                foreach (bool back in new[] { false, true })
                {
                    yield return DoorSheet(root, cam, tag, "doorsA", singles, back);
                    yield return DoorSheet(root, cam, tag, "doorsB", singles2, back);
                }
                Destroy(root.gameObject);
                yield return null;
            }
            cam.fieldOfView = 62f;
            headlight.enabled = false;

            // ---------------- the doors the floor has
            var gen = FindFirstObjectByType<FloorGenerator>();
            if (Env("BE_DOOR_PACK", "1") == "0") gen.packDoors = false;
            yield return LoadLevel(hm, floor);
            yield return new WaitForSeconds(1.5f);
            foreach (var g in new List<GuardAI>(GuardAI.All)) if (g) g.gameObject.SetActive(false);
            var floorRoot = gen.Current;
            var dm = floorRoot ? floorRoot.GetComponentInChildren<DoorManager>() : null;
            if (dm == null || dm.Count == 0) { Log("[DoorLooks] FAIL the floor has no doors"); Quit(1); yield break; }
            Log($"[DoorLooks] floor {floor}: {dm.Count} doors; wall skin {gen.WallSkinActive} ({gen.SkinPieces} pieces, {gen.SkinTilesBuilt} tiles); slide {gen.DoorSlides}, swing {gen.DoorSwings}, glass doors {gen.GlassDoors}; pack doors {gen.PackDoorCount} (white {gen.PackDoorsWhite}, wood {gen.PackDoorsWood}, glass {gen.PackDoorsGlass})");

            var interior = floorRoot.Find("Interior");
            foreach (var culler in floorRoot.GetComponentsInChildren<PropCuller>(true)) culler.enabled = false;
            if (interior) foreach (var rr in interior.GetComponentsInChildren<Renderer>(true)) if (rr) rr.enabled = true;
            foreach (var tr in floorRoot.GetComponentsInChildren<Transform>(true))
                if (tr && tr.name == "CeilingTiles") foreach (var rr in tr.GetComponentsInChildren<Renderer>(true)) if (rr) rr.enabled = true;       // (the skin's ceiling is not under "Interior")
            var lamp = new GameObject("DoorLooksLamp").AddComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 14f; lamp.intensity = 1.1f; lamp.shadows = LightShadows.None;

            var perKind = new Dictionary<string, List<int>>();
            for (int i = 0; i < dm.Count; i++)
            {
                var d = dm.Doors[i];
                string kind = DoorKindOf(gen, d);
                if (!perKind.TryGetValue(kind, out var list)) perKind[kind] = list = new List<int>();
                list.Add(i);
            }
            Log("[DoorLooks] kinds: " + string.Join(", ", perKind.Select(kv => kv.Key + " x" + kv.Value.Count)));
            int Count(string k) => perKind.TryGetValue(k, out var l) ? l.Count : 0;

            // ---------------- the checks of the pack's doors (when they are on)
            if (gen.PackDoorCount > 0 || Env("BE_DOOR_EXPECT", "0") == "1")
            {
                int solid = Count("room") + Count("wood") + Count("restroom"), glass = Count("glass") + Count("hall");
                Check("pack doors: the floor on show is floor 7 with its walls skinned", floor == 7 && gen.WallSkinActive);
                Check("pack doors: every door but the guard outpost's is the pack's", gen.PackDoorCount == dm.Count - Count("outpost"), $"({gen.PackDoorCount} of {dm.Count}, {Count("outpost")} outpost)");
                Check("pack doors: the hall doors and the doors of the glass offices are the glass ones", gen.PackDoorsGlass == glass, $"({gen.PackDoorsGlass} glass, expected {glass})");
                Check("pack doors: the rooms and the restrooms have the solid ones (white or wood)", gen.PackDoorsWhite + gen.PackDoorsWood == solid && gen.PackDoorsWhite > 0 && gen.PackDoorsWood > 0, $"({gen.PackDoorsWhite} white + {gen.PackDoorsWood} wood, expected {solid})");
                int badPanels = 0, badBox = 0, badShadow = 0, badCollider = 0, badBlank = 0, outpostTouched = 0, checkedPanels = 0;
                string firstBad = "";
                for (int i = 0; i < dm.Count; i++)
                {
                    var d = dm.Doors[i];
                    string kind = DoorKindOf(gen, d);
                    int expect = kind == "outpost" ? 0 : kind == "hall" ? 2 : 1;
                    foreach (var leaf in new[] { d.LowLeaf, d.HighLeaf })
                    {
                        var panels = PackPanels(leaf).ToList();
                        var mf = leaf.GetComponent<MeshFilter>();
                        bool blank = mf && mf.sharedMesh && mf.sharedMesh.name == "PackLeafBlank";
                        if (kind == "outpost") { if (panels.Count > 0 || blank) outpostTouched++; continue; }
                        if (panels.Count != expect) { badPanels++; if (firstBad == "") firstBad = $"door {i} ({kind}) has {panels.Count} panels in a leaf, not {expect}"; }
                        if (!blank) badBlank++;
                        if (leaf.GetComponentsInChildren<Collider>(true).Length > 0) badCollider++;
                        var box = LeafWorldBox(leaf);
                        box.Expand(0.04f);
                        foreach (var p in panels)
                            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                            {
                                checkedPanels++;
                                var rb = r.bounds;
                                if (!(box.Contains(rb.min) && box.Contains(rb.max))) { badBox++; if (firstBad == "") firstBad = $"door {i} ({kind}): {r.name} sticks out of its leaf ({rb.size.x:0.00} x {rb.size.y:0.00} x {rb.size.z:0.00} at {rb.center}, leaf box {box.center} / {box.size})"; }
                                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) badShadow++;
                            }
                    }
                }
                Check("pack doors: every leaf of every pack door holds the right number of panels (1 in a 2 m doorway, 2 in a hall doorway)", badPanels == 0, firstBad);
                Check("pack doors: every panel stands inside its leaf's box (the box the old slab filled), " + checkedPanels + " renderers", badBox == 0 && checkedPanels > 0, firstBad);
                Check("pack doors: the old slab of every pack door is not drawn (its mesh is the blank one) and no panel has a collider", badBlank == 0 && badCollider == 0, $"({badBlank} slabs drawn, {badCollider} colliders)");
                Check("pack doors: the panels cast no shadow", badShadow == 0);
                Check("pack doors: the guard outpost's door is left alone", outpostTouched == 0);

                // they show and hide with the leaf: a door that slides into the wall shows nothing when it is open, a door that swings stays in view
                int FirstDoor(System.Func<SlidingDoor, bool> test) { for (int i = 0; i < dm.Count; i++) if (test(dm.Doors[i])) return i; return -1; }
                var samples = new List<(string label, int idx)>
                {
                    ("a sliding room door", FirstDoor(x => x.Slides && DoorKindOf(gen, x) == "room")),
                    ("a swinging room door", FirstDoor(x => x.Swings && DoorKindOf(gen, x) == "room")),
                    ("a sliding wooden door", FirstDoor(x => x.Slides && DoorKindOf(gen, x) == "wood")),
                    ("a swinging glass door", FirstDoor(x => x.Swings && (DoorKindOf(gen, x) == "glass" || DoorKindOf(gen, x) == "hall"))),
                    ("a hall door", FirstDoor(x => DoorKindOf(gen, x) == "hall")),
                };
                bool InSync(Transform leaf) { var own = leaf.GetComponent<Renderer>(); return PackPanels(leaf).SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).All(r => r.enabled == own.enabled); }
                foreach (var (label, idx) in samples)
                {
                    if (idx < 0) { Log($"[DoorLooks] SKIP {label}: the floor has none"); continue; }
                    var d = dm.Doors[idx];
                    var rends = PackPanels(d.LowLeaf).Concat(PackPanels(d.HighLeaf)).SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).ToList();
                    Check($"pack doors: {label} is shown while it is shut ({rends.Count} renderers)", rends.Count > 0 && rends.All(r => r.enabled));
                    if (!d.IsShut) { dm.Use(idx, false); yield return new WaitForSeconds(1.0f); }
                    dm.Use(idx, true);
                    yield return new WaitForSeconds(1.2f);
                    // (a leaf shows and hides as SlidingDoor says: the panels must do exactly what the leaf's own renderer does. A 2 m door that slides is wholly inside the wall when it is open; a hall leaf may stay in view beside the doorway)
                    Check($"pack doors: {label}, open, shows exactly what its leaves show ({rends.Count(r => r.enabled)} of {rends.Count} drawn)", InSync(d.LowLeaf) && InSync(d.HighLeaf));
                    if (d.Slides && !d.Wide) Check($"pack doors: {label}, open, is out of sight in the wall", rends.All(r => !r.enabled));
                    if (d.Swings) Check($"pack doors: {label}, open, is still in view", rends.All(r => r.enabled));
                    dm.Use(idx, false);
                    yield return new WaitForSeconds(1.2f);
                    Check($"pack doors: {label} is back in view once it is shut again", rends.All(r => r.enabled) && InSync(d.LowLeaf) && InSync(d.HighLeaf));
                }
            }

            // the table of the floor's doors, so that a picture can be asked for by number (BE_DOOR_PICKS=12,40,...) and the same doors are shot with the pack's doors on and off
            for (int i = 0; i < dm.Count; i++)
            {
                var d = dm.Doors[i];
                Log($"[DoorLooks] door {i}: {DoorKindOf(gen, d)}{(gen.TryGetPackLook(d, out var lk) ? " (" + lk + ")" : "")} at ({d.Centre.x:0.0}, {d.Centre.z:0.0}), {(d.Slides ? "slides" : d.Swings ? "swings" : "narrows")}");
            }

            void Look(string name, Vector3 from, Vector3 at)
            {
                cam.transform.position = from; cam.transform.LookAt(at);
                lamp.transform.position = from + Vector3.up * 0.7f;
                ShotCam(cam, $"{tag}_{name}");
            }

            // which doors: BE_DOOR_PICKS (door numbers, from the table above) or, by default, two of each kind (one restroom, the outpost)
            var chosen = new List<int>();
            var pickEnv = Env("BE_DOOR_PICKS", "");
            if (pickEnv != "") { foreach (var s in pickEnv.Split(',')) if (int.TryParse(s.Trim(), out var pi) && pi >= 0 && pi < dm.Count) chosen.Add(pi); }
            else
                foreach (var kv in perKind.OrderBy(k => k.Key))
                {
                    int take = kv.Key == "outpost" || kv.Key == "restroom" ? 1 : 2;
                    for (int n = 0; n < take && n < kv.Value.Count; n++) chosen.Add(kv.Value[n * Mathf.Max(1, kv.Value.Count / take) % kv.Value.Count]);
                }

            int shots = 0;
            var lights = floorRoot.Find("AfterHours") ? floorRoot.Find("AfterHours").GetComponent<FloorLights>() : null;
            if (pics)
                foreach (int idx in chosen)
                {
                    var d = dm.Doors[idx];
                    var across = d.AlongX ? Vector3.forward : Vector3.right;
                    var along = d.AlongX ? Vector3.right : Vector3.forward;
                    float back = d.Wide ? 3.8f : 3.3f;
                    var mid = d.Centre + Vector3.up * 1.15f;
                    string id = "d" + idx;
                    lamp.enabled = true;
                    if (!d.IsShut) { dm.Use(idx, false); yield return new WaitForSeconds(1.0f); }
                    Look($"{id}_shut_a", d.Centre + across * back + Vector3.up * 1.55f, mid); shots++;
                    Look($"{id}_shut_b", d.Centre - across * back + Vector3.up * 1.55f, mid); shots++;
                    Look($"{id}_shut_c", d.Centre + across * (back * 0.75f) + along * (back * 0.55f) + Vector3.up * 1.5f, mid); shots++;
                    // ...and in the light the player has: the floor's own ceiling lights round the camera, no lamp of mine
                    if (lights != null)
                    {
                        lamp.enabled = false;
                        var from = d.Centre + across * back + Vector3.up * 1.6f;
                        cam.transform.position = from; cam.transform.LookAt(mid);
                        lights.Scan(from);
                        yield return new WaitForSeconds(1.1f);
                        ShotCam(cam, $"{tag}_{id}_lit_a"); shots++;
                        from = d.Centre - across * back + Vector3.up * 1.6f;
                        cam.transform.position = from; cam.transform.LookAt(mid);
                        lights.Scan(from);
                        yield return new WaitForSeconds(1.1f);
                        ShotCam(cam, $"{tag}_{id}_lit_b"); shots++;
                        lamp.enabled = true;
                    }
                    dm.Use(idx, true);
                    yield return new WaitForSeconds(1.1f);
                    Look($"{id}_open_a", d.Centre + across * back + Vector3.up * 1.55f, mid); shots++;
                    Look($"{id}_open_c", d.Centre + across * (back * 0.75f) + along * (back * 0.55f) + Vector3.up * 1.5f, mid); shots++;
                    dm.Use(idx, false);
                    yield return new WaitForSeconds(1.0f);
                    Log($"[DoorLooks] pictures of door {idx} ({DoorKindOf(gen, d)}, {(d.Slides ? "slides" : d.Swings ? "swings" : "narrows")}) done");
                }
            Log($"[DoorLooks] DONE fails={fails} pictures={shots}");
            Quit(fails == 0 ? 0 : 1);
        }
    }
}
