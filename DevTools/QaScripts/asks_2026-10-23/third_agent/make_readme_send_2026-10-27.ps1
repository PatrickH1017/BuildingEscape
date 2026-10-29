# The friends' README for the next build after the owner's combat list of 2026-10-27 (hip fire and aiming, damage by ammunition, headshots, the hit marker): the README of 2026-10-25 with the MINIMAL FACTUAL changes (the bullet counts) and a short item for each new thing.
# Writes DevTools\QaScripts\asks_2026-10-23\third_agent\README_SEND_2026-10-27.txt. Every change is listed in DEVELOPER_NOTES.md 2.72 under "Words I changed". Exact matches only (patchlib).
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $here 'README_SEND_2026-10-27.txt'
Copy-Item (Join-Path $here 'README_SEND_2026-10-25.txt') $out -Force
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src $out

# 9 and 17: how many bullets an officer takes
Rep 'three hits drop an officer with no shield, four one with a light shield)' 'four bullets drop an officer with no shield, six one with a light shield)'
Rep 'A bullet damages an officer and never knocks him over: three pew bullets drop an officer with no shield, four one with a light shield (a vest on his chest);' 'A bullet damages an officer and never knocks him over: four pew bullets drop an officer with no shield, six one with a light shield (a vest on his chest);'

# the new items, after the consumables (35)
Rep 'a row above the weapon bar shows what you carry and what is running.' @'
a row above the weapon bar shows what you carry and what is running.
36. Aiming. A gun is held at your hip. Hold the right mouse button to aim, in first and in third person: the gun comes up (the sights, the red dot or the scope are looked through in first person) and a shot flies truer than from the hip (from the hip it scatters one and a half times wider). Your teammates see you aim.
37. Damage by ammunition. Light ammo (the pistol and the SMG) does 25 to an officer's body and 40 to his head; medium ammo (the rifle) 40 and 60; heavy ammo (the sniper rifle) 100; a shotgun shell does 75 to an officer in range (its eight pellets count as one hit on each officer). An officer has 100 health; a shield takes a share of every bullet. A hit to the head (the top fifth of his body) does more than a hit to the body.
38. The hit marker. A small X flashes at the crosshair when your bullet hits an officer (white, with a tick); a headshot flashes gold with a ding; the shot that puts an officer down for good flashes red, bigger, with a thump.
39. Every gun has its own hip-fire and aiming animations, standing and running (the pistol is held in both hands now): your character and your teammates' hold the pistol, the SMG, the rifle, the shotgun and the sniper rifle at the chest, or up at the sights while the right mouse button is held, and the gun moves with the run. A shot kicks the barrel up. Reloading still lowers a long gun.
40. The crowbar, the bat and the hatchet have hold animations too, idle and standing or running: the weapon is held up in front of you, and while the right mouse button is held it is raised to the ready, cocked back over the shoulder (your teammates see it). The swing is the same as before.
'@
Save-Src
"README_SEND_2026-10-27.txt written"
