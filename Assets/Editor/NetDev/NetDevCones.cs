using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The guards' cones of vision are off by default (the owner, 2026-10-22: "Make all guards vision cones off by default to players but still work the same way. Put a check box in the test menu that lets us toggle it on and off so we can see
    /// their vision as we dev"), with real guards on floor 1: a guard held still in plain view of the camera has no cone drawn while <c>GuardVision.ShowCones</c> is off, still has his flashlight beam, gets the cone when it is switched on, loses it
    /// again when it is switched off, and the tests' hook forces it on whatever the switch says. Run: <c>qa_regress.ps1 -Methods Cones</c>; it ends with <c>[Cones] DONE fails=N</c>. NOT covered: the check box in the F1 menu itself (look at it by hand),
    /// and that a guard still SEES the same (the GuardSight scenario covers his sight check and the cone it is drawn from).
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator ConesScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            int fails = 0;
            void Chk(string name, bool ok, string detail = "") { if (!ok) fails++; Log($"[Cones] {(ok ? "PASS" : "FAIL")} {name}" + (detail.Length > 0 ? " " + detail : "")); }
            var fanField = typeof(GuardVision).GetField("fanObject", BindingFlags.NonPublic | BindingFlags.Instance);
            var beamField = typeof(GuardVision).GetField("beam", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fanField == null || beamField == null)
            {
                Chk("the cone's fan object and the beam are where this test looks for them", false, "(were they renamed? update NetDevCones.cs)");
                Log($"[Cones] DONE fails={fails}");
                Quit(1);
                yield break;
            }
            bool FanOn(GuardVision v) { var o = v ? fanField.GetValue(v) as GameObject : null; return o && o.activeSelf; }
            bool BeamOn(GuardVision v) { var l = v ? beamField.GetValue(v) as Light : null; return l && l.enabled; }

            GuardVision.AlwaysShownForTests = false;
            yield return R5LoadFloor(1);
            var player = WobblyPlayerRegistry.All[0]; var rb = player.GetComponent<Rigidbody>(); var cam = Camera.main; var rig = FindFirstObjectByType<CameraFollow>();
            Chk("the cones are off by default (ShowCones is false when a floor starts)", !GuardVision.ShowCones || PlayerPrefs.GetInt(TestMode.ConesPrefKey, 0) == 1, "(the F1 menu's check box was ticked in an earlier session: untick it for this test)");
            GuardVision.ShowCones = false;

            // a guard held still with a clear 6 m line to stand at (as in R7)
            GuardAI target = null; Vector3 spot = default;
            foreach (var g in GuardAI.All)
            {
                if (!g || g.Dead || g.IsPuppet) continue;
                if (OpenSpot(g.transform.position, 6f, out spot, false)) { target = g; break; }
            }
            Chk("a guard with a clear 6 m line to stand at was found on floor 1", target != null);
            if (target == null || cam == null) { Log($"[Cones] DONE fails={fails}"); Quit(1); yield break; }
            var agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
            target.enabled = false;
            if (agent && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            var vis = target.GetComponent<GuardVision>();
            Chk("the guard has a vision cone component", vis != null);
            if (vis == null) { Log($"[Cones] DONE fails={fails}"); Quit(1); yield break; }
            if (rig) rig.enabled = false;
            player.TeleportTo(spot + Vector3.up * 1.1f, player.transform.rotation);
            yield return new WaitForSeconds(0.6f);
            var at = target.transform.position + Vector3.up * 0.9f;
            Face(player, target.transform.position, rb);
            AimCamera(cam, player, at);
            yield return new WaitForSeconds(1.2f);
            AimCamera(cam, player, at);
            yield return new WaitForSeconds(0.6f);

            Chk("in plain view of the camera he is shown (the fade the cone and the beam share)", vis.Shown > 0.95f, $"(shown {vis.Shown:0.00})");
            Chk("with the cones off his cone is NOT drawn, although he is in plain view", !FanOn(vis));
            Chk("... and his flashlight beam is still on (only the cone's picture is switched off)", BeamOn(vis));
            int anyOn = GuardAI.All.Count(g => g && FanOn(g.GetComponent<GuardVision>()));
            Chk("... and no other guard of the floor shows a cone either", anyOn == 0, $"({anyOn} drawn)");

            GuardVision.ShowCones = true;
            yield return new WaitForSeconds(0.6f);
            Chk("switched on (the F1 menu's check box) his cone is drawn", FanOn(vis));
            Chk("... and his beam is as it was", BeamOn(vis));
            GuardVision.ShowCones = false;
            yield return new WaitForSeconds(0.6f);
            Chk("switched off again his cone is gone", !FanOn(vis));
            GuardVision.AlwaysShownForTests = true;
            yield return new WaitForSeconds(0.6f);
            Chk("the tests' hook forces the cone on while the switch is off (the other scenarios read it)", FanOn(vis));
            GuardVision.AlwaysShownForTests = false;
            yield return new WaitForSeconds(0.6f);
            Chk("the hook off again, his cone is gone", !FanOn(vis));

            if (rig) rig.enabled = true;
            target.enabled = true;
            Log($"[Cones] DONE fails={fails}");
            Quit(fails == 0 ? 0 : 1);
        }
    }
}
