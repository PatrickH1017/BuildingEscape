using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Round 12 (2026-10-19): the owner's white plane in the Intro's window shot, the owner's cars on the streets of the night city, the owner's buildings round the building (night) and as the skyline
    /// behind the boss (Intro). <c>qa_regress.ps1 -Methods CityNew:gfx</c>; pictures %TEMP%\be_shot_citynew_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int cityNewFails;

        void CityNewCheck(string name, bool ok, string detail = "")
        {
            if (!ok) cityNewFails++;
            Log($"[CityNew] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>The bounds of some renderers in the axes of a frame (a car's length is its z, however it is turned).</summary>
        static Bounds BoundsIn(Transform frame, IEnumerable<Renderer> rs)
        {
            bool first = true;
            var b = new Bounds();
            foreach (var r in rs)
            {
                var lb = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = frame.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>A picture of the boss's office the way the Intro films it (its own flat ambient light, no fog).</summary>
        static void OfficeShot(Camera cam, string name)
        {
            var mode = RenderSettings.ambientMode; var colour = RenderSettings.ambientLight; float intensity = RenderSettings.ambientIntensity; bool fog = RenderSettings.fog;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.50f, 0.50f, 0.54f); RenderSettings.ambientIntensity = 1f; RenderSettings.fog = false;
            ShotCam(cam, name);
            RenderSettings.ambientMode = mode; RenderSettings.ambientLight = colour; RenderSettings.ambientIntensity = intensity; RenderSettings.fog = fog;
        }

        static Transform CarModel(Transform car)
        {
            foreach (Transform c in car) if (c.name.StartsWith("Model ")) return c;
            return null;
        }

        IEnumerator CityNewScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;

            // ---- the catalog (made by CityCatalogBuilder)
            var cat = CityCatalog.Instance;
            CityNewCheck("the city catalog holds the plane, the nine cars and the buildings", cat != null && cat.plane != null && cat.cars != null && cat.cars.Length == 9 && cat.buildings != null && cat.buildings.Length >= 30,
                $"(plane {(cat && cat.plane ? cat.plane.name : "none")}, {(cat && cat.cars != null ? cat.cars.Length : 0)} cars, {(cat && cat.buildings != null ? cat.buildings.Length : 0)} buildings)");
            CityNewCheck("the pack's towers are found for the Intro's skyline", CityBuildings.Towers(cat).Length >= 6 && CityBuildings.Pool(cat).Length >= 30, $"({CityBuildings.Towers(cat).Length} towers, {CityBuildings.Pool(cat).Length} for a plot)");

            // ---- the Intro: the white plane in the window shot, the pack's towers as the skyline
            BossOffice.Prewarm();
            var office = BossOffice.Build(BossOffice.DefaultOrigin);
            var plane = office.root.Find("Jet/Plane");
            CityNewCheck("the Intro's jet is the pack's white plane", plane != null);
            var ocam = new GameObject("CityNewOfficeCam").AddComponent<Camera>();
            ocam.fieldOfView = 34f; ocam.nearClipPlane = 0.08f; ocam.farClipPlane = 9000f; ocam.enabled = false; ocam.allowHDR = false;
            ocam.clearFlags = CameraClearFlags.SolidColor; ocam.backgroundColor = BossOffice.Haze; ocam.cullingMask = 1 << BossOffice.Layer;
            if (plane != null)
            {
                office.SetJet(0.5f);
                var prs = plane.GetComponentsInChildren<Renderer>();
                var pb = prs[0].bounds;
                foreach (var r in prs) pb.Encapsulate(r.bounds);
                CityNewCheck("the plane is 10 to 18 m long, its length along the flight line (x)", pb.size.x > 10f && pb.size.x < 18f && pb.size.x > pb.size.z, $"(size {pb.size.x:0.0} x {pb.size.y:0.0} x {pb.size.z:0.0})");
                CityNewCheck("the plane is centred on the jet's path", (pb.center - office.root.TransformPoint(BossOffice.JetPoint(0.5f))).magnitude < 0.4f, $"(off by {(pb.center - office.root.TransformPoint(BossOffice.JetPoint(0.5f))).magnitude:0.00} m)");
                CityNewCheck("the plane's landing gear is up", prs.All(r => !r.name.Contains("LandingGear")), $"({prs.Length} parts show)");
                CityNewCheck("the plane has no colliders", plane.GetComponentsInChildren<Collider>(true).Length == 0);
                CityNewCheck("the plane is on the office's layer", plane.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == BossOffice.Layer));
            }
            int towers = 0, towersOff = 0, towerColliders = 0;
            foreach (Transform c in office.root)
                if (c.name.StartsWith("Building "))
                {
                    towers++;
                    if (c.GetComponentsInChildren<Transform>(true).Any(x => x.gameObject.layer != BossOffice.Layer)) towersOff++;
                    towerColliders += c.GetComponentsInChildren<Collider>(true).Length;
                }
            CityNewCheck("the Intro's skyline has the pack's towers (on the office's layer, no colliders)", towers >= 12 && towersOff == 0 && towerColliders == 0, $"({towers} towers, {towersOff} off the layer, {towerColliders} colliders)");
            // the old glass skyscrapers still stand in the distance (the farthest band's mesh reaches 100 m or more over the street) and between the pack's towers (the middle band's too)
            foreach (string bandName in new[] { "City1", "City2" })
            {
                var bandT = office.root.Find(bandName);
                var bandMesh = bandT ? bandT.GetComponent<MeshFilter>().sharedMesh : null;
                CityNewCheck($"the old skyscrapers still stand in {bandName}", bandMesh != null && bandMesh.bounds.max.y + 96f >= 100f && bandMesh.vertexCount > 3000, $"(tallest {(bandMesh ? bandMesh.bounds.max.y + 96f : 0f):0} m, {(bandMesh ? bandMesh.vertexCount : 0)} vertices)");
            }
            // the boss from the front (the Medium shot: the skyline is his background), then the window shot with the plane at three places
            var o0 = office.root.position;
            ocam.fieldOfView = 27f;
            ocam.transform.position = o0 + new Vector3(0.45f, 1.45f, -3.2f);
            ocam.transform.rotation = Quaternion.LookRotation(o0 + new Vector3(0f, 1.20f, 1.3f) - ocam.transform.position, Vector3.up);
            OfficeShot(ocam, "citynew_medium");
            ocam.fieldOfView = 34f;
            foreach (float j in new[] { 0.25f, 0.5f, 0.75f })
            {
                office.SetJet(j);
                float t = 18.4f + 4.6f * j, p = Mathf.Clamp01((t - 18f) / 5f);              // (the window beat runs from 18 s to 23 s; the jet from 18.4 s for 4.6 s)
                var o = office.root.position;
                ocam.transform.position = o + new Vector3(-1.0f, 1.50f, 2.5f);
                ocam.transform.rotation = Quaternion.LookRotation(o + Vector3.Lerp(new Vector3(20f, 12f, 64f), new Vector3(-20f, 12f, 64f), p) - ocam.transform.position, Vector3.up);
                OfficeShot(ocam, $"citynew_window_{(int)(j * 100f)}");
            }
            office.Destroy();
            Destroy(ocam.gameObject);
            yield return null;

            // ---- a night floor: the cars and the buildings
            const int floor = 6;
            float groundY = -(4f + 3.2f * floor);
            tm.GoToLevel(floor);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == floor);
            yield return WaitFor(() => !tm.Loading && hm.Floor == floor && hm.Phase == HeistPhase.Heist, 90, $"floor {floor}");
            yield return new WaitForSeconds(0.8f);
            var gen = FindFirstObjectByType<FloorGenerator>();
            var traffic = FindFirstObjectByType<CityTraffic>();
            CityNewCheck("the cars are driving", traffic != null && traffic.Count >= 6 && gen.CityCars >= 6, $"({(traffic ? traffic.Count : 0)} cars, {gen.CityCars} made, {gen.CityRoads} roads, {gen.CityTowers} towers)");
            var cam = new GameObject("CityNewCam").AddComponent<Camera>();
            cam.fieldOfView = 80f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f; cam.enabled = false;
            if (traffic != null)
            {
                int withModel = 0, fitted = 0, onRoad = 0, glowing = 0, colliders = 0, total = 0;
                var kinds = new HashSet<string>();
                foreach (Transform car in traffic.transform)
                {
                    total++;
                    var model = CarModel(car);
                    if (model == null) continue;
                    withModel++;
                    var rs = model.GetComponentsInChildren<Renderer>();
                    var cb = BoundsIn(car, rs);
                    if (cb.size.z > 4.2f && cb.size.z < 5.4f && cb.size.x > 1.5f && cb.size.x < 3.2f && Mathf.Abs(cb.center.x) < 0.15f && Mathf.Abs(cb.center.z) < 0.15f) fitted++;
                    else Log($"[CityNew] car {model.name}: size {cb.size.x:0.00} x {cb.size.y:0.00} x {cb.size.z:0.00}, centre {cb.center.x:0.00} / {cb.center.z:0.00}");
                    if (Mathf.Abs(cb.min.y - (-0.40f)) < 0.06f) onRoad++;
                    if (rs.All(r => r.sharedMaterials.All(m => m != null && m.name.StartsWith("CityGlow")))) glowing++;
                    colliders += model.GetComponentsInChildren<Collider>(true).Length;
                    kinds.Add(model.name);
                }
                CityNewCheck("every car is one of the pack's models", total >= 6 && withModel == total, $"({withModel} of {total})");
                CityNewCheck("every car is about 4.7 m long, 2 m wide and centred", fitted == withModel, $"({fitted} of {withModel})");
                CityNewCheck("every car stands on the road (its wheels' bottom at -0.40)", onRoad == withModel, $"({onRoad} of {withModel})");
                CityNewCheck("every car glows by itself (night copies of the pack's materials)", glowing == withModel, $"({glowing} of {withModel})");
                CityNewCheck("the cars have no colliders", colliders == 0, $"({colliders})");
                CityNewCheck("the cars are of at least three models", kinds.Count >= 3, $"({kinds.Count}: {string.Join(", ", kinds.Take(9))})");
            }

            // ---- the buildings round the building
            Transform city = traffic != null ? traffic.transform.parent : null;
            CityNewCheck("the city has a root", city != null);
            if (city != null)
            {
                var blds = city.Cast<Transform>().Where(c => c.name.StartsWith("Building ")).ToList();
                int withColliders = 0, tooBig = 0, floating = 0;
                foreach (var bt in blds)
                {
                    var brs = bt.GetComponentsInChildren<Renderer>();
                    var bb = brs[0].bounds;
                    foreach (var r in brs) bb.Encapsulate(r.bounds);
                    withColliders += bt.GetComponentsInChildren<Collider>(true).Length;
                    if (bb.size.x > 17.4f || bb.size.z > 17.4f) tooBig++;
                    if (Mathf.Abs(bb.min.y - groundY) > 0.06f) floating++;
                }
                CityNewCheck("the city has the pack's buildings round the building", blds.Count >= 30, $"({blds.Count} buildings, {gen.CityTowers} box towers)");
                CityNewCheck("the buildings have no colliders", withColliders == 0, $"({withColliders} colliders)");
                CityNewCheck("every building fits its plot (17.2 m) and stands on the ground", tooBig == 0 && floating == 0, $"({tooBig} too big, {floating} off the ground)");
            }

            // ---- pictures: the street from a window, the east side, a car among the buildings, two cars close up
            float width = gen.CellColumns * 18f, depth = gen.CellRows * 18f;
            float bayX = width / Mathf.Round(width / 4.5f), bayZ = depth / Mathf.Round(depth / 4.5f);
            float wx = (Mathf.Floor(width * 0.5f / bayX) + 0.5f) * bayX, wz = (Mathf.Floor(depth * 0.5f / bayZ) + 0.5f) * bayZ;
            cam.transform.position = new Vector3(wx, 1.6f, 0.9f); cam.transform.rotation = Quaternion.Euler(24f, 180f, 0f);
            ShotCam(cam, $"citynew_floor{floor}_south");
            cam.transform.position = new Vector3(width - 0.9f, 1.6f, wz); cam.transform.rotation = Quaternion.Euler(24f, 90f, 0f);
            ShotCam(cam, $"citynew_floor{floor}_east");
            cam.transform.position = new Vector3(wx, 1.6f, depth - 0.9f); cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            ShotCam(cam, $"citynew_floor{floor}_north");
            if (traffic != null && traffic.transform.childCount > 0)
            {
                var carA = traffic.transform.GetChild(0);
                cam.fieldOfView = 32f;
                // the nearest car on the south side, seen from the south window
                Transform south = null; float best = 1e9f;
                var windowAt = new Vector3(wx, 1.6f, 0.9f);
                foreach (Transform c in traffic.transform)
                    if (c.position.z < -12f && (c.position - windowAt).sqrMagnitude < best) { best = (c.position - windowAt).sqrMagnitude; south = c; }
                if (south != null)
                {
                    cam.transform.position = windowAt;
                    cam.transform.LookAt(south.position + Vector3.up * 1f);
                    ShotCam(cam, "citynew_cars_window");
                }
                cam.fieldOfView = 45f;
                Transform carB = null;
                foreach (Transform c in traffic.transform)
                    if (CarModel(c) != null && CarModel(carA) != null && CarModel(c).name != CarModel(carA).name) { carB = c; break; }
                int n = 0;
                foreach (var car in new[] { carA, carB })
                {
                    if (car == null) continue;
                    string tag = n++ == 0 ? "a" : "b";
                    var at = car.position;
                    cam.transform.position = at - car.forward * 7f + Vector3.up * 3.0f;                 // (behind it, in the street)
                    cam.transform.LookAt(at + car.forward * 1.5f);
                    ShotCam(cam, $"citynew_car_{tag}_back");
                    Log($"[CityNew] picture {tag}: {(CarModel(car) ? CarModel(car).name : "no model")}");
                }
            }

            Log($"[CityNew] DONE fails={cityNewFails}");
            Quit(cityNewFails == 0 ? 0 : 1);
        }
    }
}
