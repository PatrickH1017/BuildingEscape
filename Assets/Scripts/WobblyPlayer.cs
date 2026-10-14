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
        public float stumbleSeconds = 1.6f;
        public float jumpImpulse = 5.5f;

        public bool IsStumbling { get; private set; }
        public bool IsHidden { get; set; }
        public bool IsSprinting { get; private set; }

        Rigidbody rb;
        float stumbleUntil, nextStep;
        Vector3 moveInput;
        bool grounded;
        Transform cam;
        CameraFollow rig;

        void Awake() { rb = GetComponent<Rigidbody>(); rb.maxAngularVelocity = 12f; cam = Camera.main ? Camera.main.transform : null; }

        void Update()
        {
            var raw = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
            if (!rig) rig = FindFirstObjectByType<CameraFollow>();
            float yaw = rig ? rig.AimYaw : (cam ? cam.eulerAngles.y : 0f);
            var basis = Quaternion.Euler(0, yaw, 0);
            moveInput = basis * raw.normalized;
            IsSprinting = Input.GetKey(KeyCode.LeftShift) && moveInput.sqrMagnitude > 0.01f;
            if (Input.GetKeyDown(KeyCode.Space) && grounded && !IsStumbling)
                rb.AddForce(Vector3.up * jumpImpulse, ForceMode.VelocityChange);
            if (IsStumbling && Time.time > stumbleUntil) IsStumbling = false;
        }

        void FixedUpdate()
        {
            grounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.35f);
            if (!IsStumbling)
            {
                float f = moveForce * (IsSprinting ? sprintMultiplier : 1f);
                var flat = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                if (flat.magnitude < maxSpeed * (IsSprinting ? sprintMultiplier : 1f))
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

        void KeepUpright()
        {
            var axis = Vector3.Cross(transform.up, Vector3.up);
            rb.AddTorque(axis * uprightStrength - rb.angularVelocity * uprightDamping, ForceMode.Acceleration);
        }

        void Footsteps()
        {
            if (!grounded || moveInput.sqrMagnitude < 0.01f || IsHidden || Time.time < nextStep) return;
            nextStep = Time.time + (IsSprinting ? 0.28f : 0.5f);
            NoiseBus.Emit(transform.position, IsSprinting ? sprintNoise : footstepNoise, gameObject);
        }

        /// <summary>Knock the player over; physics takes over until the timer runs out.</summary>
        public void Stumble(Vector3 impulse, float? seconds = null)
        {
            IsStumbling = true;
            stumbleUntil = Time.time + (seconds ?? stumbleSeconds);
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
