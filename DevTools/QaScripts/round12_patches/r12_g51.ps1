# Round 12, batch G51 (test only): the Combat duels (a guard shoots at the player) are tried up to three times: a guard can lose sight of the player on the way (a partition, the NavMesh) and give up the chase before he shoots
# ("Guard_0 lost the player and gave up the chase": the shotgun duel failed that way in t19 and t47 and passed on a rerun every time); the duel is about what his HIT does, not about his eyes.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevCombat.cs'
Rep '        /// <summary>An armed guard of this kind shoots at the player from <paramref name="dist"/> metres (god mode off, nobody else sees him): did the hit knock him down, did it blow him up?</summary>' @'
        /// <summary>The duel, tried up to three times (another guard each time): a guard can lose sight of the player on the way (a partition, the NavMesh) and give up the chase before he shoots; the duel is about what his hit does, not about his eyes.</summary>
        IEnumerator CombatDuelRetry(RangedWeapon.Kind kind, float dist, string label, bool oneShot = false)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                yield return CombatDuel(kind, dist, label, oneShot);
                if (duelHits >= 1) yield break;
                Log($"[Combat] {label}: no hit this time (attempt {attempt + 1} of 3)");
            }
        }

        /// <summary>An armed guard of this kind shoots at the player from <paramref name="dist"/> metres (god mode off, nobody else sees him): did the hit knock him down, did it blow him up?</summary>
'@
Rep '            yield return CombatDuel(RangedWeapon.Kind.Rifle, 7f, "rifle guard");' '            yield return CombatDuelRetry(RangedWeapon.Kind.Rifle, 7f, "rifle guard");'
Rep '            yield return CombatDuel(RangedWeapon.Kind.Smg, 6f, "SMG guard");' '            yield return CombatDuelRetry(RangedWeapon.Kind.Smg, 6f, "SMG guard");'
Rep '            yield return CombatDuel(RangedWeapon.Kind.Shotgun, 5f, "shotgun guard");' '            yield return CombatDuelRetry(RangedWeapon.Kind.Shotgun, 5f, "shotgun guard");'
Rep '            yield return CombatDuel(RangedWeapon.Kind.Sniper, 9f, "sniper guard");' '            yield return CombatDuelRetry(RangedWeapon.Kind.Sniper, 9f, "sniper guard");'
Save-Src
Write-Output 'round 12 batch G51 patched'
