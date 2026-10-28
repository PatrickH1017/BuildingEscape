# s3 (the stances in, the older gun scenarios): Guns missed one SMG bullet on the dummy (a gun is at the hip since 2026-10-27 and a shot from the hip scatters 1.5 times wider than the 8 m the test shoots from allows: the test holds the right mouse button),
# FpLook looks at the AIMED first-person views (the red dot, the scope, the reload motion), so it holds it too, and its pistol check expects both arms in the picture now (the stances hold the pistol in both hands).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevGuns.cs')
Rep '            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var pack = player.GetComponent<Backpack>();
            var cam = Camera.main;' @'
            hm.godMode = true; hm.freezeTimer = true;
            GunController.TestAim = true;              // (the right mouse button held: the cone of the shots is the aimed one, since 2026-10-27 a shot from the hip scatters 1.5 times wider)
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var pack = player.GetComponent<Backpack>();
            var cam = Camera.main;
'@
Save-Src
Open-Src ($Root + 'Editor\NetDev\NetDevFpLook.cs')
Rep '            hm.godMode = true;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var rig = FindFirstObjectByType<CameraFollow>();
            if (!rig || !pack) { Log("[FpLook] no camera rig or pack"); Quit(1); yield break; }' @'
            hm.godMode = true;
            GunController.TestAim = true;              // (the right mouse button held: these are the AIMED first-person views; at the hip a gun is lower and no sight is looked through, since 2026-10-27)
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var rig = FindFirstObjectByType<CameraFollow>();
            if (!rig || !pack) { Log("[FpLook] no camera rig or pack"); Quit(1); yield break; }
'@
Rep 'HeldGun.LocalArmMode == 1 && rig.ArmsShown && HeldGun.LocalDotShare == 0f && !hg.AlignerUsed' 'HeldGun.LocalArmMode == (hg.Clips != null && hg.Clips.Stances ? 2 : 1) && rig.ArmsShown && HeldGun.LocalDotShare == 0f && !hg.AlignerUsed'
Save-Src
"stance fix 3 patched"
