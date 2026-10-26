# Two test fixes after the first run of patch_pack_keys.ps1 / patch_t7_battlefield.ps1:
#  - PackKeys bought the crowbar from the elevator: the dealer sells only to somebody at his counter (the pack must be in his zone): the scenario walks there first;
#  - R12's PVP-room check wanted 20 steel shelves and 6 pallets / crates / barrels: the battlefield (T7) took 8 racks and the pallets of the middle away and put the Battle Royale cover there: 15 shelves, and the cover counts the battlefield's pieces too.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevPlaytestFixes.cs')
Rep '            Vendor.Current.TestBuy(4);                                                                         // (the crowbar)' @'
            var counter = Vendor.Current.GetComponent<Collider>();
            player.TeleportTo(counter.bounds.center + Vector3.up * 0.2f, player.transform.rotation);           // (the dealer sells only to somebody at his counter)
            yield return new WaitForSeconds(1.5f);
            Vendor.Current.TestBuy(4);                                                                         // (the crowbar)
'@
Save-Src
Open-Src ($Root + 'Editor\NetDev\NetDevRound12.cs')
Rep 'int cover = warehouse.Count(w => (w.name == "pallet_loaded" || w.name == "crate_large" || w.name == "barrel") && InPvp(w.position));' 'int cover = warehouse.Count(w => (w.name == "pallet_loaded" || w.name == "crate_large" || w.name == "barrel" || PvpBattlefieldRules.Pieces.Any(bp => bp.key == w.name)) && InPvp(w.position));          // (T7: the battlefield''s pieces are cover too)'
Rep 'pvpContainers.Count == 0 && pvpRacks.Count >= 20 && racksSolid' 'pvpContainers.Count == 0 && pvpRacks.Count >= (PvpBattlefieldRules.On ? 15 : 20) && racksSolid'
Rep 'steel shelving with its boxes does (20 units or more, each solid with one box collider)' 'steel shelving with its boxes does (20 units or more, 15 with the battlefield of T7; each solid with one box collider)'
Save-Src
"pack keys test and R12 fixed"
