using System;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Hub = the basement safe zone (vendor, no guards, no timer, no quota).</summary>
    public enum HeistPhase { Lobby, Heist, FloorComplete, Won, Failed, Hub }

    /// <summary>Per-floor state: quota, timer, banked / lost value, caught players, run total.</summary>
    public class HeistManager : MonoBehaviour
    {
        public static HeistManager Instance { get; private set; }

        [Tooltip("Per-floor time limit. A full sweep is ~7 min, so this leaves slack.")]
        public float timeLimitSeconds = 600f;
        public int maxCaught = 3;

        public HeistPhase Phase { get; private set; } = HeistPhase.Lobby;
        /// <summary>Level index: 0 = Basement, 1..10 = office floors.</summary>
        public int Floor { get; private set; }
        /// <summary>Number of levels including the basement.</summary>
        public int TotalFloors { get; private set; } = 11;
        public string FloorLabel => Floor <= 0 ? "Basement" : $"Floor {Floor}";
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

        [Header("Test mode")]
        public bool godMode;
        public bool freezeTimer;

        public event Action<HeistPhase> OnPhaseChanged;
        public event Action<int> OnBankedChanged;
        public event Action<int> OnCashChanged;

        void Awake() { Instance = this; }

        public void StartFloor(int floor, int totalFloors, int quota)
        {
            Floor = floor; TotalFloors = totalFloors; Quota = quota;
            Banked = Lost = Caught = 0;
            BackupCalled = false;
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
            if (freezeTimer) return;
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f && Net.NetRole.Simulates) SetPhase(HeistPhase.Failed);
        }

        /// <summary>Client: take the host's run state (the countdown keeps running locally between updates).</summary>
        internal void ApplyNet(HeistPhase phase, int floor, int totalFloors, int quota, int banked, int lost, int caught,
                               int runTotal, int cash, int surplusPaid, int timeLeft, bool backupCalled)
        {
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
            if (Phase == HeistPhase.Heist && Banked >= Quota)
                SetPhase(Floor >= TotalFloors - 1 ? HeistPhase.Won : HeistPhase.FloorComplete);
        }

        /// <summary>Test mode: meet the quota right now.</summary>
        public void DebugCompleteFloor()
        {
            if (Phase != HeistPhase.Heist) return;
            Banked = Quota;
            OnBankedChanged?.Invoke(Banked);
            SetPhase(Floor >= TotalFloors - 1 ? HeistPhase.Won : HeistPhase.FloorComplete);
        }

        /// <summary>A charging guard caught a player: the team is busted.</summary>
        public void Bust(WobblyPlayer p)
        {
            if (Phase != HeistPhase.Heist || godMode) return;
            p.Stumble(Vector3.up * 3f, 3f);
            SetPhase(HeistPhase.Failed);
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

        /// <summary>A guard finished radioing: the team is busted.</summary>
        public void CallBackup(GuardAI by)
        {
            if (Phase != HeistPhase.Heist) return;
            var gen = FindFirstObjectByType<FloorGenerator>();
            int max = gen ? gen.maxGuardsAlive : 24;
            int alive = 0;
            foreach (var g in GuardAI.All) if (g && !g.Dead) alive++;
            int n = Mathf.Max(0, Mathf.Min(2, max - alive));
            if (n > 0 && gen) n = gen.SpawnReinforcements(by ? by.transform.position : Vector3.zero, n);
            BackupMessage = n > 0
                ? $"BACKUP CALLED - {n} more guards are on the way ({alive + n}/{max})"
                : $"BACKUP CALLED - but the floor is already full of guards ({alive}/{max})";
            BackupMessageUntil = Time.time + 4f;
            Net.RunSync.Instance?.BroadcastBackupMessage(BackupMessage);
        }

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