using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A melee weapon in the hand in first person (the owner, 2026-10-20: the weapon must be visible in the hand). The animation lets the arm hang, so a bat, a crowbar or an axe is out of the picture below the eyes. For the body the camera is looking
    /// from, the right arm is raised to the lower right of the screen with the weapon pointing up and forward - the way a first-person shooter holds a melee weapon; a swing or a punch (the animation's) has the arm back while it lasts. Third person
    /// and every other body are as they were.
    /// The weapons' Blender swings (2026-10-27, <see cref="WeaponAnimation"/>) end their blow low, at the height of a guard's belly, under the picture: in first person the arm is turned up at the shoulder for as long as the hand would be
    /// below the bottom of the view, so that the blow and the follow-through are seen at the bottom of the screen (the wind-up over the head stays out of the picture, as it would).
    /// </summary>
    public sealed class FirstPersonMelee : MonoBehaviour
    {
        const float Forward = 0.42f, Right = 0.20f, Down = 0.21f;                // where the hand is, from the camera (m)
        /// <summary>A Blender swing in first person: the lowest the hand goes, in degrees under the line of sight (the view reaches about 30 degrees down).</summary>
        const float SwingLowest = 22f;
        Transform upper, fore, hand, weapon;
        CharacterAnimator anim;
        float weight;

        /// <summary>The weapon (the <c>X_Held</c> model, a child of the hand) that is in the right hand now.</summary>
        public void Hold(Transform handBone, Transform heldModel, CharacterAnimator animator)
        {
            hand = handBone; fore = hand ? hand.parent : null; upper = fore ? fore.parent : null; weapon = heldModel; anim = animator;
        }

        void LateUpdate()
        {
            if (!weapon || !upper || !fore || !hand) return;
            var r = CameraFollow.Current;
            float share = 0f;
            if (r && r.ViewedBody && transform.IsChildOf(r.ViewedBody)) share = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.95f, r.Zoom));
            bool swing = anim && anim.Slashing;
            float want = share * (swing ? 0f : 1f) * (1f - (anim ? anim.MeleeReadyShare : 0f));          // (the ready of the owner's melee holds is the clip's own first-person picture: the fist and the handle at the right edge)
            weight = Mathf.MoveTowards(weight, want, Time.deltaTime * 7f);
            if (!r) return;
            var cam = r.transform;
            if (weight >= 0.001f)
            {
                var target = cam.position + cam.forward * Forward + cam.right * Right - cam.up * Down;
                HeldGun.SolveArm(upper, fore, hand, target, -cam.up * 0.8f + cam.right * 0.6f, weight);
                var along = (cam.forward * 0.8f + cam.up * 0.6f - cam.right * 0.15f).normalized;           // (up and forward, a little in)
                hand.rotation = Quaternion.Slerp(hand.rotation, Quaternion.FromToRotation(weapon.up, along) * hand.rotation, weight);
            }
            if (swing && share > 0.001f && anim.MeleeClipsNow != null && anim.MeleeWeight > 0.5f) KeepSwingInView(cam, share);
        }

        /// <summary>The hand of a Blender swing no lower than <see cref="SwingLowest"/> under the line of sight: the whole arm (and the weapon in its hand) is turned up at the shoulder.</summary>
        void KeepSwingInView(Transform cam, float share)
        {
            var local = cam.InverseTransformPoint(hand.position);
            if (local.z < 0.05f) return;                                                   // (behind the eyes: the wind-up)
            float lowest = -Mathf.Tan(SwingLowest * Mathf.Deg2Rad) * local.z;
            if (local.y >= lowest) return;
            var target = cam.TransformPoint(new Vector3(local.x, lowest, local.z));
            var turn = Quaternion.FromToRotation(hand.position - upper.position, target - upper.position);
            upper.rotation = Quaternion.Slerp(Quaternion.identity, turn, share) * upper.rotation;
        }
    }
}
