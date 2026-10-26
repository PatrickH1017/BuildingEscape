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
        /// <summary>Tests: the free look key is held like the real key (the view then unlocks only after <see cref="FreeLookRules.HoldSeconds"/>, as with the real one).</summary>
        public static bool TestFreeLookKey;
        float freeLookHeld;                   // seconds the free look key has been held (FreeLookRules)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTestHooks() { TestFreeLookKey = false; }

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

        float deskYaw, deskPitch;
        /// <summary>The turn of the view towards the cursor at the security computer (degrees): sideways to the right and down. Tests read it.</summary>
        public Vector2 DeskLook => new Vector2(deskYaw, deskPitch);

        /// <summary>
        /// At the security computer (the owner, 2026-10-23): with the cursor over the monitor the view stays; with the cursor outside it the view turns towards the cursor, a little (<see cref="DeskLookRules.MaxYaw"/>, <see cref="DeskLookRules.MaxPitch"/> degrees)
        /// and very slowly (<see cref="DeskLookRules.Ease"/>); back over the monitor, or with the screen shut, it eases back. It drives the free look's own offsets, which nothing else touches while a screen has the cursor.
        /// </summary>
        void DeskLookStep()
        {
            if (!ComputerScreen.TryGetMonitor(out var mon)) { deskYaw = deskPitch = 0f; return; }          // (the screen is shut: the usual return takes over from wherever the view is)
            var cursor = ComputerScreen.CursorPosition;
            DeskLookRules.Target(cursor.x, Screen.height - cursor.y, mon.x, mon.y, mon.width, mon.height, Screen.width, Screen.height, out float wantYaw, out float wantPitch);
            float dt = Time.unscaledDeltaTime;
            deskYaw = DeskLookRules.Ease(deskYaw, wantYaw, dt);
            deskPitch = DeskLookRules.Ease(deskPitch, wantPitch, dt);
            if (wantYaw == 0f && Mathf.Abs(deskYaw) < 0.05f) deskYaw = 0f;
            if (wantPitch == 0f && Mathf.Abs(deskPitch) < 0.05f) deskPitch = 0f;
            lookYaw = deskYaw;
            lookPitch = deskPitch;
        }
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
        // looking through a sight puts the gun's parts a hand's width from the lens: the near plane comes in with the first person view (the camera's own value is put back in third person)
        public const float FirstPersonNear = 0.04f;
        Camera viewCamera; float baseNear = -1f;
        // the sniper rifle's scope (the owner, 2026-10-20 night: "look down the scope and it zooms in a little"): the field of view is narrowed by HeldGun.ScopeMagnification while the view is down the scope;
        // the field it had is remembered when the zoom starts and put back when it ends
        float scopeBaseFov; bool scopeFovTaken;

        void ApplyScopeFov()
        {
            if (!viewCamera) return;
            float t = HeldGun.LocalScope;
            if (t > 0.001f)
            {
                if (!scopeFovTaken) { scopeBaseFov = viewCamera.fieldOfView; scopeFovTaken = true; }
                float mag = Mathf.Lerp(1f, HeldGun.ScopeMagnification, t);
                viewCamera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(scopeBaseFov * Mathf.Deg2Rad * 0.5f) / mag) * Mathf.Rad2Deg;
            }
            else if (scopeFovTaken) { viewCamera.fieldOfView = scopeBaseFov; scopeFovTaken = false; }
        }
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
            DeskLookStep();
            var hm = HeistManager.Instance;
            bool menu = hm && (hm.Phase == HeistPhase.Won || hm.Phase == HeistPhase.Failed);
            cursorFree = EscapeMenu.Open; // Esc opens the menu (it does not pause)
            bool locked = !menu && !cursorFree && !Backpack.UiOpen && !TestMode.PanelOpen && !Vendor.ShopOpen && !ElevatorPanel.Open && !OutpostUi.FreeCursor;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (!locked) { freeLookHeld = 0f; FreeLooking = false; return; }          // (a menu is open: the key does not count)

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            zoomTarget = ViewRules.Target(zoomTarget, Input.mouseScrollDelta.y, ctrl && Input.GetKeyDown(KeyCode.UpArrow), ctrl && Input.GetKeyDown(KeyCode.DownArrow), Input.GetKeyDown(viewKey));

            float mx = Input.GetAxis("Mouse X") * sensitivity;
            float my = Input.GetAxis("Mouse Y") * sensitivity;
            if (scopeFovTaken && viewCamera)          // (down the scope the mouse turns the view less, by as much as the picture is narrowed)
            {
                float k = Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) / Mathf.Tan(scopeBaseFov * Mathf.Deg2Rad * 0.5f);
                mx *= k; my *= k;
            }
            // Alt (or V) unlocks the view only while it is HELD, and only after FreeLookRules.HoldSeconds: a tap does nothing (the owner, 2026-10-23: "Clicking Alt should not unlock the camera view only when holding it");
            // a window that lost the focus (Alt+Tab) counts as let go
            bool freeKey = (Application.isFocused && (Input.GetKey(freeLookKey) || Input.GetKey(altFreeLookKey))) || TestFreeLookKey;
            freeLookHeld = FreeLookRules.Held(freeLookHeld, freeKey, Time.unscaledDeltaTime);
            FreeLooking = !aimLimited && FreeLookRules.Unlocked(freeLookHeld);
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

        // ---- what is seen of the player's own body in first person (the owner, 2026-10-20: "When in first person with a weapon the character is somewhat visible blocking the screen": the gun and a forearm, nothing else)
        const string ArmsShaderName = "BuildingEscape/FirstPersonArms";
        const float HeadClip = 0.22f;                                    // the radius (m) round the head that the arms shader throws away
        const float ArmReach = 0.105f;                                   // how far from a forearm's line the body is still drawn (m): the arm's own thickness and a little more
        static Shader armsShader; static bool armsShaderLooked;
        readonly Dictionary<Renderer, Material[]> armsOriginal = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<Material, Material> armsMade = new Dictionary<Material, Material>();
        Transform armRU, armRE, armRH, armLU, armLE, armLH, armHead;     // the right and the left upper arm, elbow and wrist, and the head (what is worn on it is never part of the arms)
        /// <summary>
        /// Are the two forearms drawn in first person whatever is in the hands? No since 2026-10-20 night (the owner, with a picture of the view through the red dot: "when scrolled into first person can you remove the character model from being seen here"):
        /// nothing of the body is drawn, only what is in the hands (and not the pistol's arm either since 2026-10-21: <see cref="HeldGun.ShowPistolArm"/>, <see cref="HeldGun.LocalArmMode"/>). Set it to true to bring both arms back.
        /// </summary>
        public static bool ShowArms = false;

        /// <summary>First person with a rig that has arms and <see cref="ShowArms"/> on: the body is drawn as just its two forearms and hands (the shader throws the rest away), with the gun or the tool in the hands whole.</summary>
        public bool ArmsShown { get; private set; }

        /// <summary>The camera of this machine (the guns ask where it is: in first person they are held in front of its eyes) and the body it is looking from.</summary>
        public static CameraFollow Current { get; private set; }
        public Transform ViewedBody => spectate ? spectate : target;

        void OnEnable() { Camera.onPreCull += PreCullArms; Current = this; }
        void OnDisable()
        {
            Camera.onPreCull -= PreCullArms; if (Current == this) Current = null;
            if (viewCamera && baseNear > 0f) viewCamera.nearClipPlane = baseNear;
            if (viewCamera && scopeFovTaken) { viewCamera.fieldOfView = scopeBaseFov; scopeFovTaken = false; }
        }
        void PreCullArms(Camera c) => PushArms();
        void OnDestroy() { foreach (var m in armsMade.Values) if (m) Destroy(m); armsMade.Clear(); }

        /// <summary>The bones of the two arms of a humanoid body; false for a rig without them (the Party character), which is then out of the picture altogether.</summary>
        bool FindArms(Transform body)
        {
            var an = body.GetComponentInChildren<Animator>();
            if (!an || !an.isHuman) return false;
            armRU = an.GetBoneTransform(HumanBodyBones.RightUpperArm); armRE = an.GetBoneTransform(HumanBodyBones.RightLowerArm); armRH = an.GetBoneTransform(HumanBodyBones.RightHand);
            armLU = an.GetBoneTransform(HumanBodyBones.LeftUpperArm); armLE = an.GetBoneTransform(HumanBodyBones.LeftLowerArm); armLH = an.GetBoneTransform(HumanBodyBones.LeftHand);
            armHead = an.GetBoneTransform(HumanBodyBones.Head);
            return armRU && armRE && armRH && armLU && armLE && armLH;
        }

        /// <summary>The gun or the tool in the hands (<c>X_Held</c>, built by RangedWeapon.BuildHeld / MeleeWeapon.BuildHeld) stays whole in the picture.</summary>
        public static bool IsHeldModel(Transform t, Transform body)
        {
            for (var p = t; p && p != body; p = p.parent) if (p.name.EndsWith("_Held")) return true;
            return false;
        }

        /// <summary>A body mesh drawn with Standard materials (the characters' colour atlas): the kind the arms shader can take over.</summary>
        static bool IsSkin(Renderer r)
        {
            if (!(r is SkinnedMeshRenderer || r is MeshRenderer) || !r.enabled) return false;
            var ms = r.sharedMaterials;
            if (ms.Length == 0) return false;
            foreach (var m in ms) if (!m || !m.shader || m.shader.name != "Standard") return false;
            return true;
        }

        Material[] ArmMaterialsFor(Material[] source)
        {
            var made = new Material[source.Length];
            for (int i = 0; i < made.Length; i++)
            {
                var m = source[i];
                if (!armsMade.TryGetValue(m, out var a) || !a)
                {
                    a = new Material(armsShader) { name = "Arms " + m.name, mainTexture = m.mainTexture, color = m.color, hideFlags = HideFlags.HideAndDontSave };
                    if (a.HasProperty("_Glossiness") && m.HasProperty("_Glossiness")) a.SetFloat("_Glossiness", m.GetFloat("_Glossiness"));
                    armsMade[m] = a;
                }
                made[i] = a;
            }
            return made;
        }

        /// <summary>Hands the two forearms to the arms shader just before a camera draws (after the gun's arm solver has put them where they are).</summary>
        void PushArms()
        {
            if (!ArmsShown || !armRE || !armRH || !armLE || !armLH) return;
            bool rightOnly = !ShowArms && HeldGun.LocalArmMode == 1;                   // (a pistol: the whole right arm reaching out, no left arm)
            SetArm("_BEArmRA", "_BEArmRB", armRU, armRE, armRH, rightOnly ? 0.75f : 0.30f);
            if (rightOnly) { Shader.SetGlobalVector("_BEArmLA", new Vector4(0f, -5000f, 0f, 0f)); Shader.SetGlobalVector("_BEArmLB", new Vector4(0f, -5000f, 0f, 0f)); }
            else SetArm("_BEArmLA", "_BEArmLB", armLU, armLE, armLH, 0.30f);
            Shader.SetGlobalFloat("_BEArmReach", ArmReach);
            // the head (a sphere round the middle of it): never drawn, except what is within a hand's reach of a fingertip (the shader keeps the hands whole)
            if (armHead) { var hp = armHead.position + Vector3.up * 0.08f; Shader.SetGlobalVector("_BEHead", new Vector4(hp.x, hp.y, hp.z, HeadClip)); }
            else Shader.SetGlobalVector("_BEHead", Vector4.zero);
        }

        static void SetArm(string idA, string idB, Transform upper, Transform elbow, Transform hand, float upperShare)
        {
            var dir = hand.position - elbow.position;
            float len = dir.magnitude;
            var tip = len > 0.001f ? hand.position + dir / len * 0.11f : hand.position;                    // (past the wrist: the fingers)
            var start = upper ? Vector3.Lerp(elbow.position, upper.position, upperShare) : elbow.position;       // (a little of the upper arm: the cut stays out of the picture)
            Shader.SetGlobalVector(idA, start);
            Shader.SetGlobalVector(idB, tip);
        }

        readonly List<Material> matScratch = new List<Material>();
        bool armsAppliedFor;                                                     // the arms setting the body was last set up with (a pistol in the hand brings the right arm, another gun takes it away)

        /// <summary>
        /// Every frame in first person: a body part whose materials were swapped for others (a look that was applied again, a flash) or whose shadow mode was changed is put back the way the first person view wants it,
        /// so that the body does not come back into the picture for a moment while the character moves (the owner, 2026-10-20 night: "I am able to see my whole character while I am moving").
        /// </summary>
        void KeepBodyHidden()
        {
            foreach (var r in hiddenBody)
            {
                if (!r) continue;
                if (armsOriginal.ContainsKey(r))
                {
                    r.GetSharedMaterials(matScratch);
                    if (matScratch.Count > 0 && matScratch[0] && !armsMade.ContainsValue(matScratch[0]))
                    {
                        armsOriginal[r] = r.sharedMaterials;                          // (the new look is what is put back in third person)
                        if (armsShader != null && IsSkin(r)) r.sharedMaterials = ArmMaterialsFor(r.sharedMaterials);
                    }
                }
                else if (r.shadowCastingMode != ShadowCastingMode.ShadowsOnly) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                if (!r.enabled && armsOriginal.ContainsKey(r)) r.enabled = true;
            }
        }

        /// <summary>In first person only the two arms are in the picture (the body casts its shadow still) with what is in the hands; in third person the body comes back as it was.</summary>
        void ShowBody(bool show)
        {
            var body = spectate ? spectate : target;
            if (!body) return;
            if (show)
            {
                if (!bodyHidden) return;
                foreach (var r in hiddenBody)
                {
                    if (!r) continue;
                    if (bodyModes.TryGetValue(r, out var mode)) r.shadowCastingMode = mode;
                    if (armsOriginal.TryGetValue(r, out var mats)) r.sharedMaterials = mats;
                }
                hiddenBody.Clear(); bodyModes.Clear(); armsOriginal.Clear();
                bodyHidden = false; ArmsShown = false;
                return;
            }
            if (bodyHidden) KeepBodyHidden();
            if (bodyHidden && Time.unscaledTime < nextBodyScan) return;
            nextBodyScan = Time.unscaledTime + 0.2f;                                  // (a hat or another look can come later: looked at again five times a second)
            if (!armsShaderLooked) { armsShaderLooked = true; armsShader = Shader.Find(ArmsShaderName); }
            bool wantArms = ShowArms || HeldGun.LocalArmMode != 0;
            armsAppliedFor = wantArms;
            bool arms = wantArms && armsShader != null && FindArms(body);
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (!r || bodyModes.ContainsKey(r) || IsHeldModel(r.transform, body)) continue;
                bodyModes[r] = r.shadowCastingMode;
                hiddenBody.Add(r);
                if (arms && IsSkin(r) && !(armHead && r.transform.IsChildOf(armHead))) { armsOriginal[r] = r.sharedMaterials; r.sharedMaterials = ArmMaterialsFor(r.sharedMaterials); }          // (a cap, goggles, a beard hang on the head: hidden whole)
                else r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            ArmsShown = arms;
            bodyHidden = true;
        }

        void LateUpdate()
        {
            if (!target && !spectate) return;           // (a developer who watches has no body of his own: DevSpectator)
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
            // (first person: the eyes follow the body almost rigidly, or a running body gets ahead of the camera and its head and shoulders come into the picture)
            smoothedPivot = Vector3.Lerp(smoothedPivot, Pivot(), 1f - Mathf.Exp(-followSmooth * Mathf.Lerp(1f, 8f, zs) * Time.deltaTime));
            if (!viewCamera) { viewCamera = GetComponent<Camera>(); if (viewCamera) baseNear = viewCamera.nearClipPlane; }
            if (viewCamera && baseNear > 0f) viewCamera.nearClipPlane = Mathf.Lerp(baseNear, Mathf.Min(baseNear, FirstPersonNear), zs);
            ApplyScopeFov();
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
            bool firstNow = ViewRules.IsFirstPerson(zoom);
            if (firstNow && bodyHidden && (ShowArms || HeldGun.LocalArmMode != 0) != armsAppliedFor) ShowBody(true);          // (the gun in the hand changed: the body is set up again)
            ShowBody(!firstNow);
        }
    }
}
