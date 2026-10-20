# Round 9, batch G01 (the owner, 2026-10-18: "Yes, add the SMG, rifle, shotgun and sniper guns from the Synty pack"): a QA-only editor tool method that prints how the candidate guns of the pack lie (parts, sizes, which way they run).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor'

Open-Src "$Ed\NetDev\WeaponInspect.cs"
Rep @'
        /// <summary>
        /// Where the pistol's barrel leaves the slide and what the silencer's two ends look like: the vertices at the front of the heavy pistol (the body and the slide) by depth, and the silencer's rings (radius and
'@ @'
        /// <summary>The candidate long guns of the pack (the owner, 2026-10-18: the SMG, rifle, shotgun and sniper): parts, sizes and which way each runs; exits when done.</summary>
        public static void Guns()
        {
            try
            {
                foreach (var n in new[]
                {
                    "SM_Wep_SubMGun_01", "SM_Wep_SubMGun_Lite_01", "SM_Wep_Rifle_Assault_01", "SM_Wep_Rifle_Assault_02", "SM_Wep_Rifle_Assault_03", "SM_Wep_Rifle_Assault_04", "SM_Wep_Rifle_Assault_05",
                    "SM_Wep_Shotgun_01", "SM_Wep_Sniper_01", "SM_Wep_Sniper_Lite_01", "SM_Wep_Sniper_Mil_01", "SM_Wep_Sniper_Heavy_01", "SM_Wep_MachineGun_01",
                    "SM_Wep_Shotgun_Ammo_01", "SM_Wep_Bullet_Ammo_01", "SM_Wep_Bullet_Ammo_02", "SM_Wep_Ammo_BulletLarge_01", "SM_Wep_Attachment_Grip_01", "SM_Wep_Attachment_Redot_01"
                })
                    Inspect(n);
            }
            finally { EditorApplication.Exit(0); }
        }

        /// <summary>
        /// Where the pistol's barrel leaves the slide and what the silencer's two ends look like: the vertices at the front of the heavy pistol (the body and the slide) by depth, and the silencer's rings (radius and
'@
Save-Src
