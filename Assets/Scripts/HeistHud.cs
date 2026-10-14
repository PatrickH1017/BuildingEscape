using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Placeholder IMGUI HUD: floor, quota, timer, cash, caught count, controls, vendor shop, end screens.</summary>
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
                GUI.Label(new Rect(12, 8, 900, 28), $"{m.FloorLabel} - SAFE ZONE   Cash ${m.Cash}   Run total ${m.RunTotal}", style);
            }
            else
            {
                GUI.Label(new Rect(12, 8, 900, 28),
                    $"{m.FloorLabel} ({m.Floor + 1}/{m.TotalFloors})   Banked ${m.Banked} / ${m.Quota}   Lost ${m.Lost}   Caught {m.Caught}/{m.maxCaught}", style);
                int t = Mathf.CeilToInt(Mathf.Max(0, m.TimeLeft));
                GUI.Label(new Rect(12, 34, 600, 28), $"Time {t / 60}:{t % 60:00}   Cash ${m.Cash}   Run total ${m.RunTotal}", style);
            }
            GUI.Label(new Rect(12, Screen.height - 30, 1400, 28),
                "WASD move  Mouse look  Hold Alt (or V) look around  Shift sprint  Space jump  E pick up  Tab inventory  Q grab/drop  LMB throw/swing weapon  H hide  F sell (basement)  drop loot in the elevator to bank it  G elevator (basement)  1-7 buy  Esc cursor", style);

            var big = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            if (m.Phase == HeistPhase.FloorComplete)
            {
                string msg = ElevatorExtraction.EveryoneAboard
                    ? "Quota met - doors closing, heading down to the basement..."
                    : "Quota met - get everyone into the elevator to head down to the basement";
                GUI.Label(new Rect(0, 80, Screen.width, 60), msg, big);
                GUI.Label(new Rect(0, 135, Screen.width, 32), $"Surplus ${m.Surplus} over the quota becomes your cash to spend at the vendor",
                    new GUIStyle(style) { alignment = TextAnchor.MiddleCenter, fontSize = 22 });
            }
            else if (hub)
            {
                var tm = TowerManager.Instance;
                int next = tm ? tm.ResumeLevel : 1;
                string nextLabel = next <= 0 ? "the Basement" : $"Floor {next}";
                string msg = ElevatorExtraction.EveryoneAboard
                    ? $"Press G to ride up to {nextLabel} (finished floors are closed)"
                    : $"Safe zone - everyone into the elevator, then G to go up to {nextLabel}";
                GUI.Label(new Rect(0, 80, Screen.width, 60), msg, big);
                if (m.SurplusPaid > 0)
                    GUI.Label(new Rect(0, 135, Screen.width, 32), $"+${m.SurplusPaid} surplus from the last floor added to your cash",
                        new GUIStyle(style) { alignment = TextAnchor.MiddleCenter, fontSize = 22 });
            }
            else if (m.Phase == HeistPhase.Won || m.Phase == HeistPhase.Failed)
            {
                string msg = m.Phase == HeistPhase.Won ? "ESCAPED THE TOWER" : (m.BackupCalled ? $"BACKUP CALLED - BUSTED on {m.FloorLabel}" : $"BUSTED on {m.FloorLabel}");
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), msg, big);
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
            GUI.Label(new Rect(r.x + 12f, y, w - 24f, row),
                v.LootCount > 0
                    ? $"Hot goods: {v.LootCount} item(s). I'll give you ${v.SellValue}, cash, no receipts  -  press F"
                    : "Got something hot? Put it on the counter (or keep it in your pack) and press F.", style);
            y += row + 4f;

            for (int i = 0; i < Vendor.Catalog.Length; i++)
            {
                var p = Vendor.Catalog[i];
                string note = m.Cash >= p.price ? "" : "   (not enough cash)";
                GUI.Label(new Rect(r.x + 12f, y, w - 24f, row), $"[{i + 1}] {p.name}  ${p.price}  -  {p.blurb}{note}", style);
                y += row;
            }
        }
    }
}
