using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The alarm beacons of an office floor (the owner, 2026-10-24; <see cref="AlarmBeacons"/>, <see cref="BeaconRules"/>): mounted high up on the walls of the hallways and the big rooms, scattered, their backs against the wall (the first build hung them in the middle of rooms).
    /// Planned from the layout alone (the partitions, the shell between the cells and round the floor, the rooms), so every machine hangs the same ones. They stay dark until the Big Alarm goes on.
    /// </summary>
    public partial class FloorGenerator
    {
        /// <summary>The beacons of the floor on show (null on a floor without any); for the tests.</summary>
        public AlarmBeacons Beacons { get; private set; }

        /// <summary>The height of the underside of the ceiling (m): what hangs from the ceiling (the loudspeakers, the beacons) is measured from it.</summary>
        public float CeilingUnderside => wallHeight - CeilingSink;

        void BuildBeacons(int floor)
        {
            Beacons = null;
            if (layout == null || !Application.isPlaying) return;
            float ceilingY = CeilingUnderside - 0.04f;                          // (the underside of the pack's ceiling tiles, which hang 0.039 m from the flat ceiling)
            float lampY = ceilingY - BeaconRules.Drop, low = lampY - 0.2f, high = lampY + 0.2f;

            // the walls that reach the lamps' height: the shell between the cells and round the floor (boxes of the generator), the partitions and the beams over the doorways (the layout)
            var tall = new List<Rc>();
            var faces = new List<WallFace>();
            foreach (var go in shellBoxes)
            {
                if (!go) continue;
                var p = go.transform.position; var s = go.transform.localScale;
                var r = new Rc(p.x - s.x * 0.5f, p.z - s.z * 0.5f, p.x + s.x * 0.5f, p.z + s.z * 0.5f);
                tall.Add(r);
                BeaconRules.FacesOf(r.x0, r.z0, r.x1, r.z1, 0.6f, faces);
            }
            foreach (var w in layout.walls)
            {
                if (w.kind != "wall" && w.kind != "header") continue;
                if (w.y0 > low || w.y1 < high) continue;
                tall.Add(w.rc);
                if (w.kind == "wall" && !windowedWalls.Contains(w)) BeaconRules.FacesOf(w.rc.x0, w.rc.z0, w.rc.x1, w.rc.z1, 0.6f, faces);          // (a glass front carries none)
            }

            var rooms = layout.rooms;
            int ZoneAt(float x, float z)
            {
                foreach (var room in rooms)
                {
                    var b = room.bounds;
                    if (!b.Contains(x, z)) continue;
                    return room.kind == 2 ? 2 : (b.W * b.D >= BeaconRules.BigRoomArea ? 1 : 0);
                }
                return 0;
            }
            bool Solid(float x, float z)
            {
                foreach (var r in tall) if (r.Contains(x, z)) return true;
                return false;
            }

            var spots = BeaconRules.Plan(faces, layoutSeed * 31 + floor, ZoneAt, Solid);
            if (spots.Count == 0) return;
            var holder = new GameObject("AlarmBeacons");
            holder.transform.SetParent(current, false);
            var beacons = holder.AddComponent<AlarmBeacons>();
            beacons.Build(spots, ceilingY);
            Beacons = beacons;
        }
    }
}
