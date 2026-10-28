using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Gives the player capsule its Party Characters look and animates it. Added automatically by
    /// WobblyPlayer (untick "Use Party Character" there to keep the greybox capsule).
    /// Each teammate gets a different look, in the order they joined.
    /// </summary>
    [RequireComponent(typeof(WobblyPlayer))]
    public class PlayerVisual : MonoBehaviour
    {
        [Tooltip("Which entry of PartyCharacterFactory.PlayerLooks to use. -1 = by player order.")]
        public int lookIndex = -1;
        /// <summary>Set before Start (or call Apply) to use a specific look, e.g. a teammate's chosen one.</summary>
        public CharacterLook lookOverride;

        bool started;
        WobblyPlayer player;
        Rigidbody rb;
        CharacterAnimator anim;

        void Start()
        {
            player = GetComponent<WobblyPlayer>();
            rb = GetComponent<Rigidbody>();
            int i = lookIndex >= 0 ? lookIndex : Mathf.Max(0, WobblyPlayerRegistry.All.IndexOf(player));
            CharacterLook look;
            if (lookOverride != null) look = lookOverride;
            else if (!GetComponent<Net.NetPlayer>() && PlayerAppearance.TryLoadSaved(out var saved)) look = saved.ToLook();
            else look = PlayerAppearance.ForSlot(i).ToLook();       // (a Tiny Toon person by slot until the player chooses)
            anim = PartyCharacterFactory.Attach(gameObject, look);
            player.Jumped += OnJumped;
            started = true;
        }

        /// <summary>Re-dress as another look (a networked teammate's customization arriving).</summary>
        public void Apply(CharacterLook look)
        {
            lookOverride = look;
            if (!started) return; // Start will use it
            if (anim) Destroy(anim.gameObject);
            anim = PartyCharacterFactory.Attach(gameObject, look);
        }

        public CharacterAnimator Anim => anim;

        void OnDestroy() { if (player) player.Jumped -= OnJumped; }

        void OnJumped() { if (anim) anim.Jump(); }

        // crouching squashes the model down towards its feet (the rigs have no crouch clip)
        CharacterAnimator scaledFor;
        Vector3 baseScale;
        float crouchBlend;

        void Update()
        {
            if (!anim) return;
            if (scaledFor != anim) { scaledFor = anim; baseScale = anim.transform.localScale; crouchBlend = 0f; }
            float crouchTarget = player.IsCrouching ? 1f : 0f;
            if (crouchBlend != crouchTarget)       // (the scale is only written while it is changing: a write dirties the whole model's transforms)
            {
                crouchBlend = Mathf.MoveTowards(crouchBlend, crouchTarget, Time.deltaTime * 5f);
                float squash = Mathf.Lerp(1f, player.crouchHeight, crouchBlend);
                float widen = Mathf.Lerp(1f, 1.06f, crouchBlend);
                anim.transform.localScale = new Vector3(baseScale.x * widen, baseScale.y * squash, baseScale.z * widen);
            }

            if (player.IsStumbling || player.InCustody) { anim.Fall(); return; }
            anim.Recover();
            var v = player.VelocityOverride ?? rb.linearVelocity; v.y = 0f;
            anim.Locomote(v.magnitude > 0.5f, Mathf.Clamp(v.magnitude / 3.5f, 0.6f, 1.6f), v.magnitude);
        }
    }
}
