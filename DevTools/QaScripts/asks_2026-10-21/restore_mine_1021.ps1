# Puts my changed and new files (kept in scratchpad\mine_1021 with the list mine_1021_list.txt) back into both scratch trees (BE_dev and BE_dev_c) and proves it by hash.
$mine = Split-Path -Parent $MyInvocation.MyCommand.Path
function Norm-Hash([string]$p) { $b = [IO.File]::ReadAllBytes($p); $ms = New-Object IO.MemoryStream; foreach ($x in $b) { if ($x -ne 13) { $ms.WriteByte($x) } }; $ms.Position = 0; $h = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($ms)).Replace('-', ''); $ms.Dispose(); $h }
$list = [IO.File]::ReadAllLines("$mine\mine_1021_list.txt") | Where-Object { $_ }
foreach ($tree in "$mine\BE_dev", "$mine\BE_dev_c") {
  foreach ($rel in $list) {
    $src = "$mine\mine_1021\$rel"
    if (-not (Test-Path $src)) { "MISSING in the backup: $rel"; continue }
    $dst = "$tree\$rel"
    New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
    Copy-Item $src $dst -Force
  }
}
$bad = 0
foreach ($tree in "$mine\BE_dev", "$mine\BE_dev_c") {
  foreach ($rel in $list) {
    $src = "$mine\mine_1021\$rel"; $dst = "$tree\$rel"
    if (-not (Test-Path $src)) { continue }
    if (-not (Test-Path $dst)) { $bad++; "missing after copy: $dst"; continue }
    if ((Norm-Hash $src) -ne (Norm-Hash $dst)) { $bad++; "hash differs: $dst" }
  }
}
"restored $($list.Count) files into both trees, problems: $bad"
