using System;

namespace BuildingEscape
{
    /// <summary>The consumables of the vending machines (the owner, 2026-10-25).</summary>
    public enum ConsumableKind { SpeedBurst, EnduranceLaster, SilentSteps, Invisible, AccuracyCola }

    /// <summary>What a player is told who tries to use a consumable (<see cref="BuffSet.Check"/>).</summary>
    public enum UseCheck { Ok, OnCooldown, UsedThisRound }

    /// <summary>
    /// The consumables the vending machines sell (the owner, 2026-10-25: "please implement these consumables that will be sold from vending machines": speed burst - limit of 3 in the inventory, lasts 10 seconds, 1 minute cooldown; endurance laster - limit of 3, slows stamina
    /// drain for 1 minute, 30 second cooldown; silent steps - limit of 2, steps are silent for 15 seconds, 1 minute cooldown; invisible - limit of 1, lasts 10 seconds, can be used once a round; accuracy cola - "increasing your accuracy", no numbers given), without Unity types so the numbers
    /// and the timers are tested outside Unity. THE OWNER'S NUMBERS are the limits, durations and cooldowns of the first four; everything marked PLACEHOLDER is mine (the strength of each effect, every price and weight, and all the numbers of the accuracy cola) for him to set.
    /// </summary>
    public static class ConsumableRules
    {
        public const int Count = 5;

        public struct Def
        {
            public string name;
            /// <summary>What the machine asks for it (PLACEHOLDER) and what it weighs in the pack (PLACEHOLDER; the pack holds 20).</summary>
            public int price;
            public float weight;
            /// <summary>The most of this kind a pack may hold (the owner's "limit of n in inventory").</summary>
            public int limit;
            /// <summary>How long the effect lasts, and how long after it ends the next one may be used (the owner's "cooldown"; see <see cref="CooldownFromEnd"/>).</summary>
            public float effectSeconds, cooldownSeconds;
            /// <summary>"Can be used once a round" (invisible): one use for every floor the party enters.</summary>
            public bool oncePerRound;
            /// <summary>How strong the effect is (PLACEHOLDER): the speed burst's walking speed, the endurance laster's stamina drain and the accuracy cola's spread are multiplied by it. Unused by silent steps (0 = no sound) and invisible.</summary>
            public float factor;
            /// <summary>The effect in a few plain words, for the machine's line (PLACEHOLDER words made of the owner's own).</summary>
            public string effectText;
        }

        /// <summary>
        /// The cooldown starts when the effect ends (true: the owner's numbers read that way - the endurance laster lasts 60 s and its cooldown is 30 s, so a cooldown counted from the use would let a second one start while the first still runs) or at the use (false).
        /// ASSUMPTION: ask the owner.
        /// </summary>
        public const bool CooldownFromEnd = true;

        public static readonly Def[] Defs =
        {
            new Def { name = "Speed Burst", price = 200, weight = 0.3f, limit = 3, effectSeconds = 10f, cooldownSeconds = 60f, factor = 1.5f, effectText = "run faster" },
            new Def { name = "Endurance Laster", price = 150, weight = 0.3f, limit = 3, effectSeconds = 60f, cooldownSeconds = 30f, factor = 0.5f, effectText = "slows stamina drain" },
            new Def { name = "Silent Steps", price = 300, weight = 0.3f, limit = 2, effectSeconds = 15f, cooldownSeconds = 60f, factor = 0f, effectText = "silent steps" },
            new Def { name = "Invisible", price = 800, weight = 0.3f, limit = 1, effectSeconds = 10f, cooldownSeconds = 0f, oncePerRound = true, factor = 0f, effectText = "invisible to guards" },
            new Def { name = "Accuracy Cola", price = 250, weight = 0.3f, limit = 3, effectSeconds = 30f, cooldownSeconds = 60f, factor = 0.5f, effectText = "better accuracy" },
        };

        public static Def Of(ConsumableKind kind) => Defs[(int)kind];

