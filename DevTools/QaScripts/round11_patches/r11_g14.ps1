# Round 11, batch G14 (the owner, 2026-10-19):
#  - "On the journal should be a tab that has check boxes and a list: All valuables collected / All unique items collected / All safes opened / All cash collected. After collecting all of one of those the check box after the text should get
#    checked (green check) and the text should get a line through it crossing it out (green color)"
#  - "The scan should make safes glow red, unique items purple (for example a weapon), cash green, valuables gold"
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'

# the new files: CRLF, no BOM
if (-not $env:R2_DRY) {
  Copy-Item 'C:\bqs\r11_draft\FloorChecklist.cs' "$Src\FloorChecklist.cs" -Force
  Copy-Item 'C:\bqs\r11_draft\NetDevChecklistTest.cs' "$Ed\NetDevChecklistTest.cs" -Force
  Copy-Item 'C:\bqs\r11_draft\NetDevChecklist.cs' "$Ed\NetDevChecklist.cs" -Force
  foreach ($p in "$Src\FloorChecklist.cs", "$Ed\NetDevChecklistTest.cs", "$Ed\NetDevChecklist.cs") {
    $t = [IO.File]::ReadAllText($p)
    [IO.File]::WriteAllText($p, ($t -replace "`r?`n", "`r`n"), (New-Object Text.UTF8Encoding($false)))
  }
}

# ======================================================================== the scan: safes, unique items, cash and valuables, each with its glow
Open-Src "$Src\LootScanner.cs"
Rep 'public Pickup pickup;' @'
public Component target;
            public Pickup pickup;
            public ScanKind kind;
'@
Rep 'readonly Dictionary<Pickup, Find> marks = new Dictionary<Pickup, Find>();' 'readonly Dictionary<Component, Find> marks = new Dictionary<Component, Find>();'
Rep 'readonly List<Pickup> candidates = new List<Pickup>();' 'readonly List<Component> candidates = new List<Component>();'
Rep 'readonly List<Pickup> reached = new List<Pickup>();' 'readonly List<Component> reached = new List<Component>();'
Rep 'readonly List<Pickup> gone = new List<Pickup>();' 'readonly List<Component> gone = new List<Component>();'
Rep @'
        long roomWorth;
'@ @'
        long roomWorth;
        int roomOther;                 // (safes and unique items in the room: they have no worth in money)
'@
Rep @'
        public bool IsMarked(Pickup p) => p && marks.ContainsKey(p);
'@ @'
        public bool IsMarked(Pickup p) => p && marks.ContainsKey(p);
        /// <summary>What kind a marked thing is (for tests).</summary>
        public bool TryGetKind(Component c, out ScanKind kind)
        {
            if (c && marks.TryGetValue(c, out var m)) { kind = m.kind; return true; }
            kind = ScanKind.Valuable;
            return false;
        }
'@
Rep 'roomCount = 0; roomWorth = 0;' 'roomCount = 0; roomWorth = 0; roomOther = 0;'
RepRx '(?s)foreach \(var p in FindObjectsByType<Pickup>\(FindObjectsSortMode\.None\)\)\s*\{\s*if \(!Scannable\(p\)\) continue;.*?roomCount\+\+; roomWorth \+= p\.Value;\s*\}' @'
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
'@
RepRx '(?s)static bool Scannable\(Pickup p\)\s*\{.*?return true;\s*\}' @'
static bool Scannable(Pickup p)
        {
            if (!p || !p.isActiveAndEnabled || p.IsHeld || FloorChecklist.Classify(p) < 0) return false;          // (valuables, cash, weapons and armour: not a spare clip or a mug)
            if (p.Loot && p.Loot.Broken) return false;
            var car = ElevatorExtraction.Current;
            if (car && car.TryGetComponent(out Collider cc) && cc.bounds.Contains(p.transform.position)) return false;      // (about to be banked)
            return true;
        }
'@
RepRx '(?s)void MarkOne\(Pickup p\)\s*\{.*?m\.value = p\.Value;\s*\}' @'
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
'@
RepRx '(?s)void Prune\(\)\s*\{.*?foreach \(var p in gone\) marks\.Remove\(p\);\s*\}' @'
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
'@
Rep @'
        static readonly Color[] TierColour =
