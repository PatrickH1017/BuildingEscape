# MeleeNet, the melee holds over the network (the owner's Melee_Hold_Animations, 2026-10-28): the client holds the right mouse button (his own test button) with the crowbar in his hand and the host must see the weapon at the ready here (the aiming flag
# the guns send is the same one), and back at the idle when he lets go.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevMeleeNet.cs')
Rep '            Signal("h_done");' @'
            // ---- the client holds the right mouse button: the host sees his crowbar at the ready, and at the idle again when he lets go
            var cmc = clientPlayer ? clientPlayer.GetComponent<MeleeController>() : null;
            yield return WaitSig("c_ready_on", 90);
            yield return new WaitForSeconds(1.2f);
            MeleeCheck("host: the client holds the right mouse button: his crowbar is at the ready here (the flag came over)", cmc != null && cmc.Ready && cmc.ReadyShare > 0.95f, cmc != null ? $"(ready {cmc.Ready}, share {cmc.ReadyShare:0.00}, animated {cmc.Animated})" : "(no melee controller)");
            Signal("h_ready_on_ok");
            yield return WaitSig("c_ready_off", 90);
            yield return new WaitForSeconds(1.2f);
            MeleeCheck("host: the client lets go: his crowbar is at the idle here", cmc != null && !cmc.Ready && cmc.ReadyShare < 0.05f, cmc != null ? $"(ready {cmc.Ready}, share {cmc.ReadyShare:0.00})" : "(no melee controller)");
            Signal("h_ready_off_ok");
            Signal("h_done");
'@
Rep '            yield return WaitSig("h_done", 60);' @'
            GunController.TestAim = true;
            Signal("c_ready_on");
            yield return WaitSig("h_ready_on_ok", 60);
            GunController.TestAim = false;
            Signal("c_ready_off");
            yield return WaitSig("h_ready_off_ok", 60);
            GunController.TestAim = null;
            yield return WaitSig("h_done", 60);
'@
Save-Src
"melee fix 2 patched"
