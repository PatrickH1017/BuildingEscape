using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of batch C part 2 of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63): the vending machines of the Loading Docks that sell the consumables (T6) and are the black machine, both in the main room (T23). Offline, in the docks.
    /// Run: <c>qa_regress_bd.ps1 -Methods Asks1023V</c>; the log ends with <c>[Asks1023V] DONE fails=N</c>. <c>Asks1023VUi</c> (<c>ui.ps1</c>, a real window) takes the pictures of the machine's green screen.
    /// </summary>
    public partial class NetDevRunner
    {
        int a23vFails;

        void A23V(string name, bool ok, string detail = "")
        {
            if (!ok) a23vFails++;
            Log($"[Asks1023V] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>A spot in front of a vending machine of the docks, close enough to work it (the multiplayer scenarios stand there to buy the consumables).</summary>
        static Vector3 MachineSpot(int number)
        {
            var m = VendingMachine.ByNumber(number);
            var t = m ? m.TrayPoint : Vector3.zero;
            return new Vector3(t.x, 1.1f, t.z);
        }

        IEnumerator Asks1023VScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(1.0f);
            var gen = FindFirstObjectByType<FloorGenerator>();
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var pack = player.GetComponent<Backpack>();
            var vendor = Vendor.Current;
            A23V("(test) the docks have the dealer and the player", vendor != null && player != null && pack != null);
            if (vendor == null || player == null || pack == null) { Quit(1); yield break; }

            // ---- T23 + T6: three machines, the two of the main room alike, each one something E works
            var machines = VendingMachine.Machines;
            A23V("T23: the docks have three vending machines (two in the main room, one in the break room), each an interactable thing with a box to aim at", machines.Count == 3 && machines.All(m => m && Interactable.All.Contains(m) && m.AimBox.size.magnitude > 1f), $"({machines.Count} machines)");
            var spots = gen.DockVendingSpots;
            A23V("T23: the two machines of the main room are the same black machine (no red one)", spots.Length == 3 && Mathf.Abs(spots[0].width - spots[1].width) < 1e-3f && Mathf.Abs(spots[0].depth - spots[1].depth) < 1e-3f,
                spots.Length == 3 ? $"({spots[0].width:0.00} x {spots[0].depth:0.00} and {spots[1].width:0.00} x {spots[1].depth:0.00})" : "");
            A23V("T23: ... and the red prop is not in the docks", !FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t && t.name.StartsWith("vending_red")));

            var m0 = VendingMachine.ByNumber(0);
            // the crosshair on the machine, a couple of metres in front of it: E would work it
            spots[0].Front(out int fx, out int fz);
            var front = new Vector3(fx, 0f, fz);
            var centre = m0.AimBox.center;
            player.TeleportTo(new Vector3(centre.x, 1.1f, centre.z) + front * (1.6f + (fz != 0 ? m0.AimBox.extents.z : m0.AimBox.extents.x)), Quaternion.LookRotation(-front));
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.6f);
            var eye = new Vector3(player.transform.position.x, 1.6f, player.transform.position.z);
            var aim = Crosshair.Resolve(eye, (centre - eye).normalized, player);
            A23V("T6: with the crosshair on a machine and close enough, E would work it (the prompt is the machine's)", aim.Kind == AimKind.Interact && aim.Interactable == m0 && m0.Prompt.StartsWith("[E]"), $"(kind {aim.Kind}, distance {aim.Distance:0.0})");

            // ---- T6: using it brings its screen up, which counts as a screen over the game; walking away shuts it
            m0.Use(player);
            yield return null;
            A23V("T6: using the machine opens its screen, a screen over the game (keys blocked, cursor free)", VendingScreen.Open && TestMode.PanelOpen && Crosshair.MenuOpen);
            int cash0, items0;

            // ---- T6: a purchase takes the cash and puts the item in the pack, with the dealer's words
            hm.TrySpend(hm.Cash); hm.AddCash(1000);
            cash0 = hm.Cash; items0 = pack.Items.Count;
            VendingScreen.TestBuy(0);
            yield return new WaitForSeconds(0.3f);
            var mug = pack.Items.FirstOrDefault(i => i && i.Label == "Coffee Mug");
            A23V("T6: the coffee mug costs $60 and goes into the pack (the screen says so)", hm.Cash == cash0 - 60 && pack.Items.Count == items0 + 1 && mug != null && VendingScreen.Status.StartsWith("Bought Coffee Mug") && VendingScreen.Status.Contains("pack"), $"(cash {cash0} -> {hm.Cash}, '{VendingScreen.Status}')");
            cash0 = hm.Cash; items0 = pack.Items.Count;
            VendingScreen.TestBuy(3);
            yield return new WaitForSeconds(0.3f);
            A23V("T6: the fire extinguisher costs $600 and goes into the pack too", hm.Cash == cash0 - 600 && pack.Items.Count == items0 + 1 && pack.Items.Any(i => i && i.Label == "Fire Extinguisher"), $"(cash {cash0} -> {hm.Cash})");

            // not enough cash: nothing happens but the answer
            hm.TrySpend(hm.Cash); hm.AddCash(100);
            cash0 = hm.Cash; items0 = pack.Items.Count;
            VendingScreen.TestBuy(3);
            A23V("T6: with $100 the $600 fire extinguisher is refused, no cash taken and nothing bought", hm.Cash == cash0 && pack.Items.Count == items0 && VendingScreen.Status.StartsWith("Not enough cash"), $"('{VendingScreen.Status}')");

            // no room in the pack: the cash stays too
            hm.TrySpend(hm.Cash); hm.AddCash(1000);
            var ballast = AsksItem("Ballast", player.transform.position + Vector3.up * 0.5f); ballast.weight = Mathf.Max(0.1f, pack.capacity - pack.Weight - 0.5f);
            pack.TryStore(ballast);
            cash0 = hm.Cash; items0 = pack.Items.Count;
            VendingScreen.TestBuy(3);                                                    // the fire extinguisher: 2 kg
            A23V("T6: with a pack that has no room the fire extinguisher is refused, no cash taken and nothing bought", hm.Cash == cash0 && pack.Items.Count == items0 && VendingScreen.Status.StartsWith("No room"), $"('{VendingScreen.Status}')");
            pack.Drop(ballast); yield return null; if (ballast) Destroy(ballast.gameObject);
            foreach (var it in pack.Snapshot()) if (it && (it.Label == "Coffee Mug" || it.Label == "Fire Extinguisher")) { pack.Drop(it); if (it) Destroy(it.gameObject); }

            // the screen shuts when the player walks away from the machine
            player.TeleportTo(new Vector3(centre.x, 1.1f, centre.z) + front * 12f, Quaternion.identity);
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.3f);
            A23V("T6: walking away shuts the machine's screen", !VendingScreen.Open && !TestMode.PanelOpen);

            // ---- the dealer sells none of the four any more, even to somebody who asks for one
            player.TeleportTo(vendor.transform.position + Vector3.up * 1.1f, Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
            hm.TrySpend(hm.Cash); hm.AddCash(1000);
            var buyFor = typeof(Vendor).GetMethod("BuyFor", BindingFlags.NonPublic | BindingFlags.Instance);
            cash0 = hm.Cash; items0 = pack.Items.Count;
            string said = buyFor != null ? (string)buyFor.Invoke(vendor, new object[] { hm, pack, 0 }) : "";
            A23V("T6: the dealer will not sell the coffee mug any more (it is the machines')", buyFor != null && said.Contains("nothing like that") && hm.Cash == cash0 && pack.Items.Count == items0, $"('{said}')");

            Log($"[Asks1023V] DONE fails={a23vFails}");
            Quit(a23vFails == 0 ? 0 : 1);
        }

        /// <summary>Pictures of the machine's screen (a real window: <c>ui.ps1 -Methods Asks1023VUi</c>): %TEMP%\be_ui_vending*.png.</summary>
        IEnumerator Asks1023VUiScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(1.0f);
            var player = WobblyPlayerRegistry.All[0];
            var m0 = VendingMachine.ByNumber(0);
            A23V("(test) a machine to look at", m0 != null);
            if (m0 == null) { Quit(1); yield break; }
            hm.TrySpend(hm.Cash); hm.AddCash(260);
            player.TeleportTo(MachineSpot(0), Quaternion.identity);
            yield return new WaitForSeconds(0.8f);
            m0.Use(player);
            yield return new WaitForSeconds(0.5f);
            yield return Snap("vending");
            VendingScreen.TestBuy(1);
            yield return new WaitForSeconds(0.4f);
            yield return Snap("vending_bought");
            VendingScreen.Close();
            Log($"[Asks1023V] DONE fails={a23vFails}");
            Quit(a23vFails == 0 ? 0 : 1);
        }
    }
}
