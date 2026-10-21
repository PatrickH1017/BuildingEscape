# Round 12, batch G36 (the owner, 2026-10-20, while the build was being tested): "Guards that are hit should call for backup upon getting up" / "Backup guards should have a much shorter scan time when a player enters their vision cone" /
# "The dart camera should also be able to go up and down as well".
#  (1) a guard who was KNOCKED OVER (melee, punch, a thrown thing: `GuardAI.Stun` with somebody who did it) radios for backup when he gets up (the same visible call as a sighting, `callSeconds`, whether or not he sees anybody; another blow
#      cuts it like any call), then goes after the one who hit him (a bullet does not knock over, so it starts no call; the boss and a stun without an attacker (the test panel's) do not either);
#  (2) a backup officer (`GuardAI.IsBackup`, set when the floor spawns the reinforcements) notices a player in his cone in `GuardRules.BackupSpotFactor` (a quarter of) the usual time;
#  (3) the dart camera: the pitch range is -30 .. 50 degrees (it was -8 .. 24, which could not even reach the lower third of the board), `CameraFollow.TiltAim` / `AimPitch`, and the keys plate says so.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'

# ---------------------------------------------------------------- the rules
Open-Src "$A\GuardRules.cs"
Rep 'public static float SpotSeconds(float distance, float baseSeconds = SpotBase, float perMetre = SpotPerMetre) => baseSeconds + perMetre * (distance > 0f ? distance : 0f);' @'
public static float SpotSeconds(float distance, float baseSeconds = SpotBase, float perMetre = SpotPerMetre) => baseSeconds + perMetre * (distance > 0f ? distance : 0f);

        /// <summary>A backup officer (called in by a radio call or the alarm) notices a player in his cone in this fraction of the usual time (the owner, 2026-10-20: "Backup guards should have a much shorter scan time when a player enters their vision cone"): 14 m takes about 0.5 s instead of 1.8 s.</summary>
        public const float BackupSpotFactor = 0.25f;
'@
Save-Src

# ---------------------------------------------------------------- the guard
Open-Src "$A\GuardAI.cs"
Rep 'float unseenFor; Vector3 lastSeenPos;' @'
float unseenFor; Vector3 lastSeenPos;

        // ---- a guard who was knocked over radios for backup when he gets up (the owner, 2026-10-20: "Guards that are hit should call for backup upon getting up")
        bool callOnGetUp, callAnyway;
        WobblyPlayer hitBy;
        /// <summary>A backup officer (spawned by a radio call or the alarm): he notices a player in his cone much faster (<see cref="GuardRules.BackupSpotFactor"/>; the owner, 2026-10-20). Set by the floor when it spawns the reinforcements.</summary>
        public bool IsBackup;
        /// <summary>How long a player at <paramref name="distance"/> metres has to stay in his cone before he notices (a backup officer needs a quarter of it).</summary>
        public float SpotSecondsAt(float distance) => GuardRules.SpotSeconds(distance, spotBase, spotPerMetre) * (IsBackup ? GuardRules.BackupSpotFactor : 1f);
'@
Rep 'if (by) noiseTarget = by.transform.position;' @'
if (by)
            {
                noiseTarget = by.transform.position;
                hitBy = by.GetComponentInParent<WobblyPlayer>();
                if (!IsBoss) callOnGetUp = true;                     // (knocked over by somebody: when he gets up he radios for backup)
            }
'@
Rep 'if (calling) { agent.isStopped = true; State = GuardState.Calling; return; } // still watching' @'
if (calling) { agent.isStopped = true; State = GuardState.Calling; return; } // still watching
                        if (callOnGetUp) { callOnGetUp = false; BeginGetUpCall(); return; }
'@
Rep 'calling = true; callTimer = 0f; unseenFor = 0f;' 'calling = true; callTimer = 0f; unseenFor = 0f; callAnyway = false;'
Rep 'if (State == GuardState.Calling && !StillSees()) { CancelCall(); return; }' 'if (State == GuardState.Calling && !callAnyway && !StillSees()) { CancelCall(); return; }'
Rep '// he still saw the player after watching: backup is on the way and he starts the chase' @'
if (callAnyway) { callAnyway = false; AfterGetUpCall(); return; }
                    // he still saw the player after watching: backup is on the way and he starts the chase
