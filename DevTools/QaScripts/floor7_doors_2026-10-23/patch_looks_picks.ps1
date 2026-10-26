# Rewrites the picture part of the DoorLooks scenario (scratchpad copy): doors can be asked for by number (BE_DOOR_PICKS), so before and after shoot the same doors, and every pick is also shot in the player's own light.
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
$SC = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-Eternum\2cfb45a0-f269-4edd-ba06-8b8b2fc18a76\scratchpad\doorwork\NetDevDoorLooks.cs'
Open-Src $SC
$new = @'
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
'@
RepRegion '            void Look(string name, Vector3 from, Vector3 at)' '            Log($"[DoorLooks] DONE fails={fails} pictures={shots}");' $new
Save-Src
