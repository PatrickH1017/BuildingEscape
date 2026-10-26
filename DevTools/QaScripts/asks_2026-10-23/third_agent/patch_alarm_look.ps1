# The owner, 2026-10-24: "The speakers are currently placed on lights which is wrong, they should just be attached to the ceiling like the attached image reference"; "when the big alarm is activated there should be red lights activated that spin in circles (similar to a police car light).
# I added lamp assets to use for this, please use the one I sent a reference image of and add these lights around the floors on corners near the ceiling" (the small roof lamp of the PBR Lamps Pack; hallway corners + big rooms); "the intense music is already in the game so don't change
# anything with it, just make sure it plays while guards are alerted and while the big alarm is going off".
# BeaconRules / AlarmBeacons / FloorGenerator.Beacons (new), PaRules.MountSpot + FloorGenerator.Speakers (round flat discs off the light panels), MusicDirector (the trigger only), tests. Patches the scratch copy C:\bd; exact matches only (patchlib); new files are written with CRLF.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 'beacon_new\BeaconRules.cs') ($A + 'Scripts\BeaconRules.cs')
Put-Crlf (Join-Path $here 'beacon_new\AlarmBeacons.cs') ($A + 'Scripts\AlarmBeacons.cs')
Put-Crlf (Join-Path $here 'beacon_new\FloorGenerator.Beacons.cs') ($A + 'Scripts\FloorGenerator.Beacons.cs')
Put-Crlf (Join-Path $here 'beacon_new\BeaconPrefabBuilder.cs') ($A + 'Editor\BeaconPrefabBuilder.cs')
Put-Crlf (Join-Path $here 'beacon_new\NetDevAlarmLooks.cs') ($A + 'Editor\NetDev\NetDevAlarmLooks.cs')

# ---------------------------------------------------------------- PaRules: the round ceiling speaker and where it hangs
Open-Src ($A + 'Scripts\Outpost\PaRules.cs')
Rep '        public const float Height = 3.2f;' @'
        public const float Height = 3.2f;

        /// <summary>
        /// The round ceiling speaker (the owner, 2026-10-24: "they should just be attached to the ceiling like the attached image reference"): a flat disc <see cref="DiscDiameter"/> across and <see cref="DiscHeight"/> thick whose top is <see cref="CeilingGap"/> under the ceiling, with a
        /// darker grille disc under it. (<see cref="Height"/> is the old hanging height; the speakers keep their places for the PA, only the model sits on the ceiling.)
        /// </summary>
        public const float DiscDiameter = 0.40f, DiscHeight = 0.05f, GrilleDiameter = 0.30f, GrilleHeight = 0.012f, CeilingGap = 0.03f;

        /// <summary>
        /// Where a hallway's speaker is hung: at <paramref name="centre"/> (the PA's place), or the nearest spot in the hallway that no light panel (<paramref name="panels"/>, their frames) covers: a speaker on a light is wrong (the owner, 2026-10-24). The speaker moves along the hallway
        /// and across it, the smallest move first; where nothing is free it stays at the centre.
        /// </summary>
        public static Vector2 MountSpot(Vector2 centre, Rc hall, IReadOnlyList<Rc> panels)
        {
            float radius = DiscDiameter * 0.5f + 0.06f;
            bool alongX = hall.W >= hall.D;
            float half = (alongX ? hall.D : hall.W) * 0.5f;
            int across = (int)(Mathf.Max(0f, half - 0.45f) / 0.4f);
            var tries = new List<Vector2>();
            for (int a = -7; a <= 7; a++)
                for (int c = -across; c <= across; c++) tries.Add(new Vector2(a * 0.6f, c * 0.4f));
            tries.Sort((p, q) => (p.x * p.x + p.y * p.y).CompareTo(q.x * q.x + q.y * q.y));
            foreach (var t in tries)
            {
                var at = alongX ? new Vector2(centre.x + t.x, centre.y + t.y) : new Vector2(centre.x + t.y, centre.y + t.x);
                if (at.x < hall.x0 + 0.3f || at.x > hall.x1 - 0.3f || at.y < hall.z0 + 0.3f || at.y > hall.z1 - 0.3f) continue;
                if (!Covered(at, radius, panels)) return at;
            }
            return centre;
        }

        static bool Covered(Vector2 at, float radius, IReadOnlyList<Rc> panels)
        {
            if (panels == null) return false;
            var disc = new Rc(at.x - radius, at.y - radius, at.x + radius, at.y + radius);
            for (int i = 0; i < panels.Count; i++) if (disc.Overlaps(panels[i])) return true;
            return false;
        }
'@
Rep '    /// <summary>One hallway loudspeaker on the ceiling (a grey box with a grille): its place in <see cref="PaState.Speakers"/>.</summary>' '    /// <summary>One hallway loudspeaker on the ceiling (a round disc with a grille, flat against it): its place in <see cref="PaState.Speakers"/>.</summary>'
Save-Src

# ---------------------------------------------------------------- FloorGenerator: the panel plan is kept; the speakers are discs on the ceiling; the beacons are built
Open-Src ($A + 'Scripts\FloorGenerator.AfterHours.cs')
Rep '        Material ceilingMat, panelFrameMat, panelOffMat, panelWarmMat, panelCoolMat, nightSky;' @'
        Material ceilingMat, panelFrameMat, panelOffMat, panelWarmMat, panelCoolMat, nightSky;

        /// <summary>The light panels of the floor on show (the loudspeakers keep off them; for the tests); null where there are none.</summary>
        AfterHours.Plan panelPlan;
        public AfterHours.Plan PanelPlan => panelPlan;
