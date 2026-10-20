# Round 11 (third part): the friends' README (README_SEND.txt) learns the scan's colours and the journal's checklist tab.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
else { Copy-Item 'C:\bqs\README_SEND.txt' 'C:\bqs\README_SEND_before_r11c.txt' -Force }

Open-Src 'C:\bqs\README_SEND.txt'
Rep 'Hold Z to scan the room you stand in: the valuables in it light up for a few seconds.' 'Hold Z to scan the room you stand in: what is in it glows for a few seconds - safes red, unique items such as weapons purple, cash green, valuables gold.'
Rep @'
An officer's bullet then knocks you down for 75 % of the usual time. Carry the piece back to the Loading Docks and the dealer sells Light Armour to the whole party ($600).
'@ @'
An officer's bullet then knocks you down for 75 % of the usual time. Carry the piece back to the Loading Docks and the dealer sells Light Armour to the whole party ($600).
19. The journal (J) has a second tab, CHECKLIST: All valuables collected, All unique items collected, All safes opened and All cash collected. When you have collected all of one kind its box is ticked in green and the line is crossed out in green.
20. The gun wall behind the dealer in the Loading Docks fills up as you unlock guns: a gun hangs there once it is unlocked (the pew once he sells it).
'@
Save-Src
