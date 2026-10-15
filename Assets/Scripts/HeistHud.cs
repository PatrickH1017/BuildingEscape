using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Placeholder IMGUI HUD: floor, quota, cash, custody count, controls and the end screens.</summary>
    public class HeistHud : MonoBehaviour
    {
        void OnGUI()
        {
            var m = HeistManager.Instance;
            if (!m) return;
            // The text only draws on Repaint (HudText skips the other events anyway, but the strings below are not free);
            // the end-screen button needs every event.
            if (Event.current.type == EventType.Repaint) DrawLabels(m);
            if (m.Phase == HeistPhase.Won || m.Phase == HeistPhase.Failed) DrawEndButton();
        }

        static void DrawLabels(HeistManager m)
        {
            var style = HudStyles.Label(18);
            bool hub = m.Phase == HeistPhase.Hub;

            if (hub)
            {
                var tm0 = TowerManager.Instance;
                int nextQuota = TowerRules.QuotaFor(tm0 ? tm0.ResumeLevel : 1);
                string nextInfo = nextQuota > 0 ? $"   Next quota ${nextQuota}" : "";
                HudText.Label(new Rect(12, 8, 900, 28), $"{m.FloorLabel}   Cash ${m.Cash}{nextInfo}   Run total ${m.RunTotal}", style);
            }
            else if (TowerRules.IsBoss(m.Floor))
            {
                HudText.Label(new Rect(12, 8, 900, 28),
                    $"{m.FloorLabel} ({m.Floor + 1}/{m.TotalFloors})   Knock out the boss to escape   Cash ${m.Cash}", style);
                HudText.Label(new Rect(12, 34, 900, 28), $"{CustodyLine(m)}   Run total ${m.RunTotal}", style);
            }
            else
            {
                // Quota: loot value you have dropped in the elevator so far / the quota total. Cash: your spendable money.
                HudText.Label(new Rect(12, 8, 900, 28),
                    $"{m.FloorLabel} ({m.Floor + 1}/{m.TotalFloors})   Quota ${m.Banked} / ${m.Quota}   Cash ${m.Cash}", style);
                HudText.Label(new Rect(12, 34, 900, 28),
                    $"Lost ${m.Lost}   {CustodyLine(m)}   Run total ${m.RunTotal}", style);
            }
            float ctrlW = Screen.width - 24f;
            HudText.Label(new Rect(12, Screen.height - 56, ctrlW, 28),
                "WASD move   Mouse look   Hold Alt (or V) look around   Shift sprint   Space jump   E pick up   Tab inventory   Q grab/drop   LMB throw/swing weapon", style);
            HudText.Label(new Rect(12, Screen.height - 30, ctrlW, 28),
                "H hide   F elevator panel / sell (loading docks)   Drop loot in the elevator to bank it   M mute   Esc cursor", style);

            var big = HudStyles.Label(40, TextAnchor.MiddleCenter);
            var sub = HudStyles.Label(22, TextAnchor.MiddleCenter);
            if (m.Phase == HeistPhase.FloorComplete)
            {
                bool groupEnd = TowerRules.IsGroupEnd(m.Floor);
                string msg = groupEnd
                    ? "Quota met - return to the Loading Docks (elevator panel: choose a floor, then GO)"
                    : "Quota met - use the elevator panel to go up (choose a floor, then GO)";
                HudText.Label(new Rect(0, 80, Screen.width, 60), msg, big);
                HudText.Label(new Rect(0, 135, Screen.width, 32), $"Surplus ${m.Surplus} over the quota becomes your cash to spend at the vendor", sub);
            }
            else if (hub)
            {
                var tm = TowerManager.Instance;
                int next = tm ? tm.ResumeLevel : 1;
                string nextLabel = TowerRules.Label(next);
                string msg = ElevatorExtraction.EveryoneAboard
                    ? $"Use the elevator panel (F): choose {nextLabel}, then GO (finished floors are closed)"
                    : $"Loading Docks - everyone into the elevator, then use the panel (F) to go up to {nextLabel}";
                HudText.Label(new Rect(0, 80, Screen.width, 60), msg, big);
                if (m.SurplusPaid > 0)
                    HudText.Label(new Rect(0, 135, Screen.width, 32), $"+${m.SurplusPaid} surplus from the last floor added to your cash", sub);
            }
            else if (m.Phase == HeistPhase.Won || m.Phase == HeistPhase.Failed)
            {
                string msg = m.Phase == HeistPhase.Won ? "ESCAPED THE TOWER" : (m.BackupCalled ? $"BACKUP CALLED - BUSTED on {m.FloorLabel}" : $"BUSTED on {m.FloorLabel}");
                HudText.Label(new Rect(0, 0, Screen.width, Screen.height), msg, big);
            }
        }

        /// <summary>The way out of the end screen: back to the menu online, restart the scene offline.</summary>
        static void DrawEndButton()
        {
            bool online = Net.NetSession.Online;
            if (!GUI.Button(new Rect(Screen.width / 2 - 70, Screen.height / 2 + 50, 140, 36), online ? "Back to menu" : "Restart")) return;
            if (online) Net.NetSession.Instance.Leave();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>"Free 2  Pinned 1  Busted 0": how the team stands with the police.</summary>
        static string CustodyLine(HeistManager m)
        {
            m.CountCustody(out int free, out int pinned, out int busted);
            return $"Free {free}   Pinned {pinned}   Busted {busted}";
        }
    }
}
