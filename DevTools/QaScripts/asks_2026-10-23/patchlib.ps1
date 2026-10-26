function Open-Src($p) {
  $script:path = $p
  $bytes = [IO.File]::ReadAllBytes($p)
  $script:bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
  $script:s = [IO.File]::ReadAllText($p)
  $crlf = ([regex]::Matches($script:s, "`r`n")).Count
  $lf = ([regex]::Matches($script:s, "(?<!`r)`n")).Count
  $script:eol = if ($crlf -gt $lf) { "`r`n" } else { "`n" }
}
function Make-Pattern([string]$old) {
  $parts = $old.Replace("`r`n", "`n").Split("`n") | ForEach-Object { [regex]::Escape($_) }
  return ($parts -join '\r?\n')
}
function Rep([string]$old, [string]$new, [int]$count = 1) {
  $pat = Make-Pattern $old
  $n = ([regex]::Matches($script:s, $pat)).Count
  if ($n -ne $count) { throw "expected $count match(es), got $n for: $old" }
  $repl = $new.Replace("`r`n", "`n").Replace("`n", $script:eol).Replace('$', '$$')
  $script:s = [regex]::Replace($script:s, $pat, $repl)
}
function Save-Src() {
  [IO.File]::WriteAllText($script:path, $script:s, (New-Object Text.UTF8Encoding($script:bom)))
  Write-Output "saved $script:path (eol=$(if ($script:eol -eq "`r`n") {'CRLF'} else {'LF'}) bom=$script:bom)"
}
