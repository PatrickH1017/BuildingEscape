# T29, part 2 (the owner's notes of 2026-10-23 for the spectator: "Free-Cam / Player Toggle", "Live Entity / AI Tracking Overlay", "On-Screen Network Diagnostics"): the developer who watches gets a free camera next to the follow camera (F), an overlay over every guard and player (O:
# what a guard does, whom he is after, his health, what he is) and a small network panel (N: round trip, packet loss, bytes per second, tick rate, frame rate). Health and the target come from a small host -> spectator record (about four times a second, only while somebody watches).
# Needs T29 part 1 (patch_t29.ps1 of the second agent) applied. Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept; new files are written with CRLF. -Dry only checks the anchors (and says what it would write).
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
  New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 't29b_new\DevOverlayRules.cs') ($A + 'Scripts\Dev\DevOverlayRules.cs')
Put-Crlf (Join-Path $here 't29b_new\DevSpectatorView.cs') ($A + 'Scripts\Dev\DevSpectatorView.cs')
Put-Crlf (Join-Path $here 't29b_new\RunSync.DevView.cs') ($A + 'Scripts\Net\RunSync.DevView.cs')
Put-Crlf (Join-Path $here 't29b_new\NetDevAsks1023ITest.cs') ($A + 'Editor\NetDev\NetDevAsks1023ITest.cs')

# ---- the guard says whom he is after (only the host's own guards know: the spectator's overlay gets it from the host's record)
$SC = $A + 'Scripts\GuardAI.cs'; Open-Src $SC
Rep @'
        public WobblyPlayer Holding => holding;
        WobblyPlayer holding;
'@ @'
        public WobblyPlayer Holding => holding;
        WobblyPlayer holding;
        /// <summary>The player this officer is after now (host only: a puppet knows nothing of it), or null. The developer spectator's overlay shows it (RunSync.DevView.cs).</summary>
        public WobblyPlayer Target => target;
'@
Save-Src

# ---- the transport tells the numbers of its connection
$SC = $A + 'Scripts\Net\SteamNetworkTransport.cs'; Open-Src $SC
Rep @'
        public override ulong GetCurrentRtt(ulong clientId)
'@ @'
        /// <summary>The numbers of a connection as Steam counts them (the developer spectator's network panel).</summary>
        public struct LinkStats { public int pingMs; public float quality, inBytes, outBytes; }

        /// <summary>The round trip (ms), the share of packets that arrived (0..1) and the bytes per second in and out of the connection to this client (on a client: to the host); false when there is no such connection.</summary>
        public bool TryGetLinkStats(ulong clientId, out LinkStats stats)
        {
            stats = default;
            if (!TryGetConnection(clientId, out var conn)) return false;
            var status = new SteamNetConnectionRealTimeStatus_t();
            var lane = new SteamNetConnectionRealTimeLaneStatus_t();
            if (SteamNetworkingSockets.GetConnectionRealTimeStatus(conn, ref status, 0, ref lane) != EResult.k_EResultOK) return false;
            stats = new LinkStats { pingMs = Mathf.Max(0, status.m_nPing), quality = status.m_flConnectionQualityLocal, inBytes = status.m_flInBytesPerSec, outBytes = status.m_flOutBytesPerSec };
            return true;
        }

        public override ulong GetCurrentRtt(ulong clientId)
'@
Save-Src

# ---- the run sync: the record is sent and listened for
$SC = $A + 'Scripts\Net\RunSync.cs'; Open-Src $SC
Rep '            SpawnSnapshots();' @'
            SpawnSnapshots();
            SpawnDevView();                      // (a developer who watches listens for the host's debug record: RunSync.DevView.cs)
'@
Rep '            DespawnSnapshots();' @'
            DespawnSnapshots();
            DespawnDevView();
'@
Rep '                HostSendSnapshots();' @'
                HostSendSnapshots();
                HostSendDevView();               // (only while a developer watches)
'@
Save-Src

