# Round 2, batch 1 (2026-10-17 afternoon): toon default, bay door shut in the cutscene, 2 s elevator delay, see-through hallway door windows, key jingle, end-screen button.
# Patches the QA tree with exact-match replacements (patchlib). Safe to run twice. Variables $s $path $bom $eol belong to patchlib.
param([Parameter(Mandatory = $true)][string]$Tree)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$here\patchlib.ps1"

function Replace-Between([string]$startMarker, [string]$endMarker, [string]$new) {
  $i1 = $script:s.IndexOf($startMarker); $i2 = $script:s.IndexOf($endMarker)
  if ($i1 -lt 0 -or $i2 -lt 0 -or $i2 -le $i1) { throw "markers not found: $startMarker / $endMarker" }
  $text = $new.Replace("`r`n", "`n").Replace("`n", $script:eol)
  $script:s = $script:s.Substring(0, $i1) + $text + $script:s.Substring($i2)
}

# ---- 1. the toon is the default character
Open-Src "$Tree\Assets\Scripts\PlayerAppearance.cs"
if ($script:s.Contains('DefaultToon(int slot)')) { 'PlayerAppearance already defaults to a toon person' } else {
  Rep @'
        /// <summary>The pack look this player slot would get by default.</summary>
        public static PlayerAppearance ForSlot(int slot)
        {
            var looks = PartyCharacterFactory.PlayerLooks;
            var l = looks[((slot % looks.Length) + looks.Length) % looks.Length];
            int hat = System.Array.IndexOf(HatNames, l.hat);
            return new PlayerAppearance { body = l.body, face = l.face, hat = Mathf.Max(0, hat), height = 1f, width = 1f };
        }
'@ @'
        /// <summary>The Tiny Toon people a player is by default, in slot order (the owner, 2026-10-17: "Make the toon the default character").</summary>
        public static readonly string[] DefaultToons = { "male_a", "female", "male_b" };

        /// <summary>The toon person a slot is by default, or null on a machine without the toon pack (the slot's Party character stands in).</summary>
        public static string DefaultToon(int slot)
        {
            var cat = CharacterCatalog.Instance;
            if (cat == null) return null;
            int count = 0;
            foreach (var id in DefaultToons) if (cat.Find(id) != null) count++;
            if (count == 0) return null;
            int at = ((slot % count) + count) % count;
            foreach (var id in DefaultToons) if (cat.Find(id) != null && at-- == 0) return id;
            return null;
        }

        /// <summary>
        /// The look this player slot has until the player chooses one: a Tiny Toon person (the body colour, face and hat are the slot's Party character's, kept for the
        /// roster colours and for a machine without the toon pack).
        /// </summary>
        public static PlayerAppearance ForSlot(int slot)
        {
            var looks = PartyCharacterFactory.PlayerLooks;
            var l = looks[((slot % looks.Length) + looks.Length) % looks.Length];
            int hat = System.Array.IndexOf(HatNames, l.hat);
            return new PlayerAppearance { body = l.body, face = l.face, hat = Mathf.Max(0, hat), height = 1f, width = 1f, model = DefaultToon(slot) };
        }
'@
  Save-Src }

Open-Src "$Tree\Assets\Scripts\PlayerVisual.cs"
if ($script:s.Contains('PlayerAppearance.ForSlot(i)')) { 'PlayerVisual already defaults to a toon person' } else {
  Rep @'
            var looks = PartyCharacterFactory.PlayerLooks;
            CharacterLook look;
'@ @'
            CharacterLook look;
'@
  Rep 'else look = looks[i % looks.Length];' 'else look = PlayerAppearance.ForSlot(i).ToLook();       // (a Tiny Toon person by slot until the player chooses)'
  Save-Src }

Open-Src "$Tree\Assets\Scripts\Net\NetPlayer.cs"
if ($script:s.Contains('PlayerAppearance.ForSlot(Mathf.Max(0, slot.Value))')) { 'NetPlayer already sends the slot look' } else {
  Rep 'if (IsOwner) look.Value = PlayerAppearance.LoadOrDefault().Serialize();' 'if (IsOwner) look.Value = (PlayerAppearance.TryLoadSaved(out var chosen) ? chosen : PlayerAppearance.ForSlot(Mathf.Max(0, slot.Value))).Serialize();       // (a player who never chose is a toon person by slot: every friend looks different)'
  Save-Src }

