using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The small valuables of a floor (watches, phones, wallets, laptops, jewellery, cash...): <see cref="PlaceValuables"/> puts the ones <see cref="ValuablePlan"/>
    /// chose on the desks, tables and counters, and <see cref="BuildValuable"/> makes one of them out of a few primitives. The owner, 2026-10-17: "Loot needs to be
    /// more consistent as in rather than some monitors be interactable and some not, make the loot more stuff like watches and phones and such". Each one is a pack item
    /// (E) that is also thrown (Q-less: left mouse) and banked by its value when it is left in the elevator; none of them breaks. The guard outposts' valuables
    /// (<see cref="OutpostLoot"/>) are made by the same code.
    /// </summary>
    public partial class FloorGenerator
    {
        readonly Dictionary<string, Material> valuableMats = new Dictionary<string, Material>();

        void DestroyValuableMaterials()
        {
            foreach (var m in valuableMats.Values) if (m) Kill(m);
            valuableMats.Clear();
        }

        /// <summary>The look of the small valuables (the outpost's own materials are used where they fit).</summary>
        Material VM(string id)
        {
            if (valuableMats.TryGetValue(id, out var m) && m) return m;
            switch (id)
            {
                case "phoneBody": m = Surface("ValPhone", new Color(0.05f, 0.055f, 0.07f), 0.85f, false, 0.4f); break;
                case "screen": m = Surface("ValScreen", new Color(0.30f, 0.52f, 0.85f), 0.9f, true); break;
                case "screenDim": m = Surface("ValScreenDim", new Color(0.10f, 0.17f, 0.30f), 0.9f, true); break;
                case "silver": m = Surface("ValSilver", new Color(0.74f, 0.76f, 0.80f), 0.7f, false, 0.85f); break;
                case "keys": m = Surface("ValKeys", new Color(0.09f, 0.10f, 0.12f), 0.3f, false); break;
                case "brown": m = Surface("ValBrown", new Color(0.40f, 0.22f, 0.10f), 0.25f, false); break;
                case "brownDark": m = Surface("ValBrownDark", new Color(0.20f, 0.10f, 0.05f), 0.2f, false); break;
                case "velvet": m = Surface("ValVelvet", new Color(0.50f, 0.04f, 0.09f), 0.1f, false); break;
                case "velvetDark": m = Surface("ValVelvetDark", new Color(0.24f, 0.02f, 0.05f), 0.1f, false); break;
                case "gem": m = Surface("ValGem", new Color(0.85f, 0.97f, 1.00f), 1f, true, 0.2f); break;
                case "lens": m = Surface("ValLens", new Color(0.02f, 0.03f, 0.07f), 0.95f, false, 0.3f); break;
                case "red": m = Surface("ValRed", new Color(0.82f, 0.10f, 0.10f), 0.4f, false); break;
                case "blue": m = Surface("ValBlue", new Color(0.14f, 0.30f, 0.82f), 0.4f, false); break;
                case "yellow": m = Surface("ValYellow", new Color(0.96f, 0.80f, 0.14f), 0.4f, false); break;
                case "grey": m = Surface("ValGrey", new Color(0.55f, 0.57f, 0.60f), 0.4f, false); break;
                case "white": m = Surface("ValWhite", new Color(0.93f, 0.93f, 0.90f), 0.3f, false); break;
                case "pad": m = Surface("ValPad", new Color(0.16f, 0.12f, 0.10f), 0.15f, false); break;
                default: return OMat(id);
            }
            valuableMats[id] = m;
            return m;
        }

        // ------------------------------------------------------------------ on the desks

        /// <summary>Plans (<see cref="ValuablePlan"/>) and makes the floor's small valuables; adds their number and worth to <paramref name="count"/> and <paramref name="total"/>.</summary>
        void PlaceValuables(int floor, ref int total, ref int count)
        {
            if (layout == null || !Application.isPlaying) return;
            List<PlannedValuable> plan;
            try { plan = ValuablePlan.Plan(layout, layoutSeed, floor, cellSize, cols, rows); }
            catch (System.Exception e) { LogOnce("planning the valuables", e); return; }
            foreach (var pv in plan)
            {
                try
                {
                    BuildValuable(pv.def.kind, pv.value, new Vector3(pv.spot.x, pv.spot.y + 0.004f, pv.spot.z), pv.spot.yaw, current);
                    total += pv.value; count++;
                }
                catch (System.Exception e) { LogOnce("valuable " + pv.def.label, e); }
            }
        }

        // ------------------------------------------------------------------ one valuable

        /// <summary>
        /// A small valuable to steal and bank, lying with its middle at <paramref name="pos"/> (on a surface) and turned by <paramref name="yaw"/>. A pack item with
        /// <paramref name="value"/> dollars, thrown with the left mouse button and banked in the elevator.
        /// </summary>
        GameObject BuildValuable(ValuableKind kind, int value, Vector3 pos, float yaw, Transform parent)
        {
            var def = Valuables.Of(kind);
            var root = new GameObject(def.label);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var t = root.transform;
            Vector3 cc, cs;
            switch (kind)
            {
                case ValuableKind.Watch: ModelWatch(t, out cc, out cs); break;
                case ValuableKind.Chips: ModelChips(t, yaw, out cc, out cs); break;
                case ValuableKind.Phone: ModelPhone(t, out cc, out cs); break;
                case ValuableKind.Wallet: ModelWallet(t, out cc, out cs); break;
                case ValuableKind.Tablet: ModelTablet(t, out cc, out cs); break;
                case ValuableKind.Laptop: ModelLaptop(t, out cc, out cs); break;
                case ValuableKind.Camera: ModelCamera(t, out cc, out cs); break;
                case ValuableKind.Headphones: ModelHeadphones(t, out cc, out cs); break;
                case ValuableKind.Console: ModelConsole(t, yaw, out cc, out cs); break;
                case ValuableKind.Jewelry: ModelJewelry(t, out cc, out cs); break;
                case ValuableKind.Pen: ModelPen(t, out cc, out cs); break;
                case ValuableKind.Cash: ModelCash(t, out cc, out cs); break;
                default: ModelEnvelope(t, out cc, out cs); break;
            }
            var bc = root.AddComponent<BoxCollider>();
            bc.center = cc; bc.size = cs;
            root.AddComponent<Rigidbody>().mass = Mathf.Clamp(def.weight * 0.35f, 0.15f, 1.0f);
            var pk = root.AddComponent<Pickup>();             // (registers its network id now, in the same order on every machine)
            pk.weight = def.weight; pk.label = def.label; pk.bankValue = value;
            var th = root.AddComponent<Throwable>();
            th.impactNoise = 4f; th.stunSeconds = 0.4f;
            return root;
        }

        static GameObject Turn(GameObject go, float x, float y, float z) { go.transform.localRotation = Quaternion.Euler(x, y, z); return go; }

        void ModelEnvelope(Transform t, out Vector3 cc, out Vector3 cs)
        {
            Part(t, "Envelope", new Vector3(0f, 0.008f, 0f), new Vector3(0.24f, 0.016f, 0.16f), VM("paper"));
            Part(t, "Seal", new Vector3(0f, 0.0175f, 0f), new Vector3(0.04f, 0.0015f, 0.04f), VM("seal"), PrimitiveType.Cylinder);
            cc = new Vector3(0f, 0.008f, 0f); cs = new Vector3(0.24f, 0.016f, 0.16f);
        }

        void ModelWatch(Transform t, out Vector3 cc, out Vector3 cs)
        {
            Part(t, "Strap", new Vector3(0f, 0.004f, 0f), new Vector3(0.026f, 0.006f, 0.20f), VM("leather"));
            Part(t, "Buckle", new Vector3(0f, 0.0075f, -0.088f), new Vector3(0.03f, 0.003f, 0.012f), VM("gold"));
            Part(t, "Face", new Vector3(0f, 0.01f, 0f), new Vector3(0.07f, 0.005f, 0.07f), VM("gold"), PrimitiveType.Cylinder);
            Part(t, "Dial", new Vector3(0f, 0.0152f, 0f), new Vector3(0.052f, 0.0008f, 0.052f), VM("white"), PrimitiveType.Cylinder);
            Turn(Part(t, "HandA", new Vector3(0f, 0.0165f, 0.009f), new Vector3(0.0035f, 0.0006f, 0.022f), VM("phoneBody")), 0f, 25f, 0f);
            Turn(Part(t, "HandB", new Vector3(0.006f, 0.0165f, -0.002f), new Vector3(0.0035f, 0.0006f, 0.016f), VM("phoneBody")), 0f, 100f, 0f);
            cc = new Vector3(0f, 0.008f, 0f); cs = new Vector3(0.07f, 0.016f, 0.20f);
        }

        void ModelChips(Transform t, float yaw, out Vector3 cc, out Vector3 cs)
        {
            string[] colours = { "chipRed", "chipWhite", "chipBlue", "chipGreen" };
            for (int i = 0; i < 4; i++)
                Part(t, "Chip", new Vector3(0f, 0.006f + i * 0.0125f, 0f), new Vector3(0.085f, 0.006f, 0.085f), VM(colours[(i + (int)yaw / 90) & 3]), PrimitiveType.Cylinder);
            cc = new Vector3(0f, 0.025f, 0f); cs = new Vector3(0.085f, 0.05f, 0.085f);
        }

        void ModelPhone(Transform t, out Vector3 cc, out Vector3 cs)
        {
            Part(t, "Body", new Vector3(0f, 0.004f, 0f), new Vector3(0.075f, 0.008f, 0.155f), VM("phoneBody"));
            Part(t, "Screen", new Vector3(0f, 0.0083f, -0.002f), new Vector3(0.067f, 0.0014f, 0.142f), VM("screen"));
            Part(t, "Clock", new Vector3(0f, 0.0094f, 0.032f), new Vector3(0.034f, 0.0008f, 0.009f), VM("white"));
            Part(t, "Notch", new Vector3(0f, 0.0094f, 0.067f), new Vector3(0.016f, 0.0008f, 0.004f), VM("phoneBody"));
            cc = new Vector3(0f, 0.005f, 0f); cs = new Vector3(0.075f, 0.012f, 0.155f);
        }

        void ModelWallet(Transform t, out Vector3 cc, out Vector3 cs)
        {
            Part(t, "Body", new Vector3(0f, 0.009f, 0f), new Vector3(0.11f, 0.018f, 0.085f), VM("brown"));
            Part(t, "Fold", new Vector3(0f, 0.0185f, 0f), new Vector3(0.11f, 0.0015f, 0.004f), VM("brownDark"));
            Part(t, "Stitch", new Vector3(0f, 0.0187f, 0.036f), new Vector3(0.1f, 0.0008f, 0.002f), VM("yellow"));
            Part(t, "Note", new Vector3(0.004f, 0.0105f, 0.058f), new Vector3(0.075f, 0.003f, 0.04f), VM("bill"));
            Part(t, "Card", new Vector3(0.03f, 0.0125f, -0.055f), new Vector3(0.05f, 0.002f, 0.03f), VM("silver"));
            cc = new Vector3(0f, 0.01f, 0.01f); cs = new Vector3(0.11f, 0.02f, 0.13f);
        }

        void ModelTablet(Transform t, out Vector3 cc, out Vector3 cs)
        {
            Part(t, "Body", new Vector3(0f, 0.004f, 0f), new Vector3(0.21f, 0.008f, 0.15f), VM("silver"));
            Part(t, "Screen", new Vector3(0f, 0.0083f, 0f), new Vector3(0.195f, 0.0014f, 0.135f), VM("screenDim"));
            Part(t, "Glow", new Vector3(0.02f, 0.0094f, 0.01f), new Vector3(0.1f, 0.0008f, 0.07f), VM("screen"));
            cc = new Vector3(0f, 0.005f, 0f); cs = new Vector3(0.21f, 0.012f, 0.15f);
        }

        void ModelLaptop(Transform t, out Vector3 cc, out Vector3 cs)
        {
            Part(t, "Base", new Vector3(0f, 0.006f, 0.01f), new Vector3(0.33f, 0.012f, 0.23f), VM("silver"));
            Part(t, "Keys", new Vector3(0f, 0.0126f, 0.03f), new Vector3(0.29f, 0.002f, 0.11f), VM("keys"));
            Part(t, "Pad", new Vector3(0f, 0.0126f, 0.098f), new Vector3(0.11f, 0.002f, 0.04f), VM("grey"));
            Turn(Part(t, "Lid", new Vector3(0f, 0.1158f, -0.1328f), new Vector3(0.33f, 0.215f, 0.007f), VM("silver")), -15f, 0f, 0f);
            Turn(Part(t, "Screen", new Vector3(0f, 0.117f, -0.1285f), new Vector3(0.30f, 0.19f, 0.002f), VM("screen")), -15f, 0f, 0f);
            cc = new Vector3(0f, 0.1125f, -0.01f); cs = new Vector3(0.33f, 0.225f, 0.27f);
        }

        void ModelCamera(Transform t, out Vector3 cc, out Vector3 cs)
        {
            Part(t, "Body", new Vector3(0f, 0.035f, 0f), new Vector3(0.13f, 0.07f, 0.06f), VM("phoneBody"));
            Part(t, "Top", new Vector3(0f, 0.0745f, 0f), new Vector3(0.13f, 0.009f, 0.06f), VM("silver"));
            Part(t, "Flash", new Vector3(-0.04f, 0.0805f, 0f), new Vector3(0.025f, 0.004f, 0.016f), VM("screen"));
            Turn(Part(t, "Barrel", new Vector3(0.015f, 0.034f, 0.055f), new Vector3(0.058f, 0.025f, 0.058f), VM("grey"), PrimitiveType.Cylinder), 90f, 0f, 0f);
            Turn(Part(t, "Glass", new Vector3(0.015f, 0.034f, 0.0805f), new Vector3(0.04f, 0.001f, 0.04f), VM("lens"), PrimitiveType.Cylinder), 90f, 0f, 0f);
            cc = new Vector3(0f, 0.04f, 0.03f); cs = new Vector3(0.13f, 0.085f, 0.115f);
        }

        void ModelHeadphones(Transform t, out Vector3 cc, out Vector3 cs)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Turn(Part(t, "Cup", new Vector3(s * 0.078f, 0.045f, 0f), new Vector3(0.09f, 0.015f, 0.09f), VM("phoneBody"), PrimitiveType.Cylinder), 0f, 0f, 90f);
                Turn(Part(t, "Pad", new Vector3(s * 0.06f, 0.045f, 0f), new Vector3(0.075f, 0.004f, 0.075f), VM("pad"), PrimitiveType.Cylinder), 0f, 0f, 90f);
            }
            const int segs = 9;
            for (int i = 0; i < segs; i++)
            {
                float a = (i + 0.5f) / segs * Mathf.PI, b = Mathf.PI / segs;
                var p = new Vector3(Mathf.Cos(a) * 0.078f, 0.045f, -Mathf.Sin(a) * 0.078f);
                float yawDeg = Mathf.Atan2(-Mathf.Sin(a), -Mathf.Cos(a)) * Mathf.Rad2Deg;      // along the tangent
                Turn(Part(t, "Band", p, new Vector3(0.012f, 0.012f, 2f * 0.078f * Mathf.Sin(b * 0.5f) + 0.006f), VM("phoneBody")), 0f, yawDeg, 0f);
            }
            cc = new Vector3(0f, 0.045f, -0.03f); cs = new Vector3(0.19f, 0.09f, 0.17f);
        }

        void ModelConsole(Transform t, float yaw, out Vector3 cc, out Vector3 cs)
        {
            string body = ((int)(yaw / 15f) % 3) == 0 ? "red" : ((int)(yaw / 15f) % 3) == 1 ? "blue" : "grey";
            Part(t, "Body", new Vector3(0f, 0.011f, 0f), new Vector3(0.19f, 0.022f, 0.085f), VM(body));
            Part(t, "Screen", new Vector3(0f, 0.0225f, 0f), new Vector3(0.085f, 0.0016f, 0.058f), VM("screen"));
            Part(t, "PadA", new Vector3(-0.07f, 0.0225f, 0f), new Vector3(0.03f, 0.002f, 0.008f), VM("phoneBody"));
            Part(t, "PadB", new Vector3(-0.07f, 0.0225f, 0f), new Vector3(0.008f, 0.002f, 0.03f), VM("phoneBody"));
            Part(t, "BtnA", new Vector3(0.07f, 0.0228f, 0.012f), new Vector3(0.014f, 0.002f, 0.014f), VM("yellow"), PrimitiveType.Cylinder);
            Part(t, "BtnB", new Vector3(0.056f, 0.0228f, -0.006f), new Vector3(0.014f, 0.002f, 0.014f), VM("phoneBody"), PrimitiveType.Cylinder);
            cc = new Vector3(0f, 0.012f, 0f); cs = new Vector3(0.19f, 0.026f, 0.085f);
        }

        void ModelJewelry(Transform t, out Vector3 cc, out Vector3 cs)
        {
            Part(t, "Base", new Vector3(0f, 0.0175f, 0f), new Vector3(0.10f, 0.035f, 0.10f), VM("velvet"));
            Part(t, "Cushion", new Vector3(0f, 0.036f, 0f), new Vector3(0.075f, 0.004f, 0.075f), VM("velvetDark"));
            Part(t, "Ring", new Vector3(0f, 0.0425f, 0f), new Vector3(0.034f, 0.005f, 0.034f), VM("gold"), PrimitiveType.Cylinder);
            Turn(Part(t, "Gem", new Vector3(0f, 0.054f, 0f), new Vector3(0.014f, 0.014f, 0.014f), VM("gem")), 45f, 45f, 0f);
            Turn(Part(t, "Lid", new Vector3(0f, 0.0833f, -0.0629f), new Vector3(0.10f, 0.006f, 0.10f), VM("velvet")), -105f, 0f, 0f);
            cc = new Vector3(0f, 0.065f, -0.01f); cs = new Vector3(0.10f, 0.13f, 0.13f);
        }

        void ModelPen(Transform t, out Vector3 cc, out Vector3 cs)
        {
            Turn(Part(t, "Barrel", new Vector3(0f, 0.007f, 0f), new Vector3(0.012f, 0.055f, 0.012f), VM("gold"), PrimitiveType.Cylinder), 0f, 0f, 90f);
            Turn(Part(t, "Cap", new Vector3(0.045f, 0.0075f, 0f), new Vector3(0.014f, 0.02f, 0.014f), VM("phoneBody"), PrimitiveType.Cylinder), 0f, 0f, 90f);
            Turn(Part(t, "Tip", new Vector3(-0.058f, 0.007f, 0f), new Vector3(0.006f, 0.007f, 0.006f), VM("silver"), PrimitiveType.Cylinder), 0f, 0f, 90f);
            Part(t, "Clip", new Vector3(0.045f, 0.0155f, 0f), new Vector3(0.03f, 0.002f, 0.004f), VM("silver"));
            cc = new Vector3(0f, 0.012f, 0f); cs = new Vector3(0.16f, 0.03f, 0.04f);
        }

        void ModelCash(Transform t, out Vector3 cc, out Vector3 cs)
        {
            Part(t, "Bills", new Vector3(0f, 0.014f, 0f), new Vector3(0.156f, 0.028f, 0.066f), VM("bill"));
            Part(t, "Band", new Vector3(0f, 0.014f, 0f), new Vector3(0.026f, 0.0295f, 0.0675f), VM("band"));
            cc = new Vector3(0f, 0.014f, 0f); cs = new Vector3(0.156f, 0.03f, 0.068f);
        }
    }
}
