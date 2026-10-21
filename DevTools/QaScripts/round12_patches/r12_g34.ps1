# Round 12, batch G34 (after the picture of the keys, `u2`): the crosshair started a hand's width to the right of the board - the camera hangs behind the head and a shoulder to the right, so a view turned square to the board looks past it.
# `CameraFollow.LimitAim` now turns the view so that the line of sight from the camera meets the point it is told to look at (the board's middle): atan(shoulder / (distance behind the head + distance to the point)) to the left.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$A\CameraFollow.cs"
Rep @'
                var d = lookAt.Value - Pivot();                                       // (the line of sight runs through the pivot: the camera sits behind it)
                pitch = Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
'@ @'
                var d = lookAt.Value - Pivot();                                       // (the line of sight runs through the pivot: the camera sits behind it)
                float flat = new Vector2(d.x, d.z).magnitude;
                pitch = Mathf.Atan2(-d.y, flat) * Mathf.Rad2Deg;
                // the camera also hangs a shoulder to the right of the head: turned a little to the left, so that the crosshair is ON the point and not beside it
                float side = Mathf.Lerp(shoulderOffset, 0f, ViewRules.Smooth(zoom)), back = ViewRules.Distance(distance, zoom);
                aimYaw = centreYaw - Mathf.Atan2(side, back + flat) * Mathf.Rad2Deg;
'@
Save-Src

Open-Src "$E\NetDevRound12.cs"
Rep @'
                if (gfx)
                {
                    var scam = new GameObject("R12StanceCam").AddComponent<Camera>();
'@ @'
                var camMain = Camera.main;
                if (camMain != null)
                {
                    var sight = new Ray(camMain.transform.position, camMain.transform.forward);
                    float den = Vector3.Dot(sight.direction, board.Normal);
                    float tt = den < -0.001f ? Vector3.Dot(board.FaceCentre - sight.origin, board.Normal) / den : -1f;
                    float off = tt > 0f ? Vector3.Distance(sight.GetPoint(tt), board.FaceCentre) : 99f;
                    R12Check("the crosshair starts on the board: the view is turned so that the line of sight from the camera meets the board's middle (within 12 cm)", off < 0.12f, $"({off * 100f:0} cm off)");
                }
                if (gfx)
                {
                    var scam = new GameObject("R12StanceCam").AddComponent<Camera>();
'@
Save-Src
Write-Output 'round 12 batch G34 patched'
