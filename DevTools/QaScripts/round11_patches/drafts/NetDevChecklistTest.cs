namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The pure rules of the journal's checklist and of the scan's colours (the owner, 2026-10-19): the four lines of the checklist, when a line is done and that it stays done, and the glow of each kind.
        /// </summary>
        static void ChecklistTest()
        {
            Check("checklist: the four lines are 'All valuables collected', 'All unique items collected', 'All safes opened' and 'All cash collected', in that order",
                ChecklistRules.Count == 4 && ChecklistRules.Label(ScanKind.Valuable) == "All valuables collected" && ChecklistRules.Label(ScanKind.Unique) == "All unique items collected"
                && ChecklistRules.Label(ScanKind.Safe) == "All safes opened" && ChecklistRules.Label(ScanKind.Cash) == "All cash collected"
                && (int)ScanKind.Valuable == 0 && (int)ScanKind.Unique == 1 && (int)ScanKind.Safe == 2 && (int)ScanKind.Cash == 3);

            ChecklistRules.Rgb(ScanKind.Safe, out float sr, out float sg, out float sb);
            ChecklistRules.Rgb(ScanKind.Unique, out float ur, out float ug, out float ub);
            ChecklistRules.Rgb(ScanKind.Cash, out float cr, out float cg, out float cb);
            ChecklistRules.Rgb(ScanKind.Valuable, out float vr, out float vg, out float vb);
            Check("scan: safes glow red, unique items purple, cash green and valuables gold",
                sr > 0.8f && sg < 0.4f && sb < 0.4f && ur > 0.5f && ub > 0.9f && ug < 0.5f && cg > 0.8f && cr < 0.4f && cb < 0.6f && vr > 0.9f && vg > 0.7f && vb < 0.3f,
                $"(safe {sr:0.00}/{sg:0.00}/{sb:0.00}, unique {ur:0.00}/{ug:0.00}/{ub:0.00}, cash {cr:0.00}/{cg:0.00}/{cb:0.00}, valuable {vr:0.00}/{vg:0.00}/{vb:0.00})");

            // ---- when a line is done
            var st = new ChecklistState();
            const ScanKind val = ScanKind.Valuable, uni = ScanKind.Unique, safe = ScanKind.Safe, cash = ScanKind.Cash;
            st.Update(new[] { 5, 2, 1, 3 }, 10f);                                                     // (the first count of a floor: it has only just been built)
            bool early = !st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash) && st.First(val) == -1;
            st.Update(new[] { 5, 2, 1, 3 }, 10f + ChecklistRules.SettleSeconds + 0.1f);               // (after the settle time: the count begins)
            bool began = st.First(val) == 5 && st.First(uni) == 2 && st.First(safe) == 1 && st.First(cash) == 3 && !st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash);
            st.Update(new[] { 0, 2, 1, 3 }, 14f);                                                      // (every valuable is taken)
            bool oneDone = st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash);
            st.Update(new[] { 0, 0, 0, 3 }, 15f);                                                      // (the unique items and the safes too)
            bool threeDone = st.Done(val) && st.Done(uni) && st.Done(safe) && !st.Done(cash);
            st.Update(new[] { 4, 0, 0, 0 }, 16f);                                                      // (the cash is taken, and somebody puts valuables down again: the tick stays)
            bool latched = st.Done(val) && st.Done(uni) && st.Done(safe) && st.Done(cash);
            st.Reset();
            bool reset = !st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash) && st.First(val) == -1;
            st.Update(new[] { 3, 0, 2, 0 }, 20f); st.Update(new[] { 3, 0, 2, 0 }, 22f);                // (a floor that never had a unique item or any cash: those lines are done at once)
            bool vacuous = st.Done(uni) && st.Done(cash) && !st.Done(val) && !st.Done(safe);
            Check("checklist: counting waits for the floor to settle, a line is done when none of its kind is left, the tick stays, a new floor starts again, and a kind the floor never had is done at once",
                early && began && oneDone && threeDone && latched && reset && vacuous, $"(early {early}, began {began}, one {oneDone}, three {threeDone}, stays {latched}, reset {reset}, nothing there {vacuous})");
        }
    }
}
