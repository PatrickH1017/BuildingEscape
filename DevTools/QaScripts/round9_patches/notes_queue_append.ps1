# Appends the owner's queue of 2026-10-19 (C:\bqs\notes_queue_2026-10-19.md) to the end of the real DEVELOPER_NOTES.md as section 2.28, after a backup. Safe to run twice (it stops if the section is there).
$file = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DEVELOPER_NOTES.md'
$bak = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaBackups\before_2_28_queue_notes_2026-10-19'
$bytes = [IO.File]::ReadAllBytes($file)
$bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
$text = [IO.File]::ReadAllText($file)
if ($text.Contains('### 2.28 The owner''s queue of 2026-10-19')) { 'section 2.28 is already there'; return }
if (-not (Test-Path $bak)) { New-Item -ItemType Directory -Path $bak | Out-Null; Copy-Item $file "$bak\DEVELOPER_NOTES.md" }
$crlf = ([regex]::Matches($text, "`r`n")).Count; $lf = ([regex]::Matches($text, "(?<!`r)`n")).Count
$eol = if ($crlf -gt $lf) { "`r`n" } else { "`n" }
$add = [IO.File]::ReadAllText('C:\bqs\notes_queue_2026-10-19.md').Replace("`r`n", "`n").Trim("`n").Replace("`n", $eol)
if (-not $text.EndsWith($eol)) { $text += $eol }
[IO.File]::WriteAllText($file, $text + $eol + $add + $eol, (New-Object Text.UTF8Encoding($bom)))
"section 2.28 appended (eol=$(if ($eol -eq "`r`n") {'CRLF'} else {'LF'}), backup $bak)"
