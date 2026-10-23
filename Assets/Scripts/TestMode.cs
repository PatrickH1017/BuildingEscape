using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Tester's cheat panel (F1). Only exists when Test Mode is switched on in the Unity editor
    /// (menu: Employees Take Revenge > Test Mode), so normal play is untouched.
    /// Gives cash, weapons and loot, jumps between floors, completes quotas, stuns guards, god mode.
    /// </summary>
    public class TestMode : MonoBehaviour
    {
        public const string PrefKey = "OvertimeEscape.TestMode";
        /// <summary>The check box "Show guards' vision cones" is remembered between Play sessions (the editor only: the menu exists only with Test Mode on).</summary>
        public const string ConesPrefKey = "OvertimeEscape.TestMode.ShowGuardCones";

        /// <summary>True while the panel is open (the camera frees the cursor).</summary>
        public static bool PanelOpen { get; private set; }

        bool open;
        Vector2 scroll;
        string status = "";
        float statusUntil;
        float capacity = 20f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isEditor || PlayerPrefs.GetInt(PrefKey, 0) != 1) return;
            if (FindFirstObjectByType<TestMode>()) return;
            GuardVision.ShowCones = PlayerPrefs.GetInt(ConesPrefKey, 0) == 1;          // (off unless the check box was ticked in an earlier session)
            var go = new GameObject("TestMode");
            DontDestroyOnLoad(go);
            go.AddComponent<TestMode>();
            Debug.Log("Test Mode is ON - press F1 for the cheat panel. (Employees Take Revenge > Test Mode turns it off.)");
        }

        void OnDestroy() { PanelOpen = false; }

        void Update()
        {
            if (Net.NetSession.Online) { open = false; PanelOpen = false; return; } // cheats create things the sync does not know about
            if (Input.GetKeyDown(KeyCode.F1)) { open = !open; PanelOpen = open; }
            TickSafeTrip();
        }

        // ------------------------------------------------------------------ "Go" to a safe: to the room it stands in

        int tripFloor = -1, tripIndex;
        float tripSince, tripReadyAt;

        /// <summary>Go to the room of safe <paramref name="index"/> on floor <paramref name="floor"/>: at once when that floor is on show, else the floor is built first and the trip finishes when it stands.</summary>
        void GoToSafe(TowerManager tm, int floor, int index)
        {
            var hm = HeistManager.Instance;
            tripFloor = floor; tripIndex = index; tripSince = Time.time; tripReadyAt = 0f;
            if (hm && hm.Floor == floor && !tm.Loading && TryTripToSafe()) return;
            tm.GoToLevel(floor);
            Say("Going to floor " + floor + ", then to the safe's room");
        }

        void TickSafeTrip()
        {
            if (tripFloor < 0) return;
            var tm = TowerManager.Instance;
            if (!tm || Time.time - tripSince > 90f) { tripFloor = -1; return; }
            if (tm.Loading || !tm.HasFloor) { tripReadyAt = 0f; return; }
            if (tripReadyAt <= 0f) tripReadyAt = Time.time + 0.6f;            // (let the team be put down in the elevator first)
            if (Time.time < tripReadyAt) return;
            if (!TryTripToSafe()) { Say("Floor " + tripFloor + " has no safe " + (tripIndex + 1) + " on show"); tripFloor = -1; }
        }

        bool TryTripToSafe()
        {
            var pack = LocalPack();
            if (!pack) return false;
            foreach (var safe in FloorSafe.OnShow)
            {
                if (!safe || safe.Index != tripIndex) continue;
                TeleportTo(pack, new Vector3(safe.transform.position.x, 1.1f, safe.transform.position.z) + safe.transform.forward * 1.4f);
                Say("In the room of safe " + (tripIndex + 1) + " on floor " + tripFloor);
                tripFloor = -1;
                return true;
            }
            return false;
        }

        static Backpack LocalPack()
        {
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p) continue;
                var b = p.GetComponent<Backpack>();
                if (b && b.Viewed) return b;
            }
            foreach (var p in WobblyPlayerRegistry.All) if (p && p.GetComponent<Backpack>()) return p.GetComponent<Backpack>();
            return null;
        }

        void Say(string s) { status = s; statusUntil = Time.time + 3f; }

        Vector3 SpawnPoint(Backpack b) => b.transform.position + b.transform.forward * 2f + Vector3.up * 0.8f;

        void OnGUI()
        {
            if (Net.NetSession.Online) return;
            if (!open)
            {
                if (Event.current.type == EventType.Repaint) HudWidgets.TestTag();
                return;
            }

            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            var pack = LocalPack();
            float w = Mathf.Min(Screen.width - 24f, 620f);                  // (wide enough that the rows fit without sliding the menu left and right)
            float barTop = Screen.height - 130f * HudTheme.S;                // (the item shortcut bar starts about 122 px up from the bottom: the window ends above it)
            var r = new Rect(12, 60, w, Mathf.Max(200f, Mathf.Min(barTop - 66f, 760f)));
            GUI.Box(r, "TEST MODE");
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 26, w - 20, r.height - 34));
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);          // (no sideways scrollbar: up and down only)

            GUILayout.Label($"Cash: ${(hm ? hm.Cash : 0)}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+$100") && hm) hm.AddCash(100);
            if (GUILayout.Button("+$1,000") && hm) hm.AddCash(1000);
            if (GUILayout.Button("+$10,000") && hm) hm.AddCash(10000);
            GUILayout.EndHorizontal();

            AlarmTools(hm, pack);

            Header("Weapons (into your pack, equipped)");
            if (!MeleeWeapon.Available) GUILayout.Label("Weapons not set up: run Employees Take Revenge > Setup Weapons");
            GUILayout.BeginHorizontal();
            foreach (MeleeWeapon.Kind k in System.Enum.GetValues(typeof(MeleeWeapon.Kind)))
                if (GUILayout.Button(MeleeWeapon.Specs[(int)k].name) && pack && MeleeWeapon.Available) GiveWeapon(pack, k);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (RangedWeapon.Kind gk in System.Enum.GetValues(typeof(RangedWeapon.Kind)))
            {
                if (!GUILayout.Button(RangedWeapon.Specs[(int)gk].name)) continue;
                if (pack && RangedWeapon.Available && RangedWeapon.Has(gk)) GiveGun(pack, gk);
                else Say("The Guns pack is not set up: run Employees Take Revenge > Setup Weapons");
            }
            GUILayout.EndHorizontal();
            GunTools(pack);

            Header("Pack");
            GUILayout.Label($"Capacity {WeightUnits.Lbs(capacity)}   (using {WeightUnits.Num(pack ? pack.Weight : 0)})");
            float nc = GUILayout.HorizontalSlider(capacity, 5f, 300f);
            if (!Mathf.Approximately(nc, capacity)) { capacity = Mathf.Round(nc); if (pack) pack.capacity = capacity; }
            if (pack && GUILayout.Button("Drop all loot")) pack.DropAll();                   // (loot only: weapons, armour and the rest of the equipment stay in the pack)

            Header("Spawn loot in front of you");
            var gen = FindFirstObjectByType<FloorGenerator>();
            if (gen && pack)
            {
                var names = gen.SpawnableLootNames;
                for (int i = 0; i < names.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < Mathf.Min(i + 2, names.Length); j++)
                        if (GUILayout.Button(names[j]))
                        {
                            var floor = hm ? hm.Floor : 1;
                            if (!gen.SpawnLoot(j, SpawnPoint(pack), floor)) Say("Couldn't spawn (is the Office pack set up?)");
                        }
                    GUILayout.EndHorizontal();
                }
            }

            Header("Go to level");
            if (tm)
            {
                int rows = Mathf.CeilToInt(tm.totalFloors / 4f);          // the basement and every floor up to the boss, four to a row
                for (int row = 0; row < rows; row++)
                {
                    GUILayout.BeginHorizontal();
                    for (int c = 0; c < 4; c++)
                    {
                        int lvl = row * 4 + c;
                        if (lvl >= tm.totalFloors) break;
                        if (GUILayout.Button(lvl == 0 ? "Basement" : $"F{lvl}")) { tm.GoToLevel(lvl); Say($"Going to {(lvl == 0 ? "the basement" : "floor " + lvl)}"); }
                    }
                    GUILayout.EndHorizontal();
                }
            }

            TeleportTools(tm, gen, pack);

            Header("Floor tools");
            if (hm && GUILayout.Button("Complete quota (unlock elevator up)")) hm.DebugCompleteFloor();
            if (GUILayout.Button("Stun all guards 30s"))
                foreach (var g in FindObjectsByType<GuardAI>(FindObjectsSortMode.None)) g.Stun(30f, null);
            if (hm)
            {
                hm.godMode = GUILayout.Toggle(hm.godMode, "God mode (guards can't catch you)");
            }
            bool cones = GUILayout.Toggle(GuardVision.ShowCones, "Show guards' vision cones (hidden from players by default; what the guards see is the same either way)");
            if (cones != GuardVision.ShowCones)
            {
                GuardVision.ShowCones = cones;
                PlayerPrefs.SetInt(ConesPrefKey, cones ? 1 : 0);
                Say(cones ? "Guards' vision cones ON" : "Guards' vision cones off");
            }
            OutpostTools(tm, gen, pack);
            Header("Police (pinning / busted / backup)");
            WobblyPlayer me = null;
            if (pack) pack.TryGetComponent(out me);
            if (hm && me)
            {
                GUILayout.Label($"You: {me.Custody}" + (me.Custody == CustodyState.Pinned ? $" ({me.PinLeft:0} s)" : ""));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Pin me")) PinMe(hm, me);
                if (GUILayout.Button("Free me")) { me.PinImmuneUntil = 0f; hm.FreePinned(me); hm.ReleaseBusted(me); }
                GUILayout.EndHorizontal();
                GUILayout.Label("Pin needs a guard to hold you: the nearest one is used. Wait 12 s to get busted.");
            }
            if (hm)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Call backup now (starts the 30 s repeat)")) hm.CallBackup(null);
                if (GUILayout.Button("Cancel backup call"))
                    Say(hm.StandDown() ? "Backup called off: no more officers are coming" : "No backup call going - nothing to cancel");
                GUILayout.EndHorizontal();
            }

            if (Time.time < statusUntil) GUILayout.Label(status);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ the guard outpost and its alarm

        /// <summary>Switch the reinforcement alarm on or off (the teleports are in <see cref="TeleportTools"/>).</summary>
        void AlarmTools(HeistManager hm, Backpack pack)
        {
            Header("Alarm (reinforcements)");
            // The reinforcement alarm: what a guard's radio does. Two officers now, one more every 30 s until it is called off.
            GUILayout.Label("Reinforcements: " + (hm && hm.BackupActive ? "ON (an officer every 30 s)" : "off"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Trip the alarm (call reinforcements)"))
            {
                if (!hm || hm.Phase != HeistPhase.Heist) Say("Reinforcements only come on a floor with the heist on (not the basement)");
                else
                {
                    hm.CallBackupAt(pack ? pack.transform.position : Vector3.zero, "BACKUP CALLED");
                    Say("Reinforcements called: officers are on the way");
                }
            }
            if (GUILayout.Button("Turn alarm OFF (call them off)"))
                Say(hm && hm.StandDown() ? "Reinforcements called off" : "No reinforcements going - nothing to switch off");
            GUILayout.EndHorizontal();
            if (Time.time < statusUntil) GUILayout.Label(status);          // (the answer shows here, next to the buttons)
        }

        // ------------------------------------------------------------------ all the teleports, in one place (right under "Go to level")

        /// <summary>Every "take me there" button of the menu: the elevator, the guard outpost (door / inside / alarm panel), this floor's safes and the direction signs.</summary>
        void TeleportTools(TowerManager tm, FloorGenerator gen, Backpack pack)
        {
            Header("Teleports (this level)");
            var outpost = OutpostController.Active;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Elevator") && tm && pack) TeleportTo(pack, tm.CurrentInfo.spawn + new Vector3(0, 1.1f, 0));
            if (GUILayout.Button("Outpost door"))
            {
                if (!outpost || !pack) Say("No guard outpost on this level");
                else TeleportTo(pack, new Vector3(outpost.KeypadPosition.x, 1.1f, outpost.KeypadPosition.z) + outpost.Outward * 1.4f);
            }
            if (GUILayout.Button("Inside outpost"))
            {
                if (!outpost || !pack) Say("No guard outpost on this level");
                else
                {
                    var door = outpost.Door != null ? outpost.Door.Centre : outpost.KeypadPosition;
                    TeleportTo(pack, new Vector3(door.x, 1.1f, door.z) - outpost.Outward * 1.6f);
                }
            }
            if (outpost && outpost.PanelLamps != null && outpost.PanelLamps.Length > 0 && pack && GUILayout.Button("Alarm panel"))       // (the outposts have no alarm panel any more; the button stays for a floor that builds one)
                TeleportTo(pack, new Vector3(outpost.PanelPosition.x, 1.1f, outpost.PanelPosition.z) - outpost.Outward * 1.2f);
            GUILayout.EndHorizontal();

            bool anySafe = false;
            foreach (var safe in FloorSafe.OnShow) { if (safe) { anySafe = true; break; } }
            if (anySafe)
            {
                GUILayout.BeginHorizontal();
                foreach (var safe in FloorSafe.OnShow)
                    if (safe && pack && GUILayout.Button($"This floor's safe {safe.Index + 1}"))
                        TeleportTo(pack, new Vector3(safe.transform.position.x, 1.1f, safe.transform.position.z) + safe.transform.forward * 1.4f);
                GUILayout.EndHorizontal();
            }

            SignTools(gen, pack);
        }

        static void TeleportTo(Backpack pack, Vector3 position)
        {
            if (pack.TryGetComponent(out WobblyPlayer body)) body.TeleportTo(position, pack.transform.rotation);
            else pack.transform.position = position;
        }

        // ------------------------------------------------------------------ direction signs

        int signTour;

        /// <summary>The signs of this floor: what they say and a button that walks you from one to the next.</summary>
        void SignTools(FloorGenerator gen, Backpack pack)
        {
            var signs = gen ? gen.PlanSigns() : null;
            if (signs == null || signs.Count == 0) { GUILayout.Label("This level has no direction signs."); return; }
            var s = signs[signTour % signs.Count];
            GUILayout.Label($"{signs.Count} signs. #{signTour % signs.Count + 1}: {s.line1} {s.line2} {(s.arrowRight ? "->" : "<-")} ({s.steps} doorways to cell {s.toCellX},{s.toCellY})");
            if (pack && GUILayout.Button("To the next sign"))
            {
                signTour = (signTour + 1) % signs.Count;
                var t = signs[signTour];
                TeleportTo(pack, new Vector3(t.x, 1.1f, t.z) + new Vector3(t.nx, 0f, t.nz) * 2f);
            }
        }

        // ------------------------------------------------------------------ guard outposts and safes

        /// <summary>What a safe on show was really filled with (the rules' words are in the list above): guns and armour, bundles of cash, clips, watches - and a warning when the rules call for a gun or armour that was not made.</summary>
        static string SafeFilling(FloorSafe safe, int seed)
        {
            string text = safe.GunCount + " gun/armour, " + safe.BundleCount + " cash bundle(s), " + safe.ClipCount + " clip(s), " + safe.WatchCount + " watch(es)";
            if (SafeRules.IsWeaponFloor(safe.Floor) && safe.GunCount == 0 && SafeRules.WeaponContents(seed, safe.Floor, safe.Index).Length > 0) text += " - the gun/armour the rules call for was NOT made";
            return text;
        }

        /// <summary>The floors with a safe (and their codes), the lock of this floor's outpost, and this floor's safe.</summary>
        void OutpostTools(TowerManager tm, FloorGenerator gen, Backpack pack)
        {
            Header("Guard outpost & safes");
            if (!tm) return;

            int seed = tm.IntelSeed;
            var safeFloors = SafeRules.SafeFloors(seed);
            GUILayout.Label($"Safe seed {seed}: " + (safeFloors.Length == 0 ? "no floor has a safe" : safeFloors.Length + " floor(s) have one"));
            foreach (int f in safeFloors)
            {
                int count = SafeRules.SafeCount(seed, f);
                for (int i = 0; i < count; i++)
                {
                    string room = gen ? gen.SafeRoomLabel(f, i) : null;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"F{f}" + (count > 1 ? $" safe {i + 1}/{count}" : "") + $": room {room ?? "?"}, code {SafeRules.CodeText(SafeRules.Code(seed, f, i))}, " +
                                    $"({(SafeRules.RecordGivesCode(seed, f, i) ? "code on record" : "no code: crowbar")}) - holds {SafeRules.ContentsText(seed, f, i)}", GUILayout.MaxWidth(Screen.width > 700 ? 500f : 300f));
                    if (GUILayout.Button("Go", GUILayout.Width(36))) GoToSafe(tm, f, i);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Learn every safe (journal)"))
            {
                foreach (int f in safeFloors)
                    for (int i = 0, n = SafeRules.SafeCount(seed, f); i < n; i++)
                        SafeIntel.Add(f, i, n, gen ? gen.SafeRoomLabel(f, i) : null, SafeRules.Code(seed, f, i));
                Say("The journal (J) lists every safe now");
            }
            if (GUILayout.Button("New safes (re-roll)")) { tm.RerollIntel(); Say("New safes - the floor is built again"); }
            GUILayout.EndHorizontal();

            var outpost = OutpostController.Active;
            if (outpost)
            {
                GUILayout.Label("Outpost lock: " + (outpost.Unlocked ? "OPEN" : outpost.AlarmOn ? "ALARM (jammed)" : "locked"));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Unlock it")) outpost.HostHackResult(true);
                GUILayout.EndHorizontal();
                if (outpost.PanelLamps != null && outpost.PanelLamps.Length > 0)      // (the outposts have no alarm panel any more; the row stays for a floor that builds one)
                {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Alarm panel: " + (outpost.AlertOn ? "ALERT (alarm or backup going)" : "quiet"));
                if (GUILayout.Button("Press it")) { if (outpost.HostStandDown()) outpost.PlayStandDown(); else Say("Nothing to switch off"); }
                GUILayout.EndHorizontal();
                }
            }
            else GUILayout.Label("This level has no guard outpost.");

            foreach (var safe in FloorSafe.OnShow)
            {
                if (!safe) continue;
                GUILayout.Label($"This floor's safe {safe.Index + 1}: {(safe.IsOpen ? "OPEN" : "shut")}, code {SafeRules.CodeText(safe.Code)}, rules say {SafeRules.ContentsText(seed, safe.Floor, safe.Index)}; in it: {SafeFilling(safe, seed)}");
                GUILayout.BeginHorizontal();
                if (!safe.IsOpen && GUILayout.Button("Open it")) safe.ApplyOpen(false);
                GUILayout.EndHorizontal();
            }
        }

        static GUIStyle headerStyle;

        static void Header(string s)
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>" + s + "</b>", headerStyle ??= new GUIStyle(GUI.skin.label) { richText = true });
        }

        /// <summary>Pin the local player under the nearest live guard (offline only; with one player that busts the team at once).</summary>
        void PinMe(HeistManager hm, WobblyPlayer me)
        {
            GuardAI best = null; float bd = float.MaxValue;
            foreach (var g in GuardAI.All)
            {
                if (!g || g.Dead) continue;
                float d = (g.transform.position - me.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = g; }
            }
            if (!best) { Say("No guard on this floor to pin you"); return; }
            me.PinImmuneUntil = 0f;
            if (hm.Pin(me, best)) best.BeginHold(me);
        }

        void GiveWeapon(Backpack pack, MeleeWeapon.Kind k)
        {
            var go = MeleeWeapon.Create(k, SpawnPoint(pack), null);
            var floor = FindFirstObjectByType<FloorGenerator>();
            if (floor && floor.Current) go.transform.SetParent(floor.Current, true);
            var p = go.GetComponent<Pickup>();
            if (!pack.TryStore(p)) { Say("Pack too full - dropped it in front of you (raise capacity)"); return; }
            pack.SetEquipped(p);
        }

        // ------------------------------------------------------------------ the pew and armed guards

        void GiveGun(Backpack pack, RangedWeapon.Kind kind)
        {
            var go = RangedWeapon.Create(kind, SpawnPoint(pack), null);
            var floor = FindFirstObjectByType<FloorGenerator>();
            if (floor && floor.Current) go.transform.SetParent(floor.Current, true);
            var p = go.GetComponent<Pickup>();
            if (!pack.TryStore(p)) { Say("Pack too full - dropped it in front of you (raise capacity)"); return; }
            pack.SetEquipped(p);
            RunUnlocks.Unlock(kind);                                       // (the dealer sells its ammunition now: it was "found")
            Say(RangedWeapon.Specs[(int)kind].name + " in your hand: LMB fires, R reloads");
        }

        void GiveArmour(Backpack pack, ShieldTier tier)
        {
            foreach (var held in pack.Snapshot())
                if (held && held.TryGetComponent(out ArmourItem _)) { pack.Drop(held); Destroy(held.gameObject); }          // (one piece at a time: the old one goes)
            if (tier == ShieldTier.None) { Say("No armour on you"); return; }
            var go = ArmourItem.Create(tier, SpawnPoint(pack), null, 0f);
            var floor = FindFirstObjectByType<FloorGenerator>();
            if (floor && floor.Current) go.transform.SetParent(floor.Current, true);
            if (!pack.TryStore(go.GetComponent<Pickup>())) { Say("Pack too full - dropped it in front of you (raise capacity)"); return; }
            RunUnlocks.UnlockArmour(tier);                                 // (the dealer sells it now: it was "found")
            Say(ArmourRules.Name(tier) + " on you: a guard's bullet knocks you down for " + Mathf.RoundToInt(ArmourRules.HitShare(tier) * 100f) + "% of the time");
        }

        void GivePew(Backpack pack) => GiveGun(pack, RangedWeapon.Kind.Pew);          // (the tests call it by this name)

        void GunTools(Backpack pack)
        {
            Header("Pew & armed guards");
            var gun = pack && pack.Equipped ? pack.Equipped.GetComponent<RangedWeapon>() : null;
            if (gun != null)
            {
                var m = gun.Magazine;
                GUILayout.Label(m.Reloading ? gun.Data.name + ": reloading ..." : $"{gun.Data.name}: {m.Rounds} / {m.Capacity} rounds, {(m.InfiniteReserve ? "endless" : m.Reserve.ToString())} spare (LMB fires, R reloads)");
                if (GUILayout.Button("Refill the magazine and the spare clips"))
                {
                    m.Fill();
                    if (!m.InfiniteReserve) m.SetReserve(Mathf.Max(m.Reserve, RangedWeapon.Specs[(int)gun.kind].spareClips * m.Capacity));
                }
            }
            else GUILayout.Label("Equip the Pew (inventory) to shoot: 8 rounds a clip, two spare clips, then it only clicks");

            bool every = GUILayout.Toggle(ArmedGuards.Everywhere, "Every guard carries a pew (now and on new floors)");
            if (every != ArmedGuards.Everywhere)
            {
                ArmedGuards.Everywhere = every;
                Say(every ? $"{ArmedGuards.ArmAllNow()} guard(s) armed" : $"{ArmedGuards.DisarmAll()} guard(s) disarmed");
            }
            int alive = 0, armed = 0;
            foreach (var g in GuardAI.All) { if (!g || g.Dead) continue; alive++; if (g.Armed) armed++; }
            GUILayout.Label($"Guards on this floor: {alive}, armed: {armed}");
            GUILayout.Label("Every guard on this floor carries (and wears the shield that goes with it):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("nothing")) Say($"{ArmedGuards.SetAllNow(null)} guard(s) unarmed");
            foreach (var k in new[] { RangedWeapon.Kind.Pew, RangedWeapon.Kind.Smg, RangedWeapon.Kind.Rifle, RangedWeapon.Kind.Shotgun, RangedWeapon.Kind.Sniper })
                if (RangedWeapon.Has(k) && GUILayout.Button(RangedWeapon.Specs[(int)k].name)) Say($"{ArmedGuards.SetAllNow(k)} guard(s) now carry the {RangedWeapon.Specs[(int)k].name}, with a {GuardLoadouts.ShieldOf(k).ToString().ToLower()} shield");
            GUILayout.EndHorizontal();
            GUILayout.Label("Armour on you (the dealer sells that tier from then on):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("none")) GiveArmour(pack, ShieldTier.None);
            foreach (var at in new[] { ShieldTier.Light, ShieldTier.Medium, ShieldTier.Heavy })
                if (GUILayout.Button(ArmourRules.Name(at))) GiveArmour(pack, at);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Show the blast of a shotgun or sniper hit (only the picture, in front of you)") && WobblyPlayerRegistry.All.Count > 0 && WobblyPlayerRegistry.All[0])
            {
                var me = WobblyPlayerRegistry.All[0];
                BlastFx.Play(me.transform.position + me.transform.forward * 3.5f + Vector3.up * 0.3f, HudTheme.PlayerColour(1));
            }

            GUILayout.BeginHorizontal();
            int first = ArmedGuards.FirstArmedFloor;
            GUILayout.Label(first == ArmedRules.Never ? "Guards are armed from floor: none" : $"Guards are armed from floor: {first}");
            if (GUILayout.Button("-", GUILayout.Width(28))) ArmedGuards.FirstArmedFloor = first == ArmedRules.Never ? 1 : Mathf.Max(1, first - 1);
            if (GUILayout.Button("+", GUILayout.Width(28))) ArmedGuards.FirstArmedFloor = first == ArmedRules.Never ? 1 : first + 1;
            if (GUILayout.Button("none", GUILayout.Width(48))) ArmedGuards.FirstArmedFloor = ArmedRules.Never;
            GUILayout.EndHorizontal();
            GUILayout.Label("(the floor rule applies to floors you enter from now on)");
        }
    }
}
