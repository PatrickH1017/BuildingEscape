# Appends section 2.43 to the REAL DEVELOPER_NOTES.md (once), with a backup of the file first. The text: notes_1021_draft.md (the heading and the asks) + notes_1021_body.md (what was built) + regression_1021.txt (the regression line).
param([string]$Notes = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DEVELOPER_NOTES.md',
      [string]$Backup = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaBackups\before_asks_2026-10-21')
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$s = [IO.File]::ReadAllText($Notes)
if ($s.Contains("### 2.43 The owner's list of 2026-10-21")) { "section 2.43 exists already"; return }
New-Item -ItemType Directory -Force $Backup | Out-Null
if (-not (Test-Path "$Backup\DEVELOPER_NOTES.md")) { Copy-Item $Notes "$Backup\DEVELOPER_NOTES.md" }
$draft = [IO.File]::ReadAllText("$here\notes_1021_draft.md")
$body = [IO.File]::ReadAllText("$here\notes_1021_body.md")
$reg = [IO.File]::ReadAllText("$here\regression_1021.txt").Trim()
$section = $draft.Replace('@@BODY@@', $body.TrimEnd() + "`n`n" + $reg)
$section = $section.Replace("`r`n", "`n").Replace("`n", "`r`n")
$prefix = if ($s.EndsWith("`r`n")) { "`r`n" } else { "`r`n`r`n" }
[IO.File]::WriteAllText($Notes, $s + $prefix + $section.TrimEnd() + "`r`n", (New-Object Text.UTF8Encoding($false)))
"appended 2.43 ($($section.Length) characters) to $Notes"
