using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Builds the Greybox tower scene (player, camera, HUD, managers). The floors themselves are
    /// generated at runtime by FloorGenerator. Menu: Employees Take Revenge > Build Greybox Scene.
    /// </summary>
    public static class GreyboxBuilder
    {
        static readonly Dictionary<string, Material> mats = new();

        [MenuItem("Employees Take Revenge/Build Greybox Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 0.9f;
            sun.transform.rotation = Quaternion.Euler(60, 30, 0);
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

            var mgr = new GameObject("HeistManager");
            mgr.AddComponent<HeistManager>();
            mgr.AddComponent<HeistHud>();

            var tower = new GameObject("Tower");
            var gen = tower.AddComponent<FloorGenerator>();
            gen.floorMat = Mat("floor"); gen.wallMat = Mat("wall"); gen.deskMat = Mat("desk");
            gen.elevatorMat = Mat("elevator"); gen.lootMat = Mat("loot"); gen.mugMat = Mat("mug"); gen.guardMat = Mat("guard");
            gen.vendorMat = Mat("vendor");
            var tm = tower.AddComponent<TowerManager>();
            tm.generator = gen;

            // camera + player (TowerManager moves the player to each level's spawn)
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.AddComponent<Camera>(); cam.AddComponent<AudioListener>();
            var cf = cam.AddComponent<CameraFollow>();
            cam.transform.position = new Vector3(9, 3, 6);
            var player = SpawnPlayer(new Vector3(9, 1.1f, 9));
            player.AddComponent<Net.OfflinePlayerOnly>(); // networked runs spawn one player per client instead
            cf.target = player.transform;

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Greybox.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Greybox.unity", true) };
            Debug.Log("Greybox scene built: Assets/Scenes/Greybox.unity");
        }

        /// <summary>Generates all 11 levels in edit mode and logs size / quota / guard numbers.</summary>
        [MenuItem("Employees Take Revenge/Smoke Test Floors")]
        public static void SmokeTest()
        {
            var go = new GameObject("SmokeTest");
            var gen = go.AddComponent<FloorGenerator>();
            gen.floorMat = gen.wallMat = gen.deskMat = gen.elevatorMat = gen.lootMat = gen.mugMat = gen.guardMat = gen.vendorMat = Mat("wall");
            for (int level = 0; level <= 10; level++)
            {
                var info = gen.Generate(level, 20240 + level * 7919);
                int agents = gen.Current.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>().Length;
                bool shaft = gen.Current.Find("ElevatorRoom") != null;
                Debug.Log($"[Smoke] level {level}: {info.cols}x{info.rows} rooms ({info.cols * gen.cellSize:0}m x {info.rows * gen.cellSize:0}m), " +
                          $"~{info.estimatedMinutes:0.0} min, loot {info.lootCount} ${info.lootValue}, quota ${info.quota}, " +
                          $"guards {info.guards} (agents {agents}), shaftRoom {shaft}, safeZone {info.isHub}");
                gen.Clear();
            }
            Object.DestroyImmediate(go);
        }

        internal static GameObject SpawnPlayer(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player"; go.transform.position = pos;
            go.GetComponent<Renderer>().sharedMaterial = Mat("player");
            var rb = go.AddComponent<Rigidbody>(); rb.mass = 4f; rb.linearDamping = 1.5f; rb.angularDamping = 1.0f;
            go.AddComponent<WobblyPlayer>();
            go.AddComponent<WobblyPlayerRegistry>();
            var grab = go.AddComponent<PlayerGrab>();
            go.AddComponent<VoiceNoise>();
            var hand = new GameObject("Hand").transform;
            hand.SetParent(go.transform, false); hand.localPosition = new Vector3(0, 0.3f, 1.1f);
            grab.hand = hand;

            // face (so free look has something to show): two eyes + nose on the front (+Z)
            var face = new GameObject("Face").transform;
            face.SetParent(go.transform, false);
            foreach (var x in new[] { -0.17f, 0.17f })
                Part(face, PrimitiveType.Sphere, "Eye", new Vector3(x, 0.62f, 0.42f), new Vector3(0.16f, 0.16f, 0.1f), "mug");
            Part(face, PrimitiveType.Cube, "Nose", new Vector3(0, 0.5f, 0.5f), new Vector3(0.1f, 0.14f, 0.14f), "guard");
            Part(face, PrimitiveType.Cube, "Mouth", new Vector3(0, 0.32f, 0.46f), new Vector3(0.28f, 0.05f, 0.06f), "desk");
            return go;
        }

        static void Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, string mat)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Mat(mat);
            Object.DestroyImmediate(g.GetComponent<Collider>());
        }

        internal static Material Mat(string key)
        {
            if (mats.TryGetValue(key, out var m) && m) return m;
            var color = key switch
            {
                "floor" => new Color(0.35f, 0.37f, 0.42f),
                "wall" => new Color(0.75f, 0.75f, 0.7f),
                "desk" => new Color(0.5f, 0.35f, 0.2f),
                "elevator" => new Color(0.3f, 0.8f, 0.4f),
                "loot" => new Color(1f, 0.82f, 0.1f),
                "mug" => new Color(0.95f, 0.95f, 0.95f),
                "guard" => new Color(0.8f, 0.15f, 0.15f),
                "player" => new Color(0.2f, 0.5f, 0.95f),
                "vendor" => new Color(0.6f, 0.3f, 0.8f),
                _ => Color.magenta
            };
            System.IO.Directory.CreateDirectory("Assets/Materials");
            var path = $"Assets/Materials/{key}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Standard")) { color = color };
                AssetDatabase.CreateAsset(m, path);
            }
            mats[key] = m;
            return m;
        }
    }
}