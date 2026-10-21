. 'C:\bqs\patchlib.ps1'
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevHarness.cs'
Rep 'mode != "gunsheet" && mode != "gunitems"' 'mode != "gunsheet" && mode != "charsheet" && mode != "gunitems"'
Save-Src
