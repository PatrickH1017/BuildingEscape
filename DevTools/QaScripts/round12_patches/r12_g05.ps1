# Round 12, batch G05: after the first look at the pictures: the Intro's pack towers a little darker (they were washed out white), the night city's pack buildings reach 200 m, and the CityNew car pictures are taken
# from the south window and from behind a car in the street (the first ones were taken inside a tower).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Scripts\Intro\BossOffice.cs'
Rep 'var m = new Material(Shader.Find("Standard")) { color = new Color(1f - haze, 1f - haze, 1f - haze, 1f), mainTexture = src ? src.mainTexture : null };' @'
float shade = (1f - haze) * 0.78f;                            // (the pack's pale walls are white in this much light: a little darker)
            var m = new Material(Shader.Find("Standard")) { color = new Color(shade, shade, shade, 1f), mainTexture = src ? src.mainTexture : null };
'@
Save-Src

Open-Src 'C:\bqm\Assets\Scripts\Interior\ExteriorCity.cs'
Rep 'public const float BuildingReach = 160f;' 'public const float BuildingReach = 200f;'
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevCityNew.cs'
RepRegion 'cam.fieldOfView = 60f;' 'Log($"[CityNew] picture {tag}: {(CarModel(car) ? CarModel(car).name : "no model")}");' @'
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
'@
Save-Src

# the Intro's pre-warm also loads the city catalog (the plane, the cars and the buildings), so the first floor does not stall on it
Open-Src 'C:\bqm\Assets\Scripts\Intro\BossOffice.cs'
Rep 'public static void Prewarm() { SkyTexture(); WindowTexture(0); WindowTexture(1); LampTexture(); }' 'public static void Prewarm() { SkyTexture(); WindowTexture(0); WindowTexture(1); LampTexture(); _ = CityCatalog.Instance; }'
Save-Src
