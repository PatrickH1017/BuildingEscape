# Test fixes after the first runs of the combat list: a test's own aiming button must work without a locked cursor (a batch run has none), and the tests aim the game camera at what they shoot (the shot goes along the camera when the point is behind it).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Dry) { Copy-Item (Join-Path $here 'combat2\NetDevBulletDamage.cs') ($Root + 'Editor\NetDev\NetDevBulletDamage.cs') -Force; Copy-Item (Join-Path $here 'combat2\NetDevAimHip.cs') ($Root + 'Editor\NetDev\NetDevAimHip.cs') -Force; Write-Output 'copied the two test files' }
Open-Src ($Root + 'Scripts\Guns\GunController.cs')
Rep 'Aiming = viewed ? usable && (TestAim ?? Input.GetMouseButton(1)) : wobbly && wobbly.IsAiming;' 'Aiming = viewed ? (TestAim ?? (usable && Input.GetMouseButton(1))) : wobbly && wobbly.IsAiming;'
Save-Src
"combat fix 1 patched"
