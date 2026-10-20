using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The rules of the PVP room (the owner, 2026-10-17: "when players enter the room a health bar appears above their head and players can PVP but they only get briefly knocked out upon losing and then they get
    /// back up"). Inside the room, in the basement, a blow or a bullet from one player takes health off another; a player who is down to nothing is knocked out for a few seconds (nothing can hurt him
    /// meanwhile) and gets up with full health. Nothing happens to a player outside the room, and nobody outside it can hurt anybody. Pure numbers, so they can be tested.
    /// </summary>
    public static class PvpRules
    {
        public const float MaxHealth = 100f;
        /// <summary>A fist, and a bullet (three bullets knock a player out); a melee weapon takes the damage it does to the training dummy.</summary>
        public const float FistDamage = 8f, BulletDamage = 34f;
        /// <summary>How long a knocked-out player lies there (and cannot be hurt) before he gets up with full health.</summary>
        public const float KnockoutSeconds = 4f;
        /// <summary>The most one hit may claim (the host refuses a client who asks for more) and how far apart attacker and victim may be (m).</summary>
        public const float MaxClaim = 40f, MaxReach = 5f;
        /// <summary>A player who leaves the room is back to full health after this many seconds.</summary>
        public const float ResetOutsideSeconds = 3f;

        public static float After(float health, float damage) => Mathf.Max(0f, health - Mathf.Max(0f, damage));
        public static bool KnockedOut(float health) => health <= 0f;
        /// <summary>Can this blow count? Both inside the room, neither knocked out, and not the same player.</summary>
        public static bool CanHurt(bool attackerInside, bool victimInside, bool attackerDown, bool victimDown, bool samePlayer) => attackerInside && victimInside && !attackerDown && !victimDown && !samePlayer;
        /// <summary>Is a point inside a room (its inner rectangle, a little widened so that the doorway counts)?</summary>
        public static bool Inside(Rect room, float x, float z) => room.width > 0f && x >= room.xMin - 0.1f && x <= room.xMax + 0.1f && z >= room.yMin - 0.1f && z <= room.yMax + 0.1f;
    }

    /// <summary>
    /// A player's health in the PVP room (<see cref="PvpRules"/>). Added to every player. The host decides what a hit does in a networked run (<c>RunSync.RequestPvpHit</c>) and tells every machine the
    /// result, so everybody's bar is the same; offline the blow is applied at once. The knock-out is an ordinary stumble of the body (<see cref="WobblyPlayer.Stumble"/>).
    /// </summary>
    public sealed class PvpFighter : MonoBehaviour
    {
        public float Health { get; private set; } = PvpRules.MaxHealth;
        /// <summary>Is he lying knocked out right now (nothing hurts him)?</summary>
        public bool Down => Time.time < downUntil;

        float downUntil, outsideFor;
        WobblyPlayer player;
        static FloorGenerator gen;
        static float nextGenLookup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { gen = null; nextGenLookup = 0f; All.Clear(); }

        /// <summary>Every fighter (the health bars are drawn for those inside the room).</summary>
        public static readonly List<PvpFighter> All = new List<PvpFighter>();

        void Awake() { player = GetComponent<WobblyPlayer>(); }
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>The PVP room's inside, or an empty rectangle when there is no such room now (not in the basement).</summary>
        public static Rect Arena
        {
            get
            {
                var hm = HeistManager.Instance;
                if (!hm || hm.Floor != 0) return default;
                if (!gen && Time.unscaledTime >= nextGenLookup) { gen = FindFirstObjectByType<FloorGenerator>(); nextGenLookup = Time.unscaledTime + 1f; }
                return gen ? gen.PvpRoomArea : default;
            }
        }

        public bool Inside => PvpRules.Inside(Arena, transform.position.x, transform.position.z);

        void Update()
        {
            bool inside = Inside;
            if (!inside) { outsideFor += Time.deltaTime; if (outsideFor > PvpRules.ResetOutsideSeconds && !Down) Health = PvpRules.MaxHealth; }
            else outsideFor = 0f;
            if (Health <= 0f && !Down) Health = PvpRules.MaxHealth;                       // (he gets up with full health)
        }

        /// <summary>
        /// A blow or a bullet of this player landed on <paramref name="victim"/>: counts when both are in the room. Offline it is applied at once; in a networked run it is a request to the host (true = the
        /// blow connected, for the sound).
        /// </summary>
        public static bool TryStrike(WobblyPlayer attacker, WobblyPlayer victim, float damage)
        {
            if (!attacker || !victim) return false;
            var a = attacker.GetComponent<PvpFighter>(); var v = victim.GetComponent<PvpFighter>();
            if (!a || !v || !PvpRules.CanHurt(a.Inside, v.Inside, a.Down, v.Down, attacker == victim)) return false;
            if (Net.NetRole.Synced)
            {
                if (victim.TryGetComponent(out Net.NetPlayer np) && Net.RunSync.Instance != null) Net.RunSync.Instance.RequestPvpHit((byte)np.Slot, damage);
                return true;
            }
            v.Hurt(damage);
            return true;
        }

        /// <summary>The real effect of a hit (offline, or on the host): health off, and a knock-out when it is all gone.</summary>
        public void Hurt(float damage)
        {
            if (Down) return;
            Health = PvpRules.After(Health, damage);
            if (PvpRules.KnockedOut(Health))
            {
                downUntil = Time.time + PvpRules.KnockoutSeconds;
                if (player) player.Stumble(Vector3.zero, PvpRules.KnockoutSeconds);          // (a teammate's body is moved by its own machine: Stumble tells it)
            }
        }

        /// <summary>What the host says (every machine): this player's health, and whether he was just knocked out.</summary>
        public void SetState(float health, bool down)
        {
            Health = health;
            if (down) downUntil = Time.time + PvpRules.KnockoutSeconds;
        }
    }

    /// <summary>The health bar over the head of every player inside the PVP room, for everybody (HudTheme pieces only, drawn on Repaint only).</summary>
    public sealed class PvpHud : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<PvpHud>()) return;
            var go = new GameObject("PvpHud");
            DontDestroyOnLoad(go);
            go.AddComponent<PvpHud>();
        }

        /// <summary>How many bars were drawn the last time (for the tests).</summary>
        public static int LastBars;

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            LastBars = 0;
            var cam = Camera.main;
            if (!cam || IntroCinematic.Playing) return;
            float s = HudTheme.S;
            foreach (var f in PvpFighter.All)
            {
                if (!f || !f.Inside) continue;
                var sp = cam.WorldToScreenPoint(f.transform.position + Vector3.up * 1.7f);
                if (sp.z <= 0f) continue;
                float w = 96f * s, h = 12f * s;
                var r = new Rect(sp.x - w * 0.5f, Screen.height - sp.y - h, w, h);
                float frac = f.Health / PvpRules.MaxHealth;
                HudTheme.Bar(r, frac, f.Down ? HudTheme.Dim : frac < 0.34f ? HudTheme.Danger : frac < 0.67f ? HudTheme.Warning : HudTheme.Cash, 2f);
                LastBars++;
            }
        }
    }
}
