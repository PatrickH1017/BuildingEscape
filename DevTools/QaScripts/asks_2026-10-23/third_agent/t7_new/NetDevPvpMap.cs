using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// A map of the PVP room for T7 (the owner, 2026-10-23: "The PvP room should use the battle royale assets from SYNTY to make it seem more like a battlefield"): the docks are built, then the room (the hall, its north part and the pocket behind the elevator room) is printed
    /// as text - one character per 0.25 m, north at the top: # = something solid between knee and head height (the shelves, the crates), . = free, M = free on the red mat, D = the doorway's lane - and every solid root in the room is listed with its box, so a layout can be
    /// designed on paper and checked afterwards. Run: <c>qa_regress_bd.ps1 -Methods PvpMap</c>; the map is in the log (lines "[PvpMap]").
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator PvpMapScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            yield return new WaitForSeconds(0.5f);
            gen.Clear();
            gen.Generate(0, 1000);
            yield return null; yield return null;
            Physics.SyncTransforms();
            var room = gen.PvpRoomArea; var annex = gen.PvpRoomAnnex; var door = gen.PvpRoomDoor;
            Log($"[PvpMap] hall x {room.xMin:0.0}..{room.xMax:0.0}, z {room.yMin:0.0}..{room.yMax:0.0}; pocket x {annex.xMin:0.0}..{annex.xMax:0.0}, z {annex.yMin:0.0}..{annex.yMax:0.0}; doorway at x {door.x:0.0}, z {door.z:0.0}");
            const float step = 0.25f;
            float x0 = 0f, x1 = 10.2f, z0 = annex.yMin - 0.4f, z1 = room.yMax + 0.4f;
            var sb = new StringBuilder();
            sb.AppendLine("[PvpMap] (north at the top; one character is 0.25 m; # solid, . free, M mat, D the doorway's lane, + a wall's face)");
            for (float z = z1; z >= z0; z -= step)
            {
                sb.Append($"[PvpMap] z {z,5:0.00} ");
                for (float x = x0; x <= x1; x += step)
                {
                    var p = new Vector3(x, 0f, z);
                    bool inRoom = room.Contains(new Vector2(x, z)) || annex.Contains(new Vector2(x, z));
                    bool solid = Physics.CheckCapsule(p + Vector3.up * 0.3f, p + Vector3.up * 1.7f, 0.1f, ~0, QueryTriggerInteraction.Ignore);
                    char c = !inRoom ? (solid ? '+' : ' ') : solid ? '#' : '.';
                    if (inRoom && !solid)
                    {
                        if (Mathf.Abs(z - door.z) <= 1.2f && x >= 5.6f) c = 'D';
                        else if (x >= 2.65f && x <= 6.85f && z >= room.center.y - 1.7f && z <= room.center.y + 1.7f) c = 'M';
                    }
                    sb.Append(c);
                }
                sb.AppendLine();
            }
            Log(sb.ToString().TrimEnd());
            // every solid thing inside the room: its root's name and its box
            var seen = new HashSet<Transform>();
            var found = new List<string>();
            foreach (var col in Physics.OverlapBox(new Vector3(5f, 1.2f, (z0 + z1) * 0.5f), new Vector3(5f, 1.5f, (z1 - z0) * 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                var b = col.bounds;
                if (!(room.Contains(new Vector2(b.center.x, b.center.z)) || annex.Contains(new Vector2(b.center.x, b.center.z)))) continue;
                var root = col.transform; while (root.parent != null && root.parent.parent != null && root.parent.name != "DockDressing" && root.parent.name != "Current" && root.parent.name != "Floor") root = root.parent;
                if (!seen.Add(root)) continue;
                found.Add($"{root.name} at ({b.center.x:0.00}, {b.center.z:0.00}) size ({b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00})");
            }
            foreach (var line in found.OrderBy(s => s)) Log("[PvpMap] solid " + line);
            Log($"[PvpMap] DONE solids {found.Count}");
            Quit(0);
        }
    }
}
