# The friends' README for the build of 2026-10-23 (the demo goes to floor 6; the owner's list of that day changed how a lot of it plays): the old README (C:\bqs\README_SEND.txt, kept as it was) with the MINIMAL FACTUAL changes the day's tasks need, plus a short item for each new thing.
# Writes DevTools\QaScripts\asks_2026-10-23\third_agent\README_SEND_2026-10-23.txt; final_build.ps1 -Readme <that file> puts it in the send copy. Every change is listed in DEVELOPER_NOTES.md 2.63 under "Words I changed". Exact matches only (patchlib).
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
$out = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'README_SEND_2026-10-23.txt'
Copy-Item 'C:\bqs\README_SEND.txt' $out -Force
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src $out

# 7: the computer (the tabs came in a new order and "Hacked" is "Commands"; the scan takes three seconds)
Rep 'Hold Z to scan the room you stand in:' 'Hold Z for three seconds to scan the room you stand in:'
Rep 'Its tabs: SAFES (the safes of the floor you are on, and no other floor''s: each one''s room and its code when the computer has it), NAV (a plan of the floor:' 'Its tabs, in this order: NAV (a plan of the floor:'
Rep 'a little terminal sits under the plan) and HACKED (the same terminal, bigger).' 'a little terminal sits under the plan), SAFES (the safes of the floor you are on, and no other floor''s: each one''s room and its code) and COMMANDS (the same terminal, bigger).'
# 9: melee weapons, the pistol and its bullets
Rep 'Melee weapons are only ever on the desk in the guards'' outpost (two on floor 1, a chance of one on the others).' 'Melee weapons are on the desk in the guards'' outpost (two on floor 1, a chance of one on the others); one crowbar also lies somewhere on floor 1 outside the outpost.'
Rep 'The pew pistol (8 rounds a clip, 3 clips, three hits drop' 'The pew pistol (8 rounds in the gun; the bullets you carry reload it, there is no limit on them; three hits drop'
Rep 'sells the pistol and extra clips for money (three clips of a gun is the most anybody can carry: he will not sell a fourth).' 'sells the pistol and bullets for money.'
Rep 'so buy clips.' 'so buy bullets.'
# 11: how far the demo goes
Rep 'The demo ends after floor 4.' 'The demo ends after floor 6.'
# 12: the safes
Rep 'Floor 3 has two safes and floor 4 three - every one in a different numbered room.' 'Floor 3 has two safes and floors 4, 5 and 6 three - every one in a different numbered room.'
Rep 'A safe on floors 3 and 4 holds a pew only some of the time (about 4 times in 10 when it holds no armour)' 'A safe on floors 3 to 6 holds a gun only some of the time (about 4 times in 10 when it holds no armour; on floors 5 and 6 it is not always the pew)'
Rep '(it does not say what is inside; the first safe''s code is always there, the others'' codes most of the time)' '(it does not say what is inside; every safe''s code is there)'
Rep 'it sometimes holds a little cash and a spare pistol clip as well:' 'it sometimes holds a little cash and a spare clip of bullets as well:'
Rep 'A clip''s rounds go into your pistol''s reserve (no pistol, no clip).' 'A clip''s bullets go into your gun''s reserve (no gun, no clip).'
# 17 and 18: what an officer drops, the armour
Rep 'An armed officer who goes down drops a clip for his gun: take it with E like a spare clip.' 'An officer who goes down sometimes drops a few bullets (none most of the time, up to three): run over them to take them (you need a gun they fit).'
Rep 'A safe on floor 3 or 4 may hold a piece of Light Armour instead of a pew.' 'A safe on floors 3 to 6 may hold a piece of Light Armour instead of a gun.'
Rep 'and you wear it while it is in the pack - one piece at a time, and a vest shows on your character.' 'and you wear it while it is in the pack - the better of two pieces (a second one is a very heavy spare to bring to a teammate), and a vest shows on your character.'
# the new things of the day
Rep '20. The gun wall behind the dealer in the Loading Docks fills up as you unlock guns: a gun hangs there once it is unlocked (the pew once he sells it).' @'
20. The gun wall behind the dealer in the Loading Docks fills up as you unlock guns: a gun hangs there once it is unlocked (the pew once he sells it).
21. Officers. An officer needs two seconds next to you to pin you. When an officer goes down for good another comes out of a stairwell 15 seconds later, and an officer who sees a body raises the alarm. Take hold of a body with Q (Q again lets go)
    and drag it away to hide it; you walk slower while you drag it. Hold Alt (or V) to look around: a tap does nothing.
22. The dealer sells a walkie talkie: hold it out (equip it) and talk to a teammate who holds one out too, at any distance.
23. The ammunition panel shows the rounds in your gun and all the bullets you carry; a ring fills up in it while you reload. Weapons weigh about half what they did, Drop all loot drops everything worth money, and the elevator's buttons rise from left to right.
'@
Save-Src
"README written: $out"
