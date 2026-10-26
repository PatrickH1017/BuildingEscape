# The two-machine scenarios of the batch, one after another (host = C:\bd, the second machine = C:\bdc, a copy of the same project that is mirrored and compiled first with -Sync).
# DevSpectate / DevDenied: the developer spectator (T29). DragNet: the walkie talkie and a guard's body, both done by a CLIENT (T5, T12). DemoNet and PingNet: older scenarios that cross the connection code this batch touched (the player count, the payload).
# Summaries: C:\bqs\qa_summary_<prefix>.txt (m1 .. m5). Usage: .\run_mp_series.ps1 [-From 1]   (one heavy Unity job at a time: run it in the background and wait for the notification)
param([int]$From = 1)
$list = @(
  @{ n = 1; m = 'DevSpectate'; sync = $true },
  @{ n = 2; m = 'DevDenied'; sync = $false },
  @{ n = 3; m = 'DragNet'; sync = $false },
  @{ n = 4; m = 'DemoNet'; sync = $false },
  @{ n = 5; m = 'PingNet'; sync = $false })
foreach ($e in $list) {
  if ($e.n -lt $From) { continue }
  Remove-Item "C:\bqs\qa_summary_m$($e.n).txt" -ErrorAction SilentlyContinue
  if ($e.sync) { & 'C:\bqs\qa_mp_bd.ps1' -Method $e.m -Prefix "m$($e.n)" -Sync | Out-Null } else { & 'C:\bqs\qa_mp_bd.ps1' -Method $e.m -Prefix "m$($e.n)" | Out-Null }
  Get-Content "C:\bqs\qa_summary_m$($e.n).txt" | ForEach-Object { if ($_.Length -gt 300) { $_.Substring(0, 300) } else { $_ } }
}
