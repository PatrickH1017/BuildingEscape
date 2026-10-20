using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The pure rules of stamina (the owner, 2026-10-18: "There should be stamina with a stamina bar on the HUD"; the energy drink: "less stamina drain"). Sprinting drains it, walking and standing
    /// give it back after a short breather; run it dry and the player is winded until a quarter of it is back. Nothing here knows about Unity, so the numbers can be tested.
    /// Inspirations: the sprint bars of Skyrim and Dragonwilds (a bar that drains while running and fills when you stop).
    /// </summary>
    public sealed class StaminaMeter
    {
        public const float Max = 100f;
        /// <summary>Sprinting for about six seconds from full empties it; the bar is back to full about eight seconds after the player stops.</summary>
        public const float DrainPerSecond = 16f, RegenPerSecond = 12f;
        /// <summary>Seconds after the last sprint before it starts to fill again.</summary>
        public const float RegenDelaySeconds = 1f;
        /// <summary>Winded (no sprint) from running dry until this share of the bar is back.</summary>
        public const float RecoverFraction = 0.25f;

        public float Value { get; private set; } = Max;
        /// <summary>Ran dry: sprinting is off until <see cref="RecoverFraction"/> of the bar is back.</summary>
        public bool Exhausted { get; private set; }
        public float Fraction => Value / Max;
        public bool CanSprint => !Exhausted && Value > 0f;
        float sinceUse = RegenDelaySeconds;

        /// <summary>Advance by <paramref name="dt"/> seconds; <paramref name="drainMultiplier"/> is below 1 with an energy drink.</summary>
        public void Step(float dt, bool sprinting, float drainMultiplier = 1f)
        {
            dt = Mathf.Max(0f, dt);
            if (sprinting && CanSprint)
            {
                Value = Mathf.Max(0f, Value - DrainPerSecond * Mathf.Max(0f, drainMultiplier) * dt);
                sinceUse = 0f;
                if (Value <= 0f) Exhausted = true;
                return;
            }
            sinceUse += dt;
            if (sinceUse < RegenDelaySeconds) return;
            Value = Mathf.Min(Max, Value + RegenPerSecond * dt);
            if (Exhausted && Value >= Max * RecoverFraction) Exhausted = false;
        }

        /// <summary>Full again at once (a new floor, a test).</summary>
        public void Refill() { Value = Max; Exhausted = false; sinceUse = RegenDelaySeconds; }
    }

    /// <summary>
    /// A player's stamina. Added to every player by <see cref="WobblyPlayer"/>; only the player this machine moves spends it (like the movement itself).
    /// <see cref="DrainMultiplier"/> is what a buff (the energy drink) turns down.
    /// </summary>
    public class Stamina : MonoBehaviour
    {
        public StaminaMeter Meter { get; } = new StaminaMeter();
        /// <summary>1 = normal; the energy drink sets it below 1 for a while.</summary>
        public float DrainMultiplier { get; set; } = 1f;

        public bool CanSprint => Meter.CanSprint;
        public void Step(float dt, bool sprinting) => Meter.Step(dt, sprinting, DrainMultiplier);
    }

    /// <summary>The stamina bar: bottom centre, for the player the camera follows. Drawn with the HUD's own pieces only (<see cref="HudTheme"/>), on Repaint only.</summary>
    public class StaminaHud : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<StaminaHud>()) return;
            var go = new GameObject("StaminaHud");
            DontDestroyOnLoad(go);
            go.AddComponent<StaminaHud>();
        }

        /// <summary>What the bar showed last (for the tests): its fill, whether it was drawn, and whether the player was winded.</summary>
        public static float LastFraction = -1f;
        public static bool LastWinded;

        Stamina mine;
        float nextLookup;

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!HeistManager.Instance || IntroCinematic.Playing) return;
            if (!mine && Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + 0.5f;
                foreach (var p in WobblyPlayerRegistry.All)
                {
                    if (!p) continue;
                    var pack = p.GetComponent<Backpack>();
                    if (pack && pack.Viewed && p.TryGetComponent(out Stamina st)) { mine = st; break; }
                }
            }
            if (!mine) return;
            var pk = mine.GetComponent<Backpack>();
            if (pk && !pk.Viewed) { mine = null; return; }                         // (the camera follows somebody else now)
            var m = mine.Meter;
            LastFraction = m.Fraction; LastWinded = m.Exhausted;

            float s = HudTheme.S;
            float w = 320f * s, h = 16f * s;
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height - 14f * s - h, w, h);
            Color fill = m.Exhausted ? HudTheme.Danger : m.Fraction < 0.3f ? HudTheme.Warning : HudTheme.Cash;
            HudTheme.Bar(r, m.Fraction, fill);
            HudTheme.Text(new Rect(r.x, r.y - 22f * s, r.width, 20f * s), m.Exhausted ? "WINDED" : "STAMINA", 14f, m.Exhausted ? HudTheme.Danger : HudTheme.Muted, TextAnchor.MiddleCenter);
        }
    }
}
