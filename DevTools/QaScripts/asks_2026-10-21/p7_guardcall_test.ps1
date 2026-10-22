# The GuardCall scenario said "a bullet starts no call" (2026-10-20); since 2026-10-21 a bullet that does not put an officer down starts one.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
Open-Src "$Tree\Assets\Editor\NetDev\NetDevGuardCall.cs"
if ($s.Contains('2026-10-21')) { "already patched"; return }
Rep 'blow cuts the call; a punch does the same; a bullet does not knock over and starts no call; the officers that come are backup officers with a quarter of the usual spotting time.' 'blow cuts the call; a punch does the same; a bullet does not knock over, but (since 2026-10-21) it starts a call too: the shot officer stands and radios at once (the call is cancelled when he is put down in time: scenario <c>Asks1021</c>); the officers that come are backup officers with a quarter of the usual spotting time.'
Rep '            // ---- a punch knocks over too: he calls on getting up. A bullet does not knock over: no call' '            // ---- a punch knocks over too: he calls on getting up. A bullet does not knock over, but it starts the call at once (the owner, 2026-10-21)'
Rep '            GuardCallCheck("a punch knocks a guard over, a bullet does not", punch.State == GuardState.Stunned && bullet.State != GuardState.Stunned);' @'
            GuardCallCheck("a punch knocks a guard over, a bullet does not", punch.State == GuardState.Stunned && bullet.State != GuardState.Stunned);
            GuardCallCheck("a bullet that does not put him down starts his radio call at once: he stands where he is and radios (the owner, 2026-10-21)", bullet.State == GuardState.Calling && bullet.IsCalling, $"(state {bullet.State}, calling {bullet.IsCalling})");
'@
Rep '            GuardCallCheck("the guard who was only shot at starts no call", !bullet.IsCalling && bullet.State != GuardState.Calling, $"(state {bullet.State})");' '            GuardCallCheck("the shot guard''s call is through by now (backup was called) and he is after the player who shot him", !bullet.IsCalling && (bullet.State == GuardState.Chase || bullet.State == GuardState.Investigate), $"(state {bullet.State})");'
Save-Src
"p7 done"