Open-Src "$Tree\Assets\Editor\NetDev\NetDevTest.cs"
if ($script:s.Contains('the default look is a Tiny Toon person')) { 'NetDevTest already checks the toon default' } else {
  Rep 'PlayerAppearance.ForSlot(0).ToLook("x").model == null);' 'PlayerAppearance.ForSlot(0).ToLook("x").model == (CharacterCatalog.Instance != null ? "male_a" : null));'
  Rep @'
            Check("old five-part string is a Party look", PlayerAppearance.TryParse("40E6B2-1-2-100-100", out var old) && !old.IsToon && old.model == null);
'@ @'
            Check("old five-part string is a Party look", PlayerAppearance.TryParse("40E6B2-1-2-100-100", out var old) && !old.IsToon && old.model == null);
            // the owner, 2026-10-17: "Make the toon the default character" - a player who never chose is a Tiny Toon person, a different one by slot, and it survives the network string
            bool toonDefault = CharacterCatalog.Instance == null || CharacterCatalog.Instance.Find("male_a") != null;
            string[] want = { "male_a", "female", "male_b", "male_a", "female" };
            bool slotsOk = true;
            for (int i = 0; i < 5; i++)
            {
                var d = PlayerAppearance.ForSlot(i);
                slotsOk &= PlayerAppearance.TryParse(d.Serialize(), out var back) && back.model == d.model && (CharacterCatalog.Instance == null || (d.IsToon && d.model == want[i]));
            }
            Check("the default look is a Tiny Toon person (male_a, female, male_b, then round again), kept by the network string", toonDefault && slotsOk);
'@
  Save-Src }

Open-Src "$Tree\Assets\Editor\NetDev\NetDevToon.cs"
if ($script:s.Contains('party.model = null')) { 'NetDevToon already makes its own Party character' } else {
  Rep 'looks.Add(PlayerAppearance.ForSlot(3).Serialize()); // and a Party character beside them' @'
var party = PlayerAppearance.ForSlot(3); party.model = null;
            looks.Add(party.Serialize()); // and a Party character beside them
'@
  Save-Src }

# ---- 2. the vendor's bay door: shut during the cutscene
Open-Src "$Tree\Assets\Scripts\FloorGenerator.Dock.cs"
if ($script:s.Contains('BuildBayDoor(')) { 'the dock already has its bay door' } else {
  Rep 'if (i == DockOpenDoor) continue; // the bay with the trailer in it' @'
if (i == DockOpenDoor)
                {
                    // the bay with the trailer in it: its roller door is rolled up (the way it always was) except during the run-start cutscene (DockBayDoor)
                    BuildBayDoor(root.transform, dc, zWall, doorMat, rib, dark);
                    continue;
                }
'@
  Rep '        void BuildLoadingDock(Vector2Int cell, int seed)' @'
        /// <summary>The vendor bay's roller door: the same panel, ribs and handle as the two shut ones beside it, hung from the top edge of the opening and rolled up until the cutscene shuts it.</summary>
        void BuildBayDoor(Transform parent, float dc, float zWall, Material doorMat, Material rib, Material dark)
        {
            var bay = new GameObject("BayDoor");
            bay.transform.SetParent(parent, false);
            bay.transform.position = new Vector3(dc, DockDoorHeight, zWall - 0.08f);          // the pivot is the top edge of the opening
            Box("BayDoorPanel", new Vector3(dc, DockDoorHeight / 2f, zWall - 0.08f), new Vector3(DockDoorWidth - 0.1f, DockDoorHeight, 0.12f), doorMat, bay.transform);
            for (int k = 0; k < 6; k++)
                Box("DoorRib", new Vector3(dc, 0.3f + k * 0.5f, zWall - 0.18f), new Vector3(DockDoorWidth - 0.1f, 0.07f, 0.1f), rib, bay.transform, collider: false);
            Box("DoorHandle", new Vector3(dc, 1.0f, zWall - 0.28f), new Vector3(0.7f, 0.12f, 0.1f), dark, bay.transform, collider: false);
            bay.AddComponent<DockBayDoor>();
        }

        void BuildLoadingDock(Vector2Int cell, int seed)
'@
  Save-Src }

