# Round 12, batch G48e (first person): the left hand wraps the left side of the handguard (up at barrel height, left of it) and is rolled to match, so that it - and the forearm reaching in from the left, like the owner's reference - is in the picture.
. 'C:\bqs\patchlib.ps1'
Open-Src 'C:\bqm\Assets\Scripts\Guns\HeldGun.cs'
Rep '            if (fp > 0.001f) tl += (-right * 0.07f + up * 0.05f) * fp;' '            if (fp > 0.001f) tl += (-right * 0.075f + up * 0.085f) * fp;'
Rep '            lHand.rotation = Quaternion.Slerp(lHand.rotation, gunRot * Quaternion.Euler(0f, 0f, LeftRoll) * MirrorX(Quaternion.Inverse(Grip.localRotation)), b);' '            lHand.rotation = Quaternion.Slerp(lHand.rotation, gunRot * Quaternion.Euler(0f, 0f, LeftRoll + FpLeftRoll * fp) * MirrorX(Quaternion.Inverse(Grip.localRotation)), b);'
Rep '        const float FpForward = 0.42f, FpRight = 0.075f, FpDown = 0.215f;' '        const float FpForward = 0.42f, FpRight = 0.075f, FpDown = 0.215f, FpLeftRoll = 70f;'
Save-Src
