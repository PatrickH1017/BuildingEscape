# Round 12, batch G35 (after the suite `t18`: the check "the crosshair starts on the board" found it 22 cm off - G34 turned the view by atan(shoulder / (distance behind the head + distance to the point)), which ignored that turning also swings the
# camera round the head): the line of sight from the camera passes through a point a shoulder's width to the right of the head (the camera is `back` behind that point on the same line), so the view is turned left by asin(shoulder / distance to the
# point) from the bearing of the point, and tilted by atan((head height - point height) / sqrt(distance^2 - shoulder^2)).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'

Open-Src "$A\CameraFollow.cs"
Rep @'
                var d = lookAt.Value - Pivot();                                       // (the line of sight runs through the pivot: the camera sits behind it)
                float flat = new Vector2(d.x, d.z).magnitude;
                pitch = Mathf.Atan2(-d.y, flat) * Mathf.Rad2Deg;
                // the camera also hangs a shoulder to the right of the head: turned a little to the left, so that the crosshair is ON the point and not beside it
                float side = Mathf.Lerp(shoulderOffset, 0f, ViewRules.Smooth(zoom)), back = ViewRules.Distance(distance, zoom);
                aimYaw = centreYaw - Mathf.Atan2(side, back + flat) * Mathf.Rad2Deg;
'@ @'
                // the camera hangs behind the head and a shoulder to the right: its line of sight passes through the point a shoulder's width to the right of the head, so the view is turned to the left of the point's bearing
                // by asin(shoulder / distance) and tilted by the drop over the distance from that shoulder point, and the crosshair is ON the point, not beside it
                var d = lookAt.Value - Pivot();
                float flat = new Vector2(d.x, d.z).magnitude, side = Mathf.Lerp(shoulderOffset, 0f, ViewRules.Smooth(zoom));
                if (flat > side + 0.05f)
                {
                    aimYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - Mathf.Asin(side / flat) * Mathf.Rad2Deg;
                    pitch = Mathf.Atan2(-d.y, Mathf.Sqrt(flat * flat - side * side)) * Mathf.Rad2Deg;
                }
'@
Save-Src
Write-Output 'round 12 batch G35 patched'
