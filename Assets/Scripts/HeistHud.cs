using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Placeholder IMGUI HUD: floor, quota, cash, caught count, controls, vendor shop, end screens.</summary>
    public class HeistHud : MonoBehaviour
    {
        void OnGUI()
        {
            var m = HeistManager.Instance;
            if (!m) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
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
                HudText.Label(new Rect(12, 34, 900, 28), $"Caught {m.Caught}/{m.maxCaught}   Run total ${m.RunTotal}", style);
            }
            else
            {
                // Quota: loot value you have dropped in the elevator so far / the quota total. Cash: your spendable money.
                HudText.Label(new Rect(12, 8, 900, 28),
                    $"{m.FloorLabel} ({m.Floor + 1}/{m.TotalFloors})   Quota ${m.Banked} / ${m.Quota}   Cash ${m.Cash}", style);
                HudText.Label(new Rect(12, 34, 900, 28),
                    $"Lost ${m.Lost}   Caught {m.Caught}/{m.maxCaught}   Run total ${m.RunTotal}", style);
            }
            float ctrlW = Screen.width - 24f;
            HudText.Label(new Rect(12, Screen.height - 56, ctrlW, 28),
                "WASD move   Mouse look   Hold Alt (or V) look around   Shift sprint   Space jump   E pick up   Tab inventory   Q grab/drop   LMB throw/swing weapon", style);
            HudText.Label(new Rect(12, Screen.height - 30, ctrlW, 28),
                "H hide   F elevator panel / sell (loading docks)   Drop loot in the elevator to bank it   M mute   Esc cursor", style);

            var big = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            if (m.Phase == HeistPhase.FloorComplete)
            {
                bool groupEnd = TowerRules.IsGroupEnd(m.Floor);
                string msg = groupEnd
                    ? "Quota met - return to the Loading Docks (elevator panel: choose a floor, then GO)"
                    : "Quota met - use the elevator panel to go up (choose a floor, then GO)";
                HudText.Label(new Rect(0, 80, Screen.width, 60), msg, big);
                HudText.Label(new Rect(0, 135, Screen.width, 32), $"Surplus ${m.Surplus} over the quota becomes your cash to spend at the vendor",
                    new GUIStyle(style) { alignment = TextAnchor.MiddleCenter, fontSize = 22 });
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
                    HudText.Label(new Rect(0, 135, Screen.width, 32), $"+${m.SurplusPaid} surplus from the last floor added to your cash",
                        new GUIStyle(style) { alignment = TextAnchor.MiddleCenter, fontSize = 22 });
            }
            else if (m.Phase == HeistPhase.Won || m.Phase == HeistPhase.Failed)
            {
                string msg = m.Phase == HeistPhase.Won ? "ESCAPED THE TOWER" : (m.BackupCalled ? $"BACKUP CALLED - BUSTED on {m.FloorLabel}" : $"BUSTED on {m.FloorLabel}");
                HudText.Label(new Rect(0, 0, Screen.width, Screen.height), msg, big);
                bool online = Net.NetSession.Online;
                if (GUI.Button(new Rect(Screen.width / 2 - 70, Screen.height / 2 + 50, 140, 36), online ? "Back to menu" : "Restart"))
                {
                    if (online) Net.NetSession.Instance.Leave();
                    else UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                }
            }

        }

        static void DrawShop(HeistManager m, GUIStyle style)
        {
            var v = Vendor.Nearby;
            if (!v) return;

            const float w = 700f, row = 28f;
            float h = 40f + row * (Vendor.Catalog.Length + 2);
            var r = new Rect(Screen.width - w - 16f, 150f, w, h);
            GUI.Box(r, "BLACK MARKET  -  no questions asked");

            float y = r.y + 30f;
            HudText.Label(new Rect(r.x + 12f, y, w - 24f, row),
                v.LootCount > 0
                    ? $"Hot goods: {v.LootCount} item(s). I'll give you ${v.SellValue}, cash, no receipts  -  press F"
                    : "Got something hot? Put it on the counter (or keep it in your pack) and press F.", style);
            y += row + 4f;

            for (int i = 0; i < Vendor.Catalog.Length; i++)
            {
                var p = Vendor.Catalog[i];
                string note = m.Cash >= p.price ? "" : "   (not enough cash)";
                HudText.Label(new Rect(r.x + 12f, y, w - 24f, row), $"[{i + 1}] {p.name}  ${p.price}  -  {p.blurb}{note}", style);
                y += row;
            }
        }
    }
}
