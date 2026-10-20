using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape
{
    /// <summary>
    /// Gives a guard capsule one of the mean Party Characters looks (PartyCharacterFactory.GuardLooks)
    /// and animates it: runs while walking / investigating / chasing, falls down while stunned.
    /// </summary>
    [RequireComponent(typeof(GuardAI))]
    public class GuardVisual : MonoBehaviour
    {
        [Tooltip("Which entry of PartyCharacterFactory.GuardLooks to use (wraps around).")]
        public int lookIndex;

        GuardAI ai;
        NavMeshAgent agent;
        CharacterAnimator anim;
        Transform donut;
        float nextMunch;

        /// <summary>The guard's animation driver (his model), or null while there is none.</summary>
        public CharacterAnimator Anim => anim;

        void Start()
        {
            if (!GetComponent<FootstepPlayer>()) gameObject.AddComponent<FootstepPlayer>();
            if (!GetComponent<GuardSounds>()) gameObject.AddComponent<GuardSounds>();   // keys on his belt, walkie-talkie static
            if (!GetComponent<GuardBubble>()) gameObject.AddComponent<GuardBubble>();   // the occasional comic-book curse
            ai = GetComponent<GuardAI>();
            agent = GetComponent<NavMeshAgent>();
            var looks = PartyCharacterFactory.GuardLooks;
            var look = looks[Mathf.Abs(lookIndex) % looks.Length];
            // Guards are the Tiny Toon police officers when that pack is set up; the look's build (height / width) is
            // kept so they still come in different sizes. Without the pack they stay the mean Party characters.
            var catalog = CharacterCatalog.Instance;
            var officers = catalog ? catalog.WithRole(ToonRole.Guard) : null;
            if (officers != null && officers.Count > 0) look = look.WithModel(officers[Mathf.Abs(lookIndex) % officers.Count].id); // several officer designs (full pack): one per look
            anim = PartyCharacterFactory.Attach(gameObject, look);
            // flashlight in the hand + the visible cone of vision
            var vision = GetComponent<GuardVision>();
            if (!vision) vision = gameObject.AddComponent<GuardVision>();
            vision.Init(anim);
            var hm = HeistManager.Instance;
            // body armour from floor 4 on (the owner, 2026-10-18): a vest on his chest; the host's rules count a bullet for half the hits (GuardAI.Armoured)
            if (!ai.IsBoss && GuardRules.Armoured(hm ? hm.Floor : 0)) { ai.Armoured = true; GuardArmour.AddVest(anim, transform); }
            donut = GuardDonut.Attach(anim);                   // (hidden until he stops for one: GuardAI.Eating)
            // armed guards (the floor rule, or the test menu): the pew in his hand
            if (!GetComponent<GuardGun>() && ArmedGuards.ShouldArm(hm ? hm.Floor : 0)) gameObject.AddComponent<GuardGun>();
        }

        void Update()
        {
            if (!anim) return;
            bool eat = ai.Eating && !ai.Dead;
            if (donut && donut.gameObject.activeSelf != eat) donut.gameObject.SetActive(eat);
            if (eat && Time.time >= nextMunch)
            {
                nextMunch = Time.time + 1.0f + 0.5f * Random.value;
                GameAudio.PlaySound(SoundId.Munch, transform.position + Vector3.up * 1.5f, 0.6f, Random.Range(0.92f, 1.08f), true, 14f, 2f);
            }
            if (ai.State == GuardState.Stunned) { anim.Fall(); return; }
            anim.Recover();
            float v = ai.CurrentSpeed; // a puppet has no agent running
            anim.Locomote(v > 0.15f, Mathf.Clamp(v / 3f, 0.5f, 1.8f));
        }
    }
}
