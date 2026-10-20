# Sets the status cell of one row of the status table in section 2.23 of the real DEVELOPER_NOTES.md (exact: the row's id cell must exist once).
# Usage: .\notes_set.ps1 -Row R0 -Status 'APPLIED 18:13 ...'        and  -Append 'text' adds a paragraph at the end of section 2.23 (before "## 3.")
param([string]$Row, [string]$Status, [string]$Append)
. "$PSScriptRoot\patchlib.ps1"
$notes = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DEVELOPER_NOTES.md'
Open-Src $notes
if ($Row) {
  $pat = '(?m)^(\| ' + [regex]::Escape($Row) + ' \|[^\r\n]*\| )([^|\r\n]*)( \|)(\r?)$'
  $m = [regex]::Matches($script:s, $pat)
  if ($m.Count -ne 1) { throw "row $Row matched $($m.Count) times" }
  $script:s = [regex]::Replace($script:s, $pat, { param($x) $x.Groups[1].Value + $Status + $x.Groups[3].Value + $x.Groups[4].Value })
}
if ($Append) {
  $marker = '## 3. Things to check when playing'
  $i = $script:s.IndexOf($marker)
  if ($i -lt 0) { throw 'marker not found' }
  $text = $Append.Replace("`r`n", "`n").Replace("`n", $script:eol)
  $script:s = $script:s.Substring(0, $i) + $text + $script:eol + $script:eol + $script:s.Substring($i)
}
Save-Src
