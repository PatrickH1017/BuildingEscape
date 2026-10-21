# Round 12, batch G22 (looks only): the scoreboard hangs a little lower (it was close under the pipes), the throw-line picture is taken from far enough back to show the tape, and two more pictures of the PVP warehouse.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Scripts\Darts\DartBoard.cs'
Rep 'ScoreboardHeight = 0.95f' 'ScoreboardHeight = 0.88f'
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevRound12.cs'
Rep @'
                    cam.transform.position = board.LineCentre + board.Normal * 1.3f + Vector3.up * 1.6f; cam.fieldOfView = 60f;
                    cam.transform.LookAt(board.FaceCentre);
                    ShotCam(cam, "round12_throw_line");
'@ @'
                    cam.transform.position = board.LineCentre + board.Normal * 3.0f + Vector3.up * 1.7f; cam.fieldOfView = 60f;
                    cam.transform.LookAt(Vector3.Lerp(board.LineCentre, board.FaceCentre, 0.5f) + Vector3.up * 0.9f);
                    ShotCam(cam, "round12_throw_line");
'@
Rep 'ShotCam(cam, "round12_pvp_doorway");' 'ShotCam(cam, "round12_pvp_doorway");
                    cam.transform.position = new Vector3(8.6f, 1.7f, gen.PvpRoomArea.yMin + 2.3f);
                    cam.transform.LookAt(new Vector3(2.0f, 1.0f, gen.PvpRoomArea.yMin + 1.2f));
                    ShotCam(cam, "round12_pvp_hall");
                    cam.transform.position = new Vector3(3.5f, 1.8f, gen.PvpRoomAnnex.yMin + 0.8f);
                    cam.transform.LookAt(new Vector3(3.0f, 1.2f, gen.PvpRoomAnnex.yMax - 0.5f));
                    ShotCam(cam, "round12_pvp_annex");'
Save-Src
Write-Output 'round 12 batch G22 patched'
