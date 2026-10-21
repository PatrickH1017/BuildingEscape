using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The loot scanner. The owner, 2026-10-17: "Players should be able to hold a button to scan rooms for loot" and "Scanning should only work within the room and show
    /// items in that room". Hold <see cref="ScanKey"/> (Z) and a pulse leaves the player and races over the floor of the room they are standing in (the wave follows the walls and
    /// stops at them); it marks every valuable lying in THAT room - not the rooms next to it, however near: a player has to go in to scan a room. A mark is a diamond and a
    /// label (what, how much, how far) hung where the thing lies - seen through desks and cabinets, the point of a scan - and fades after a few seconds; the top of the screen says
    /// which room it is and what is in it ("nothing to steal here" is worth knowing, too). Another pulse leaves every couple of seconds while the key is held. It is purely local: every
    /// machine has the same loot in the same places, so nothing is sent. Nothing is scanned at the docks, in the elevator car, with a screen open, while pinned or busted, or during the
    /// run-start cinematic. The rules are in <see cref="LootScanRules"/>.
    /// </summary>
    public sealed class LootScanner : MonoBehaviour
    {
        public const KeyCode ScanKey = KeyCode.Z;

        public static LootScanner Instance { get; private set; }
        /// <summary>Tests: the scan key is held.</summary>
        public static bool TestHold;
        /// <summary>The key is held and a scan is possible.</summary>
        public static bool Scanning { get; private set; }

        sealed class Find
        {
            public Component target;
            public Pickup pickup;
            public ScanKind kind;
            public float hitAt;
            public string label;
            public int value, tier, lifters;
        }

        readonly Dictionary<Component, Find> marks = new Dictionary<Component, Find>();
        readonly List<Component> candidates = new List<Component>();
        readonly List<Component> reached = new List<Component>();
        readonly List<Component> gone = new List<Component>();
        Vector3 origin;
        float pulseAge = -1f, sinceLast = -1f, reach, nextRoomCheck;
        bool pulsing, inCar, scannedRoom;
        RoomSpot room = new RoomSpot { index = -1 };
        int roomCount;
        long roomWorth;
        int roomOther;                 // (safes and unique items in the room: they have no worth in money)
        Transform ring;
        MeshFilter ringFilter;
        Mesh ringMesh;
        Material ringMat;
        Texture2D ringTex;
        AudioSource audioSource;
        AudioClip ping;
        FloorGenerator gen;

        /// <summary>The valuables marked now (for tests and the HUD).</summary>
        public int MarkCount => marks.Count;
        /// <summary>How many pulses have left in this session.</summary>
        public int Pulses { get; private set; }
        /// <summary>The room the player is in (index -1: none, or the elevator car).</summary>
        public RoomSpot Room => room;
        /// <summary>How many valuables the last pulse found in the room, and what they are worth.</summary>
        public int RoomCount => roomCount;
        public long RoomWorth => roomWorth;
        public bool InElevator => inCar;
        public bool IsMarked(Pickup p) => p && marks.ContainsKey(p);
        /// <summary>What kind a marked thing is (for tests).</summary>
        public bool TryGetKind(Component c, out ScanKind kind)
        {
            if (c && marks.TryGetValue(c, out var m)) { kind = m.kind; return true; }
            kind = ScanKind.Valuable;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; TestHold = false; Scanning = false; }

        void Awake() { Instance = this; }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Scanning = false;
            if (ring) Destroy(ring.gameObject);
            if (ringMesh) Destroy(ringMesh);
            if (ringMat) Destroy(ringMat);
            if (ringTex) Destroy(ringTex);
            if (ping) Destroy(ping);
        }

        static bool ScreenOpen => Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open || ElevatorPanel.Open || OutpostUi.Open || TestMode.PanelOpen;

        static Transform LocalPlayer()
        {
            var np = Net.NetPlayer.Local;
            if (np) return np.transform;
            return WobblyPlayerRegistry.All.Count > 0 && WobblyPlayerRegistry.All[0] ? WobblyPlayerRegistry.All[0].transform : null;
        }

        void Update()
        {
            var hm = HeistManager.Instance;
            var me = LocalPlayer();
            bool inCustody = me && me.TryGetComponent(out WobblyPlayer wp) && wp.InCustody;
            bool can = hm != null && me != null && LootScanRules.CanScan(hm.Floor, ScreenOpen, inCustody, IntroCinematic.Playing);
            bool holding = can && (Input.GetKey(ScanKey) || TestHold);
            Scanning = holding;

            if (can && (Time.time >= nextRoomCheck || holding && sinceLast < 0f)) { nextRoomCheck = Time.time + 0.15f; RefreshRoom(me); }
            if (hm == null || hm.Floor < 1) { room = new RoomSpot { index = -1 }; ClearMarks(); StopPulse(); }

            float dt = Time.unscaledDeltaTime;
            if (sinceLast >= 0f) sinceLast += dt;
            if (LootScanRules.NextPulse(holding, sinceLast)) StartPulse(me);
            if (pulsing) AdvancePulse(dt);
            UpdateRing();
            Prune();
        }

        /// <summary>Which room is the player in (the elevator car is none: it lies inside its lobby, but it is a room of its own to the player)? Another room: the old marks go.</summary>
        void RefreshRoom(Transform me)
        {
            gen = gen ? gen : FindFirstObjectByType<FloorGenerator>();
            var spot = new RoomSpot { index = -1 };
            var car = ElevatorExtraction.Current;
            inCar = car && car.TryGetComponent(out Collider cc) && cc.bounds.Contains(me.position);
            if (gen && !inCar) spot = gen.RoomSpotAt(me.position);
            if (spot.index != room.index) { ClearMarks(); StopPulse(); sinceLast = -1f; scannedRoom = false; }        // (a new room: a new pulse at once, not after the gap)
            room = spot;
        }

        void ClearMarks() { marks.Clear(); candidates.Clear(); }

        void StopPulse() { pulsing = false; pulseAge = -1f; if (sinceLast > LootScanRules.PulseGap) sinceLast = -1f; }

        void StartPulse(Transform me)
        {
            sinceLast = 0f;
            roomCount = 0; roomWorth = 0; roomOther = 0;
            pulsing = false; candidates.Clear();
            if (!room.Valid || !gen) return;                           // (nothing to scan: the elevator car, a doorway between nothing)
            origin = me.position;
            pulseAge = 0f; pulsing = true; scannedRoom = true;
            reach = LootScanRules.Reach(origin.x, origin.z, room.bounds) + 0.6f;
            Pulses++;
            foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
            {
                if (!Scannable(p)) continue;
                var at = p.transform.position;
                if (!LootScanRules.SameRoom(room.index, gen.RoomSpotAt(at).index)) continue;          // (only what lies in this room)
                candidates.Add(p);
                int kind = FloorChecklist.Classify(p);
                if (kind == (int)ScanKind.Valuable || kind == (int)ScanKind.Cash) { roomCount++; roomWorth += p.Value; } else roomOther++;
            }
            // the safes that are still shut glow red (the owner, 2026-10-19)
            foreach (var safe in FloorSafe.OnShow)
            {
                if (!safe || safe.IsOpen || !LootScanRules.SameRoom(room.index, gen.RoomSpotAt(safe.transform.position).index)) continue;
                candidates.Add(safe); roomOther++;
            }
            var gunSafe = GunSafeProp.Active;
            if (gunSafe && !gunSafe.IsOpen && LootScanRules.SameRoom(room.index, gen.RoomSpotAt(gunSafe.transform.position).index)) { candidates.Add(gunSafe); roomOther++; }
            candidates.Sort((a, b) => (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f; audioSource.playOnAwake = false;
            }
            if (!ping) ping = MakePing();
            audioSource.PlayOneShot(ping, 0.3f);
        }

        /// <summary>Something a scan can mark: a pickup worth something that is lying about (not in a pack, not in a hand, not in the elevator car).</summary>
        static bool Scannable(Pickup p)
        {
            if (!p || !p.isActiveAndEnabled || p.IsHeld || FloorChecklist.Classify(p) < 0) return false;          // (valuables, cash, weapons and armour: not a spare clip or a mug)
            if (p.Loot && p.Loot.Broken) return false;
            var car = ElevatorExtraction.Current;
            if (car && car.TryGetComponent(out Collider cc) && cc.bounds.Contains(p.transform.position)) return false;      // (about to be banked)
            return true;
        }

        void AdvancePulse(float dt)
        {
            pulseAge += dt;
            float radius = LootScanRules.RadiusAt(pulseAge);
            reached.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                var p = candidates[i];
                if (!p) { candidates.RemoveAt(i); i--; continue; }
                if (!LootScanRules.Reached(Vector3.Distance(origin, p.transform.position), radius)) break;       // (sorted: the rest are further out)
                reached.Add(p);
                candidates.RemoveAt(i); i--;
            }
            foreach (var p in reached) MarkOne(p);
            if (radius >= reach) { pulsing = false; candidates.Clear(); }
        }

        void MarkOne(Component c)
        {
            if (!marks.TryGetValue(c, out var m))
            {
                var hm = HeistManager.Instance;
                var p = c as Pickup;
                if (p != null)
                {
                    int kind = FloorChecklist.Classify(p);
                    m = new Find { target = c, pickup = p, label = p.Label, value = p.Value, lifters = p.Loot ? p.Loot.lifters : 1, kind = kind >= 0 ? (ScanKind)kind : ScanKind.Valuable };
                    m.tier = LootScanRules.Tier(m.value, hm ? Mathf.RoundToInt((float)(hm.Quota / TowerRules.QuotaMultiplier)) : TowerRules.FirstQuota);       // (the tiers stay calibrated on the old quotas)
                    if (p.Loot) m.tier = Mathf.Max(m.tier, 2);
                    if (m.kind == ScanKind.Unique || m.kind == ScanKind.Cash) m.tier = Mathf.Max(m.tier, 1);
                }
                else m = new Find { target = c, label = "Safe", kind = ScanKind.Safe, lifters = 1, tier = 2 };
                marks[c] = m;
            }
            m.hitAt = Time.time;
            if (m.pickup) m.value = m.pickup.Value;
        }

        void Prune()
        {
            if (marks.Count == 0) return;
            gone.Clear();
            foreach (var kv in marks)
            {
                var c = kv.Key;
                bool lost = !c || Time.time - kv.Value.hitAt >= LootScanRules.MarkSeconds;
                if (!lost && c is Pickup pick) lost = !pick.isActiveAndEnabled || pick.IsHeld || (pick.Loot && pick.Loot.Broken);
                else if (!lost && c is FloorSafe safe) lost = safe.IsOpen;                                   // (a safe that is open no longer glows)
                else if (!lost && c is GunSafeProp gunSafe) lost = gunSafe.IsOpen;
                if (lost) gone.Add(c);
            }
            foreach (var c in gone) marks.Remove(c);
        }

        // ------------------------------------------------------------------ the wave on the floor (it follows the walls of the room and stops at them)

        const int RingSegments = 96;
        const float RingWidth = 0.45f, WallInset = 0.18f;

        void UpdateRing()
        {
            if (!pulsing)
            {
                if (ring && ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
                return;
            }
            if (!ring) MakeRing();
            ring.gameObject.SetActive(true);
            float radius = LootScanRules.RadiusAt(pulseAge);
            var b = room.bounds;
            float y = Mathf.Max(0.04f, origin.y - 0.9f + 0.05f);
            float x0 = b.x0 + WallInset, x1 = b.x1 - WallInset, z0 = b.z0 + WallInset, z1 = b.z1 - WallInset;
            var verts = new Vector3[(RingSegments + 1) * 2];
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i <= RingSegments; i++)
            {
                float a = i / (float)RingSegments * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                // the outer edge is where the pulse has got to, the inner edge a little behind it; both are held inside the room's walls
                var outer = new Vector3(Mathf.Clamp(origin.x + dir.x * radius, x0, x1), y, Mathf.Clamp(origin.z + dir.y * radius, z0, z1));
                float r0 = Mathf.Max(0f, radius - RingWidth);
                var inner = new Vector3(Mathf.Clamp(origin.x + dir.x * r0, x0, x1), y, Mathf.Clamp(origin.z + dir.y * r0, z0, z1));
                verts[i * 2] = inner; verts[i * 2 + 1] = outer;
                uvs[i * 2] = new Vector2(i / (float)RingSegments, 0f); uvs[i * 2 + 1] = new Vector2(i / (float)RingSegments, 1f);
            }
            ringMesh.Clear();
            ringMesh.vertices = verts; ringMesh.uv = uvs;
            ringMesh.triangles = RingTriangles;
            ringMesh.bounds = new Bounds(new Vector3(origin.x, y, origin.z), new Vector3(80f, 2f, 80f));
            float fade = Mathf.Clamp01(1f - radius / (reach + 0.5f));
            ringMat.color = new Color(0.55f, 0.95f, 1f, 0.35f + 0.65f * fade);
        }

        static int[] ringTriangles;
        static int[] RingTriangles
        {
            get
            {
                if (ringTriangles != null) return ringTriangles;
                var t = new int[RingSegments * 12];
                for (int i = 0; i < RingSegments; i++)
                {
                    int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
                    int k = i * 12;
                    t[k] = a; t[k + 1] = b; t[k + 2] = c; t[k + 3] = c; t[k + 4] = b; t[k + 5] = d;           // seen from above
                    t[k + 6] = a; t[k + 7] = c; t[k + 8] = b; t[k + 9] = c; t[k + 10] = d; t[k + 11] = b;     // and from below
                }
                return ringTriangles = t;
            }
        }

        void MakeRing()
        {
            var go = new GameObject("ScanWave");
            ringFilter = go.AddComponent<MeshFilter>();
            ringMesh = new Mesh { name = "ScanWave" };
            ringMesh.MarkDynamic();
            ringFilter.sharedMesh = ringMesh;
            ringTex = MakeRingTexture();
            ringMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = ringTex, hideFlags = HideFlags.HideAndDontSave };
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = ringMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ring = go.transform;
        }

        /// <summary>Across the band: nothing at the trailing edge, a bright line at the front.</summary>
        static Texture2D MakeRingTexture()
        {
            const int h = 32;
            var px = new Color32[h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                float a = Mathf.Pow(v, 2.5f);
                if (v > 0.92f) a = 1f;
                px[y] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            var t = new Texture2D(1, h, TextureFormat.RGBA32, false) { name = "ScanWave", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }

        /// <summary>A sonar ping: a short rising sweep with an echo.</summary>
        static AudioClip MakePing()
        {
            const int rate = 44100;
            int n = rate * 55 / 100;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate, u = i / (float)n;
                float f = Mathf.Lerp(520f, 1040f, Mathf.Min(1f, t / 0.18f));
                float env = Mathf.Exp(-t * 7f);
                float echo = i > rate / 5 ? Mathf.Exp(-(t - 0.2f) * 9f) * 0.35f : 0f;
                d[i] = Mathf.Sin(6.2831853f * f * t) * env * 0.6f + Mathf.Sin(6.2831853f * 1040f * t) * echo * 0.4f;
                d[i] *= Mathf.Clamp01(t / 0.004f) * Mathf.Clamp01((1f - u) / 0.1f);
            }
            var c = AudioClip.Create("ScanPing", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }

        // ------------------------------------------------------------------ on screen

        /// <summary>The glow of a kind: safes and valuables gold, unique items (weapons) red, cash green (the owner, 2026-10-20).</summary>
        static Color KindColour(ScanKind k)
        {
            ChecklistRules.Rgb(k, out float r, out float g, out float b);
            return new Color(r, g, b);
        }

        static Texture2D glowTex;
        static Texture2D GlowTex()
        {
            if (glowTex) return glowTex;
            const int n = 64;
            glowTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "ScanGlow", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
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

        static readonly Color[] TierColour =
        {
            new Color(0.93f, 0.95f, 1.00f), new Color(0.40f, 0.95f, 0.55f), new Color(0.40f, 0.72f, 1.00f), new Color(1.00f, 0.80f, 0.20f)
        };

        /// <summary>What the top of the screen says (also for tests): which room, and what is in it.</summary>
        public string StatusLine
        {
            get
            {
                if (inCar) return "THE ELEVATOR   step out to scan a room";
                if (!room.Valid) return "NO ROOM HERE   step inside a room to scan it";
                string title = LootScanRules.RoomTitle(room);
                if (scannedRoom)
                    return roomCount > 0 ? $"SCANNING {title}   {roomCount} valuable{(roomCount == 1 ? "" : "s")} here, about {HudTheme.Money((int)System.Math.Min(roomWorth, int.MaxValue))}{(roomOther > 0 ? $" + {roomOther} item{(roomOther == 1 ? "" : "s")}" : "")}"
                        : roomOther > 0 ? $"SCANNING {title}   {roomOther} item{(roomOther == 1 ? "" : "s")} here" : $"SCANNING {title}   nothing to steal here";
                return $"SCANNING {title}...";
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var cam = Camera.main;
            if (!cam) return;
            bool any = marks.Count > 0;
            if (!any && !Scanning) return;
            if (HeistManager.Instance == null || HeistManager.Instance.Floor < 1) return;

            float s = HudTheme.S;
            int offScreen = 0, shown = 0;
            var edges = new List<(Vector2 at, Color colour, float dist)>();
            var order = new List<Find>(marks.Values);
            order.Sort((a, b) => (b.target.transform.position - cam.transform.position).sqrMagnitude.CompareTo((a.target.transform.position - cam.transform.position).sqrMagnitude));
            foreach (var m in order)
            {
                if (!m.target) continue;
                float age = Time.time - m.hitAt;
                float alpha = LootScanRules.Alpha(age);
                if (alpha <= 0f) continue;
                var world = m.target.transform.position + Vector3.up * 0.12f;
                var sp = cam.WorldToScreenPoint(world);
                if (sp.z <= 0.1f || sp.x < -20f || sp.x > Screen.width + 20f || sp.y < -20f || sp.y > Screen.height + 20f)
                {
                    offScreen++;
                    if (edges.Count < 14)
                    {
                        // an arrow-head on the edge of the screen that points the way to it (behind the camera: the opposite way, so it points round the side)
                        var dir = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f);
                        if (sp.z <= 0.1f) dir = -dir;
                        if (dir.sqrMagnitude < 4f) dir = Vector2.down;
                        float margin = 46f * s;
                        float k = Mathf.Min((Screen.width * 0.5f - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.x)), (Screen.height * 0.5f - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                        var at = new Vector2(Screen.width * 0.5f + dir.x * k, Screen.height * 0.5f - dir.y * k);
                        var tc = KindColour(m.kind);
                        edges.Add((at, new Color(tc.r, tc.g, tc.b, alpha), Vector3.Distance(cam.transform.position, world)));
                    }
                    continue;
                }
                if (shown >= 28) continue;                                  // (a very crowded room shows the nearest ones: the far ones are drawn first, so these are skipped from the far end)
                shown++;
                float pop = LootScanRules.Pop(age);
                float dist = Vector3.Distance(cam.transform.position, world);
                var colour = KindColour(m.kind);
                var c = new Color(colour.r, colour.g, colour.b, alpha);
                var centre = new Vector2(sp.x, Screen.height - sp.y);
                float d = (m.tier >= 3 ? 20f : m.tier == 2 ? 17f : 14f) * s * pop;
                // the glow in the colour of its kind (seen through walls, like the diamond): gold safes, red unique items, green cash, gold valuables
                float glowSize = (74f + 20f * Mathf.Sin(Time.time * 4f + centre.x * 0.01f)) * s * pop;
                var glowSaved = GUI.color;
                GUI.color = new Color(colour.r, colour.g, colour.b, alpha * 0.8f);
                GUI.DrawTexture(new Rect(centre.x - glowSize * 0.5f, centre.y - glowSize * 0.5f, glowSize, glowSize), GlowTex());
                GUI.color = glowSaved;
                // the diamond
                var saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, centre);
                HudTheme.Rounded(new Rect(centre.x - d * 0.5f - 2f * s, centre.y - d * 0.5f - 2f * s, d + 4f * s, d + 4f * s), new Color(0.07f, 0.05f, 0.18f, alpha * 0.9f), 3f * s);
                HudTheme.Rounded(new Rect(centre.x - d * 0.5f, centre.y - d * 0.5f, d, d), c, 2f * s);
                GUI.matrix = saved;
                if (pop < 0.6f) continue;
                // the label: what it is and what it is worth, and below it how far
                string top = m.value <= 0 ? m.label : m.lifters > 1 ? $"{m.label}  {HudTheme.Money(m.value)}  (x{m.lifters} carry)" : $"{m.label}  {HudTheme.Money(m.value)}";
                float tw = HudTheme.Width(top, 15f) + 22f * s, th = 26f * s;
                var chip = new Rect(centre.x - tw * 0.5f, centre.y - d * 0.5f - th - 6f * s, tw, th);
                HudTheme.Chip(chip, top, new Color(0.10f, 0.08f, 0.26f, alpha * 0.92f), 15f, new Color(c.r, c.g, c.b, alpha));
                HudTheme.Text(new Rect(centre.x - 80f * s, centre.y + d * 0.5f + 3f * s, 160f * s, 18f * s), $"{dist:0} m", 12f, new Color(1f, 1f, 1f, alpha * 0.9f), TextAnchor.MiddleCenter);
            }
            foreach (var e in edges)
            {
                float d = 12f * s;
                var saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, e.at);
                HudTheme.Rounded(new Rect(e.at.x - d * 0.5f - 2f * s, e.at.y - d * 0.5f - 2f * s, d + 4f * s, d + 4f * s), new Color(0.07f, 0.05f, 0.18f, e.colour.a * 0.9f), 2.5f * s);
                HudTheme.Rounded(new Rect(e.at.x - d * 0.5f, e.at.y - d * 0.5f, d, d), e.colour, 2f * s);
                GUI.matrix = saved;
                HudTheme.Text(new Rect(e.at.x - 30f * s, e.at.y + d * 0.5f + 3f * s, 60f * s, 16f * s), $"{e.dist:0} m", 11f, new Color(1f, 1f, 1f, e.colour.a * 0.9f), TextAnchor.MiddleCenter);
            }

            // the readout at the top: which room and what is in it; and what lies behind you
            if (Scanning || any)
            {
                string line = Scanning ? StatusLine : $"{marks.Count} thing{(marks.Count == 1 ? "" : "s")} marked";
                float w = HudTheme.Width(line, 17f) + 44f * s;
                var r = new Rect((Screen.width - w) * 0.5f, 86f * s, w, 34f * s);
                bool nothing = Scanning && (inCar || !room.Valid || (scannedRoom && roomCount + roomOther == 0));
                HudTheme.Pill(r, nothing ? new Color(0.35f, 0.30f, 0.50f, 0.95f) : new Color(0.10f, 0.45f, 0.62f, 0.95f), 2.5f, 2.5f);
                HudTheme.Text(r, line, 17f, HudTheme.Bright, TextAnchor.MiddleCenter, false);
                if (offScreen > 0)
                {
                    string more = $"+{offScreen} more out of view";
                    float mw = HudTheme.Width(more, 14f) + 30f * s;
                    var rr = new Rect((Screen.width - mw) * 0.5f, r.yMax + 6f * s, mw, 26f * s);
                    HudTheme.Pill(rr, new Color(0.12f, 0.10f, 0.30f, 0.9f), 2f, 2f);
                    HudTheme.Text(rr, more, 14f, HudTheme.Cream, TextAnchor.MiddleCenter, false);
                }
            }
        }
    }
}
