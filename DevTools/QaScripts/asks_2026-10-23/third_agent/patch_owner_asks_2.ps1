# The owner, 2026-10-24, after the glass / window messages:
#  - a water cooler beside an interior window is fine ("the water jug works perfectly there"; the bookshelf may go: the strip rule takes it out): ViewClearRules.MayStandBy;
#  - "a clock is placed on the side of a desk, this should not be the case as it is not really the norm": the Floor 7 dressing pass pinned clocks on the cubicle panels (PoolPins): no clock there now, and the scan reports one;
#  - the journal: "the Checklist tab should be first and the Safe notes second and when clicking J it should open directly to the checklist (the bottom left of the screen can also say 'Press J for journal')";
#  - "I also still don't see mic talking up top and click m to mute in solo play": the voice pill (under the player badge: "mic open (M mutes)", "MIC: talking ...", "MIC MUTED (M)") was drawn only in a network session; a solo run shows it too (the guards hear the microphone there).
# Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

# ---- the water cooler may stand by the glass
Open-Src ($A + 'Scripts\ViewClearRules.cs')
Rep '        public static bool BlocksView(bool mount, float y0, float height) => !mount && y0 + height > LowTop;' @'
        public static bool BlocksView(bool mount, float y0, float height) => !mount && y0 + height > LowTop;

        /// <summary>What may stand in the strip outside a glass front all the same (the owner, 2026-10-24, with a picture of one by an interior window: "the water jug works perfectly there"): the water cooler.</summary>
        public static bool MayStandBy(string key) => key == "water";
'@
Save-Src
Open-Src ($A + 'Scripts\FloorGenerator.ViewClear.cs')
Rep '                else if (ViewClearRules.BlocksView(false, p.y, p.h))' '                else if (ViewClearRules.BlocksView(false, p.y, p.h) && !ViewClearRules.MayStandBy(p.key))'
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevViewClear.cs')
Rep '            var s = ViewClearRules.Strip(true, 10f, 0.2f, 0f, 5f, 1f, 1.4f);' @'
            Check("views: the water cooler may stand outside a glass front (the owner's picture), a filing cabinet, a bookshelf and a big plant may not", ViewClearRules.MayStandBy("water") && !ViewClearRules.MayStandBy("file_cabinet") && !ViewClearRules.MayStandBy("bookshelf") && !ViewClearRules.MayStandBy("plant_big"));
            var s = ViewClearRules.Strip(true, 10f, 0.2f, 0f, 5f, 1f, 1.4f);
'@
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevPropScan.cs')
Rep 'else if (ViewClearRules.BlocksView(false, b.min.y, b.size.y))' 'else if (ViewClearRules.BlocksView(false, b.min.y, b.size.y) && !ViewClearRules.MayStandBy(def.key))'
Rep 'windowMounts = 0, viewBlockers = 0, cleared = 0;' 'windowMounts = 0, viewBlockers = 0, cleared = 0, panelClocks = 0;'
Rep 'if (PropSolid(h.collider, t) && h.distance <= def.D * 0.5f + 0.25f) { held = true; break; }' 'if (PropSolid(h.collider, t) && h.distance <= def.D * 0.5f + 0.25f) { held = true; if (def.key == "clock" && h.collider.name == "Screen") { panelClocks++; offenders.Add($"a clock is pinned on a cubicle panel at ({b.center.x:0.0}, {b.center.y:0.0}, {b.center.z:0.0})"); } break; }'
Rep '            fails += midRoom + floatingMounts + floatingFloor + windowMounts + viewBlockers;' '            fails += midRoom + floatingMounts + floatingFloor + windowMounts + viewBlockers + panelClocks;'
Rep 'taken out for the windows: {cleared}");' 'taken out for the windows: {cleared}; clocks on a cubicle panel: {panelClocks}");'
Save-Src

# ---- no clock pinned on a cubicle panel
Open-Src ($A + 'Scripts\Interior\OfficePlanner.Dressing.cs')
Rep '        static readonly string[] PoolPins = { "certificate", "calendar", "child_art", "painting_small", "clock" };' '        static readonly string[] PoolPins = { "certificate", "calendar", "child_art", "painting_small" };          // (no clock: the owner, 2026-10-24, "a clock is placed on the side of a desk, this should not be the case as it is not really the norm")'
Save-Src

# ---- the journal: the checklist first, J opens on it, "Press J for journal" bottom left
Open-Src ($A + 'Scripts\Outpost\IntelScreens.cs')
Rep '        public int JournalTab { get => journalTab; set => journalTab = value; }' @'
        public int JournalTab { get => journalTab; set => journalTab = value; }

        /// <summary>Opens the journal on its checklist (the owner, 2026-10-24: "the Checklist tab should be first ... when clicking J it should open directly to the checklist").</summary>
        public void OpenJournal()
        {
            openedFrame = Time.frameCount;
            journalTab = 1;
            OutpostUi.Show(OutpostUi.Kind.Journal);
        }
'@
Rep @'
                    openedFrame = Time.frameCount;
                    journalTab = 0;
                    OutpostUi.Show(OutpostUi.Kind.Journal);
'@ @'
                    OpenJournal();
