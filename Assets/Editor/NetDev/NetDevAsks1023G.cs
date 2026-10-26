using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of batch D, T5, of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63; offline, in the Loading Docks where the Vendor stands): the walkie talkie is bought at the Vendor, goes into the pack and is not held out by itself, is held out
    /// from the weapon bar / the pack's EQUIP button (the model shows in the right hand), is put away again, and is never counted as loot. The radio itself is the voice system's (<c>VoiceChat.RadioOf</c> answers from the pack; two machines are needed to hear it).
    /// Run: <c>qa_regress_bd.ps1 -Methods Asks1023G</c>; the log ends with <c>[Asks1023G] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int a23gFails;

        void A23G(string name, bool ok, string detail = "")
        {
            if (!ok) a23gFails++;
            Log($"[Asks1023G] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator Asks1023GScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(1.0f);
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var vendor = Vendor.Current;
            A23G("T5: the walkie talkie model is in the weapon catalog (SpawnCampGames)", WalkieTalkie.Available);
            A23G("T5: the voice chat asks the pack who holds a radio out (VoiceChat.RadioOf is set)", Net.VoiceChat.RadioOf != null);
            A23G("(test) the docks have a Vendor", vendor != null);
            if (vendor == null) { Quit(1); yield break; }
            foreach (var held in pack.Snapshot()) if (held && RangedWeapon.IsWeapon(held)) pack.Drop(held);            // (an empty hand to start from)
            hm.AddCash(2000);
            int cash0 = hm.Cash;
            var buyFor = typeof(Vendor).GetMethod("BuyFor", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            string said = (string)buyFor.Invoke(vendor, new object[] { hm, pack, Vendor.WalkieIndex });
            yield return new WaitForSeconds(0.4f);
            var item = pack.Items.FirstOrDefault(i => i && i.TryGetComponent(out WalkieTalkie _));
            A23G("T5: the Vendor sells it for its price and it goes into the pack", said.Contains("Bought") && cash0 - hm.Cash == WalkieRules.Price && item != null, $"(\"{said}\", cash {cash0} -> {hm.Cash})");
            if (item == null) { Quit(1); yield break; }
            A23G("T5: it weighs what the rules say and is no loot (it stays when everything else is dropped)", Mathf.Approximately(item.weight, WalkieRules.Weight) && !item.CountsAsLoot && item.Label == WalkieRules.Name, $"({item.weight} kg, label '{item.Label}')");
            A23G("T5: bought, it is not held out by itself: the hand is empty and the radio is off", pack.Equipped == null && !WalkieTalkie.HoldsOut(pack));

            pack.SetEquipped(item);
            yield return new WaitForSeconds(0.7f);
            var held2 = player.GetComponent<HeldWalkie>();
            var model = held2 ? held2.Model : null;
            A23G("T5: held out from the pack it is the item in the hand and the radio is on", pack.Equipped == item && WalkieTalkie.HoldsOut(pack));
            bool inHand = model != null && model.transform.parent != null && model.transform.parent.name.ToLowerInvariant().Contains("hand");
            float away = model != null && model.transform.parent != null ? Vector3.Distance(model.transform.position, model.transform.parent.position) : -1f;
            A23G("T5: the model is in the right hand, a hand's reach from the wrist", inHand && away >= 0f && away < 0.45f, $"(model {(model ? model.name : "none")} under '{(model && model.transform.parent ? model.transform.parent.name : "-")}', {away:0.00} m from the wrist)");
            A23G("T5: while it is held out nothing else is (no punch, no weapon): the hand is full", pack.Equipped == item);

            pack.SetEquipped(null);
            yield return new WaitForSeconds(0.7f);
            A23G("T5: put away, the radio is off and the model is gone from the hand", !WalkieTalkie.HoldsOut(pack) && (held2 == null || held2.Model == null));

            // dropped everything that is loot: the radio stays
            pack.DropAll();
            yield return null;
            A23G("T5: 'drop all loot' leaves the walkie talkie in the pack", pack.Contains(item));

            // one lying about: a real item with a collider and a body, which a player can pick up
            var lying = WalkieTalkie.Create(player.transform.position + player.transform.forward * 1.5f + Vector3.up * 0.5f, null, 30f);
            yield return new WaitForSeconds(0.6f);
            A23G("T5: a walkie talkie lying about is an item (a body, a collider, a pickup with its weight) and rests on the floor", lying != null && lying.GetComponent<Rigidbody>() && lying.GetComponent<Collider>() && lying.GetComponent<Pickup>() && lying.transform.position.y < player.transform.position.y + 0.2f, $"(at height {(lying ? lying.transform.position.y : 0f):0.00})");
            if (lying) Destroy(lying);

            Log($"[Asks1023G] DONE fails={a23gFails}");
            Quit(a23gFails == 0 ? 0 : 1);
        }
    }
}
