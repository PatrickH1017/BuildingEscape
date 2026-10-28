# GunNet failed before the combat list too ("timeout waiting for the pew in the pack", DEVELOPER_NOTES 2.64): the pew has lain in floor 3's gun safe since 2026-10-23 and stays out of the world until the safe is open (GunSafeProp), and this
# two-machine test was never taught that. The host opens the safe as PewLoot does (the hack itself is played in the R7 scenario), the client waits until the pew is in the world on his machine.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevPewLoot.cs')
Rep '            var cult = System.Globalization.CultureInfo.InvariantCulture;' @'
            if (GunSafeProp.Active != null) GunSafeProp.Active.HostResult(true);          // (the pew lies in the gun safe since 2026-10-23: open it, as PewLoot does)
            yield return new WaitForSeconds(1.5f);
            GunNetCheck("host: the gun safe is open and the pew is in the world", GunSafeProp.Active != null && GunSafeProp.Active.IsOpen && pew.gameObject.activeSelf);
            var cult = System.Globalization.CultureInfo.InvariantCulture;
'@
Rep '            var pack = MyPack;
            wob.TeleportTo(pew.transform.position + new Vector3(0.6f, 1.1f, 0f), wob.transform.rotation);' @'
            var pack = MyPack;
            yield return WaitFor(() => pew.gameObject.activeSelf, 20, "the gun safe open on this machine (the pew in the world)");
            wob.TeleportTo(pew.transform.position + new Vector3(0.6f, 1.1f, 0f), wob.transform.rotation);
'@
Save-Src
"combat fix 5 patched"
