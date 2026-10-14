using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Placeholder IMGUI HUD: floor, quota, timer, caught count, controls, end screens.</summary>
    public class HeistHud : MonoBehaviour
    {
        void OnGUI()
        {
            var m = HeistManager.Instance;
            if (!m) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(12, 8, 900, 28),
                $"{m.FloorLabel} ({m.Floor + 1}/{m.TotalFloors})   Banked ${m.Banked} / ${m.Quota}   Lost ${m.Lost}   Caught {m.Caught}/{m.maxCaught}", style);
            int t = Mathf.CeilToInt(Mathf.Max(0, m.TimeLeft));
            GUI.Label(new Rect(12, 34, 600, 28), $"Time {t / 60}:{t % 60:00}   Run total ${m.RunTotal}", style);
            GUI.Label(new Rect(12, Screen.height - 30, 1100, 28),
                "WASD move  Mouse look  Hold Alt (or V) look around your character  Shift sprint  Space jump  E grab/drop  LMB throw  H hide  F bank  G ride up  Esc cursor", style);

            var big = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            if (m.Phase == HeistPhase.FloorComplete)
            {
                string msg = ElevatorExtraction.EveryoneAboard
                    ? "Quota met - press G to ride up"
                    : "Quota met - get everyone into the elevator, then press G";
                GUI.Label(new Rect(0, 80, Screen.width, 60), msg, big);
            }
            else if (m.Phase == HeistPhase.Won || m.Phase == HeistPhase.Failed)
            {
                string msg = m.Phase == HeistPhase.Won ? "ESCAPED THE TOWER" : $"BUSTED on {m.FloorLabel}";
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), msg, big);
                if (GUI.Button(new Rect(Screen.width / 2 - 60, Screen.height / 2 + 50, 120, 36), "Restart"))
                    UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}