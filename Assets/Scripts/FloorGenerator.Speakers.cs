using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The loudspeakers in the hallways of an office floor (the owner, 2026-10-18; <see cref="PaRules"/>): a round disc with a grille flat against the ceiling (since 2026-10-24: not on a light) every 13 m of corridor, planned from the layout
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
            var panels = new List<Rc>();
            if (panelPlan != null) foreach (var p in panelPlan.panels) panels.Add(AfterHours.FrameRect(p));
            float top = CeilingUnderside - PaRules.CeilingGap;          // (the top of the disc: flat against the ceiling, the owner, 2026-10-24: "attached to the ceiling")
            Rc HallOf(Vector2 p) { foreach (var c in corridors) if (c.Contains(p.x, p.y)) return c; return new Rc(p.x - 1f, p.y - 1f, p.x + 1f, p.y + 1f); }
            for (int i = 0; i < plan.Count; i++)
            {
                var at = PaRules.MountSpot(plan[i], HallOf(plan[i]), panels);          // (the PA keeps the planned place; the model hangs where no light panel is)
                var go = new GameObject("LoudSpeaker_" + i);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(at.x, top - PaRules.DiscHeight * 0.5f, at.y);
                Part(go.transform, "Disc", Vector3.zero, new Vector3(PaRules.DiscDiameter, PaRules.DiscHeight * 0.5f, PaRules.DiscDiameter), body, PrimitiveType.Cylinder);
                Part(go.transform, "Grille", new Vector3(0f, -(PaRules.DiscHeight * 0.5f + PaRules.GrilleHeight * 0.5f), 0f), new Vector3(PaRules.GrilleDiameter, PaRules.GrilleHeight * 0.5f, PaRules.GrilleDiameter), dark, PrimitiveType.Cylinder);
                go.AddComponent<LoudSpeaker>().Index = i;
            }
        }
    }
}
