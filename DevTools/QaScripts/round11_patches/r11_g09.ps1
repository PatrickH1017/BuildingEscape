# Round 11, batch G09 (the owner, 2026-10-19: "The build folder should be completely safe to send to play-testers. They should not be able to access anything integral to the game or its undeveloped features yet"):
#  - the QA autopilot only runs in the Editor or in a copy with the marker file be_qa.flag next to its exe (hd.ps1 puts it there; the send copy never has it)
#  - the send copy is scanned before it is zipped (final_build.ps1): only what a player needs, nothing of ours, no QA switch, a README that says nothing about floors or guns the demo does not have
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Scripts\QaAutopilot.cs'
Rep @'
        static bool Has(string name) => System.Environment.GetCommandLineArgs().Contains(name);
'@ @'
        static bool Has(string name) => System.Environment.GetCommandLineArgs().Contains(name);

        /// <summary>The autopilot is a QA tool: it runs in the Editor, or in a copy that has the marker file <c>be_qa.flag</c> next to its exe (the QA scripts put it there; the build that goes to the playtesters never has it, so the switch does nothing for them).</summary>
        static bool QaAllowed()
        {
            if (Application.isEditor) return true;
            try { return File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "be_qa.flag")); } catch { return false; }
        }
'@
Rep @'
            if (!Has("-be-autopilot") || FindFirstObjectByType<QaAutopilot>()) return;
'@ @'
            if (!Has("-be-autopilot") || !QaAllowed() || FindFirstObjectByType<QaAutopilot>()) return;
'@
Save-Src

# every QA script starts the built player through hd.ps1: with the autopilot switch it leaves the marker beside the exe it starts (a QA copy: never the send copy, which the scan below checks)
Open-Src 'C:\bqs\hd.ps1'
Rep @'
param([Parameter(Mandatory = $true)][string]$Exe, [string]$Arguments = '', [int]$TimeoutSeconds = 120, [string]$Desktop = 'ClaudeHiddenDesktop')
'@ @'
param([Parameter(Mandatory = $true)][string]$Exe, [string]$Arguments = '', [int]$TimeoutSeconds = 120, [string]$Desktop = 'ClaudeHiddenDesktop')
if ($Arguments -match '-be-autopilot') { Set-Content -Path (Join-Path (Split-Path -Parent $Exe) 'be_qa.flag') -Value 'a QA copy: the autopilot may run here' -Encoding ASCII }
'@
Save-Src

Open-Src 'C:\bqs\final_build.ps1'
Rep @'
Copy-Item 'C:\bqs\README_PLAYTEST.txt' "$sendDir\README.txt" -Force
'@ @'
Copy-Item 'C:\bqs\README_SEND.txt' "$sendDir\README.txt" -Force          # (the friends' README says nothing about floors, guns or armour the demo does not have; README_PLAYTEST.txt is our own list of changes)
'@
Rep @'
# 4: a zip of the send copy
'@ @'
# 3b: the safety scan of the send copy (the owner, 2026-10-19: "completely safe to send to play-testers ... not able to access anything integral to the game or its undeveloped features yet"): only what a player needs, nothing of ours, no QA marker, a README without the later floors
$unsafe = New-Object System.Collections.Generic.List[string]
$allowedTop = 'EmployeesTakeRevenge.exe', 'EmployeesTakeRevenge_Data', 'MonoBleedingEdge', 'D3D12', 'UnityCrashHandler64.exe', 'UnityPlayer.dll', 'README.txt', 'steam_appid.txt'
foreach ($e in Get-ChildItem $sendDir -Force) { if ($allowedTop -notcontains $e.Name) { $unsafe.Add("unexpected top-level entry $($e.Name)") } }
foreach ($pat in '*DoNotShip*', '*BackUpThisFolder*', '*.pdb', '*.mdb', '*.cs', '*.csproj', '*.sln', '*.md', '*.ps1', '*.bat', '*.meta', '*.log', '*.unity', '*.prefab', '*.mat', '*.fbx', '*.blend', '*.psd', '*.pfx', '*.pem', '*.key', 'be_qa.flag') {
  foreach ($f in Get-ChildItem $sendDir -Recurse -Force -Filter $pat -ErrorAction SilentlyContinue) { $unsafe.Add("forbidden file $($f.FullName.Substring($sendDir.Length))") }
}
$latin = [Text.Encoding]::GetEncoding(28591)
foreach ($f in Get-ChildItem $sendDir -Recurse -File -Force) {
  $s = $latin.GetString([IO.File]::ReadAllBytes($f.FullName))
  foreach ($n in 'phick', 'OneDrive', 'C:\Users', 'BuildingEscape\Assets', 'DEVELOPER_NOTES', 'Supabase') {
    $wide = (($n.ToCharArray() | ForEach-Object { [string]$_ + [char]0 }) -join '')
    if ($s.IndexOf($n, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $s.IndexOf($wide, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $unsafe.Add("'$n' found in $($f.FullName.Substring($sendDir.Length))") }
  }
}
$readme = Get-Content "$sendDir\README.txt" -Raw
foreach ($w in 'sniper', 'shotgun', 'rifle', 'sub machine', 'SMG', 'Medium Armour', 'Heavy Armour', 'rocket', 'Deagle', 'full game', 'Synty', 'floor 5', 'floor 6', 'floor 7', 'floor 8', 'floor 9', 'floors 5', 'floor 10', 'floor 11', 'floor 12', 'floor 13', 'DEVELOPER') { if ($readme -match [regex]::Escape($w)) { $unsafe.Add("README mentions '$w'") } }
Out-Sum "=== safety scan of the send copy: $($unsafe.Count) problem(s)$(if ($unsafe.Count) { ': ' + (($unsafe | Select-Object -First 8) -join ' | ') })"
if ($unsafe.Count -gt 0) { Out-Sum 'STOPPED: the send copy is not safe to send (no zip made)'; Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')"; return }

# 4: a zip of the send copy
'@
Save-Src
