using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The owner's restraint, drag and rescue clips (2026-10-28), scenario <c>RestraintRescue</c> (stage 4, one process): on floor 1 the real player lies hogtied (busted) and a copy of him (the teammate) frees him by holding E for four seconds:
    /// the free-up clip runs as the hold does, the ties come off, a let-go winds it back, a full hold puts him on his feet with the pin immunity; a captive an officer drags or is being dropped cannot be freed. Needs graphics:
    /// <c>qa_regress_bd.ps1 -Methods RestraintRescue:gfx</c>. Pictures: %TEMP%\be_shot_restraintr_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int rrFails;

        void RrCheck(string name, bool ok, string detail = "")
        {
            if (!ok) rrFails++;
            Log($"[RestraintRescue] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator RestraintRescueScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            hm.godMode = true;
            hm.freezeTimer = true;
            GunController.TestCursorLocked = true;
            Time.captureDeltaTime = 1f / 30f;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(1.0f);
            var cap = WobblyPlayerRegistry.All[0];
            var rig = FindFirstObjectByType<CameraFollow>();
            var capRv = cap.GetComponent<RestraintVisual>();
            if (!rig || capRv == null) { Log("[RestraintRescue] no camera rig or restraint visual"); Quit(1); yield break; }
            rig.SetFirstPerson(false);

            // the teammate: a copy of the player, a few metres off
            var mateGo = Object.Instantiate(cap.gameObject); mateGo.name = "Rescuer";
            var mate = mateGo.GetComponent<WobblyPlayer>();
            var mateRv = mate.GetComponent<RestraintVisual>();
            var mateRescue = mate.GetComponent<PlayerRescue>();
            RrCheck("(test) the teammate has the rescue component and his own restraint visual", mateRescue != null && mateRv != null && mateRv != capRv && mate != cap);
            Vector3 SpotNear(Vector3 around)
            {
                return NavMesh.SamplePosition(around, out var h, 1.0f, NavMesh.AllAreas) ? h.position + Vector3.up * 1.1f : around + Vector3.up * 0.3f;          // (the spawn room is not on the navigation mesh: then the spot itself)
            }
            mate.TeleportTo(SpotNear(cap.transform.position + Vector3.right * 4f), Quaternion.identity);
            yield return new WaitForSeconds(0.5f);

            var side = new GameObject("RestraintRescueCam").AddComponent<Camera>();
            side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = new Color(0.55f, 0.66f, 0.82f); side.enabled = false; side.nearClipPlane = 0.02f;
            void Shot(string name, float away = 2.8f, float high = 1.5f, float fov = 45f)
            {
                var focus = (capRv.ModelRoot ? capRv.ModelRoot.position : cap.transform.position) + Vector3.up * 0.3f;
                side.fieldOfView = fov;
                foreach (var ang in new[] { 90f, -90f, 135f, -135f, 45f, -45f, 180f, 0f })
                {
                    var pos = focus + Quaternion.Euler(0f, ang, 0f) * cap.transform.forward * away + Vector3.up * high;
                    if (Physics.Linecast(focus, pos, ~0, QueryTriggerInteraction.Ignore)) continue;
                    side.transform.position = pos; side.transform.LookAt(focus);
                    ShotCam(side, "restraintr_" + name);
                    return;
                }
                side.transform.position = focus + Vector3.up * 3f; side.transform.LookAt(focus);
                ShotCam(side, "restraintr_" + name);
            }

            // ---------------- the captive lies hogtied; the teammate is too far to free him
            cap.ApplyCustody(CustodyState.Busted, 0f, 1);
            yield return new WaitForSeconds(1.0f);
            RrCheck("(test) the captive lies hogtied", capRv.Current == RestraintVisual.Look.Hogtied && cap.Custody == CustodyState.Busted, $"(look {capRv.Current})");
            RrCheck("4 m away the teammate has nobody to free", PlayerRescue.FindCaptive(mate) == null && mateRescue.Candidate == null);
            var spot = SpotNear(cap.transform.position + Vector3.right * 1.5f);
            Log($"[RestraintRescue] captive at {cap.transform.position}, teammate was at {mate.transform.position}, goes to {spot}, kinematic {mate.GetComponent<Rigidbody>().isKinematic}");
            mate.TeleportTo(spot, Quaternion.identity);
            yield return new WaitForSeconds(0.4f);
            Log($"[RestraintRescue] teammate now at {mate.transform.position}");
            RrCheck("beside him (within reach) the teammate has a captive to free", PlayerRescue.FindCaptive(mate) == cap && mateRescue.Candidate == cap, $"(distance {Vector3.Distance(mate.transform.position, cap.transform.position):0.0} m)");

            // ---------------- not while an officer drags him, nor while an officer is dropping him
            var anyGuard = GuardAI.All.FirstOrDefault(x => x && !x.Dead);
            cap.DraggedByGuard = anyGuard;
            RrCheck("a captive an officer drags cannot be freed", !PlayerRescue.CanBeFreed(cap) && PlayerRescue.FindCaptive(mate) == null);
            cap.DraggedByGuard = null;
            cap.NoteDrop(anyGuard, 1);
            RrCheck("a captive the officer has just dropped cannot be freed until the drop is over", !PlayerRescue.CanBeFreed(cap));
            yield return new WaitForSeconds(RestraintRules.DropSeconds + 0.3f);
            RrCheck("and can be when it is over", PlayerRescue.CanBeFreed(cap) && PlayerRescue.FindCaptive(mate) == cap);
            yield return new WaitForSeconds(0.2f);

            // ---------------- hold E: the clip runs as the hold does
            float held0 = Time.time;
            PlayerRescue.TestHold = true;
            yield return new WaitForSeconds(0.5f);
            RrCheck("holding E beside him: the teammate works on him, is held still beside him, and the progress grows (about an eighth after half a second)", mate.RescueTarget == cap && mate.inputLocked && cap.FreeProgress > 0.08f && cap.FreeProgress < 0.22f, $"(target {(mate.RescueTarget ? mate.RescueTarget.name : "none")}, locked {mate.inputLocked}, progress {cap.FreeProgress:0.00})");
            RrCheck("the two halves of the free-up: the captive plays his, the teammate kneels beside him with the other", capRv.Current == RestraintVisual.Look.FreeUpFreed && (mateRv.Anim == null || !mateRv.Anim.CanPlayBody || mateRv.Current == RestraintVisual.Look.FreeUpRescuer), $"(captive {capRv.Current}, teammate {mateRv.Current})");
            RrCheck("an eighth in (clip 0.75 s): all three ties are still on", capRv.Ties != null && capRv.Ties.WristsOn && capRv.Ties.AnklesOn && capRv.Ties.LinkOn, $"(progress {cap.FreeProgress:0.00})");
            Shot("hold_q1");
            while (cap.FreeProgress < 0.45f && Time.time - held0 < 5f) yield return null;
            RrCheck("(half way) the ankles' ties are off and the wrists' are still on", capRv.Ties != null && !capRv.Ties.AnklesOn && capRv.Ties.WristsOn, $"(progress {cap.FreeProgress:0.00}, ankles {capRv.Ties?.AnklesOn}, wrists {capRv.Ties?.WristsOn})");
            Shot("hold_half");
            while (cap.FreeProgress < 0.6f && Time.time - held0 < 5f) yield return null;
            Shot("hold_60");

            // ---------------- let go: it winds back, he is not free
            PlayerRescue.TestHold = false;
            float atRelease = cap.FreeProgress;
            yield return new WaitForSeconds(0.2f);
            RrCheck("let go: the teammate can walk again", !mate.inputLocked && mate.RescueTarget == null, $"(locked {mate.inputLocked})");
            yield return new WaitForSeconds(0.8f);
            RrCheck("and the progress winds back (a quarter of it a second), he is still a captive", cap.FreeProgress < atRelease - 0.15f && cap.FreeProgress > 0f && cap.Custody == CustodyState.Busted && capRv.Current == RestraintVisual.Look.FreeUpFreed, $"(from {atRelease:0.00} to {cap.FreeProgress:0.00}, look {capRv.Current})");
            yield return WaitFor(() => cap.FreeProgress <= 0f, 6f, "the progress to wind back");
            yield return new WaitForSeconds(0.4f);
            RrCheck("wound all the way back he lies hogtied again, all three ties on", capRv.Current == RestraintVisual.Look.Hogtied && capRv.Ties != null && capRv.Ties.WristsOn && capRv.Ties.AnklesOn && capRv.Ties.LinkOn, $"(look {capRv.Current})");
            RrCheck("the ties' ratchets were heard going off and coming back on", capRv.Ratchets >= 4, $"({capRv.Ratchets})");

            // ---------------- the full hold
            float t1 = Time.time;
            PlayerRescue.TestHold = true;
            yield return WaitFor(() => cap.Custody == CustodyState.Free, 8f, "him to be freed");
            float took = Time.time - t1;
            PlayerRescue.TestHold = false;
            yield return new WaitForSeconds(0.3f);
            RrCheck("a full hold takes four seconds and frees him", cap.Custody == CustodyState.Free && took > 3.7f && took < 4.8f, $"({took:0.00} s, custody {cap.Custody})");
            RrCheck("freed: no progress left, three seconds in which nobody can pin him (a break-free's)", cap.FreeProgress == 0f && cap.PinImmuneUntil > Time.time && !CustodyRules.CanPin(CustodyState.Free, Time.time, cap.PinImmuneUntil), $"(immune for {cap.PinImmuneUntil - Time.time:0.0} s more)");
            RrCheck("freed: he stands (the capsule is upright again, no clip on him, the ties off) and the teammate is free to walk", !cap.IsLying && capRv.Current == RestraintVisual.Look.None && (capRv.Ties == null || !capRv.Ties.WristsOn) && !mate.inputLocked && mate.RescueTarget == null, $"(lying {cap.IsLying}, look {capRv.Current}, teammate locked {mate.inputLocked})");
            RrCheck("the banner says he was freed", hm.BackupMessage != null && hm.BackupMessage.Contains("freed"), $"(\"{hm.BackupMessage}\")");
            Shot("freed");

            PlayerRescue.TestHold = false;
            Object.Destroy(mateGo);
            GunController.TestCursorLocked = null;
            Time.captureDeltaTime = 0f;
            Log($"[RestraintRescue] DONE fails={rrFails}");
            Quit(rrFails == 0 ? 0 : 1);
        }
    }
}
