using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape
{
    /// <summary>
    /// Gives a guard capsule one of the mean Party Characters looks (PartyCharacterFactory.GuardLooks)
    /// and animates it: runs while walking / investigating / chasing, stands dazed (head down, arms hanging) while stunned (the owner, 2026-10-20; it used to fall down).
    /// A guard who has been killed plays the asset's death clip instead and stays down (the owner, 2026-10-21): a dead guard is also marked Stunned in GuardAI (so the AI leaves him alone), so
    /// the dazed loop is for a living Stunned guard only (a blow, a punch or a thrown thing), never a bullet and never a corpse.
    /// </summary>
    [RequireComponent(typeof(GuardAI))]
    public class GuardVisual : MonoBehaviour
    {
        [Tooltip("Which entry of PartyCharacterFactory.GuardLooks to use (wraps around).")]
        public int lookIndex;

        GuardAI ai;
        NavMeshAgent agent;
        CharacterAnimator anim;
        CapsuleCollider hitbox;
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
            hitbox = GetComponent<CapsuleCollider>();
            FitHitbox();
            // flashlight in the hand + the visible cone of vision
            var vision = GetComponent<GuardVision>();
            if (!vision) vision = gameObject.AddComponent<GuardVision>();
            vision.Init(anim);
            donut = GuardDonut.Attach(anim);                   // (hidden until he stops for one: GuardAI.Eating)
            ApplyLoadout();                                    // the shield on his chest and the gun in his hand: what the host decided (GuardAI.Loadout)
        }

        /// <summary>
        /// The guard's hitbox is the greybox capsule (2 m tall, 1 m wide) but his model is built to his look (<see cref="CharacterLook.height"/> goes up to 1.25, and a hat or a hard hat sits on top): the Brute's head is 0.3 m above the
        /// capsule, so a bullet or a blow aimed at a tall guard's head went over it (QA 2026-10-21, "the attack does not connect even though it definitely should"). The capsule grows UPWARD to the top of what is drawn: it
        /// never shrinks, its bottom stays on the floor, its width is unchanged and it grows by 0.7 m at most.
        /// </summary>
        void FitHitbox()
        {
            if (!hitbox || !anim) return;
            var box = hitbox.bounds;
            if (box.size.sqrMagnitude < 1e-4f) return;                     // (no world box yet: leave the capsule as it is)
            float top = float.MinValue;
            foreach (var r in anim.GetComponentsInChildren<Renderer>(false))
                if (r.enabled) top = Mathf.Max(top, r.bounds.max.y);
            if (top <= float.MinValue) return;
            float extra = top - box.max.y;                                  // (world metres of the model above the capsule)
            if (extra < 0.02f) return;
            float grow = Mathf.Min(extra, 0.7f) / Mathf.Max(0.01f, transform.lossyScale.y);           // (in the capsule's own units: the boss is built 1.35 times bigger)
            hitbox.height += grow;
            hitbox.center += new Vector3(0f, grow * 0.5f, 0f);
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
            if (ai.Dead)
            {
                // (killed: the asset's death clip, played once and held on its last frame; GuardAI also marks a dead guard Stunned, which used to play the dazed loop)
                // He lies on the floor now, so his standing hitbox goes: it would stop bullets and blows meant for somebody behind him and block the way as an invisible pillar (QA 2026-10-21).
                if (hitbox && hitbox.enabled) hitbox.enabled = false;
                anim.Fall();
                return;
            }
            if (ai.State == GuardState.Stunned) { anim.Stun(); return; }
            anim.Recover();
            float v = ai.CurrentSpeed; // a puppet has no agent running
            anim.Locomote(v > 0.15f, Mathf.Clamp(v / 3f, 0.5f, 1.8f));
        }
    }
}
