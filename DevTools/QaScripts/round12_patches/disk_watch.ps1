# Watches the free space of C: while a build runs (the owner's Unity Editor is open on the same disk and a full disk could corrupt its Library): when less than -MinGB is free it writes C:\bqs\DISK_LOW.txt and stops the Unity BUILD processes
# that WE started (command line has -executeMethod ... DemoBuild.Build); it never touches the owner's Editor. Usage: .\disk_watch.ps1 [-MinGB 1.0] [-Seconds 3600]
param([double]$MinGB = 1.0, [int]$Seconds = 3600)
$end = (Get-Date).AddSeconds($Seconds)
Remove-Item 'C:\bqs\DISK_LOW.txt' -ErrorAction SilentlyContinue
while ((Get-Date) -lt $end) {
  $free = (Get-PSDrive C).Free / 1GB
  if ($free -lt $MinGB) {
    Set-Content 'C:\bqs\DISK_LOW.txt' "free $([math]::Round($free, 2)) GB at $(Get-Date -Format 'HH:mm:ss')"
    foreach ($p in Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") { if ($p.CommandLine -match 'DemoBuild\.Build') { Stop-Process -Id $p.ProcessId -Force } }
    break
  }
  Start-Sleep -Seconds 10
}
