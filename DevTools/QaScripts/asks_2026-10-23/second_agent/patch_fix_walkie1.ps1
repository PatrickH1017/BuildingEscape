# T5: run b3 found no model in the hand (Asks1023G). Say why in the log, as HeldGun does when it cannot attach (no hand bone / no mesh to measure) - it is also what a person who runs the game with a broken import needs to see.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$t = [IO.File]::ReadAllText((Join-Path $here 't5_new\NetDevWalkieProbe.cs')).Replace("`r`n", "`n").Replace("`n", "`r`n")
if ($Dry) { Write-Output "dry ok (would write) NetDevWalkieProbe.cs" } else { [IO.File]::WriteAllText('C:\bd\Assets\Editor\NetDev\NetDevWalkieProbe.cs', $t, (New-Object Text.UTF8Encoding($false))); Write-Output "wrote NetDevWalkieProbe.cs" }
$SC = 'C:\bd\Assets\Scripts\Walkie\HeldWalkie.cs'; Open-Src $SC
Rep '            if (!WalkieTalkie.Measure(out _, out float scale, out var centre, out var size)) return null;' @'
            if (!WalkieTalkie.Measure(out _, out float scale, out var centre, out var size)) { Debug.LogWarning("HeldWalkie: the walkie talkie model has nothing to measure (no mesh in the catalog's prefab: is the .blend model imported?)."); return null; }
'@.TrimEnd()
Save-Src
$SC = 'C:\bd\Assets\Scripts\Walkie\WalkieTalkie.cs'; Open-Src $SC
Rep '                if (Available && WeaponCatalog.Measure(MeleeWeapon.Catalog.walkie, Quaternion.identity, null, out var b))' @'
                if (Available && !WeaponCatalog.Measure(MeleeWeapon.Catalog.walkie, Quaternion.identity, null, out _)) Debug.LogWarning("WalkieTalkie: the catalog's walkie prefab has no renderer to measure (is the SpawnCampGames .blend model imported?): a dark box is used.");
                if (Available && WeaponCatalog.Measure(MeleeWeapon.Catalog.walkie, Quaternion.identity, null, out var b))
'@.TrimEnd()
Save-Src
"walkie warnings added"
