using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pictures of the in-run HUD in the states a player sees: the docks, a floor in progress (with a "+$" floater), the pew's ammunition panel,
    /// a pick-up prompt, quota met, a backup call, a pinned player. They are real screen captures of the Game view, so this scenario needs a
    /// real (not batch-mode) Editor window: IMGUI is not drawn in batch mode. Written to %TEMP%\be_ui_*.png. The look itself is tested more
    /// cheaply in a throw-away project (see DEVELOPER_NOTES 8.2); this one proves the real numbers reach the widgets.
    /// Run: <c>Unity.exe -projectPath &lt;copy&gt; -executeMethod BuildingEscape.EditorTools.NetDevHarness.Ui</c> (no -batchmode).
    /// </summary>
    public partial class NetDevRunner
    {
        int uiFails;

        void UiCheck(string name, bool ok, string detail = "")
        {
            if (!ok) uiFails++;
            Log($"[Ui] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>The whole Game view as the player sees it, HUD included.</summary>
        IEnumerator Snap(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_ui_" + name + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Log($"[Ui] saved {path} ({tex.width}x{tex.height}, screen {Screen.width}x{Screen.height})");
            Object.Destroy(tex);
        }

        static void SetProp(object o, string prop, object value)
        {
            var p = o.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var set = p != null ? p.GetSetMethod(true) : null;
            if (set != null) set.Invoke(o, new[] { value });
            else Log($"[Ui] cannot set {prop}");
        }

        IEnumerator UiScenario()
        {
            // a big Game view: the window of a non-batch Editor is the picture's size
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;

            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var gen = FindFirstObjectByType<FloorGenerator>();
            Log($"[Ui] screen {Screen.width}x{Screen.height}");

            // ---- the docks: some cash, a few things in the pack, a crowbar in hand
            hm.AddCash(1250);
            var menu = new GameObject("TestModeForUi").AddComponent<TestMode>();
            typeof(TestMode).GetMethod("GiveWeapon", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, new object[] { pack, MeleeWeapon.Kind.Crowbar });
            for (int i = 0; i < 3; i++) gen.SpawnLoot(i, player.transform.position + Vector3.forward * 2f + Vector3.up * 0.8f, 1);
            yield return new WaitForSeconds(0.6f);
            foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (p && p.Loot && p.isActiveAndEnabled && pack.CanFit(p, out _)) pack.TryStore(p);
            yield return new WaitForSeconds(0.25f);
            yield return Snap("hub_pop");              // just after the cash came in: the number is still counting, a "+$" is rising
            yield return new WaitForSeconds(1.6f);
            yield return Snap("hub");
            UiCheck("the pack holds the loot", pack.LootCount >= 1, $"loot={pack.LootCount}");
            // no loot at all from here on: the pack must have room for the pew, and loot lying in the elevator would be banked on the next floor
            foreach (var l in pack.TakeLoot()) if (l) Object.Destroy(l.gameObject);
            foreach (var l in FindObjectsByType<HeavyLoot>(FindObjectsSortMode.None)) if (l) Object.Destroy(l.gameObject);
            yield return new WaitForSeconds(0.3f);

            // ---- a floor in progress
            TowerManager.Instance.GoToLevel(1);
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == 1);
            yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == 1 && hm.Phase == HeistPhase.Heist, 90, "floor 1");
            hm.godMode = true; hm.freezeTimer = true;
            foreach (var g in GuardAI.All) if (g) g.sightRange = 0.1f;
            SetProp(hm, "Banked", Mathf.Max(1, hm.Quota * 6 / 10));      // part of the quota: the bar is part full
            SetProp(hm, "Lost", 320);
            yield return WaitFor(() => GuardAI.All.Count > 0, 30, "a guard on the floor");
            yield return new WaitForSeconds(1.5f);
            yield return Snap("floor");

            // ---- the pew: its ammunition panel
            typeof(TestMode).GetMethod("GivePew", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, new object[] { pack });
            yield return new WaitForSeconds(1.0f);
            yield return Snap("pew_full");
            var gc = player.GetComponent<GunController>();
            for (int i = 0; i < 4; i++) { gc.Fire(); yield return new WaitForSeconds(0.33f); }
            yield return Snap("pew_part");
            for (int i = 0; i < 6; i++) { gc.Fire(); yield return new WaitForSeconds(0.33f); }
            yield return new WaitForSeconds(0.6f);
            yield return Snap("pew_reloading");
            yield return new WaitForSeconds(2.2f);
            pack.SetEquipped(null);

            // ---- a loose item right under the crosshair: the pick-up prompt
            var cam = Camera.main;
            var mug = FreeMug();
            if (mug && cam)
            {
                var rbm = mug.GetComponent<Rigidbody>();
                if (rbm) { rbm.linearVelocity = Vector3.zero; rbm.isKinematic = true; }
                var o = cam.transform.position; var d = cam.transform.forward;
                float depth = Vector3.Dot(player.transform.position - o, d) + 1.6f;
                mug.transform.position = o + d * depth;
                yield return new WaitForSeconds(0.8f);
                yield return Snap("prompt");
            }

            // ---- the police: a backup call, then a pinned player (pinning only works while the floor is still being played)
            hm.CallBackup(null);
            yield return new WaitForSeconds(1.2f);
            yield return Snap("backup");
            hm.godMode = false;
            typeof(TestMode).GetMethod("PinMe", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, new object[] { hm, player });
            yield return new WaitForSeconds(1.0f);
            yield return Snap("pinned");
            UiCheck("the player is pinned while the pinned panel shows", player.Custody == CustodyState.Pinned, $"custody={player.Custody}");
            hm.godMode = true;
            hm.FreePinned(player);

            // ---- quota met
            hm.DebugCompleteFloor();
            yield return new WaitForSeconds(1.0f);
            yield return Snap("complete");

            Log($"[Ui] DONE fails={uiFails}");
            Quit(uiFails == 0 ? 0 : 1);
        }
    }
}
