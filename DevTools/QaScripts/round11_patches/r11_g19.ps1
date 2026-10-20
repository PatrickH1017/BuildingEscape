# Round 11, batch G19 (the owner, 2026-10-19: "try an IL2CPP build for the send copy: check it builds on this machine, retest it fully, and tell me the build time and anything that breaks"):
# the build can be made with a chosen scripting backend (DemoBuild -buildBackend il2cpp|mono; final_build.ps1 and ship.ps1 take -Backend, Mono stays the default), and the send copy of an IL2CPP build leaves out the folder that holds the generated C++ (it is for debugging, not for friends).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Editor\DemoBuild.cs'
Rep 'PlayerSettings.productName = ProductName;' @'
PlayerSettings.productName = ProductName;
            var backend = Arg("-buildBackend");
            if (!string.IsNullOrEmpty(backend))
            {
                // (the friends' build can be made with IL2CPP: the game's code is then native code with a metadata file, not a plain .NET assembly that any decompiler reads)
                var impl = backend.ToLowerInvariant() == "il2cpp" ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, impl);
                Debug.Log($"[DemoBuild] scripting backend {impl}");
            }
'@
Save-Src

Open-Src 'C:\bqs\final_build.ps1'
Rep "[string]`$Send = 'Playtest_2026-10-17_final')" "[string]`$Send = 'Playtest_2026-10-17_final', [string]`$Backend = 'mono')"
Rep "'-executeMethod', 'BuildingEscape.EditorTools.DemoBuild.Build', '-buildOutput', `$rawDir, '-logFile', `$log -PassThru" "'-executeMethod', 'BuildingEscape.EditorTools.DemoBuild.Build', '-buildOutput', `$rawDir, '-buildBackend', `$Backend, '-logFile', `$log -PassThru"
Rep 'if (-not $p.WaitForExit(1500000))' 'if (-not $p.WaitForExit($(if ($Backend -eq ''il2cpp'') { 3300000 } else { 1500000 })))'
Rep 'Out-Sum "=== build: exit=$($p.ExitCode) in' 'Out-Sum "=== build ($Backend): exit=$($p.ExitCode) in'
Rep "/XD '*DoNotShip'" "/XD '*DoNotShip' '*ButDontShipItWithYourGame'"
Rep "'UnityPlayer.dll', 'README.txt', 'steam_appid.txt'" "'UnityPlayer.dll', 'GameAssembly.dll', 'README.txt', 'steam_appid.txt'"
Save-Src

Open-Src 'C:\bqs\ship.ps1'
Rep "[string]`$Tag = 'fs1')" "[string]`$Tag = 'fs1', [string]`$Backend = 'mono')"
Rep "& 'C:\bqs\final_build.ps1' -Raw `$Raw -Send `"Playtest_`$Name`"" "& 'C:\bqs\final_build.ps1' -Raw `$Raw -Send `"Playtest_`$Name`" -Backend `$Backend"
Save-Src
