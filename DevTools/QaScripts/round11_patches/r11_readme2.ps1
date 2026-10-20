# Round 11 (second part): the player-facing README (the copy that goes into the build zip): lucky safes, the sniper in the outposts.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
else { Copy-Item 'C:\bqs\README_PLAYTEST.txt' 'C:\bqs\README_PLAYTEST_before_r11b.txt' -Force }

Open-Src 'C:\bqs\README_PLAYTEST.txt'
Rep @'
holding one pew pistol, and sometimes a little cash and a spare pistol clip
'@ @'
holding a gun only some of the time (item 19), and sometimes a little cash and a spare pistol clip
'@
Rep @'
18. Armour. A safe that is not the first one of its floor may hold a piece of armour instead of a gun:
'@ @'
18. Armour. A safe may hold a piece of armour instead of a gun:
'@
Rep @'
(Light $600, Medium $1,400, Heavy $2,800).
'@ @'
(Light $600, Medium $1,400, Heavy $2,800).
19. Lucky safes. A weapon safe (floor 3 and up) holds a gun only some of the time - about 4 times in 10 when it holds no armour - and may hold nothing at all, or only its cash, spare clip or gold watch. No safe is sure to hold the gun a floor
    unlocks, the first one of a floor either. The sniper rifle is never in a safe: from floor 10 on every guard outpost has one lying on its desk, in the open. A shotgun, when a safe holds one, stands upright in a taller safe.
'@
Save-Src
