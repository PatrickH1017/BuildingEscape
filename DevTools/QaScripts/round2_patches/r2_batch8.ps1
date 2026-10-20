# Round 2, batch 8: the outpost computer (log-in + post-it + Safes tab) replaces the paper note; the typing keys are gated while a screen is up. Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

function Cut([string]$pattern, [string]$what) {
  $rx = New-Object System.Text.RegularExpressions.Regex($pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
  $n = $rx.Matches($script:s).Count
  if ($n -ne 1) { throw "expected 1 block to cut ($what), got $n" }
  $script:s = $rx.Replace($script:s, '')
}

# ---- OutpostUi: the computer is a screen of its own, and the note is gone
Open-Src (Join-Path $Dir 'Scripts\Outpost\OutpostUi.cs')
Rep 'public enum Kind { None, Hack, Safe, Note, Journal, Intro }' 'public enum Kind { None, Hack, Safe, Journal, Intro, Computer }'
Rep 'public static bool FreeCursor => Current == Kind.Safe || Current == Kind.Note || Current == Kind.Journal;' 'public static bool FreeCursor => Current == Kind.Safe || Current == Kind.Computer || Current == Kind.Journal;'
Rep 'The screens of the guard outpost: the lock hack, the safe''s keypad, a note on a desk and the journal of notes.' 'The screens of the guard outpost: the lock hack, the safe''s keypad, the computer (a log-in to type, so the keys of the game are gated while it is up) and the journal.'
Save-Src

# ---- SafeIntel: which outposts' computers the player has logged in to
Open-Src (Join-Path $Dir 'Scripts\Outpost\SafeIntel.cs')
Rep '        static readonly List<Entry> entries = new List<Entry>();' @'
        static readonly List<Entry> entries = new List<Entry>();
        static readonly HashSet<int> loggedIn = new HashSet<int>();

        /// <summary>Has the player logged in to the computer of the guard outpost on this floor (in this run)?</summary>
        public static bool IsLoggedIn(int outpostFloor) => loggedIn.Contains(outpostFloor);
        public static void LogIn(int outpostFloor) { loggedIn.Add(outpostFloor); }
        public static void LogOut(int outpostFloor) { loggedIn.Remove(outpostFloor); }
'@
Rep 'static void ResetStatics() { entries.Clear(); }' 'static void ResetStatics() { entries.Clear(); loggedIn.Clear(); }'
Rep 'public static void Reset() { entries.Clear(); }' 'public static void Reset() { entries.Clear(); loggedIn.Clear(); }'
Save-Src

# ---- IntelScreens: only the journal is left
Open-Src (Join-Path $Dir 'Scripts\Outpost\IntelScreens.cs')
Rep '        string noteTitle = "", noteBody = "";
        bool noteLearned;
        int noteFloor;
        int openedFrame' '        int openedFrame'
Rep '            OutpostUi.Hide(OutpostUi.Kind.Note);
' ''
Cut '        /// <summary>The local player reads a note[^\n]*\r?\n        public static void ShowNote\([^\n]*\r?\n        \{.*?\r?\n        \}\r?\n\r?\n' 'ShowNote'
Rep 'if (current != OutpostUi.Kind.Note && current != OutpostUi.Kind.Journal) return;' 'if (current != OutpostUi.Kind.Journal) return;'
Rep '            if (current == OutpostUi.Kind.Note) DrawNote();
            else if (current == OutpostUi.Kind.Journal) DrawJournal();' '            if (current == OutpostUi.Kind.Journal) DrawJournal();'
Cut '        void DrawNote\(\)\r?\n        \{.*?\r?\n        \}\r?\n\r?\n(?=        void DrawJournal)' 'DrawNote'
Rep '"sometimes keep a note about one: hack the lock, then read what is on the desks."' '"sometimes keep a record about one on their computer: hack the lock, then log in at the computer (the log-in is on a post-it on its monitor)."'
Save-Src

# ---- SafeNotes: no note texts any more (the journal's lines stay)
Open-Src (Join-Path $Dir 'Scripts\Outpost\SafeNotes.cs')
Cut '        /// <summary>The heading of the note\.</summary>.*?(?=        /// <summary>A short line for the journal)' 'SafeNotes.Title and Body'
Save-Src

# ---- the planner: the computer's place is on the last monitoring desk
Open-Src (Join-Path $Dir 'Scripts\Interior\OfficePlanner.Outpost.cs')
Rep 'op.itemSlots.Insert(0, TopSlot(r, desks[desks.Count - 1], 0.5f, 0.02f, "note", 12f));' 'op.itemSlots.Insert(0, TopSlot(r, desks[desks.Count - 1], 0.5f, 0.0f, "computer", 0f));'
Rep '// the monitoring desks under the window, facing it; the note about a safe lies on the last of them' '// the monitoring desks under the window, facing it; the computer with the record of a safe stands on the last of them'
Rep '// the note''s place and one for each thing' '// the computer''s place and one for each thing'
Rep 'bool hasNote = op.itemSlots.Exists(s => s.kind == "note");' 'bool hasNote = op.itemSlots.Exists(s => s.kind == "computer");'
Rep 'string kind = hasNote ? "item" : "note";' 'string kind = hasNote ? "item" : "computer";'
Save-Src

# ---- the outpost builder: the computer with its post-it instead of the sheet of paper
Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Outpost.cs')
Rep '                case "wired":' @'
                case "postit": m = Surface("OpPostIt", new Color(1f, 0.90f, 0.35f), 0.05f, false); break;
                case "screen": m = Surface("OpScreen", new Color(0.30f, 0.55f, 0.95f), 0f, true); break;
                case "wired":
'@
Rep @'
                if (stash.HasNote)
                {
                    var noteSlot = slots.Find(s => s.kind == "note");
                    if (noteSlot != null) MakeNote(noteSlot, stash, floor, parent);
                }
'@ @'
                var computerSlot = slots.Find(s => s.kind == "computer");
                if (computerSlot != null) MakeComputer(computerSlot, stash, floor, parent);
                else Debug.LogWarning("[Outpost] The outpost of floor " + floor + " has no place for its computer.");
'@
Cut '        /// <summary>A sheet of paper on the desk\. Point at it and press E to read it[^\n]*\r?\n        void MakeNote\(.*?root\.AddComponent<NoteProp>\(\)[^\n]*\r?\n        \}\r?\n' 'MakeNote'
Rep '        /// <summary>A small valuable of an outpost:' @'
        /// <summary>
        /// The outpost's computer on its desk (the owner, 2026-10-17: the safes' room and code are no longer on a note, "a player should have to use the computer"): a flat monitor on a stand and a
        /// keyboard, with a post-it stuck to the lower right corner of the monitor that says the user name and the password (dot lettering, a centimetre and a half tall). Point at it and press E
        /// (<see cref="ComputerProp"/>). What it has on record is what the note used to say (<see cref="OutpostLoot.Roll"/>).
        /// </summary>
        void MakeComputer(Slot s, OutpostStash stash, int floor, Transform parent)
        {
            var root = new GameObject("Computer");
            root.transform.SetParent(parent, false);
            float lift = s.y < 0.2f ? 0.75f : 0f;                                          // (a place on the floor, not on a desk: it gets a little table)
            root.transform.SetPositionAndRotation(new Vector3(s.x, s.y + lift + 0.004f, s.z), Quaternion.Euler(0f, s.yaw, 0f));
            var dark = OMat("dark");
            if (lift > 0f) Part(root.transform, "Table", new Vector3(0f, -lift * 0.5f, 0f), new Vector3(0.9f, lift, 0.6f), OMat("locker"), PrimitiveType.Cube, true);
            Part(root.transform, "Base", new Vector3(0f, 0.008f, -0.17f), new Vector3(0.20f, 0.016f, 0.14f), dark);
            Part(root.transform, "Neck", new Vector3(0f, 0.075f, -0.20f), new Vector3(0.045f, 0.12f, 0.03f), dark);
            var frame = Part(root.transform, "Frame", new Vector3(0f, 0.285f, -0.20f), new Vector3(0.44f, 0.30f, 0.035f), dark);
            Part(root.transform, "Screen", new Vector3(0f, 0.285f, -0.1805f), new Vector3(0.40f, 0.26f, 0.004f), OMat("screen"));
            var keyboard = Part(root.transform, "Keyboard", new Vector3(0f, 0.012f, 0.10f), new Vector3(0.34f, 0.024f, 0.12f), dark);
            Part(root.transform, "Keys", new Vector3(0f, 0.026f, 0.10f), new Vector3(0.31f, 0.004f, 0.09f), OMat("keycap"));

            // the post-it on the monitor's frame, turned a little, with four lines of dot lettering (capitals and digits: see ComputerRules)
            var login = ComputerRules.CredentialsFor(intelNow, floor);
            var pivot = new GameObject("PostIt").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0.135f, 0.2125f, -0.1795f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, -4f);
            Part(pivot, "PostItPaper", Vector3.zero, new Vector3(0.135f, 0.125f, 0.003f), OMat("postit"));
            var ink = OMat("ink");
            const float zText = 0.0022f;
            DotLabel(pivot, "PostItUser", "USER", 0.0022f, new Vector3(0f, 0.047f, zText), ink);
            DotLabel(pivot, "PostItUserName", login.user, 0.0031f, new Vector3(0f, 0.027f, zText), ink);
            DotLabel(pivot, "PostItPass", "PASS", 0.0022f, new Vector3(0f, 0.0045f, zText), ink);
            DotLabel(pivot, "PostItPassWord", login.pass, 0.0031f, new Vector3(0f, -0.0155f, zText), ink);

            var box = frame.GetComponent<Renderer>().bounds;
            box.Encapsulate(keyboard.GetComponent<Renderer>().bounds);
            box.Expand(new Vector3(0.12f, 0.18f, 0.12f));
            root.AddComponent<ComputerProp>().Init(box, floor, stash.noteSafeFloor, intelNow, stash.noteGivesCode);
        }

        /// <summary>A small valuable of an outpost:
