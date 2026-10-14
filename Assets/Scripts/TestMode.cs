using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Tester's cheat panel (F1). Only exists when Test Mode is switched on in the Unity editor
    /// (menu: Overtime Escape > Test Mode), so normal play is untouched.
    /// Gives cash, weapons and loot, jumps between floors, completes quotas, stuns guards, god mode.
    /// </summary>
    public class TestMode : MonoBehaviour
    {
        public const string PrefKey = "OvertimeEscape.TestMode";

        /// <summary>True while the panel is open (the camera frees the cursor).</summary>
        public static bool PanelOpen { get; private set; }

        bool open;
        Vector2 scroll;
        string status = "";
        float statusUntil;
        float capacity = 20f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isEditor || PlayerPrefs.GetInt(PrefKey, 0) != 1) return;
            if (FindFirstObjectByType<TestMode>()) return;
            var go = new GameObject("TestMode");
            DontDestroyOnLoad(go);
            go.AddComponent<TestMode>();
            Debug.Log("Test Mode is ON - press F1 for the cheat panel. (Overtime Escape > Test Mode turns it off.)");
        }

        void OnDestroy() { PanelOpen = false; }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) { open = !open; PanelOpen = open; }
        }

        static Backpack LocalPack()
        {
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p) continue;
                var b = p.GetComponent<Backpack>();
                if (b && b.Viewed) return b;
            }
            foreach (var p in WobblyPlayerRegistry.All) if (p && p.GetComponent<Backpack>()) return p.GetComponent<Backpack>();
            return null;
        }

        void Say(string s) { status = s; statusUntil = Time.time + 3f; }

        Vector3 SpawnPoint(Backpack b) => b.transform.position + b.transform.forward * 2f + Vector3.up * 0.8f;

        void OnGUI()
        {
            if (!open)
            {
                GUI.Label(new Rect(Screen.width - 230, 6, 224, 22), "TEST MODE  (F1: cheats)");
                return;
            }

            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            var pack = LocalPack();
            const float w = 360f;
            var r = new Rect(12, 60, w, Mathf.Min(Screen.height - 80f, 640f));
            GUI.Box(r, "TEST MODE");
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 26, w - 20, r.height - 34));
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label($"Cash: ${(hm ? hm.Cash : 0)}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+$100")) hm?.AddCash(100);
            if (GUILayout.Button("+$1,000")) hm?.AddCash(1000);
            if (GUILayout.Button("+$10,000")) hm?.AddCash(10000);
            GUILayout.EndHorizontal();

            Header("Weapons (into your pack, equipped)");
            if (!MeleeWeapon.Available) GUILayout.Label("Weapons not set up: run Overtime Escape > Setup Weapons");
            GUILayout.BeginHorizontal();
            foreach (MeleeWeapon.Kind k in System.Enum.GetValues(typeof(MeleeWeapon.Kind)))
                if (GUILayout.Button(MeleeWeapon.Specs[(int)k].name) && pack && MeleeWeapon.Available) GiveWeapon(pack, k);
            GUILayout.EndHorizontal();

            Header("Pack");
            GUILayout.Label($"Capacity {capacity:0} kg   (using {(pack ? pack.Weight : 0):0.#})");
            float nc = GUILayout.HorizontalSlider(capacity, 5f, 300f);
            if (!Mathf.Approximately(nc, capacity)) { capacity = Mathf.Round(nc); if (pack) pack.capacity = capacity; }
            if (pack && GUILayout.Button("Empty pack (drop everything)")) pack.DropAll();

            Header("Spawn loot in front of you");
            var gen = FindFirstObjectByType<FloorGenerator>();
            if (gen && pack)
            {
                var names = gen.SpawnableLootNames;
                for (int i = 0; i < names.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < Mathf.Min(i + 2, names.Length); j++)
                        if (GUILayout.Button(names[j]))
                        {
                            var floor = hm ? hm.Floor : 1;
                            if (!gen.SpawnLoot(j, SpawnPoint(pack), floor)) Say("Couldn't spawn (is the Office pack set up?)");
                        }
                    GUILayout.EndHorizontal();
                }
            }

            Header("Go to level");
            if (tm)
            {
                for (int row = 0; row < 3; row++)
                {
                    GUILayout.BeginHorizontal();
                    for (int c = 0; c < 4; c++)
                    {
                        int lvl = row * 4 + c;
                        if (lvl >= tm.totalFloors) break;
                        if (GUILayout.Button(lvl == 0 ? "Basement" : $"F{lvl}")) { tm.GoToLevel(lvl); Say($"Going to {(lvl == 0 ? "the basement" : "floor " + lvl)}"); }
                    }
                    GUILayout.EndHorizontal();
                }
            }

            Header("Floor tools");
            if (hm && GUILayout.Button("Complete quota (unlock elevator up)")) hm.DebugCompleteFloor();
            if (GUILayout.Button("Stun all guards 30s"))
                foreach (var g in FindObjectsByType<GuardAI>(FindObjectsSortMode.None)) g.Stun(30f, null);
            if (hm)
            {
                hm.godMode = GUILayout.Toggle(hm.godMode, "God mode (guards can't catch you)");
                hm.freezeTimer = GUILayout.Toggle(hm.freezeTimer, "Freeze floor timer");
            }
            if (tm && pack && GUILayout.Button("Teleport to elevator"))
            {
                pack.transform.position = tm.CurrentInfo.spawn + new Vector3(0, 1.1f, 0);
                if (pack.TryGetComponent(out Rigidbody rb)) rb.linearVelocity = Vector3.zero;
            }

            if (Time.time < statusUntil) GUILayout.Label(status);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static void Header(string s)
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>" + s + "</b>", new GUIStyle(GUI.skin.label) { richText = true });
        }

        void GiveWeapon(Backpack pack, MeleeWeapon.Kind k)
        {
            var go = MeleeWeapon.Create(k, SpawnPoint(pack), null);
            var floor = FindFirstObjectByType<FloorGenerator>();
            if (floor && floor.Current) go.transform.SetParent(floor.Current, true);
            var p = go.GetComponent<Pickup>();
            if (!pack.TryStore(p)) Say("Pack too full - dropped it in front of you (raise capacity)");
        }
    }
}
