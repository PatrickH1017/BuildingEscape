using UnityEngine;

namespace BuildingEscape
{
    /// <summary>What the crosshair is on that E can work.</summary>
    public enum AimKind { None, Pickup, Door, Dealer, Interact }

    /// <summary>What the crosshair points at right now (see <see cref="Crosshair.Aim"/>).</summary>
    public struct AimTarget
    {
        public AimKind Kind;
        /// <summary>The item under the crosshair (when <see cref="Kind"/> is Pickup).</summary>
        public Pickup Pickup;
        /// <summary>Index of the door under the crosshair in <see cref="DoorManager.Doors"/> (when Kind is Door; otherwise -1).</summary>
        public int Door;
        /// <summary>The keypad, safe or note under the crosshair (when <see cref="Kind"/> is Interact).</summary>
        public Interactable Interactable;
        /// <summary>A guard or a training dummy is under the crosshair and within swinging distance.</summary>
        public bool Foe;
        /// <summary>The crosshair is on something E could work, but it is out of reach (nothing else is targeted).</summary>
        public bool TooFar;
        /// <summary>Metres from the start of the aim ray to the target (0 when there is none).</summary>
        public float Distance;

        public static AimTarget Nothing => new AimTarget { Kind = AimKind.None, Door = -1 };
    }

    /// <summary>
    /// The crosshair: a small reticle in the middle of the screen, and the one place that decides what the player is pointing at.
    ///
    /// The ray goes from the camera straight through the middle of the screen. It starts at the player's own depth (so a wall or a desk
    /// between the camera and the player is not "in the way") and runs a few metres past the player's reach. Whatever it meets first
    /// within reach is the target: an item (E puts it in the backpack, Q carries it), a doorway (E opens it, E shuts it again) or the
    /// dealer (E talks). Items are aimed at by their box, grown a little, so a mug on a desk is easy to hit; doors are aimed at where
    /// the doorway is, open or shut; walls hide what is behind them. Allies never get in the way.
    ///
    /// Everything that used to act on "whatever is nearest" (E for items, doors and the dealer, Q for carrying) now asks <see cref="Aim"/>,
    /// and a swing is aimed towards the crosshair (<see cref="SwingForward"/>). The reticle changes colour: yellow when E would work
    /// something, red when a swing would hit a guard, grey when the thing is too far away.
    /// Purely local: nothing here is sent over the network (the pick-up and door requests that follow are the old ones).
    /// The geometry is <see cref="AimMath"/>; <see cref="Resolve"/> is the testable entry.
    /// </summary>
    public sealed class Crosshair : MonoBehaviour
    {
        /// <summary>Show the reticle (the targeting still works without it).</summary>
        public bool show = true;
        [Range(0.5f, 2f)] public float size = 1f;

        /// <summary>Used when the player has no backpack to say how far he reaches.</summary>
        public const float DefaultReach = 2.2f;
        /// <summary>A guard this close (flat distance) can be hit by a swing: the reticle turns red.</summary>
        public const float SwingRange = 3.2f;
        /// <summary>How far past the player's depth the swing is aimed (the swing's hit zone sits about this far ahead).</summary>
        public const float SwingAhead = 2.4f;
        const float DealerRadius = 0.7f;
        const float OccluderSlack = 0.25f;       // a doorway's own blocker, and things lying on a surface, are not "in the way"
        const float ItemSlack = 0.15f;

        public static Crosshair Instance { get; private set; }

        // ------------------------------------------------------------------ the target

        static int frame = -1;
        static AimTarget aim = AimTarget.Nothing;
        static CameraFollow rig;
        static WobblyPlayer rigPlayer;
        static float nextRigLookup;

        /// <summary>
        /// What the local player's crosshair is on. Worked out once a frame, the first time anybody asks, from where the camera was drawn
        /// last. Nothing while a menu or the shop is open (the cursor is free then).
        /// </summary>
        public static AimTarget Aim
        {
            get
            {
                if (frame == Time.frameCount) return aim;
                frame = Time.frameCount;
                aim = AimTarget.Nothing;
                if (MenuOpen || !TryGetView(out var cam, out var player)) return aim;
                aim = Resolve(cam.transform.position, cam.transform.forward, player);
                return aim;
            }
        }