'@
Save-Src

# ---- the how-to-play text (the wording is the owner's; only what is no longer true is changed)
Open-Src (Join-Path $Dir 'Scripts\HowToPlayContent.cs')
Rep 'hack a guard outpost''s lock / read a note / work a safe' 'hack a guard outpost''s lock / use a guard outpost''s computer / work a safe'
Rep 'learned about from notes (their room and, if a note gave it, their code)' 'learned about from the outposts'' computers (their room and, if the computer had it, their code)'
Rep 'Inside are desks with notes, weapons and valuables' 'Inside are desks with a computer, weapons and valuables'
Rep 'A note in an outpost names the safe''s room and sometimes its code ' 'The computer in an outpost (its log-in is on a post-it on the monitor; click its Safes tab) names the safe''s room and sometimes its code '
Rep '(read it with E; J opens your journal).' '(use it with E; J opens your journal).'
Save-Src

# ---- the keys of the game are not to fire while a name and a password are typed (the other screens never needed this: they are played with the mouse, digits and W A S D)
Open-Src (Join-Path $Dir 'Scripts\HideSpot.cs')
Rep 'if (!inside || inside.InCustody || !Input.GetKeyDown(hideKey)) return;' 'if (!inside || inside.InCustody || OutpostUi.Open || !Input.GetKeyDown(hideKey)) return;'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Net\Voice\VoiceChat.cs')
Rep 'if (Input.GetKeyDown(muteKey) && !EscapeMenu.Open && !Backpack.UiOpen && !Vendor.ShopOpen) Muted = !Muted;' 'if (Input.GetKeyDown(muteKey) && !EscapeMenu.Open && !Backpack.UiOpen && !Vendor.ShopOpen && !OutpostUi.Open) Muted = !Muted;'
Rep '(mode == Mode.OpenMic || Input.GetKey(talkKey));' '(mode == Mode.OpenMic || (Input.GetKey(talkKey) && !OutpostUi.Open));'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\WobblyPlayer.cs')
Rep 'bool crouchKey = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);' 'bool crouchKey = !OutpostUi.Open && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C));'
Save-Src