'@
Rep '/// <summary>The first seconds after spotting: the guard stands still and looks at what he saw. No chasing yet.</summary>' @'
/// <summary>
        /// He was knocked over and is on his feet again: he radios for backup where he stands, for <see cref="callSeconds"/> and whether or not he sees anybody (the owner, 2026-10-20: "Guards that are hit should call for backup upon
        /// getting up"). It is the call of a sighting (the ring over his head, another blow cuts it); at the end the officers are sent to him (<see cref="AfterGetUpCall"/>).
        /// </summary>
        void BeginGetUpCall()
        {
            calling = true; callTimer = 0f; unseenFor = 0f; callAnyway = true;
            target = hitBy;
            if (hitBy) lastSeenPos = hitBy.transform.position;
            State = GuardState.Calling;
            agent.isStopped = true;
            Debug.Log($"{name} got up and is calling for backup");
        }

        /// <summary>The call he made on getting up is through (backup is on the way): he goes after the player who hit him, or to where the blow came from.</summary>
        void AfterGetUpCall()
        {
            if (hitBy && hitBy.Custody == CustodyState.Free)
            {
                target = hitBy;
                State = GuardState.Chase;
                unseenFor = 0f;
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                agent.SetDestination(hitBy.transform.position);
            }
            else
            {
                State = GuardState.Investigate;
                agent.isStopped = false;
                agent.speed = investigateSpeed;
                agent.SetDestination(noiseTarget);
                waitUntil = 0;
            }
        }

        /// <summary>The first seconds after spotting: the guard stands still and looks at what he saw. No chasing yet.</summary>
'@
Rep 'seenFor >= GuardRules.SpotSeconds(dist, spotBase, spotPerMetre)' 'seenFor >= SpotSecondsAt(dist)'
Save-Src

# ---------------------------------------------------------------- the floor: reinforcements are backup officers
Open-Src "$A\FloorGenerator.cs"
Rep 'ai.roam = RandomWalkable;   // after reaching the spot, wander freely again' @'
ai.roam = RandomWalkable;   // after reaching the spot, wander freely again
                ai.IsBackup = true;         // (a backup officer notices a player in his cone much faster: GuardRules.BackupSpotFactor)
'@ 2
Save-Src

# ---------------------------------------------------------------- the dart camera: up and down as well
Open-Src "$A\Darts\DartsRules.cs"
Rep 'TurnHalf = 25f, PitchMin = -8f, PitchMax = 24f;' 'TurnHalf = 25f, PitchMin = -30f, PitchMax = 50f;'
Rep '("MOUSE", "turn the camera slightly"),' '("MOUSE", "turn the camera slightly, up and down too"),'
Save-Src

Open-Src "$A\CameraFollow.cs"
Rep '        public void ClearAimLimit() => aimLimited = false;' @'
        public void ClearAimLimit() => aimLimited = false;
        /// <summary>The camera tilts by this many degrees (positive: further down; the tests call it too): kept within the limit, if there is one (the owner, 2026-10-20: "The dart camera should also be able to go up and down as well").</summary>
        public void TiltAim(float dpitch) => pitch = aimLimited ? Mathf.Clamp(pitch + dpitch, limitPitchMin, limitPitchMax) : Mathf.Clamp(pitch + dpitch, minPitch, maxPitch);
        /// <summary>How far the camera is tilted down (negative: up), in degrees.</summary>
        public float AimPitch => pitch;
'@
Rep '                pitch = aimLimited ? Mathf.Clamp(pitch - my, limitPitchMin, limitPitchMax) : Mathf.Clamp(pitch - my, minPitch, maxPitch);' '                TiltAim(-my);'
Save-Src

# ---------------------------------------------------------------- the tests (pure)
Open-Src "$E\NetDevRound12.cs"
Rep '&& DartsRules.StandBehind > 0f && DartsRules.StandBehind < DartsRules.LineZoneBehind && DartsRules.LateralHalf < DartsRules.LineZoneHalfWidth && DartsRules.PitchMin < DartsRules.PitchMax);' '&& DartsRules.StandBehind > 0f && DartsRules.StandBehind < DartsRules.LineZoneBehind && DartsRules.LateralHalf < DartsRules.LineZoneHalfWidth && DartsRules.PitchMin <= -20f && DartsRules.PitchMax >= 40f);'
Rep @'
            // ---- the weapon bar: the first three weapons of the pack, in the order they came into it
'@ @'
            // ---- backup officers spot a player much faster (the owner, 2026-10-20)
            Check("guards: a backup officer notices a player in his cone in a quarter of the usual time (14 m: about 0.5 s instead of 1.8 s), the usual guard as before",
                GuardRules.BackupSpotFactor <= 0.34f && GuardRules.BackupSpotFactor > 0f && GuardRules.SpotSeconds(14f) * GuardRules.BackupSpotFactor < 0.5f && GuardRules.SpotSeconds(14f) > 1.5f);
            // ---- the weapon bar: the first three weapons of the pack, in the order they came into it
