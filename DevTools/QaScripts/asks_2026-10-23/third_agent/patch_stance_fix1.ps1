# GunStance run 1 (s1): 139 of 157 passed. The left hand came off the gun by 2-7 cm while running (the left arm is nearly straight on a foregrip, and the turn of the gun to the crosshair - larger while running, the body wobbles - can put the foregrip
# beyond the arm's reach): the gun is moved towards the left shoulder by what the left arm lacks (the right arm has the slack), for the long guns and for the pistol held in both hands.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Scripts\Guns\HeldGun.cs')
Rep '            var rRot = q * hand.rotation;' @'
            var rRot = q * hand.rotation;
            if (stances)
            {
                // the left arm is nearly straight on a foregrip: turning the gun about the shoulders can put the foregrip beyond its reach (running, the body wobbles and the turn is larger): the gun is moved towards the left shoulder by what the arm lacks (the right arm has the slack)
                var lPred = pivot + q * (lHand.position - pivot);
                float lReach = (lFore.position - lUpper.position).magnitude + (lHand.position - lFore.position).magnitude - 0.004f;
                var toL = lPred - lUpper.position;
                float lack = toL.magnitude - lReach;
                if (lack > 0f) rTarget -= toL.normalized * Mathf.Min(lack, 0.12f);
            }
'@
Rep '                var lTarget = Grip.TransformPoint(leftOnGun);
                SolveArm(lUpper, lFore, lHand, lTarget, lFore.position - (lUpper.position + lHand.position) * 0.5f, w);' @'
                var lTarget = Grip.TransformPoint(leftOnGun);
                float lArm = (lFore.position - lUpper.position).magnitude + (lHand.position - lFore.position).magnitude - 0.004f;
                var toLeft = lTarget - lUpper.position;
                if (toLeft.magnitude - lArm > 0.002f)
                {
                    // the left arm cannot reach the gun where it is turned to now (running): the gun is brought towards the left shoulder by what the arm lacks, by the right arm, which has the slack
                    var keepRot = hand.rotation;
                    SolveArm(upper, fore, hand, hand.position - toLeft.normalized * Mathf.Min(toLeft.magnitude - lArm, 0.12f), fore.position - (upper.position + hand.position) * 0.5f, w);
                    hand.rotation = keepRot;
                    lTarget = Grip.TransformPoint(leftOnGun);
                }
                SolveArm(lUpper, lFore, lHand, lTarget, lFore.position - (lUpper.position + lHand.position) * 0.5f, w);
'@
Save-Src
"stance fix 1 patched"
