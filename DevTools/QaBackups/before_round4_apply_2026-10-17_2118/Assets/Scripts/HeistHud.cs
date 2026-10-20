using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The in-run HUD in the toon style: the status card (cash, floor, quota, with the lost / run total / team chips under it), the banner for
    /// the moments that matter and the way out of the end screen. It only reads the game's numbers and hands them to <see cref="HudWidgets"/>,
    /// which draws them (the look itself lives in <see cref="HudTheme"/>). There are no key hints on screen: they are in the Keybinds screen
    /// (<see cref="KeybindScreen"/>, from Settings and the Esc menu).
    /// </summary>
    public class HeistHud : MonoBehaviour
    {
        int lastCash, lastBanked;
        bool primed;

        void OnGUI()
        {
            var m = HeistManager.Instance;
            if (!m) return;
            // everything draws only on Repaint (the strings below are not free); the end-screen button needs every event
            if (Event.current.type == EventType.Repaint) Draw(m);
            if (m.Phase == HeistPhase.Won || m.Phase == HeistPhase.Failed) DrawEndButton();
            else endSince = -1f;
        }

        void Draw(HeistManager m)
        {
            bool hub = m.Phase == HeistPhase.Hub;
            bool boss = !hub && TowerRules.IsBoss(m.Floor);
            var tm = TowerManager.Instance;
            int next = tm ? tm.ResumeLevel : 1;
            m.CountCustody(out int free, out int pinned, out int busted);

            // money that came in: a "+$250" floats up beside the card
            if (primed)
            {
                if (m.Cash > lastCash) HudAnim.Float("+" + HudTheme.Money(m.Cash - lastCash), HudTheme.Cash);
                if (m.Banked > lastBanked && !hub) HudAnim.Float("+" + HudTheme.Money(m.Banked - lastBanked), HudTheme.Quota);
            }
            primed = true;
            lastCash = m.Cash;
            lastBanked = m.Banked;

            HudWidgets.StatusCard(new HudWidgets.StatusModel
            {
                place = m.FloorLabel,
                level = m.Floor,
                stops = TowerRules.DemoStops(m.TotalFloors),          // ("FLOOR 1/4" while the tower is a demo)
                docks = hub,
                boss = boss,
                met = m.Phase == HeistPhase.FloorComplete,
                cash = m.Cash,
                banked = m.Banked,
                quota = m.Quota,
                lost = m.Lost,
                runTotal = m.RunTotal,
                demoDone = hub && TowerRules.DemoOver(next),
                nextQuota = hub && !TowerRules.DemoOver(next) ? TowerRules.QuotaFor(next) : 0,
                free = free,
                pinned = pinned,
                busted = busted,
            });
            DrawBanner(m, hub, next);
        }

        /// <summary>The ribbon for the moments that matter: quota met, the docks, the end screens.</summary>
        static void DrawBanner(HeistManager m, bool hub, int next)
        {
            if (m.Phase == HeistPhase.FloorComplete)
            {
                bool groupEnd = TowerRules.IsGroupEnd(m.Floor), demoEnd = TowerRules.IsDemoEnd(m.Floor);
                HudWidgets.Banner("QUOTA MET!",
                    demoEnd ? "That was the last floor of the demo: return to the Loading Docks (elevator panel: choose a floor, then GO)"
                    : groupEnd ? "Return to the Loading Docks (elevator panel: choose a floor, then GO)" : "Use the elevator panel to go up (choose a floor, then GO)",
                    HudTheme.Cash, $"Surplus {HudTheme.Money(m.Surplus)} over the quota becomes your cash to spend at the vendor");
            }
            else if (hub && TowerRules.DemoOver(next))
            {
                // the demo's last floor is done: the vendor has a free pew now (Vendor.PewForSale)
                HudWidgets.Banner("DEMO COMPLETE", "That was the last floor of the demo. The vendor has a pew pistol for free: try it on the training dummy", HudTheme.Cash,
                    m.SurplusPaid > 0 ? $"+{HudTheme.Money(m.SurplusPaid)} surplus from the last floor added to your cash" : null);
            }
            else if (hub)
            {
                string nextLabel = TowerRules.Label(next);
                string msg = ElevatorExtraction.EveryoneAboard
                    ? $"Use the elevator panel (F): choose {nextLabel}, then GO (finished floors are closed)"
                    : $"Everyone into the elevator, then use the panel (F) to go up to {nextLabel}";
                HudWidgets.Banner("LOADING DOCKS", msg, HudTheme.Info,
                    m.SurplusPaid > 0 ? $"+{HudTheme.Money(m.SurplusPaid)} surplus from the last floor added to your cash" : null);
            }
            else if (m.Phase == HeistPhase.Won) HudWidgets.Banner("ESCAPED THE TOWER", null, HudTheme.Cash);
            else if (m.Phase == HeistPhase.Failed)
                HudWidgets.Banner("BUSTED", m.BackupCalled ? $"Backup was called - busted on {m.FloorLabel}" : $"Busted on {m.FloorLabel}", HudTheme.Danger);
        }

        /// <summary>When the end screen came up (unscaled time; -1 while it is not up): the button waits a beat, so a player hammering the attack button as the team goes down does not hit it.</summary>
        static float endSince = -1f;
        public const float EndButtonDelay = 1.2f;

        /// <summary>
        /// Where the end screen's button is: up under the banner, nowhere near the middle of the screen, where the cursor appears (and where a player who is spamming the attack button
        /// keeps clicking) when the team goes down. The owner, 2026-10-17: "the exit button after wiping in a run should be higher so you don't click it on accident".
        /// </summary>
        public static Rect EndButtonRect(float screenW, float screenH, float s) => new Rect((screenW - 210f * s) * 0.5f, Mathf.Max(150f * s, screenH * 0.14f), 210f * s, 52f * s);

        /// <summary>The way out of the end screen: back to the menu online, restart the scene offline.</summary>
        static void DrawEndButton()
        {
            if (endSince < 0f) endSince = Time.unscaledTime;
            if (Time.unscaledTime - endSince < EndButtonDelay) return;
            bool online = Net.NetSession.Online;
            float s = HudTheme.S;
            var r = EndButtonRect(Screen.width, Screen.height, s);
            if (!HudWidgets.Button(r, online ? "Back to menu" : "Restart", HudTheme.Info, 22f)) return;
            if (online) Net.NetSession.Instance.Leave();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
