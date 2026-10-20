using System.Collections;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode check (offline, Greybox scene, batch mode is fine) of the last round: the outpost door (shutting it locks nothing and the officers open it; the code at the security terminal, SECURITY then LOCK,
    /// is the hard lock the officers cannot open, an outside hack opens it again, E from inside and UNLOCK undo it), the security computer's door lock (shut at once, nobody opens it, an officer at it does not either, unlock undoes it), every
    /// numbered room of a live floor finding its doors, and where the melee weapons lie (only on the outpost's desk: two on floor 1, at most one on floor 2).
    /// Run: Unity -batchmode -executeMethod BuildingEscape.EditorTools.NetDevHarness.R5   (look for "[R5] DONE fails=0").
    /// </summary>
    public partial class NetDevRunner
    {
        int r5Fails;

        void R5Check(string name, bool ok, string detail = "")
        {
            if (!ok) r5Fails++;
            Log($"[R5] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator R5LoadFloor(int floor)
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            tm.GoToLevel(floor);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == floor);
            yield return WaitFor(() => !tm.Loading && hm.Floor == floor, 90, "floor " + floor);
            yield return new WaitForSeconds(1.5f);
            hm.godMode = true; hm.freezeTimer = true;
            foreach (var g in GuardAI.All) if (g) { g.sightRange = 0.1f; g.hearingMultiplier = 0f; }          // (blind and deaf: nobody comes to the door)
        }

        IEnumerator R5Scenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return R5LoadFloor(1);
            var player = WobblyPlayerRegistry.All[0];
            var oc = OutpostController.Active;
            var doors = DoorManager.Active;
            R5Check("floor 1 has an outpost with a door", oc != null && oc.Door != null && doors != null);
            if (oc == null || oc.Door == null || doors == null) { Quit(1); yield break; }
            var door = oc.Door;
            int di = oc.DoorIndex;
            var inside = oc.KeypadPosition - oc.Outward * 2.2f + Vector3.up * 0.2f;
            var outside = oc.KeypadPosition + oc.Outward * 1.3f + Vector3.up * 0.2f;
            void Stand(Vector3 at) => player.TeleportTo(at, Quaternion.LookRotation(at == inside ? oc.Outward : -oc.Outward));

            // ---- melee weapons: only on the outpost's desk
            var weapons = Object.FindObjectsByType<MeleeWeapon>(FindObjectsSortMode.None);
            bool onDesk = weapons.All(w => oc.IsInside(w.transform.position) && (w.transform.position - oc.KeypadPosition).magnitude < 14f);
            R5Check("floor 1 holds exactly two melee weapons", weapons.Length == 2, $"({weapons.Length})");
            R5Check("...and both lie inside the outpost, nowhere else on the floor", onDesk);

            // ---- the room directory of a live floor: every numbered room finds its doors
            var dir = FloorDirectory.Current(1);
            R5Check("the directory of floor 1 exists with numbered rooms", dir != null && dir.rooms.Count > 5, $"({(dir != null ? dir.rooms.Count : 0)} rooms)");
            if (dir != null)
            {
                int without = dir.rooms.Count(rm => rm.doors.Count == 0);
                var all = dir.rooms.SelectMany(rm => rm.doors).ToList();
                R5Check("every numbered room found at least one door", without == 0, $"({without} without)");
                R5Check("no door belongs to two rooms", all.Distinct().Count() == all.Count);
            }

            // ---- the outpost door
            R5Check("start: the door is locked for the hack, not hard-locked", door.Locked && !door.ComputerLocked && !oc.Unlocked);
            oc.HostHackResult(true);
            yield return new WaitForSeconds(1.2f);
            R5Check("a clean hack unlocks it and it opens", oc.Unlocked && !door.Locked && !door.ComputerLocked && door.Latched);
            Stand(inside);
            yield return new WaitForSeconds(0.4f);
            doors.Use(di, false);                                                      // (a player shuts it from inside)
            yield return new WaitForSeconds(1.6f);
            R5Check("shut from inside: it is shut and NOT locked (shutting a door locks nothing)", door.IsShut && !door.Locked && !door.ComputerLocked && oc.Unlocked);
            float open1 = 0f;
            for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime) { door.GuardNear = true; open1 = Mathf.Max(open1, door.Logic.Open); yield return null; }
            R5Check("an officer at the shut, unlocked outpost door gets it open", open1 > 0.9f, $"(opened {open1:0.00})");
            door.GuardNear = false;
            yield return new WaitForSeconds(2.5f);
            R5Check("...and it closes again behind him", door.IsShut && !door.Locked && !door.ComputerLocked && oc.Unlocked);

            // ---- the code: SECURITY, then LOCK (the terminal's name for this office's own door)
            R5Check("a name that is neither a room nor the office does nothing", !doors.ComputerLock(1, "ZZZ9", true) && !door.ComputerLocked);
            R5Check("the terminal locks the outpost's own door by its name (any capitals, spaces)", doors.ComputerLock(1, "  Security ", true));
            yield return new WaitForSeconds(1.0f);
            R5Check("locked with the code: shut, hard-locked, and the lock is shut again", door.IsShut && door.Locked && door.ComputerLocked && !oc.Unlocked);
            float worst = 0f;
            for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime) { door.GuardNear = true; worst = Mathf.Max(worst, door.Logic.Open); yield return null; }
            R5Check("an officer at the code-locked door does not get it open", door.IsShut && door.BlockerEnabled && worst < 0.01f, $"(opened {worst:0.00})");
            door.GuardNear = false;
            Stand(outside);
            doors.Use(di, true);
            R5Check("E on the code-locked door from outside does not open it", !door.Latched && door.IsShut);
            oc.HostHackResult(true);                                                   // (a second player hacks it from outside)
            yield return new WaitForSeconds(1.2f);
            R5Check("a hack from outside opens it again", oc.Unlocked && !door.Locked && !door.ComputerLocked && door.Latched);
            doors.Use(di, false);                                                      // (shut again, from outside this time)
            yield return new WaitForSeconds(1.6f);
            R5Check("shut again, from either side: it is not locked", door.IsShut && !door.Locked && !door.ComputerLocked && oc.Unlocked);
            R5Check("locked again with another name of the office (security office)", doors.ComputerLock(1, "security office", true));
            yield return new WaitForSeconds(1.0f);
            Stand(inside);
            doors.Use(di, true);                                                       // (E from inside)
            yield return new WaitForSeconds(0.6f);
            R5Check("E from inside unlocks and opens it", oc.Unlocked && !door.Locked && !door.ComputerLocked && door.Latched);
            doors.Use(di, false);
            yield return new WaitForSeconds(1.6f);
            R5Check("locked once more (SEC)", doors.ComputerLock(1, "sec", true));
            yield return new WaitForSeconds(1.0f);
            bool unlockedByCode = doors.ComputerLock(1, "office", false);
            yield return new WaitForSeconds(0.6f);
            R5Check("...and UNLOCK at the terminal undoes it and opens the door (as a hack and E from inside do)", unlockedByCode && oc.Unlocked && !door.Locked && !door.ComputerLocked && door.Latched);
            doors.ComputerLock(1, "outpost", false);
            R5Check("UNLOCK when it is not locked changes nothing", oc.Unlocked && !door.Locked && !door.ComputerLocked && door.Latched);
            doors.Use(di, false);
            yield return new WaitForSeconds(1.6f);

            // ---- the security computer's lock on an ordinary room
            if (dir != null)
            {
                var room = dir.rooms.FirstOrDefault(rm => rm.doors.Count > 0 && rm.role == null && !rm.doors.Contains(di));
                R5Check("a room to lock was found", room != null);
                if (room != null)
                {
                    int ri = room.doors[0];
                    var rd = doors.Get(ri);
                    var near = new Vector3(rd.Centre.x, 0.2f, rd.Centre.z) + (rd.AlongX ? Vector3.forward : Vector3.right) * 1.2f;
                    player.TeleportTo(near, Quaternion.identity);
                    doors.Use(ri, true);
                    yield return new WaitForSeconds(0.5f);
                    bool openedFirst = rd.Latched;
                    R5Check("the room's door opens normally first", openedFirst);
                    R5Check("locking an unknown room does nothing", !doors.ComputerLock(1, "ZZZ9", true));
                    R5Check("locking a room by its number (any capitals, spaces) works", doors.ComputerLock(1, " " + room.label.ToLowerInvariant() + " ", true));
                    yield return new WaitForSeconds(1.5f);
                    R5Check("the room's doors are locked and shut at once", room.doors.All(i => doors.Get(i).ComputerLocked && doors.Get(i).IsShut));
                    doors.Use(ri, true);
                    R5Check("E on a computer-locked door does nothing", !rd.Latched && rd.IsShut);
                    float worst2 = 0f;
                    for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime) { rd.GuardNear = true; worst2 = Mathf.Max(worst2, rd.Logic.Open); yield return null; }
                    R5Check("an officer at a computer-locked door does not get it open either", rd.IsShut && worst2 < 0.01f, $"(opened {worst2:0.00})");
                    R5Check("unlocking works", doors.ComputerLock(1, room.label, false));
                    yield return new WaitForSeconds(0.3f);
                    doors.Use(ri, true);
                    yield return new WaitForSeconds(0.5f);
                    R5Check("after the unlock the door opens again", !rd.ComputerLocked && rd.Latched);
                }
            }

            // ---- ammunition: a gun emptied on this floor has a full magazine again once the elevator has taken everybody to the next one
            GameObject ammoGun = null; RangedWeapon gun = null;
            if (RangedWeapon.Available)
            {
                ammoGun = RangedWeapon.Create(RangedWeapon.Kind.Pew, new Vector3(0f, 40f, 0f), null);
                gun = ammoGun.GetComponent<RangedWeapon>();
                float clock = Time.time;
                for (int i = 0; i < 40 && gun.Magazine.Rounds > 0; i++) { clock += 0.4f; gun.Magazine.TryFire(clock); }
                R5Check("ammo: a magazine empties on a floor (floors have no unlimited ammunition)", gun.Magazine.Rounds == 0 && !GunRules.UnlimitedAmmo(hm.Floor), $"({gun.Magazine.Rounds} left)");
            }

            // ---- floor 2: at most one melee weapon, on the desk
            yield return R5LoadFloor(2);
            if (gun != null)
            {
                R5Check("ammo: ...and it is full again after the elevator ride to the next floor", gun.Magazine.Rounds == gun.Magazine.Capacity, $"({gun.Magazine.Rounds} of {gun.Magazine.Capacity})");
                Object.Destroy(ammoGun);
            }
            var oc2 = OutpostController.Active;
            var w2 = Object.FindObjectsByType<MeleeWeapon>(FindObjectsSortMode.None);
            R5Check("floor 2 holds at most one melee weapon, on the outpost's desk", w2.Length <= 1 && (oc2 == null || w2.All(w => oc2.IsInside(w.transform.position) && (w.transform.position - oc2.KeypadPosition).magnitude < 14f)), $"({w2.Length})");
            var dir2 = FloorDirectory.Current(2);
            R5Check("floor 2: every numbered room found a door", dir2 != null && dir2.rooms.Count > 5 && dir2.rooms.All(rm => rm.doors.Count > 0));

            Log($"[R5] DONE fails={r5Fails}");
            Quit(r5Fails == 0 ? 0 : 1);
        }
    }
}