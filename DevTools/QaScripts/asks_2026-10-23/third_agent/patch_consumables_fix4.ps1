# PlayerBuffs handed the stamina meter its drain factor every frame, which undid the one an older test (R7) sets by hand: now it only touches the drain while the endurance laster runs and puts it back to 1 once, when the effect ends.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcFile = Join-Path $here 'consumables\PlayerBuffs.cs'
Open-Src $srcFile
Rep '            if (stamina) stamina.DrainMultiplier = Set.DrainFactor(now);' @'
            if (stamina)
            {
                float drain = Set.DrainFactor(now);
                if (drain != 1f) { stamina.DrainMultiplier = drain; drainApplied = true; }          // (only while the endurance laster runs: the factor is not touched otherwise)
                else if (drainApplied) { stamina.DrainMultiplier = 1f; drainApplied = false; }
            }
'@
Rep '        Stamina stamina;' @'
        Stamina stamina;
        bool drainApplied;
'@
Rep '            if (stamina) stamina.DrainMultiplier = 1f;' '            if (stamina && drainApplied) { stamina.DrainMultiplier = 1f; drainApplied = false; }'
if ($Dry) { Write-Output "dry ok $script:path" } else { Save-Src; Copy-Item $srcFile ($Root + 'Scripts\Consumables\PlayerBuffs.cs') -Force; Write-Output 'copied PlayerBuffs.cs' }
"consumables fix 4 patched"
