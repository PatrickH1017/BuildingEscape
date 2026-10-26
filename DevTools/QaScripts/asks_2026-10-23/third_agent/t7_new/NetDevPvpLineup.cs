using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// T7 (the owner, 2026-10-23: "The PvP room should use the battle royale assets from SYNTY to make it seem more like a battlefield"): pictures of the Synty Battle Royale pieces that could dress it, in rows on a bare stage high above the docks, a 1.8 m figure at the
    /// left of every row for the scale, every piece named above itself. A real window is needed (<c>ui.ps1 -Project C:\bd -Methods PvpLineupUi</c>); the pictures are %TEMP%\be_ui_pvp_lineup_1.png and so on, and the log lists each piece's size ([PvpLineup] lines).
    /// </summary>
    public partial class NetDevRunner
    {
        const string PvpLineupRoot = "Assets/Synty/PolygonBattleRoyale/Prefabs/";

        static readonly string[][] PvpLineupRows =
        {
            new[] { "Props/SM_Prop_Barrier_01", "Props/SM_Prop_Barrier_02", "Props/SM_Prop_Barrier_03", "Props/SM_Prop_Barrier_04", "Props/SM_Prop_Barrier_Dirt_01", "Props/SM_Prop_Barricade_01", "Props/SM_Prop_TankTrap_01", "Props/SM_Prop_TireStack_01" },
            new[] { "Props/SM_Prop_Cinderblock_Wall_01", "Props/SM_Prop_Cinderblock_01", "Props/SM_Prop_WireFence_01", "Props/SM_Prop_WireFence_02", "Props/SM_Prop_Iron_Sheet_01", "Props/SM_Prop_Wirespool_01", "Props/SM_Prop_Slatbox_01", "Props/SM_Prop_Crate_Medical_01" },
            new[] { "Props/SM_Prop_BaseWall_01", "Props/SM_Prop_BaseWall_Broken_01", "Props/SM_Prop_Range_Wall_01", "Props/SM_Prop_Range_Wall_Half_01", "Props/SM_Prop_Generator_01", "Props/SM_Prop_Propane_01", "Props/SM_Prop_MissileStack_01", "Props/SM_Prop_Spotlight_01" },
            new[] { "Environments/SM_Env_Rubble_Pile_01", "Environments/SM_Env_Rubble_Pile_02", "Environments/SM_Env_Rubble_Stone_01", "Environments/SM_Env_Port_Wall_01", "Environments/SM_Env_Port_Concrete_Slab_01", "Props/SM_Prop_EmergencyDrop_Crate_01", "Props/SM_Prop_Target_01", "Props/SM_Prop_WeaponCase_01" },
            new[] { "Vehicles/SM_Veh_Armored_Car_Destroyed_01", "Vehicles/SM_Veh_Armored_Car_01" },
        };

        IEnumerator PvpLineupUiScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(1.0f);
            var rig = CameraFollow.Current; if (rig) rig.enabled = false;
            var cam = Camera.main;
            const float stageY = 300f;
            var stage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stage.name = "PvpLineupStage"; stage.transform.position = new Vector3(0f, stageY - 0.1f, 0f); stage.transform.localScale = new Vector3(80f, 0.2f, 40f);
            var sun = new GameObject("PvpLineupSun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.3f; sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            int row = 0;
            foreach (var names in PvpLineupRows)
            {
                row++;
                var made = new List<GameObject>();
                var spawned =new List<(GameObject go, Bounds b, string name)>();
                foreach (var n in names)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PvpLineupRoot + n + ".prefab");
                    if (!prefab) { Log($"[PvpLineup] missing {n}"); continue; }
                    var go = Object.Instantiate(prefab, new Vector3(0f, stageY, 0f), Quaternion.identity);
                    foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
                    var rs = go.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) { Object.Destroy(go); continue; }
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    spawned.Add((go, b, n.Substring(n.LastIndexOf('/') + 1)));
                    Log($"[PvpLineup] {n.Substring(n.LastIndexOf('/') + 1)}: size x {b.size.x:0.00} y {b.size.y:0.00} z {b.size.z:0.00}, centre {b.center.x:0.00} {b.center.y - stageY:0.00} {b.center.z:0.00}, min y {b.min.y - stageY:0.00}");
                }
                float total = spawned.Sum(s => Mathf.Max(s.b.size.x, s.b.size.z, 0.5f) + 0.8f);
                float x = -total * 0.5f + 1.2f;
                var fig = GameObject.CreatePrimitive(PrimitiveType.Capsule);               // (1.8 m tall, 0.5 m across: a person for the scale)
                Object.Destroy(fig.GetComponent<Collider>());
                fig.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f); fig.transform.position = new Vector3(x - 1.2f, stageY + 0.9f, 0f);
                made.Add(fig);
                foreach (var s in spawned)
                {
                    float w = Mathf.Max(s.b.size.x, s.b.size.z, 0.5f);
                    x += w * 0.5f;
                    s.go.transform.position += new Vector3(x - s.b.center.x, stageY - s.b.min.y, -s.b.center.z);
                    var label = new GameObject("label").AddComponent<TextMesh>();
                    label.text = s.name.Replace("SM_Prop_", "").Replace("SM_Env_", "").Replace("SM_Veh_", ""); label.characterSize = 0.07f; label.fontSize = 48; label.anchor = TextAnchor.LowerCenter; label.color = Color.yellow;
                    label.transform.position = new Vector3(x, stageY + s.b.size.y + 0.15f, 0f);
                    label.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                    made.Add(s.go); made.Add(label.gameObject);
                    x += w * 0.5f + 0.8f;
                }
                float dist = Mathf.Max(7f, total * 0.8f);
                cam.transform.position = new Vector3(0f, stageY + 2.2f, -dist);
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, stageY + 1.0f, 0f) - cam.transform.position, Vector3.up);
                yield return new WaitForSeconds(0.5f);
                yield return Snap($"pvp_lineup_{row}");
                foreach (var g in made) if (g) Object.Destroy(g);
                yield return null;
            }
            Object.Destroy(stage); Object.Destroy(sun.gameObject);
            Log("[PvpLineup] DONE");
            Quit(0);
        }
    }
}
