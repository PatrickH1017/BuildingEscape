using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The weapon bar's layout in the real pack (the owner, 2026-10-22: "give players the ability to drag equipment such as weapons into different shortcut menus from their backpack or from a different shortcut ... if they drag to another item it
    /// should swap spots with the item"): seven weapons come into a pack in the basement; the first six take the slots 1 to 6, the seventh none; what dropping a weapon on a slot and on another weapon does (the pack's MoveToBarSlot and SwapBarSpots, which
    /// the Tab window calls when the mouse lets go), that the keys 1 to 6 take the weapon of the slot in hand after the moves, and that a weapon dropped on the floor frees its slot for the one that was off the bar. Run: <c>qa_regress.ps1 -Methods BarDrag</c>;
    /// it ends with <c>[BarDrag] DONE fails=N</c>. NOT covered (a scenario cannot move a mouse over the Tab window): the dragging itself, the lit slots, the weapon in the air, the order of the list and the "SLOT" chips - look at those by hand.
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator BarDragScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(0.5f);
            int fails = 0;
            void Chk(string name, bool ok, string detail = "") { if (!ok) fails++; Log($"[BarDrag] {(ok ? "PASS" : "FAIL")} {name}" + (detail.Length > 0 && !ok ? " " + detail : "")); }
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            if (pack == null || !MeleeWeapon.Available || !RangedWeapon.Available || !RangedWeapon.Has(RangedWeapon.Kind.Pew))
            {
                Chk("the pack and the weapon packs are there (Employees Take Revenge > Setup Weapons)", false);
                Log($"[BarDrag] DONE fails={fails}");
                Quit(1);
                yield break;
            }
            float oldCapacity = pack.capacity;
            pack.capacity = 500f;
            foreach (var held in pack.Snapshot()) if (held && RangedWeapon.IsWeapon(held)) pack.Drop(held);          // (a fresh basement holds none: start from an empty hand)
            yield return null;

            var at = player.transform.position + Vector3.up * 0.5f;
            var w = new List<Pickup>();
            for (int i = 0; i < 7; i++)
            {
                var go = i % 2 == 0 ? MeleeWeapon.Create(MeleeWeapon.Kind.Crowbar, at, null) : RangedWeapon.Create(RangedWeapon.Kind.Pew, at, null, 0f, 2);
                var p = go.GetComponent<Pickup>();
                pack.TryStore(p);
                w.Add(p);
            }
            yield return new WaitForSeconds(0.4f);
            Chk("seven weapons are in the pack", w.All(p => p && pack.Contains(p)));
            Chk("the first six are on the slots 1 to 6 in the order they came, the seventh is on none", Enumerable.Range(0, 6).All(i => pack.BarWeapon(i) == w[i] && pack.BarSlotOf(w[i]) == i) && pack.BarSlotOf(w[6]) == -1);

            // a weapon dropped on a slot that holds another: they swap slots
            bool a = pack.MoveToBarSlot(w[0], 3);
            Chk("a weapon of the bar dropped on another slot swaps with the weapon there (slot 1 onto slot 4)", a && pack.BarWeapon(3) == w[0] && pack.BarWeapon(0) == w[3] && pack.BarSlotOf(w[0]) == 3 && pack.BarSlotOf(w[3]) == 0);
            // a weapon of the pack's list (off the bar) dropped on a slot: it takes it, the one there leaves the bar but stays in the pack
            bool b = pack.MoveToBarSlot(w[6], 1);
            Chk("the weapon that was off the bar dropped on slot 2 takes it; the weapon that was there is off the bar, still in the pack", b && pack.BarWeapon(1) == w[6] && pack.BarSlotOf(w[1]) == -1 && pack.Contains(w[1]));
            // a weapon of the bar dropped on a weapon off the bar: it gives it its slot
            bool c = pack.SwapBarSpots(w[5], w[1]);
            Chk("a weapon of the bar dropped on a weapon that is off the bar gives it its slot (slot 6) and leaves the bar", c && pack.BarWeapon(5) == w[1] && pack.BarSlotOf(w[5]) == -1);
            // two weapons of the bar dropped on each other
            bool d = pack.SwapBarSpots(w[2], w[4]);
            Chk("two weapons of the bar dropped on each other swap slots (3 and 5)", d && pack.BarWeapon(2) == w[4] && pack.BarWeapon(4) == w[2]);
            Chk("nothing dropped, a weapon on its own slot and on itself change nothing", !pack.MoveToBarSlot(null, 2) && !pack.MoveToBarSlot(w[4], 2) && !pack.SwapBarSpots(w[4], w[4]) && !pack.MoveToBarSlot(w[4], 6) && pack.BarWeapon(2) == w[4]);

            // the keys 1 to 6 take what the layout says
            bool keyed = true;
            for (int slot = 0; slot < 6; slot++)
            {
                var want = pack.BarWeapon(slot);
                keyed &= want != null && pack.SelectSlot(slot);
                yield return null;
                keyed &= pack.Equipped == want;
            }
            Chk("after the moves each key 1 to 6 takes the weapon that is on its slot in hand", keyed);

            // a weapon dropped on the floor frees its slot for the one that was off the bar (the owner's earlier rule: the bar is full while there are weapons for it)
            var off = w.First(p => pack.BarSlotOf(p) == -1);
            var before = Enumerable.Range(0, 6).Select(i => pack.BarWeapon(i)).ToArray();
            var gone = pack.BarWeapon(4);
            pack.Drop(gone);
            yield return new WaitForSeconds(0.3f);
            bool others = Enumerable.Range(0, 6).Where(i => i != 4).All(i => pack.BarWeapon(i) == before[i]);
            Chk("a weapon dropped on the floor leaves its slot to the weapon that was off the bar; the other slots stay as they were", others && pack.BarWeapon(4) == off && pack.BarSlotOf(off) == 4 && !pack.Contains(gone));
            Chk("the Tab window is closed and nothing is being dragged", !Backpack.UiOpen && !pack.Dragging);

            foreach (var p in w) if (p && pack.Contains(p)) pack.Drop(p);
            foreach (var p in w) if (p) Destroy(p.gameObject);
            pack.capacity = oldCapacity;
            Log($"[BarDrag] DONE fails={fails}");
            Quit(fails == 0 ? 0 : 1);
        }
    }
}
