param([string[]]$Methods = @('Menus'), [string]$Prefix = 'u1', [int]$Timeout = 900, [string]$Project = 'C:\bq')
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$proj = $Project
robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
foreach ($m in $Methods) {
  $log = "C:\bqs\${Prefix}_$m.log"
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $r = & C:\bqs\hd.ps1 -Exe $unity -Arguments "-projectPath `"$proj`" -executeMethod BuildingEscape.EditorTools.NetDevHarness.$m -logFile `"$log`"" -TimeoutSeconds $Timeout
  "$m : $r in $([int]$sw.Elapsed.TotalSeconds) s"
  Select-String -Path $log -Pattern '\[Ui\]|\[Menu\]|\[Menus\]|\[Inv\]|error CS|FAIL|DONE|NullReference' | Select-Object -First 25 | ForEach-Object { $l = $_.Line.Trim(); if ($l.Length -gt 220) { $l.Substring(0, 220) } else { $l } }
}
