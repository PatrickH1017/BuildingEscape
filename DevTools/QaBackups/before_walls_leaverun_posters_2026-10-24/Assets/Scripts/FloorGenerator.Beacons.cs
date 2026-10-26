using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>The alarm beacons of an office floor (the owner, 2026-10-24; <see cref="AlarmBeacons"/>, <see cref="BeaconRules"/>): planned from the layout alone, so every machine hangs the same ones. They stay dark until the Big Alarm goes on.</summary>
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
            var halls = new List<Rc>();
            var rooms = new List<Rc>();
            foreach (var r in layout.rooms)
            {
                if (r.kind == 2) halls.Add(r.bounds);
                else rooms.Add(r.bounds);
            }
            var spots = BeaconRules.Plan(halls, rooms);
            if (spots.Count == 0) return;
            var holder = new GameObject("AlarmBeacons");
            holder.transform.SetParent(current, false);
            var beacons = holder.AddComponent<AlarmBeacons>();
            beacons.Build(spots, wallHeight - CeilingSink - 0.04f);          // (the underside of the pack's ceiling tiles, which hang 0.039 m from the flat ceiling)
            Beacons = beacons;
        }
    }
}
