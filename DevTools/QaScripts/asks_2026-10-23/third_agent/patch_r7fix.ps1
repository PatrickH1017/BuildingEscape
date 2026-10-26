# R7 test flaw (found 2026-10-23, 22:45, with the diagnostic of patch_r7diag.ps1): "in plain view of the camera the cone and the beam are shown" picked a guard and a spot 6 m from him with a clear line between the two, but the game's camera hangs 2.6 m BEHIND the
# player, and on this floor layout it was on the far side of a floor wall (1.4 m from it): the guard was round a corner for the camera, so GuardVision.CanBeSeen was right to say no. The test now picks a guard whose line from the camera's own place is free too
# (the same rule as GuardVision.Clear: nothing solid without a rigidbody). Needs patch_r7diag.ps1 first (it adds the helper block this one extends).
# Patches the scratch copy C:\bd (NetDevR7.cs); exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevR7.cs')
Rep '        IEnumerator R7Scenario()' @'
        /// <summary>
        /// Would the game's camera, held where <see cref="AimCamera"/> puts it for a player standing at <paramref name="spot"/> (the pivot 1.1 m above it) looking at the guard, see him? The camera hangs 2.6 m behind the player; his feet, middle or head must be on a free
        /// line from it (nothing solid without a rigidbody and not his own: the rule of GuardVision).
        /// </summary>
        static bool R7CameraSees(GuardAI g, Vector3 spot)
        {
            var pivot = spot + Vector3.up * 1.1f;
            var flat = g.transform.position - pivot; flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return false;
            flat.Normalize();
            var camAt = pivot - flat * 2.6f + Vector3.Cross(Vector3.up, flat) * 0.45f + Vector3.up * 1.35f;
            foreach (var h in new[] { 0.15f, 0.9f, 1.7f })
            {
                var to = g.transform.position + Vector3.up * h; var d = to - camAt;
                bool free = true;
                foreach (var hit in Physics.RaycastAll(camAt, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.attachedRigidbody == null && !hit.collider.transform.IsChildOf(g.transform)) { free = false; break; }
                if (free) return true;
            }
            return false;
        }

        IEnumerator R7Scenario()
'@
Rep 'if (OpenSpot(g.transform.position, 6f, out spot, false)) { target = g; break; }' 'if (OpenSpot(g.transform.position, 6f, out spot, false) && R7CameraSees(g, spot)) { target = g; break; }          // (the camera hangs 2.6 m behind the player: it must see him from where it is)'
Save-Src
"R7 selection fixed"
