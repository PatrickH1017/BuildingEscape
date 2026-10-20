using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pictures of the demo's screens (real screen captures, so a real non-batch Editor on the hidden desktop, like <c>Ui</c> and <c>Menus</c>):
    /// the shop before the demo is over (no pew), floor 4 with its quota met (the banner says it is the last floor of the demo), the docks
    /// afterwards (DEMO COMPLETE on the card and the banner) and the shop with the free pew. Written to %TEMP%\be_ui_demo_*.png.
    /// Run: <c>Unity.exe -projectPath &lt;copy&gt; -executeMethod BuildingEscape.EditorTools.NetDevHarness.DemoUi</c> (no -batchmode).
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator OpenShop(Vendor vendor, WobblyPlayer player)
        {
            var zone = vendor.GetComponent<Collider>();
            player.TeleportTo(zone.bounds.center + Vector3.up * 0.2f, player.transform.rotation);
            yield return new WaitForSeconds(0.8f);
            for (int i = 0; i < 4; i++)
            {
                typeof(Vendor).GetProperty("ShopOpen", BindingFlags.Public | BindingFlags.Static).GetSetMethod(true).Invoke(null, new object[] { true });
                yield return null;
            }
            yield return new WaitForSeconds(0.2f);
        }

        IEnumerator DemoUiScenario()
        {
            ResetGameViewSize();
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;

            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            hm.AddCash(800);
            var player = WobblyPlayerRegistry.All[0];
            Log($"[Ui] screen {Screen.width}x{Screen.height}");

            yield return OpenShop(Vendor.Current, player);
            yield return Snap("demo_shop_before");
            typeof(Vendor).GetProperty("ShopOpen", BindingFlags.Public | BindingFlags.Static).GetSetMethod(true).Invoke(null, new object[] { false });

            // ---- floor 4, quota met
            tm.GoToLevel(4);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 4);
            yield return WaitFor(() => !tm.Loading && hm.Floor == 4 && hm.Phase == HeistPhase.Heist, 90, "floor 4");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(1.0f);
            yield return Snap("demo_floor4");
            hm.DebugCompleteFloor();
            yield return new WaitForSeconds(1.2f);
            yield return Snap("demo_floor4_complete");

            // ---- back at the docks: the demo is over
            tm.RideTo(0);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 0);
            yield return WaitFor(() => !tm.Loading && hm.Floor == 0 && hm.Phase == HeistPhase.Hub, 90, "the docks");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(1.2f);
            yield return Snap("demo_docks_over");
            yield return OpenShop(Vendor.Current, WobblyPlayerRegistry.All[0]);
            yield return Snap("demo_shop_after");

            Log($"[Ui] DONE fails={uiFails}");
            Quit(uiFails == 0 ? 0 : 1);
        }
    }
}