'@
Rep @'
        {
            if (!afterHours || !Application.isPlaying) return;   // (the editor tools that build floors do not need them)
'@ @'
        {
            panelPlan = null;
            if (!afterHours || !Application.isPlaying) return;   // (the editor tools that build floors do not need them)
'@
Rep '            var plan = AfterHours.Build(input);' @'
            var plan = AfterHours.Build(input);
            panelPlan = plan;
'@
Save-Src
Open-Src ($A + 'Scripts\FloorGenerator.Speakers.cs')
Rep '    /// The loudspeakers in the hallways of an office floor (the owner, 2026-10-18; <see cref="PaRules"/>): a small grey box with a grille under the ceiling every 13 m of corridor, planned from the layout' '    /// The loudspeakers in the hallways of an office floor (the owner, 2026-10-18; <see cref="PaRules"/>): a round disc with a grille flat against the ceiling (since 2026-10-24: not on a light) every 13 m of corridor, planned from the layout'
RepRegion '        void BuildSpeakers(int floor)' '                go.AddComponent<LoudSpeaker>().Index = i;' @'
        void BuildSpeakers(int floor)
        {
            if (layout == null || !Application.isPlaying) return;
            var corridors = new List<Rc>();
            foreach (var r in layout.rooms) if (r.kind == 2) corridors.Add(r.bounds);
            var plan = PaRules.Plan(corridors);
            PaState.NewFloor(plan);
            if (plan.Count == 0) return;
            var body = OMat("keycap"); var dark = OMat("dark");
            var root = new GameObject("LoudSpeakers").transform;
            root.SetParent(current, false);
            var panels = new List<Rc>();
            if (panelPlan != null) foreach (var p in panelPlan.panels) panels.Add(AfterHours.FrameRect(p));
            float top = CeilingUnderside - PaRules.CeilingGap;          // (the top of the disc: flat against the ceiling, the owner, 2026-10-24: "attached to the ceiling")
            Rc HallOf(Vector2 p) { foreach (var c in corridors) if (c.Contains(p.x, p.y)) return c; return new Rc(p.x - 1f, p.y - 1f, p.x + 1f, p.y + 1f); }
            for (int i = 0; i < plan.Count; i++)
            {
                var at = PaRules.MountSpot(plan[i], HallOf(plan[i]), panels);          // (the PA keeps the planned place; the model hangs where no light panel is)
                var go = new GameObject("LoudSpeaker_" + i);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(at.x, top - PaRules.DiscHeight * 0.5f, at.y);
                Part(go.transform, "Disc", Vector3.zero, new Vector3(PaRules.DiscDiameter, PaRules.DiscHeight * 0.5f, PaRules.DiscDiameter), body, PrimitiveType.Cylinder);
                Part(go.transform, "Grille", new Vector3(0f, -(PaRules.DiscHeight * 0.5f + PaRules.GrilleHeight * 0.5f), 0f), new Vector3(PaRules.GrilleDiameter, PaRules.GrilleHeight * 0.5f, PaRules.GrilleDiameter), dark, PrimitiveType.Cylinder);
                go.AddComponent<LoudSpeaker>().Index = i;
'@.TrimEnd()
Save-Src
Open-Src ($A + 'Scripts\FloorGenerator.cs')
Rep '                OutpostStep("the hallway loudspeakers", floor, () => BuildSpeakers(floor));' @'
                OutpostStep("the hallway loudspeakers", floor, () => BuildSpeakers(floor));
                OutpostStep("the alarm beacons", floor, () => BuildBeacons(floor));          // (the owner, 2026-10-24: red lights that spin in the corners near the ceiling when the Big Alarm goes on; FloorGenerator.Beacons.cs)
'@
Save-Src

# ---------------------------------------------------------------- the music: only WHEN the existing intense loop is chosen (it is not touched)
Open-Src ($A + 'Scripts\Audio\MusicDirector.cs')
Rep '            if (floorLive && !sealing && (hm.BackupCalled || hm.BackupActive)) return Mood.Intense;' '            if (IntenseWanted(floorLive, sealing, hm.BackupCalled, hm.BackupActive, hm.BigAlarmActive)) return Mood.Intense;          // (the owner, 2026-10-24: while the guards are alerted and while the Big Alarm goes off)'
Rep '        /// <summary>What should be playing: decided from shared run state plus where the local player is.</summary>' @'
        /// <summary>Is the intense loop wanted: on a live floor that is not being left, while backup was called or is on, and while the Big Alarm is on (the owner, 2026-10-24: "just make sure it plays while guards are alerted and while the big alarm is going off").</summary>
        public static bool IntenseWanted(bool floorLive, bool sealing, bool backupCalled, bool backupActive, bool bigAlarm) => floorLive && !sealing && (backupCalled || backupActive || bigAlarm);

        /// <summary>What should be playing: decided from shared run state plus where the local player is.</summary>
'@
Save-Src

# ---------------------------------------------------------------- the tests
Open-Src ($A + 'Editor\NetDev\NetDevTest.cs')
Rep '            AlertRulesTest();' @'
            AlertRulesTest();
            AlarmRulesTest();
'@
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void PropScan()' @'
        public static void AlarmLooks()
        {
            SessionState.SetString("netdev.mode", "alarmlooks");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PropScan()
'@
Rep 'mode != "lamppreview" &&' 'mode != "lamppreview" && mode != "alarmlooks" &&'
Rep '                case "lamppreview": yield return LampPreviewScenario(); break;' @'
                case "lamppreview": yield return LampPreviewScenario(); break;
                case "alarmlooks": yield return AlarmLooksScenario(); break;
'@
Save-Src
"alarm look patched"
