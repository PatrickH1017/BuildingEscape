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
            anim = PartyCharacterFactory.Attach(gameObject, looks[Mathf.Abs(lookIndex) % looks.Length]);
        }

        void Update()
        {
            if (!anim) return;
            if (ai.State == GuardState.Stunned) { anim.Fall(); return; }
            anim.Recover();
            float v = agent.velocity.magnitude;
            anim.Locomote(v > 0.15f, Mathf.Clamp(v / 3f, 0.5f, 1.8f));
        }
    }
}
