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

        [Header("Test mode")]
        public bool godMode;
        public bool freezeTimer;

        public event Action<HeistPhase> OnPhaseChanged;
        public event Action<int> OnBankedChanged;
        public event Action<int> OnCashChanged;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void StartFloor(int floor, int totalFloors, int quota)
        {
            Floor = floor; TotalFloors = totalFloors; Quota = quota;
            Banked = Lost = Caught = 0;
            BackupCalled = false;
            backupClock.Reset();
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

        void Update()
        {
            if (Phase != HeistPhase.Heist) return;
            if (Net.NetRole.Simulates)
            {
                TickCustody(Time.deltaTime);
                TickBackup(Time.deltaTime);
                CheckTeamDown();   // also when a free player leaves the game: the ones left may all be pinned or busted
            }
            if (Phase != HeistPhase.Heist) return;
            if (!useTimer || freezeTimer) return; // no countdown on a normal run
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f && Net.NetRole.Simulates) SetPhase(HeistPhase.Failed);
        }

        /// <summary>Client: take the host's run state (the countdown keeps running locally between updates).</summary>
        internal void ApplyNet(HeistPhase phase, int floor, int totalFloors, int quota, int banked, int lost, int caught,
                               int runTotal, int cash, int surplusPaid, int timeLeft, bool backupCalled, int backupIn = -1)
        {
            netBackupIn = backupIn;
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
        public void CallBackup(GuardAI by)
        {
            if (Phase != HeistPhase.Heist) return;
            lastBackupPos = by ? by.transform.position : Vector3.zero;
            BackupCalled = true;
            backupClock.Start();
            SendOfficers(2, lastBackupPos, "BACKUP CALLED");
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
            backupClock.Start();
            SendOfficers(2, where, headline);
        }

        void TickBackup(float dt)
        {
            if (!backupClock.Tick(dt)) return;
            // the next officer heads for the team: where a free player is, else where the last call came from
            var target = lastBackupPos;
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.Custody == CustodyState.Free) { target = p.transform.position; break; }
            SendOfficers(1, target, "ANOTHER OFFICER CALLED");
        }

        void SendOfficers(int wanted, Vector3 target, string headline)
        {
            var gen = FindFirstObjectByType<FloorGenerator>();
            int max = gen ? gen.maxGuardsAlive : 24;
            int alive = 0;
            foreach (var g in GuardAI.All) if (g && !g.Dead) alive++;
            int n = Mathf.Max(0, Mathf.Min(wanted, max - alive));
            if (n > 0 && gen) n = gen.SpawnReinforcements(target, n);
            BackupMessage = n > 0
                ? $"{headline} - {n} more {(n == 1 ? "officer is" : "guards are")} on the way ({alive + n}/{max}), next in {CustodyRules.BackupIntervalSeconds:0} s"
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
