using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Physics-driven clumsy worker: force-based movement, a self-righting torque, and
    /// ragdoll-style stumbling (tripping, being shoved or tripped by teammates).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class WobblyPlayer : MonoBehaviour
    {
        public float moveForce = 38f;
        public float sprintMultiplier = 1.6f;
        public float maxSpeed = 5f;
        public float turnSpeed = 8f;
        public float uprightStrength = 30f;
        public float uprightDamping = 4f;
        public float footstepNoise = 3f;
        public float sprintNoise = 8f;
        [Header("Crouching (hold Ctrl or C)")]
        [Tooltip("Share of the walking speed while crouched.")]
        [Range(0.2f, 1f)] public float crouchSpeedMultiplier = 0.55f;
        [Tooltip("Radius, in metres, at which a guard hears a crouched footstep (walking is footstepNoise).")]
        public float crouchNoise = 0.9f;
        [Tooltip("Capsule height while crouched, as a share of the standing height.")]
        [Range(0.4f, 0.9f)] public float crouchHeight = 0.65f;
        public float stumbleSeconds = 1.6f;
        public float jumpImpulse = 5.5f;
        [Tooltip("Dress the capsule as a Party Characters pack character (adds PlayerVisual).")]
        public bool usePartyCharacter = true;

        public bool IsStumbling { get; private set; }
        public bool IsHidden { get; set; }
        public bool IsSprinting { get; private set; }
        /// <summary>Crouched: slower, lower, and a footstep only carries <see cref="crouchNoise"/> metres.</summary>
        public bool IsCrouching { get; private set; }
        /// <summary>The loudness (guard hearing radius) a footstep has right now.</summary>
        public float CurrentFootstepNoise => IsSprinting ? sprintNoise : IsCrouching ? crouchNoise : footstepNoise;
        /// <summary>Set by NetPlayer on remote puppets, whose script is switched off.</summary>
        public void SetPuppetCrouch(bool crouching) => IsCrouching = crouching;
#if UNITY_EDITOR
        // Test hooks (Editor only): drive the movement and crouch without a keyboard.
        [System.NonSerialized] public Vector3? testMove;
        [System.NonSerialized] public bool testSprint, testCrouch;
#endif
        /// <summary>Raised when the player jumps (used to play the jump animation).</summary>
        public event System.Action Jumped;
        /// <summary>Set by NetPlayer on remote puppets (kinematic, so the rigidbody reports no speed).</summary>
        public Vector3? VelocityOverride { get; set; }

        Rigidbody rb;
        float stumbleUntil, nextStep;
        Vector3 moveInput;
        bool grounded;
        Transform cam;
        CameraFollow rig;
        CapsuleCollider capsule;
        float standHeight;
        Vector3 standCenter;
        bool crouchApplied;

        void Awake()
        {
            rb = GetComponent<Rigidbody>(); rb.maxAngularVelocity = 12f; cam = Camera.main ? Camera.main.transform : null;
            capsule = GetComponent<CapsuleCollider>();
            if (capsule) { standHeight = capsule.height; standCenter = capsule.center; }
            if (!GetComponent<Backpack>()) gameObject.AddComponent<Backpack>();
            if (!GetComponent<FootstepPlayer>()) gameObject.AddComponent<FootstepPlayer>();
            if (!GetComponent<MeleeController>()) gameObject.AddComponent<MeleeController>();
            if (usePartyCharacter && !GetComponent<PlayerVisual>()) gameObject.AddComponent<PlayerVisual>();
        }

        void Update()
        {
            var raw = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
            bool sprintKey = Input.GetKey(KeyCode.LeftShift);
            bool crouchKey = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
#if UNITY_EDITOR
            if (testMove.HasValue) raw = testMove.Value;
            sprintKey |= testSprint;
            crouchKey |= testCrouch;
#endif
            if (!rig) rig = FindFirstObjectByType<CameraFollow>();
            float yaw = rig ? rig.AimYaw : (cam ? cam.eulerAngles.y : 0f);
            var basis = Quaternion.Euler(0, yaw, 0);
            moveInput = basis * raw.normalized;
            // crouching: not while knocked over or hidden, and it takes the place of sprinting and jumping
            SetCrouch(crouchKey && !IsStumbling && !IsHidden);
            IsSprinting = sprintKey && !IsCrouching && moveInput.sqrMagnitude > 0.01f;
            if (Input.GetKeyDown(KeyCode.Space) && grounded && !IsStumbling && !IsCrouching)
            {
                rb.AddForce(Vector3.up * jumpImpulse, ForceMode.VelocityChange);
                Jumped?.Invoke();
            }
            if (IsStumbling && Time.time > stumbleUntil) IsStumbling = false;
        }

        void SetCrouch(bool crouching)
        {
            IsCrouching = crouching;
            if (!capsule || crouching == crouchApplied) return;
            crouchApplied = crouching;
            // a lower capsule with the feet where they were
            float h = crouching ? standHeight * crouchHeight : standHeight;
            float bottom = standCenter.y - standHeight * 0.5f;
            capsule.height = h;
            capsule.center = new Vector3(standCenter.x, bottom + h * 0.5f, standCenter.z);
        }

        void FixedUpdate()
        {
            grounded = CheckGrounded();
            if (!IsStumbling)
            {
                float gait = IsSprinting ? sprintMultiplier : IsCrouching ? crouchSpeedMultiplier : 1f;
                float f = moveForce * gait;
                var flat = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                if (flat.magnitude < maxSpeed * gait)
                    rb.AddForce(moveInput * f, ForceMode.Acceleration);
                if (rig)
                {
                    // body follows the aim yaw (stays put while the player free-looks)
                    var face = Quaternion.Euler(0, rig.AimYaw, 0);
                    transform.rotation = Quaternion.Slerp(transform.rotation, face, turnSpeed * Time.fixedDeltaTime);
                }
                else if (moveInput.sqrMagnitude > 0.01f)
                {
                    var target = Quaternion.LookRotation(moveInput);
                    transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.fixedDeltaTime);
                }
                KeepUpright();
                Footsteps();
            }
        }

        readonly RaycastHit[] groundHits = new RaycastHit[8];

        /// <summary>
        /// Is something solid just under the feet? The ray starts at the middle of the capsule and reaches a little
        /// past the feet (the old check only reached 0.25 m below the middle, so it was never true: no jumping, and
        /// no footstep noise for the guards to hear).
        /// </summary>
        bool CheckGrounded()
        {
            float half = capsule ? capsule.height * 0.5f : 1f;
            var origin = capsule ? transform.TransformPoint(capsule.center) : transform.position;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, half + 0.25f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = groundHits[i].collider;
                if (c && c.attachedRigidbody != rb && !c.transform.IsChildOf(transform)) return true;
            }
            return false;
        }

        void KeepUpright()
        {
            var axis = Vector3.Cross(transform.up, Vector3.up);
            rb.AddTorque(axis * uprightStrength - rb.angularVelocity * uprightDamping, ForceMode.Acceleration);
        }

        void Footsteps()
        {
            if (!grounded || moveInput.sqrMagnitude < 0.01f || IsHidden || Time.time < nextStep) return;
            nextStep = Time.time + (IsSprinting ? 0.28f : IsCrouching ? 0.75f : 0.5f);
            NoiseBus.Emit(transform.position, CurrentFootstepNoise, gameObject);
        }

        /// <summary>Knock the player over; physics takes over until the timer runs out.</summary>
        public void Stumble(Vector3 impulse, float? seconds = null)
        {
            var np = GetComponent<Net.NetPlayer>();
            if (np != null && np.IsSpawned && !np.IsOwner)
            {
                // a puppet on this machine: only the owner's machine can move the body
                Net.RunSync.Instance?.BroadcastStumble(np.Slot, impulse, seconds ?? stumbleSeconds);
                return;
            }
            StumbleLocal(impulse, seconds ?? stumbleSeconds);
        }

        /// <summary>Actually knock this player over (their own machine).</summary>
        public void StumbleLocal(Vector3 impulse, float seconds)
        {
            IsStumbling = true;
            stumbleUntil = Time.time + seconds;
            rb.AddForce(impulse, ForceMode.VelocityChange);
            rb.AddTorque(Random.onUnitSphere * 6f, ForceMode.VelocityChange);
            NoiseBus.Emit(transform.position, 10f, gameObject);
        }

        /// <summary>Teammate sabotage: shove/trip another player.</summary>
        public void Shove(WobblyPlayer other)
        {
            if (!other) return;
            var dir = (other.transform.position - transform.position).normalized;
            other.Stumble(dir * 4f + Vector3.up * 2f);
        }

        void OnCollisionEnter(Collision c)
        {
            // Trip on hard impacts (stairs, being hit by flung loot).
            if (!IsStumbling && c.relativeVelocity.magnitude > 9f) Stumble(Vector3.zero, 1.2f);
        }
    }
}
