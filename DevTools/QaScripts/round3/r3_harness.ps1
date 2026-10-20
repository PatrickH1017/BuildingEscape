# Registers the round-3 scenarios in NetDevHarness.cs of a tree (idempotent) and copies NetDevR3.cs (CRLF) into the tree.
# Usage: .\r3_harness.ps1 [-Tree <project>] -Names InvUi,...      (every name needs a "<Name>Scenario()" in NetDevR3*.cs)
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev',
      [string[]]$Names = @('InvUi'))
. "$PSScriptRoot\patchlib.ps1"
$src = "$PSScriptRoot\NetDevR3.cs.txt"
if (Test-Path $src) {
  $text = [IO.File]::ReadAllText($src).Replace("`r`n", "`n").Replace("`n", "`r`n")
  [IO.File]::WriteAllText("$Tree\Assets\Editor\NetDev\NetDevR3.cs", $text, (New-Object Text.UTF8Encoding($false)))
  "copied NetDevR3.cs"
}
Open-Src "$Tree\Assets\Editor\NetDev\NetDevHarness.cs"
foreach ($name in $Names) {
  $mode = $name.ToLowerInvariant()
  if ($s.Contains("public static void $name()")) { "$name is registered already"; continue }
  # the entry point (before PewLook, which follows Ui)
  Rep '        public static void PewLook()' @"
        public static void $name()
        {
            SessionState.SetString("netdev.mode", "$mode");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PewLook()
"@
  # an offline scenario: it does not wait for the menu scene
  $pat = '(mode != "[A-Za-z]+")\)(\r?\n\s+yield return WaitFor\(\(\) => NetSession\.Instance != null && SceneManager\.GetActiveScene\(\)\.name == NetConst\.MenuScene)'
  if (-not [regex]::IsMatch($script:s, $pat)) { throw 'the offline-mode list was not found' }
  $script:s = [regex]::Replace($script:s, $pat, ('$1 && mode != "' + $mode + '")$2'))
  Rep '                case "ui": yield return UiScenario(); break;' "                case `"ui`": yield return UiScenario(); break;`r`n                case `"$mode`": yield return ${name}Scenario(); break;"
}
Save-Src
