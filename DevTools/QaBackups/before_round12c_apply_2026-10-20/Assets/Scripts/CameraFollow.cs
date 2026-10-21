using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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

        /// <summary>
        /// Spectating (the owner, 2026-10-18): while this is set the camera orbits THIS body instead of <see cref="target"/>. Only the picture follows it: everything that asks who is "viewed"
        /// (the pack, the keys, the HUD) still looks at <see cref="target"/>, so a knocked-out player who watches a teammate cannot act through him. See <see cref="Spectator"/>.
        /// </summary>
        public Transform spectate;
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
        [Header("View (the owner: wheel up = first person, wheel down = third; Ctrl + Up / Down and P for those without a mouse)")]
        public KeyCode viewKey = KeyCode.P;
        public float eyeHeight = 0.62f;       // first person: the eyes above the capsule centre

        /// <summary>Yaw the character should face / move relative to (ignores free look).</summary>
        public float AimYaw => aimYaw;
        public bool FreeLooking { get; private set; }

        // the dart game (the owner, 2026-10-20): at the throw line the aim can turn only a little, like Wii bowling
        bool aimLimited; float limitCentre, limitHalf, limitPitchMin, limitPitchMax;
        public bool AimLimited => aimLimited;
        /// <summary>
        /// The aim may turn <paramref name="halfRange"/> degrees either side of <paramref name="centreYaw"/> and tilt between the two pitches; the view is put on the centre (and tilted at <paramref name="lookAt"/>, if there is one).
        /// Free look is off meanwhile. <see cref="ClearAimLimit"/> lifts it.
        /// </summary>
        public void LimitAim(float centreYaw, float halfRange, float pitchMin, float pitchMax, Vector3? lookAt = null)
        {
            aimLimited = true; limitCentre = centreYaw; limitHalf = halfRange; limitPitchMin = pitchMin; limitPitchMax = pitchMax;
            aimYaw = centreYaw; lookYaw = 0f; lookPitch = 0f;
            if (lookAt.HasValue && (spectate ? spectate : target))
            {
                // the camera hangs behind the head and a shoulder to the right: its line of sight passes through the point a shoulder's width to the right of the head, so the view is turned to the left of the point's bearing
                // by asin(shoulder / distance) and tilted by the drop over the distance from that shoulder point, and the crosshair is ON the point, not beside it
                var d = lookAt.Value - Pivot();
                float flat = new Vector2(d.x, d.z).magnitude, side = Mathf.Lerp(shoulderOffset, 0f, ViewRules.Smooth(zoom));
                if (flat > side + 0.05f)
                {
                    aimYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - Mathf.Asin(side / flat) * Mathf.Rad2Deg;
                    pitch = Mathf.Atan2(-d.y, Mathf.Sqrt(flat * flat - side * side)) * Mathf.Rad2Deg;
                }
            }
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        }
        public void ClearAimLimit() => aimLimited = false;
        /// <summary>The camera tilts by this many degrees (positive: further down; the tests call it too): kept within the limit, if there is one (the owner, 2026-10-20: "The dart camera should also be able to go up and down as well").</summary>
        public void TiltAim(float dpitch) => pitch = aimLimited ? Mathf.Clamp(pitch + dpitch, limitPitchMin, limitPitchMax) : Mathf.Clamp(pitch + dpitch, minPitch, maxPitch);
        /// <summary>How far the camera is tilted down (negative: up), in degrees.</summary>
        public float AimPitch => pitch;
        /// <summary>The mouse turned the aim by this many degrees (the tests call it too): kept within the limit, if there is one.</summary>
        public void TurnAim(float dyaw)
        {
            aimYaw += dyaw;
            if (aimLimited) aimYaw = limitCentre + Mathf.Clamp(Mathf.DeltaAngle(limitCentre, aimYaw), -limitHalf, limitHalf);
        }

        float aimYaw, pitch = 12f, lookYaw, lookPitch;
        float zoom, zoomTarget;               // 0 = third person .. 1 = first person (ViewRules)
        readonly List<Renderer> hiddenBody = new List<Renderer>();
        readonly Dictionary<Renderer, ShadowCastingMode> bodyModes = new Dictionary<Renderer, ShadowCastingMode>();
        bool bodyHidden; float nextBodyScan;

        /// <summary>Is the view at the eyes (the body is out of the picture)?</summary>
        public bool IsFirstPerson => ViewRules.IsFirstPerson(zoom);
        public float Zoom => zoom;
        public void SetFirstPerson(bool on) => zoomTarget = on ? ViewRules.FirstPerson : ViewRules.ThirdPerson;
        public void ToggleView() => zoomTarget = zoomTarget > 0.5f ? ViewRules.ThirdPerson : ViewRules.FirstPerson;
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
            var body = spectate ? spectate : target;
            if (!targetPlayer || targetPlayer.transform != body) targetPlayer = body.GetComponent<WobblyPlayer>();
            bool crouched = targetPlayer && targetPlayer.IsCrouching;
            crouchDrop = Mathf.MoveTowards(crouchDrop, crouched ? 1f : 0f, Time.deltaTime * 5f);
            return body.position + Vector3.up * (Mathf.Lerp(headHeight, eyeHeight, ViewRules.Smooth(zoom)) - 0.5f * crouchDrop);
        }

        void Update()
        {
            var hm = HeistManager.Instance;
            bool menu = hm && (hm.Phase == HeistPhase.Won || hm.Phase == HeistPhase.Failed);
            cursorFree = EscapeMenu.Open; // Esc opens the menu (it does not pause)
            bool locked = !menu && !cursorFree && !Backpack.UiOpen && !TestMode.PanelOpen && !Vendor.ShopOpen && !ElevatorPanel.Open && !OutpostUi.FreeCursor;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (!locked) return;

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            zoomTarget = ViewRules.Target(zoomTarget, Input.mouseScrollDelta.y, ctrl && Input.GetKeyDown(KeyCode.UpArrow), ctrl && Input.GetKeyDown(KeyCode.DownArrow), Input.GetKeyDown(viewKey));

            float mx = Input.GetAxis("Mouse X") * sensitivity;
            float my = Input.GetAxis("Mouse Y") * sensitivity;
            FreeLooking = !aimLimited && (Input.GetKey(freeLookKey) || Input.GetKey(altFreeLookKey));
            if (FreeLooking)
            {
                lookYaw += mx;
                lookPitch = Mathf.Clamp(lookPitch - my, minPitch - pitch, maxPitch - pitch);
            }
            else
            {
                TurnAim(mx);
                TiltAim(-my);
                float k = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
                lookYaw = Mathf.LerpAngle(lookYaw, 0f, k);
                lookPitch = Mathf.Lerp(lookPitch, 0f, k);
                if (Mathf.Abs(Mathf.DeltaAngle(lookYaw, 0f)) < 0.05f) lookYaw = 0f;
            }
        }

        /// <summary>The body goes out of the picture in first person (its meshes only cast their shadow; what is in the hands stays) and comes back as it was.</summary>
        void ShowBody(bool show)
        {
            var body = spectate ? spectate : target;
            if (!body) return;
            if (show)
            {
                if (!bodyHidden) return;
                foreach (var r in hiddenBody) if (r && bodyModes.TryGetValue(r, out var mode)) r.shadowCastingMode = mode;
                hiddenBody.Clear(); bodyModes.Clear();
                bodyHidden = false;
                return;
            }
            if (bodyHidden && Time.unscaledTime < nextBodyScan) return;
            nextBodyScan = Time.unscaledTime + 0.5f;                                  // (a hat or another look can come later: looked at again twice a second)
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (!r || bodyModes.ContainsKey(r) || r.GetComponentInParent<HeldGun>() != null || r.GetComponentInParent<MeleeWeapon>() != null) continue;
                bodyModes[r] = r.shadowCastingMode;
                hiddenBody.Add(r);
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            bodyHidden = true;
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
            zoom = ViewRules.Step(zoom, zoomTarget, Time.unscaledDeltaTime);
            float zs = ViewRules.Smooth(zoom);
            smoothedPivot = Vector3.Lerp(smoothedPivot, Pivot(), 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
            var rot = Quaternion.Euler(pitch + lookPitch, aimYaw + lookYaw, 0f);
            var dir = rot * new Vector3(Mathf.Lerp(shoulderOffset, 0f, zs), 0f, -ViewRules.Distance(distance, zoom));
            float len = dir.magnitude;
            if (len < 0.02f)
            {
                transform.SetPositionAndRotation(smoothedPivot, rot);                 // (first person: the camera is at the eyes)
            }
            else
            {
                var ndir = dir / len;
                // pull the camera in front of walls (ignore rigidbodies like loot, mugs, players)
                int n = Physics.SphereCastNonAlloc(smoothedPivot, collisionRadius, ndir, hits, len, ~0, QueryTriggerInteraction.Ignore);
                float best = len;
                for (int i = 0; i < n; i++)
                {
                    if (hits[i].rigidbody != null || hits[i].distance <= 0f) continue;
                    if (hits[i].distance < best) best = hits[i].distance;
                }
                transform.position = smoothedPivot + ndir * Mathf.Max(Mathf.Lerp(0.3f, 0f, zs), best);
                transform.rotation = rot;
            }
            ShowBody(!ViewRules.IsFirstPerson(zoom));
        }
    }
}
