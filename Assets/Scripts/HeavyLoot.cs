using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A valuable object that needs several players to lift and loses value (or shatters)
    /// when it hits things too hard.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class HeavyLoot : MonoBehaviour
    {
        public string displayName = "Golden Stapler";
        public int baseValue = 1000;
        [Min(1)] public int lifters = 2;
        public float massPerLifter = 6f;
        [Tooltip("Impact speed (m/s) that starts causing damage.")]
        public float fragility = 4f;
        [Tooltip("Damage points before it shatters.")]
        public float maxIntegrity = 100f;
        public GameObject shatterPrefab;

        public float Integrity { get; private set; }
        public bool Broken { get; private set; }
        public int CurrentValue => Broken ? 0 : Mathf.RoundToInt(baseValue * Mathf.Clamp01(Integrity / maxIntegrity));
        public int Holders => holders.Count;
        public bool IsLiftable => holders.Count >= lifters;

        readonly System.Collections.Generic.HashSet<PlayerGrab> holders = new();
        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = lifters * massPerLifter;
            Integrity = maxIntegrity;
            if (!GetComponent<Pickup>()) gameObject.AddComponent<Pickup>().weight = lifters * 6f;
        }

        public void AddHolder(PlayerGrab g) { holders.Add(g); }
        public void RemoveHolder(PlayerGrab g) { holders.Remove(g); }

        void OnCollisionEnter(Collision c)
        {
            if (Broken) return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < fragility) return;
            float dmg = (speed - fragility) * 12f;
            Integrity -= dmg;
            NoiseBus.Emit(c.GetContact(0).point, Mathf.Clamp(speed * 2.5f, 4f, 25f), gameObject);
            if (Integrity <= 0f) Shatter();
        }

        void Shatter()
        {
            Broken = true;
            NoiseBus.Emit(transform.position, 30f, gameObject);
            if (shatterPrefab) Instantiate(shatterPrefab, transform.position, transform.rotation);
            HeistManager.Instance?.NotifyLootBroken(this);
            Destroy(gameObject);
        }
    }
}
