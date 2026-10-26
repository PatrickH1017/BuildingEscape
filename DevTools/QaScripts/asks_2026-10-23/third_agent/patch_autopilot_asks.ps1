# The built-player self-play also checks, solo, what the owner asked on 2026-10-24: the mic pill is up in a solo run (with the mute hint) and the journal opens on its checklist.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Scripts\QaAutopilot.cs')
Rep '            yield return PackActs("solo");' @'
            yield return PackActs("solo");
            {
                var vc = VoiceChat.Instance;
                string micLine = vc != null ? vc.HudLine(out bool micOff) : "";
                Check("solo: the mic pill is up under the player badge and says how to mute (M)", micLine.Length > 0 && micLine.Contains("M"), $"('{micLine}')");
                if (IntelScreens.Instance != null)
                {
                    IntelScreens.Instance.JournalTab = 0;
                    IntelScreens.Instance.OpenJournal();
                    Check("solo: J opens the journal on its CHECKLIST tab", IntelScreens.Instance.JournalTab == 1 && OutpostUi.Current == OutpostUi.Kind.Journal, $"(tab {IntelScreens.Instance.JournalTab})");
                    OutpostUi.Hide(OutpostUi.Kind.Journal);
                }
            }
'@
Save-Src
"autopilot: mic pill and journal"