'@ @'
        /// <summary>The glow of a kind: safes red, unique items purple, cash green, valuables gold (the owner, 2026-10-19).</summary>
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
'@
Rep @'
                    return roomCount > 0 ? $"SCANNING {title}   {roomCount} valuable{(roomCount == 1 ? "" : "s")} here, about {HudTheme.Money((int)System.Math.Min(roomWorth, int.MaxValue))}" : $"SCANNING {title}   nothing to steal here";
'@ @'
                    return roomCount > 0 ? $"SCANNING {title}   {roomCount} valuable{(roomCount == 1 ? "" : "s")} here, about {HudTheme.Money((int)System.Math.Min(roomWorth, int.MaxValue))}{(roomOther > 0 ? $" + {roomOther} item{(roomOther == 1 ? "" : "s")}" : "")}"
                        : roomOther > 0 ? $"SCANNING {title}   {roomOther} item{(roomOther == 1 ? "" : "s")} here" : $"SCANNING {title}   nothing to steal here";
'@
Rep 'order.Sort((a, b) => (b.pickup.transform.position - cam.transform.position).sqrMagnitude.CompareTo((a.pickup.transform.position - cam.transform.position).sqrMagnitude));' 'order.Sort((a, b) => (b.target.transform.position - cam.transform.position).sqrMagnitude.CompareTo((a.target.transform.position - cam.transform.position).sqrMagnitude));'
Rep 'if (!m.pickup) continue;' 'if (!m.target) continue;'
Rep 'var world = m.pickup.transform.position + Vector3.up * 0.12f;' 'var world = m.target.transform.position + Vector3.up * 0.12f;'
Rep 'var tc = TierColour[Mathf.Clamp(m.tier, 0, 3)];' 'var tc = KindColour(m.kind);'
Rep 'var colour = TierColour[Mathf.Clamp(m.tier, 0, 3)];' 'var colour = KindColour(m.kind);'
Rep @'
                // the diamond
'@ @'
                // the glow in the colour of its kind (seen through walls, like the diamond): red safes, purple unique items, green cash, gold valuables
                float glowSize = (74f + 20f * Mathf.Sin(Time.time * 4f + centre.x * 0.01f)) * s * pop;
                var glowSaved = GUI.color;
                GUI.color = new Color(colour.r, colour.g, colour.b, alpha * 0.8f);
                GUI.DrawTexture(new Rect(centre.x - glowSize * 0.5f, centre.y - glowSize * 0.5f, glowSize, glowSize), GlowTex());
                GUI.color = glowSaved;
                // the diamond
'@
Rep 'string top = m.lifters > 1 ?' 'string top = m.value <= 0 ? m.label : m.lifters > 1 ?'
Rep '(scannedRoom && roomCount == 0)' '(scannedRoom && roomCount + roomOther == 0)'
Rep '$"{marks.Count} valuable{(marks.Count == 1 ? "" : "s")} marked"' '$"{marks.Count} thing{(marks.Count == 1 ? "" : "s")} marked"'
Save-Src

# ======================================================================== the journal's checklist tab
Open-Src "$Src\Outpost\IntelScreens.cs"
Rep @'
        readonly System.Collections.Generic.List<SafeIntel.Entry> journalShown
'@ @'
        int journalTab;                 // (0 the safe notes, 1 the checklist: the owner, 2026-10-19)
        /// <summary>Which tab of the journal is up (for tests).</summary>
        public int JournalTab { get => journalTab; set => journalTab = value; }
        readonly System.Collections.Generic.List<SafeIntel.Entry> journalShown
'@
Rep @'
                    openedFrame = Time.frameCount;
'@ @'
                    openedFrame = Time.frameCount;
                    journalTab = 0;