'@
Rep 'new Rect(r.x + 28f * s, r.y + 74f * s, 190f * s, 36f * s), "SAFE NOTES"' 'new Rect(r.x + 226f * s, r.y + 74f * s, 190f * s, 36f * s), "SAFE NOTES"'
Rep 'new Rect(r.x + 226f * s, r.y + 74f * s, 190f * s, 36f * s), "CHECKLIST"' 'new Rect(r.x + 28f * s, r.y + 74f * s, 190f * s, 36f * s), "CHECKLIST"'
Rep '            if (current == OutpostUi.Kind.Journal) DrawJournal();' @'
            if (current == OutpostUi.Kind.Journal) DrawJournal();
            else DrawJournalHint(current);
'@
Rep '        void DrawJournal()' @'
        /// <summary>"Press J for journal" at the bottom left (the owner, 2026-10-24) while a floor is being played and no screen is up.</summary>
        void DrawJournalHint(OutpostUi.Kind current)
        {
            if (Event.current.type != EventType.Repaint || current != OutpostUi.Kind.None || OtherScreenUp) return;
            var hm = HeistManager.Instance;
            if (hm == null || hm.Floor < 1 || IntroCinematic.Playing) return;
            float s = HudTheme.S;
            HudTheme.Text(new Rect(18f * s, Screen.height - 46f * s, 360f * s, 30f * s), "Press J for journal", 16f, HudTheme.Muted, TextAnchor.MiddleLeft);
        }

        void DrawJournal()
'@
Save-Src

# ---- the voice pill in a solo run
Open-Src ($A + 'Scripts\Net\Voice\VoiceChat.cs')
RepRegion '        void OnGUI()' 'HudWidgets.VoicePill(ref y, $"{who} is talking", HudTheme.Info);' @'
        /// <summary>
        /// The words of the mic pill under the player badge ("mic open (M mutes)", "MIC: talking - guards hear you up to 12 m", "MIC MUTED (M)", "hold T to talk", "voice off"); "" when there is none (the menus, a developer who watches). A solo run has it too (the owner, 2026-10-24: "I also still
        /// don't see mic talking up top and click m to mute in solo play"): the guards hear the microphone there.
        /// </summary>
        public string HudLine(out bool off)
        {
            off = false;
            var nm = NetworkManager.Singleton;
            bool online = nm != null && nm.IsListening;
            if (Watching || !(online || SoloPlayer() != null)) return "";
            var mode = CurrentMode;
            off = Muted || mode == Mode.Off;
            return mode == Mode.Off ? "voice off"
                : Muted ? $"MIC MUTED ({muteKey})"
                : mode == Mode.PushToTalk ? $"hold {talkKey} to talk"
                : Speaking ? $"MIC: talking - guards hear you up to {HearingRadius:0} m" : $"mic open ({muteKey} mutes)";
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            string state = HudLine(out bool off);
            if (state.Length == 0) return;
            float y = HudTheme.Px(HudWidgets.TopRightBelowBadge);          // (under the player badge)
            HudWidgets.VoicePill(ref y, state, off ? HudTheme.Warning : (Speaking ? HudTheme.Good : HudTheme.Muted), true, off);
            foreach (var kv in voices)
            {
                if (!kv.Value || Time.unscaledTime - kv.Value.lastPacketTime >= 0.5f) continue;   // (the speakers heard in the last half second)
                string who = "teammate";
                foreach (var p in NetPlayer.All) if (p.OwnerClientId == kv.Key) { who = $"Player {p.Slot + 1}"; break; }
                HudWidgets.VoicePill(ref y, $"{who} is talking", HudTheme.Info);
'@.TrimEnd()
Save-Src

# ---- the tests (one process: PackKeys is a solo run in the docks)
Open-Src ($A + 'Editor\NetDev\NetDevPlaytestFixes.cs')
Rep '            Log($"[Playtest] PackKeys DONE fails={ptFails}");' @'
            // the owner, 2026-10-24: the mic pill is up in a solo run, with the mute hint, and M mutes it
            var vc = VoiceChat.Instance;
            string micLine = vc != null ? vc.HudLine(out bool micOff) : "";
            PtCheck("solo: the mic pill is up under the player badge and says how to mute", vc != null && micLine.Length > 0 && micLine.Contains("M"), $"('{micLine}')");
            if (vc != null)
            {
                vc.Muted = true;
                string mutedLine = vc.HudLine(out bool mutedOff);
                PtCheck("solo: muted, the pill says MIC MUTED (M)", mutedOff && mutedLine.StartsWith("MIC MUTED"), $"('{mutedLine}')");
                vc.Muted = false;
            }
            // the journal opens on its checklist
            if (IntelScreens.Instance != null)
            {
                IntelScreens.Instance.JournalTab = 0;
                IntelScreens.Instance.OpenJournal();
                PtCheck("journal: J opens it on the CHECKLIST tab", IntelScreens.Instance.JournalTab == 1 && OutpostUi.Current == OutpostUi.Kind.Journal, $"(tab {IntelScreens.Instance.JournalTab}, screen {OutpostUi.Current})");
                OutpostUi.Hide(OutpostUi.Kind.Journal);
            }
            Log($"[Playtest] PackKeys DONE fails={ptFails}");
'@
Save-Src
"owner asks 2 patched"
