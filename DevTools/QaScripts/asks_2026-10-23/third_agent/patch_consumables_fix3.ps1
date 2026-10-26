# The machine's picture of the Consumables scenario: the player stood in the elevator, too far from the machine (the screen closes itself when the player is away): he is put at the machine's tray first.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcFile = Join-Path $here 'consumables\NetDevConsumables.cs'
Open-Src $srcFile
Rep '                VendingScreen.Show(machine);' @'
                player.TeleportTo(machine.TrayPoint + Vector3.up * 0.6f, player.transform.rotation);          // (the screen closes itself when the player is away from the machine)
                yield return new WaitForSeconds(0.6f);
                VendingScreen.Show(machine);
'@
if ($Dry) { Write-Output "dry ok $script:path" } else { Save-Src; Copy-Item $srcFile ($Root + 'Editor\NetDev\NetDevConsumables.cs') -Force; Write-Output 'copied NetDevConsumables.cs' }
"consumables fix 3 patched"