'@
Rep 'turnedRight && turnedLeft, $"(yaw {rig.AimYaw:0}, centre {centreYaw:0})");' @'
turnedRight && turnedLeft, $"(yaw {rig.AimYaw:0}, centre {centreYaw:0})");
                // the camera also goes up and down (the owner, 2026-10-20: "The dart camera should also be able to go up and down as well"): from looking 30 degrees up to 50 degrees down, which takes in the scoreboard and all of the board
                float pitchStart = rig.AimPitch;
                rig.TiltAim(1000f); float pitchDown = rig.AimPitch;
                float sightDown = float.NaN, sightUp = float.NaN;
                var camTilt = Camera.main;
                float SightHeight() { var sr = new Ray(camTilt.transform.position, camTilt.transform.forward); float dn = Vector3.Dot(sr.direction, board.Normal); return dn > -0.001f ? float.NaN : sr.GetPoint(Vector3.Dot(board.FaceCentre - sr.origin, board.Normal) / dn).y; }
                if (camTilt != null) { yield return new WaitForSeconds(0.4f); sightDown = SightHeight(); }
                rig.TiltAim(-1000f); float pitchUp = rig.AimPitch;
                if (camTilt != null) { yield return new WaitForSeconds(0.4f); sightUp = SightHeight(); }
                R12Check("the camera also tilts up and down: from 30 degrees up to 50 degrees down and no further; looking down the line of sight passes below the board, looking up it passes above the scoreboard",
                    Mathf.Abs(pitchDown - DartsRules.PitchMax) < 0.01f && Mathf.Abs(pitchUp - DartsRules.PitchMin) < 0.01f && (camTilt == null || (sightDown < board.FaceCentre.y - 0.35f && sightUp > board.FaceCentre.y + DartBoard.ScoreboardHeight + 0.3f)),
                    $"(start {pitchStart:0}, down {pitchDown:0}, up {pitchUp:0}, the line of sight at the board: {sightDown:0.00} m low, {sightUp:0.00} m high)");
'@
Save-Src

