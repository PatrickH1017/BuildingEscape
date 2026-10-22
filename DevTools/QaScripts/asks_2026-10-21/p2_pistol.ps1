# 2026-10-21, the owner's list: "the first person mode for the pistol has the character's hands somewhat visible and blocking the weapon model a bit, please make the hands not visible for the pistol similar to the rifle"
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

Open-Src "$Tree\Assets\Scripts\Guns\HeldGun.cs"
if (-not $s.Contains('ShowPistolArm')) {
Rep '        public static int LocalArmMode { get; private set; }' @'
        public static int LocalArmMode { get; private set; }

        /// <summary>
        /// Is the right arm drawn in first person when a pistol is held out in one hand? No since 2026-10-21 (the owner: "the first person mode for the pistol has the character's hands somewhat visible and blocking the weapon model a bit, please make
        /// the hands not visible for the pistol similar to the rifle"): the gun alone is in the picture, as with every other gun (the arm still holds it, out of sight, and casts its shadow). Set it to true to bring the arm back.
        /// </summary>
        public static bool ShowPistolArm = false;
'@
Rep '            if (viewCamera) { LocalDotShare = sightIsDot ? ads : 0f; LocalArmMode = !twoHand && fpShare > 0.5f ? 1 : 0; }' '            if (viewCamera) { LocalDotShare = sightIsDot ? ads : 0f; LocalArmMode = ShowPistolArm && !twoHand && fpShare > 0.5f ? 1 : 0; }'
Save-Src
}

Open-Src "$Tree\Assets\Scripts\CameraFollow.cs"
if (-not $s.Contains('HeldGun.ShowPistolArm')) {
Rep '        /// nothing of the body is drawn, only what is in the hands; except the right arm that holds a pistol out (<see cref="HeldGun.LocalArmMode"/>). Set it to true to bring both arms back.' '        /// nothing of the body is drawn, only what is in the hands (and not the pistol''s arm either since 2026-10-21: <see cref="HeldGun.ShowPistolArm"/>, <see cref="HeldGun.LocalArmMode"/>). Set it to true to bring both arms back.'
Save-Src
}

Open-Src "$Tree\Assets\Editor\NetDev\NetDevFpLook.cs"
if (-not $s.Contains('ShowPistolArm')) {
Rep 'rig.ArmsShown == (CameraFollow.ShowArms || kind == RangedWeapon.Kind.Pew) &&' 'rig.ArmsShown == (CameraFollow.ShowArms || (HeldGun.ShowPistolArm && kind == RangedWeapon.Kind.Pew)) &&'
Rep '                FpLookCheck($"{kind}: nothing of the body but its arms is drawn"' @'
                if (kind == RangedWeapon.Kind.Pew)
                    FpLookCheck("Pew: no hands in the picture - the pistol is seen alone like every other gun, no arm is drawn (the owner, 2026-10-21)", !HeldGun.ShowPistolArm && HeldGun.LocalArmMode == 0 && !rig.ArmsShown && bodyBits.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly), $"(arm mode {HeldGun.LocalArmMode}, arms shown {rig.ArmsShown}, {bodyBits.Count} parts)");
                FpLookCheck($"{kind}: nothing of the body but its arms is drawn"
'@
Save-Src
}
"p2 done"
