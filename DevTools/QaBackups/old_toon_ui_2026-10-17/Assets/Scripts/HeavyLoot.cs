using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A valuable object that needs several players to lift and loses value (or shatters)
    /// when it hits things too hard.
    ///
    /// The owner, 2026-10-17: "Carrying loot together was glitchy ... Also at times the piece being carried by two players disappeared". What was wrong
    /// and what is done about it: (1) the piece was damaged by its own carriers - it bumped the people holding it and whatever the pair walked into, at
    /// speeds of their own making, and a few knocks shattered it without a word. Now a thing that is held (or was set down a moment ago) takes no damage,
    /// a person bumping it never hurts it, one knock never does more than <see cref="MaxHit"/>, and when it does shatter everybody is told and sees it
    /// burst (<see cref="LootShards"/>). (2) It fought its carriers: it is not solid to the people who hold it while they hold it (and a moment after),
    /// it turns slowly, and it cannot get faster than <see cref="MaxCarrySpeed"/>. (3) It got stuck or lost: if the carriers get far away from a stuck piece it
    /// is brought to them, and a piece that falls out of the world is put back where it last stood safe.
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

        /// <summary>The most one knock can take off (of 100): a whole piece survives any single bump.</summary>
        public const float MaxHit = 45f;
        /// <summary>Seconds after it was set down in which it takes no damage (the landing of a thing that is let go of).</summary>
        public const float SetDownGrace = 0.75f;
        /// <summary>Carried, it is never faster than this (m/s): a carried piece cannot be whipped through a wall.</summary>
        public const float MaxCarrySpeed = 9f;
        /// <summary>Further than this (m) from where its carriers hold it for <see cref="StuckSeconds"/> and it is brought to them; further than <see cref="LostDistance"/> at once.</summary>
        public const float StuckDistance = 3.2f, LostDistance = 8f, StuckSeconds = 0.8f;
        /// <summary>A piece lower than this (m) has fallen out of the world.</summary>
        public const float OutOfWorldY = -5f;

        public float Integrity { get; private set; }
        public bool Broken { get; private set; }
        public int CurrentValue => Broken ? 0 : Mathf.RoundToInt(baseValue * Mathf.Clamp01(Integrity / maxIntegrity));
        public int Holders => holders.Count;
        public bool IsLiftable => holders.Count >= lifters;

        readonly HashSet<PlayerGrab> holders = new();
        Rigidbody rb;
        Collider[] own;
        float graceUntil, stuckFor, nextWarn, nextSafe, normalAngularDamping;
        Vector3 lastSafe;
        // players it was held by: not solid to them until they are clear of it again
        readonly List<PlayerGrab> ignoring = new();
        float nextRestoreCheck;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = lifters * massPerLifter;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;      // (a thin wall cannot be tunnelled by a carried piece)
            normalAngularDamping = rb.angularDamping;
            own = GetComponentsInChildren<Collider>();
            Integrity = maxIntegrity;
            lastSafe = transform.position + Vector3.up * 0.05f;
            if (!GetComponent<Pickup>()) gameObject.AddComponent<Pickup>().weight = lifters * 6f;
        }

        public void AddHolder(PlayerGrab g)
        {
            if (!holders.Add(g)) return;
            graceUntil = 0f;
            if (!Net.NetRole.Simulates) return;
            rb.angularDamping = 6f;                      // a carried piece turns slowly, it does not spin between its carriers
            SetIgnoring(g, true);
            if (!ignoring.Contains(g)) ignoring.Add(g);
        }

        public void RemoveHolder(PlayerGrab g, bool thrown = false)
        {
            if (!holders.Remove(g)) return;
            if (holders.Count == 0)
            {
                stuckFor = 0f;
                graceUntil = thrown ? 0f : Time.time + SetDownGrace;
                if (rb) rb.angularDamping = normalAngularDamping;
            }
        }

        /// <summary>Makes the piece solid or not solid to a person (the one holding it).</summary>
        void SetIgnoring(PlayerGrab g, bool ignore)
        {
            if (!g) return;
            var theirs = g.GetComponentsInChildren<Collider>();
            for (int i = 0; i < own.Length; i++)
                for (int k = 0; k < theirs.Length; k++)
                    if (own[i] && theirs[k]) Physics.IgnoreCollision(own[i], theirs[k], ignore);
        }

        void FixedUpdate()
        {
            if (Broken || !Net.NetRole.Simulates) return;
            var p = rb.position;

            // fell out of the world (through a floor, off the edge of a floor): back where it last stood
            if (p.y < OutOfWorldY || float.IsNaN(p.x + p.y + p.z))
            {
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                rb.position = lastSafe; transform.position = lastSafe;
                Debug.LogWarning($"[Loot] {displayName} fell out of the world at y={p.y:0}; put back at {lastSafe}");
                HeistManager.Instance?.Say($"{displayName.ToUpper()} fell out of the world - put back where it last stood");
                return;
            }
            if (Time.time >= nextSafe && p.y > -0.2f && rb.linearVelocity.sqrMagnitude < 4f && holders.Count == 0)
            {
                lastSafe = p + Vector3.up * 0.05f;
                nextSafe = Time.time + 1f;
            }

            if (holders.Count > 0)
            {
                var v = rb.linearVelocity;
                if (v.sqrMagnitude > MaxCarrySpeed * MaxCarrySpeed) rb.linearVelocity = v.normalized * MaxCarrySpeed;
                if (IsLiftable)
                {
                    // stuck on something while the carriers walk on: bring it to them
                    var mean = Vector3.zero; int n = 0;
                    foreach (var g in holders) if (g) { mean += g.HandPoint; n++; }
                    if (n > 0)
                    {
                        mean /= n;
                        float d = Vector3.Distance(p, mean);
                        stuckFor = d > StuckDistance ? stuckFor + Time.fixedDeltaTime : Mathf.Max(0f, stuckFor - Time.fixedDeltaTime);
                        if (d > LostDistance || stuckFor > StuckSeconds)
                        {
                            stuckFor = 0f;
                            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                            rb.position = mean; transform.position = mean;
                        }
                    }
                }
            }

            // let go of: solid to the people who held it again, once nobody overlaps it
            if (ignoring.Count > 0 && holders.Count == 0 && Time.time >= nextRestoreCheck)
            {
                nextRestoreCheck = Time.time + 0.25f;
                for (int i = ignoring.Count - 1; i >= 0; i--)
                {
                    var g = ignoring[i];
                    if (!g) { ignoring.RemoveAt(i); continue; }
                    if (holders.Contains(g)) continue;
                    if (Overlaps(g)) continue;
                    SetIgnoring(g, false);
                    ignoring.RemoveAt(i);
                }
            }
        }

        bool Overlaps(PlayerGrab g)
        {
            var theirs = g.GetComponentsInChildren<Collider>();
            for (int i = 0; i < own.Length; i++)
                for (int k = 0; k < theirs.Length; k++)
                    if (own[i] && theirs[k] && own[i].bounds.Intersects(theirs[k].bounds)) return true;
            return false;
        }

        void OnCollisionEnter(Collision c)
        {
            if (Broken || Net.NetRole.IsRemoteClient) return; // the host runs the physics consequences
            if (holders.Count > 0 || Time.time < graceUntil) return;                 // carried (or just set down): the carriers' own doing is not a knock
            if (c.collider.GetComponentInParent<WobblyPlayer>() != null) return;     // a person bumping it never hurts it
            // how hard it hit, not how fast it slid: only the speed INTO the surface counts (a piece skidding along the floor, or touching down on it again, is not a knock)
            float speed = Mathf.Abs(Vector3.Dot(c.relativeVelocity, c.GetContact(0).normal));
            if (speed < fragility) return;
            float dmg = Mathf.Min(MaxHit, (speed - fragility) * 12f);
            Integrity -= dmg;
            NoiseBus.Emit(c.GetContact(0).point, Mathf.Clamp(speed * 2.5f, 4f, 25f), gameObject);
            if (Integrity <= 0f) { Shatter(); return; }
            if (dmg >= 10f && Time.time >= nextWarn)
            {
                nextWarn = Time.time + 2f;
                HeistManager.Instance?.Say($"Careful! {displayName} took a knock (-${Mathf.RoundToInt(baseValue * dmg / maxIntegrity)})");
            }
        }

        void Shatter()
        {
            Broken = true;
            NoiseBus.Emit(transform.position, 30f, gameObject);
            if (TryGetComponent(out Pickup pk)) Net.RunSync.Instance?.BroadcastBroken(pk.NetId);
            Burst();
            HeistManager.Instance?.NotifyLootBroken(this);
            HeistManager.Instance?.Say($"{displayName.ToUpper()} SHATTERED - ${baseValue} lost");      // (everybody is told: a piece that goes without a word looks like a bug)
            Destroy(gameObject);
        }

        /// <summary>Client: the host says this one broke.</summary>
        public void NetShatter()
        {
            Broken = true;
            Burst();
            Destroy(gameObject);
        }

        void Burst()
        {
            if (shatterPrefab) { Instantiate(shatterPrefab, transform.position, transform.rotation); return; }
            var r = GetComponentInChildren<Renderer>();
            Color colour = r && r.sharedMaterial && r.sharedMaterial.HasProperty("_Color") ? r.sharedMaterial.color : new Color(0.9f, 0.75f, 0.3f);
            LootShards.Burst(transform.position, colour, Mathf.Clamp(own != null && own.Length > 0 && own[0] ? own[0].bounds.size.magnitude : 0.8f, 0.4f, 2f));
        }
    }
}
