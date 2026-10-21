# Round 12, batch G49 (first person, the owner: "the weapon is not visible enough in the hand"): a bat, a crowbar or an axe in the hand was out of the picture altogether in first person (the animation lets the arm hang below the eyes, and it was hidden
# with the body before): FirstPersonMelee raises the right arm to the lower right of the screen with the weapon up and forward while the camera is at the eyes; a swing or a punch takes the arm back while it lasts.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor'

Open-Src "$A\Guns\HeldGun.cs"
Rep '        static void SolveArm(Transform up, Transform fo, Transform ha, Vector3 target, Vector3 pole, float weight)' '        public static void SolveArm(Transform up, Transform fo, Transform ha, Vector3 target, Vector3 pole, float weight)'
Save-Src

Open-Src "$A\MeleeController.cs"
Rep '            heldModel = MeleeWeapon.BuildHeld(mw.kind, hand);' @'
            heldModel = MeleeWeapon.BuildHeld(mw.kind, hand);
            var firstPerson = anim.GetComponent<FirstPersonMelee>();
            if (!firstPerson) firstPerson = anim.gameObject.AddComponent<FirstPersonMelee>();
            firstPerson.Hold(hand, heldModel.transform, anim);                      // (in first person the arm is raised so that the weapon is in the picture)
'@
Save-Src

Open-Src "$E\NetDev\NetDevFpLook.cs"
Rep '            ShotCam(Camera.main, "fplook_crowbar");' @'
            ShotCam(Camera.main, "fplook_crowbar");
            var crowbarHeld = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("_Held") && t.name.StartsWith("Crowbar"));
            var crowbarRenderer = crowbarHeld ? crowbarHeld.GetComponentInChildren<Renderer>() : null;
            var vpCrowbar = crowbarRenderer ? Camera.main.WorldToViewportPoint(crowbarRenderer.bounds.center) : Vector3.zero;
            FpLookCheck("crowbar: the weapon in the hand is in the picture in first person - raised to the lower right, up and forward (the owner, 2026-10-20: it was out of the picture below the eyes)", crowbarRenderer != null && crowbarRenderer.enabled && crowbarRenderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly && vpCrowbar.z > 0.3f && vpCrowbar.x > 0.5f && vpCrowbar.x < 0.97f && vpCrowbar.y > 0.08f && vpCrowbar.y < 0.7f, $"(weapon at {vpCrowbar:F2})");
'@
Save-Src
Write-Output 'round 12 batch G49 patched'
