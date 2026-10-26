# Puts the patches that were written but not yet in the scratch copy C:\bd into it, in the order that works (checked on a staging copy of the code folders first):
#   patch_t12b (busted teammates' bodies) -> patch_t29 (the developer spectator, part 1) -> patch_t29b (part 2: free camera, overlay, network panel) -> patch_t12c (a body follows the player's PATH)
#   -> patch_netk (two-machine scenario DragNet) -> patch_autopilot_bullets (the built-player autopilot's guard drop test: bullets, not a clip).
# Stops at the first patch that does not match (nothing is half-written inside a patch: a patch saves a file only after all its anchors matched). Usage: .\apply_chain_to_scratch.ps1 [-Dry]
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
$sa = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$steps = @(
  "$sa\second_agent\patch_t12b.ps1",
  "$sa\second_agent\patch_t29.ps1",
  "$sa\third_agent\patch_t29b.ps1",
  "$sa\third_agent\patch_t12c.ps1",
  "$sa\third_agent\patch_netk.ps1",
  "$sa\third_agent\patch_autopilot_bullets.ps1")
if ($Dry) { Write-Output 'A dry run checks each patch alone against the files as they are now: patches that build on an earlier one (t29b, t12c, netk) only match once the earlier ones are in. Use a staging copy (-Root) for the whole chain.' }
foreach ($s in $steps) {
  Write-Output "=== $(Split-Path -Leaf $s)"
  if ($Dry) { & $s -Dry } else { & $s }
}
Write-Output 'chain applied'