# ---------------------------------------------------------------- the scenario: guards that are hit call for backup when they get up
Open-Src "$E\NetDevHarness.cs"
Rep @'
        public static void DockLook()
        {
'@ @'
        public static void GuardCall()
        {
            SessionState.SetString("netdev.mode", "guardcall");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void DockLook()
        {
'@
Rep 'mode != "docklook" && ' 'mode != "docklook" && mode != "guardcall" && '
Rep @'
                case "docklook": yield return DockLookScenario(); break;
'@ @'
                case "docklook": yield return DockLookScenario(); break;
                case "guardcall": yield return GuardCallScenario(); break;
'@
Save-Src

$cs = @'
using System.Collections;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Guards that are hit call for backup when they get up, and backup officers spot a player faster (the owner, 2026-10-20: "Guards that are hit should call for backup upon getting up" / "Backup guards should have a much shorter scan time when a
    /// player enters their vision cone"). On floor 1 nobody can see the player (sight range 0.1 m), so only the blows count: a melee blow knocks a guard over, on his feet he radios (the call ring) and backup is called when it is through; a second
    /// blow cuts the call; a punch does the same; a bullet does not knock over and starts no call; the officers that come are backup officers with a quarter of the usual spotting time. Run: <c>qa_regress.ps1 -Methods GuardCall</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int guardCallFails;

        void GuardCallCheck(string name, bool ok, string detail = "")
        {
            if (!ok) guardCallFails++;
            Log($"[GuardCall] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator GuardCallScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            tm.GoToLevel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return WaitFor(() => !tm.Loading && hm.Floor == 1 && hm.Phase == HeistPhase.Heist, 90, "floor 1");
            hm.godMode = true; hm.freezeTimer = true;
            yield return WaitFor(() => GuardAI.All.Count(g => g && !g.Dead && !g.IsBoss) >= 3, 30, "three guards on the floor");
            yield return new WaitForSeconds(1.0f);
            foreach (var g in GuardAI.All) if (g) g.sightRange = 0.1f;                 // (nobody sees the player: only the blows count)
            var guards = GuardAI.All.Where(g => g && !g.Dead && !g.IsBoss && !g.IsBackup).ToList();
            var melee = guards[0]; var bullet = guards[1]; var punch = guards[2];
            GuardCallCheck("no backup has been called yet and the guards are ordinary ones (not backup officers)", !hm.BackupCalled && guards.All(g => !g.IsBackup));

            // ---- a melee blow: knocked over, then on his feet he radios; a second blow cuts the call and he gets up and calls again
            melee.MeleeHitHost(1.0f, player.gameObject);
            GuardCallCheck("a melee blow knocks the guard over: he is stunned and not calling yet", melee.State == GuardState.Stunned && !melee.IsCalling);
            yield return new WaitForSeconds(1.5f);
            GuardCallCheck("on his feet again he radios for backup where he stands (the call has begun, no officer is sent yet)", melee.State == GuardState.Calling && melee.IsCalling && !hm.BackupCalled, $"(state {melee.State}, calling {melee.IsCalling}, backup {hm.BackupCalled})");
            melee.MeleeHitHost(1.0f, player.gameObject);
            GuardCallCheck("another blow cuts the call and knocks him over again", melee.State == GuardState.Stunned && !melee.IsCalling && !hm.BackupCalled);
            yield return new WaitForSeconds(1.5f);
            GuardCallCheck("and when he gets up the second time he radios again", melee.State == GuardState.Calling && melee.IsCalling && !hm.BackupCalled);
            int before = GuardAI.All.Count(g => g && !g.Dead);
            yield return new WaitForSeconds(melee.callSeconds + 0.8f);
            int after = GuardAI.All.Count(g => g && !g.Dead);
            GuardCallCheck("when the call is through backup is called: officers are sent", hm.BackupCalled && after > before, $"(backup {hm.BackupCalled}, guards {before} -> {after})");
            GuardCallCheck("and he goes after the player who hit him (or to where the blow came from), no longer calling", !melee.IsCalling && (melee.State == GuardState.Chase || melee.State == GuardState.Investigate), $"(state {melee.State})");

            // ---- the officers that came are backup officers: a quarter of the usual spotting time
            var officers = GuardAI.All.Where(g => g && g.name.StartsWith("Backup_")).ToList();
            float usual = melee.SpotSecondsAt(10f);
            GuardCallCheck("the officers that came are backup officers and notice a player at 10 m in a quarter of the usual time", officers.Count >= 1 && officers.All(g => g.IsBackup && Mathf.Abs(g.SpotSecondsAt(10f) - GuardRules.SpotSeconds(10f, g.spotBase, g.spotPerMetre) * GuardRules.BackupSpotFactor) < 0.001f) && usual > 1f && !melee.IsBackup,
                $"({officers.Count} officers, usual {usual:0.00} s, backup {(officers.Count > 0 ? officers[0].SpotSecondsAt(10f) : -1f):0.00} s)");

            // ---- a punch knocks over too: he calls on getting up. A bullet does not knock over: no call
            punch.PunchHost(1.0f, player.gameObject);
            bullet.BulletHitHost(1f, false, player.gameObject);
            GuardCallCheck("a punch knocks a guard over, a bullet does not", punch.State == GuardState.Stunned && bullet.State != GuardState.Stunned);
            yield return new WaitForSeconds(1.5f);
            GuardCallCheck("the guard who was punched radios for backup on getting up", punch.State == GuardState.Calling && punch.IsCalling, $"(state {punch.State}, calling {punch.IsCalling})");
            yield return new WaitForSeconds(2.0f);
            GuardCallCheck("the guard who was only shot at starts no call", !bullet.IsCalling && bullet.State != GuardState.Calling, $"(state {bullet.State})");

            Log($"[GuardCall] DONE fails={guardCallFails}");
            Quit(guardCallFails == 0 ? 0 : 1);
        }
    }
}
'@
if ($env:R2_DRY) { Write-Output "dry run: would write $E\NetDevGuardCall.cs" }
else {
    [IO.File]::WriteAllText("$E\NetDevGuardCall.cs", $cs.Replace("`r`n", "`n").Replace("`n", "`r`n"), (New-Object Text.UTF8Encoding($false)))
    Write-Output "wrote $E\NetDevGuardCall.cs"
}

# ---------------------------------------------------------------- the words in the playtest change list
Open-Src 'C:\bqs\README_PLAYTEST.txt'
Rep 'E leaves; the keys are shown on the screen.' 'E leaves; the camera also tilts up and down; the keys are shown on the screen. A guard you knock over radios for backup when he gets up (another blow cuts the call), and the officers who come as backup notice you much faster than the others.'
Save-Src
Write-Output 'round 12 batch G36 patched'