Open-Src "$Tree\Assets\Scripts\Intro\IntroCinematic.cs"
if ($script:s.Contains('void CloseBay()')) { 'IntroCinematic already shuts the bay door' } else {
  Rep 'office = BossOffice.Build(BossOffice.DefaultOrigin);' @'
office = BossOffice.Build(BossOffice.DefaultOrigin);
            CloseBay();                                                // (the vendor is the guy they will call: he is not to be seen yet)
'@
  Rep @'
        void Cleanup()
        {
'@ @'
        bool bayShut;

        /// <summary>The roller door of the vendor's bay comes down for the cutscene (the owner, 2026-10-17: "the vendor should not be visible ... the loading dock door should still be shut").</summary>
        void CloseBay()
        {
            var bay = DockBayDoor.Current;
            if (bay) { bay.Close(); bayShut = true; }
        }

        void OpenBay()
        {
            if (!bayShut) return;
            bayShut = false;
            var bay = DockBayDoor.Current;
            if (bay) bay.Open();
        }

        void Cleanup()
        {
            OpenBay();
'@
  Save-Src }

# ---- 3. the elevator waits two seconds before its doors close
Open-Src "$Tree\Assets\Scripts\ElevatorExtraction.cs"
if ($script:s.Contains('public const float CloseDelay')) { 'the elevator already waits' } else {
  Rep @'
        System.Collections.IEnumerator Seal(int destination)
        {
            sealing = true; Sealing = true;
            if (door) door.SetOpen(false);
'@ @'
        /// <summary>The doors wait this long after GO before they begin to close (the owner, 2026-10-17: "a two second delay before the elevator door closes after clicking the Go button").</summary>
        public const float CloseDelay = 2f;

        System.Collections.IEnumerator Seal(int destination)
        {
            sealing = true; Sealing = true;
            Announce("Doors closing in 2 seconds...");
            yield return new WaitForSeconds(CloseDelay);
            if (!EveryoneAboard)
            {
                // somebody stepped out while the doors waited: they stay open, and GO can be pressed again
                Announce("Everyone must be inside the elevator");
                Sealing = false; sealing = false;
                yield break;
            }
            if (door) door.SetOpen(false);
'@
  Save-Src }

# ---- 4. the hallway doors' windows are real openings
Open-Src "$Tree\Assets\Scripts\FloorGenerator.Doors.cs"
if ($script:s.Contains('MakeHallLeafMesh')) { 'the hallway doors already have real windows' } else {
  Rep @'
        // (measured in a leaf's own unit cube, -0.5 = the floor, +0.5 = the top of the 2.45 m door; sideways, a fraction of the leaf's width)
        const float HallWindowY = 0.112f, HallWindowW = 0.28f, HallWindowH = 0.43f, HallWindowX = 0.27f;
        const float HallBarY = -0.092f, HallBarW = 0.76f, HallBarH = 0.017f;
'@ @'
        // (measured in a leaf's own unit cube, -0.5 = the floor, +0.5 = the top of the 2.45 m door; sideways, a fraction of the leaf's width). The windows are REAL openings
        // in the leaf (MakeHallLeafMesh): 1.15 m to 2.2 m up, two to a leaf; the owner, 2026-10-17: "make the windows on the hallway doors see through".
        const float HallWindowY = 0.1837f, HallWindowW = 0.30f, HallWindowH = 0.4286f, HallWindowX = 0.25f;
        const float HallBarY = -0.112f, HallBarW = 0.76f, HallBarH = 0.017f;
'@
  Rep @'
                var glass = HallGlassMat();
                var bar = HallBarMat();
'@ @'
                var frame = HallLeafMesh();
                door.LowLeaf.GetComponent<MeshFilter>().sharedMesh = frame;           // (the leaves are frames with real openings: a player looks through the windows)
                door.HighLeaf.GetComponent<MeshFilter>().sharedMesh = frame;
                var glass = HallGlassMat();
                var bar = HallBarMat();
'@
  Rep 'dressed). Each leaf gets two tall windows (dark glass with a reflection, in a steel frame) and a push bar' 'dressed). Each leaf is a frame with two tall openings, a faint bluish pane with two reflections in each, and a push bar'
  Rep 'The windows are flat pictures hung on the leaves (like the' 'The panes are flat pictures hung in the openings (like the'
  Rep @'
            hallGlassMat = Surface("HallGlass", Color.white, 0.9f, false, 0.1f);
            hallGlassTex = MakeHallGlassTexture();
            hallGlassMat.mainTexture = hallGlassTex;
            hallGlassMat.EnableKeyword("_EMISSION");                          // a faint glow, so the windows read as glass in a dark corridor too
            hallGlassMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (hallGlassMat.HasProperty("_EmissionColor")) hallGlassMat.SetColor("_EmissionColor", new Color(0.05f, 0.10f, 0.13f));
            return hallGlassMat;
'@ @'
            hallGlassTex = MakeHallGlassTexture();
            // an unlit, see-through picture (Sprites/Default blends by the texture's alpha): the glass dims and tints what is behind it a little and shows two reflections
            hallGlassMat = new Material(Shader.Find("Sprites/Default")) { name = "HallGlass", mainTexture = hallGlassTex, hideFlags = HideFlags.HideAndDontSave };
            return hallGlassMat;
'@
  Rep @'
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u) / 0.07f, Mathf.Min(v, 1f - v) / 0.04f);
                    if (edge < 1f) { float f = Mathf.Clamp01(edge); r = Mathf.Lerp(0.10f, r, f); g = Mathf.Lerp(0.11f, g, f); b = Mathf.Lerp(0.13f, b, f); }
                    px[y * w + x] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), 255);
'@ @'
                    float alpha = Mathf.Clamp01(Mathf.Lerp(0.14f, 0.26f, v) + streak * 0.55f);          // mostly clear: a player sees through the window
                    px[y * w + x] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), (byte)(alpha * 255f));
