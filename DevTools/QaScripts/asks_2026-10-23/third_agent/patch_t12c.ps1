# T12, part 3 (the owner, 2026-10-23: "Eliminated guards and players bodies should be draggable (in order to hide them)"): a dragged body follows the PATH the player walked, not a spot behind where he faces.
# Found by run b5 (Asks1023F): the player's body follows the camera, so "behind him" is not where he walks; the old hold spot lay across a wall whenever he walked sideways, the body stopped at the wall's edge and stayed there (3.9 m behind him). A body that follows his steps goes through every
# doorway and round every corner he did (DraggableBodyRules.Trail, pure, tested by Asks1023JTest). Needs T12 (guards) and T12 part 2 (busted teammates) applied. Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept; new files are written with CRLF.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 't12c_new\NetDevAsks1023JTest.cs') ($A + 'Editor\NetDev\NetDevAsks1023JTest.cs')

# ---- the pure rule: the path
$SC = $A + 'Scripts\DraggableBodyRules.cs'; Open-Src $SC
Rep 'using System;' @'
using System;
using System.Collections.Generic;
'@
Rep @'
            x = cx + dx / d * reach;
            z = cz + dz / d * reach;
        }
    }
}
'@ @'
            x = cx + dx / d * reach;
            z = cz + dz / d * reach;
        }

        /// <summary>
        /// The path the dragger has walked, which the body he drags follows. Not a spot behind where he FACES (a player's body follows the camera, so that is not where he walks: a body pulled straight to such a spot catches on the first wall), but his own steps: it goes through every
        /// doorway and round every corner he did. The path starts with the line from the body to the player and grows by a point for every <see cref="MinStep"/> he walks; the body slides along it at <see cref="FollowSpeed"/> until it is <see cref="FollowDistance"/> behind him (measured along
        /// the path), never goes backwards on it, and forgets what lies behind it, so the path stays short. Pure numbers (the floor plane only), so it is tested outside the editor.
        /// </summary>
        public sealed class Trail
        {
            /// <summary>A point is added to the path only after the player has walked this far from the last one (m).</summary>
            public const float MinStep = 0.15f;

            readonly List<float> xs = new List<float>(), zs = new List<float>();
            float bodyAt;                                   // how far along the path the body is (m)

            /// <summary>How many points the path holds now.</summary>
            public int Count => xs.Count;

            /// <summary>The length of the path from the body to the player's last point (m).</summary>
            public float Length { get; private set; }

            /// <summary>How far, along the path, the body is behind the player's last point (m).</summary>
            public float Lag => Math.Max(0f, Length - bodyAt);

            static float Dist(float ax, float az, float bx, float bz) { float dx = bx - ax, dz = bz - az; return (float)Math.Sqrt(dx * dx + dz * dz); }

            /// <summary>A new path: from the body to the player.</summary>
            public void Begin(float bodyX, float bodyZ, float playerX, float playerZ)
            {
                xs.Clear(); zs.Clear(); Length = 0f; bodyAt = 0f;
                xs.Add(bodyX); zs.Add(bodyZ);
                Add(playerX, playerZ, true);
            }

            /// <summary>The player stands here now: the path grows when he is a <see cref="MinStep"/> from its last point (or <paramref name="force"/>).</summary>
            public void Add(float x, float z, bool force = false)
            {
                int n = xs.Count;
                if (n == 0) { xs.Add(x); zs.Add(z); return; }
                float d = Dist(xs[n - 1], zs[n - 1], x, z);
                if (d < 1e-4f || (!force && d < MinStep)) return;
                xs.Add(x); zs.Add(z);
                Length += d;
            }

            /// <summary>One step of the body along the path (<paramref name="dt"/> seconds): where it is now.</summary>
            public void Advance(float dt, out float x, out float z)
            {
                float want = Math.Max(bodyAt, Length - FollowDistance);
                bodyAt = Math.Min(want, bodyAt + FollowSpeed * Math.Max(0f, dt));
                PointAt(bodyAt, out x, out z);
                Forget();
            }

            void PointAt(float s, out float x, out float z)
            {
                x = xs[0]; z = zs[0];
                float run = 0f;
                for (int i = 1; i < xs.Count; i++)
                {
                    float seg = Dist(xs[i - 1], zs[i - 1], xs[i], zs[i]);
                    if (run + seg >= s)
                    {
                        float t = seg < 1e-6f ? 1f : (s - run) / seg;
                        x = xs[i - 1] + (xs[i] - xs[i - 1]) * t; z = zs[i - 1] + (zs[i] - zs[i - 1]) * t;
                        return;
                    }
                    run += seg;
                }
                x = xs[xs.Count - 1]; z = zs[zs.Count - 1];
            }

            /// <summary>Drops the part of the path behind the body: it becomes the path's first point.</summary>
            void Forget()
            {
                if (bodyAt < 1e-4f || xs.Count < 2) return;
                PointAt(bodyAt, out float bx, out float bz);
                float run = 0f; int k = xs.Count - 1;
                for (int i = 1; i < xs.Count; i++)
                {
                    float seg = Dist(xs[i - 1], zs[i - 1], xs[i], zs[i]);
                    if (run + seg >= bodyAt) { k = i; break; }
                    run += seg;
                }
                xs.RemoveRange(0, k); zs.RemoveRange(0, k);
                xs.Insert(0, bx); zs.Insert(0, bz);
                bodyAt = 0f;
                float len = 0f;
                for (int i = 1; i < xs.Count; i++) len += Dist(xs[i - 1], zs[i - 1], xs[i], zs[i]);
                Length = len;
            }
        }
    }
}
'@
Save-Src

