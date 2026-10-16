using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>What the safe says about an attempt to open it (the host decides; see <see cref="FloorSafe.HostAttempt"/>).</summary>
    public enum SafeReply : byte
    {
        /// <summary>Nothing happened (the safe was already open).</summary>
        Ignored,
        /// <summary>The right code, or a crowbar that held: the safe is open.</summary>
        Opened,
        /// <summary>Pried open, but the crowbar snapped doing it.</summary>
        OpenedCrowbarBroke,
        /// <summary>Wrong code.</summary>
        Wrong,
        /// <summary>A wrong code was just typed: the keypad is locked out for a moment.</summary>
        LockedOut,
        /// <summary>The player tried to pry it without a crowbar.</summary>
        NoCrowbar
    }

    /// <summary>
    /// The safe on a floor (one floor in five has one, never more). A small steel safe against the wall of one of the numbered rooms: point the
    /// crosshair at it and press E for the keypad (<see cref="SafePanel"/>). The right 4-digit code opens it; without the code a crowbar pries
    /// it open (it takes a few noisy seconds and the crowbar breaks 35% of the time, <see cref="SafeRules"/>). Inside is cash, as bundles that
    /// are banked in the elevator like any other money: half of the floor's quota. The bundles exist from the start (so every machine numbers them
    /// the same) but are dormant, switched off, until the safe opens.
    /// </summary>
    public sealed class FloorSafe : Interactable
    {
        public const float Width = 0.9f, Height = 0.85f, Depth = 0.65f;
        const float WallThickness = 0.06f, DoorThickness = 0.08f, OpenAngle = 105f, OpenSeconds = 0.7f, LockoutSeconds = 1.5f;
        static readonly Vector3 BundleSize = new Vector3(0.16f, 0.045f, 0.08f);

        /// <summary>The safe of the floor that is on show (null when it has none).</summary>
        public static FloorSafe Active { get; private set; }

        public int Floor { get; private set; }
        public int Code { get; private set; }
        public bool IsOpen { get; private set; }
        /// <summary>How many bundles of cash it holds, and what they are worth together.</summary>
        public int BundleCount => cash.Count;

        Transform hinge;
        Collider doorCollider;
        Renderer led;
        MaterialPropertyBlock block;
        float openK;                        // 0 shut ... 1 open
        bool moving;
        float lockedUntil;
        float bornAt;
        readonly List<Pickup> cash = new List<Pickup>();
        static readonly int ColorId = Shader.PropertyToID("_Color"), EmissionId = Shader.PropertyToID("_EmissionColor");

        public override bool Available => !IsOpen;
        public override string Prompt => "[E] Open the safe";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = null; }

        void Awake() { bornAt = Time.time; }

        void OnDestroy() { if (Active == this) Active = null; }

        // ------------------------------------------------------------------ building

        /// <summary>Materials the safe is made of (the generator makes and frees them).</summary>
        public sealed class Mats
        {
            public Material steel, dark, brass, lining, led;
        }

        /// <summary>
        /// Builds the safe standing on the floor at (x, z), its front facing (sin yaw, cos yaw). Everything is boxes: the body is five steel
        /// panels round a hollow, the door hangs on a hinge at its left edge, and the dial, handle and keypad are on the door.
        /// </summary>
        public static FloorSafe Build(Transform parent, float x, float z, float yaw, int floor, int code, Mats m)
        {
            var root = new GameObject("FloorSafe");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
            var safe = root.AddComponent<FloorSafe>();
            safe.Floor = floor;
            safe.Code = code;

            float w = Width, h = Height, d = Depth, t = WallThickness;
            Panel(root.transform, "Back", new Vector3(0f, h * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(w, h, t), m.steel, true);
            Panel(root.transform, "Left", new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), m.steel, true);
            Panel(root.transform, "Right", new Vector3(w * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), m.steel, true);
            Panel(root.transform, "Top", new Vector3(0f, h - t * 0.5f, 0f), new Vector3(w, t, d), m.steel, true);
            Panel(root.transform, "Bottom", new Vector3(0f, t * 0.5f, 0f), new Vector3(w, t, d), m.steel, true);

            // the dark lining of the inside, a hair inside the panels (visual only)
            float iw = w - 2f * t, ih = h - 2f * t, id = d - t - DoorThickness;
            float izc = (-d * 0.5f + t + (d * 0.5f - DoorThickness)) * 0.5f;
            Panel(root.transform, "LiningBack", new Vector3(0f, h * 0.5f, -d * 0.5f + t + 0.003f), new Vector3(iw, ih, 0.006f), m.lining, false);
            Panel(root.transform, "LiningLeft", new Vector3(-iw * 0.5f + 0.003f, h * 0.5f, izc), new Vector3(0.006f, ih, id), m.lining, false);
            Panel(root.transform, "LiningRight", new Vector3(iw * 0.5f - 0.003f, h * 0.5f, izc), new Vector3(0.006f, ih, id), m.lining, false);
            Panel(root.transform, "LiningTop", new Vector3(0f, h - t - 0.003f, izc), new Vector3(iw, 0.006f, id), m.lining, false);
            Panel(root.transform, "LiningFloor", new Vector3(0f, t + 0.003f, izc), new Vector3(iw, 0.006f, id), m.lining, false);

            // the door, hinged on its left edge
            float leafW = w - 0.04f, leafH = h - 0.04f;
            var pivot = new GameObject("Hinge").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(-w * 0.5f + 0.02f, h * 0.5f, d * 0.5f - DoorThickness * 0.5f);
            safe.hinge = pivot;
            var bc = pivot.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(leafW * 0.5f, 0f, 0f);
            bc.size = new Vector3(leafW, leafH, DoorThickness);
            safe.doorCollider = bc;
            Panel(pivot, "Door", new Vector3(leafW * 0.5f, 0f, 0f), new Vector3(leafW, leafH, DoorThickness), m.steel, false);
            Panel(pivot, "DoorInset", new Vector3(leafW * 0.5f, 0f, DoorThickness * 0.5f + 0.004f), new Vector3(leafW - 0.12f, leafH - 0.12f, 0.008f), m.dark, false);
            // the combination dial
            var dial = Cylinder(pivot, "Dial", new Vector3(leafW * 0.30f, 0.10f, DoorThickness * 0.5f + 0.02f), 0.20f, 0.02f, m.brass);
            Panel(dial, "DialMark", new Vector3(0f, 0.6f, 0f), new Vector3(0.05f, 1.2f, 0.06f), m.dark, false);
            // the handle
            Panel(pivot, "Handle", new Vector3(leafW * 0.50f, -0.17f, DoorThickness * 0.5f + 0.028f), new Vector3(0.30f, 0.035f, 0.035f), m.brass, false);
            Panel(pivot, "HandleFootL", new Vector3(leafW * 0.50f - 0.12f, -0.17f, DoorThickness * 0.5f + 0.012f), new Vector3(0.03f, 0.03f, 0.03f), m.brass, false);
            Panel(pivot, "HandleFootR", new Vector3(leafW * 0.50f + 0.12f, -0.17f, DoorThickness * 0.5f + 0.012f), new Vector3(0.03f, 0.03f, 0.03f), m.brass, false);
            // the keypad, with a lamp that is red while the safe is shut
            float kx = leafW * 0.74f;
            Panel(pivot, "Keypad", new Vector3(kx, 0.10f, DoorThickness * 0.5f + 0.014f), new Vector3(0.17f, 0.24f, 0.02f), m.dark, false);
            var lamp = Panel(pivot, "Lamp", new Vector3(kx, 0.19f, DoorThickness * 0.5f + 0.026f), new Vector3(0.12f, 0.025f, 0.008f), m.led, false);
            safe.led = lamp.GetComponent<Renderer>();
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 3; col++)
                    Panel(pivot, "Key", new Vector3(kx + (col - 1) * 0.045f, 0.145f - row * 0.042f, DoorThickness * 0.5f + 0.027f), new Vector3(0.03f, 0.03f, 0.008f), m.lining, false);
            // hinge barrels
            Cylinder(pivot, "HingeTop", new Vector3(-0.005f, leafH * 0.5f - 0.1f, DoorThickness * 0.5f), 0.04f, 0.09f, m.brass, true);
            Cylinder(pivot, "HingeBottom", new Vector3(-0.005f, -leafH * 0.5f + 0.1f, DoorThickness * 0.5f), 0.04f, 0.09f, m.brass, true);

            safe.block = new MaterialPropertyBlock();
            safe.FitAimBoxToChildren();
            safe.reach = 2.5f;
            safe.UpdateLamp();
            Active = safe;
            return safe;
        }

        static GameObject Panel(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (!collider) { var c = go.GetComponent<Collider>(); if (c) Kill(c); }
            return go;
        }

        /// <summary>Destroys now in the editor (the floor preview builds outside Play mode, where Destroy is not allowed), next frame in the game.</summary>
        static void Kill(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>A short cylinder lying with its axis along the door's thickness (or standing, for the hinge barrels).</summary>
        static Transform Cylinder(Transform parent, string name, Vector3 localPos, float diameter, float length, Material mat, bool standing = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = standing ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var c = go.GetComponent<Collider>();
            if (c) Kill(c);
            return go.transform;
        }

        /// <summary>
        /// Puts the safe's cash in it: <paramref name="values"/> bundles, lying in rows on the floor of the hollow. They are real items (rigid
        /// bodies you pick up and bank), but switched off until the safe is open. Call after the NavMesh was baked.
        /// </summary>
        public void FillWithCash(Transform parent, int[] values, Material bill, Material band)
        {
            float[] xs = { -0.27f, -0.09f, 0.09f, 0.27f };
            float[] zs = { -0.18f, -0.03f, 0.12f };
            float y = WallThickness + BundleSize.y * 0.5f + 0.004f;
            for (int i = 0; i < values.Length && i < xs.Length * zs.Length; i++)
            {
                var local = new Vector3(xs[i % xs.Length], y, zs[i / xs.Length]);
                var pos = transform.TransformPoint(local);
                var rot = transform.rotation * Quaternion.Euler(0f, ((i * 37) % 11 - 5) * 1.5f, 0f);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Cash bundle";
                go.transform.SetParent(parent, true);
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = BundleSize;
                go.GetComponent<Renderer>().sharedMaterial = bill;
                var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = "Band";
                stripe.transform.SetParent(go.transform, false);
                stripe.transform.localScale = new Vector3(0.22f, 1.08f, 1.08f);
                stripe.GetComponent<Renderer>().sharedMaterial = band;
                Kill(stripe.GetComponent<Collider>());

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.4f;
                var pk = go.AddComponent<Pickup>();         // (registers its network id now, in the same order on every machine)
                pk.weight = 0.4f; pk.label = "Cash bundle"; pk.bankValue = values[i];
                var th = go.AddComponent<Throwable>();
                th.impactNoise = 5f; th.stunSeconds = 0.6f;

                rb.isKinematic = true;                      // dormant: it stays in the safe...
                pk.enabled = false;                         // ...and cannot be aimed at, picked up or streamed until the safe is open
                cash.Add(pk);
            }
        }

        // ------------------------------------------------------------------ opening

        /// <summary>Everybody (host and clients): the safe is open now. <paramref name="quiet"/>: it already was when this machine joined (no sound, no swing).</summary>
        public void ApplyOpen(bool quiet, bool crowbarBroke = false)
        {
            if (IsOpen) return;
            IsOpen = true;
            if (doorCollider) doorCollider.enabled = false;
            foreach (var pk in cash)
            {
                if (!pk) continue;
                pk.enabled = true;
                if (pk.TryGetComponent(out Rigidbody rb))
                {
                    rb.isKinematic = Net.NetRole.IsRemoteClient;       // the host simulates them; the others follow its snapshots
                    if (!rb.isKinematic) rb.WakeUp();
                }
            }
            if (quiet) { openK = 1f; ApplyDoor(); UpdateLamp(); return; }
            moving = true;
            UpdateLamp();
            var at = transform.position + Vector3.up * 0.5f;
            if (crowbarBroke) GameAudio.PlaySound(SoundId.CrowbarSnap, at, 0.9f, 1f, true, 30f, 3f);
            GameAudio.PlaySound(SoundId.SafeOpen, at, 1f, 1f, true, 34f, 3f);
        }

        void Update()
        {
            if (!moving) return;
            openK = Mathf.MoveTowards(openK, 1f, Time.deltaTime / OpenSeconds);
            ApplyDoor();
            if (openK >= 1f) moving = false;
        }

        void ApplyDoor()
        {
            if (!hinge) return;
            float ease = openK * openK * (3f - 2f * openK);
            hinge.localRotation = Quaternion.Euler(0f, -OpenAngle * ease, 0f);
        }

        void UpdateLamp()
        {
            if (!led) return;
            if (block == null) block = new MaterialPropertyBlock();
            var c = IsOpen ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.15f, 0.1f);
            led.GetPropertyBlock(block);
            block.SetColor(ColorId, c);
            block.SetColor(EmissionId, c * 1.6f);
            led.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------------ what a player does

        public override void Use(WobblyPlayer who)
        {
            if (who) SafePanel.Begin(this, who);
        }

        /// <summary>Is this position close enough to the safe for a player there to be working it? (The host's check.)</summary>
        public bool InReach(Vector3 playerPosition, float slack) => DistanceFrom(playerPosition) <= reach + slack;

        /// <summary>The local player typed a code: offline it is judged here, in a networked run the host judges it.</summary>
        public void SubmitCode(int code, WobblyPlayer who)
        {
            if (Net.NetRole.Synced) { Net.RunSync.Instance.RequestSafe(false, code); return; }
            Resolve(HostAttempt(false, code, who ? who.GetComponent<Backpack>() : null));
        }

        /// <summary>The local player finished prying.</summary>
        public void SubmitPry(WobblyPlayer who)
        {
            if (Net.NetRole.Synced) { Net.RunSync.Instance.RequestSafe(true, 0); return; }
            Resolve(HostAttempt(true, 0, who ? who.GetComponent<Backpack>() : null));
        }

        /// <summary>Offline only: acts on the answer (there is no host to announce it).</summary>
        void Resolve(SafeReply reply)
        {
            if (reply == SafeReply.Opened || reply == SafeReply.OpenedCrowbarBroke)
            {
                ApplyOpen(false, reply == SafeReply.OpenedCrowbarBroke);
                if (reply == SafeReply.OpenedCrowbarBroke) Backpack.NoticeToLocal("The crowbar snapped!");
            }
            else NetReply(reply);
        }

        /// <summary>
        /// Host (or offline): somebody tries the safe - with a 4-digit code, or with a crowbar from their backpack (<paramref name="pry"/>).
        /// The checks that need the player (is he close, can he act) are the caller's; this one decides the outcome: the right code opens
        /// it, a wrong one locks the keypad for a moment, a crowbar always opens it but snaps 35% of the time (and is then used up).
        /// </summary>
        public SafeReply HostAttempt(bool pry, int typedCode, Backpack by)
        {
            if (IsOpen) return SafeReply.Ignored;
            if (pry)
            {
                var crowbar = FindCrowbar(by);
                if (crowbar == null) return SafeReply.NoCrowbar;
                bool breaks = SafeRules.CrowbarBreaks(Random.value);
                if (breaks) UseUp(by, crowbar);
                return breaks ? SafeReply.OpenedCrowbarBroke : SafeReply.Opened;
            }
            if (Time.time < lockedUntil) return SafeReply.LockedOut;
            if (SafeRules.CodeMatches(typedCode.ToString("D4"), Code)) return SafeReply.Opened;
            lockedUntil = Time.time + LockoutSeconds;
            return SafeReply.Wrong;
        }

        /// <summary>The crowbar in a backpack (the one in hand first), or null.</summary>
        public static Pickup FindCrowbar(Backpack pack)
        {
            if (!pack) return null;
            if (pack.Equipped && IsCrowbar(pack.Equipped)) return pack.Equipped;
            foreach (var p in pack.Items) if (p && IsCrowbar(p)) return p;
            return null;
        }

        static bool IsCrowbar(Pickup p) => p.TryGetComponent(out MeleeWeapon w) && w.kind == MeleeWeapon.Kind.Crowbar;

        static void UseUp(Backpack pack, Pickup item)
        {
            int id = item.NetId;
            pack.RemoveUsed(item);
            Net.RunSync.Instance?.BroadcastConsumed(id);
            Destroy(item.gameObject);
        }

        /// <summary>The safe says no (this player only): wrong code, keypad locked out, no crowbar.</summary>
        public void NetReply(SafeReply reply)
        {
            if (reply == SafeReply.Wrong) GameAudio.PlaySound(SoundId.SafeWrong, transform.position + Vector3.up * 0.5f, 0.8f, 1f, true, 20f, 2f);
            SafePanel.Instance?.OnReply(reply);
        }
    }
}
