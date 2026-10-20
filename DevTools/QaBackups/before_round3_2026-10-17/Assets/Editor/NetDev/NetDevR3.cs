using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Round 3 of 2026-10-17 evening (DEVELOPER_NOTES 2.23): the scenarios that look at what was built. <c>InvUi</c> takes pictures of the restyled inventory
    /// (the Tab window) in a few states and at a few window sizes; IMGUI is not drawn in batch mode, so run it in a real Editor on the hidden desktop
    /// (<c>ui_bq.ps1 -Method InvUi</c>). Pictures: %TEMP%\be_ui_inv_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int r3Fails;

        void R3Check(string name, bool ok, string detail = "")
        {
            if (!ok) r3Fails++;
            Log($"[R3] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator InvUiScenario()
        {
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            ResetGameViewSize();
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;

            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var gen = FindFirstObjectByType<FloorGenerator>();
            Log($"[R3] screen {Screen.width}x{Screen.height}");

            // ---- an empty pack and nothing in reach (the empty lists)
            foreach (var l in FindObjectsByType<HeavyLoot>(FindObjectsSortMode.None)) if (l) Destroy(l.gameObject);
            yield return new WaitForSeconds(0.3f);
            SetStaticProp(typeof(Backpack), "UiOpen", true);
            yield return new WaitForSeconds(0.9f);
            R3Check("the inventory is open", Backpack.UiOpen);
            yield return Snap("inv_empty");
            SetStaticProp(typeof(Backpack), "UiOpen", false);

            // ---- a crowbar, the pew, three pieces of loot in the pack and two on the ground
            var menu = new GameObject("TestModeForInv").AddComponent<TestMode>();
            typeof(TestMode).GetMethod("GiveWeapon", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, new object[] { pack, MeleeWeapon.Kind.Crowbar });
            typeof(TestMode).GetMethod("GivePew", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, new object[] { pack });
            pack.capacity = 40f;
            for (int i = 0; i < 3; i++) gen.SpawnLoot(i, player.transform.position + Vector3.forward * 2f + Vector3.up * 0.8f, 1);
            yield return new WaitForSeconds(0.6f);
            foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (p && p.Loot && p.isActiveAndEnabled && !pack.Contains(p) && pack.CanFit(p, out _)) pack.TryStore(p);
            yield return new WaitForSeconds(0.25f);
            // something that is neither a weapon nor loot ("OTHER"): one in the pack, one on the ground
            Pickup Mug(Vector3 at)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Coffee Mug"; go.transform.localScale = new Vector3(0.18f, 0.12f, 0.18f); go.transform.position = at;
                go.AddComponent<Rigidbody>().mass = 0.4f;
                var pk = go.AddComponent<Pickup>(); pk.label = "Coffee Mug"; pk.weight = 0.4f;
                return pk;
            }
            var packMug = Mug(player.transform.position + Vector3.up * 1.2f);
            yield return new WaitForSeconds(0.3f);
            pack.TryStore(packMug);
            Mug(player.transform.position + player.transform.forward * 1.3f + player.transform.right * -0.6f + Vector3.up * 0.6f);
            gen.SpawnLoot(1, player.transform.position + player.transform.forward * 1.4f + Vector3.up * 0.6f, 1);
            gen.SpawnLoot(4, player.transform.position + player.transform.forward * 1.6f + player.transform.right * 0.7f + Vector3.up * 0.6f, 1);
            yield return new WaitForSeconds(0.8f);
            SetStaticProp(typeof(Backpack), "UiOpen", true);
            yield return new WaitForSeconds(1.2f);
            R3Check("the pack holds the two weapons and some loot", pack.Items.Count >= 3, $"items={pack.Items.Count} loot={pack.LootCount}");
            yield return Snap("inv_full");

            // ---- nearly full: the weight bar turns orange / red
            float w = pack.Weight;
            pack.capacity = Mathf.Max(1f, w * 1.04f);
            yield return new WaitForSeconds(0.4f);
            yield return Snap("inv_heavy");
            pack.capacity = 40f;

            // ---- other window sizes
            foreach (var (sw, sh) in new[] { (1280, 720), (960, 540), (3840, 2160) })
            {
                if (!SetGameViewSize(sw, sh)) { Log("[R3] SKIP other window sizes: the Game view cannot be resized from here"); break; }
                yield return new WaitForSeconds(0.8f);
                Log($"[R3] screen now {Screen.width}x{Screen.height}");
                yield return Snap($"inv_{sw}");
            }
            ResetGameViewSize();
            yield return new WaitForSeconds(0.5f);

            SetStaticProp(typeof(Backpack), "UiOpen", false);
            Log($"[R3] DONE fails={r3Fails}");
            Quit(r3Fails == 0 ? 0 : 1);
        }
    }
}
