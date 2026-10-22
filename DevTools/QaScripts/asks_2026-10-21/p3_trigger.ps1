# 2026-10-21, the owner's list: "When you click go on the elevator menu you fire a shot from your gun that should not be happening."
# Cause: the elevator panel closes on the click (the ride starts), the mouse button is still held, and the very next frame the gun is usable again and sees "fire held". A click that begins while the gun cannot be used never becomes a shot now.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

$latchPath = "$Tree\Assets\Scripts\Guns\TriggerLatch.cs"
if (-not (Test-Path $latchPath)) {
$latch = @'
namespace BuildingEscape
{
    /// <summary>
    /// A click that begins while the gun cannot be used never becomes a shot (the owner, 2026-10-21: "When you click go on the elevator menu you fire a shot from your gun that should not be happening"). The elevator panel (like the shop, the pack
    /// or a menu) closes on the click while the mouse button is still held down; the next frame the gun is usable again, sees the button held, and fires. With the latch the button has to be let go of, and pressed again, before the gun works:
    /// a button that is down while the gun is not usable latches, and the latch opens only when the button is up. Pure: the controller asks it once a frame.
    /// </summary>
    public struct TriggerLatch
    {
        bool latched;

        /// <summary>Is the click that is still held a click that began while the gun could not be used?</summary>
        public bool Latched => latched;

        /// <summary>
        /// Once a frame: whether the gun may be used right now (no menu, the cursor in the game, the hands free) and whether the fire button is held down. True when a shot may be fired this frame (a held button repeats, as before).
        /// </summary>
        public bool Pull(bool usable, bool held)
        {
            if (!held) { latched = false; return false; }
            if (!usable) { latched = true; return false; }
            return !latched;
        }

        /// <summary>Forget the latch (a new gun, a new floor).</summary>
        public void Reset() { latched = false; }
    }
}
'@
[IO.File]::WriteAllText($latchPath, $latch.Replace("`r`n", "`n").Replace("`n", "`r`n"), (New-Object Text.UTF8Encoding($false)))
"TriggerLatch.cs written"
}

Open-Src "$Tree\Assets\Scripts\Guns\GunController.cs"
if (-not $s.Contains('TriggerLatch')) {
Rep @'
            if (!usable) return;

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (mag.StartReload(now)) reloadStep = 0;
                else if (mag.OutOfAmmo) OutOfAmmoNotice();
            }
            if (Input.GetMouseButton(0) && !DartBoard.Playing) Fire();
        }
'@ @'
            if (!usable) { if (viewed) trigger.Pull(false, TriggerHeld()); return; }       // (a click that begins now must not become a shot when the menu closes on it: see TriggerLatch)

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (mag.StartReload(now)) reloadStep = 0;
                else if (mag.OutOfAmmo) OutOfAmmoNotice();
            }
            if (trigger.Pull(!DartBoard.Playing, TriggerHeld())) Fire();
        }

        TriggerLatch trigger;

        /// <summary>A test's own fire button (null: the real mouse): the tests cannot press a mouse button.</summary>
        public static bool? TestTrigger;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTestTrigger() { TestTrigger = null; }

        static bool TriggerHeld() => TestTrigger ?? Input.GetMouseButton(0);

        /// <summary>Is the click that is held right now one the gun is ignoring (it began while a menu or the elevator panel was up)? For the tests.</summary>
        public bool TriggerLatched => trigger.Latched;
'@
Save-Src
}
"p3 done"
