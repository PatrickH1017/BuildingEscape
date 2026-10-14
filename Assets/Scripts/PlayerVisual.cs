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

        WobblyPlayer player;
        Rigidbody rb;
        CharacterAnimator anim;

        void Start()
        {
            player = GetComponent<WobblyPlayer>();
            rb = GetComponent<Rigidbody>();
            int i = lookIndex >= 0 ? lookIndex : Mathf.Max(0, WobblyPlayerRegistry.All.IndexOf(player));
            var looks = PartyCharacterFactory.PlayerLooks;
            anim = PartyCharacterFactory.Attach(gameObject, looks[i % looks.Length]);
            player.Jumped += OnJumped;
        }

        public CharacterAnimator Anim => anim;

        void OnDestroy() { if (player) player.Jumped -= OnJumped; }

        void OnJumped() { if (anim) anim.Jump(); }

        void Update()
        {
            if (!anim) return;
            if (player.IsStumbling) { anim.Fall(); return; }
            anim.Recover();
            var v = rb.linearVelocity; v.y = 0f;
            anim.Locomote(v.magnitude > 0.5f, Mathf.Clamp(v.magnitude / 3.5f, 0.6f, 1.6f));
        }
    }
}
