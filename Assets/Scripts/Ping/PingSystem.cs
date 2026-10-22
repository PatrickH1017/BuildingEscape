using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>What a ping marks: an item or a doorway, where its marker hangs, what it is called.</summary>
    public struct PingTarget
    {
        public PingTargetKind kind;
        /// <summary>The item's network id (an item), or the door's place in <see cref="DoorManager.Doors"/> (a door); -1 when it has none.</summary>
        public int id;
        /// <summary>The world point the marker hangs at.</summary>
        public Vector3 pos;
        public string label;
        public PingCategory category;
        /// <summary>Metres from where the aim ray started to the thing.</summary>
        public float distance;
    }

    /// <summary>
    /// The ping (the owner, 2026-10-21: "Add a ping system. Players can ping by pressing down on the scroll wheel and whatever is in their crosshairs will be the target. The ping should only work on lootable items, consumable items, weapons
    /// and doors."). Click the mouse wheel: the thing the crosshair is on - an item lying about (loot, a weapon or armour, a clip or anything used up) or a doorway, never a wall, a desk, a guard or a player - gets a marker that the whole team sees
    /// through walls for <see cref="PingRules.Seconds"/>: a diamond in the pinger's colour with the thing's name, who pinged and how far it is, and an arrow on the edge of the screen when it is out of view. A player has one ping at a time (a new one
    /// replaces it), and a marker goes when the item is taken. In a networked run the pinger's machine shows it at once and asks the host (<c>RunSync.RequestPing</c>), which checks it and tells everybody else; offline only the player's own shows.
    /// Installs itself (like the crosshair); the aiming is <see cref="TryAim"/>, which the tests call.
    /// </summary>
    public sealed class PingSystem : MonoBehaviour
    {
        public static PingSystem Instance { get; private set; }

        sealed class Mark
        {
            public int slot;
            public PingTarget target;
            public Pickup item;            // an item ping follows its item (it is pushed, carried...) and ends when it is taken
            public bool hadItem;           // the item was found when the marker went up (so a destroyed one can be told from one that was never there)
            public Vector3 offset;         // where the marker hangs, from the item
            public DoorManager doors;      // a door ping ends with its floor
            public float at;
        }

        readonly Dictionary<int, Mark> marks = new Dictionary<int, Mark>();
        float nextLocalPing;

        /// <summary>How many markers are up right now (for the tests).</summary>
        public static int Count => Instance ? Instance.marks.Count : 0;

        /// <summary>The marker a player has up, or false. For the tests.</summary>
        public static bool TryGet(int slot, out PingTarget target, out float age)
        {
            target = default; age = 0f;
            if (!Instance || !Instance.marks.TryGetValue(slot, out var m)) return false;
            target = m.target; age = Time.time - m.at;
            return true;
        }

        /// <summary>How many pings this machine has made (for the tests).</summary>
        public static int Made { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; rig = null; nextRigLookup = 0f; Made = 0; System.Array.Clear(shownFrom, 0, shownFrom.Length); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance) return;
            var go = new GameObject("Pings");
            DontDestroyOnLoad(go);
            go.AddComponent<PingSystem>();
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ------------------------------------------------------------------ the local player pings

        static CameraFollow rig;
        static float nextRigLookup;

        /// <summary>The main camera and the player it follows.</summary>
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
            player = rig.target.GetComponentInParent<WobblyPlayer>();
            return player != null;
        }

        /// <summary>May this player ping now? Not while a menu, the shop, the elevator panel or a screen is up, not with the cursor free, not pinned or busted.</summary>
        public static bool CanPing(WobblyPlayer p) => p && !p.InCustody && !Crosshair.MenuOpen && Cursor.lockState == CursorLockMode.Locked;

        /// <summary>This player's slot (offline: 0, the only player).</summary>
        public static int LocalSlot => Net.NetPlayer.Local ? Net.NetPlayer.Local.Slot : 0;

        void Update()
        {
            Prune();
            if (!Input.GetMouseButtonDown(PingRules.MouseButton)) return;
            if (!TryGetView(out var cam, out var player) || !CanPing(player)) return;
            PingFromView(cam.transform.position, cam.transform.forward, player);
        }

        /// <summary>
        /// A ping from this view (the camera's position and the way it looks): the thing the crosshair is on gets a marker, here and, in a networked run, on the team's machines. False when there is nothing to ping
        /// or the player pinged a moment ago. The click works this; the tests call it.
        /// </summary>
        public static bool PingFromView(Vector3 origin, Vector3 direction, WobblyPlayer player)
        {
            if (!Instance || Time.unscaledTime < Instance.nextLocalPing) return false;
            if (!TryAim(origin, direction, player, out var t)) return false;
            Instance.nextLocalPing = Time.unscaledTime + PingRules.Cooldown;
            Send(t);
            return true;
        }

        /// <summary>Puts this player's marker up on <paramref name="t"/> (here at once) and, in a networked run, asks the host to tell the team.</summary>
        public static void Send(PingTarget t)
        {
            if (!Instance) return;
            Made++;
            Instance.Place(LocalSlot, t, true);
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestPing((byte)t.kind, (ushort)Mathf.Max(0, t.id), t.pos);
        }

        /// <summary>The target a ping on this item would have (the same as the one the crosshair finds): for the tests.</summary>
        public static PingTarget ItemTarget(Pickup p)
        {
            var cat = CategoryOf(p);
            var box = p.TryGetBounds(out var b) ? b : new Bounds(p.transform.position, Vector3.zero);
            return new PingTarget { kind = PingTargetKind.Item, id = p.NetId, pos = box.center + Vector3.up * (box.extents.y + 0.1f), label = PingRules.NameOf(p.Label, cat, false, p.Value, p.Loot != null ? p.Loot.lifters : 1), category = cat };
        }

        static readonly int[] shownFrom = new int[8];

        /// <summary>How many markers have gone up for this player's slot since the game started (the player's own and the ones the host announced), for the tests.</summary>
        public static int ShownFrom(int slot) => shownFrom[Mathf.Clamp(slot, 0, shownFrom.Length - 1)];

        /// <summary>A teammate's ping (the host told everybody): the marker goes up here too. Not the player's own (it went up the moment it was made).</summary>
        public static void OnPinged(int slot, byte kind, int id, Vector3 pos)
        {
            if (!Instance || (Net.NetRole.Synced && slot == LocalSlot)) return;
            Instance.Place(slot, Describe(kind, id, pos), false);
        }

        /// <summary>The target a ping message names: the item or door by its number, with the point the pinger marked as the fallback.</summary>
        static PingTarget Describe(byte kind, int id, Vector3 pos)
        {
            var t = new PingTarget { kind = (PingTargetKind)kind, id = id, pos = pos, label = "Item", category = PingCategory.Loot };
            if (t.kind == PingTargetKind.Door)
            {
                t.category = PingCategory.Door;
                var d = DoorManager.Active ? DoorManager.Active.Get(id) : null;
                t.label = PingRules.NameOf(null, PingCategory.Door, d != null && (d.Locked || d.ComputerLocked));
            }
            else
            {
                var p = Net.NetIds.Get<Pickup>(id);
                if (p) { t.category = CategoryOf(p); t.label = PingRules.NameOf(p.Label, t.category, false, p.Value, p.Loot != null ? p.Loot.lifters : 1); }
            }
            return t;
        }

        // ------------------------------------------------------------------ what the crosshair is on

        static readonly RaycastHit[] hits = new RaycastHit[64];

        sealed class HitOrder : IComparer<RaycastHit>
        {
            public static readonly HitOrder Instance = new HitOrder();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        const float ItemSlack = 0.15f, OccluderSlack = 0.25f;

        /// <summary>What kind of thing a pickup is (for its marker).</summary>
        public static PingCategory CategoryOf(Pickup p)
        {
            if (!p) return PingCategory.Consumable;
            return PingRules.CategoryOf(RangedWeapon.IsWeapon(p), p.TryGetComponent(out ArmourItem _), p.TryGetComponent(out AmmoClip _), p.Value);
        }

        /// <summary>
        /// What a ping from this view would mark: the first item or doorway the aim ray meets before a wall, a desk or a guard, within <see cref="PingRules.MaxRange"/>; false when there is none (a wall, a desk, a guard, a player, a safe
        /// and the rest are not pingable). The ray starts at the player's own depth like the crosshair's, so a wall between a camera behind the player and the player does not count; items are aimed at by their box, grown a little
        /// with the distance (a phone across a room is only a few pixels wide).
        /// </summary>
        public static bool TryAim(Vector3 origin, Vector3 direction, WobblyPlayer player, out PingTarget target)
        {
            target = default;
            if (!player || direction.sqrMagnitude < 1e-6f) return false;
            direction.Normalize();
            var body = player.transform.position;
            float depth = AimMath.Depth(origin.x, origin.y, origin.z, direction.x, direction.y, direction.z, body.x, body.y, body.z);
            var start = origin + direction * AimMath.StartT(depth);
            float len = PingRules.MaxRange;

            // ---- what is in the way: the first wall, desk or guard (items and people do not count)
            float blockS = len;
            int n = Physics.RaycastNonAlloc(start, direction, hits, len, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, 0, n, HitOrder.Instance);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (!c || c.transform.IsChildOf(player.transform)) continue;
                if (c.GetComponentInParent<WobblyPlayer>() != null || c.GetComponentInParent<Pickup>() != null) continue;
                blockS = hits[i].distance;
                break;
            }
            System.Array.Clear(hits, 0, n);

            // ---- the nearest item the ray goes through the box of
            Pickup item = null; float itemS = float.PositiveInfinity; Bounds itemBox = default;
            foreach (var kv in Net.NetIds.All)
            {
                var p = kv.Value as Pickup;
                if (!p || !p.isActiveAndEnabled || p.IsHeld) continue;
                var loot = p.Loot;
                if (loot != null && loot.Holders > 0) continue;                          // (somebody is carrying it)
                if (!p.TryGetBounds(out var b)) continue;
                float cd = (b.center - start).magnitude;
                if (cd > len + 4f) continue;
                b.Expand(0.12f + 0.014f * cd);
                if (!AimMath.RayItem(start.x, start.y, start.z, direction.x, direction.y, direction.z, b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z, len, out float s)) continue;
                if (s > blockS + ItemSlack) continue;                                      // (behind a wall or a desk front)
                if (s < itemS) { itemS = s; item = p; itemBox = b; }
            }

            // ---- the nearest doorway the ray goes through (open or shut, it is aimed at where it is)
            int door = -1; float doorS = float.PositiveInfinity;
            var dm = DoorManager.Active;
            if (dm != null)
                for (int i = 0; i < dm.Count; i++)
                {
                    var d = dm.Get(i);
                    if (d == null) continue;
                    float dx = d.Centre.x - start.x, dz = d.Centre.z - start.z;
                    if (dx * dx + dz * dz > len * len) continue;
                    if (!d.RayHit(start, direction, len, out float en, out _)) continue;
                    if (en > blockS + OccluderSlack) continue;
                    if (en < doorS) { doorS = en; door = i; }
                }

            if (item != null && (door < 0 || itemS <= doorS + 0.3f))
            {
                var cat = CategoryOf(item);
                target = new PingTarget { kind = PingTargetKind.Item, id = item.NetId, pos = itemBox.center + Vector3.up * (itemBox.extents.y + 0.1f), label = PingRules.NameOf(item.Label, cat, false, item.Value, item.Loot != null ? item.Loot.lifters : 1), category = cat, distance = itemS };
                return true;
            }
            if (door >= 0)
            {
                var d = dm.Get(door);
                target = new PingTarget
                {
                    kind = PingTargetKind.Door, id = door, pos = new Vector3(d.Centre.x, d.Centre.y + d.Height * 0.8f, d.Centre.z),
                    label = PingRules.NameOf(null, PingCategory.Door, d.Locked || d.ComputerLocked), category = PingCategory.Door, distance = doorS,
                };
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the markers

        void Place(int slot, PingTarget t, bool local)
        {
            var m = new Mark { slot = slot, target = t, at = Time.time };
            if (t.kind == PingTargetKind.Item)
            {
                m.item = Net.NetIds.Get<Pickup>(t.id);
                if (m.item) { m.offset = t.pos - m.item.transform.position; m.hadItem = true; }
            }
            else m.doors = DoorManager.Active;
            marks[slot] = m;
            shownFrom[Mathf.Clamp(slot, 0, shownFrom.Length - 1)]++;
            GameAudio.PlaySound(SoundId.Ping, Vector3.zero, local ? 0.5f : 0.7f, 1f + 0.06f * Mathf.Clamp(slot, 0, 5), false);
        }

        /// <summary>Takes the markers that have run out, whose item was taken or destroyed, or whose floor is gone.</summary>
        void Prune()
        {
            if (marks.Count == 0) return;
            List<int> gone = null;
            foreach (var kv in marks)
            {
                var m = kv.Value;
                bool over = Time.time - m.at >= PingRules.Seconds;
                if (!over && m.hadItem && !m.item) over = true;                                                                       // (destroyed: banked, sold, shattered)
                if (!over && m.item && (m.item.IsHeld || !m.item.isActiveAndEnabled || (m.item.Loot != null && m.item.Loot.Holders > 0))) over = true;     // (taken, stored, being carried)
                if (!over && m.target.kind == PingTargetKind.Door && m.doors != DoorManager.Active) over = true;
                if (over) (gone ?? (gone = new List<int>())).Add(kv.Key);
            }
            if (gone != null) foreach (var k in gone) marks.Remove(k);
        }

        static Texture2D glowTex;

        static Texture2D GlowTex()
        {
            if (glowTex) return glowTex;
            const int n = 48;
            glowTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "PingGlow", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - n * 0.5f + 0.5f, dy = y - n * 0.5f + 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / (n * 0.5f));
                    glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            glowTex.Apply();
            return glowTex;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || marks.Count == 0) return;
            var cam = Camera.main;
            if (!cam) return;
            if (OutpostUi.Open && OutpostUi.Current == OutpostUi.Kind.Intro) return;
            GUI.depth = 30;                                                           // (under the HUD, over the world)
            float s = HudTheme.S;
            var order = new List<Mark>(marks.Values);
            order.Sort((a, b) => (Where(b) - cam.transform.position).sqrMagnitude.CompareTo((Where(a) - cam.transform.position).sqrMagnitude));
            foreach (var m in order)
            {
                float age = Time.time - m.at;
                float alpha = PingRules.Alpha(age);
                if (alpha <= 0f) continue;
                var world = Where(m);
                var sp = cam.WorldToScreenPoint(world);
                var tint = HudTheme.PlayerColour(m.slot + 1);
                var colour = new Color(tint.r, tint.g, tint.b, alpha);
                float dist = Vector3.Distance(cam.transform.position, world);
                float pop = PingRules.Pop(age);
                if (sp.z <= 0.1f || sp.x < -20f || sp.x > Screen.width + 20f || sp.y < -20f || sp.y > Screen.height + 20f)
                {
                    // out of view: a marker on the edge of the screen that points the way to it
                    var dir = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f);
                    if (sp.z <= 0.1f) dir = -dir;
                    if (dir.sqrMagnitude < 4f) dir = Vector2.down;
                    float margin = 52f * s;
                    float k = Mathf.Min((Screen.width * 0.5f - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.x)), (Screen.height * 0.5f - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                    var at = new Vector2(Screen.width * 0.5f + dir.x * k, Screen.height * 0.5f - dir.y * k);
                    DrawDiamond(at, 15f * s * Mathf.Max(0.2f, pop), colour, PingRules.Sign(m.target.category), alpha, s);
                    HudTheme.Text(new Rect(at.x - 40f * s, at.y + 11f * s, 80f * s, 16f * s), $"{dist:0} m", 11f, new Color(1f, 1f, 1f, alpha * 0.9f), TextAnchor.MiddleCenter);
                    continue;
                }
                var centre = new Vector2(sp.x, Screen.height - sp.y);
                float d = 24f * s * pop;
                float glowSize = (96f + 16f * Mathf.Sin(Time.time * 5f)) * s * pop;
                var saved = GUI.color;
                GUI.color = new Color(tint.r, tint.g, tint.b, alpha * 0.55f);
                GUI.DrawTexture(new Rect(centre.x - glowSize * 0.5f, centre.y - glowSize * 0.5f, glowSize, glowSize), GlowTex());
                GUI.color = saved;
                // a ring that spreads out from it as it goes up
                if (age < 0.9f)
                {
                    float ring = (30f + 150f * (age / 0.9f)) * s;
                    HudTheme.Ring(new Rect(centre.x - ring * 0.5f, centre.y - ring * 0.5f, ring, ring), new Color(tint.r, tint.g, tint.b, alpha * (1f - age / 0.9f) * 0.9f), 3f * s, ring * 0.5f);
                }
                DrawDiamond(centre, d, colour, PingRules.Sign(m.target.category), alpha, s);
                if (pop < 0.6f) continue;
                string top = m.target.label;
                float tw = HudTheme.Width(top, 16f) + 24f * s, th = 28f * s;
                var chip = new Rect(centre.x - tw * 0.5f, centre.y - d * 0.5f - th - 8f * s, tw, th);
                HudTheme.Chip(chip, top, new Color(0.10f, 0.08f, 0.26f, alpha * 0.92f), 16f, new Color(1f, 1f, 1f, alpha));
                HudTheme.Text(new Rect(centre.x - 100f * s, centre.y + d * 0.5f + 4f * s, 200f * s, 18f * s), $"PLAYER {m.slot + 1}   {dist:0} m", 13f, new Color(colour.r, colour.g, colour.b, alpha), TextAnchor.MiddleCenter);
            }
        }

        /// <summary>Where a marker hangs now (an item's follows the item).</summary>
        static Vector3 Where(Mark m) => m.item ? m.item.transform.position + m.offset : m.target.pos;

        static void DrawDiamond(Vector2 centre, float d, Color colour, string sign, float alpha, float s)
        {
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, centre);
            HudTheme.Rounded(new Rect(centre.x - d * 0.5f - 3f * s, centre.y - d * 0.5f - 3f * s, d + 6f * s, d + 6f * s), new Color(0.07f, 0.05f, 0.18f, alpha * 0.95f), 4f * s);
            HudTheme.Rounded(new Rect(centre.x - d * 0.5f, centre.y - d * 0.5f, d, d), colour, 3f * s);
            GUI.matrix = saved;
            HudTheme.Text(new Rect(centre.x - d, centre.y - d * 0.5f, d * 2f, d), sign, Mathf.Max(10f, d / s * 0.72f), new Color(0.07f, 0.05f, 0.18f, alpha), TextAnchor.MiddleCenter, false);
        }
    }
}
