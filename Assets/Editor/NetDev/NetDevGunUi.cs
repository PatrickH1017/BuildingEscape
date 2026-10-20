using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pictures of the screens the new guns change (real screen captures, so a real non-batch Editor on the hidden desktop, like <c>DemoUi</c>): the dealer's window once every gun has been found and the pew is for sale (thirteen cards), and the
    /// player with each long gun in the hands, the ammunition panel at the bottom right (a magazine of 30 is the longest row of bullets). Written to %TEMP%\be_ui_gunui_*.png.
    /// Run: <c>ui.ps1 -Methods GunUi</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator GunUiScenario()
        {
            ResetGameViewSize();
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;

            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            hm.AddCash(20000);
            GunController.LimitedAmmoForTests = true;                                   // (the docks have unlimited ammunition: the panel would say so instead of the rounds)
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            tm.NetSetResume(4);                                                         // (floor 3 is done: the pew is for sale)
            foreach (RangedWeapon.Kind k in System.Enum.GetValues(typeof(RangedWeapon.Kind))) RunUnlocks.Unlock(k);
            Log($"[Ui] screen {Screen.width}x{Screen.height}, unlocked mask {RunUnlocks.Mask}");

            yield return OpenShop(Vendor.Current, player);
            yield return Snap("gunui_shop");
            typeof(Vendor).GetProperty("ShopOpen", BindingFlags.Public | BindingFlags.Static).GetSetMethod(true).Invoke(null, new object[] { false });
            yield return new WaitForSeconds(0.3f);

            var menu = new GameObject("TestModeForTest").AddComponent<TestMode>();
            var giveGun = typeof(TestMode).GetMethod("GiveGun", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Pickup previous = null;
            foreach (var kind in new[] { RangedWeapon.Kind.Smg, RangedWeapon.Kind.Rifle, RangedWeapon.Kind.Shotgun, RangedWeapon.Kind.Sniper })
            {
                if (previous) { pack.Drop(previous); Object.Destroy(previous.gameObject); yield return new WaitForSeconds(0.3f); }
                giveGun.Invoke(menu, new object[] { pack, kind });
                yield return new WaitForSeconds(1.2f);
                previous = null;
                foreach (var i in pack.Items) if (i && i.TryGetComponent(out RangedWeapon rw) && rw.kind == kind) previous = i;
                yield return Snap("gunui_" + kind);
            }
            Log($"[Ui] DONE fails={uiFails}");
            Quit(uiFails == 0 ? 0 : 1);
        }
    }
}