        /// <summary>A menu, the shop, the elevator panel or the inventory is open (E means something else there, and the cursor is free).</summary>
        public static bool MenuOpen => Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || EscapeMenu.Open || TestMode.PanelOpen || OutpostUi.Open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            frame = -1; aim = AimTarget.Nothing; rig = null; rigPlayer = null; nextRigLookup = 0f; Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance) return;
            var go = new GameObject("Crosshair");
            DontDestroyOnLoad(go);
            go.AddComponent<Crosshair>();
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            show = PlayerPrefs.GetInt("OvertimeEscape.Crosshair", 1) != 0;       // (0 hides the reticle; the targeting stays)
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>E on a keypad, a safe or a note: the crosshair says which (items, doors and the dealer have their own scripts).</summary>
        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.E)) return;
            var a = Aim;
            if (a.Kind != AimKind.Interact || a.Interactable == null) return;
            if (!TryGetView(out _, out var player) || player.InCustody || player.IsHidden) return;
            a.Interactable.Use(player);
        }

        /// <summary>The main camera and the player it follows (the body whose crosshair this is).</summary>
        static bool TryGetView(out Camera cam, out WobblyPlayer player)
        {
            player = null;
            cam = Camera.main;
            if (!cam) return false;
            if (!rig && Time.unscaledTime >= nextRigLookup)
            {
                rig = FindFirstObjectByType<CameraFollow>();
                nextRigLookup = Time.unscaledTime + 0.5f;
            }
            if (!rig || !rig.target) return false;
            if (!rigPlayer || rigPlayer.transform != rig.target) rigPlayer = rig.target.GetComponentInParent<WobblyPlayer>();
            player = rigPlayer;
            return player != null;
        }

        // ------------------------------------------------------------------ finding the target

        static readonly RaycastHit[] hits = new RaycastHit[48];
        static readonly System.Collections.Generic.List<Pickup> candidates = new System.Collections.Generic.List<Pickup>(32);

        sealed class HitOrder : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitOrder Instance = new HitOrder();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        /// <summary>
        /// What a player is pointing at with a ray from <paramref name="origin"/> along <paramref name="direction"/> (the camera's view).
        /// The tests call this directly.
        /// </summary>
        public static AimTarget Resolve(Vector3 origin, Vector3 direction, WobblyPlayer player)
        {
            var res = AimTarget.Nothing;
            if (!player) return res;
            direction.Normalize();
            var body = player.transform.position;
            float reach = player.TryGetComponent(out Backpack pack) ? pack.reach : DefaultReach;

            float depth = AimMath.Depth(origin.x, origin.y, origin.z, direction.x, direction.y, direction.z, body.x, body.y, body.z);
            float t0 = AimMath.StartT(depth);
            float len = AimMath.EndT(depth, Mathf.Max(reach, DoorManager.UseReach)) - t0;
            var start = origin + direction * t0;

            // ---- what is in the way: the first wall, desk or guard (items, the player himself and his allies do not count)
            float blockS = len;
            Collider blocker = null;
            int n = Physics.RaycastNonAlloc(start, direction, hits, len, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, 0, n, HitOrder.Instance);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (!c || c.transform.IsChildOf(player.transform)) continue;
                if (c.GetComponentInParent<WobblyPlayer>() != null || c.GetComponentInParent<Pickup>() != null) continue;
                blockS = hits[i].distance;
                blocker = c;
                break;
            }
            System.Array.Clear(hits, 0, n);

            // ---- a guard or a dummy within swinging distance
            if (blocker != null && (blocker.GetComponentInParent<GuardAI>() != null || blocker.GetComponentInParent<TrainingDummy>() != null))
            {
                var p = blocker.transform.position;
                float fx = p.x - body.x, fz = p.z - body.z;
                res.Foe = fx * fx + fz * fz <= SwingRange * SwingRange;
            }

            // ---- an item: the aim ray against the box around it
            Pickup item = null;
            float itemS = float.PositiveInfinity;
            bool farItem = false;
            Pickup.ScanAround(body, reach + 2.5f, candidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                var p = candidates[i];
                if (!p.TryGetBounds(out var b)) continue;
                if (!AimMath.RayItem(start.x, start.y, start.z, direction.x, direction.y, direction.z,
                                     b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z, len, out float s)) continue;
                if (s > blockS + ItemSlack) continue;                    // behind a wall or a desk front
                if (p.DistanceFrom(body) > reach) { farItem = true; continue; }
                if (s < itemS) { itemS = s; item = p; }
            }
            candidates.Clear();

            // ---- a door
            float doorEnter = float.PositiveInfinity, doorExit = float.PositiveInfinity, farDoor = float.PositiveInfinity;
            int door = -1;
            var doors = DoorManager.Active;
            if (doors != null)
            {
                door = doors.Aimed(start, direction, len, body, DoorManager.UseReach, out doorEnter, out doorExit, out farDoor);
                if (door >= 0 && blockS < doorEnter - OccluderSlack) { door = -1; doorEnter = doorExit = float.PositiveInfinity; }
            }
            bool farDoorAimed = !float.IsPositiveInfinity(farDoor) && blockS >= farDoor - OccluderSlack;

            // ---- the dealer (a longer ray: he can be called from across the trailer; only while you may talk to him)
            float dealerS = float.PositiveInfinity;
            var vendor = Vendor.Nearby;
            if (vendor != null && vendor.CanTalk && vendor.npc != null)
            {
                var np = vendor.npc.position;
                if (AimMath.RayCylinderY(start.x, start.y, start.z, direction.x, direction.y, direction.z,
                                         np.x, np.z, DealerRadius, np.y - 1.1f, np.y + 1.3f, len + vendor.talkRange, out float ds))
                    dealerS = ds;
            }

            // ---- something in the world that E works (the outpost's keypad, a safe, a note)
            float thingEnter = float.PositiveInfinity, farThing = float.PositiveInfinity;
            var thing = Interactable.Aimed(start, direction, len, body, out thingEnter, out farThing);
            if (thing != null && blockS < thingEnter - OccluderSlack) { thing = null; thingEnter = float.PositiveInfinity; }
            bool farThingAimed = !float.IsPositiveInfinity(farThing) && blockS >= farThing - OccluderSlack;

            // ---- which one E works
            float pickS = item != null ? itemS : float.PositiveInfinity;
            switch (AimMath.Choose(pickS, door >= 0 ? doorEnter : float.PositiveInfinity, doorExit, dealerS, thing != null ? thingEnter : float.PositiveInfinity))
            {
                case AimMath.Pick.Pickup: res.Kind = AimKind.Pickup; res.Pickup = item; res.Distance = itemS; break;
                case AimMath.Pick.Door: res.Kind = AimKind.Door; res.Door = door; res.Distance = doorEnter; break;
                case AimMath.Pick.Dealer: res.Kind = AimKind.Dealer; res.Distance = dealerS; break;
                case AimMath.Pick.Interact: res.Kind = AimKind.Interact; res.Interactable = thing; res.Distance = thingEnter; break;
                default: res.TooFar = farItem || farDoorAimed || farThingAimed; break;
            }
            return res;
        }

        // ------------------------------------------------------------------ the swing

        /// <summary>
        /// The way a swing by <paramref name="body"/> goes (a flat unit vector): towards where the crosshair points, so the swing and the
        /// reticle agree. The way the body faces when it is not the one the camera follows, or while the camera is orbiting it.
        /// </summary>
        public static Vector3 SwingForward(Transform body)
        {
            var facing = body.forward;
            facing.y = 0f;
            facing = facing.sqrMagnitude > 1e-4f ? facing.normalized : Vector3.forward;
            if (!TryGetView(out var cam, out var player) || player.transform != body || rig.FreeLooking) return facing;
            var o = cam.transform.position;
            var d = cam.transform.forward;
            var p = body.position;
            return AimMath.SwingDirection(o.x, o.y, o.z, d.x, d.y, d.z, p.x, p.y, p.z, SwingAhead, out float sx, out float sz)
                ? new Vector3(sx, 0f, sz) : facing;
        }

        // ------------------------------------------------------------------ the reticle

        static readonly Color Idle = new Color(1f, 1f, 1f, 0.8f);
        static readonly Color Usable = new Color(1f, 0.9f, 0.25f, 1f);
        static readonly Color Hostile = new Color(1f, 0.3f, 0.25f, 1f);
        static readonly Color Far = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.55f);

        bool Visible()
        {
            if (!show || Cursor.lockState != CursorLockMode.Locked || MenuOpen) return false;
            return TryGetView(out _, out _);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Visible()) return;
            var a = Aim;
            bool armed = false;
            if (a.Foe && TryGetView(out _, out var me) && me.TryGetComponent(out Backpack pack) && pack.Equipped != null)
                armed = pack.Equipped.GetComponent<MeleeWeapon>() != null;

            Color c = armed && a.Foe ? Hostile : a.Kind != AimKind.None ? Usable : a.TooFar ? Far : Idle;
            bool locked = a.Kind != AimKind.None || (armed && a.Foe);

            float s = Mathf.Clamp(Screen.height / 1080f, 0.8f, 2f) * size;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            float dot = Mathf.Round(3f * s), thick = Mathf.Max(2f, Mathf.Round(2f * s));
            float gap = Mathf.Round((locked ? 4f : 7f) * s), arm = Mathf.Round(9f * s);

            var old = GUI.color;
            Draw(cx, cy, dot, thick, gap, arm, 1f, Shadow);
            Draw(cx, cy, dot, thick, gap, arm, 0f, c);
            GUI.color = old;

            if (a.Kind == AimKind.Interact && a.Interactable != null)
                HudText.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 36), a.Interactable.Prompt, HudStyles.Label(22, TextAnchor.MiddleCenter));
        }

        /// <summary>A dot with four ticks round it; <paramref name="grow"/> makes the shadow a pixel bigger on every side.</summary>
        static void Draw(float cx, float cy, float dot, float thick, float gap, float arm, float grow, Color colour)
        {
            GUI.color = colour;
            var t = Texture2D.whiteTexture;
            float g = grow;
            GUI.DrawTexture(new Rect(cx - dot * 0.5f - g, cy - dot * 0.5f - g, dot + 2f * g, dot + 2f * g), t);
            GUI.DrawTexture(new Rect(cx - thick * 0.5f - g, cy - gap - arm - g, thick + 2f * g, arm + 2f * g), t);   // up
            GUI.DrawTexture(new Rect(cx - thick * 0.5f - g, cy + gap - g, thick + 2f * g, arm + 2f * g), t);         // down
            GUI.DrawTexture(new Rect(cx - gap - arm - g, cy - thick * 0.5f - g, arm + 2f * g, thick + 2f * g), t);   // left
            GUI.DrawTexture(new Rect(cx + gap - g, cy - thick * 0.5f - g, arm + 2f * g, thick + 2f * g), t);         // right
        }
    }
}