# ---- the spectator: the free camera (F), the overlay (O) and the network panel (N)
$SC = $A + 'Scripts\Dev\DevSpectator.cs'; Open-Src $SC
Rep '        public static bool TestMenuToggle;' @'
        public static bool TestMenuToggle;
        /// <summary>The free camera is on (F): the camera has left the player it followed and flies where the keys (W A S D, E up, Q down, Shift fast, Ctrl slow) and the mouse say.</summary>
        public static bool FreeCam { get; private set; }
        /// <summary>Tests: F is pressed on the next frame; the free camera's keys held as (right, up, forward).</summary>
        public static bool TestFreeToggle;
        public static Vector3 TestMove;
'@
Rep 'MenuOpen = false; TestStep = 0; TestMenuToggle = false; LastCommand = ""; }' 'MenuOpen = false; TestStep = 0; TestMenuToggle = false; LastCommand = ""; FreeCam = false; TestFreeToggle = false; TestMove = Vector3.zero; }'
Rep '        Vector2 scroll;' @'
        Vector2 scroll;
        Vector3 freePos;
        float savedMinPitch, savedMaxPitch;
'@
Rep @'
            if (!rig && Time.unscaledTime >= nextLookup) { rig = FindFirstObjectByType<CameraFollow>(); nextLookup = Time.unscaledTime + 0.5f; }
            if (!rig) return;
'@ @'
            if (!rig && Time.unscaledTime >= nextLookup) { rig = FindFirstObjectByType<CameraFollow>(); nextLookup = Time.unscaledTime + 0.5f; }
            if (!rig) return;

            bool keys = !EscapeMenu.Open && !DevTerminal.Open && !MenuOpen;
            if (keys && Input.GetKeyDown(KeyCode.O)) DevSpectatorView.OverlayOn = !DevSpectatorView.OverlayOn;
            if (keys && Input.GetKeyDown(KeyCode.N)) DevSpectatorView.NetPanelOn = !DevSpectatorView.NetPanelOn;
            if (TestFreeToggle || (keys && Input.GetKeyDown(KeyCode.F))) SetFree(!FreeCam);
            TestFreeToggle = false;
            if (FreeCam) { Active = false; Watching = null; WatchingNumber = 0; if (rig.spectate) rig.spectate = null; return; }          // (the free camera: no player is followed, LateUpdate flies it)
