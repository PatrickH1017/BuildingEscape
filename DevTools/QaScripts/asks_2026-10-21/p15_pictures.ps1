# The server pictures: the racks use a Synty shader graph that this scratch copy cannot draw (pink). The picture swaps such materials for a plain dark one while it is taken (and puts them back), and a pink picture is a note, not a failure.
. "$PSScriptRoot\patchlib.ps1"
$p = "$PSScriptRoot\NetDevAsks1021.cs.txt"
Open-Src $p
if ($s.Contains('AsksShot')) { "already"; return }
Rep @'
                            ShotCam(sc, $"asks_server_f{hm.Floor}_{(int)srv.transform.position.x}"); Destroy(sc.gameObject);
                            var rackInfo = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r && r.name.Contains("Server_Cabinet") && Vector3.Distance(r.bounds.center, sb.center) < 4f).Take(2)
                                .Select(r => $"{r.name}: {string.Join(" / ", r.sharedMaterials.Select(m => m ? $"{m.name} [{(m.shader ? m.shader.name : "no shader")}{(m.shader && !m.shader.isSupported ? " NOT SUPPORTED" : "")}]" : "no material"))}");
                            Log($"[Asks] racks near the server: {string.Join("; ", rackInfo)}");
                            break;
'@ @'
                            // the materials near the server that this copy cannot draw (a shader graph: pink) are swapped for a plain dark one while the picture is taken
                            var plain = new Material(Shader.Find("Standard")) { color = new Color(0.16f, 0.18f, 0.24f) };
                            var swapped = new System.Collections.Generic.List<(Renderer r, Material[] was)>();
                            var seen = new System.Collections.Generic.HashSet<string>();
                            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                            {
                                if (!r || !r.enabled || Vector3.Distance(r.bounds.center, sb.center) > 7f) continue;
                                var ms = r.sharedMaterials;
                                if (!ms.Any(m => m && m.shader && (!m.shader.isSupported || m.shader.name.StartsWith("Shader Graphs/") || m.shader.name.Contains("Hidden/InternalErrorShader")))) continue;
                                if (seen.Add(r.name + "|" + string.Join(",", ms.Select(m => m && m.shader ? m.shader.name : "-")))) Log($"[Asks] a material this copy cannot draw: {r.name} [{string.Join(", ", ms.Select(m => m && m.shader ? m.shader.name + (m.shader.isSupported ? "" : " (not supported)") : "none"))}]");
                                swapped.Add((r, ms)); r.sharedMaterials = ms.Select(m => plain).ToArray();
                            }
                            AsksShot(sc, $"asks_server_f{hm.Floor}_{(int)srv.transform.position.x}");
                            foreach (var (r, was) in swapped) if (r) r.sharedMaterials = was;
                            Destroy(plain); Destroy(sc.gameObject);
                            break;
'@
Rep '        /// <summary>A walkable spot (a guard''s stop) with a clear line of 4.5 m' @'
        /// <summary>A picture like ShotCam, but a pink picture is only a note (the scratch copy cannot draw some of the Synty shader graphs).</summary>
        static void AsksShot(Camera cam, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            cam.targetTexture = null; RenderTexture.active = null;
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_shot_" + name + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Log($"[Asks] picture {path} (pink pixels: {RenderAudit.CountMagenta(tex.GetPixels32())})");
            Destroy(rt); Destroy(tex);
        }

        /// <summary>A walkable spot (a guard''s stop) with a clear line of 4.5 m
'@
Save-Src
"p15 done"
