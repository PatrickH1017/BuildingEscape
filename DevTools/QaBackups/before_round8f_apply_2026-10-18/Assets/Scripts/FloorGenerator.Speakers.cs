using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The loudspeakers in the hallways of an office floor (the owner, 2026-10-18; <see cref="PaRules"/>): a small grey box with a grille under the ceiling every 13 m of corridor, planned from the layout
    /// alone (so every machine hangs the same ones). The player at the outpost's computer sees them on the Nav map and switches them on and off there.
    /// </summary>
    public partial class FloorGenerator
    {
        void BuildSpeakers(int floor)
        {
            if (layout == null || !Application.isPlaying) return;
            var corridors = new List<Rc>();
            foreach (var r in layout.rooms) if (r.kind == 2) corridors.Add(r.bounds);
            var plan = PaRules.Plan(corridors);
            PaState.NewFloor(plan);
            if (plan.Count == 0) return;
            var body = OMat("keycap"); var dark = OMat("dark");
            var root = new GameObject("LoudSpeakers").transform;
            root.SetParent(current, false);
            for (int i = 0; i < plan.Count; i++)
            {
                var go = new GameObject("LoudSpeaker_" + i);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(plan[i].x, PaRules.Height, plan[i].y);
                Part(go.transform, "Box", Vector3.zero, new Vector3(0.34f, 0.15f, 0.22f), body);
                Part(go.transform, "Grille", new Vector3(0f, -0.01f, 0.112f), new Vector3(0.28f, 0.11f, 0.014f), dark);
                go.AddComponent<LoudSpeaker>().Index = i;
            }
        }
    }
}
