# Round 12, batch G12 (the words that G08..G11 made out of date, minimal and factual; the owner writes the game text, every change is listed in DEVELOPER_NOTES 2.33 "Words I changed"):
# the pistol carries twelve clips now (the dealer's refusal and the blurb said three), and the controls lists lacked the weapon bar (1 2 3), P / wheel / Ctrl + Up Down and the darts.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'

Open-Src "$A\Backpack.cs"
Rep @'
        public const string TooManyClips = "You can't carry more than three clips";
'@ @'
        /// <summary>What a player is told who carries the most clips of a gun already: "three" for most guns, "twelve" for the pistol (the owner writes the words: this keeps the old sentence with the right number).</summary>
        public static string TooManyClipsFor(GunMagazine m) => "You can't carry more than " + ClipWord(m.Capacity > 0 ? m.MaxRounds / m.Capacity : 3) + " clips";
        static string ClipWord(int n) => n >= 1 && n <= 12 ? new[] { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve" }[n - 1] : n.ToString();
'@
Rep 'if (gun.Magazine.Room < clip.rounds) { Say(TooManyClips); return false; }' 'if (gun.Magazine.Room < clip.rounds) { Say(TooManyClipsFor(gun.Magazine)); return false; }'
Save-Src

Open-Src "$A\Net\RunSync.Requests.cs"
Rep 'if (clipGun.Magazine.Room < clip.rounds) { Notice(who, Backpack.TooManyClips); return; }' 'if (clipGun.Magazine.Room < clip.rounds) { Notice(who, Backpack.TooManyClipsFor(clipGun.Magazine)); return; }'
Save-Src

Open-Src "$A\Vendor.cs"
Rep 'if (gun.Magazine.Room < p.rounds) return "You can''t carry more than three clips";' 'if (gun.Magazine.Room < p.rounds) return Backpack.TooManyClipsFor(gun.Magazine);'
Rep '8 rounds a clip, 3 clips, 3 hits drop a guard. Try it on the dummy' '8 rounds a clip, up to 12 clips, 3 hits drop a guard. Try it on the dummy'
Save-Src

Open-Src "$A\Hud\KeybindScreen.cs"
Rep @'
                    new Entry("CTRL / C", "crouch (hold): slower, quieter"),
'@ @'
                    new Entry("CTRL / C", "crouch (hold): slower, quieter"),
                    new Entry("P", "first / third person"),
'@
Rep @'
                    new Entry("LMB", "throw / swing weapon / fire / punch"),
'@ @'
                    new Entry("LMB", "throw / swing weapon / fire / punch"),
                    new Entry("1 2 3", "switch weapon"),
'@
Save-Src

Open-Src "$A\HowToPlayContent.cs"
Rep @'
            ("Space", "Jump"),
'@ @'
            ("P (or mouse wheel, Ctrl + Up / Down)", "Switch between first and third person: wheel up or Ctrl + Up for first person, wheel down or Ctrl + Down for third"),
            ("Space", "Jump"),
'@
Rep '/ use a guard outpost''s computer / work a safe")' '/ use a guard outpost''s computer / work a safe / play darts at the dart board")'
Rep @'
            ("1 - 7", "Buy from the vendor's shop"),
'@ @'
            ("1 2 3", "Switch to the first, second or third weapon in your pack (the bar at the bottom of the screen shows them)"),
'@
Save-Src

# the playtest change list (our own, not the friends' README): the pistol's clips, and the round's changes as item 20
$readme = 'C:\bqs\README_PLAYTEST.txt'
Open-Src $readme
Rep 'The pew pistol (8 rounds a clip, 3 clips, three hits drop an officer' 'The pew pistol (8 rounds a clip, up to 12 clips, three hits drop an officer'
Rep '(three clips of a gun is the most anybody can carry: he will not sell a fourth)' '(three clips of a gun is the most anybody can carry, twelve of the pistol: he will not sell more)'
Rep @'

If something goes wrong
'@ @'
20. Round 12 (2026-10-19). The Intro's plane is the pack's white plane and its skyline has the pack's towers; the cars and the buildings of the night city outside the windows are the pack's. The Intro now ends on the Loading Docks' bay:
    the dealer's roller door opens after the workers say they are calling him (37.8 s in all). The pistol carries up to twelve clips (96 rounds). Floor 3's safes hold no gun and no clip (the only pistol of floor 3 is the one in the
    outpost's gun safe), only armour, cash and watches. Keys 1 / 2 / 3 switch between the first three weapons in your pack (a bar at the bottom of the screen shows them). P, the mouse wheel and Ctrl + Up / Down switch between first and third
    person. The forklift no longer blocks the PVP room's doorway. A dart board hangs on the Loading Docks' east wall across from the break room's doorway (E to play 301 or Cricket; hold the left mouse button to wind up a throw, let go to throw;
    the dart drops over a distance).

If something goes wrong
'@
Save-Src
Write-Output 'round 12 batch G12 patched'
