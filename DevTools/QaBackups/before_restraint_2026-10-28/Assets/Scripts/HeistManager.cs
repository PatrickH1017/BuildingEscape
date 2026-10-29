using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Hub = the basement safe zone (vendor, no guards, no quota).</summary>
    public enum HeistPhase { Lobby, Heist, FloorComplete, Won, Failed, Hub }

    /// <summary>Per-floor state: quota, banked / lost value, caught players, run total.</summary>
    public class HeistManager : MonoBehaviour
    {
        public static HeistManager Instance { get; private set; }

        [Tooltip("Off by default: floors have no countdown. Turn on to bust the team when timeLimitSeconds runs out.")]
        public bool useTimer = false;
        [Tooltip("Per-floor time limit (only used when Use Timer is on).")]
        public float timeLimitSeconds = 600f;
        public int maxCaught = 3;

        public HeistPhase Phase { get; private set; } = HeistPhase.Lobby;
        /// <summary>Level index: 0 = Basement, 1..10 = office floors.</summary>
        public int Floor { get; private set; }
        /// <summary>Number of levels including the basement.</summary>
        public int TotalFloors { get; private set; } = TowerRules.Levels;
        public string FloorLabel => TowerRules.Label(Floor);
        public int Quota { get; private set; }
        public int Banked { get; private set; }
        public int Lost { get; private set; }
        public int Caught { get; private set; }
        public int RunTotal { get; private set; }
        /// <summary>Spendable money for the whole run. Earned by selling loot to the basement vendor.</summary>
        public int Cash { get; private set; }
        public float TimeLeft { get; private set; }
        /// <summary>True if the floor was lost because a guard called for backup.</summary>
        public bool BackupCalled { get; private set; }
        public string BackupMessage { get; private set; }
        public float BackupMessageUntil { get; private set; }

        // ---- backup, repeating: after the first call another officer arrives every CustodyRules.BackupIntervalSeconds
        readonly BackupClock backupClock = new();
        int netBackupIn = -1;
        /// <summary>True from the first backup call on this floor until it ends.</summary>
        public bool BackupActive => Net.NetRole.Simulates ? backupClock.Active : netBackupIn >= 0;
        /// <summary>Whole seconds until the next officer is called, or -1 while no backup has been called.</summary>
        public int BackupIn => Net.NetRole.Simulates ? backupClock.ShownSeconds : netBackupIn;
        /// <summary>Where the last backup call came from (the next officers are sent towards the team).</summary>
        Vector3 lastBackupPos;

        // ---- marked players and the Big Alarm (the owner, 2026-10-24): a sighting marks THAT player and nobody else; the alert goes off when every marked player is pinned (unless the Big Alarm is on); the Big Alarm stays on until the floor ends
        readonly AlertState alertState = new AlertState();
        bool netBigAlarm;
        /// <summary>Tests: count the party as this many players (0: count the players there are).</summary>
        public static int TestPartySize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAlertStatics() { TestPartySize = 0; }

        /// <summary>The Big Alarm is on on this floor: enough of the party was marked (<see cref="AlertRules.BigAlarmReached"/>). It never goes off before the floor ends.</summary>
        public bool BigAlarmActive => Net.NetRole.Simulates ? alertState.BigAlarm : netBigAlarm;
        /// <summary>How many players are marked now (host / offline).</summary>
        public int MarkedCount => alertState.MarkedCount;
        /// <summary>Is this player marked (host / offline)? A guard that spots a player marks him and nobody else.</summary>
        public bool IsMarked(WobblyPlayer p) => p && alertState.IsMarked(p.GetInstanceID());
        /// <summary>The most guards alive at once on this floor now: 6 on floors 1 and 2, 8 with the Big Alarm; 13 from floor 3 (<see cref="AlertRules.GuardCap"/>).</summary>
        public int GuardCap => AlertRules.GuardCap(Floor, BigAlarmActive);
        /// <summary>Until this time the officers' radios are jammed (the alarm panel in a guard outpost was just used): they call no backup.</summary>
        float radiosJammedUntil;
        /// <summary>True while the radios are jammed after the alarm panel was used.</summary>
        public bool RadiosJammed => CustodyRules.RadioJammed(Time.time, radiosJammedUntil);

        [Header("Test mode")]
        public bool godMode;
        public bool freezeTimer;

        public event Action<HeistPhase> OnPhaseChanged;
        public event Action<int> OnBankedChanged;
        public event Action<int> OnCashChanged;

        void Awake()
        {
            Instance = this;
            if (!GetComponent<LootScanner>()) gameObject.AddComponent<LootScanner>();      // hold Z to scan the rooms round about for loot
        }
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>How many floors the party has entered (the docks do not count): "once a round" (the owner, 2026-10-25: the invisible consumable) is once for each.</summary>
        public int Round { get; private set; }

        public void StartFloor(int floor, int totalFloors, int quota)
        {
            Floor = floor; TotalFloors = totalFloors; Quota = quota;
            if (floor > 0) Round++;
            Banked = Lost = Caught = 0;
            BackupCalled = false;
            backupClock.Reset();
            radiosJammedUntil = 0f;
            alertState.Reset(); netBigAlarm = false;           // (a new floor: nobody is marked, no Big Alarm)
            replacementsDue.Clear(); ReplacementsMade = 0;     // (a new floor: nobody is waiting to be replaced)
            if (Net.NetRole.Simulates) ClearPins();   // a new floor: nobody is held down any more (busted players stay busted)
            if (floor > 0) SurplusPaid = 0;
            TimeLeft = timeLimitSeconds;
            SetPhase(floor <= 0 ? HeistPhase.Hub : HeistPhase.Heist);
        }

        public void AddCash(int amount)
        {
            if (amount <= 0) return;
            Cash += amount;
            OnCashChanged?.Invoke(Cash);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > Cash) return false;
            Cash -= amount;
            OnCashChanged?.Invoke(Cash);
            return true;
        }

        float unlockClock;

        /// <summary>
        /// In the docks, twice a second (the host, or offline): every gun a player carries back to the Loading Docks is unlocked for the party (<see cref="RunUnlocks"/>: the dealer then sells its ammunition).
        /// </summary>
        void TickUnlocks(float dt)
        {
            if (!Net.NetRole.Simulates) return;
            unlockClock += dt;
            if (unlockClock < 0.5f) return;
            unlockClock = 0f;
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.TryGetComponent(out Backpack bp)) RunUnlocks.CollectFrom(bp);
        }

        void Update()
        {
            if (Phase == HeistPhase.Hub) { TickUnlocks(Time.deltaTime); return; }
            if (Phase != HeistPhase.Heist) return;
            if (Net.NetRole.Simulates)
            {
                TickCustody(Time.deltaTime);
                TickBackup(Time.deltaTime);
                TickReplacements();
                TickAlert();
                CheckTeamDown();   // also when a free player leaves the game: the ones left may all be pinned or busted
            }
            if (Phase != HeistPhase.Heist) return;
            if (!useTimer || freezeTimer) return; // no countdown on a normal run
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f && Net.NetRole.Simulates) SetPhase(HeistPhase.Failed);
        }

        /// <summary>Client: take the host's run state (the countdown keeps running locally between updates).</summary>
        internal void ApplyNet(HeistPhase phase, int floor, int totalFloors, int quota, int banked, int lost, int caught,
                               int runTotal, int cash, int surplusPaid, int timeLeft, bool backupCalled, int backupIn = -1, bool bigAlarm = false)
        {
            netBackupIn = backupIn; netBigAlarm = bigAlarm;
            Floor = floor; TotalFloors = totalFloors; Quota = quota; Lost = lost; Caught = caught; RunTotal = runTotal;
            SurplusPaid = surplusPaid; BackupCalled = backupCalled;
            if (banked != Banked) { Banked = banked; OnBankedChanged?.Invoke(Banked); }
            if (cash != Cash) { Cash = cash; OnCashChanged?.Invoke(Cash); }
            if (Mathf.Abs(TimeLeft - timeLeft) > 1.5f) TimeLeft = timeLeft;
            if (phase != Phase) SetPhase(phase);
        }

        /// <summary>Client: the host's backup-call banner.</summary>
        public void NetBackupMessage(string message)
        {
            BackupMessage = message;
            BackupMessageUntil = Time.time + 4f;
        }

        public void Bank(HeavyLoot l) => BankAmount(l.CurrentValue);

        /// <summary>Adds cash value to the floor quota (loot or small items dropped in the elevator).</summary>
        public void BankAmount(int v)
        {
            if (Phase != HeistPhase.Heist && Phase != HeistPhase.FloorComplete) return;
            Banked += v; RunTotal += v;
            OnBankedChanged?.Invoke(Banked);
            // the boss floor is won by knocking the boss out, not by banking loot
            if (Phase == HeistPhase.Heist && Banked >= Quota && !TowerRules.IsBoss(Floor))
                SetPhase(HeistPhase.FloorComplete);
        }

        /// <summary>The boss is down for good: the team has escaped.</summary>
        public void BossDefeated()
        {
            if (Phase == HeistPhase.Heist && TowerRules.IsBoss(Floor)) SetPhase(HeistPhase.Won);
        }

        /// <summary>Test mode: meet the quota right now.</summary>
        public void DebugCompleteFloor()
        {
            if (Phase != HeistPhase.Heist) return;
            Banked = Quota;
            OnBankedChanged?.Invoke(Banked);
            SetPhase(TowerRules.IsBoss(Floor) ? HeistPhase.Won : HeistPhase.FloorComplete);
        }

        /// <summary>The whole team is busted: the run is over (nobody is free any more, or a test forced it).</summary>
        public void Bust(WobblyPlayer p)
        {
            if (Phase != HeistPhase.Heist || godMode) return;
            if (p) p.Stumble(Vector3.up * 3f, 3f);
            SetPhase(HeistPhase.Failed);
        }

        // ------------------------------------------------------------------ pinned / busted / buy-back (host or offline)

        /// <summary>
        /// An officer touched a player: they are pinned down for <see cref="CustodyRules.PinSeconds"/>. Returns false when the
        /// officer could not pin them (already down, just freed, god mode...), so he keeps chasing. If that leaves nobody
        /// free, the team is busted at once.
        /// </summary>
        public bool Pin(WobblyPlayer p, GuardAI by)
        {
            if (Phase != HeistPhase.Heist || godMode || !p || !Net.NetRole.Simulates) return false;
            if (!CustodyRules.CanPin(p.Custody, Time.time, p.PinImmuneUntil)) return false;
            p.PinnedBy = by;
            SetCustody(p, CustodyState.Pinned, CustodyRules.PinSeconds, 0);
            Announce($"{Who(p)} PINNED - hit the officer to free them ({CustodyRules.PinSeconds:0} s)");
            CheckTeamDown();
            return true;
        }

        /// <summary>The officer holding a player down was hit (or stunned, or knocked out): the pinned player gets free.</summary>
        public void FreePinned(WobblyPlayer p)
        {
            if (!p || !Net.NetRole.Simulates || p.Custody != CustodyState.Pinned) return;
            p.PinImmuneUntil = Time.time + CustodyRules.PinImmunitySeconds;
            SetCustody(p, CustodyState.Free, 0f, 0);
            Announce($"{Who(p, "broke", "break")} free!");
        }

        /// <summary>
        /// A shotgun or a sniper rifle hit this player (<see cref="GuardLoadouts.Eliminates"/>; the owner, 2026-10-19: "they should explode in a manner with reference to R.E.P.O"): their floor is over. They are busted at once - knocked
        /// out for the floor, they spectate their teammates, and the vendor buys them back at the Loading Docks - and the blast is shown on every machine (<see cref="BlastFx"/>). Host / offline. False when it could not happen (not in a run,
        /// god mode, already out): the hit is then only a knock-down.
        /// </summary>
        public bool Blast(WobblyPlayer p, GuardAI by)
        {
            if (Phase != HeistPhase.Heist || godMode || !p || !Net.NetRole.Simulates) return false;
            if (p.Custody == CustodyState.Busted) return false;
            var holder = p.PinnedBy;
            SetCustody(p, CustodyState.Busted, 0f, Floor);
            if (holder) holder.ReleaseHold();
            var np = p.GetComponent<Net.NetPlayer>();
            int slot = np && np.IsSpawned ? np.Slot : -1;
            var at = p.transform.position + Vector3.up * 0.25f;                  // (a body's pivot is its middle: the chest is a little above it)
            BlastFx.Play(at, HudTheme.PlayerColour(slot + 1));
            Net.RunSync.Instance?.BroadcastBlast(slot, at);
            var away = by ? p.transform.position - by.transform.position : Vector3.back;
            away.y = 0f;
            p.Stumble((away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back) * 6f + Vector3.up * 5f, 2f);       // (the body is thrown clear)
            Announce($"{Who(p, "was", "were")} blown up - buy them back from the vendor at the Loading Docks");
            CheckTeamDown();
            return true;
        }

        /// <summary>The pin ran out: the player is arrested. They stay busted until the vendor gets them out.</summary>
        void BustPlayer(WobblyPlayer p)
        {
            var guard = p.PinnedBy;
            SetCustody(p, CustodyState.Busted, 0f, Floor);
            if (guard) guard.ReleaseHold();
            Announce($"{Who(p)} BUSTED - buy them back from the vendor at the Loading Docks");
            CheckTeamDown();
        }

        /// <summary>Vendor buy-back: the busted player is out (host). The caller already took the money.</summary>
        public void ReleaseBusted(WobblyPlayer p)
        {
            if (!p || !Net.NetRole.Simulates || p.Custody != CustodyState.Busted) return;
            p.PinImmuneUntil = Time.time + CustodyRules.PinImmunitySeconds;
            SetCustody(p, CustodyState.Free, 0f, 0);
            Announce($"{Who(p, "was", "were")} bought back from the vendor");
        }

        /// <summary>Tells everyone something that happened to the loot (a piece shattered, fell out of the world...): the banner, on every machine. Host or offline.</summary>
        public void Say(string text) => Announce(text);

        /// <summary>Tell everyone what happened (the banner, on every machine).</summary>
        void Announce(string text)
        {
            BackupMessage = text;
            BackupMessageUntil = Time.time + 4f;
            Net.RunSync.Instance?.BroadcastBackupMessage(text);
        }

        /// <summary>Apply a custody state here and publish it to the other machines.</summary>
        void SetCustody(WobblyPlayer p, CustodyState state, float pinLeft, int bustedLevel)
        {
            p.ApplyCustody(state, pinLeft, bustedLevel);
            var np = p.GetComponent<Net.NetPlayer>();
            if (np && np.IsSpawned) np.HostSetCustody(state, CustodyRules.ShownSeconds(pinLeft), bustedLevel);
        }

        void ClearPins()
        {
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.Custody == CustodyState.Pinned) { p.PinnedBy = null; SetCustody(p, CustodyState.Free, 0f, 0); }
        }

        /// <summary>Nobody free any more (everybody pinned or busted): the team is busted.</summary>
        void CheckTeamDown()
        {
            if (Phase != HeistPhase.Heist || godMode) return;
            int free = 0, total = 0;
            foreach (var p in WobblyPlayerRegistry.All) { if (!p) continue; total++; if (p.Custody == CustodyState.Free) free++; }
            if (CustodyRules.TeamDown(free, total)) SetPhase(HeistPhase.Failed);
        }

        void TickCustody(float dt)
        {
            for (int i = 0; i < WobblyPlayerRegistry.All.Count; i++)
            {
                var p = WobblyPlayerRegistry.All[i];
                if (!p || p.Custody != CustodyState.Pinned) continue;
                // the officer can be gone (destroyed with the floor, knocked out): then there is nobody holding them
                if (!p.PinnedBy || p.PinnedBy.Dead) { FreePinned(p); continue; }
                float left = CustodyRules.PinLeftAfter(p.PinLeft, dt);
                if (left <= 0f) { BustPlayer(p); if (Phase != HeistPhase.Heist) return; continue; }
                SetCustody(p, CustodyState.Pinned, left, 0);
            }
        }

        /// <summary>Players of the team that are free / pinned / busted right now (for the HUD).</summary>
        public void CountCustody(out int free, out int pinned, out int busted)
        {
            free = pinned = busted = 0;
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p) continue;
                if (p.Custody == CustodyState.Free) free++;
                else if (p.Custody == CustodyState.Pinned) pinned++;
                else busted++;
            }
        }

        /// <summary>"Player 2 is" / "You are" (or the past / plural forms given), for the banner texts.</summary>
        static string Who(WobblyPlayer p, string third = "is", string you = "are")
        {
            var np = p.GetComponent<Net.NetPlayer>();
            return np && np.IsSpawned ? $"Player {np.Slot + 1} {third}" : $"You {you}";
        }

        // ------------------------------------------------------------------ backup

        /// <summary>
        /// A guard finished radioing. The first call on a floor sends two officers and starts the clock: from then on
        /// ANOTHER officer is called every <see cref="CustodyRules.BackupIntervalSeconds"/> until the floor ends.
        /// </summary>
        public void CallBackup(GuardAI by, WobblyPlayer spotted = null)
        {
            if (Phase != HeistPhase.Heist) return;
            if (by && Net.NetRole.Simulates && RadiosJammed)
            {
                // the alarm panel was just used: his radio is dead, so he chases without calling anybody
                Announce("RADIOS JAMMED - the officer cannot call for backup");
                return;
            }
            lastBackupPos = by ? by.transform.position : Vector3.zero;
            bool bigNow = MarkSpotted(spotted, false);                                  // (the player the guard spotted is marked, nobody else; an alarm or a body marks nobody)
            BackupCalled = true;
            backupClock.Start(CustodyRules.BackupInterval(Floor));
            SendOfficers(2, lastBackupPos, bigNow ? "BIG ALARM" : "BACKUP CALLED");
        }

        /// <summary>
        /// An alarm (the guard outpost's lock shorting out) calls for backup exactly as a guard who spotted a player does: two officers
        /// now, one more every <see cref="CustodyRules.BackupIntervalSeconds"/> until the floor ends. The first officers are sent to
        /// <paramref name="where"/>; the later ones go for the team. Host or offline only.
        /// </summary>
        public void CallBackupAt(Vector3 where, string headline = "ALARM TRIPPED - BACKUP CALLED")
        {
            if (Phase != HeistPhase.Heist || !Net.NetRole.Simulates) return;
            lastBackupPos = where;
            BackupCalled = true;
            backupClock.Start(CustodyRules.BackupInterval(Floor));
            SendOfficers(2, where, headline);
        }

        /// <summary>
        /// The alarm panel in a guard outpost was used (host or offline only): the backup is called off, whatever started it - a lock that
        /// shorted out or an officer who spotted somebody. No more officers are called (the ones already on the floor stay and keep
        /// chasing), and for <see cref="CustodyRules.StandDownJamSeconds"/> the officers' radios are jammed, so an officer who was in the
        /// middle of keying his radio cannot start it all over again. A later sighting calls backup as usual. Returns false when there
        /// was nothing to call off.
        /// </summary>
        public bool StandDown()
        {
            if (Phase != HeistPhase.Heist || !Net.NetRole.Simulates) return false;
            if (alertState.BigAlarm) return false;                                       // (the Big Alarm is never shut off: the owner, 2026-10-24)
            bool wasOn = backupClock.Active || BackupCalled;
            if (!wasOn) return false;
            backupClock.Reset();
            BackupCalled = false;
            alertState.ClearMarks();
            radiosJammedUntil = Time.time + CustodyRules.StandDownJamSeconds;
            Announce("ALL QUIET - the alarm is off and no more officers are coming");
            return true;
        }

        /// <summary>
        /// The dev menu's "turn the alarm off / cancel the backup" (the owner, 2026-10-27: "the dev menu buttons for getting rid of wanted level/backup don't work"; host / offline): the backup is called off whatever started it, the Big Alarm - which the alarm panel can never shut off (<see cref="StandDown"/>
        /// refuses) - and every mark are cleared, the officers the backup called leave and every other officer who was after somebody goes back to his round. True when anything was on.
        /// </summary>
        public bool DevClearAlert()
        {
            if (Phase != HeistPhase.Heist || !Net.NetRole.Simulates) return false;
            bool was = backupClock.Active || BackupCalled || alertState.BigAlarm || alertState.MarkedCount > 0;
            backupClock.Reset();
            BackupCalled = false;
            alertState.Reset(); netBigAlarm = false;
            radiosJammedUntil = Time.time + CustodyRules.StandDownJamSeconds;
            foreach (var g in new System.Collections.Generic.List<GuardAI>(GuardAI.All))
            {
                if (!g || g.Dead || g.IsPuppet) continue;
                if (g.IsBackup) { was = true; Destroy(g.gameObject); }
                else g.DevCalm();
            }
            if (was) Announce("ALL QUIET - the alarm is off and no more officers are coming");
            return was;
        }

        /// <summary>The party: how many players are in the run (<see cref="TestPartySize"/> when a test sets it).</summary>
        int PartySize()
        {
            if (TestPartySize > 0) return TestPartySize;
            int n = 0;
            foreach (var p in WobblyPlayerRegistry.All) if (p) n++;
            return n;
        }

        /// <summary>
        /// A guard spotted <paramref name="p"/> (host / offline; the owner, 2026-10-24: "when a player gets spotted it should only mark that player not all players, if another player is spotted they should now be marked as well"): that player is marked and nobody else.
        /// When enough of the party is marked (<see cref="AlertRules.BigAlarmReached"/>: half of it, both players of a duo) the Big Alarm goes on and stays on. True when this mark turned it on; <paramref name="announce"/> puts the banner up for it.
        /// </summary>
        public bool MarkSpotted(WobblyPlayer p, bool announce = true)
        {
            if (!p || !Net.NetRole.Simulates || Phase != HeistPhase.Heist) return false;
            bool big = alertState.Mark(p.GetInstanceID(), PartySize());
            if (big && announce) Announce("BIG ALARM");
            return big;
        }

        /// <summary>Host / offline, every frame: the alert goes off when every marked player is in custody (pinned or busted) and the Big Alarm is not on (the owner, 2026-10-24: "the security alert should turn off and the respawn rate of backup guards should go back to 45 seconds").</summary>
        void TickAlert()
        {
            if (alertState.MarkedCount == 0 || alertState.BigAlarm) return;
            int free = 0;
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && alertState.IsMarked(p.GetInstanceID()) && p.Custody == CustodyState.Free) free++;
            if (!alertState.ShouldEnd(free)) return;
            alertState.ClearMarks();
            if (!(backupClock.Active || BackupCalled)) return;
            backupClock.Reset();
            BackupCalled = false;
            Announce("ALL QUIET - the alarm is off and no more officers are coming");
        }

        void TickBackup(float dt)
        {
            if (!backupClock.Tick(dt)) return;
            // the next officer heads for the team: where a free player is, else where the last call came from
            var target = lastBackupPos;
            bool found = false;
            foreach (var p in WobblyPlayerRegistry.All)                                  // (the officers go for a MARKED player who is free, the owner, 2026-10-24)
                if (p && p.Custody == CustodyState.Free && IsMarked(p)) { target = p.transform.position; found = true; break; }
            if (!found && alertState.MarkedCount == 0)                                   // (an alarm with nobody marked: the team, as before)
                foreach (var p in WobblyPlayerRegistry.All)
                    if (p && p.Custody == CustodyState.Free) { target = p.transform.position; break; }
            SendOfficers(1, target, "ANOTHER OFFICER CALLED");
        }

        // ---- replacement guards (the owner, 2026-10-23: "If a guard is eliminated after 15 seconds a new guard should come from a stairwell")
        readonly System.Collections.Generic.List<float> replacementsDue = new();
        /// <summary>Tests: how many replacement guards came out of a stairwell on this floor, and how many are still waiting.</summary>
        public int ReplacementsMade { get; private set; }
        public int ReplacementsWaiting => replacementsDue.Count;
        /// <summary>Tests: the guards waiting for their replacement have been waiting <paramref name="seconds"/> longer.</summary>
        public void AgeReplacements(float seconds) { for (int i = 0; i < replacementsDue.Count; i++) replacementsDue[i] -= seconds; }

        /// <summary>A guard went down for good (host / offline): a new guard comes out of a stairwell after <see cref="AlertRules.RespawnSeconds"/> (the owner, 2026-10-24: 45 s while it is quiet, 20 s while the backup is on, on floors 1 and 2). Only while the floor is a live heist; the boss is never replaced.</summary>
        public void GuardEliminated(GuardAI g)
        {
            if (!Net.NetRole.Simulates || Phase != HeistPhase.Heist || !g || g.IsBoss || g.IsPuppet) return;
            replacementsDue.Add(Time.time);                      // (when he went down: the wait is judged when it is over, by the alert as it is then)
        }

        /// <summary>The replacements that are due come out of a stairwell, as long as fewer guards are alive than the floor's cap (<see cref="GuardCap"/>); a replacement that finds the floor full is not made.</summary>
        void TickReplacements()
        {
            if (replacementsDue.Count == 0) return;
            FloorGenerator gen = null;
            for (int i = replacementsDue.Count - 1; i >= 0; i--)
            {
                if (!AlertRules.RespawnDue(Time.time - replacementsDue[i], Floor, BackupActive)) continue;        // (45 s while it is quiet, 20 s while the backup is on: AlertRules.RespawnSeconds)
                replacementsDue.RemoveAt(i);
                int alive = 0;
                foreach (var g in GuardAI.All) if (g && !g.Dead) alive++;
                if (!GuardRules.ReplacementWanted(alive, GuardCap)) continue;
                if (!gen) gen = FindFirstObjectByType<FloorGenerator>();
                if (gen && gen.SpawnReplacement()) ReplacementsMade++;
            }
        }

        void SendOfficers(int wanted, Vector3 target, string headline)
        {
            var gen = FindFirstObjectByType<FloorGenerator>();
            int max = GuardCap;                                  // (the floor's cap, the owner, 2026-10-24: 6, 8 with the Big Alarm; 13 from floor 3)
            int alive = 0;
            foreach (var g in GuardAI.All) if (g && !g.Dead) alive++;
            int n = Mathf.Max(0, Mathf.Min(wanted, max - alive));
            if (n > 0 && gen) n = gen.SpawnReinforcements(target, n);
            BackupMessage = n > 0
                ? $"{headline} - {n} more {(n == 1 ? "officer is" : "guards are")} on the way ({alive + n}/{max}), next in {backupClock.Interval:0} s"
                : $"{headline} - but the floor is already full of guards ({alive}/{max})";
            BackupMessageUntil = Time.time + 4f;
            Net.RunSync.Instance?.BroadcastBackupMessage(BackupMessage);
        }

        /// <summary>Banked value above the quota: it becomes spendable cash when the team rides down.</summary>
        public int Surplus => Mathf.Max(0, Banked - Quota);

        public void PayOutSurplus()
        {
            int extra = Surplus;
            if (extra <= 0) return;
            AddCash(extra);
            SurplusPaid = extra;
        }

        /// <summary>Surplus paid into cash on the last ride down (shown in the basement).</summary>
        public int SurplusPaid { get; private set; }

        public void NotifyLootBroken(HeavyLoot l) { Lost += l.baseValue; }

        public void NotifyPlayerCaught(WobblyPlayer p)
        {
            if (Phase != HeistPhase.Heist || godMode) return;
            Caught++;
            p.Stumble(Vector3.up * 3f, 3f);
            if (Caught >= maxCaught) SetPhase(HeistPhase.Failed);
        }

        void SetPhase(HeistPhase p) { Phase = p; OnPhaseChanged?.Invoke(p); }
    }
}
