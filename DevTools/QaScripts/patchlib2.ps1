# Helpers on top of patchlib.ps1: replace a whole region between two anchors, and a regex replace (both need exactly the expected number of matches).
function RepRegion([string]$startAnchor, [string]$endAnchor, [string]$new) {
  $s = $script:s
  $i = $s.IndexOf($startAnchor)
  if ($i -lt 0) { throw "start anchor not found: $startAnchor" }
  if ($s.IndexOf($startAnchor, $i + 1) -ge 0) { throw "start anchor found twice: $startAnchor" }
  $j = $s.IndexOf($endAnchor, $i)
  if ($j -lt 0) { throw "end anchor not found: $endAnchor" }
  $repl = $new.Replace("`r`n", "`n").Replace("`n", $script:eol)
  $script:s = $s.Substring(0, $i) + $repl + $s.Substring($j + $endAnchor.Length)
}
function RepRx([string]$pattern, [string]$new, [int]$count = 1) {
  $n = ([regex]::Matches($script:s, $pattern)).Count
  if ($n -ne $count) { throw "expected $count regex match(es), got $n for: $pattern" }
  $repl = $new.Replace("`r`n", "`n").Replace("`n", $script:eol).Replace('$', '$$')
  $script:s = [regex]::Replace($script:s, $pattern, $repl)
}
