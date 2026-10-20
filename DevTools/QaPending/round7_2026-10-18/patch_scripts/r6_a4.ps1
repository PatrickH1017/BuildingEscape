# Round 6, batch A4 (the owner's task list of 2026-10-18, added later): "When a team wipes, players other than the host should get a "Waiting for host..." pop up above the exit button". BE_qa (= C:\bq) only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\HeistHud.cs')
Rep '            if (m.Phase == HeistPhase.Won || m.Phase == HeistPhase.Failed) DrawEndButton();' '            if (m.Phase == HeistPhase.Won || m.Phase == HeistPhase.Failed) DrawEndButton(m.Phase);'
Rep '        static void DrawEndButton()
        {' '        static void DrawEndButton(HeistPhase phase)
        {'
Rep '            if (leavePopup) { DrawLeavePopup(s); return; }
            bool exit;' '            if (leavePopup) { DrawLeavePopup(s); return; }
            if (ShowWaitingForHost(online, online && Net.NetSession.Instance != null && Net.NetSession.Instance.IsHost, phase)) DrawWaitingForHost(r, s);
            bool exit;'
Rep '        /// <summary>The host clicked Exit online: the pop-up that asks whether the party goes with them.</summary>' '        /// <summary>The pop-up over the Exit button of the players who are not the host (the owner, 2026-10-18: "When a team wipes, players other than the host should get a "Waiting for host..." pop up above the exit button").</summary>
        public const string WaitingText = "Waiting for host...";

        /// <summary>Does this machine show <see cref="WaitingText"/>? In an online run, when the team has wiped (the Failed phase), on every machine but the host''s: the host decides what happens next (back to the lobby with the party, or not).</summary>
        public static bool ShowWaitingForHost(bool online, bool isHost, HeistPhase phase) => online && !isHost && phase == HeistPhase.Failed;

        /// <summary>A small plate right above the Exit button (between it and the banner), its lettering breathing a little.</summary>
        static void DrawWaitingForHost(Rect button, float s)
        {
            if (Event.current.type != EventType.Repaint) return;
            float w = 300f * s, h = 38f * s;
            var r = new Rect(button.center.x - w * 0.5f, button.y - h - 10f * s, w, h);
            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            HudTheme.Body(r, HudTheme.Card, 16f, 3f, 4f, true, 1.6f);
            HudTheme.Text(r, WaitingText, 20f, Color.Lerp(HudTheme.Muted, HudTheme.Bright, breathe), TextAnchor.MiddleCenter);
        }

        /// <summary>The host clicked Exit online: the pop-up that asks whether the party goes with them.</summary>'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR6Test.cs')
Rep '            // ---- the intro: the filled bar saves the vote' '            // ---- a wipe: the players who are not the host get "Waiting for host..." over the Exit button
            Check("wipe: online, not the host, the team wiped: the pop-up shows", HeistHud.ShowWaitingForHost(true, false, HeistPhase.Failed));
            Check("wipe: the host does not get it (they choose: leave with the party, leave solo, stay)", !HeistHud.ShowWaitingForHost(true, true, HeistPhase.Failed));
            Check("wipe: offline there is nobody to wait for", !HeistHud.ShowWaitingForHost(false, false, HeistPhase.Failed));
            Check("wipe: a won run does not show it either, and neither does a run in progress", !HeistHud.ShowWaitingForHost(true, false, HeistPhase.Won) && !HeistHud.ShowWaitingForHost(true, false, HeistPhase.Heist));
            Check("wipe: the words are the owner''s", HeistHud.WaitingText == "Waiting for host...");

            // ---- the intro: the filled bar saves the vote'
Save-Src
Write-Output 'round 6 batch A4 patched'
