# Takes the UI picture scenarios one after another and collects every be_*.png they write into C:\bqs\uipics\<Tag>.
# Usage: .\uipics.ps1 -Tag before -Methods Ui,InvUi,DemoUi,Menus,MenusLobby,MenusRun
param([Parameter(Mandatory)][string]$Tag, [Parameter(Mandatory)][string[]]$Methods)
$out = "C:\bqs\uipics\$Tag"
New-Item -ItemType Directory -Force $out | Out-Null
$temp = [IO.Path]::GetTempPath()
foreach ($m in $Methods) {
  $t0 = Get-Date
  $r = & C:\bqs\ui.ps1 -Methods $m -Prefix "uip_$Tag" -Timeout 600 | Select-Object -First 1
  $n = 0
  Get-ChildItem $temp -Filter 'be_*.png' | Where-Object { $_.LastWriteTime -ge $t0 } | ForEach-Object { Copy-Item $_.FullName (Join-Path $out $_.Name) -Force; $n++ }
  "$m : $r ; $n pictures"
}
"pictures in $out : $((Get-ChildItem $out -Filter *.png | Measure-Object).Count)"
