using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Edit-mode floor preview. Builds one generated floor into the open scene so you can look at
    /// it (and tweak FloorGenerator settings) without pressing Play. Uses the same seed as Play, so
    /// the preview matches what you will get in game for that level.
    /// The preview is never saved with the scene and is removed automatically when you enter
    /// Play mode or close the scene.
    /// Menu: Overtime Escape > Floor Preview.
    /// </summary>
    [InitializeOnLoad]
    public class FloorPreviewWindow : EditorWindow
    {
        const string PreviewPrefix = "Floor_";

        [SerializeField] int level;
        [SerializeField] bool customSeed;
        [SerializeField] int seed = 12345;
        [SerializeField] bool autoFrame = true;
        [SerializeField] string lastInfo;

        static FloorPreviewWindow()
        {
            // Never let a preview floor leak into Play mode or into another scene.
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) ClearPreview();
            };
            EditorSceneManager.sceneClosing += (scene, removingScene) => ClearPreview();
        }

        [MenuItem("Overtime Escape/Floor Preview", priority = 20)]
        static void Open()
        {
            var w = GetWindow<FloorPreviewWindow>("Floor Preview");
            w.minSize = new Vector2(300, 260);
        }

        [MenuItem("Overtime Escape/Clear Floor Preview", priority = 21)]
        static void ClearMenu()
        {
            ClearPreview();
            foreach (var w in Resources.FindObjectsOfTypeAll<FloorPreviewWindow>()) { w.lastInfo = null; w.Repaint(); }
        }

        static FloorGenerator FindGenerator() => FindFirstObjectByType<FloorGenerator>();

        static string LevelName(int l) => l <= 0 ? "Basement" : $"Floor {l}";

        void OnGUI()
        {
            var gen = FindGenerator();
            if (!gen)
            {
                EditorGUILayout.HelpBox(
                    "No FloorGenerator in the open scene.\nOpen Assets/Scenes/Greybox.unity " +
                    "(or run Overtime Escape > Build Greybox Scene).", MessageType.Warning);
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Floor Preview works in Edit mode. Stop Play mode to use it.", MessageType.Info);
                return;
            }

            var tm = gen.GetComponent<TowerManager>();
            int total = tm ? Mathf.Max(1, tm.totalFloors) : 11;
            int baseSeed = tm ? tm.baseSeed : 20240;
            level = Mathf.Clamp(level, 0, total - 1);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(LevelName(level), EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                bool go = false;
                using (new EditorGUI.DisabledScope(level <= 0))
                    if (GUILayout.Button("<", GUILayout.Width(28))) { level--; go = true; }
                level = EditorGUILayout.IntSlider(level, 0, total - 1);
                using (new EditorGUI.DisabledScope(level >= total - 1))
                    if (GUILayout.Button(">", GUILayout.Width(28))) { level++; go = true; }
                if (go) { Generate(gen, EffectiveSeed(baseSeed)); GUIUtility.ExitGUI(); }
            }

            customSeed = EditorGUILayout.ToggleLeft("Use a custom seed (otherwise same layout as Play)", customSeed);
            using (new EditorGUI.DisabledScope(!customSeed))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    seed = EditorGUILayout.IntField("Seed", customSeed ? seed : EffectiveSeed(baseSeed));
                    if (GUILayout.Button("Random", GUILayout.Width(64))) { seed = Random.Range(0, int.MaxValue); customSeed = true; }
                }
            }
            autoFrame = EditorGUILayout.ToggleLeft("Frame the floor in the Scene view", autoFrame);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate", GUILayout.Height(28)))
                {
                    Generate(gen, EffectiveSeed(baseSeed));
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("Clear", GUILayout.Height(28), GUILayout.Width(80)))
                {
                    ClearPreview();
                    lastInfo = null;
                }
            }

            if (!string.IsNullOrEmpty(lastInfo))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(lastInfo, MessageType.None);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(
                "The preview isn't saved with the scene and is removed automatically when you press Play.",
                EditorStyles.wordWrappedMiniLabel);
        }

        int EffectiveSeed(int baseSeed) => customSeed ? seed : baseSeed + level * 7919; // same formula as TowerManager

        void Generate(FloorGenerator gen, int seedValue)
        {
            ClearPreview();
            var info = gen.Generate(level, seedValue);
            var root = gen.Current;
            if (!root) return;

            // Keep the preview out of the saved scene.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags |= HideFlags.DontSaveInEditor;

            lastInfo = info.isHub
                ? $"{LevelName(level)}   (safe zone)\n" +
                  $"{info.cols} x {info.rows} rooms  ({info.cols * gen.cellSize:0} m x {info.rows * gen.cellSize:0} m)\n" +
                  "Vendor shop. No guards, loot or quota."
                : $"{LevelName(level)}   (seed {seedValue})\n" +
                  $"{info.cols} x {info.rows} rooms  ({info.cols * gen.cellSize:0} m x {info.rows * gen.cellSize:0} m)\n" +
                  $"~{info.estimatedMinutes:0.0} min full sweep\n" +
                  $"Loot: {info.lootCount} items worth ${info.lootValue}\n" +
                  $"Quota: ${info.quota}\n" +
                  $"Guards: {info.guards}";

            if (autoFrame) Frame(root);
            SceneView.RepaintAll();
            Repaint();
        }

        static void Frame(Transform root)
        {
            var sv = SceneView.lastActiveSceneView;
            if (!sv) return;
            var rends = root.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            sv.Frame(b, false);
        }

        /// <summary>Removes any generated floor sitting under the scene's FloorGenerator.</summary>
        static void ClearPreview()
        {
            var gen = FindGenerator();
            if (!gen) return;
            gen.Clear();
            var t = gen.transform;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i);
                if (c.name.StartsWith(PreviewPrefix)) DestroyImmediate(c.gameObject);
            }
            SceneView.RepaintAll();
        }
    }
}
