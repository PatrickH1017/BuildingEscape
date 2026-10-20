# Round 10, batch G08: the blast is centred on the player's chest (a body's pivot is its middle, so 0.8 m above it was above the head); the blast picture is taken from a clear spot with the blast on the player.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'
function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}
FromDraft "$Ed\NetDevCombat.cs" 'C:\bqs\r10_draft\NetDevCombat.cs'

Open-Src "$Src\HeistManager.cs"
Rep @'
            var at = p.transform.position + Vector3.up * 0.8f;
'@ @'
            var at = p.transform.position + Vector3.up * 0.25f;                  // (a body's pivot is its middle: the chest is a little above it)
'@
Save-Src

Open-Src "$Src\TestMode.cs"
Rep @'
BlastFx.Play(me.transform.position + me.transform.forward * 3.5f + Vector3.up * 0.9f, HudTheme.PlayerColour(1));
'@ @'
BlastFx.Play(me.transform.position + me.transform.forward * 3.5f + Vector3.up * 0.3f, HudTheme.PlayerColour(1));
'@
Save-Src
