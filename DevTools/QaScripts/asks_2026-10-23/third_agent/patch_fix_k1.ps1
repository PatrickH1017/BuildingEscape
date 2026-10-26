# Found by the two-machine runs of 2026-10-23 (m3 DragNet, m4 DemoNet):
# 1. A GAME BUG in T12 (DragNet): a body dragged by a CLIENT was let go at once on the host. DraggableBody.Update let go of the body when "the dragger is not active and enabled" - but on every machine but its own a player's WobblyPlayer is switched OFF
#    (NetPlayer.OnNetworkSpawn: Enable<WobblyPlayer>(mine); the owner's machine drives it), so the host always saw a remote dragger as gone. The drag only worked for the player of the machine that moves the body (the single-machine scenario Asks1023F had only that one). The check is
#    "the dragger's object is in the scene" now.
# 2. A test flaw in the older DemoNet client: it waited for the bullets of the bought clip to reach the reserve and then checked the team's cash at once, but the cash arrives with the host's next state (a frame or more after the ammunition announcement): it saw the old 420 instead of 300.
#    It waits for both now. Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$SC = $Root + 'Scripts\DraggableBody.cs'; Open-Src $SC
Rep '            if (!Dragger.isActiveAndEnabled || Dragger.InCustody) { End(); return; }' '            if (!Dragger.gameObject.activeInHierarchy || Dragger.InCustody) { End(); return; }          // (not isActiveAndEnabled: the WobblyPlayer of a player on another machine is switched off here, NetPlayer.OnNetworkSpawn)'
Save-Src
$SC = $Root + 'Editor\NetDev\NetDevDemo.cs'; Open-Src $SC
Rep '            yield return WaitFor(() => gc.Gun != null && gc.Gun.Magazine.Reserve == 24, 20, "the clip in the reserve");' '            yield return WaitFor(() => gc.Gun != null && gc.Gun.Magazine.Reserve == 24 && hm.Cash == 300, 20, "the clip in the reserve and its price off the team''s cash");          // (the cash comes with the host''s next state, a frame or more after the ammunition)'
Save-Src
"fix K1 patched"
