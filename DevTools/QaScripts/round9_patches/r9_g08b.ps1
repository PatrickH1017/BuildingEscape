# Round 9, batch G08b: puts the GunLook picture scenario (the draft inside r9_g08.ps1) into NetDevGunSheet.cs. (G08's own insertion did not take: its anchor needed a newline the here-string does not have; this is what was run instead.)
$ErrorActionPreference = 'Stop'
$g8 = [IO.File]::ReadAllText('C:\bqs\r9_g08.ps1')
$m = [regex]::Match($g8, "(?s)\`$draft = @'\r?\n(.*?)\r?\n'@")
if (-not $m.Success) { throw 'draft not found in g08' }
$draft = $m.Groups[1].Value.Replace("`r`n", "`n")
if (-not $draft.EndsWith("        }`n    }`n}")) { throw 'draft end unexpected' }
$method = $draft.Substring(0, $draft.Length - "    }`n}".Length)       # (the class and namespace closers are already in the file)
. 'C:\bqs\patchlib.ps1'
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevGunSheet.cs'
if ($script:s.Contains('GunLookScenario')) { 'already there'; return }
$old = "            Log(`"[GunItems] DONE`");`n            Quit(0);`n        }`n"
$pat = Make-Pattern $old
if (([regex]::Matches($script:s, $pat)).Count -ne 1) { throw 'anchor not found once' }
$new = ($old + "`n" + $method.TrimEnd("`n") + "`n").Replace("`n", $script:eol)
$script:s = [regex]::Replace($script:s, $pat, { param($mm) $new })
Save-Src