# ---- the tests
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevPewLootTest.cs')
Rep 'noteToo &= op.itemSlots.Exists(s => s.kind == "note");' 'noteToo &= op.itemSlots.Exists(s => s.kind == "computer");'
Rep 'Check("pew loot: and a place for its note", noteToo);' 'Check("pew loot: and a place for its computer", noteToo);'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevOutpost.cs')
Cut '            // ================= the note and the journal\r?\n            \{\r?\n                NoteProp note = null;.*?OutpostCheck\("note: reading it again learns nothing new"[^\n]*\r?\n                \}\r?\n            \}\r?\n' 'the note test'
Rep '            // ================= the safe
' @'
            // ================= the computer, its post-it and the journal
            {
                ComputerProp compo = null;
                foreach (var i in Interactable.All) if (i is ComputerProp c0) { compo = c0; break; }
                OutpostCheck("computer: floor 1's outpost has one, with the record of floor 1's safe", compo != null && compo.safeFloor == 1 && compo.outpostFloor == 1, compo ? $"(about floor {compo.safeFloor})" : "");
                if (compo != null)
                {
                    OutpostCheck("computer: the code is on record exactly when the rules say so", compo.givesCode == SafeRules.NoteGivesCode(seed, 1));
                    var lgn = ComputerRules.CredentialsFor(seed, 1);
                    OutpostCheck("computer: its log-in is the one the rules give (capitals and digits)", compo.Credentials.user == lgn.user && compo.Credentials.pass == lgn.pass && ComputerRules.IsPostItText(lgn.user) && ComputerRules.IsPostItText(lgn.pass));
                    int postItParts = 0;
                    foreach (var tp in compo.GetComponentsInChildren<Transform>(true)) if (tp.name.StartsWith("PostIt")) postItParts++;
                    OutpostCheck("computer: a post-it with four lines of lettering is stuck to the monitor", postItParts == 6, $"({postItParts} parts)");
                    GoTo(compo.AimBox.center + Vector3.up * 0.4f);
                    int before = SafeIntel.Entries.Count;
                    compo.Use(player);
                    yield return null;
                    OutpostCheck("computer: using it brings the log-in screen up, logged out", OutpostUi.Current == OutpostUi.Kind.Computer && ComputerScreen.Showing && !ComputerScreen.LoggedIn);
                    OutpostCheck("computer: a wrong password does not open it", !ComputerScreen.TryLogin(lgn.user, "NOTIT") && !ComputerScreen.LoggedIn && SafeIntel.Entries.Count == before);
                    OutpostCheck("computer: a wrong user name does not either", !ComputerScreen.TryLogin("NOBODY1", lgn.pass) && !ComputerScreen.LoggedIn);
                    ComputerScreen.SelectTab(ComputerScreen.TabSafes);
                    OutpostCheck("computer: no tab can be clicked before logging in, and nothing is learned", ComputerScreen.Tab == ComputerScreen.TabNone && SafeIntel.Entries.Count == before);
                    OutpostCheck("computer: the post-it's words, typed in small letters, open it", ComputerScreen.TryLogin(lgn.user.ToLowerInvariant(), "  " + lgn.pass.ToLowerInvariant() + " ") && ComputerScreen.LoggedIn);
                    OutpostCheck("computer: logged in, nothing is learned until the Safes tab is clicked", SafeIntel.Entries.Count == before && ComputerScreen.Tab == ComputerScreen.TabNone);
                    ComputerScreen.SelectTab(ComputerScreen.TabSafes);
                    SafeIntel.Entry e;
                    bool known = SafeIntel.TryGet(1, out e);
                    string room = gen.SafeRoomLabel(1);
                    OutpostCheck("computer: the Safes tab lists the room, and the journal learns it", ComputerScreen.Tab == ComputerScreen.TabSafes && known && !string.IsNullOrEmpty(room) && e.room == room && SafeIntel.Entries.Count == before + 1, $"(room {room})");
                    OutpostCheck("computer: ... and the code if it is on record", known && (compo.givesCode ? e.code == SafeRules.Code(seed, 1) : e.code == 0));
                    OutpostUi.Hide(OutpostUi.Kind.Computer);
                    yield return null;
                    compo.Use(player);
                    yield return null;
                    OutpostCheck("computer: a player who has logged in stays logged in on this floor", ComputerScreen.Showing && ComputerScreen.LoggedIn);
                    ComputerScreen.SelectTab(ComputerScreen.TabSafes);
                    OutpostCheck("computer: opening the tab again learns nothing new", SafeIntel.Entries.Count == before + 1);
                    OutpostUi.Hide(OutpostUi.Kind.Computer);
                    yield return null;
                    OutpostCheck("computer: closing it frees the player", !OutpostUi.Open && !ComputerScreen.Showing);
                }
            }

            // ================= the safe
