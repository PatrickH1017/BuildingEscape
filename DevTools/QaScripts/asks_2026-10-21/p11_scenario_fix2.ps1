# Round 3 of the Asks1021 scenario: which collider blocks a carrier (diagnosis), the rack materials (the magenta in the server pictures), and the whole ping section rewritten (one item at a time, then the markers).
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
$p = "$PSScriptRoot\NetDevAsks1021.cs.txt"
Open-Src $p
if ($s.Contains('SpotBlocker')) { "already patched"; return }

# ---- the carry check names what stands in a carrier's way
Rep @'
                    bool ok = false;
                    foreach (var axis in new[] { Vector3.right, Vector3.forward })
                    {
                        bool a = SpotFree(box.center + axis * 1.0f, onFloor, l), b = SpotFree(box.center - axis * 1.0f, onFloor, l);
                        if (a && b) { ok = true; break; }
                    }
                    if (ok) bothSides++; else bad.Add($"{l.displayName} at {l.transform.position}");
'@ @'
                    bool ok = false;
                    var why = new System.Text.StringBuilder();
                    foreach (var axis in new[] { Vector3.right, Vector3.forward })
                    {
                        string a = SpotBlocker(box.center + axis * 1.0f, onFloor, l), b = SpotBlocker(box.center - axis * 1.0f, onFloor, l);
                        if (a == null && b == null) { ok = true; break; }
                        why.Append($"[{(axis == Vector3.right ? "x" : "z")}: +{a ?? "free"} / -{b ?? "free"}] ");
                    }
                    if (ok) bothSides++; else bad.Add($"{l.displayName} at {l.transform.position} {why}");
'@
Rep @'
        /// <summary>Is there room for a person to stand at this spot (a capsule clear of everything that is built into the floor, the treasure and loose things)?</summary>
        static bool SpotFree(Vector3 at, float floorY, HeavyLoot self)
        {
'@ @'
        /// <summary>What stands where a person would stand at this spot (null: nothing): the name and height of the first thing that is built into the floor.</summary>
        static string SpotBlocker(Vector3 at, float floorY, HeavyLoot self)
        {
            var bottom = new Vector3(at.x, floorY + 0.7f, at.z);
            foreach (var c in Physics.OverlapCapsule(bottom, bottom + Vector3.up * 0.7f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!c || c.GetComponentInParent<HeavyLoot>() == self) continue;
                if (c.attachedRigidbody != null) continue;
                return $"{c.name}({c.bounds.min.y:0.0}-{c.bounds.max.y:0.0}m)";
            }
            return null;
        }

        /// <summary>Is there room for a person to stand at this spot (a capsule clear of everything that is built into the floor, the treasure and loose things)?</summary>
        static bool SpotFree(Vector3 at, float floorY, HeavyLoot self)
        {
'@

# ---- the server pictures: the rack materials, and the same view through a copy of the game's own camera
Rep @'
                            var sc = new GameObject("AsksServerCam").AddComponent<Camera>(); sc.enabled = false; sc.fieldOfView = 62f;
                            sc.transform.position = camPos; sc.transform.LookAt(sb.center);
                            ShotCam(sc, $"asks_server_f{hm.Floor}_{(int)srv.transform.position.x}"); Destroy(sc.gameObject);
                            break;
'@ @'
                            var sc = new GameObject("AsksServerCam").AddComponent<Camera>();
                            if (Camera.main) sc.CopyFrom(Camera.main);                                    // (the game''s own camera settings)
                            sc.enabled = false; sc.targetTexture = null; sc.fieldOfView = 62f;
                            sc.transform.position = camPos; sc.transform.LookAt(sb.center);
                            ShotCam(sc, $"asks_server_f{hm.Floor}_{(int)srv.transform.position.x}"); Destroy(sc.gameObject);
                            var rackInfo = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r && r.name.Contains("Server_Cabinet") && Vector3.Distance(r.bounds.center, sb.center) < 4f).Take(2)
                                .Select(r => $"{r.name}: {string.Join(" / ", r.sharedMaterials.Select(m => m ? $"{m.name} [{(m.shader ? m.shader.name : "no shader")}{(m.shader && !m.shader.isSupported ? " NOT SUPPORTED" : "")}]" : "no material"))}");
                            Log($"[Asks] racks near the server: {string.Join("; ", rackInfo)}");
                            break;
'@

# ---- the whole ping section, one item at a time and then the markers
$start = $s.IndexOf('            // ================================================================== the ping')
$end = $s.IndexOf('            Log($"[Asks] DONE fails={asksFails}");')
if ($start -lt 0 -or $end -lt 0) { throw "the ping section was not found" }
$new = [IO.File]::ReadAllText("$PSScriptRoot\ping_section.txt").Replace("`r`n", "`n")
$s = $s.Substring(0, $start) + $new + "`n" + $s.Substring($end)
Save-Src
"p11 prepared"
