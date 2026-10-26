# The test flaws the a7 run found (not game bugs): the replacement-guard / body scenario counted guards after the voice test had called backup, the dev terminal scenario took the echo of a WRONG code for the secret,
# and the Pew scenario's disarm count did not allow for the replacement guard that comes out of a stairwell 15 s after an elimination (T13). Patches the scratch copy C:\bd; exact matches only.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = 'C:\bd\Assets\Editor\NetDev\'

$SC = $A + 'NetDevAsks1023C.cs'; Open-Src $SC
Rep '                int alive0 = guards.Count;' @'
                yield return LoadLevel(hm, 2);                                  // (a fresh floor: the voice test above made guards go and look and call backup, which would change the counts)
                yield return new WaitForSeconds(1.0f);
                gen = FindFirstObjectByType<FloorGenerator>();
                guards = AliveGuards();
                int alive0 = guards.Count;
'@.TrimEnd()
Rep '                var alive = AliveGuards();' @'
                yield return LoadLevel(hm, 2);                                  // (a fresh floor again: no backup has been called yet, no replacement is waiting)
                yield return new WaitForSeconds(1.0f);
                gen = FindFirstObjectByType<FloorGenerator>();
                var alive = AliveGuards();
'@.TrimEnd()
Save-Src

$SC = $A + 'NetDevAsks1023D.cs'; Open-Src $SC
Rep '            bool echoed = DevTerminal.Lines.Any(l => l.IndexOf("Catdog", System.StringComparison.OrdinalIgnoreCase) >= 0);' '            bool echoed = DevTerminal.Lines.Any(l => l.Contains("@Catdog813"));          // (the secret itself; the echo of the wrong guess "@catdog813" above is only what was typed)'
Save-Src

$SC = $A + 'NetDevPew.cs'; Open-Src $SC
Rep 'PewCheck("test menu: disarm takes the pew from every guard", taken == armTargets.Count && armTargets.All(g => g && !g.Armed && g.GetComponent<GuardGun>() == null), $"({taken})");' 'PewCheck("test menu: disarm takes the pew from every guard", taken >= armTargets.Count && armTargets.All(g => g && !g.Armed && g.GetComponent<GuardGun>() == null), $"({taken})");          // (>=: a guard that was eliminated earlier has been replaced by one out of a stairwell 15 s later - T13 - and he carries a pew too while "every guard carries a pew" is on)'
Save-Src
"fix tests patched"
