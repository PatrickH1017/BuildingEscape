# Round 12, batch G48b (first person, the owner: "the weapon is not visible enough in the hand"): the gun a little more to the right of the line of sight (a three-quarter view of its left side), the left hand on the side of the handguard so that its
# forearm is seen reaching in from the left (the owner's reference); and GunController: a gun item that is DESTROYED while it is equipped (a sale, a test) left its model in the hand, because a destroyed object compares equal to "no gun".
. 'C:\bqs\patchlib.ps1'
$A = 'C:\bqm\Assets\Scripts'
Open-Src "$A\Guns\HeldGun.cs"
Rep 'const float FpForward = 0.34f, FpRight = 0.03f, FpDown = 0.15f;' 'const float FpForward = 0.34f, FpRight = 0.075f, FpDown = 0.15f;'
Rep '            var tl = pr + toForegrip;' @"
            var tl = pr + toForegrip;
            if (fp > 0.001f) tl += (-right * 0.07f + up * 0.05f) * fp;                               // (first person: the left hand grips the side of the handguard, so that its forearm is seen reaching in from the left)
"@
Save-Src
Open-Src "$A\Guns\GunController.cs"
Rep '            if (wanted != gun && !ChangeTo(wanted)) return;' '            if (!ReferenceEquals(wanted, gun) && !ChangeTo(wanted)) return;          // (a gun that was destroyed while it was in the hand compares equal to "none" with ==: its model must still go)'
Save-Src
