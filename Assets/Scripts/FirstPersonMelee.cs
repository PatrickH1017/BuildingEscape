using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A melee weapon in the hand in first person (the owner, 2026-10-20: the weapon must be visible in the hand). The animation lets the arm hang, so a bat, a crowbar or an axe is out of the picture below the eyes. For the body the camera is looking
    /// from, the right arm is raised to the lower right of the screen with the weapon pointing up and forward - the way a first-person shooter holds a melee weapon; a swing or a punch (the animation's) has the arm back while it lasts. Third person
    /// and every other body are as they were.
    /// </summary>
    public sealed class FirstPersonMelee : MonoBehaviour
    {
        const float Forward = 0.42f, Right = 0.20f, Down = 0.21f;                // where the hand is, from the camera (m)
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
            float want = share * (anim && anim.Slashing ? 0f : 1f);
            weight = Mathf.MoveTowards(weight, want, Time.deltaTime * 7f);
            if (weight < 0.001f || !r) return;
            var cam = r.transform;
            var target = cam.position + cam.forward * Forward + cam.right * Right - cam.up * Down;
            HeldGun.SolveArm(upper, fore, hand, target, -cam.up * 0.8f + cam.right * 0.6f, weight);
            var along = (cam.forward * 0.8f + cam.up * 0.6f - cam.right * 0.15f).normalized;           // (up and forward, a little in)
            hand.rotation = Quaternion.Slerp(hand.rotation, Quaternion.FromToRotation(weapon.up, along) * hand.rotation, weight);
        }
    }
}
