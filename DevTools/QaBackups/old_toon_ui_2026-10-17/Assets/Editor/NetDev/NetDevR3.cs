using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Round 3 of 2026-10-17 evening (DEVELOPER_NOTES 2.23): the scenarios that look at what was built. <c>InvUi</c> takes pictures of the restyled inventory
    /// (the Tab window) in a few states and at a few window sizes; IMGUI is not drawn in batch mode, so run it in a real Editor on the hidden desktop
    /// (<c>ui_bq.ps1 -Method InvUi</c>). Pictures: %TEMP%\be_ui_inv_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int r3Fails;

        void R3Check(string name, bool ok, string detail = "")
        {
            if (!ok) r3Fails++;
            Log($"[R3] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator InvUiScenario()
        {
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            ResetGameViewSize();
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;

            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var gen = FindFirstObjectByType<FloorGenerator>();
            Log($"[R3] screen {Screen.width}x{Screen.height}");

            // ---- an empty pack and nothing in reach (the empty lists)
            foreach (var l in FindObjectsByType<HeavyLoot>(FindObjectsSortMode.None)) if (l) Destroy(l.gameObject);
            yield return new WaitForSeconds(0.3f);
            SetStaticProp(typeof(Backpack), "UiOpen", true);
            yield return new WaitForSeconds(0.9f);
            R3Check("the inventory is open", Backpack.UiOpen);
            yield return Snap("inv_empty");
            SetStaticProp(typeof(Backpack), "UiOpen", false);

            // ---- a crowbar, the pew, three pieces of loot in the pack and two on the ground
            var menu = new GameObject("TestModeForInv").AddComponent<TestMode>();
            typeof(TestMode).GetMethod("GiveWeapon", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, new object[] { pack, MeleeWeapon.Kind.Crowbar });
            typeof(TestMode).GetMethod("GivePew", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, new object[] { pack });
            pack.capacity = 40f;
            for (int i = 0; i < 3; i++) gen.SpawnLoot(i, player.transform.position + Vector3.forward * 2f + Vector3.up * 0.8f, 1);
            yield return new WaitForSeconds(0.6f);
            foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (p && p.Loot && p.isActiveAndEnabled && !pack.Contains(p) && pack.CanFit(p, out _)) pack.TryStore(p);
            yield return new WaitForSeconds(0.25f);
            // something that is neither a weapon nor loot ("OTHER"): one in the pack, one on the ground
            Pickup Mug(Vector3 at)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Coffee Mug"; go.transform.localScale = new Vector3(0.18f, 0.12f, 0.18f); go.transform.position = at;
                go.AddComponent<Rigidbody>().mass = 0.4f;
                var pk = go.AddComponent<Pickup>(); pk.label = "Coffee Mug"; pk.weight = 0.4f;
                return pk;
            }
            var packMug = Mug(player.transform.position + Vector3.up * 1.2f);
            yield return new WaitForSeconds(0.3f);
            pack.TryStore(packMug);
            Mug(player.transform.position + player.transform.forward * 1.3f + player.transform.right * -0.6f + Vector3.up * 0.6f);
            gen.SpawnLoot(1, player.transform.position + player.transform.forward * 1.4f + Vector3.up * 0.6f, 1);
            gen.SpawnLoot(4, player.transform.position + player.transform.forward * 1.6f + player.transform.right * 0.7f + Vector3.up * 0.6f, 1);
            yield return new WaitForSeconds(0.8f);
            SetStaticProp(typeof(Backpack), "UiOpen", true);
            yield return new WaitForSeconds(1.2f);
            R3Check("the pack holds the two weapons and some loot", pack.Items.Count >= 3, $"items={pack.Items.Count} loot={pack.LootCount}");
            yield return Snap("inv_full");

            // ---- nearly full: the weight bar turns orange / red
            float w = pack.Weight;
            pack.capacity = Mathf.Max(1f, w * 1.04f);
            yield return new WaitForSeconds(0.4f);
            yield return Snap("inv_heavy");
            pack.capacity = 40f;

            // ---- other window sizes
            foreach (var (sw, sh) in new[] { (1280, 720), (960, 540), (3840, 2160) })
            {
                if (!SetGameViewSize(sw, sh)) { Log("[R3] SKIP other window sizes: the Game view cannot be resized from here"); break; }
                yield return new WaitForSeconds(0.8f);
                Log($"[R3] screen now {Screen.width}x{Screen.height}");
                yield return Snap($"inv_{sw}");
            }
            ResetGameViewSize();
            yield return new WaitForSeconds(0.5f);

            SetStaticProp(typeof(Backpack), "UiOpen", false);
            Log($"[R3] DONE fails={r3Fails}");
            Quit(r3Fails == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ R12: the doors slide into the wall

        static Bounds LeafBox(SlidingDoor d, Transform leaf)
        {
            if (d.Swings && leaf.TryGetComponent(out Renderer swingRenderer)) return swingRenderer.bounds;       // (a leaf that swings is turned: its renderer knows its box)
            var s = leaf.localScale;
            var half = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) * 0.5f;
            if (!d.AlongX) half = new Vector3(half.z, half.y, half.x);          // (a door in a wall along z is turned a quarter)
            return new Bounds(leaf.position, half * 2f);
        }

        IEnumerator DoorsSlideScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            tm.GoToLevel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return WaitFor(() => !tm.Loading && hm.Floor == 1 && hm.Phase == HeistPhase.Heist, 90, "floor 1");
            yield return new WaitForSeconds(0.8f);
            var gen = FindFirstObjectByType<FloorGenerator>();
            var dm = FindFirstObjectByType<DoorManager>();
            R3Check("the floor has doors", dm != null && dm.Count > 20, $"({(dm ? dm.Count : 0)})");
            if (dm == null) { Quit(1); yield break; }
            R3Check("most of the doors slide into the wall", gen.DoorSlides >= dm.Count * 7 / 10, $"({gen.DoorSlides} slide, {gen.DoorSwings} swing open, {dm.Count} doors)");

            int Panes(SlidingDoor d) => d.LowLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Pane") + d.HighLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Pane");
            var doors = dm.Doors;
            var plain = doors.FirstOrDefault(d => d.Slides && !d.Wide && Panes(d) == 0);
            var wide = doors.FirstOrDefault(d => d.Slides && d.Wide);
            var glass = doors.FirstOrDefault(d => d.Slides && !d.Wide && Panes(d) == 4);
            var swinging = doors.FirstOrDefault(d => d.Swings && !d.Wide && Panes(d) == 0);
            Log($"[R3] doors: plain={(plain != null)} wide={(wide != null)} glass={(glass != null)} swinging={(swinging != null)}");

            var cam = new GameObject("DoorShotCam").AddComponent<Camera>();
            cam.fieldOfView = 72f; cam.nearClipPlane = 0.05f; cam.enabled = false;
            IEnumerator Show(SlidingDoor d, string name, float back = 3.4f, float side = 0f, bool flip = false)
            {
                if (d == null) yield break;
                var c = d.Centre; var fwd = d.AlongX ? Vector3.forward : Vector3.right; var along = d.AlongX ? Vector3.right : Vector3.back;
                // the camera goes on the side of the doorway with more clear room in front of it
                float ClearAhead(Vector3 dir) => Physics.Raycast(c + Vector3.up * 1.5f + along * side, dir, out var hit, back + 1f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : back + 1f;
                if (ClearAhead(-fwd) > ClearAhead(fwd)) fwd = -fwd;
                if (flip) fwd = -fwd;
                float dist = Mathf.Clamp(ClearAhead(fwd) - 0.5f, 1.6f, back);
                cam.transform.position = c + fwd * dist + along * side + Vector3.up * 1.55f; cam.transform.LookAt(c + along * side * 0.4f + Vector3.up * 1.25f);
                d.SetOpen(false, Time.time);
                yield return new WaitForSeconds(1.0f);
                ShotCam(cam, name + "_closed");
                d.SetOpen(true, Time.time);
                yield return new WaitForSeconds(d.Wide ? 0.16f : 0.12f);
                ShotCam(cam, name + "_half");
                yield return new WaitForSeconds(0.9f);
                ShotCam(cam, name + "_open");
                d.SetOpen(false, Time.time);
                yield return new WaitForSeconds(1.0f);
            }
            yield return Show(plain, "door_plain", 3.4f, 1.2f);
            yield return Show(wide, "door_wide", 5.2f, 0f);
            yield return Show(glass, "door_glass", 3.4f, 1.2f);
            yield return Show(swinging, "door_swing", 3.4f, 1.2f);
            yield return Show(swinging, "door_swing_other", 3.4f, -1.2f, true);
            R3Check("the floor has doors that swing open (the ones with too little wall beside them to slide into)", gen.DoorSwings > 5 && swinging != null, $"({gen.DoorSwings})");

            // ---- every door open at once: no two doors' leaves meet
            foreach (var d in doors) d.SetOpen(true, Time.time);
            yield return new WaitForSeconds(1.2f);
            int pairs = 0, overlaps = 0, outside = 0;
            for (int i = 0; i < doors.Count; i++)
            {
                var a = doors[i];
                if (!a.Slides && !a.Swings) continue;
                for (int j = i + 1; j < doors.Count; j++)
                {
                    var b = doors[j];
                    if ((!b.Slides && !b.Swings) || (a.Centre - b.Centre).sqrMagnitude > 64f) continue;
                    pairs++;
                    foreach (var la in new[] { a.LowLeaf, a.HighLeaf })
                        foreach (var lb in new[] { b.LowLeaf, b.HighLeaf })
                        {
                            var ba = LeafBox(a, la); var bb = LeafBox(b, lb);
                            ba.Expand(-0.03f); bb.Expand(-0.03f);
                            if (ba.Intersects(bb)) overlaps++;
                        }
                }
            }
            R3Check($"no two doors' leaves overlap when every door is open ({pairs} pairs of neighbouring doors)", overlaps == 0, $"(overlaps {overlaps})");
            // a leaf that slides into the wall is out of the doorway: it is not drawn, or the part left over is next to the jamb
            foreach (var d in doors) if (d.Slides && !d.Wide && Panes(d) == 0 && d.LowLeaf.GetComponent<Renderer>().enabled == false && d.HighLeaf.GetComponent<Renderer>().enabled == false) outside++;
            R3Check("plain doors that slide in all the way are not drawn at all when open (they are inside the wall)", outside >= 20, $"({outside} such doors)");

            // an open swing door: both leaves stand square to the wall (their box is thin along the wall and about as deep as the leaf is wide)
            int swingChecked = 0, swingBad = 0;
            foreach (var d in doors.Where(x => x.Swings).Take(20))
                foreach (var leaf in new[] { d.LowLeaf, d.HighLeaf })
                {
                    var b = leaf.GetComponent<Renderer>().bounds;
                    float thin = d.AlongX ? b.size.x : b.size.z, deep = d.AlongX ? b.size.z : b.size.x;
                    swingChecked++;
                    if (Mathf.Abs(thin - SlidingDoor.LeafThickness) > 0.05f || deep < d.HalfGap * 0.8f) swingBad++;
                }
            R3Check($"an open swing door has its leaves square to the wall (about 90 degrees: {swingChecked} leaves looked at)", swingChecked > 10 && swingBad == 0, $"(bad {swingBad})");

            // a picture of two neighbouring doors open
            SlidingDoor pairA = null, pairB = null;
            for (int i = 0; i < doors.Count && pairA == null; i++)
                for (int j = i + 1; j < doors.Count; j++)
                    if (doors[i].Slides && doors[j].Slides && doors[i].AlongX == doors[j].AlongX && (doors[i].Centre - doors[j].Centre).sqrMagnitude < 16f && (doors[i].Centre - doors[j].Centre).sqrMagnitude > 4f) { pairA = doors[i]; pairB = doors[j]; break; }
            if (pairA != null)
            {
                var mid = (pairA.Centre + pairB.Centre) * 0.5f; var fwd = pairA.AlongX ? Vector3.forward : Vector3.right;
                cam.transform.position = mid + fwd * 4.2f + Vector3.up * 1.6f; cam.transform.LookAt(mid + Vector3.up * 1.2f);
                ShotCam(cam, "door_pair_open");
            }
            foreach (var d in doors) d.SetOpen(false, Time.time);
            yield return new WaitForSeconds(0.5f);

            Log($"[R3] DONE fails={r3Fails}");
            Quit(r3Fails == 0 ? 0 : 1);
        }
        // ------------------------------------------------------------------ R10: streets and cars in the night city

        IEnumerator CityLookScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var gen = FindFirstObjectByType<FloorGenerator>();
            var cam = new GameObject("CityShotCam").AddComponent<Camera>();
            cam.fieldOfView = 80f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f; cam.enabled = false;
            foreach (int floor in new[] { 1, 6, 12 })
            {
                tm.GoToLevel(floor);
                yield return new WaitUntil(() => tm.Loading || hm.Floor == floor);
                yield return WaitFor(() => !tm.Loading && hm.Floor == floor && hm.Phase == HeistPhase.Heist, 90, $"floor {floor}");
                yield return new WaitForSeconds(0.8f);
                R3Check($"floor {floor}: the city has streets and cars", gen.CityRoads > 20 && gen.CityCars >= 6, $"({gen.CityRoads} roads, {gen.CityCars} cars, {gen.CityTowers} towers)");
                float width = gen.CellColumns * 18f, depth = gen.CellRows * 18f;
                float bayX = width / Mathf.Round(width / 4.5f), bayZ = depth / Mathf.Round(depth / 4.5f);
                float wx = (Mathf.Floor(width * 0.5f / bayX) + 0.5f) * bayX, wz = (Mathf.Floor(depth * 0.5f / bayZ) + 0.5f) * bayZ;
                cam.transform.position = new Vector3(wx, 1.6f, 0.9f); cam.transform.rotation = Quaternion.Euler(24f, 180f, 0f);        // the south wall, looking out and down
                ShotCam(cam, $"city_floor{floor}_south");
                cam.transform.position = new Vector3(width - 0.9f, 1.6f, wz); cam.transform.rotation = Quaternion.Euler(24f, 90f, 0f);  // the east wall
                ShotCam(cam, $"city_floor{floor}_east");

                var traffic = FindFirstObjectByType<CityTraffic>();
                R3Check($"floor {floor}: the cars are driving (a CityTraffic with {(traffic ? traffic.Count : 0)} cars)", traffic != null && traffic.Count >= 6);
                if (traffic != null && traffic.transform.childCount > 0)
                {
                    var car = traffic.transform.GetChild(0); var at = car.position;
                    yield return new WaitForSeconds(1.2f);
                    R3Check($"floor {floor}: a car has moved on in a second", Vector3.Distance(at, car.position) > 4f, $"(moved {Vector3.Distance(at, car.position):0.0} m)");
                    // the street lies lower on a higher floor: the car's height is the street's
                    R3Check($"floor {floor}: the street is {(4f + 3.2f * floor):0} m below the floor", Mathf.Abs(car.position.y - (-(4f + 3.2f * floor) + 0.55f)) < 0.2f, $"(car y {car.position.y:0.0})");
                }
                var any = FindObjectsByType<Collider>(FindObjectsSortMode.None);
                int cityColliders = 0;
                foreach (var c in any) if (c && c.GetComponentInParent<CityTraffic>() != null) cityColliders++;
                R3Check($"floor {floor}: the cars have no colliders", cityColliders == 0);
            }
            Log($"[R3] DONE fails={r3Fails}");
            Quit(r3Fails == 0 ? 0 : 1);
        }    }
}
