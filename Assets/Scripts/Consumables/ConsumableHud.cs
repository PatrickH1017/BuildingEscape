using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The consumables on the screen (the owner, 2026-10-25): a row of chips above the weapon bar - "[7] Speed Burst x3", with the seconds left while it runs (green) or while it cools down (orange) - for the kinds the player carries or has in use. For the player the camera follows,
    /// on Repaint only, drawn with the HUD's own pieces (a plain first version: the owner polishes the look).
    /// </summary>
    public sealed class ConsumableHud : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<ConsumableHud>()) return;
            var go = new GameObject("ConsumableHud");
            DontDestroyOnLoad(go);
            go.AddComponent<ConsumableHud>();
        }

        /// <summary>What the row showed last (for the tests): the number of chips.</summary>
        public static int LastChips = -1;

        Backpack mine;
        PlayerBuffs buffs;
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
                    if (pack && pack.Viewed && p.TryGetComponent(out buffs)) { mine = pack; break; }
                }
            }
            if (!mine || !buffs) return;
            if (!mine.Viewed) { mine = null; buffs = null; return; }                       // (the camera follows somebody else now)

            float s = HudTheme.S, h = 28f * s, gap = 8f * s;
            float now = Time.time;
            int n = 0;
            var texts = new string[ConsumableRules.Count];
            var fills = new Color[ConsumableRules.Count];
            float total = 0f;
            for (int k = 0; k < ConsumableRules.Count; k++)
            {
                var kind = (ConsumableKind)k;
                int count = mine.CountConsumable(kind);
                float running = buffs.Set.EffectLeft(kind, now), cooling = buffs.Set.CooldownLeft(kind, now);
                if (count == 0 && running <= 0f && cooling <= 0f) continue;
                string text = "[" + ConsumableItem.KeyNames[k] + "] " + ConsumableRules.Of(kind).name + " x" + count;
                Color fill = HudTheme.Info;
                if (running > 0f) { text += "  " + Mathf.CeilToInt(running) + " s"; fill = HudTheme.Good; }
                else if (cooling > 0f) { text += "  wait " + Mathf.CeilToInt(cooling) + " s"; fill = HudTheme.Warning; }
                texts[n] = text; fills[n] = fill;
                total += HudTheme.Width(text, 14f) + h * 0.9f + gap;
                n++;
            }
            LastChips = n;
            if (n == 0) return;
            total -= gap;
            var bar = HudWidgets.WeaponBarSlot(0);
            float x = (Screen.width - total) * 0.5f, y = bar.y - h - 8f * s;
            for (int i = 0; i < n; i++) x += HudWidgets.ChipAt(x, y, h, texts[i], fills[i], 14f) + gap;
        }
    }
}
