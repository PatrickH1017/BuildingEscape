using System.Collections;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of batch C part 3 of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63; offline, floor 1's outpost computer): at the computer the cursor over the monitor keeps the player looking at it; outside it the view looks round, a little and very
    /// slowly, within a limited range, and eases back; and with the view on the microphone and the cursor outside the monitor, E speaks into it (the PA) (T16). The real mouse cannot be moved in a test: the screen takes a test cursor.
    /// Run: <c>qa_regress_bd.ps1 -Methods Asks1023W</c>; the log ends with <c>[Asks1023W] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int a23wFails;

        void A23W(string name, bool ok, string detail = "")
        {
            if (!ok) a23wFails++;
            Log($"[Asks1023W] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator Asks1023WScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(1.5f);
            hm.godMode = true; hm.freezeTimer = true;
            foreach (var g in GuardAI.All) if (g) { g.sightRange = 0.1f; g.hearingMultiplier = 0f; }          // (the outpost's own guard would arrest the player at the computer)
            var player = WobblyPlayerRegistry.All[0];
            var rig = CameraFollow.Current;
            var computer = FindFirstObjectByType<ComputerProp>();
            A23W("(test) floor 1 has an outpost computer and the camera is there", computer != null && rig != null);
            if (computer == null || rig == null) { Quit(1); yield break; }
            var front = computer.transform.rotation * Vector3.forward;
            player.TeleportTo(computer.transform.position + front * 1.1f + Vector3.up * 0.2f, Quaternion.LookRotation(-front));
            yield return new WaitForSeconds(0.8f);
            ComputerScreen.SetTestCursor(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));          // (over the monitor from the start: the real mouse of a test run sits in a corner, and the view would already be turning)
            computer.Use(player);
            yield return new WaitForSeconds(0.4f);
            var login = computer.Credentials;
            ComputerScreen.TryLogin(login.user, login.pass);
            yield return new WaitForSeconds(0.5f);
            bool known = ComputerScreen.TryGetMonitor(out var mon);
            A23W("T16: the computer says where its monitor is on the screen", known && mon.width > 300f && mon.height > 200f, known ? $"({mon})" : "");
            if (!known) { Quit(1); yield break; }
            float midY = Screen.height - mon.center.y;                                          // (the cursor's y runs up)

            // ---- the cursor over the monitor: the player stays looking at it
            ComputerScreen.SetTestCursor(new Vector2(mon.center.x, midY));
            yield return new WaitForSeconds(1.0f);
            A23W("T16: with the cursor over the monitor the view does not turn", rig.DeskLook == Vector2.zero, $"({rig.DeskLook})");
            var baseForward = Camera.main.transform.forward;

            // ---- the cursor at the right edge of the screen: the view turns to the right, slowly, and stops at the limit
            ComputerScreen.SetTestCursor(new Vector2(Screen.width, midY));
            yield return new WaitForSeconds(0.3f);
            float after03 = rig.DeskLook.x;
            A23W("T16: it turns slowly: after 0.3 s it has made about half the way (a lot of deceleration)", after03 > 6f && after03 < 24f, $"({after03:0.0} of 35 degrees)");
            yield return new WaitForSeconds(2.7f);
            var look = rig.DeskLook;
            A23W("T16: after a few seconds it has turned the whole 35 degrees to the right and no further, with no tilt", look.x > 33f && look.x <= 35.01f && Mathf.Abs(look.y) < 1f, $"({look})");
            float turned = Vector3.Angle(new Vector3(baseForward.x, 0f, baseForward.z), new Vector3(Camera.main.transform.forward.x, 0f, Camera.main.transform.forward.z));
            A23W("T16: the camera itself is turned by that much", Mathf.Abs(turned - look.x) < 4f, $"(the camera turned {turned:0.0} degrees)");

            // ---- the cursor at the top left beyond the monitor: up and to the left, within the limits
            ComputerScreen.SetTestCursor(new Vector2(0f, Screen.height));
            yield return new WaitForSeconds(3.0f);
            look = rig.DeskLook;
            A23W("T16: with the cursor at the top left the view looks up and to the left, within 35 and 20 degrees", look.x < -33f && look.x >= -35.01f && look.y < -17f && look.y >= -20.01f, $"({look})");

            // ---- back over the monitor: it eases back
            ComputerScreen.SetTestCursor(new Vector2(mon.center.x, midY));
            yield return new WaitForSeconds(0.4f);
            float back04 = rig.DeskLook.x;
            yield return new WaitForSeconds(3.0f);
            A23W("T16: back over the monitor the view eases back (slowly: still turned after 0.4 s), and is straight again after a few seconds", Mathf.Abs(back04) > 4f && rig.DeskLook == Vector2.zero, $"(after 0.4 s {back04:0.0}, after 3.4 s {rig.DeskLook})");

            // ---- the microphone: the view turned on it with the cursor outside the monitor, E is the PA; over the monitor E is only a letter
            var mic = MicProp.Active;
            A23W("(test) the outpost's microphone is on the desk", mic != null);
            if (mic != null)
            {
                // the player's way: the cursor is moved about outside the monitor and the view follows; the best of a grid of places (what the game's own camera does, orbit and all) has to put the middle of the view on the button
                var cam = Camera.main;
                float bestAngle = 999f; Vector2 bestCursor = default; string scan = "";
                foreach (float fx in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                    foreach (float fy in new[] { 0f, 0.5f, 1f })
                    {
                        float cxs = fx < 0.5f ? Mathf.Lerp(0f, mon.xMin, fx * 2f) : fx > 0.5f ? Mathf.Lerp(mon.xMax, Screen.width, (fx - 0.5f) * 2f) : mon.center.x;
                        float cys = fy < 0.5f ? Screen.height : fy > 0.5f ? 0f : midY;                           // (y runs up: 0 is the bottom edge)
                        if (!DeskLookRules.Outside(cxs, Screen.height - cys, mon.x, mon.y, mon.width, mon.height)) continue;
                        ComputerScreen.SetTestCursor(new Vector2(cxs, cys));
                        yield return new WaitForSeconds(2.2f);
                        float ang = Vector3.Angle(cam.transform.forward, mic.ButtonWorld - cam.transform.position);
                        scan += $"[{fx:0.00},{fy:0.0}]={ang:0}  ";
                        if (ang < bestAngle) { bestAngle = ang; bestCursor = new Vector2(cxs, cys); }
                    }
                ComputerScreen.SetTestCursor(bestCursor);
                yield return new WaitForSeconds(2.5f);
                {
                    A23W("T16: with the cursor outside the monitor the view can be brought to rest on the microphone", ComputerScreen.LookingAtMicrophone(), $"(best {bestAngle:0.0} degrees off the button at the cursor {bestCursor}; the middle of the view is {Vector3.Angle(cam.transform.forward, mic.ButtonWorld - cam.transform.position):0.0} off now, {(mic.ButtonWorld - cam.transform.position).magnitude:0.0} m from the camera; angles over the grid {scan})");
                    ComputerScreen.TestKeyE = true;
                    yield return new WaitForSeconds(0.4f);
                    A23W("T16: E held with the view on the microphone puts the PA on (the microphone's button is down)", PaState.Live && mic.Pressed, $"(PA live {PaState.Live}, button pressed {mic.Pressed})");
                    ComputerScreen.TestKeyE = false;
                    yield return new WaitForSeconds(0.4f);
                    A23W("T16: let go of E, the PA goes off", !PaState.Live);
                    // the same E with the cursor over the monitor does nothing: it is a letter there
                    ComputerScreen.SetTestCursor(new Vector2(mon.center.x, midY));
                    yield return new WaitForSeconds(3.0f);
                    ComputerScreen.TestKeyE = true;
                    yield return new WaitForSeconds(0.4f);
                    A23W("T16: E with the cursor over the monitor is only a letter: the PA stays off", !PaState.Live);
                    ComputerScreen.TestKeyE = false;
                }
            }

            // ---- the screen shut: the look is given back
            ComputerScreen.SetTestCursor(new Vector2(Screen.width, midY));
            yield return new WaitForSeconds(2.0f);
            OutpostUi.Hide(OutpostUi.Kind.Computer);
            yield return new WaitForSeconds(0.3f);
            ComputerScreen.SetTestCursor(null);
            yield return new WaitForSeconds(2.5f);
            A23W("T16: with the screen shut the view is straight again and the look is no longer driven", !ComputerScreen.Showing && rig.DeskLook == Vector2.zero, $"({rig.DeskLook})");

            Log($"[Asks1023W] DONE fails={a23wFails}");
            Quit(a23wFails == 0 ? 0 : 1);
        }
    }
}
