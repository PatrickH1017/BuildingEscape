# The owner's task list of 2026-10-27, part 2: hip fire and aiming in first and third person ("a weapon should be at hip-fire unless a player is aiming which is the right mouse button"). The gun's raise (0 low .. 1 up at the sights, eased by HeldGun) is AimRules.HipRaise at the hip and 1 while aiming:
# the animated pistol's raise clip is shown at that point, a long gun's arm blends half way, and the first person sight alignment / the scope (which need the gun fully up) come on only while aiming.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Dry) { Write-Output 'dry ok would copy AimRules.cs, NetDevBulletDamage.cs, NetDevAimHip.cs' } else {
  Copy-Item (Join-Path $here 'combat2\AimRules.cs') ($A + 'Scripts\Guns\AimRules.cs') -Force
  Copy-Item (Join-Path $here 'combat2\NetDevBulletDamage.cs') ($A + 'Editor\NetDev\NetDevBulletDamage.cs') -Force
  Copy-Item (Join-Path $here 'combat2\NetDevAimHip.cs') ($A + 'Editor\NetDev\NetDevAimHip.cs') -Force
  Write-Output 'copied AimRules.cs, NetDevBulletDamage.cs, NetDevAimHip.cs'
}

Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void PropScan()' @'
        public static void AimHip()
        {
            SessionState.SetString("netdev.mode", "aimhip");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PropScan()
'@
Rep ' && mode != "bulletdamage" && ' ' && mode != "bulletdamage" && mode != "aimhip" && '
Rep '                case "bulletdamage": yield return BulletDamageScenario(); break;' @'
                case "bulletdamage": yield return BulletDamageScenario(); break;
                case "aimhip": yield return AimHipScenario(); break;
'@
Save-Src

Open-Src ($A + 'Scripts\Guns\GunController.cs')
Rep '            held.AimDirection = aim.normalized;
            held.Raise = 1f;' @'
            held.AimDirection = aim.normalized;
            held.Raise = Aiming ? 1f : AimRules.HipRaise;                       // (a teammate's gun at his hip or at his sights: the owner, 2026-10-27)
'@
Rep '                held.SlideLocked = mag.Rounds <= 0;
                held.AimDirection = dir.normalized;
                held.Raise = 1f;' @'
                held.SlideLocked = mag.Rounds <= 0;
                held.AimDirection = dir.normalized;
                held.Raise = mag.Reloading || Aiming ? 1f : AimRules.HipRaise;          // (the hip: the raise clip stops half way; the sights: all the way up; the owner, 2026-10-27)
'@
Rep '                held.ReloadProgress = 0f;
                held.AimDirection = dir.normalized;
                held.Raise = 1f;
            }
        }' @'
                held.ReloadProgress = 0f;
                held.AimDirection = dir.normalized;
                held.Raise = Aiming ? 1f : AimRules.HipRaise;                           // (hip fire unless the right mouse button is held: the owner, 2026-10-27)
            }
        }
'@
Save-Src
"combat aim patched"
