# Round 11, batch G02: the armour's tests - the pure ArmourTest, the Play Mode scenario Armour (registered in the harness), the Combat scenario's duel that can measure one bullet's knock-down, and the old safe tests that assumed a gun in every safe.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor\NetDev'

function NewFile([string]$target, [string]$draft) {
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n").Replace("`n", "`r`n") + "`r`n"
  if ($env:R2_DRY) { Write-Output "dry run: would write $target"; return }
  [IO.File]::WriteAllText($target, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "written $target"
}
function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}
NewFile "$Ed\NetDevArmourTest.cs" 'C:\bqs\r11_draft\NetDevArmourTest.cs'
NewFile "$Ed\NetDevArmour.cs" 'C:\bqs\r11_draft\NetDevArmour.cs'
FromDraft "$Ed\NetDevCombat.cs" 'C:\bqs\r10_draft\NetDevCombat.cs'

Open-Src "$Ed\NetDevTest.cs"
Rep @'
            GuardsTest();
'@ @'
            GuardsTest();
            ArmourTest();
'@
Save-Src

Open-Src "$Ed\NetDevHarness.cs"
Rep @'
        public static void Combat()
        {
'@ @'
        public static void Armour()
        {
            SessionState.SetString("netdev.mode", "armour");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Combat()
        {
'@
Rep @'
&& mode != "combat" && mode != "ui" &&
'@ @'
&& mode != "combat" && mode != "armour" && mode != "ui" &&
'@
Rep @'
                case "combat": yield return CombatScenario(); break;
'@ @'
                case "combat": yield return CombatScenario(); break;
                case "armour": yield return ArmourScenario(); break;
'@
Save-Src

# ---- the old safe tests: a safe that is not the first of its floor may hold armour instead of a gun
Open-Src "$Ed\NetDevGunsTest.cs"
Rep @'
                        deterministic &= items1.Length == items2.Length && items1[0] == items2[0];
                        int kind = (int)SafeRules.GunOfItem(items1[0]);
'@ @'
                        deterministic &= items1.Length == items2.Length && items1[0] == items2[0];
                        if (SafeRules.IsArmourItem(items1[0])) continue;                     // (armour instead of a gun, never in the first safe: ArmourTest has its rules)
                        int kind = (int)SafeRules.GunOfItem(items1[0]);
'@
Rep @'
every weapon safe holds exactly one gun, the same on every machine
'@ @'
every weapon safe holds exactly one gun (or, in a safe that is not the first of its floor, a piece of armour: ArmourTest), the same on every machine
'@
Save-Src

Open-Src "$Ed\NetDevR7Test.cs"
Rep @'
                                foreach (int k in items) contents &= SafeRules.IsGunItem(k) && f >= RangedWeapon.Specs[(int)SafeRules.GunOfItem(k)].unlockFloor && (f > 4 || k == SafeRules.PewItem);      // (a gun by floor: the pew only on floors 3 and 4, GunsTest has the rest)
'@ @'
                                foreach (int k in items) contents &= SafeRules.IsArmourItem(k) ? f >= ArmourRules.FirstFloor(SafeRules.ArmourOfItem(k)) : SafeRules.IsGunItem(k) && f >= RangedWeapon.Specs[(int)SafeRules.GunOfItem(k)].unlockFloor && (f > 4 || k == SafeRules.PewItem);      // (a gun by floor: the pew only on floors 3 and 4, GunsTest has the rest; armour from its own floor, ArmourTest has the rules)
'@
Save-Src
