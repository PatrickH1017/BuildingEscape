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

        void Start()
        {
            if (!GetComponent<FootstepPlayer>()) gameObject.AddComponent<FootstepPlayer>();
            ai = GetComponent<GuardAI>();
            agent = GetComponent<NavMeshAgent>();
            var looks = PartyCharacterFactory.GuardLooks;
            var look = looks[Mathf.Abs(lookIndex) % looks.Length];
            // Guards are the Tiny Toon police officers when that pack is set up; the look's build (height / width) is
            // kept so they still come in different sizes. Without the pack they stay the mean Party characters.
            var catalog = CharacterCatalog.Instance;
            var officer = catalog ? catalog.FirstWithRole(ToonRole.Guard) : null;
            if (officer != null) look = look.WithModel(officer.id);
            anim = PartyCharacterFactory.Attach(gameObject, look);
            // flashlight in the hand + the visible cone of vision
            var vision = GetComponent<GuardVision>();
            if (!vision) vision = gameObject.AddComponent<GuardVision>();
            vision.Init(anim);
        }

        void Update()
        {
            if (!anim) return;
            if (ai.State == GuardState.Stunned) { anim.Fall(); return; }
            anim.Recover();
            float v = ai.CurrentSpeed; // a puppet has no agent running
            anim.Locomote(v > 0.15f, Mathf.Clamp(v / 3f, 0.5f, 1.8f));
        }
    }
}
