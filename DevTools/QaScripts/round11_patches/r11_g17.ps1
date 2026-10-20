# Round 11, batch G17: the checklist scenario knows that a watch may still be inside the shut safe (then 'all valuables collected' is not true yet: that is the rule), and takes it after opening the safe.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevChecklist.cs'
Rep 'ChecklistCheck("all valuables taken: that line is done, the others are not", Done(ScanKind.Valuable) && !Done(ScanKind.Unique) && !Done(ScanKind.Safe) && !Done(ScanKind.Cash));' 'ChecklistCheck("all the valuables lying about taken: that line is done unless the shut safe still holds a watch, and no other line is", (safe == null || safe.WatchCount == 0) == Done(ScanKind.Valuable) && !Done(ScanKind.Unique) && !Done(ScanKind.Safe) && !Done(ScanKind.Cash));'
Rep 'ChecklistCheck("all unique items taken: that line is done too, the safe and the cash are not", Done(ScanKind.Valuable) && Done(ScanKind.Unique) && !Done(ScanKind.Safe) && !Done(ScanKind.Cash));' 'ChecklistCheck("all unique items taken: that line is done, the safe and the cash are not", Done(ScanKind.Unique) && !Done(ScanKind.Safe) && !Done(ScanKind.Cash));'
Rep @'
                foreach (var p in bundles) p.gameObject.SetActive(false);
'@ @'
                foreach (var p in bundles) p.gameObject.SetActive(false);
                foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => FloorChecklist.Classify(p) == (int)ScanKind.Valuable).ToList()) p.gameObject.SetActive(false);          // (the watch that was inside the safe, if there was one)
'@
Save-Src
