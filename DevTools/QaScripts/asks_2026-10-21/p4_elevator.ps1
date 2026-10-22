# 2026-10-21, the owner's list: "When someone leaves the elevator while the countdown is active, the countdown should reset (cancel) and then when they get back in the elevator a player should have to click the elevator button again"
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
Open-Src "$Tree\Assets\Scripts\ElevatorExtraction.cs"
if ($s.Contains('CancelCountdown')) { "ElevatorExtraction already patched"; return }

Rep @'
            int steps = Mathf.Max(1, Mathf.CeilToInt(CloseDelay));            // a countdown on every machine (the owner, 2026-10-17 night: "elevator door closing pop-up should be a countdown")
            for (int left = steps; left >= 1; left--)
            {
                Announce($"Doors closing in {left}");
                yield return new WaitForSeconds(CloseDelay / steps);
            }
            if (!EveryoneAboard)
            {
                // somebody stepped out while the doors waited: they stay open, and GO can be pressed again
                Announce("Everyone must be inside the elevator");
                Sealing = false; sealing = false;
                yield break;
            }
'@ @'
            int steps = Mathf.Max(1, Mathf.CeilToInt(CloseDelay));            // a countdown on every machine (the owner, 2026-10-17 night: "elevator door closing pop-up should be a countdown")
            float stepSeconds = CloseDelay / steps;
            for (int left = steps; left >= 1; left--)
            {
                Announce($"Doors closing in {left}");
                for (float t = 0f; t < stepSeconds; t += Time.deltaTime)
                {
                    // somebody steps out while the doors count down: the countdown is cancelled at once, and it does not start again when they come back - a player has to click GO again (the owner, 2026-10-21)
                    if (!EveryoneAboard) { CancelCountdown(); yield break; }
                    yield return null;
                }
            }
            if (!EveryoneAboard) { CancelCountdown(); yield break; }
'@

Rep @'
        void OnDestroy() { EveryoneAboard = false; Sealing = false; LocalInside = false; if (Current == this) Current = null; }
'@ @'
        /// <summary>The countdown is off: somebody left the car while it ran. The doors stay open; the floor stays chosen on the panel, and GO has to be clicked again (nothing restarts by itself when they get back in).</summary>
        void CancelCountdown()
        {
            Announce(CancelledText);
            Sealing = false; sealing = false;
        }

        /// <summary>What everybody is told when the countdown is cancelled (the words are a placeholder for the owner to rewrite).</summary>
        public const string CancelledText = "Countdown cancelled - somebody left the elevator. Click GO again when everyone is inside";

        void OnDestroy() { EveryoneAboard = false; Sealing = false; LocalInside = false; if (Current == this) Current = null; }
'@
Save-Src
"p4 done"
