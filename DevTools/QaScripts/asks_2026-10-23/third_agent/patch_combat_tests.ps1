# The tests of the combat list of 2026-10-27, part 1: the older ones that knew the old per-gun hit counts (pew 4, SMG 2, rifle 4, shotgun 1 a pellet, sniper 10) now say the owner's set damage (25 / 25 / 40 / 75 a shot / 100), the new tests and their registration.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Dry) { Write-Output 'dry ok would copy NetDevBulletDamage.cs' } else { Copy-Item (Join-Path $here 'combat2\NetDevBulletDamage.cs') ($A + 'Editor\NetDev\NetDevBulletDamage.cs') -Force; Write-Output 'copied NetDevBulletDamage.cs' }

Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void PropScan()' @'
        public static void BulletDamage()
        {
            SessionState.SetString("netdev.mode", "bulletdamage");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PropScan()
'@
Rep ' && mode != "consumables" && ' ' && mode != "consumables" && mode != "bulletdamage" && '
Rep '                case "consumables": yield return ConsumablesScenario(); break;' @'
                case "consumables": yield return ConsumablesScenario(); break;
                case "bulletdamage": yield return BulletDamageScenario(); break;
'@
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevTest.cs')
Rep '            ConsumablesRulesTest();' @'
            ConsumablesRulesTest();
            BulletDamageRulesTest();
'@
RepRx 'Check\("gun: three bullets knock a guard out.*\r?\n' '            Check("gun: four bullets knock a guard out (a melee weapon needs ten hits; the owner, 2026-10-27: light ammo, 25 HP a hit)", GunRules.BulletsToKill(10, pew.guardHits) == 4);
'
RepRx 'Check\("gun: the boss takes more bullets".*\r?\n' '            Check("gun: the boss takes more bullets", GunRules.BulletsToKill(30, pew.guardHits) == 12 && GunRules.BulletsToKill(10, 1) == 10 && GunRules.BulletsToKill(0, 0) == 1);
'
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevGunsTest.cs')
Rep 'S[sniper].damage > S[pew].damage && S[pew].damage > S[rifle].damage && S[rifle].damage > S[smg].damage' 'S[sniper].damage > S[rifle].damage && S[rifle].damage > S[pew].damage && S[pew].damage >= S[smg].damage'
Rep 'Check("guns: a shotgun fires eight pellets (one hit of ten each), every other gun one bullet;' 'Check("guns: a shotgun fires eight pellets (75 HP a shot, dealt once), every other gun one bullet;'
Rep 'S[shotgun].Pellets == 8 && S[shotgun].guardHits == 1 &&' 'S[shotgun].Pellets == 8 && Mathf.Approximately(S[shotgun].guardHits, 7.5f) &&'
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevR7Test.cs')
RepRx 'Check\("armour: an unshielded officer takes three pew bullets.*\r?\n' '            Check("armour: an unshielded officer takes four pew bullets (light ammo, 25 HP), one with a medium shield (the old vest) eight", GunRules.BulletsToKill(10, pew.guardHits) == 4 && GuardLoadouts.BulletsToKill(10f, pew.guardHits, ShieldTier.Medium) == 8);
'
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevMeleeHealthTest.cs')
Rep '            int pewHits = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].guardHits;' '            float pewHits = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].guardHits;'
RepRx 'Check\("melee health: a pew bullet \(4 hits\) is 40 HP.*\r?\n' '            Check("melee health: a pew bullet (2.5 hits) is 25 HP, four of them put a guard down and three do not", Mathf.Approximately(GuardHealthRules.HitsToHp(pewHits), 25f) && GunRules.BulletsToKill(GuardHealthRules.BaseHitsToKill, pewHits) == 4 && !GuardHealthRules.IsDown(3 * pewHits, GuardHealthRules.BaseHitsToKill) && GuardHealthRules.IsDown(4 * pewHits, GuardHealthRules.BaseHitsToKill));
'
Rep '            float mixed = pewHits; ' '            float mixed = RangedWeapon.Specs[(int)RangedWeapon.Kind.Rifle].guardHits; '
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevGuardsTest.cs')
RepRegion '            Check("bullets: the pew needs 3 bullets for an unshielded guard' 'B(pewK, ShieldTier.None, 30f) == 8);' @'
            Check("bullets: the pew needs 4 bullets for an unshielded guard, 6 through a light shield, 8 through a medium one, 14 through a heavy one (light ammo, 25 HP a bullet: the owner, 2026-10-27)",
                B(pewK, ShieldTier.None) == 4 && B(pewK, ShieldTier.Light) == 6 && B(pewK, ShieldTier.Medium) == 8 && B(pewK, ShieldTier.Heavy) == 14,
                $"({B(pewK, ShieldTier.None)}, {B(pewK, ShieldTier.Light)}, {B(pewK, ShieldTier.Medium)}, {B(pewK, ShieldTier.Heavy)})");
            Check("bullets: the sniper rifle needs 1 for an unshielded guard, 2 through a light or a medium shield, 4 through a heavy one; the rifle 3 / 5 (medium); the SMG 4 / 6 / 8 / 14",
                B(sniperK, ShieldTier.None) == 1 && B(sniperK, ShieldTier.Light) == 2 && B(sniperK, ShieldTier.Medium) == 2 && B(sniperK, ShieldTier.Heavy) == 4
                && B(rifleK, ShieldTier.None) == 3 && B(rifleK, ShieldTier.Medium) == 5
                && B(smgK, ShieldTier.None) == 4 && B(smgK, ShieldTier.Light) == 6 && B(smgK, ShieldTier.Medium) == 8 && B(smgK, ShieldTier.Heavy) == 14);
            Check("bullets: a shotgun shot is 75 HP: two shots knock out an unshielded guard and five through a heavy shield; the boss (30) takes 12 pew bullets",
                B(shotgunK, ShieldTier.None) == 2 && B(shotgunK, ShieldTier.Heavy) == 5 && B(pewK, ShieldTier.None, 30f) == 12);
'@
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevAsks1023Test.cs')
RepRx 'Check\("vendor cards \(T10\): the pistol says Damage: 40.*\r?\n' '                Check("vendor cards (T10): the pistol says Damage: 25 (light ammo, the owner, 2026-10-27) and Stun Time: 0s (a bullet does not stun)", WeaponCardRules.DamageLine(WeaponCardRules.GunDamage(pew.guardHits)) == "Damage: 25" && WeaponCardRules.StunLine(pew.stun) == "Stun Time: 0s");
'
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevAsks1023B.cs')
Rep 'Vector3 KillWithRoll(GuardAI g, int roll, int hitsPerBullet)' 'Vector3 KillWithRoll(GuardAI g, int roll, float hitsPerBullet)'
Rep '            int pewHits = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].guardHits;' '            float pewHits = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].guardHits;'
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevCombat.cs')
Rep '            int pewBullet = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].guardHits;' '            float pewBullet = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].guardHits;'
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevR7.cs')
RepRx 'int bullet = RangedWeapon\.Specs\[0\]\.guardHits;' 'float bullet = RangedWeapon.Specs[0].guardHits;' 2
RepRegion '                for (int i = 0; i < 3; i++) victim.BulletHitHost(bullet, false, null);' 'R7Check("the fourth one does (four bullets where an unshielded guard takes three)", victim.Dead, $"(hits {victim.Hits})");' @'
                for (int i = 0; i < 5; i++) victim.BulletHitHost(bullet, false, null);
                R7Check("five pew bullets do not put a light-shield guard down (five of 1.875 hits each, nine) and do not knock him over", !victim.Dead && victim.Hits == 9 && victim.State != GuardState.Stunned, $"(hits {victim.Hits}, dead {victim.Dead}, state {victim.State})");
                victim.BulletHitHost(bullet, false, null);
                R7Check("the sixth one does (six bullets where an unshielded guard takes four)", victim.Dead, $"(hits {victim.Hits})");
'@
Rep '                for (int i = 0; i < 3; i++) bare[0].BulletHitHost(bullet, false, null);' '                for (int i = 0; i < 4; i++) bare[0].BulletHitHost(bullet, false, null);'
Rep 'R7Check("an unshielded guard takes three pew bullets", bare[0].Dead,' 'R7Check("an unshielded guard takes four pew bullets", bare[0].Dead,'
Save-Src
"combat tests patched"
