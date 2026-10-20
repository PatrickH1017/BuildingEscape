using System.Collections;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode check of the journal's checklist and of the scan's glow (the owner, 2026-10-19): on floor 1 the checklist counts valuables, unique items, a safe and its cash; taking all of one kind ticks that line and no other; opening the safe
    /// ticks the safes while the cash lying in it is still to take; the scan marks a shut safe red-kind, the weapons on the outpost's desk unique-kind, the cash once the safe is open cash-kind and a valuable valuable-kind. Pictures of the
    /// journal's checklist tab, none ticked and all ticked: %TEMP%\be_shot_checklist_*.png. Needs graphics: <c>qa_regress.ps1 -Methods Checklist:gfx</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int checklistFails;

        void ChecklistCheck(string name, bool ok, string detail = "")
        {
            if (!ok) checklistFails++;
            Log($"[Checklist] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator JournalShot(string name)
        {
            OutpostUi.Show(OutpostUi.Kind.Journal);
            if (IntelScreens.Instance != null) IntelScreens.Instance.JournalTab = 1;
            yield return null;
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && !Application.isBatchMode)          // (the screen can only be captured in a window: ui.ps1 -Methods Checklist)
            {
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_shot_checklist_" + name + ".png"), tex.EncodeToPNG());
                Destroy(tex);
            }
            OutpostUi.Hide(OutpostUi.Kind.Journal);
            yield return null;
        }

        IEnumerator ChecklistScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var gen = FindFirstObjectByType<FloorGenerator>();
            var player = WobblyPlayerRegistry.All[0];
            yield return new WaitForSeconds(1.5f);
            ChecklistCheck("the checklist is part of every run and tracks nothing at the Loading Docks", FloorChecklistTracker.Instance != null && !FloorChecklistTracker.IsDone(ScanKind.Valuable) && !FloorChecklistTracker.IsDone(ScanKind.Safe));

            tm.GoToLevel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return WaitFor(() => !tm.Loading && hm.Floor == 1, 90, "floor 1");
            yield return new WaitForSeconds(3.2f);                                    // (the settle time, and a count)
            gen = FindFirstObjectByType<FloorGenerator>();
            player = WobblyPlayerRegistry.All[0];
            int First(ScanKind k) => FloorChecklistTracker.FirstCount(k);
            bool Done(ScanKind k) => FloorChecklistTracker.IsDone(k);
            ChecklistCheck("floor 1: the count has begun and the floor has things of every kind (valuables, unique items - the melee weapons -, one safe and the cash in it)",
                First(ScanKind.Valuable) > 0 && First(ScanKind.Unique) > 0 && First(ScanKind.Safe) == 1 && First(ScanKind.Cash) > 0,
                $"(valuables {First(ScanKind.Valuable)}, unique {First(ScanKind.Unique)}, safes {First(ScanKind.Safe)}, cash {First(ScanKind.Cash)})");
            ChecklistCheck("floor 1: nothing is done yet", !Done(ScanKind.Valuable) && !Done(ScanKind.Unique) && !Done(ScanKind.Safe) && !Done(ScanKind.Cash));
            yield return JournalShot("none");

            // ---- the scan: unique items (the weapons on the outpost's desk) are purple-kind, a shut safe is red-kind
            var scanner = LootScanner.Instance;
            var safe = FloorSafe.OnShow.FirstOrDefault(s => s);
            var weapons = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => FloorChecklist.Classify(p) == (int)ScanKind.Unique && p.isActiveAndEnabled).ToList();
            if (weapons.Count > 0 && scanner != null)
            {
                var wr = gen.RoomSpotAt(weapons[0].transform.position);
                player.TeleportTo(new Vector3(wr.bounds.Cx, 1.1f, wr.bounds.Cz), Quaternion.identity);
                yield return new WaitForSeconds(0.8f);
                LootScanner.TestHold = true;
                yield return new WaitForSeconds(2.0f);
                var inRoom = weapons.Where(p => gen.RoomSpotAt(p.transform.position).index == wr.index).ToList();
                ChecklistCheck("the scan marks the weapons lying in the room, as unique items (purple)", inRoom.Count >= 1 && inRoom.All(p => scanner.TryGetKind(p, out var k) && k == ScanKind.Unique), $"({inRoom.Count} weapons)");
                LootScanner.TestHold = false;
                yield return new WaitForSeconds(0.4f);
            }
            if (safe != null && scanner != null)
            {
                var sr = gen.RoomSpotAt(safe.transform.position);
                player.TeleportTo(new Vector3(sr.bounds.Cx, 1.1f, sr.bounds.Cz), Quaternion.identity);
                yield return new WaitForSeconds(0.8f);
                LootScanner.TestHold = true;
                yield return new WaitForSeconds(2.0f);
                ChecklistCheck("the scan marks a shut safe, as a safe (red)", scanner.TryGetKind(safe, out var sk) && sk == ScanKind.Safe, scanner.TryGetKind(safe, out sk) ? $"({sk})" : "(not marked)");
                LootScanner.TestHold = false;
                yield return new WaitForSeconds(0.4f);
            }
            var valuable = FindObjectsByType<Pickup>(FindObjectsSortMode.None).FirstOrDefault(p => FloorChecklist.Classify(p) == (int)ScanKind.Valuable && p.isActiveAndEnabled && gen.RoomSpotAt(p.transform.position).Valid);
            if (valuable != null && scanner != null)
            {
                var vr = gen.RoomSpotAt(valuable.transform.position);
                player.TeleportTo(new Vector3(vr.bounds.Cx, 1.1f, vr.bounds.Cz), Quaternion.identity);
                yield return new WaitForSeconds(0.8f);
                LootScanner.TestHold = true;
                yield return new WaitForSeconds(2.0f);
                ChecklistCheck("the scan marks a valuable, as a valuable (gold)", scanner.TryGetKind(valuable, out var vk) && vk == ScanKind.Valuable);
                LootScanner.TestHold = false;
                yield return new WaitForSeconds(0.4f);
            }

            // ---- take every valuable: that line is done and no other
            foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => FloorChecklist.Classify(p) == (int)ScanKind.Valuable).ToList()) p.gameObject.SetActive(false);
            yield return new WaitForSeconds(1.2f);
            ChecklistCheck("all the valuables lying about taken: that line is done unless the shut safe still holds a watch, and no other line is", (safe == null || safe.WatchCount == 0) == Done(ScanKind.Valuable) && !Done(ScanKind.Unique) && !Done(ScanKind.Safe) && !Done(ScanKind.Cash));

            // ---- take every unique item
            foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => FloorChecklist.Classify(p) == (int)ScanKind.Unique).ToList()) p.gameObject.SetActive(false);
            yield return new WaitForSeconds(1.2f);
            ChecklistCheck("all unique items taken: that line is done, the safe and the cash are not", Done(ScanKind.Unique) && !Done(ScanKind.Safe) && !Done(ScanKind.Cash));

            // ---- open the safe: the safes line is done, the cash lying in it is still to take (and it glows green when scanned)
            if (safe != null)
            {
                safe.ApplyOpen(true);
                yield return new WaitForSeconds(1.2f);
                ChecklistCheck("the safe is open: 'all safes opened' is done, the cash that lies in it is not collected yet", Done(ScanKind.Safe) && !Done(ScanKind.Cash));
                var sr = gen.RoomSpotAt(safe.transform.position);
                player.TeleportTo(new Vector3(sr.bounds.Cx, 1.1f, sr.bounds.Cz), Quaternion.identity);
                yield return new WaitForSeconds(0.8f);
                LootScanner.TestHold = true;
                yield return new WaitForSeconds(2.0f);
                var bundles = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => FloorChecklist.Classify(p) == (int)ScanKind.Cash && p.isActiveAndEnabled).ToList();
                ChecklistCheck("the scan marks the cash lying in the open safe, as cash (green), and the safe no longer glows", bundles.Count >= 1 && bundles.All(p => scanner.TryGetKind(p, out var ck) && ck == ScanKind.Cash) && !scanner.TryGetKind(safe, out _), $"({bundles.Count} bundles)");
                LootScanner.TestHold = false;
                yield return new WaitForSeconds(0.4f);
                foreach (var p in bundles) p.gameObject.SetActive(false);
                foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => FloorChecklist.Classify(p) == (int)ScanKind.Valuable).ToList()) p.gameObject.SetActive(false);          // (the watch that was inside the safe, if there was one)
                yield return new WaitForSeconds(1.2f);
            }
            ChecklistCheck("all four lines are done", Done(ScanKind.Valuable) && Done(ScanKind.Unique) && Done(ScanKind.Safe) && Done(ScanKind.Cash));
            yield return JournalShot("all");

            // ---- a new floor starts a new list
            tm.GoToLevel(2);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 2);
            yield return WaitFor(() => !tm.Loading && hm.Floor == 2, 90, "floor 2");
            yield return new WaitForSeconds(3.2f);
            ChecklistCheck("floor 2: a new list - the safe is shut and its line is not done", !Done(ScanKind.Safe) && FloorChecklistTracker.FirstCount(ScanKind.Safe) == 1);

            Log($"[Checklist] DONE fails={checklistFails}");
            Quit(checklistFails == 0 ? 0 : 1);
        }
    }
}
