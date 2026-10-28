# The BulletDamage scenario takes a fresh floor 1 for each gun (the earlier ones had left no officer with a clear line for the sniper rifle).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Dry) { Copy-Item (Join-Path $here 'combat2\NetDevBulletDamage.cs') ($Root + 'Editor\NetDev\NetDevBulletDamage.cs') -Force; Write-Output 'copied NetDevBulletDamage.cs' } else { Write-Output 'dry ok' }
"combat fix 2 patched"
