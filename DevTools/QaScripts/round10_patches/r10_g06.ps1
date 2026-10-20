# Round 10, batch G06: the Combat scenario's duels use a fresh guard each (a guard of the last duel was still shooting in god mode); the pew floor's comment; the built-player autopilot checks the guards' loadouts over the network.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'
function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}
FromDraft "$Ed\NetDevCombat.cs" 'C:\bqs\r10_draft\NetDevCombat.cs'

Open-Src "$Src\Outpost\OutpostLoot.cs"
Rep @'
        /// <summary>The floor whose outpost always holds a pew: floor 3, one before the first floor with armed guards (<see cref="ArmedGuards.DefaultFirstArmedFloor"/>).</summary>
'@ @'
        /// <summary>The floor whose outpost always holds a pew: floor 3, the floor where the guards are first armed (<see cref="ArmedGuards.DefaultFirstArmedFloor"/>; the owner, 2026-10-19: "The first level(3rd floor) of armed guards should have pistols").</summary>
'@
Save-Src

Open-Src "$Src\QaAutopilot.cs"
Rep @'
            if (!same) yield break;
            if (host) foreach (var g in GuardAI.All) if (g) { g.sightRange = 0.1f; g.hearingMultiplier = 0f; }       // (only looked at: an officer who arrests the player would stop the keypad)
'@ @'
            if (!same) yield break;
            // the guards' loadouts over the network (the owner, 2026-10-19: armed guards by floor, with a shield): the host decided who carries what and streams it with every guard, the client builds the same
            {
                if (!host) yield return Until(() => { int n = 0; foreach (var g in GuardAI.All) if (g && g.IsPuppet && g.Armed && g.Gun != null && g.Gun.HasModel) n++; return n >= 2; }, 40f, "the host's armed guards");
                yield return Wait(0.5f);
                int armedN = 0, wellFormed = 0;
                foreach (var g in GuardAI.All)
                {
                    if (!g || g.IsBoss || !g.Armed) continue;
                    armedN++;
                    if (g.Loadout.Kind == RangedWeapon.Kind.Pew && g.Shield == ShieldTier.Light && g.Gun != null && g.Gun.kind == RangedWeapon.Kind.Pew && g.GetComponentsInChildren<Transform>(true).Any(t => t.name == "BodyArmour")) wellFormed++;
                }
                Check($"{who}: floor {floor}'s guards - some carry a pistol and wear a light shield (a vest on the chest){(host ? "" : ", as the host told me")}", armedN >= 2 && wellFormed == armedN, $"({armedN} armed, {wellFormed} with the pistol, the light shield and the vest)");
            }
            if (host) foreach (var g in GuardAI.All) if (g) { g.sightRange = 0.1f; g.hearingMultiplier = 0f; }       // (only looked at: an officer who arrests the player would stop the keypad)
'@
Save-Src
