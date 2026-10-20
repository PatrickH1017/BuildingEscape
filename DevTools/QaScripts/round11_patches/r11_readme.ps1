# Round 11: the player-facing README (the copy that goes into the build zip): armour.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
else { Copy-Item 'C:\bqs\README_PLAYTEST.txt' 'C:\bqs\README_PLAYTEST_before_r11.txt' -Force }

Open-Src 'C:\bqs\README_PLAYTEST.txt'
Rep @'
every other gun only knocks you down.
'@ @'
every other gun only knocks you down.
18. Armour. A safe that is not the first one of its floor may hold a piece of armour instead of a gun: Light Armour from floor 3 (the only kind in the demo), Medium Armour from floor 7, Heavy Armour from floor 10. A piece goes into your pack like any
    item (light 5.5 lbs, medium 11, heavy 17.6) and you wear it while it is in the pack - one piece at a time, and a vest shows on your character. A guard's bullet then knocks you down for 75 % (light), 50 % (medium) or 30 % (heavy) of the usual
    time; a shotgun or a sniper still blows you up. Carry a piece back to the Loading Docks and the dealer sells that tier to the whole party (Light $600, Medium $1,400, Heavy $2,800).
'@
Save-Src
