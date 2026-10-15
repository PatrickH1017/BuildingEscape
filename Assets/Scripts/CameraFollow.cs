using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Tight over-the-shoulder camera (R.E.P.O. style): just above and behind the head.
    /// Mouse looks and turns the character. Hold the free-look key (Rust-style) to orbit the
    /// camera around your character without turning it, so you can see its face; it eases
    /// back when released. Esc frees the cursor.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        [Header("Framing")]
        public float headHeight = 1.35f;      // pivot above the capsule centre (just over the head)
        public float distance = 2.6f;
        public float shoulderOffset = 0.45f;  // slides the camera to the right of the head
        public float collisionRadius = 0.25f;
        [Header("Look")]
        public float sensitivity = 2.4f;
        public float minPitch = -35f, maxPitch = 60f;
        public KeyCode freeLookKey = KeyCode.LeftAlt;
        public KeyCode altFreeLookKey = KeyCode.V; // for setups where Alt is intercepted
        public float returnSpeed = 10f;
        public float followSmooth = 25f;

        /// <summary>Yaw the character should face / move relative to (ignores free look).</summary>
        public float AimYaw => aimYaw;
        public bool FreeLooking { get; private set; }

        float aimYaw, pitch = 12f, lookYaw, lookPitch;
        bool cursorFree;
        Vector3 smoothedPivot;
        readonly RaycastHit[] hits = new RaycastHit[16];

        void Start()
        {
            sensitivity = PlayerPrefs.GetFloat("OvertimeEscape.MouseSensitivity", sensitivity); // Settings page
            if (target) { aimYaw = target.eulerAngles.y; smoothedPivot = Pivot(); }
        }

        WobblyPlayer targetPlayer;
        float crouchDrop;

        /// <summary>The camera sinks a little while the player it follows is crouched.</summary>
        Vector3 Pivot()
        {
            if (!targetPlayer || targetPlayer.transform != target) targetPlayer = target.GetComponent<WobblyPlayer>();
            bool crouched = targetPlayer && targetPlayer.IsCrouching;
            crouchDrop = Mathf.MoveTowards(crouchDrop, crouched ? 1f : 0f, Time.deltaTime * 5f);
            return target.position + Vector3.up * (headHeight - 0.5f * crouchDrop);
        }

        void Update()
        {
            var hm = HeistManager.Instance;
            bool menu = hm && (hm.Phase == HeistPhase.Won || hm.Phase == HeistPhase.Failed);
            cursorFree = EscapeMenu.Open; // Esc opens the menu (it does not pause)
            bool locked = !menu && !cursorFree && !Backpack.UiOpen && !TestMode.PanelOpen && !Vendor.ShopOpen && !ElevatorPanel.Open;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (!locked) return;

            float mx = Input.GetAxis("Mouse X") * sensitivity;
            float my = Input.GetAxis("Mouse Y") * sensitivity;
            FreeLooking = Input.GetKey(freeLookKey) || Input.GetKey(altFreeLookKey);
            if (FreeLooking)
            {
                lookYaw += mx;
                lookPitch = Mathf.Clamp(lookPitch - my, minPitch - pitch, maxPitch - pitch);
            }
            else
            {
                aimYaw += mx;
                pitch = Mathf.Clamp(pitch - my, minPitch, maxPitch);
                float k = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
                lookYaw = Mathf.LerpAngle(lookYaw, 0f, k);
                lookPitch = Mathf.Lerp(lookPitch, 0f, k);
                if (Mathf.Abs(Mathf.DeltaAngle(lookYaw, 0f)) < 0.05f) lookYaw = 0f;
            }
        }

        void LateUpdate()
        {
            if (!target) return;
            // using the elevator panel: the camera glides in front of it and looks straight at it
            if (ElevatorPanel.Open && ElevatorPanel.Active && ElevatorPanel.Active.TryGetView(out var viewPos, out var viewRot))
            {
                float vk = 1f - Mathf.Exp(-9f * Time.deltaTime);
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, viewPos, vk), Quaternion.Slerp(transform.rotation, viewRot, vk));
                smoothedPivot = Pivot();
                return;
            }
            smoothedPivot = Vector3.Lerp(smoothedPivot, Pivot(), 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
            var rot = Quaternion.Euler(pitch + lookPitch, aimYaw + lookYaw, 0f);
            var dir = rot * new Vector3(shoulderOffset, 0f, -distance);
            float len = dir.magnitude;
            var ndir = dir / len;

            // pull the camera in front of walls (ignore rigidbodies like loot, mugs, players)
            int n = Physics.SphereCastNonAlloc(smoothedPivot, collisionRadius, ndir, hits, len, ~0, QueryTriggerInteraction.Ignore);
            float best = len;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].rigidbody != null || hits[i].distance <= 0f) continue;
                if (hits[i].distance < best) best = hits[i].distance;
            }
            transform.position = smoothedPivot + ndir * Mathf.Max(0.3f, best);
            transform.rotation = rot;
        }
    }
}