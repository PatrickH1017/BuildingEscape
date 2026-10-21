# Round 12, batch G25 (look only): the throw-line picture is taken from higher up and looking down, so that the tape on the floor is in the picture.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevRound12.cs'
Rep @'
                    cam.transform.position = board.LineCentre + board.Normal * 3.0f + Vector3.up * 1.7f; cam.fieldOfView = 60f;
                    cam.transform.LookAt(Vector3.Lerp(board.LineCentre, board.FaceCentre, 0.5f) + Vector3.up * 0.9f);
'@ @'
                    cam.transform.position = board.LineCentre + board.Normal * 2.6f + Vector3.up * 2.0f; cam.fieldOfView = 70f;
                    cam.transform.LookAt(Vector3.Lerp(board.LineCentre, board.FaceCentre, 0.35f) + Vector3.up * 0.3f);
'@
Save-Src
Write-Output 'round 12 batch G25 patched'