'@
  Rep '            if (hallGlassTex) Kill(hallGlassTex);' @'
            if (hallGlassTex) Kill(hallGlassTex);
            if (hallLeafMesh) Kill(hallLeafMesh);
            hallLeafMesh = null;
'@
  Rep '        Transform Leaf(string name, Transform door, Material mat)' @'
        Mesh hallLeafMesh;

        Mesh HallLeafMesh() => hallLeafMesh ? hallLeafMesh : (hallLeafMesh = MakeHallLeafMesh());

        /// <summary>
        /// The frame of a hallway door's leaf, in the leaf's own unit cube (x across, y -0.5 = the floor to +0.5 = the top, z the thickness): a solid lower panel, a top rail and
        /// the stiles between the two openings, which have NOTHING in them. (Public and static so that a test can look through it.)
        /// </summary>
        public static Mesh MakeHallLeafMesh()
        {
            float bottom = HallWindowY - HallWindowH * 0.5f, top = HallWindowY + HallWindowH * 0.5f, half = HallWindowW * 0.5f;
            var b = new MeshBuilder();
            b.Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, bottom, 0.5f));                                    // the solid lower panel
            b.Box(new Vector3(-0.5f, top, -0.5f), new Vector3(0.5f, 0.5f, 0.5f));                                        // the top rail
            b.Box(new Vector3(-0.5f, bottom, -0.5f), new Vector3(-HallWindowX - half, top, 0.5f));                       // the outer stile, the one between the openings, the other outer stile
            b.Box(new Vector3(-HallWindowX + half, bottom, -0.5f), new Vector3(HallWindowX - half, top, 0.5f));
            b.Box(new Vector3(HallWindowX + half, bottom, -0.5f), new Vector3(0.5f, top, 0.5f));
            var m = b.ToMesh("HallLeaf");
            m.hideFlags = HideFlags.HideAndDontSave;
            return m;
        }

        Transform Leaf(string name, Transform door, Material mat)
'@
  Save-Src }

