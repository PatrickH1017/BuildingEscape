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
        bool inFlight; GameObject thrower;

        public void OnThrown(GameObject who) { inFlight = true; thrower = who; }

        void OnCollisionEnter(Collision c)
        {
            NoiseBus.Emit(c.GetContact(0).point, impactNoise, thrower ? thrower : gameObject);
            if (inFlight && c.collider.GetComponentInParent<GuardAI>() is { } g) g.Stun(stunSeconds, thrower);
            inFlight = false;
        }
    }
}