'@
RepRegion 'void DrawJournal()' 'if (MenuKit.Button(new Rect(r.xMax - 226f * s, r.yMax - 62f * s, 200f * s, 44f * s), "CLOSE  (J / ESC)", HudTheme.Info, 17f)) OutpostUi.Hide(OutpostUi.Kind.Journal);' @'
void DrawJournal()
        {
            MenuKit.Begin();
            SafeIntel.OnFloor(HeistManager.Instance != null ? HeistManager.Instance.Floor : 0, journalShown);          // (the floor he is on and no other: the owner, 2026-10-18)
            var entries = journalShown;
            float s = HudTheme.S;
            float w = 760f * s;
            bool checklist = journalTab == 1;
            int rowsShown = checklist ? ChecklistRules.Count : Mathf.Max(1, entries.Count);
            float h = Mathf.Min(Screen.height - 80f, (276f + rowsShown * 46f) * s);
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            if (Event.current.type == EventType.Repaint)
            {
                MenuKit.Dim(0.35f);
                HudTheme.Panel(r, 16f);
                HudTheme.Text(new Rect(r.x + 28f * s, r.y + 18f * s, w - 56f * s, 40f * s), checklist ? "CHECKLIST  -  THIS FLOOR" : "NOTES  -  FLOOR SAFES", 26f, HudTheme.Bright, TextAnchor.MiddleLeft);
                HudTheme.Rounded(new Rect(r.x + 30f * s, r.y + 62f * s, 56f * s, Mathf.Max(3f, 4f * s)), HudTheme.Quota, 1f);
                float y = r.y + 126f * s;
                if (checklist) DrawChecklist(r, y, s);
                else if (entries.Count == 0)
                {
                    MenuKit.Wrapped(new Rect(r.x + 28f * s, y, w - 56f * s, 110f * s),
                        "Nothing yet. Every floor has a safe. The guard outposts (the rooms with a long dark window, locked with a keypad) " +
                        "keep a record of the floor's safes on their computer: hack the lock, then log in at the computer (the log-in is on a post-it on its monitor).", 20f * s, HudTheme.Muted);
                }
                else
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        var row = new Rect(r.x + 24f * s, y, w - 48f * s, 42f * s);
                        MenuKit.Plate(row, 0.22f);
                        HudTheme.Text(new Rect(row.x + 14f * s, row.y, row.width - 28f * s, row.height), SafeNotes.JournalLine(e.floor, e.index, e.count, e.room, e.code), 21f,
                            e.KnowsCode ? HudTheme.Cash : HudTheme.Quota, TextAnchor.MiddleLeft, false);
                        y += 46f * s;
                    }
                    MenuKit.Wrapped(new Rect(r.x + 28f * s, y + 8f * s, w - 56f * s, 70f * s), "A safe with no code is pried open with a crowbar (the crowbar may snap).", 18f * s, HudTheme.Dim);
                }
            }
            // the two tabs (a button answers in every kind of event, so they sit outside the paint)
            if (MenuKit.Button(new Rect(r.x + 28f * s, r.y + 74f * s, 190f * s, 36f * s), "SAFE NOTES", checklist ? HudTheme.Dim : HudTheme.Info, 16f)) journalTab = 0;
            if (MenuKit.Button(new Rect(r.x + 226f * s, r.y + 74f * s, 190f * s, 36f * s), "CHECKLIST", checklist ? HudTheme.Info : HudTheme.Dim, 16f)) journalTab = 1;
            if (MenuKit.Button(new Rect(r.xMax - 226f * s, r.yMax - 62f * s, 200f * s, 44f * s), "CLOSE  (J / ESC)", HudTheme.Info, 17f)) OutpostUi.Hide(OutpostUi.Kind.Journal);
        }

        /// <summary>
        /// The checklist (the owner, 2026-10-19): a line for each kind of thing on the floor - the text, then a check box. When all of a kind is collected the box is ticked in green and the text goes green with a green line through it. Nothing is tracked at the docks.
        /// </summary>
        void DrawChecklist(Rect r, float y, float s)
        {
            int floor = HeistManager.Instance != null ? HeistManager.Instance.Floor : 0;
            for (int i = 0; i < ChecklistRules.Count; i++)
            {
                var kind = (ScanKind)i;
                bool done = floor >= 1 && FloorChecklistTracker.IsDone(kind);
                string label = ChecklistRules.Label(kind);
                var row = new Rect(r.x + 24f * s, y, r.width - 48f * s, 42f * s);
                MenuKit.Plate(row, 0.22f);
                var text = new Rect(row.x + 14f * s, row.y, row.width - 28f * s, row.height);
                HudTheme.Text(text, label, 21f, done ? HudTheme.Cash : HudTheme.Bright, TextAnchor.MiddleLeft, false);
                float tw = HudTheme.Width(label, 21f);
                if (done) DrawLine(new Vector2(text.x, row.center.y), new Vector2(text.x + tw, row.center.y), 3f * s, HudTheme.Cash);
                var box = new Rect(text.x + tw + 18f * s, row.center.y - 15f * s, 30f * s, 30f * s);
                HudTheme.Rounded(box, done ? HudTheme.Cash : HudTheme.Ink, 7f * s);
                HudTheme.Rounded(new Rect(box.x + 3f * s, box.y + 3f * s, box.width - 6f * s, box.height - 6f * s), HudTheme.Track, 5f * s);
                if (done)
                {
                    var c = box.center;
                    DrawLine(new Vector2(c.x - 8f * s, c.y + 0f * s), new Vector2(c.x - 2.5f * s, c.y + 6f * s), 4f * s, HudTheme.Cash);
                    DrawLine(new Vector2(c.x - 2.5f * s, c.y + 6f * s), new Vector2(c.x + 8.5f * s, c.y - 7f * s), 4f * s, HudTheme.Cash);
                }
                y += 46f * s;
            }
        }

        /// <summary>A straight line of a given thickness between two points on screen.</summary>
        static void DrawLine(Vector2 a, Vector2 b, float thickness, Color c)
        {
            var mid = (a + b) * 0.5f;
            float len = (b - a).magnitude, angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, mid);
            HudTheme.Rounded(new Rect(mid.x - len * 0.5f, mid.y - thickness * 0.5f, len, thickness), c, thickness * 0.5f);
            GUI.matrix = saved;