        /// <summary>The machine's line for a consumable: what it does, for how long, when it can be used again and how many a pack holds (all from <see cref="Defs"/>).</summary>
        public static string Blurb(ConsumableKind kind)
        {
            var d = Of(kind);
            string when = d.oncePerRound ? "once a round" : "cooldown " + Seconds(d.cooldownSeconds);
            return d.effectText + " for " + Seconds(d.effectSeconds) + ", " + when + ", carry up to " + d.limit;
        }

        /// <summary>"10 s", "1 min", "90 s": whole minutes are written as minutes.</summary>
        public static string Seconds(float s)
        {
            int n = (int)Math.Round(s);
            return n >= 60 && n % 60 == 0 ? n / 60 + " min" : n + " s";
        }

        /// <summary>The words for a refused purchase and a refused use (PLACEHOLDERS: the owner writes the words).</summary>
        public static string AtLimit(ConsumableKind kind) => "You can only carry " + Of(kind).limit + " " + Of(kind).name;
        public static string NoneInPack(ConsumableKind kind) => "You have no " + Of(kind).name;
        public static string Refusal(ConsumableKind kind, UseCheck check, float cooldownLeft) =>
            check == UseCheck.UsedThisRound ? Of(kind).name + " was used this round" : Of(kind).name + " is cooling down (" + Seconds((float)Math.Ceiling(cooldownLeft)) + ")";
        public static string Started(ConsumableKind kind) => Of(kind).name + ": " + Seconds(Of(kind).effectSeconds);
    }

    /// <summary>
    /// One player's consumables in use (pure; time and the round are given): which effect runs until when, when each kind may be used again and in which round it was used last. Every machine keeps one for every player, started by the same announcement.
    /// </summary>
    public sealed class BuffSet
    {
        readonly float[] until = new float[ConsumableRules.Count];
        readonly float[] readyAt = new float[ConsumableRules.Count];
        readonly int[] roundUsed = new int[ConsumableRules.Count];

        public BuffSet() { Clear(); }

        public void Clear()
        {
            for (int i = 0; i < ConsumableRules.Count; i++) { until[i] = 0f; readyAt[i] = 0f; roundUsed[i] = int.MinValue; }
        }

        /// <summary>May this kind be used now (<paramref name="round"/> is the floor visit the party is in)?</summary>
        public UseCheck Check(ConsumableKind kind, float now, int round)
        {
            int i = (int)kind;
            if (ConsumableRules.Defs[i].oncePerRound && roundUsed[i] == round) return UseCheck.UsedThisRound;
            return now < readyAt[i] ? UseCheck.OnCooldown : UseCheck.Ok;
        }

        /// <summary>Starts the effect (the caller checked <see cref="Check"/>); an effect still running starts over (it does not stack).</summary>
        public void Start(ConsumableKind kind, float now, int round)
        {
            int i = (int)kind;
            var d = ConsumableRules.Defs[i];
            until[i] = now + d.effectSeconds;
            readyAt[i] = ConsumableRules.CooldownFromEnd ? until[i] + d.cooldownSeconds : now + Math.Max(d.cooldownSeconds, 0f);
            roundUsed[i] = round;
        }

        public bool Active(ConsumableKind kind, float now) => now < until[(int)kind];
        public float EffectLeft(ConsumableKind kind, float now) => Math.Max(0f, until[(int)kind] - now);
        public float CooldownLeft(ConsumableKind kind, float now) => Math.Max(0f, readyAt[(int)kind] - now);

        /// <summary>What the effects do now: the walking speed, the stamina drain and the gun's spread are multiplied by these; the steps and the guards' eyes are on or off.</summary>
        public float SpeedFactor(float now) => Active(ConsumableKind.SpeedBurst, now) ? ConsumableRules.Of(ConsumableKind.SpeedBurst).factor : 1f;
        public float DrainFactor(float now) => Active(ConsumableKind.EnduranceLaster, now) ? ConsumableRules.Of(ConsumableKind.EnduranceLaster).factor : 1f;
        public float SpreadFactor(float now) => Active(ConsumableKind.AccuracyCola, now) ? ConsumableRules.Of(ConsumableKind.AccuracyCola).factor : 1f;
        public bool StepsSilent(float now) => Active(ConsumableKind.SilentSteps, now);
        public bool Invisible(float now) => Active(ConsumableKind.Invisible, now);
    }
}
