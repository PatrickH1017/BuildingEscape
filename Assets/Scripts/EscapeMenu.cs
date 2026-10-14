using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Esc menu: master volume and Exit Game. It does NOT pause the game; it only frees the cursor.
    /// Created automatically.
    /// </summary>
    public class EscapeMenu : MonoBehaviour
    {
        const string VolumeKey = "OvertimeEscape.MasterVolume";

        public static bool Open { get; private set; }

        bool shopWasOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            if (FindFirstObjectByType<EscapeMenu>()) return;
            var go = new GameObject("EscapeMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<EscapeMenu>();
        }

        void OnDestroy() { Open = false; }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Backpack.UiOpen) Backpack.CloseUi();   // Esc closes the inventory first
                else if (!shopWasOpen) Open = !Open;        // (the shop closes itself on Esc)
            }
        }

        void LateUpdate() { shopWasOpen = Vendor.ShopOpen; }

        void OnGUI()
        {
            if (!Open) return;
            const float w = 340f, h = 210f;
            var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(r, "MENU  (the game keeps running)");
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 34, w - 32, h - 46));
            GUILayout.Label($"Master volume: {Mathf.RoundToInt(AudioListener.volume * 100f)}%");
            float v = GUILayout.HorizontalSlider(AudioListener.volume, 0f, 1f);
            if (!Mathf.Approximately(v, AudioListener.volume))
            {
                AudioListener.volume = v;
                PlayerPrefs.SetFloat(VolumeKey, v);
            }
            GUILayout.Space(18);
            if (GUILayout.Button("Resume", GUILayout.Height(32))) Open = false;
            GUILayout.Space(6);
            if (GUILayout.Button("Exit game", GUILayout.Height(32))) Quit();
            GUILayout.EndArea();
        }

        static void Quit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
