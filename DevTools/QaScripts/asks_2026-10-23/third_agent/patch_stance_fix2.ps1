# GunNet, the stances over the network (the owner, 2026-10-27: hip fire and aiming "in first and in third person"; teammates are told, NetPlayer.aiming): the client holds the right mouse button (his own test button) and the host must see his gun up
# at the sights, lets go and the host must see it at the hip again. (The host process draws the client's body; the raise it reaches is the number checked.)
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevPewLoot.cs')
Rep '            Signal("h_shot_ok");' @'
            Signal("h_shot_ok");

            // ---- the client aims: the host sees his gun come up to the sights, and go back to the hip when he lets go
            yield return WaitSig("c_aim_on", 90);
            yield return new WaitForSeconds(1.2f);
            GunNetCheck("host: the client holds the right mouse button: he is aiming here (the flag came over) and his gun is up at the sights", cgc != null && cgc.Aiming && cgc.Held != null && cgc.Held.RaiseNow > 0.95f, cgc != null && cgc.Held != null ? $"(aiming {cgc.Aiming}, raise {cgc.Held.RaiseNow:0.00}, stances {(cgc.Held.Clips != null && cgc.Held.Clips.Stances)})" : "(no gun)");
            Signal("h_aim_on_ok");
            yield return WaitSig("c_aim_off", 90);
            yield return new WaitForSeconds(1.2f);
            GunNetCheck("host: the client lets go: he is not aiming here and his gun is back at the hip", cgc != null && !cgc.Aiming && cgc.Held != null && Mathf.Abs(cgc.Held.RaiseNow - AimRules.HipRaise) < 0.04f, cgc != null && cgc.Held != null ? $"(aiming {cgc.Aiming}, raise {cgc.Held.RaiseNow:0.00})" : "(no gun)");
            Signal("h_aim_off_ok");
'@
Rep '            // ---- drop it for the host' @'
            // ---- the hip and the sights: the host must see them (his checks are on the other side)
            GunController.TestAim = true;
            Signal("c_aim_on");
            yield return WaitSig("h_aim_on_ok", 60);
            GunController.TestAim = false;
            Signal("c_aim_off");
            yield return WaitSig("h_aim_off_ok", 60);
            GunController.TestAim = null;

            // ---- drop it for the host
'@
Save-Src
"stance fix 2 patched"
