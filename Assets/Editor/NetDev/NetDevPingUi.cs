using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pictures of the ping markers (the owner, 2026-10-21): IMGUI is not drawn in batch mode, so run it in a real Editor on the hidden desktop (<c>ui_dev.ps1 -Method PingUi</c>). Pictures: %TEMP%\be_ui_ping_*.png.
    /// Three players' pings in their colours (a valuable, a gun, a spare clip), one out of view (the arrow on the edge of the screen), a door, and the Keybinds screen with its new row.
    /// </summary>
    public partial class NetDevRunner
    {
        int pingUiFails;

        void PingUiCheck(string name, bool ok, string detail = "")
        {
            if (!ok) pingUiFails++;
            Log($"[PingUi] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator PingUiScenario()
        {
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var rig = FindFirstObjectByType<CameraFollow>();
            yield return new WaitForSeconds(1.0f);
            Log($"[PingUi] screen {Screen.width}x{Screen.height}");
            // somewhere in the hub with a clear view: look along +z from the player's place
            var eye = player.transform.position + Vector3.up * 1.6f;
            rig.SetFirstPerson(false);
            rig.TurnAim(-rig.AimYaw);
            rig.TiltAim(-rig.AimPitch + 8f);
            yield return new WaitForSeconds(0.6f);
            var f = Vector3.forward;
            var a = NetDevRunnerAsksItem("Gold Watch", player.transform.position + f * 4.5f + Vector3.right * -1.4f + Vector3.up * 0.4f, 700);
            var b = RangedWeapon.Create(RangedWeapon.Kind.Smg, player.transform.position + f * 6.5f + Vector3.right * 1.6f + Vector3.up * 0.4f, null, 0f, 1).GetComponent<Pickup>();
            var c = NetDevRunnerAsksItem("Spare Clip", player.transform.position + f * 3.2f + Vector3.right * 0.9f + Vector3.up * 0.4f, 0, true);
            var behind = NetDevRunnerAsksItem("Jewelry Box", player.transform.position - f * 5f + Vector3.up * 0.4f, 1200);
            yield return new WaitForSeconds(0.4f);
            PingSystem.OnPinged(1, (byte)PingTargetKind.Item, a.NetId, a.transform.position);
            PingSystem.OnPinged(2, (byte)PingTargetKind.Item, b.NetId, b.transform.position);
            PingSystem.OnPinged(3, (byte)PingTargetKind.Item, c.NetId, c.transform.position);
            PingSystem.OnPinged(4, (byte)PingTargetKind.Item, behind.NetId, behind.transform.position);
            PingUiCheck("four markers are up (three teammates' and one behind the camera)", PingSystem.Count == 4, $"({PingSystem.Count})");
            yield return new WaitForSeconds(0.18f);
            yield return Snap("ping_popping");
            yield return new WaitForSeconds(1.2f);
            yield return Snap("ping_steady");
            yield return new WaitForSeconds(6.0f);
            yield return Snap("ping_fading");
            yield return new WaitForSeconds(1.5f);
            PingUiCheck("the markers are gone after their 8 seconds", PingSystem.Count == 0, $"({PingSystem.Count})");
            Log($"[PingUi] DONE fails={pingUiFails}");
            Quit(pingUiFails == 0 ? 0 : 1);
        }

        static Pickup NetDevRunnerAsksItem(string label, Vector3 pos, int value, bool clip = false) => AsksItem(label, pos, value, clip);
    }
}
