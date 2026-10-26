# The alarm beacons hung in the middle of rooms (the owner, 2026-10-24, with a picture: "the alarm lights are not properly placed, currently they are spawning in the middle of rooms ... instead they should be placed high up on the walls sporadically (touching the wall)").
# New BeaconRules (a scatter over the wall faces of hallways and big rooms), a new FloorGenerator.Beacons.cs (collects the walls) and AlarmBeacons mounts the lamp flat on the wall. Sources: beacon_walls\.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

foreach ($pair in @(@('BeaconRules.cs', 'Scripts\BeaconRules.cs'), @('FloorGenerator.Beacons.cs', 'Scripts\FloorGenerator.Beacons.cs'))) {
  $src = Join-Path $here ('beacon_walls\' + $pair[0]); $dst = $A + $pair[1]
  if ($Dry) { Write-Output "dry ok would copy $src -> $dst" } else { Copy-Item $src $dst -Force; Write-Output "copied $($pair[0])" }
}

Open-Src ($A + 'Scripts\AlarmBeacons.cs')
Rep 'dark like any lamp until the Big Alarm goes on;' 'mounted on the walls high up (<see cref="BeaconRules.Plan"/>: the lamp''s ring flat against the wall, facing out into the room), dark like any lamp until the Big Alarm goes on;'
Rep 'the pack''s small roof lamp (<c>Resources/AlarmBeacon</c>: a prefab of the PBR Lamps Pack''s Small_roof_lamp, made by <c>BeaconPrefabBuilder</c>), three times its size, in the corners (<see cref="BeaconRules.Plan"/>),' 'the pack''s small roof lamp (<c>Resources/AlarmBeacon</c>: a prefab of the PBR Lamps Pack''s Small_roof_lamp, made by <c>BeaconPrefabBuilder</c>), three times its size,'
Rep '        public bool HasColliders {' @'
        /// <summary>Where beacon <paramref name="i"/>'s back touches the wall.</summary>
        public Vector3 WallPointOf(int i) { var b = beacons[i]; var fwd = Quaternion.Euler(0f, b.yaw, 0f) * Vector3.forward; return b.at - fwd * (BeaconRules.Standoff + BeaconRules.RingBack); }

        /// <summary>How many beacons have their back against a solid wall: a ray from just in front of the lamp straight back hits a collider where the wall's face is (the lamps have no colliders of their own).</summary>
        public int TouchingWall()
        {
            int n = 0;
            foreach (var b in beacons)
            {
                var fwd = Quaternion.Euler(0f, b.yaw, 0f) * Vector3.forward;
                float expect = 0.1f + BeaconRules.Standoff + BeaconRules.RingBack;
                if (Physics.Raycast(b.at + fwd * 0.1f, -fwd, out var hit, expect + 0.1f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.distance - expect) < 0.03f) n++;
            }
            return n;
        }

        public bool HasColliders {
'@
Rep '                var b = new Beacon { index = i, yaw = s.yaw, at = new Vector3(s.x, ceilingY - BeaconRules.Drop, s.z) };' @'
                var out2 = Quaternion.Euler(0f, s.yaw, 0f) * Vector3.forward;                                   // (straight out of the wall into the room)
                float mount = BeaconRules.Standoff + BeaconRules.RingBack;                                    // (the ring's back is 2 cm off the wall: the lamp's own middle a little further)
                var b = new Beacon { index = i, yaw = s.yaw, at = new Vector3(s.x + out2.x * mount, ceilingY - BeaconRules.Drop, s.z + out2.z * mount) };
'@
Rep '                lamp.transform.rotation = Quaternion.Euler(40f, s.yaw, 0f);              // (the dome faces the middle of the room and a little down)' '                lamp.transform.rotation = Quaternion.Euler(0f, s.yaw, 0f);               // (the ring flat on the wall, the dome facing out into the room)'
Rep '                beam.position = b.at + Quaternion.Euler(40f, s.yaw, 0f) * Vector3.forward * 0.12f;' '                beam.position = b.at + out2 * 0.12f;'
Save-Src
"beacons on walls patched"
