# Round 10, batch G04: the pew in the outpost's gun safe holds one clip (a gun found does), LootQa knows the new armed rule, and the F1 menu can arm every guard with each gun and show the blast.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$Src\FloorGenerator.Outpost.cs"
Rep @'
            var pew = RangedWeapon.Create(RangedWeapon.Kind.Pew, at + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, t + 0.06f, 0.02f), parent, yaw);
'@ @'
            var pew = RangedWeapon.Create(RangedWeapon.Kind.Pew, at + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, t + 0.06f, 0.02f), parent, yaw, 0);      // (a gun found holds its one clip and no spare: the owner, 2026-10-19)
'@
Save-Src

Open-Src "$Src\Guns\ArmedGuards.cs"
Rep @'
        /// <summary>Take the pew off every guard (test menu); guards made later are armed again only if a rule says so.</summary>
'@ @'
        /// <summary>Test menu: every live guard of the floor carries this kind of gun with the shield that goes with it (<see cref="GuardLoadouts.ShieldOf"/>), or nothing when <paramref name="kind"/> is null. Returns how many were changed.</summary>
        public static int SetAllNow(RangedWeapon.Kind? kind)
        {
            int n = 0;
            foreach (var g in GuardAI.All)
            {
                if (!g || g.Dead || g.IsPuppet || g.IsBoss) continue;
                g.SetLoadout(kind.HasValue ? GuardLoadout.Of(kind.Value) : GuardLoadout.Unarmed);
                g.SyncGun();
                n++;
            }
            return n;
        }

        /// <summary>Take the pew off every guard (test menu); guards made later are armed again only if a rule says so.</summary>
'@
Save-Src

Open-Src "$Src\TestMode.cs"
Rep @'
            GUILayout.Label($"Guards on this floor: {alive}, with a pew: {armed}");
'@ @'
            GUILayout.Label($"Guards on this floor: {alive}, armed: {armed}");
            GUILayout.Label("Every guard on this floor carries (and wears the shield that goes with it):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("nothing")) Say($"{ArmedGuards.SetAllNow(null)} guard(s) unarmed");
            foreach (var k in new[] { RangedWeapon.Kind.Pew, RangedWeapon.Kind.Smg, RangedWeapon.Kind.Rifle, RangedWeapon.Kind.Shotgun, RangedWeapon.Kind.Sniper })
                if (RangedWeapon.Has(k) && GUILayout.Button(RangedWeapon.Specs[(int)k].name)) Say($"{ArmedGuards.SetAllNow(k)} guard(s) now carry the {RangedWeapon.Specs[(int)k].name}, with a {GuardLoadouts.ShieldOf(k).ToString().ToLower()} shield");
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Show the blast of a shotgun or sniper hit (only the picture, in front of you)") && WobblyPlayerRegistry.All.Count > 0 && WobblyPlayerRegistry.All[0])
            {
                var me = WobblyPlayerRegistry.All[0];
                BlastFx.Play(me.transform.position + me.transform.forward * 3.5f + Vector3.up * 0.9f, HudTheme.PlayerColour(1));
            }
'@
Save-Src

Open-Src "$Ed\NetDevLootQa.cs"
Rep @'
                    LqCheck($"{tag}: guards are {(floor >= 4 ? "all armed" : "unarmed")}", floor >= 4 ? armed == guards.Count : armed == 0, $"({armed} of {guards.Count} armed)");
'@ @'
                    LqCheck($"{tag}: {(floor >= 3 ? "some guards are armed, not all of them" : "no guard is armed")}", floor >= 3 ? armed >= 1 && armed < guards.Count : armed == 0, $"({armed} of {guards.Count} armed)");
'@
Save-Src
