# Round 10: the player-facing README (the copy that goes into the build zip): officers, shields, bullets, melee, drops, the blast.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
else { Copy-Item 'C:\bqs\README_PLAYTEST.txt' 'C:\bqs\README_PLAYTEST_before_r10.txt' -Force }

Open-Src 'C:\bqs\README_PLAYTEST.txt'
Rep @'
3 hits drop an officer)
'@ @'
three hits drop an officer with no shield, four one with a light shield)
'@
Rep @'
From floor 4 the officers wear body armour
    (a bullet counts for half) and the backup comes faster the higher you climb.
'@ @'
The backup comes faster the higher you climb.
'@
Rep @'
16. The menus and the HUD are blue now (they were purple), with thinner outlines, softer shadows and a quieter shine.
'@ @'
16. The menus and the HUD are blue now (they were purple), with thinner outlines, softer shadows and a quieter shine.
17. Officers and guns (the demo has floors 3 and 4; the other floors are in the full game). A bullet damages an officer and never knocks him over: three pew bullets drop an officer with no shield, four one with
    a light shield (a vest on his chest); an officer who is shot while he holds somebody down lets go of them. A melee blow knocks an officer over for a few seconds (the bat 3, the crowbar 4, the hatchet 6) and never
    knocks him out. From floor 3 some officers carry guns, the others nothing: pistols with a light shield on floors 3 and 4, then sub machine guns (floor 5), a shotgun with a heavy shield (floor 6), assault rifles with a
    medium shield (floor 8) and two snipers with no shield (floor 10). An armed officer who goes down drops a clip for his gun: take it with E like a spare clip. A hit from a shotgun or a sniper officer ends your
    floor with a blast (you watch your teammates, the dealer buys you back); every other gun only knocks you down.
'@
Save-Src
