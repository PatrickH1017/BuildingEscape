using System.Linq;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The dock worker's silenced pistol animation, made in Blender (the owner, 2026-10-27: "attach the blender animation for the silenced pistol for dock workers. I want all available character you pick to be able to use this
    /// animation. This way when we are looking down in fps it looks more realistic as well as in third person"). Humanoid clips in <c>Resources/Animations</c> (set up by Editor/WeaponAnimationSetup, so every character a player
    /// can pick plays them): the pistol raised in the right hand, aimed, fired, and the magazine change. <see cref="CharacterAnimator.GunPose"/> plays them on the upper body over the walk and run, <see cref="HeldGun"/> holds
    /// the pistol where the Blender files hold it (<see cref="PlaceInHand"/>) and turns the arm to the crosshair.
    /// </summary>
    public static class PistolAnimation
    {
        public const string Folder = "Animations";
        public const string OneHandFile = "Pistol_OneHand", ReloadFile = "Pistol_Reload";
        /// <summary>The clips: from the gun at the side to the aim (12 frames at 30 a second), the aim, one shot (the kick peaks two frames in, the gun is back on the aim at the end) and the 2.2 s reload (starts and ends on the aim).</summary>
        public const string Raise = "Pistol_Raise", Aim = "Pistol_Aim", Fire = "Pistol_Fire", Reload = "Pistol_Reload";

        /// <summary>A player's pistol is held and fired with the Blender animation (false: the arm is turned by code alone, as before 2026-10-27).</summary>
        public static bool Enabled = true;

        static WeaponAnimation.GunClips clips;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { clips = null; loaded = false; aimMask = null; reloadMask = null; }

        /// <summary>The clips, or null when they are not in the build (then the pistol is held the old way).</summary>
        public static WeaponAnimation.GunClips Load()
        {
            if (loaded) return clips;
            loaded = true;
            var one = Resources.LoadAll<AnimationClip>(Folder + "/" + OneHandFile);
            var rel = Resources.LoadAll<AnimationClip>(Folder + "/" + ReloadFile);
            var c = new WeaponAnimation.GunClips
            {
                name = "Pistol", raise = one.FirstOrDefault(a => a.name == Raise), aim = one.FirstOrDefault(a => a.name == Aim), fire = one.FirstOrDefault(a => a.name == Fire),
                reload = rel.FirstOrDefault(a => a.name == Reload),
            };
            if (c.Valid && c.reload) clips = c;
            else Debug.LogWarning("[PistolAnimation] the pistol clips are missing from Resources/" + Folder + ": the pistol is held the old way");
            return clips;
        }

        static AvatarMask aimMask, reloadMask;

        /// <summary>The upper body and the right arm and hand: what the raise, the aim and the shot move (the legs walk and the left arm swings as the clips under them have it).</summary>
        public static AvatarMask AimMask => aimMask ? aimMask : (aimMask = Mask(false));
        /// <summary>The same and the left arm and hand: the reload is done with both hands.</summary>
        public static AvatarMask ReloadMask => reloadMask ? reloadMask : (reloadMask = Mask(true));

        static AvatarMask Mask(bool leftArm)
        {
            var m = new AvatarMask { name = leftArm ? "PistolReload" : "PistolAim" };
            for (var p = AvatarMaskBodyPart.Root; p < AvatarMaskBodyPart.LastBodyPart; p++)
                m.SetHumanoidBodyPartActive(p, p == AvatarMaskBodyPart.Body || p == AvatarMaskBodyPart.Head || p == AvatarMaskBodyPart.RightArm || p == AvatarMaskBodyPart.RightFingers
                    || (leftArm && (p == AvatarMaskBodyPart.LeftArm || p == AvatarMaskBodyPart.LeftFingers)));
            return m;
        }

        // ---- where the pistol is in the hand (the Blender files: the gun is placed from the aim pose and rides on the hand bone)
        // The hand's own frame, as the Blender scripts measure it: the wrist (the hand bone), f from the wrist to the middle of the index and middle finger roots, u across the knuckles from the middle finger's root to the
        // index's (the thumb side), and the side the palm faces. The pistol's model has its origin this far along f, u and the palm (in units of the character's size, see SizeOf), its barrel along f and its top along u.
        public static readonly Vector3 GunOrigin = new Vector3(0.08557f, 0.00475f, 0.02f);
        /// <summary>The arm (shoulder to wrist) of the character the clips were made on, in the same units: a character's size is its own arm over this.</summary>
        public const float ArmAtSizeOne = 0.611f;

        /// <summary>A hand's frame of a humanoid rig (the right hand unless <paramref name="left"/>): false when the rig has no index or middle finger.</summary>
        public static bool HandFrame(Animator anim, out Vector3 wrist, out Vector3 f, out Vector3 u, out Vector3 palm, bool left = false)
        {
            wrist = f = u = palm = Vector3.zero;
            if (!anim || !anim.isHuman) return false;
            var hand = anim.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var index = anim.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var middle = anim.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            if (!hand || !index || !middle) return false;
            wrist = hand.position;
            f = ((index.position + middle.position) * 0.5f - wrist).normalized;
            u = Vector3.ProjectOnPlane(index.position - middle.position, f).normalized;
            palm = left ? Vector3.Cross(u, f) : Vector3.Cross(f, u);          // (Unity's cross product is left-handed: a right hand pointing forward with the thumb up has its palm to its left, a left hand to its right)
            return f.sqrMagnitude > 0.5f && u.sqrMagnitude > 0.5f;
        }

        /// <summary>
        /// Puts the gun's grip transform (<paramref name="grip"/>, a child of the right hand: its +Z the barrel, its +Y the top) where the Blender files hold the pistol: its model (the child <paramref name="model"/>, the pack's pistol, which
        /// runs along its own +Z) with its origin at <see cref="GunOrigin"/> in the hand's frame and the barrel along the hand. <paramref name="size"/> is the character's size (<see cref="ArmAtSizeOne"/>). False when the rig has no fingers to measure.
        /// </summary>
        public static bool PlaceInHand(Animator anim, Transform grip, Transform model, float size)
        {
            if (!grip || !model || !HandFrame(anim, out var wrist, out var f, out var u, out var palm)) return false;
            var rot = Quaternion.LookRotation(f, u);
            var origin = wrist + (f * GunOrigin.x + u * GunOrigin.y + palm * GunOrigin.z) * size;
            grip.rotation = rot * Quaternion.Inverse(model.localRotation);
            grip.position = origin - grip.rotation * Vector3.Scale(model.localPosition, grip.lossyScale);
            return true;
        }
    }
}