'@
Save-Src

# ======================================================================== the tests learn the new kinds
Open-Src "$Ed\NetDevEtr.cs"
Rep 'p.Value >= LootScanRules.MinValue && !(carBox' 'FloorChecklist.Classify(p) >= 0 && !(carBox'
Rep @'
            var all = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(scannable).ToList();
'@ @'
            var all = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(scannable).ToList();
            // (the scan marks the shut safes of the room as well, the owner, 2026-10-19: they glow red)
            System.Func<int, int> safesIn = idx => FloorSafe.OnShow.Count(s => s && !s.IsOpen && gen.RoomSpotAt(s.transform.position).index == idx) + (GunSafeProp.Active && !GunSafeProp.Active.IsOpen && gen.RoomSpotAt(GunSafeProp.Active.transform.position).index == idx ? 1 : 0);
'@
Rep '&& scanner.MarkCount == inA.Count,' '&& scanner.MarkCount == inA.Count + safesIn(roomA.index),'
Rep 'scanner.StatusLine.Contains(inA.Count + " valuable")' 'scanner.StatusLine.Contains(inA.Count(p => FloorChecklist.Classify(p) == (int)ScanKind.Valuable || FloorChecklist.Classify(p) == (int)ScanKind.Cash) + " valuable")'
Rep 'scanner.MarkCount == inB.Count &&' 'scanner.MarkCount == inB.Count + safesIn(roomB.index) &&'
Save-Src

Open-Src "$Src\QaAutopilot.cs"
Rep 'var inRoom = loot.Where(p => gen.RoomSpotAt(p.transform.position).index == spot.index).ToList();' @'
var inRoom = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => FloorChecklist.Classify(p) >= 0 && p.isActiveAndEnabled && !p.IsHeld && gen.RoomSpotAt(p.transform.position).index == spot.index).ToList();
                int safesHere = FloorSafe.OnShow.Count(sf => sf && !sf.IsOpen && gen.RoomSpotAt(sf.transform.position).index == spot.index) + (GunSafeProp.Active && !GunSafeProp.Active.IsOpen && gen.RoomSpotAt(GunSafeProp.Active.transform.position).index == spot.index ? 1 : 0);
'@
Rep 'onlyThose = scanner.MarkCount == inRoom.Count;' 'onlyThose = scanner.MarkCount == inRoom.Count + safesHere;'
Save-Src

# ---- registered like the others
Open-Src "$Ed\NetDevTest.cs"
Rep @'
            ArmourTest();
'@ @'
            ArmourTest();
            ChecklistTest();
'@
Save-Src

Open-Src "$Ed\NetDevHarness.cs"
Rep 'mode != "armour" && mode != "docklook"' 'mode != "armour" && mode != "checklist" && mode != "docklook"'
Rep @'
        public static void DockLook()
'@ @'
        public static void Checklist()
        {
            SessionState.SetString("netdev.mode", "checklist");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void DockLook()
'@
Rep @'
                case "docklook": yield return DockLookScenario(); break;
'@ @'
                case "docklook": yield return DockLookScenario(); break;
                case "checklist": yield return ChecklistScenario(); break;
'@
Save-Src