# ---- 5. the key ring's jingle
Open-Src "$Tree\Assets\Scripts\Audio\ProceduralSound.cs"
if ($script:s.Contains('tuned pitches')) { 'the key jingle is already retuned' } else {
  Replace-Between '        /// <summary>A ring of keys on a belt: a handful of small metallic clinks. Running makes more of them.</summary>' '        /// <summary>Walkie-talkie static:' @'
        /// <summary>
        /// A ring of keys on a belt: a handful of small metallic clinks, more of them (and closer together) when he runs. The owner, 2026-10-17: "the key jingle sound is a little off".
        /// The first version gave every clink its own random pitch (2.3 to 5.4 kHz) and four inharmonic overtones up to 14 kHz, so the ring sounded like a handful of unrelated bells.
        /// Now every key rings at one of a few tuned pitches (E7, G7, A7, B7, D8: a pentatonic cluster that agrees with itself, a hair out of tune each) with one soft overtone, a short
        /// soft attack and a tiny click, the later ones quieter as the ring settles, all rounded off a little above 8 kHz.
        /// </summary>
        public static float[] Keys(int variant, bool run)
        {
            var r = new SynthRandom(300 + variant * 31 + (run ? 7 : 0));
            int n = Dsp.Samples(run ? 0.60f : 0.50f);
            var o = new float[n];
            int clinks = run ? 8 + r.Int(4) : 4 + r.Int(3);
            float[] tuned = { 2637f, 3136f, 3520f, 3951f, 4699f };        // the tuned pitches
            float[] ratio = { 1f, 2.32f }, amp = { 1f, 0.30f };             // a small bar of metal: the first overtone a little more than an octave up
            for (int c = 0; c < clinks; c++)
            {
                float t01 = (c + r.Value() * 0.6f) / clinks;                // the clinks are spread over the sound, bunched at the start (the ring settling)
                int at = (int)(t01 * t01 * n * 0.6f);
                float f0 = tuned[r.Int(tuned.Length)] * (1f + r.Range(-0.012f, 0.012f)), gain = r.Range(0.45f, 1f) * (1f - 0.35f * t01), tau = r.Range(0.030f, 0.060f);
                for (int p = 0; p < ratio.Length; p++)
                {
                    float dec = Dsp.DecayPerSample(tau / (1f + p * 0.8f)), env = 1f, ph = 0f, dp = f0 * ratio[p] / SR;
                    for (int i = 0; at + i < n && env > 0.001f; i++)
                    {
                        float attack = i < 24 ? i / 24f : 1f;               // (half a millisecond of attack: no click from the sine starting abruptly)
                        o[at + i] += gain * amp[p] * Dsp.Sine(ph) * env * attack;
                        ph += dp; ph -= (int)ph;
                        env *= dec;
                    }
                }
                AddClick(o, at, r, 0.0010f, gain * 0.22f);
            }
            var soften = new Svf();
            soften.Set(8500f, 0.7f);
            for (int i = 0; i < n; i++) o[i] = soften.Low(o[i]);
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.80f);
            return o;
        }

'@
  Save-Src }

# ---- 6. the end screen's button is higher, and waits a beat
Open-Src "$Tree\Assets\Scripts\HeistHud.cs"
if ($script:s.Contains('EndButtonRect')) { 'the end button is already moved' } else {
  Rep 'if (m.Phase == HeistPhase.Won || m.Phase == HeistPhase.Failed) DrawEndButton();' @'
if (m.Phase == HeistPhase.Won || m.Phase == HeistPhase.Failed) DrawEndButton();
            else endSince = -1f;
'@
  Rep @'
        /// <summary>The way out of the end screen: back to the menu online, restart the scene offline.</summary>
        static void DrawEndButton()
        {
            bool online = Net.NetSession.Online;
            float s = HudTheme.S;
            var r = new Rect((Screen.width - 210f * s) * 0.5f, Mathf.Max(300f * s, Screen.height * 0.46f), 210f * s, 52f * s);
            if (!HudWidgets.Button(r, online ? "Back to menu" : "Restart", HudTheme.Info, 22f)) return;
'@ @'
        /// <summary>When the end screen came up (unscaled time; -1 while it is not up): the button waits a beat, so a player hammering the attack button as the team goes down does not hit it.</summary>
        static float endSince = -1f;
        public const float EndButtonDelay = 1.2f;

        /// <summary>
        /// Where the end screen's button is: up under the banner, nowhere near the middle of the screen, where the cursor appears (and where a player who is spamming the attack button
        /// keeps clicking) when the team goes down. The owner, 2026-10-17: "the exit button after wiping in a run should be higher so you don't click it on accident".
        /// </summary>
        public static Rect EndButtonRect(float screenW, float screenH, float s) => new Rect((screenW - 210f * s) * 0.5f, Mathf.Max(150f * s, screenH * 0.14f), 210f * s, 52f * s);

        /// <summary>The way out of the end screen: back to the menu online, restart the scene offline.</summary>
        static void DrawEndButton()
        {
            if (endSince < 0f) endSince = Time.unscaledTime;
            if (Time.unscaledTime - endSince < EndButtonDelay) return;
            bool online = Net.NetSession.Online;
            float s = HudTheme.S;
            var r = EndButtonRect(Screen.width, Screen.height, s);
            if (!HudWidgets.Button(r, online ? "Back to menu" : "Restart", HudTheme.Info, 22f)) return;
'@
  Save-Src }
'batch 1 patched in ' + $Tree
