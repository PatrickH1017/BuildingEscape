# Round 12, batch G47d (first person): what the character wears on the head (the cap's brim, goggles, a beard: the children of the Head bone) is never part of the arms - the brim stood a hand's width from the shouldered hands and was drawn as a
# brown ribbon across the picture; it is hidden whole now. And the shouldered gun a little further out (the hand 0.42 m in front of the eyes, 0.17 below the line of sight), so that more of the forearms is in the picture.
. 'C:\bqs\patchlib.ps1'
Open-Src 'C:\bqm\Assets\Scripts\CameraFollow.cs'
Rep '        Transform armRU, armRE, armRH, armLU, armLE, armLH;              // the right and the left upper arm, elbow and wrist' '        Transform armRU, armRE, armRH, armLU, armLE, armLH, armHead;     // the right and the left upper arm, elbow and wrist, and the head (what is worn on it is never part of the arms)'
Rep '            armLU = an.GetBoneTransform(HumanBodyBones.LeftUpperArm); armLE = an.GetBoneTransform(HumanBodyBones.LeftLowerArm); armLH = an.GetBoneTransform(HumanBodyBones.LeftHand);' @"
            armLU = an.GetBoneTransform(HumanBodyBones.LeftUpperArm); armLE = an.GetBoneTransform(HumanBodyBones.LeftLowerArm); armLH = an.GetBoneTransform(HumanBodyBones.LeftHand);
            armHead = an.GetBoneTransform(HumanBodyBones.Head);
"@
Rep '                if (arms && IsSkin(r)) { armsOriginal[r] = r.sharedMaterials; r.sharedMaterials = ArmMaterialsFor(r.sharedMaterials); }' '                if (arms && IsSkin(r) && !(armHead && r.transform.IsChildOf(armHead))) { armsOriginal[r] = r.sharedMaterials; r.sharedMaterials = ArmMaterialsFor(r.sharedMaterials); }          // (a cap, goggles, a beard hang on the head: hidden whole)'
Save-Src
Open-Src 'C:\bqm\Assets\Scripts\Guns\HeldGun.cs'
Rep 'const float FpForward = 0.34f, FpRight = 0.075f, FpDown = 0.15f;' 'const float FpForward = 0.42f, FpRight = 0.075f, FpDown = 0.17f;'
Save-Src
