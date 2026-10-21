# Writes the documentation of round 12 part E: README_PLAYTEST item 21 (our own list of changes) and DEVELOPER_NOTES section 2.37 (after section 2.36). Usage: .\finish_docs.ps1 -State "<one paragraph: what was run and shipped>"
param([string]$State = 'not shipped yet')
$ErrorActionPreference = 'Stop'
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'

# 1) README_PLAYTEST: item 21 before "If something goes wrong" (in C:\bqs and the repo's copy)
$item = [IO.File]::ReadAllText('C:\bqs\readme_item21.txt').TrimEnd("`r", "`n")
foreach ($rp in 'C:\bqs\README_PLAYTEST.txt', "$real\DevTools\QaScripts\README_PLAYTEST.txt") {
  $t = [IO.File]::ReadAllText($rp)
  if ($t.Contains('21. Round 12 part E')) { "README already has item 21: $rp"; continue }
  $eol = if ($t.Contains("`r`n")) { "`r`n" } else { "`n" }
  $marker = "${eol}${eol}If something goes wrong"
  $i = $t.IndexOf($marker)
  if ($i -lt 0) { throw "marker not found in $rp" }
  $block = ($item -replace "`r?`n", $eol)
  $t = $t.Substring(0, $i) + $eol + $block + $t.Substring($i)
  [IO.File]::WriteAllText($rp, $t, (New-Object Text.UTF8Encoding($false)))
  "README item 21 added to $rp"
}

# 2) DEVELOPER_NOTES: section 2.37 after the end of 2.36 (the line that starts with "**The stun animation file")
$np = "$real\DEVELOPER_NOTES.md"
$lines = [Collections.Generic.List[string]]([IO.File]::ReadAllLines($np))
if ($lines.Exists({ param($l) $l.StartsWith('### 2.37 Round 12 part E') })) { 'notes already have 2.37' }
else {
  $at = $lines.FindIndex({ param($l) $l.StartsWith('**The stun animation file (found 2026-10-20)') })
  if ($at -lt 0) { throw 'end of 2.36 not found' }
  $sec = [IO.File]::ReadAllText('C:\bqs\notes_237_draft.md').TrimEnd("`r", "`n")
  $sec += "`n`n**State when written (2026-10-20, session bad83f09):** $State"
  $new = @('') + ($sec -split "`r?`n")
  $lines.InsertRange($at + 1, $new)
  [IO.File]::WriteAllLines($np, $lines, (New-Object Text.UTF8Encoding($false)))
  "notes: section 2.37 inserted after line $($at + 1)"
}
