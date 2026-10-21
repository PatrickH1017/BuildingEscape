# Round 12, batch G13: three TEST fixes found by the first run of the new tests (the game code was right): the 20's neighbours on a dart board are 1 (right) and 5 (left), not 1 and 18; the Cricket test throws ten darts, not nine;
# the dart game ends when the player is more than 14 m from the board and the scenario's player stood in the elevator - he is put 4 m from the board first.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$E = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$E\NetDevRound12.cs"
Rep '&& DartsRules.Score(-0.8f * 0.17f, 0.8f).number == 18 && DartsRules.Score(0.8f * 0.17f, 0.8f).number == 1' '&& DartsRules.Score(-0.8f * 0.17f, 0.8f).number == 5 && DartsRules.Score(0.8f * 0.17f, 0.8f).number == 1'
Rep '6 on the right, 3 at the bottom, 11 on the left, 18 and 1 next to the 20' '6 on the right, 3 at the bottom, 11 on the left, 5 and 1 next to the 20'
Rep 'c.AllClosed && c.Won && c.DartsThrown == 9' 'c.AllClosed && c.Won && c.DartsThrown == 10'
Rep @'
                board.Begin(DartsRules.Mode.Game301);
'@ @'
                player.TeleportTo(new Vector3(board.FaceCentre.x + board.Normal.x * 4f, 1.1f, board.FaceCentre.z), player.transform.rotation);       // (a game ends when the player walks 14 m from the board: the scenario's player starts in the elevator)
                yield return new WaitForSeconds(0.3f);
                board.Begin(DartsRules.Mode.Game301);
'@
Save-Src
Write-Output 'round 12 batch G13 patched'
