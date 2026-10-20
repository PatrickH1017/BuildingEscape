# Round 11, batch G11: the docks scenario is a mode that does not wait for the menu, and the Sneak scenario counts the dock's new clean props (the old catalog cabinets, boxes and chairs are gone: the owner, 2026-10-19).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$Ed\NetDevHarness.cs"
Rep 'mode != "armour" && mode != "ui"' 'mode != "armour" && mode != "docklook" && mode != "ui"'
Save-Src

Open-Src "$Ed\NetDevSneak.cs"
RepRegion '// ---- storage junk' 'SneakCheck("dock: a chair or two", chairs >= 1 && chairs <= 3, $"({chairs} chairs)");' @'
// ---- storage: the lobby's clean props in the dock's dressing holder (the owner, 2026-10-19: "the assets from the lobby scene look way cleaner")
            int stacks = 0, blocks = 0, shelves = 0, forklifts = 0, drums = 0;
            var dressing = root.Find("Vendor/DockDressing");
            if (dressing != null)
                foreach (Transform tr in dressing)
                {
                    if (tr.name == "BoxStack") stacks++;
                    else if (tr.name == "BoxBlock") blocks++;
                    else if (tr.name == "Shelf") shelves++;
                    else if (tr.name == "Forklift") forklifts++;
                    else if (tr.name == "Drum") drums++;
                }
            var kinds = root.Find("Vendor").Cast<Transform>().GroupBy(x => x.name).OrderByDescending(g => g.Count()).Select(g => g.Key + "x" + g.Count());
            Log("[Sneak] info: dock objects: " + string.Join(", ", kinds));
            SneakCheck("dock: a lot of cardboard (stacks and pallet blocks)", stacks + blocks >= 14, $"({stacks} stacks, {blocks} pallet blocks)");
            SneakCheck("dock: steel shelving (the hall's and the trailer's)", shelves >= 6, $"({shelves} shelves)");
            SneakCheck("dock: two forklifts and some drums", forklifts == 2 && drums >= 4, $"({forklifts} forklifts, {drums} drums)");
'@
Save-Src
