# Round 2 verification, unattended: the QA tooling that lives outside BE_qa gets this round's QaAutopilot, then the single-process scenarios, then the chain
# (render audit, picture scenarios on the hidden desktop, two-process and five-player runs). Summaries: qa_summary_r1.txt, qa_summary_c1.txt, qa_summary_c1mp.txt.
$ErrorActionPreference = 'Continue'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Copy-Item "$here\BE_qa\Assets\Scripts\QaAutopilot.cs" "$here\qa_new\QaAutopilot.cs" -Force
Set-Location $here
.\qa_regress.ps1 -Prefix r1 -Methods @('Offline', 'Demo', 'PewLoot', 'Pew', 'Shaft:gfx', 'Floors:gfx', 'Doors', 'Outpost', 'Sneak', 'Etr', 'EtrCarry', 'LootQa', 'FloatQa', 'Looks:gfx') | Out-Null
.\qa_sync_clients.ps1 | Out-Null
.\qa_chain.ps1 -Prefix c1 -MpRuns @('Armed:host,c1', 'GunNet:host,c1', 'DemoNet:host,c1', 'ShaftNet:host,c1', 'OutpostNet:host,c1', 'MeleeNet:host,c1', 'Qa:host,c1,c2,c3,c4,late6:5') | Out-Null
"pipeline finished $(Get-Date -Format 'HH:mm:ss')"