'@
Rep @'
        void Release()
        {
'@ @'
        /// <summary>F: the camera leaves the player (and flies from where it was), or goes back to the one it followed. While it flies the view may tilt almost straight up and down.</summary>
        void SetFree(bool on)
        {
            if (on == FreeCam) return;
            if (on)
            {
                if (!rig) return;
                FreeCam = true;
                freePos = rig.transform.position;
                savedMinPitch = rig.minPitch; savedMaxPitch = rig.maxPitch;
                rig.minPitch = DevOverlayRules.FreeMinPitch; rig.maxPitch = DevOverlayRules.FreeMaxPitch;
                rig.spectate = null;
            }
            else
            {
                FreeCam = false;
                if (rig) { rig.minPitch = savedMinPitch; rig.maxPitch = savedMaxPitch; }
            }
        }

        void LateUpdate()
        {
            if (!FreeCam || !rig || !Spectating) return;
            Vector3 move = TestMove;
            bool fast = false, slow = false;
            if (move == Vector3.zero && Application.isFocused && !MenuOpen && !DevTerminal.Open && !EscapeMenu.Open)
            {
                if (Input.GetKey(KeyCode.W)) move.z += 1f;
                if (Input.GetKey(KeyCode.S)) move.z -= 1f;
                if (Input.GetKey(KeyCode.D)) move.x += 1f;
                if (Input.GetKey(KeyCode.A)) move.x -= 1f;
                if (Input.GetKey(KeyCode.E)) move.y += 1f;
                if (Input.GetKey(KeyCode.Q)) move.y -= 1f;
                fast = Input.GetKey(KeyCode.LeftShift); slow = Input.GetKey(KeyCode.LeftControl);
            }
            var rot = Quaternion.Euler(rig.AimPitch, rig.AimYaw, 0f);
            move = Vector3.ClampMagnitude(move, 1f);
            freePos += (rot * new Vector3(move.x, 0f, move.z) + Vector3.up * move.y) * DevOverlayRules.FreeStep(fast, slow, Time.unscaledDeltaTime);
            rig.transform.SetPositionAndRotation(freePos, rot);
        }

        void Release()
        {
            if (FreeCam) SetFree(false);
'@
Rep @'
            if (Event.current.type == EventType.Repaint && !IntroCinematic.Playing) DrawChip();
'@ @'
            if (Event.current.type == EventType.Repaint && !IntroCinematic.Playing)
            {
                if (Net.RunSync.Instance != null && DevSpectatorView.OverlayOn) DevSpectatorView.DrawOverlay(Camera.main);
                if (DevSpectatorView.NetPanelOn) DevSpectatorView.DrawNetPanel(WobblyPlayerRegistry.All.Count);
                DrawChip();
            }
'@
Rep '            string text = Active ? "DEV SPECTATOR   PLAYER " + WatchingNumber : "DEV SPECTATOR   waiting for the run";' '            string text = FreeCam ? "DEV SPECTATOR   FREE CAMERA" : Active ? "DEV SPECTATOR   PLAYER " + WatchingNumber : "DEV SPECTATOR   waiting for the run";'
Rep '            HudTheme.Chip(r, text, Active ? HudTheme.PlayerColour(WatchingNumber) : HudTheme.Dim, 22f, HudTheme.Ink);' '            HudTheme.Chip(r, text, FreeCam ? HudTheme.Info : Active ? HudTheme.PlayerColour(WatchingNumber) : HudTheme.Dim, 22f, HudTheme.Ink);'
Rep @'
            string hint = (Active ? "A / D or the mouse buttons: another player.   " : "") + (DevAccess.Unlocked ? "F1: dev menu." : "CTRL + F1: terminal.");
            HudTheme.Text(new Rect(r.x, r.yMax + 4f * s, r.width, 24f * s), hint, 15f, HudTheme.Muted, TextAnchor.MiddleCenter);
'@ @'
            string hint = (FreeCam ? "W A S D fly, E up, Q down, Shift fast, Ctrl slow.   " : Active ? "A / D or the mouse buttons: another player.   " : "")
                          + "F: free camera.   O: overlay.   N: network.   " + (DevAccess.Unlocked ? "F1: dev menu." : "CTRL + F1: terminal.");
            float hw = Mathf.Max(r.width, HudTheme.Width(hint, 15f) + 24f * s);
            HudTheme.Text(new Rect((Screen.width - hw) * 0.5f, r.yMax + 4f * s, hw, 24f * s), hint, 15f, HudTheme.Muted, TextAnchor.MiddleCenter);
'@
Save-Src

# ---- the pure checks are registered; the two-machine scenario grows a second half
$SC = $A + 'Editor\NetDev\NetDevTest.cs'; Open-Src $SC
Rep '            Asks1023HTest();' ('            Asks1023HTest();' + "`n" + '            Asks1023ITest();')
Save-Src

$SC = $A + 'Editor\NetDev\NetDevAsks1023H.cs'; Open-Src $SC
# (the host: after the party has been taken to floor 1, it hurts one guard and sends another after player 1, then tells the developer what it did)
Rep @'
            A23H("T29 host: the developer's 'F1' took the party to floor 1", hm.Floor == 1);
            Signal("h_floor1");
            yield return WaitSig("c_done", 120);
'@ @'
            A23H("T29 host: the developer's 'F1' took the party to floor 1", hm.Floor == 1);
            Signal("h_floor1");

            // ---- part 2: what the host tells the developer about the guards (the health and the target)
            yield return WaitSig("c_view_ready", 120);
            GuardAI hurt = null, chaser = null;
            foreach (var g in GuardAI.All)
            {
                if (!g || g.Dead || g.IsPuppet || g.NetId < 0) continue;
                if (hurt == null) hurt = g; else { chaser = g; break; }
            }
            A23H("(test) the host has two guards on floor 1 to try the record on", hurt != null && chaser != null);
            if (hurt != null && chaser != null)
            {
                const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
                hurt.BulletHitHost(2f, false, null);
                int hurtPct = DevOverlayRules.HealthByte(hurt.Health, hurt.MaxHealth, hurt.Dead);
                typeof(GuardAI).GetField("state", any).SetValue(chaser, GuardState.Chase);
                typeof(GuardAI).GetField("target", any).SetValue(chaser, Mine.GetComponent<WobblyPlayer>());
                A23H("T29 host: the guard who was hit has less health and the other has player 1 as his target", hurtPct < 100 && chaser.Target == Mine.GetComponent<WobblyPlayer>(), $"(health {hurtPct}%)");
                Signal("h_view", hurt.NetId + "," + hurtPct + "," + chaser.NetId);
            }
            else Signal("h_view", "-1,100,-1");
            yield return WaitSig("c_done", 120);
'@
# (the developer: reads the record, flies the free camera and looks at the network panel)
Rep @'
            A23H("T29 developer: floor 1 is built here too and the camera is on the player again", hm.Floor == 1 && DevSpectator.Active && NetPlayer.Local == null);
            Signal("c_done");
'@ @'
            A23H("T29 developer: floor 1 is built here too and the camera is on the player again", hm.Floor == 1 && DevSpectator.Active && NetPlayer.Local == null);

            // ---- part 2: the host's record of the guards, the free camera and the network panel
            yield return WaitFor(() => DevSpectatorView.RecordCount > 0, 30, "the host's record of the guards");
            A23H("T29 developer: the host's record of the guards has reached him and is fresh (one entry for every guard the host has)", DevSpectatorView.RecordCount >= 3 && DevSpectatorView.RecordAge >= 0f && DevSpectatorView.RecordAge < 1.5f, $"({DevSpectatorView.RecordCount} guards, {DevSpectatorView.RecordAge:0.00} s old)");
            Signal("c_view_ready");
            yield return WaitSig("h_view", 90);
            var seen = ReadSig("h_view").Split(',');
            int hurtId = int.Parse(seen[0]), hurtPct = int.Parse(seen[1]), chaseId = int.Parse(seen[2]);
            yield return new WaitForSeconds(1.2f);
            bool gotHurt = DevSpectatorView.TryRecord(hurtId, out var recHurt), gotChase = DevSpectatorView.TryRecord(chaseId, out var recChase);
            A23H("T29 developer: the wounded guard shows the health the host worked out", gotHurt && hurtPct < 100 && System.Math.Abs(recHurt.health - hurtPct) <= 1, $"(host {hurtPct}%, record {(gotHurt ? recHurt.health : -1)}%)");
            A23H("T29 developer: the guard the host sent after player 1 shows player 1 as his target, and the wounded one is after nobody", gotChase && DevOverlayRules.TargetNumber(recChase.target) == 1 && gotHurt && DevOverlayRules.TargetNumber(recHurt.target) == 0, $"(chaser's target {(gotChase ? recChase.target : (sbyte)-9)}, wounded one's {(gotHurt ? recHurt.target : (sbyte)-9)})");
            var chaseGuard = NetIds.Get<GuardAI>(chaseId);
            A23H("T29 developer: his own copy of that guard is a puppet that knows he is in the chase (the snapshots carry the state)", chaseGuard != null && chaseGuard.IsPuppet && chaseGuard.State == GuardState.Chase, $"(state {(chaseGuard ? chaseGuard.State.ToString() : "none")})");

            var rigNow = FindFirstObjectByType<CameraFollow>();
            DevSpectator.TestFreeToggle = true;
            yield return null; yield return null;
            A23H("T29 developer: F takes the camera off the player and makes it free", DevSpectator.FreeCam && rigNow != null && rigNow.spectate == null && !DevSpectator.Active);
            var p0 = rigNow.transform.position;
            DevSpectator.TestMove = new Vector3(0f, 0f, 1f);
            yield return new WaitForSeconds(1.0f);
            DevSpectator.TestMove = Vector3.zero;
            float flown = Vector3.Distance(rigNow.transform.position, p0);
            A23H("T29 developer: held forward for a second the free camera flies about seven metres", flown > 4.5f && flown < 9.5f, $"({flown:0.0} m)");
            DevSpectator.TestFreeToggle = true;
            yield return null; yield return null;
            A23H("T29 developer: F again gives the camera back to the player", !DevSpectator.FreeCam && DevSpectator.Active && rigNow.spectate != null);
            var link = DevSpectatorView.ReadLink();
            A23H("T29 developer: the network panel has its numbers (the tick rate, a round trip that is not negative)", link.tick > 0 && link.rttMs >= 0, $"(tick {link.tick} Hz, round trip {link.rttMs} ms, loss {link.lossPercent}, in {link.inBytes}, out {link.outBytes})");
            Signal("c_done");
'@
Save-Src
"T29 part 2 patched"
