using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Coffee mug etc. Distracts / stuns guards on hit and makes noise.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Throwable : MonoBehaviour
    {
        public float throwMultiplier = 1f;
        public float stunSeconds = 2.5f;
        public float impactNoise = 14f;
        [Tooltip("Softer bumps (resting, being set down, riding the elevator) make no noise.")]
        public float minImpactSpeed = 2f;
        bool inFlight; GameObject thrower;

        void Awake()
        {
            if (!GetComponent<Pickup>()) { var p = gameObject.AddComponent<Pickup>(); p.weight = Mathf.Max(0.2f, GetComponent<Rigidbody>().mass); p.bankValue = 25; }
        }

        public void OnThrown(GameObject who) { inFlight = true; thrower = who; }

        void OnCollisionEnter(Collision c)
        {
            if (c.relativeVelocity.magnitude >= minImpactSpeed)
                NoiseBus.Emit(c.GetContact(0).point, impactNoise, thrower ? thrower : gameObject);
            if (inFlight && stunSeconds > 0f && c.collider.GetComponentInParent<GuardAI>() is { } g) g.Stun(stunSeconds, thrower);
            inFlight = false;
        }
    }
}
