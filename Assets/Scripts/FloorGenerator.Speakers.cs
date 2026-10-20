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
        /// <summary>
        /// The desk microphone next to the computer (R9 of 2.23): in the computer's own coordinates the user stands on +z facing -z, so his right hand is on the -x side. A round base with a ring that lights
        /// while the PA is live and a big red button, a gooseneck and a black head tilted towards the user; <see cref="MicProp"/>.
        /// </summary>
        void BuildMicrophone(Transform computer)
        {
            var root = new GameObject("Microphone").transform;
            root.SetParent(computer, false);
            root.localPosition = new Vector3(-0.36f, 0f, 0.20f);
            var dark = OMat("dark"); var steel = OMat("safeSteel"); var red = OMat("alarmButton"); var grey = OMat("keycap");
            Part(root, "MicBase", new Vector3(0f, 0.010f, 0f), new Vector3(0.14f, 0.010f, 0.14f), steel, PrimitiveType.Cylinder);
            var ring = Part(root, "MicRing", new Vector3(0f, 0.021f, 0f), new Vector3(0.142f, 0.002f, 0.142f), OMat("led"), PrimitiveType.Cylinder);
            var button = Part(root, "MicButton", new Vector3(0f, 0.0295f, 0.025f), new Vector3(0.065f, 0.007f, 0.065f), red, PrimitiveType.Cylinder);
            Part(root, "MicNeck", new Vector3(0f, 0.13f, -0.045f), new Vector3(0.015f, 0.11f, 0.015f), dark, PrimitiveType.Cylinder);
            var head = Part(root, "MicHead", new Vector3(0f, 0.27f, -0.02f), new Vector3(0.065f, 0.065f, 0.065f), grey, PrimitiveType.Capsule);
            head.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            root.gameObject.AddComponent<MicProp>().Init(button.transform, ring.GetComponent<Renderer>());
        }

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
