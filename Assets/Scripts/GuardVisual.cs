using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape
{
    /// <summary>
    /// Gives a guard capsule one of the mean Party Characters looks (PartyCharacterFactory.GuardLooks)
    /// and animates it: runs while walking / investigating / chasing, stands dazed (head down, arms hanging) while stunned (the owner, 2026-10-20; it used to fall down).
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
            donut = GuardDonut.Attach(anim);                   // (hidden until he stops for one: GuardAI.Eating)
            ApplyLoadout();                                    // the shield on his chest and the gun in his hand: what the host decided (GuardAI.Loadout)
        }

        GuardLoadout applied = GuardLoadout.Unarmed;
        Transform vest;

        /// <summary>Puts on him what his loadout says (the owner, 2026-10-19: guards by floor with a gun and a light / medium / heavy shield or none): the vest of his tier and, when he is armed, the gun of his kind. A puppet gets its loadout from the host's snapshot.</summary>
        void ApplyLoadout()
        {
            var l = ai.Loadout;
            applied = l;
            if (vest) { Destroy(vest.gameObject); vest = null; }
            if (!ai.IsBoss && l.Shield != ShieldTier.None) vest = GuardArmour.AddVest(anim, transform, l.Shield);
            ai.SyncGun();
        }

        void Update()
        {
            if (!anim) return;
            if (ai.Loadout != applied) ApplyLoadout();         // (the host told a puppet what he carries, or the test menu armed him)
            bool eat = ai.Eating && !ai.Dead;
            if (donut && donut.gameObject.activeSelf != eat) donut.gameObject.SetActive(eat);
            if (eat && Time.time >= nextMunch)
            {
                nextMunch = Time.time + 1.0f + 0.5f * Random.value;
                GameAudio.PlaySound(SoundId.Munch, transform.position + Vector3.up * 1.5f, 0.6f, Random.Range(0.92f, 1.08f), true, 14f, 2f);
            }
            if (ai.State == GuardState.Stunned) { anim.Stun(); return; }
            anim.Recover();
            float v = ai.CurrentSpeed; // a puppet has no agent running
            anim.Locomote(v > 0.15f, Mathf.Clamp(v / 3f, 0.5f, 1.8f));
        }
    }
}
