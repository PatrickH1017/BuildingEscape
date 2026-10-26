# R7 (the guard's vision cone is drawn only when the camera can see him) failed two checks after the first batches of 2026-10-23 ("shown 0.00" with a clear view: it passed at 14:44 the same day). GuardVision.CanBeSeen needs the camera's line to the guard to be free of
# anything solid that has no rigidbody, so the test now NAMES what blocks that line (and says where the guard is on the screen) instead of only saying "shown 0.00".
# Patches the scratch copy C:\bd (NetDevR7.cs, LF); exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevR7.cs')
Rep '        IEnumerator R7Scenario()' @'
        /// <summary>For the report: where the guard is in the camera's picture and the first solid thing without a rigidbody on the camera's line to his feet, middle and head (what GuardVision.CanBeSeen looks at).</summary>
        static string R7WhatBlocks(Camera cam, GuardAI g)
        {
            if (!cam || !g) return "(no camera or guard)";
            var vp = cam.WorldToViewportPoint(g.transform.position + Vector3.up * 0.9f);
            var from = cam.transform.position;
            var sb = new System.Text.StringBuilder($"(on the screen at {vp.x:0.00}, {vp.y:0.00}, {vp.z:0.0} m deep; camera {from.x:0.0}, {from.y:0.0}, {from.z:0.0}; guard {g.transform.position.x:0.0}, {g.transform.position.z:0.0}; ");
            foreach (var h in new[] { 0.15f, 0.9f, 1.7f })
            {
                var to = g.transform.position + Vector3.up * h; var d = to - from;
                string first = "free";
                foreach (var hit in Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance))
                {
                    if (hit.collider.attachedRigidbody != null || hit.collider.transform.IsChildOf(g.transform)) continue;
                    string path = hit.collider.name; var t = hit.collider.transform.parent; for (int k = 0; k < 3 && t != null; k++, t = t.parent) path = t.name + "/" + path;
                    first = $"{path} at {hit.distance:0.0} m";
                    break;
                }
                sb.Append($"line to {h:0.0} m: {first}; ");
            }
            return sb.Append(')').ToString();
        }

        IEnumerator R7Scenario()
'@
Rep 'R7Check("in plain view of the camera the cone and the beam are shown", vis != null && vis.Shown > 0.95f, vis != null ? $"(shown {vis.Shown:0.00})" : "");' 'R7Check("in plain view of the camera the cone and the beam are shown", vis != null && vis.Shown > 0.95f, vis != null ? $"(shown {vis.Shown:0.00}) {(vis.Shown > 0.95f ? "" : R7WhatBlocks(cam, target))}" : "");'
Rep 'R7Check("the wall gone, he is seen again and the cone comes back", vis != null && vis.Shown > 0.95f, vis != null ? $"(shown {vis.Shown:0.00})" : "");' 'R7Check("the wall gone, he is seen again and the cone comes back", vis != null && vis.Shown > 0.95f, vis != null ? $"(shown {vis.Shown:0.00}) {(vis.Shown > 0.95f ? "" : R7WhatBlocks(cam, target))}" : "");'
Save-Src
"R7 diagnostics patched"
