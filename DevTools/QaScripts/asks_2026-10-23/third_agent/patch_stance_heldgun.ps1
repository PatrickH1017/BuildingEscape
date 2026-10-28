# The stance animations (the owner, 2026-10-27), the HeldGun's half: with the stance loops a gun is held the way the loop has it (the hip, the sights; standing, running) and only turned to the crosshair; the loops have no shot, so the kick is made here
# (the gun's own KickDegrees, as the code's hold always had it); the pistol is held in both hands now, so its left hand is kept on the gun as the right arm turns. Everything the stances do not touch (the officers' pistol, a gun without the
# four loops, the first person sights) is as it was.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Scripts\Guns\HeldGun.cs')

Rep '        bool animated;' @'
        bool animated;
        bool stances, faceKnown;             // the gun is held with the stance loops (2026-10-27: the barrel is the loop's own, the kick is made here); the head's face direction is known (the stances read the barrel from the hand as it is)
'@

Rep '            raiseSeconds = clips.raise.length;' @'
            raiseSeconds = clips.raise.length;
            stances = clips.Stances;
            if (stances && Kind == RangedWeapon.Kind.Pew) { FindLeftArm(anim); twoHand = false; }          // (the stances hold the pistol in both hands: the left arm follows the gun; for the rest of this class it is still a one-hand gun)
'@

Rep 'if (animated) { b *= fpShare; if (b < 0.001f) return; }' 'if (animated) { b = (stances ? AimRules.AimShare(raiseNow) : b) * fpShare; if (b < 0.001f) return; }'

# ---------------------------------------------------------------- the pistol (one right arm turned to the aim; with the stances the left hand is kept on the gun)
RepRegion '        void AnimatedLateUpdate()' '            if (fp > 0.001f) StiffShoulder(cam, fp * w);' @'
        void AnimatedLateUpdate()
        {
            AlignerUsed = false;
            PutShouldersBack();                                                         // (the first-person shoulders of the frame before: nothing else puts them back)
            float fp = FirstPersonShare(out var cam);
            fpShareNow = fp;
            float layer = character ? character.GunWeight : 0f;
            float reloadW = character ? character.GunReloadWeight : 0f;
            bool both = stances && lHand && lFore && lUpper;                            // (the stances hold the pistol in both hands: the left hand stays on the gun)
            if (cam) { LocalDotShare = 0f; LocalScope = 0f; LocalArmMode = fp > 0.5f ? (reloadW > 0.01f || both ? 2 : 1) : 0; }          // (first person: the arm that holds the pistol out, the left one too while it changes the magazine or holds the gun)
            SlideAndMagazine();
            float kickNow = kick;
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 9f);
            var leftOnGun = both ? Grip.InverseTransformPoint(lHand.position) : Vector3.zero;                  // (the left hand's place on the gun as the clips have it, before anything turns)
            var leftRotOnGun = both ? Quaternion.Inverse(Grip.rotation) * lHand.rotation : Quaternion.identity;
            if (fp > 0.001f && reloadW > 0.001f)
            {
                var lift = (Vector3.up * FpReloadLift.x + Quaternion.Euler(0f, cam.eulerAngles.y, 0f) * Vector3.forward * FpReloadLift.y) * (size * fp * reloadW);
                if (clavicle) clavicle.position += lift;
                if (leftClavicle) leftClavicle.position += lift;
            }
            // the aim as the clips have it, whenever it is shown just so (raised, no shot, no reload); the stances have no kick of their own, so the barrel is read from the hand as it is
            if (!stances && raiseNow >= 0.999f && layer > 0.999f && reloadW < 0.001f && Time.time - lastShot >= shotSeconds)
            {
                sUpper = upper.localRotation; sFore = fore.localRotation; sHand = hand.localRotation;
                if (head) faceLocal = Quaternion.Inverse(head.rotation) * transform.forward;
                steadyKnown = true;
            }
            else if (stances && head && raiseNow >= 0.999f && layer > 0.999f && reloadW < 0.001f && character.RunShare < 0.05f)
            {
                faceLocal = Quaternion.Inverse(head.rotation) * transform.forward;
                faceKnown = true;
            }
            float w = (stances ? AimRules.HipShare(raiseNow) : raiseNow) * layer * (1f - reloadW);          // (the reload is shown as it was made: the gun is not aimed meanwhile)
            if (w < 0.001f) { fpShoulderSet = false; return; }
            var aim = AimDirection.sqrMagnitude > 1e-4f ? AimDirection.normalized : transform.forward;
            if (Mathf.Abs(aim.y) > 0.97f) aim = new Vector3(aim.x, Mathf.Sign(aim.y) * 0.97f, aim.z).normalized;
            if (stances && kickNow > 0f)
            {
                var kr = Vector3.Cross(Vector3.up, aim);
                if (kr.sqrMagnitude > 1e-4f) aim = Quaternion.AngleAxis(-kickNow * RangedWeapon.Specs[(int)Kind].KickDegrees, kr.normalized) * aim;          // (the shot's kick: the barrel jumps up and comes back)
            }
            if (chest)
            {
                Quaternion.FromToRotation(SteadyBarrel(), aim).ToAngleAxis(out float deg, out var axis);
                if (deg > 180f) deg -= 360f;
                if (axis.sqrMagnitude > 0.5f) chest.rotation = Quaternion.AngleAxis(Mathf.Clamp(deg * ChestShare, -ChestMaxDegrees, ChestMaxDegrees) * w, axis) * chest.rotation;
            }
            upper.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(SteadyBarrel(), aim), w) * upper.rotation;
            if (head && (stances ? faceKnown : steadyKnown) && fp < 0.5f)
                head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(head.rotation * faceLocal, aim), HeadLook * w * (stances ? AimRules.AimShare(raiseNow) : 1f)) * head.rotation;
            if (fp > 0.001f) StiffShoulder(cam, fp * w);
            if (both)
            {
                var lTarget = Grip.TransformPoint(leftOnGun);
                SolveArm(lUpper, lFore, lHand, lTarget, lFore.position - (lUpper.position + lHand.position) * 0.5f, w);
                lHand.rotation = Quaternion.Slerp(lHand.rotation, Grip.rotation * leftRotOnGun, w);
                LeftHandGap = (lHand.position - lTarget).magnitude;
            }