# ---- the body of an eliminated guard follows the path
$SC = $A + 'Scripts\DraggableBody.cs'; Open-Src $SC
Rep 'The body is held behind the player and kept on the floor plan: every step is cut short where the NavMesh ends' 'The body follows the path the player walked (DraggableBodyRules.Trail), so it goes through every doorway and round every corner he did,'
Rep '/// (a wall), so a body is never pulled through one.' '/// and is kept on the floor plan under its new place.'
Rep '        float heightOverFloor;' @'
        float heightOverFloor;
        readonly DraggableBodyRules.Trail trail = new DraggableBodyRules.Trail();
'@
Rep '                heightOverFloor = FloorOffset(transform.position);' @'
                heightOverFloor = FloorOffset(transform.position);
                trail.Begin(transform.position.x, transform.position.z, by.transform.position.x, by.transform.position.z);
'@
Rep @'
            var fwd = Dragger.transform.forward;
            DraggableBodyRules.HoldSpot(player.x, player.z, fwd.x, fwd.z, out float tx, out float tz);
            DraggableBodyRules.Step(here.x, here.z, tx, tz, Time.deltaTime, out float nx, out float nz);
            // stay on the floor plan: a step that would cross an edge of it (a wall) stops there
            if (NavMesh.SamplePosition(new Vector3(here.x, here.y, here.z), out var onMesh, 3f, NavMesh.AllAreas))
            {
                var to = new Vector3(nx, onMesh.position.y, nz);
                if (NavMesh.Raycast(onMesh.position, to, out var edge, NavMesh.AllAreas)) to = new Vector3(edge.position.x, onMesh.position.y, edge.position.z);
                if (NavMesh.SamplePosition(to, out var ok, 1f, NavMesh.AllAreas)) to = new Vector3(to.x, ok.position.y, to.z);
                transform.position = new Vector3(to.x, to.y + heightOverFloor, to.z);
            }
'@ @'
            trail.Add(player.x, player.z);                                           // (the body follows his steps: through every doorway and round every corner he went)
            trail.Advance(Time.deltaTime, out float nx, out float nz);
            // on the floor: the height of the floor plan under the new place (a place off it keeps the height the body had)
            float floorY = here.y - heightOverFloor;
            if (NavMesh.SamplePosition(new Vector3(nx, floorY, nz), out var ok, 1.5f, NavMesh.AllAreas)) floorY = ok.position.y;
            transform.position = new Vector3(nx, floorY + heightOverFloor, nz);
'@
Save-Src

# ---- the body of a busted teammate follows the path too (it is a physics body on its owner's machine: it is moved to where the path says)
$SC = $A + 'Scripts\WobblyPlayer.cs'; Open-Src $SC
Rep @'
        void FollowDragger()
        {
            var by = DraggedBy;
            if (!by) return;
            if (Custody != CustodyState.Busted) { DraggedBy = null; return; }
            var p = by.transform.position; var fwd = by.transform.forward;
            DraggableBodyRules.HoldSpot(p.x, p.z, fwd.x, fwd.z, out float tx, out float tz);
            var d = new Vector3(tx - rb.position.x, 0f, tz - rb.position.z);
            var v = Vector3.ClampMagnitude(d * 6f, DraggableBodyRules.FollowSpeed);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }
'@ @'
        readonly DraggableBodyRules.Trail dragTrail = new DraggableBodyRules.Trail();
        bool dragTrailLive;

        void FollowDragger()
        {
            var by = DraggedBy;
            if (!by) { dragTrailLive = false; return; }
            if (Custody != CustodyState.Busted) { DraggedBy = null; dragTrailLive = false; return; }
            var p = by.transform.position;
            if (!dragTrailLive) { dragTrail.Begin(rb.position.x, rb.position.z, p.x, p.z); dragTrailLive = true; }
            dragTrail.Add(p.x, p.z);
            dragTrail.Advance(Time.deltaTime, out float nx, out float nz);          // (inside FixedUpdate Time.deltaTime is the fixed step)
            rb.MovePosition(new Vector3(nx, rb.position.y, nz));
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
'@
Save-Src

# ---- the scenario: the body trails along the way he WALKED (not the way he faces)
$SC = $A + 'Editor\NetDev\NetDevAsks1023F.cs'; Open-Src $SC
Rep @'
            float behind = Vector3.Dot(gap.normalized, player.transform.forward);          // (a player faces where the camera looks, not where a teleport turned him: behind = against the way he FACES)
            A23F("T12: the body followed him: it trails a little more than a metre behind the way he faces, on the floor plan", gap.magnitude > 0.6f && gap.magnitude < 2.2f && behind < -0.5f && onPlan, $"(gap {gap.magnitude:0.00} m, along his facing {behind:0.00}, on the plan {onPlan})");
'@ @'
            float behind = Vector3.Dot(gap.normalized, dir);          // (a player faces where the camera looks, not where he walks: the body follows his PATH, so behind = against the way he WALKED)
            A23F("T12: the body followed him along his path: it trails a little more than a metre behind, on the floor plan", gap.magnitude > 0.6f && gap.magnitude < 2.2f && behind < -0.5f && onPlan, $"(gap {gap.magnitude:0.00} m, along his walk {behind:0.00}, on the plan {onPlan})");
'@
Save-Src

# ---- the pure checks are registered
$SC = $A + 'Editor\NetDev\NetDevTest.cs'; Open-Src $SC
Rep '            Asks1023ITest();' ('            Asks1023ITest();' + "`n" + '            Asks1023JTest();')
Save-Src
"T12 part 3 patched"
