# The scenario's physical carry check follows the rule: two carriers need an opposite pair of free spots; three need two free sides (any two). The spots are where the planner puts them: just beyond each face of the box.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
$p = "$PSScriptRoot\NetDevAsks1021.cs.txt"
Open-Src $p
if ($s.Contains('freeSides')) { "already"; return }
Rep @'
                    bool ok = false;
                    var why = new System.Text.StringBuilder();
                    foreach (var axis in new[] { Vector3.right, Vector3.forward })
                    {
                        string a = SpotBlocker(box.center + axis * 1.0f, onFloor, l), b = SpotBlocker(box.center - axis * 1.0f, onFloor, l);
                        if (a == null && b == null) { ok = true; break; }
                        why.Append($"[{(axis == Vector3.right ? "x" : "z")}: +{a ?? "free"} / -{b ?? "free"}] ");
                    }
                    if (ok) bothSides++; else bad.Add($"{l.displayName} at {l.transform.position} {why}");
'@ @'
                    bool ok = false;
                    var why = new System.Text.StringBuilder();
                    int freeSides = 0;
                    foreach (var axis in new[] { Vector3.right, Vector3.forward })
                    {
                        // a carrier stands just beyond each face of the box (the planner's spot: half the box and 0.45 m)
                        float reachOut = (axis == Vector3.right ? box.extents.x : box.extents.z) + LootPlan.StandOffset;
                        string a = SpotBlocker(box.center + axis * reachOut, onFloor, l), b = SpotBlocker(box.center - axis * reachOut, onFloor, l);
                        if (a == null) freeSides++;
                        if (b == null) freeSides++;
                        if (l.lifters == 2 && a == null && b == null) ok = true;
                        why.Append($"[{(axis == Vector3.right ? "x" : "z")}: +{a ?? "free"} / -{b ?? "free"}] ");
                    }
                    if (l.lifters >= 3 && freeSides >= 2) ok = true;
                    if (ok) bothSides++; else bad.Add($"{l.displayName} at {l.transform.position} {why}");
'@
Rep 'AsksCheck($"floor {hm.Floor}: every treasure for two or three that stands on the floor has room for a carrier on each of two opposite sides ({bothSides} of {checkedLoot})"' 'AsksCheck($"floor {hm.Floor}: every treasure for two or three that stands on the floor has a spot for a carrier on each of two opposite sides (two carriers) or on two sides (three carriers) ({bothSides} of {checkedLoot})"'
Save-Src
"p20 done"
