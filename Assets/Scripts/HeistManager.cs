using System;
using UnityEngine;

namespace BuildingEscape
{
    public enum HeistPhase { Lobby, Heist, FloorComplete, Won, Failed }

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
        public float TimeLeft { get; private set; }

        public event Action<HeistPhase> OnPhaseChanged;
        public event Action<int> OnBankedChanged;

        void Awake() { Instance = this; }

        public void StartFloor(int floor, int totalFloors, int quota)
        {
            Floor = floor; TotalFloors = totalFloors; Quota = quota;
            Banked = Lost = Caught = 0;
            TimeLeft = timeLimitSeconds;
            SetPhase(HeistPhase.Heist);
        }

        void Update()
        {
            if (Phase != HeistPhase.Heist) return;
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f) SetPhase(HeistPhase.Failed);
        }

        public void Bank(HeavyLoot l)
        {
            if (Phase != HeistPhase.Heist && Phase != HeistPhase.FloorComplete) return;
            int v = l.CurrentValue;
            Banked += v; RunTotal += v;
            OnBankedChanged?.Invoke(Banked);
            if (Phase == HeistPhase.Heist && Banked >= Quota)
                SetPhase(Floor >= TotalFloors - 1 ? HeistPhase.Won : HeistPhase.FloorComplete);
        }

        public void NotifyLootBroken(HeavyLoot l) { Lost += l.baseValue; }

        public void NotifyPlayerCaught(WobblyPlayer p)
        {
            if (Phase != HeistPhase.Heist) return;
            Caught++;
            p.Stumble(Vector3.up * 3f, 3f);
            if (Caught >= maxCaught) SetPhase(HeistPhase.Failed);
        }

        void SetPhase(HeistPhase p) { Phase = p; OnPhaseChanged?.Invoke(p); }
    }
}