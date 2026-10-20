using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>Menu switch for the in-game cheat panel (Test Mode).</summary>
    public static class TestModeMenu
    {
        const string Path = "Employees Take Revenge/Test Mode (F1 cheat panel)";

        [MenuItem(Path, priority = 40)]
        static void Toggle()
        {
            bool on = PlayerPrefs.GetInt(TestMode.PrefKey, 0) != 1;
            PlayerPrefs.SetInt(TestMode.PrefKey, on ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log(on ? "Test Mode ON: press Play, then F1 in game." : "Test Mode OFF.");
        }

        [MenuItem(Path, true)]
        static bool Validate()
        {
            Menu.SetChecked(Path, PlayerPrefs.GetInt(TestMode.PrefKey, 0) == 1);
            return true;
        }
    }
}
