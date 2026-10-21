# Round 12, batch G48 (the owner, 2026-10-20: first person with a weapon, "reference image for how it should look more like attached"): the arms and the body are sorted out (G47); now the GUN is where a first-person shooter has it.
# Carried at the chest (the third-person hold) the gun is half a metre below the eyes, and only the tip of its muzzle shows at the bottom edge of the picture. In first person the arms now shoulder it: the grip a forearm's length out in front of the eyes,
# a little to the right and below the line of sight, the barrel along it - so the sight (the new red dot) stands in the middle of the screen and the gun fills the lower middle, with the forearms reaching in from both sides. The same for a pistol, held out in one hand.
# It is a blend of the zoom (third to first person), so nothing pops. Other bodies (other players, the guards) keep the hold they had. The camera tells the gun: CameraFollow.Current / ViewedBody.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor'

Open-Src "$A\CameraFollow.cs"
Rep '        void OnEnable() { Application.onBeforeRender += PushArms; }' @'
        /// <summary>The camera of this machine (the guns ask where it is: in first person they are held in front of its eyes) and the body it is looking from.</summary>
        public static CameraFollow Current { get; private set; }
        public Transform ViewedBody => spectate ? spectate : target;

        void OnEnable() { Application.onBeforeRender += PushArms; Current = this; }
'@
Rep '        void OnDisable() { Application.onBeforeRender -= PushArms; }' '        void OnDisable() { Application.onBeforeRender -= PushArms; if (Current == this) Current = null; }'
Save-Src

Open-Src "$A\Guns\HeldGun.cs"
Rep '        public float LeftRoll;' @'
        public float LeftRoll;

        // first person (the owner, 2026-10-20): the gun is shouldered, not carried at the chest - the grip a forearm's length in front of the eyes, a little right of the line of sight and below it, the barrel along it (metres from the camera)
        const float FpForward = 0.34f, FpRight = 0.03f, FpDown = 0.15f;

        /// <summary>How far the camera is at this body's eyes: 0 in third person (and for every other body) up to 1 in first person (a blend of the zoom, so the hold does not pop), and the camera.</summary>
        float FirstPersonShare(out Transform cam)
        {
            cam = null;
            var r = CameraFollow.Current;
            if (!r || !r.ViewedBody || !transform.IsChildOf(r.ViewedBody)) return 0f;
            cam = r.transform;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.95f, r.Zoom));
        }
'@
Rep '            if (twoHand) { TwoHandPose(aim, b); return; }' @'
            if (twoHand) { TwoHandPose(aim, b); return; }
            float fp1 = FirstPersonShare(out var cam1);
            if (fp1 > 0.001f) { OneHandShouldered(aim, b, fp1, cam1); return; }
'@
Rep '            var pr = sR + f * (fitFwd * L) - up * (0.28f * L) - right * (half * (1f - fitLat));' @'
            var pr = sR + f * (fitFwd * L) - up * (0.28f * L) - right * (half * (1f - fitLat));
            float fp = FirstPersonShare(out var viewCam);
            if (fp > 0.001f) pr = Vector3.Lerp(pr, viewCam.position + f * FpForward + right * FpRight - up * FpDown, fp);          // (first person: shouldered, the sight in the middle of the screen)
'@
Rep '        // ------------------------------------------------------------------ a gun in both hands' @'
        /// <summary>A pistol in first person: held out in the right hand in front of the eyes, a little low, the barrel along the line of sight.</summary>
        void OneHandShouldered(Vector3 aim, float b, float fp, Transform cam)
        {
            var right = Vector3.Cross(Vector3.up, aim);
            if (right.sqrMagnitude < 1e-4f) right = transform.right;
            right.Normalize();
            var up = Vector3.Cross(aim, right);
            var gunRot = Quaternion.LookRotation(aim, up);
            var carried = upper.position + aim * (0.9f * armLength);                                  // (the third-person hold: the arm straight out along the aim)
            var held = cam.position + aim * (FpForward * 1.2f) + right * (FpRight + 0.04f) - up * (FpDown - 0.02f);
            SolveArm(upper, fore, hand, Vector3.Lerp(carried, held, fp), -up * 0.8f + right * 0.6f, b);
            hand.rotation = Quaternion.Slerp(hand.rotation, gunRot * Quaternion.Inverse(Grip.localRotation), b);
        }

        // ------------------------------------------------------------------ a gun in both hands
'@
Save-Src

# the first-person scenario checks where the gun and its sight are in the picture
Open-Src "$E\NetDev\NetDevFpLook.cs"
Rep '    public partial class NetDevRunner
    {
        IEnumerator FpLookScenario()' @'
    public partial class NetDevRunner
    {
        int fpLookFails;

        void FpLookCheck(string name, bool ok, string detail = "")
        {
            if (!ok) fpLookFails++;
            Log($"[FpLook] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator FpLookScenario()
'@
Rep '                ShotCam(Camera.main, "fplook_" + kind);' @'
                ShotCam(Camera.main, "fplook_" + kind);
                var camNow = Camera.main;
                var gcn = player.GetComponent<GunController>();
                var hg = gcn ? gcn.Held : null;
                var vpMuzzle = hg ? camNow.WorldToViewportPoint(hg.Muzzle.position) : Vector3.zero;
                FpLookCheck($"{kind}: the gun is in the picture - its muzzle is on the screen, in the lower half and not at the very edge, shouldered in front of the eyes (the owner, 2026-10-20)", hg != null && vpMuzzle.z > 0.3f && vpMuzzle.x > 0.25f && vpMuzzle.x < 0.8f && vpMuzzle.y > 0.12f && vpMuzzle.y < 0.62f, $"(muzzle at {vpMuzzle:F2})");
                if (RangedWeapon.TakesRedDot(RangedWeapon.Specs[(int)kind].weaponClass))
                {
                    var dotT = hg ? hg.Grip.Find("RedDot/Dot") : null;
                    var vpDot = dotT ? camNow.WorldToViewportPoint(dotT.position) : Vector3.zero;
                    FpLookCheck($"{kind}: the red dot sight is near the middle of the screen, a little low and right of it, the way a shouldered gun's is", dotT != null && vpDot.z > 0.3f && vpDot.x > 0.42f && vpDot.x < 0.68f && vpDot.y > 0.30f && vpDot.y < 0.60f, $"(dot at {vpDot:F2})");
                }
                var bodyBits = player.GetComponentsInChildren<Renderer>(true).Where(r => r && r.enabled && r.gameObject.activeInHierarchy && !CameraFollow.IsHeldModel(r.transform, player.transform)).ToList();
                FpLookCheck($"{kind}: nothing of the body but its arms is drawn", rig.ArmsShown && bodyBits.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly || r.sharedMaterials.All(m => m && m.shader && m.shader.name == "BuildingEscape/FirstPersonArms")), $"({bodyBits.Count} parts)");
'@
Rep '            Log("[FpLook] DONE");
            Quit(0);' @'
            Log($"[FpLook] DONE fails={fpLookFails}");
            Quit(fpLookFails == 0 ? 0 : 1);
'@
Save-Src
Write-Output 'round 12 batch G48 patched'