'@
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRound2Test.cs')
Rep @'
            Check("night: the towers have lit windows, some of the panes of every tile at every distance", windowsOk, "(" + windowNote.Trim() + ")");
        }
'@ @'
            Check("night: the towers have lit windows, some of the panes of every tile at every distance", windowsOk, "(" + windowNote.Trim() + ")");

            // ---- the outpost computer's log-in
            bool repeatable = true, fitsPostIt = true, floorsDiffer = false;
            for (int sd = 1; sd <= 300; sd++)
                for (int fl2 = 1; fl2 <= 12; fl2++)
                {
                    var ca = ComputerRules.CredentialsFor(sd * 31 + 7, fl2);
                    var cb = ComputerRules.CredentialsFor(sd * 31 + 7, fl2);
                    repeatable &= ca.user == cb.user && ca.pass == cb.pass;
                    fitsPostIt &= ComputerRules.IsPostItText(ca.user) && ComputerRules.IsPostItText(ca.pass);
                    if (fl2 > 1) { var prev = ComputerRules.CredentialsFor(sd * 31 + 7, fl2 - 1); floorsDiffer |= ca.user != prev.user || ca.pass != prev.pass; }
                }
            var lg = ComputerRules.CredentialsFor(777, 3);
            Check("computer: the same run and floor give the same log-in, made of capitals and digits that fit the post-it, and floors differ", repeatable && fitsPostIt && floorsDiffer);
            Check("computer: the right words open it in capitals or small letters with spaces round them; a wrong word, an empty one, half the pair or the pair swapped does not",
                ComputerRules.Accepts(lg, lg.user, lg.pass) && ComputerRules.Accepts(lg, lg.user.ToLowerInvariant(), " " + lg.pass.ToLowerInvariant() + " ") && !ComputerRules.Accepts(lg, lg.user, "WRONG1")
                && !ComputerRules.Accepts(lg, "", "") && !ComputerRules.Accepts(lg, lg.user, null) && !ComputerRules.Accepts(lg, lg.pass, lg.user) && !ComputerRules.Accepts(lg, lg.user + "X", lg.pass));
        }
'@
Save-Src
Write-Output 'batch 8 patched'
