# The DevUi check "locking closes an open dev menu" asked PanelOpen while the terminal was still up (the terminal counts as a panel): it asks the dev menu itself. Test fix only.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevDevUi.cs')
Rep 'DevUiCheck("and locking closes an open dev menu", !TestMode.MenuOpen && !TestMode.PanelOpen);' 'DevUiCheck("and locking closes an open dev menu", !TestMode.MenuOpen);'
Save-Src
"devui lock check patched"
