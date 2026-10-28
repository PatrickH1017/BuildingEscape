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
        public float uprightDamping = 8f;
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
        /// <summary>The body is held in place by something else (the dart game holds the player at the throw line): the keys do not walk, jump, sprint or crouch it.</summary>
        [System.NonSerialized] public bool inputLocked;
        public bool IsHidden { get; set; }

        // ---- custody: free, pinned down by an officer, or busted (see CustodyRules). The host decides (HeistManager),
        // every machine applies it through ApplyCustody, so a teammate sees you lying on the ground too.
        public CustodyState Custody { get; private set; }
        /// <summary>True while pinned or busted: no walking, jumping, hiding, grabbing or swinging.</summary>
        public bool InCustody => Custody != CustodyState.Free;
        /// <summary>Seconds until a pinned player is busted (host-driven; clients show the replicated whole seconds).</summary>
        public float PinLeft { get; private set; }
        /// <summary>The level (0 = docks, 1.. = floors) this player was busted on; sets the vendor's buy-back price.</summary>
        public int BustedOnLevel { get; private set; }
        /// <summary>Host: the officer holding this player down.</summary>
        public GuardAI PinnedBy { get; set; }
        /// <summary>Host: no officer may pin this player before this time (Time.time), set when they are freed.</summary>
        public float PinImmuneUntil { get; set; }
        /// <summary>Raised on every machine when the custody state changes.</summary>
        public event System.Action<CustodyState> CustodyChanged;
        public bool IsSprinting { get; private set; }
        /// <summary>The walking speed is multiplied by this (1 = none): the speed burst (<see cref="PlayerBuffs"/>, the owner, 2026-10-25).</summary>
        public float SpeedBuff { get; set; } = 1f;
        /// <summary>The steps make no sound and no noise for the guards: silent steps (<see cref="PlayerBuffs"/>, the owner, 2026-10-25). Set on every machine, puppets included.</summary>
        public bool StepsSilent { get; set; }
        /// <summary>Crouched: slower, lower, and a footstep only carries <see cref="crouchNoise"/> metres.</summary>
        public bool IsCrouching { get; private set; }
        /// <summary>The gun is up at the sights (the right mouse button; the owner, 2026-10-27): set by the player's own <see cref="GunController"/>, and on a teammate's puppet by <c>NetPlayer</c>.</summary>
        public bool IsAiming { get; set; }
        /// <summary>The loudness (guard hearing radius) a footstep has right now.</summary>
        public float CurrentFootstepNoise => StepsSilent ? 0f : IsSprinting ? sprintNoise : IsCrouching ? crouchNoise : footstepNoise;
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
        /// <summary>The share of his walking speed this player keeps while he drags a body (1 = none; <see cref="DraggableBody"/> sets it to <see cref="DraggableBodyRules.DraggerSpeedShare"/>).</summary>
        public float DragFactor { get; set; } = 1f;
        /// <summary>The player who drags THIS player's body (this player is busted: T12), or null. Every machine knows it (<see cref="PlayerGrab.ApplyDragPlayerBody"/>); the machine that owns this body pulls it along.</summary>
        public WobblyPlayer DraggedBy { get; set; }

        Rigidbody rb;
        float stumbleUntil, nextStep, nextRigLookup;
        Vector3 moveInput;
        bool grounded;
        Transform cam;
        CameraFollow rig;
        CapsuleCollider capsule;
        PlayerGrab grab;
        Stamina stamina;
        float standHeight;
        Vector3 standCenter;
        bool crouchApplied;

        void Awake()
        {
            rb = GetComponent<Rigidbody>(); rb.maxAngularVelocity = 12f; cam = Camera.main ? Camera.main.transform : null;
            rb.maxDepenetrationVelocity = PlayerSafetyRules.MaxDepenetrationSpeed;          // (the friends' playtest, 2026-10-23: officers squeezing a body into a corner threw it up through the ceiling: the push out of an overlap is slow now)
            capsule = GetComponent<CapsuleCollider>();
            TryGetComponent(out grab);
            if (capsule) { standHeight = capsule.height; standCenter = capsule.center; }
            if (!GetComponent<Backpack>()) gameObject.AddComponent<Backpack>();
            if (!GetComponent<FootstepPlayer>()) gameObject.AddComponent<FootstepPlayer>();
            if (!GetComponent<MeleeController>()) gameObject.AddComponent<MeleeController>();
            if (!GetComponent<GunController>()) gameObject.AddComponent<GunController>();
            if (!GetComponent<Stamina>()) gameObject.AddComponent<Stamina>();
            if (!GetComponent<PlayerBuffs>()) gameObject.AddComponent<PlayerBuffs>();                              // (the consumables in use: the owner, 2026-10-25; after the stamina meter, which it hands the drain factor)
            if (!GetComponent<PvpFighter>()) gameObject.AddComponent<PvpFighter>();
            TryGetComponent(out stamina);
            if (usePartyCharacter && !GetComponent<PlayerVisual>()) gameObject.AddComponent<PlayerVisual>();
            if (!GetComponent<PlayerArmourVisual>()) gameObject.AddComponent<PlayerArmourVisual>();          // (the vest of the armour he wears: every machine reads his pack)
            if (!GetComponent<HeldWalkie>()) gameObject.AddComponent<HeldWalkie>();                              // (the walkie talkie he holds out: the same)
        }

        // This script only runs for the body this machine controls (NetPlayer switches it off on every teammate's puppet). The
        // camera follows that body every frame, so it is drawn smoothed between physics steps; a puppet is moved by the network
        // transform instead, and the physics smoothing would fight it.
        void OnEnable() { if (rb) rb.interpolation = RigidbodyInterpolation.Interpolate; }
        void OnDisable() { if (rb) rb.interpolation = RigidbodyInterpolation.None; }

        /// <summary>
        /// Puts the body somewhere else at once. With interpolation on, writing <c>transform.position</c> alone is undone by the next
        /// physics step (the body is drawn back where the rigidbody still is): that is how a player placed in the elevator used to snap
        /// back to where they spawned and fall out of the world. So the rigidbody itself is moved, and its smoothing starts over.
        /// </summary>
        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (!rb) rb = GetComponent<Rigidbody>();
            var mode = rb.interpolation;
            rb.interpolation = RigidbodyInterpolation.None;
            transform.SetPositionAndRotation(position, rotation);
            rb.position = position;
            haveSafeSpot = false;                                                      // (a good spot of the place he was before is no spot to come back to)
            rb.rotation = rotation;
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();
            rb.interpolation = mode;
        }

        /// <summary>Hold the body still (no gravity, no forces) while the floor it will stand on is being built; false lets it go.</summary>
        public void HoldStill(bool hold)
        {
            if (!rb) rb = GetComponent<Rigidbody>();
            if (hold && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = hold;
        }

        void Update()
        {
            var raw = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
            if (OutpostUi.Open) raw = Vector3.zero;       // (the hack is played on W A S D: the body stands still)
            bool sprintKey = Input.GetKey(KeyCode.LeftShift);
            bool crouchKey = !OutpostUi.Open && ((Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.UpArrow) && !Input.GetKey(KeyCode.DownArrow)) || Input.GetKey(KeyCode.C));          // (Ctrl + Up / Down is the view, not a crouch)
#if UNITY_EDITOR
            if (testMove.HasValue) raw = testMove.Value;
            sprintKey |= testSprint;
            crouchKey |= testCrouch;
#endif
            if (inputLocked) { raw = Vector3.zero; sprintKey = false; crouchKey = false; }
            if (!rig && Time.unscaledTime >= nextRigLookup)       // (looked for twice a second at most while there is no camera rig)
            {
                rig = FindFirstObjectByType<CameraFollow>();
                nextRigLookup = Time.unscaledTime + 0.5f;
            }
            float yaw = rig ? rig.AimYaw : (cam ? cam.eulerAngles.y : 0f);
            var basis = Quaternion.Euler(0, yaw, 0);
            moveInput = basis * raw.normalized;
            // crouching: not while knocked over or hidden, and it takes the place of sprinting and jumping
            SetCrouch(crouchKey && !IsStumbling && !IsHidden && !InCustody);
            bool wantsSprint = sprintKey && !IsCrouching && !InCustody && moveInput.sqrMagnitude > 0.01f;
            IsSprinting = wantsSprint && (stamina == null || stamina.CanSprint);            // (winded: no sprint until a quarter of the bar is back)
            if (stamina != null) stamina.Step(Time.deltaTime, IsSprinting);
            if (Input.GetKeyDown(KeyCode.Space) && grounded && !IsStumbling && !IsCrouching && !InCustody && !OutpostUi.Open && !inputLocked)
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

        // ---- the safety net (the friends' playtest, 2026-10-23: "a player was pushed to the roof of the building, out of the map, because too many guards pushed him into a corner"): the body is never left outside the building
        Vector3 safeSpot; bool haveSafeSpot; float nextSafeSpot;
        /// <summary>How many times the net put this body back (tests, the log).</summary>
        public int Rescues { get; private set; }

        void KeepInTheBuilding()
        {
            if (rb.isKinematic) return;
            var p = rb.position;
            if (PlayerSafetyRules.Outside(p.y))
            {
                Vector3 to;
                if (haveSafeSpot) to = safeSpot + Vector3.up * 0.1f;
                else
                {
                    var tower = TowerManager.Instance;
                    to = (tower != null && tower.HasFloor ? tower.CurrentInfo.spawn : new Vector3(p.x, 0f, p.z)) + Vector3.up * 1.1f;
                }
                Rescues++;
                Debug.LogWarning($"[Safety] a body was found outside the building at {p}: put back at {to}");
                TeleportTo(to, transform.rotation);
                safeSpot = to; haveSafeSpot = true; nextSafeSpot = Time.time + PlayerSafetyRules.RememberEvery;
                return;
            }
            var v = rb.linearVelocity;
            if (v.y > PlayerSafetyRules.MaxUpSpeed) { v.y = PlayerSafetyRules.MaxUpSpeed; rb.linearVelocity = v; }          // (a jump is 5.5: nothing but a squeeze goes faster)
            if (Time.time >= nextSafeSpot && PlayerSafetyRules.WorthRemembering(grounded, p.y, v.magnitude)) { safeSpot = p; haveSafeSpot = true; nextSafeSpot = Time.time + PlayerSafetyRules.RememberEvery; }
        }

        void FixedUpdate()
        {
            grounded = CheckGrounded();
            KeepInTheBuilding();
            if (rb.isKinematic) return;                // held still while the floor is built: nothing to push
            if (InCustody) { HoldDown(); FollowDragger(); return; }
            if (!IsStumbling)
            {
                float gait = (IsSprinting ? sprintMultiplier : IsCrouching ? crouchSpeedMultiplier : 1f) * DragFactor * SpeedBuff;
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
                LimitTilt();
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

        /// <summary>A busted body that a teammate drags (T12): it slides to the spot behind him at most <see cref="DraggableBodyRules.FollowSpeed"/> m/s (a velocity, so a wall stops it); only the machine that owns the body runs this.</summary>
        readonly DraggableBodyRules.Trail dragTrail = new DraggableBodyRules.Trail();
        bool dragTrailLive;

        void FollowDragger()
        {
            var by = DraggedBy;
            if (!by) { dragTrailLive = false; return; }
            if (Custody != CustodyState.Busted) { DraggedBy = null; dragTrailLive = false; return; }
            var p = by.transform.position;
            if (!dragTrailLive) { dragTrail.Begin(rb.position.x, rb.position.z, p.x, p.z); dragTrailLive = true; }
            dragTrail.Add(p.x, p.z);
            dragTrail.Advance(Time.deltaTime, out float nx, out float nz);          // (inside FixedUpdate Time.deltaTime is the fixed step)
            rb.MovePosition(new Vector3(nx, rb.position.y, nz));
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        /// <summary>Pinned / busted: the body lies where it fell. No walking, no righting torque; it just settles.</summary>
        void HoldDown()
        {
            var v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(v.x * 0.8f, v.y, v.z * 0.8f);
            rb.angularVelocity *= 0.95f;
            // still upright (e.g. just put into an elevator car): keep tipping until the body lies on the ground
            if (Vector3.Dot(transform.up, Vector3.up) > 0.6f) rb.AddTorque(transform.right * 3f, ForceMode.Acceleration);
        }

        /// <summary>
        /// Take the custody state the host decided (every machine calls this, the host included).
        /// <paramref name="pinLeft"/> is the countdown for a pinned player; <paramref name="bustedLevel"/> the level a busted
        /// player was arrested on.
        /// </summary>
        public void ApplyCustody(CustodyState state, float pinLeft, int bustedLevel)
        {
            PinLeft = pinLeft;
            BustedOnLevel = bustedLevel;
            if (state == Custody) return;
            var was = Custody;
            Custody = state;
            if (state != CustodyState.Free)
            {
                IsHidden = false;
                if (grab) grab.Release(false); // whatever was being carried is dropped
                if (was == CustodyState.Free && rb && !rb.isKinematic)
                {
                    // taken down: tipped over onto the ground (only the machine that owns the body moves it)
                    rb.AddForce(-transform.forward * 1.5f, ForceMode.VelocityChange);
                    rb.AddTorque(transform.right * 5f, ForceMode.VelocityChange);
                    NoiseBus.Emit(transform.position, 8f, gameObject);
                }
            }
            else
            {
                PinnedBy = null;
                if (rb && !rb.isKinematic) rb.AddForce(Vector3.up * 2f, ForceMode.VelocityChange); // gets back up
            }
            CustodyChanged?.Invoke(state);
        }

        /// <summary>Walking sideways (A / D) must not tip the body over: the wobble is kept, but the lean is capped.</summary>
        public float maxTiltDegrees = 9f;
        void LimitTilt()
        {
            float tilt = Vector3.Angle(transform.up, Vector3.up);
            if (tilt <= maxTiltDegrees) return;
            var upright = Quaternion.FromToRotation(transform.up, Vector3.up) * rb.rotation;
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, upright, tilt - maxTiltDegrees));
            rb.angularVelocity *= 0.5f;
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
            float duration = seconds ?? stumbleSeconds;
            if (TryGetComponent(out Net.NetPlayer np) && np.IsSpawned && !np.IsOwner)
            {
                // a puppet on this machine: only the owner's machine can move the body
                if (Net.RunSync.Instance) Net.RunSync.Instance.BroadcastStumble(np.Slot, impulse, duration);
                return;
            }
            StumbleLocal(impulse, duration);
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

        // Also sent to a disabled script, so the HOST gets it for a teammate's puppet body: a hard hit on a teammate is routed to
        // their machine by Stumble. Somebody who is already down (pinned / busted) cannot be tripped again.
        void OnCollisionEnter(Collision c)
        {
            // Trip on hard impacts (stairs, being hit by flung loot).
            if (!IsStumbling && !InCustody && c.relativeVelocity.sqrMagnitude > 81f) Stumble(Vector3.zero, 1.2f);
        }
    }
}
