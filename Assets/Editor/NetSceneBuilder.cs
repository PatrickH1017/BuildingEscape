using BuildingEscape.Net;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Creates the networking assets and scenes: the networked player prefab, the run-sync prefab,
    /// the NetworkPrefabsList, the Boot scene (persistent Net object) and the Menu scene.
    /// Menu: Overtime Escape > Build Network Scenes. Re-runnable.
    /// </summary>
    public static class NetSceneBuilder
    {
        const string PrefabDir = "Assets/Prefabs";

        [MenuItem("Overtime Escape/Build Network Scenes")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(PrefabDir);
            System.IO.Directory.CreateDirectory("Assets/Scenes");

            var player = MakePlayerPrefab();
            var runSync = MakeRunSyncPrefab();
            var list = MakePrefabList(player, runSync);

            BuildBootScene(player, runSync, list);
            BuildMenuScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Menu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Greybox.unity", true),
            };
            Debug.Log("Network scenes built: Boot, Menu (+ Greybox in build settings). Press Play from Boot.");
        }

        static GameObject MakePlayerPrefab()
        {
            var go = GreyboxBuilder.SpawnPlayer(Vector3.zero);
            go.name = "NetPlayer";
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<OwnerNetworkTransform>();
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;   // never scaled
            nt.SyncRotAngleX = nt.SyncRotAngleZ = false;             // only facing (Y) matters
            nt.UseHalfFloatPrecision = true;                         // smaller packets
            nt.Interpolate = true;
            go.AddComponent<NetPlayer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/NetPlayer.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject MakeRunSyncPrefab()
        {
            var go = new GameObject("RunSync");
            go.AddComponent<NetworkObject>();
            go.AddComponent<RunSync>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/RunSync.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static NetworkPrefabsList MakePrefabList(GameObject player, GameObject runSync)
        {
            const string path = PrefabDir + "/NetworkPrefabs.asset";
            AssetDatabase.DeleteAsset(path);
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            list.Add(new NetworkPrefab { Prefab = player });
            list.Add(new NetworkPrefab { Prefab = runSync });
            AssetDatabase.CreateAsset(list, path);
            return list;
        }

        static void BuildBootScene(GameObject player, GameObject runSync, NetworkPrefabsList list)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var net = new GameObject("Net");

            var nm = net.AddComponent<NetworkManager>();
            var steamTransport = net.AddComponent<SteamNetworkTransport>();
            net.AddComponent<UnityTransport>();
            net.AddComponent<SteamService>();
            var session = net.AddComponent<NetSession>();
            net.AddComponent<BootLoader>();

            // A NetworkManager added from code can have no config yet (the inspector creates it when shown).
            nm.NetworkConfig ??= new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = steamTransport;   // NetSession swaps this per mode
            nm.NetworkConfig.ConnectionApproval = true;           // enforces the 4 player cap and version match
            nm.NetworkConfig.EnableSceneManagement = true;
            nm.NetworkConfig.PlayerPrefab = null;                 // players are spawned after the run scene loads
            // Netcode auto-adds its own default list; keep exactly one list (ours) so entries are not duplicated.
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            nm.RunInBackground = true;

            session.playerPrefab = player;
            session.runSyncPrefab = runSync;

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Boot.unity");
        }

        static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.AddComponent<Camera>().backgroundColor = new Color(0.1f, 0.11f, 0.14f);
            cam.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            cam.AddComponent<AudioListener>();
            new GameObject("LobbyMenu").AddComponent<LobbyMenu>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Menu.unity");
        }
    }
}