# The Combat scenario said a shot guard "goes to look where it came from" (2026-10-19); since 2026-10-21 a shot patrol officer stops and radios for backup first and then goes after the shooter.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
Open-Src "$Tree\Assets\Editor\NetDev\NetDevCombat.cs"
if ($s.Contains('GuardState.Calling')) { "already patched"; return }
Rep '                // ---- a bullet: damage, no knock-over, he goes to look where it came from' '                // ---- a bullet: damage, no knock-over; he stops and radios for backup (the owner, 2026-10-21) and then goes after the shooter (before that, 2026-10-19: he went to look where it came from)'
Rep '                bool lookedAround = before == GuardState.Chase || before == GuardState.Stunned || a.State == GuardState.Investigate;' '                bool lookedAround = before == GuardState.Chase || before == GuardState.Stunned || a.State == GuardState.Calling || a.State == GuardState.Investigate;'
Rep 'CombatCheck("a bullet does its four hits to a guard, does not knock him over and sends him to look where it came from",' 'CombatCheck("a bullet does its four hits to a guard, does not knock him over, and he reacts: a patrolling guard stops and radios for backup (he goes after the shooter when the call is through)",'
Save-Src
"p21 done"