'@

# ---------------------------------------------------------------- the long guns (both arms turned to the aim with the gun)
RepRegion '        void AnimatedTwoHand()' 'HeadLook * w) * head.rotation;' @'
        void AnimatedTwoHand()
        {
            LongGunParts();
            float layer = character ? character.GunWeight : 0f;
            float fp = FirstPersonShare(out _);
            // first person shows the gun at the size the first-person view was made for (2026-10-20/23; the arms are not drawn there), third person at the file's size, which both hands fit: it grows about the grip as the view goes to the eyes
            Grip.localScale = Vector3.Lerp(gripScaleBlender, gripScaleFitted, fp);
            bool steadyAim = raiseNow >= 0.999f && layer > 0.999f && Time.time - lastShot >= shotSeconds;
            // the barrel as the clip has it: the stance loops have no kick of their own, so it is read from the gun as it is this frame; the raise / aim clips of before: as it is when aimed and still (so that a shot's kick stays in the picture)
            if (chest && (stances || steadyAim)) { steadyBarrelLocal = Quaternion.Inverse(chest.rotation) * Grip.forward; steadyKnown = true; }
            if (chest && head && steadyAim && (!stances || character.RunShare < 0.05f)) { faceLocal = Quaternion.Inverse(head.rotation) * transform.forward; faceKnown = true; }
            float w = (stances ? AimRules.HipShare(raiseNow) : raiseNow) * layer;
            if (w < 0.001f || !chest || !lHand) { LeftHandGap = 0f; return; }
            var aim = AimDirection.sqrMagnitude > 1e-4f ? AimDirection.normalized : transform.forward;
            if (Mathf.Abs(aim.y) > 0.97f) aim = new Vector3(aim.x, Mathf.Sign(aim.y) * 0.97f, aim.z).normalized;
            if (stances && kick > 0f)
            {
                var kr = Vector3.Cross(Vector3.up, aim);
                if (kr.sqrMagnitude > 1e-4f) aim = Quaternion.AngleAxis(-kick * RangedWeapon.Specs[(int)Kind].KickDegrees, kr.normalized) * aim;          // (the shot's kick: the barrel jumps up and comes back)
            }
            Quaternion.FromToRotation(SteadyLongBarrel(), aim).ToAngleAxis(out float deg, out var axis);
            if (deg > 180f) deg -= 360f;
            if (axis.sqrMagnitude > 0.5f) chest.rotation = Quaternion.AngleAxis(Mathf.Clamp(deg * TwoHandChestShare, -TwoHandChestMaxDegrees, TwoHandChestMaxDegrees) * w, axis) * chest.rotation;
            // the left hand's place on the gun as the clip has it, before anything turns
            var leftOnGun = Grip.InverseTransformPoint(lHand.position);
            var leftRotOnGun = Quaternion.Inverse(Grip.rotation) * lHand.rotation;
            var q = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(SteadyLongBarrel(), aim), w);
            var pivot = (upper.position + lUpper.position) * 0.5f;
            var rPole = q * (fore.position - (upper.position + hand.position) * 0.5f);
            var lPole = q * (lFore.position - (lUpper.position + lHand.position) * 0.5f);
            var rTarget = pivot + q * (hand.position - pivot);
            var rRot = q * hand.rotation;
            SolveArm(upper, fore, hand, rTarget, rPole, 1f);
            hand.rotation = rRot;
            var lTarget = Grip.TransformPoint(leftOnGun);
            SolveArm(lUpper, lFore, lHand, lTarget, lPole, 1f);
            lHand.rotation = Grip.rotation * leftRotOnGun;
            LeftHandGap = (lHand.position - lTarget).magnitude;
            if (head && (stances ? faceKnown : steadyKnown) && fp < 0.5f)
                head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(head.rotation * faceLocal, aim), HeadLook * w * (stances ? AimRules.AimShare(raiseNow) : 1f)) * head.rotation;
'@
Save-Src

# ---------------------------------------------------------------- the older animation scenarios look at the clips of before the stances (the officers' clips and the fallback): they hold the stances off
foreach ($f in 'NetDevPistolAnim.cs', 'NetDevWeaponAnim.cs') {
  Open-Src ($A + 'Editor\NetDev\' + $f)
  Rep '            GunController.TestAim = true;              // (the right mouse button held: since 2026-10-27 a gun is at the hip without it, and these scenarios look at the aimed pose)' @'
            GunController.TestAim = true;              // (the right mouse button held: since 2026-10-27 a gun is at the hip without it, and these scenarios look at the aimed pose)
            WeaponAnimation.StancesEnabled = false;    // (these scenarios look at the raise / aim / fire clips: the officers' and the fallback of a gun whose stance loops are missing; the stances have the GunStance scenario)
'@
  Save-Src
}
"stance heldgun patched"
