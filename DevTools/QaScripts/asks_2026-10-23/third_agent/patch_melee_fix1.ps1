# FpLook holds the right mouse button for the guns' aimed views (2.73); since the melee holds (2026-10-28) the same button raises a melee weapon to the READY, whose first-person picture is the clip's (the fist at the right edge, the crowbar's head
# out of the top of the picture). The crowbar check is of the IDLE hold - the weapon at the lower right, up and forward - so the button is let go for it (the ready's first-person picture is MeleeStance's).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevFpLook.cs')
Rep '            var crowbar = MeleeWeapon.Create(MeleeWeapon.Kind.Crowbar, at, null).GetComponent<Pickup>();' @'
            GunController.TestAim = false;             // (the idle hold of a melee weapon: with the right mouse button held it is raised to the ready, whose first-person picture is the MeleeStance scenario's)
            var crowbar = MeleeWeapon.Create(MeleeWeapon.Kind.Crowbar, at, null).GetComponent<Pickup>();
'@
Save-Src
"melee fix 1 patched"
