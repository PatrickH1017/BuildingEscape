using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The consumables a player has in use (the owner, 2026-10-25; <see cref="ConsumableRules"/>, <see cref="BuffSet"/>). Added to every player (<see cref="WobblyPlayer"/>); EVERY machine keeps one for every player, started by the same announcement (the host's decision in a networked run,
    /// <c>RunSync.HostUseConsumable</c>): the machine that moves the body reads the speed and the stamina drain, the machine that simulates the guards (the host, or offline) reads the invisibility and the footsteps, a shooter's machine and the host both read the spread.
    /// Each frame it hands the numbers to the places that use them: <see cref="WobblyPlayer.SpeedBuff"/> / <see cref="WobblyPlayer.StepsSilent"/>, <see cref="Stamina.DrainMultiplier"/>; the guards ask <see cref="IsInvisible"/> and the guns <see cref="SpreadFactor"/>.
    /// </summary>
    public sealed class PlayerBuffs : MonoBehaviour
    {
        public BuffSet Set { get; } = new BuffSet();

        WobblyPlayer wobbly;
        Stamina stamina;
        bool drainApplied;

        void Awake()
        {
            TryGetComponent(out wobbly);
            TryGetComponent(out stamina);
        }

        void Update()
        {
            float now = Time.time;
            if (wobbly) { wobbly.SpeedBuff = Set.SpeedFactor(now); wobbly.StepsSilent = Set.StepsSilent(now); }
            if (stamina)
            {
                float drain = Set.DrainFactor(now);
                if (drain != 1f) { stamina.DrainMultiplier = drain; drainApplied = true; }          // (only while the endurance laster runs: the factor is not touched otherwise)
                else if (drainApplied) { stamina.DrainMultiplier = 1f; drainApplied = false; }
            }
        }

        void OnDisable()
        {
            if (wobbly) { wobbly.SpeedBuff = 1f; wobbly.StepsSilent = false; }
            if (stamina && drainApplied) { stamina.DrainMultiplier = 1f; drainApplied = false; }
        }

        /// <summary>The floor visit the party is in (a consumable "once a round" is used once for each).</summary>
        public static int Round => HeistManager.Instance ? HeistManager.Instance.Round : 0;

        public UseCheck Check(ConsumableKind kind) => Set.Check(kind, Time.time, Round);

        /// <summary>The effect starts here (every machine does this for the player who used one; the item is gone already).</summary>
        public void StartLocal(ConsumableKind kind)
        {
            Set.Start(kind, Time.time, Round);
            if (TryGetComponent(out Backpack pack) && pack.Viewed) Backpack.NoticeToLocal(ConsumableRules.Started(kind));
        }

        /// <summary>Does a guard see nothing of this player (invisible)? The one question <see cref="GuardAI"/> asks before any other.</summary>
        public static bool IsInvisible(WobblyPlayer p) => p != null && p.TryGetComponent(out PlayerBuffs b) && b.Set.Invisible(Time.time);

        /// <summary>The factor a gun's spread is multiplied by for this shooter (1 = none): the accuracy cola's.</summary>
        public static float SpreadFactor(GameObject player) => player != null && player.TryGetComponent(out PlayerBuffs b) ? b.Set.SpreadFactor(Time.time) : 1f;
    }
}
