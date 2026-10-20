using System.Collections.Generic;

namespace BuildingEscape
{
    public enum PropCat { Furniture, Chair, DeskItem, Appliance, Mount, Deco }

    /// <summary>
    /// One kind of prop from the asset packs: where its prefab lives, how big it is and which way it faces.
    /// Pure data (no Unity types) so the room generator can be tested outside the editor.
    ///
    /// Sizes are the model's own x/y/z in metres BEFORE <see cref="scale"/>. <see cref="frontQ"/> is how many
    /// quarter turns (about +Y) rotate the model's own front onto +Z: 0 for the VNB, Pandazole and coffee-shop
    /// props (they face +Z once imported), -1 for the nappin props, which face +X once imported.
    /// </summary>
    public sealed class PropDef
    {
        public readonly string key, path;
        public readonly float nx, ny, nz, scale;
        public readonly int frontQ;
        public readonly bool solid;
        public readonly PropCat cat;
        /// <summary>Cash value when it is something to steal (0 = just furniture).</summary>
        public readonly int value;
        public readonly float weight;
        public readonly int lifters;
        /// <summary>Name shown for it when it is loot.</summary>
        public readonly string label;

        public PropDef(string key, string path, float nx, float ny, float nz, float scale, int frontQ, bool solid, PropCat cat,
                       int value, float weight, int lifters, string label = null)
        {
            this.key = key; this.path = path; this.nx = nx; this.ny = ny; this.nz = nz; this.scale = scale;
            this.frontQ = frontQ; this.solid = solid; this.cat = cat; this.value = value; this.weight = weight; this.lifters = lifters;
            this.label = label ?? Pretty(key);
        }

        static string Pretty(string key)
        {
            var parts = key.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        bool Odd => (frontQ & 1) != 0;
        /// <summary>Size across the front, in metres, after scaling.</summary>
        public float W => (Odd ? nz : nx) * scale;
        public float H => ny * scale;
        /// <summary>Size front-to-back, in metres, after scaling.</summary>
        public float D => (Odd ? nx : nz) * scale;
        /// <summary>Extra yaw (degrees) the prefab needs so its front faces +Z at yaw 0.</summary>
        public float FrontYaw => 90f * frontQ;
        public bool Lootable => value > 0;
    }

    public static class PropTable
    {
        const string Vnb = "Assets/VNB - Office Set/Prefabs/";
        const string Nap = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)";
        const string Pan = "Assets/Pandazole_Ultimate_Pack/Pandazole Kitchen Food/Prefabs/";
        const string Cof = "Assets/Stylized Low Poly Coffee Shop Props Pack \u2013 Free/Prefabs/";

        static readonly Dictionary<string, PropDef> map = new Dictionary<string, PropDef>();
        static readonly List<PropDef> all = new List<PropDef>();

        public static IReadOnlyList<PropDef> All => all;
        public static PropDef Get(string key) => map[key];
        public static bool TryGet(string key, out PropDef d) => map.TryGetValue(key, out d);

        static void Add(string key, string path, float nx, float ny, float nz, float scale = 1f, int frontQ = 0,
                        bool solid = true, PropCat cat = PropCat.Furniture, int value = 0, float weight = 1f, int lifters = 1,
                        string label = null)
        {
            var d = new PropDef(key, path, nx, ny, nz, scale, frontQ, solid, cat, value, weight, lifters, label);
            map[key] = d; all.Add(d);
        }

        static PropTable()
        {
            // ---------- VNB low-poly office set (front = +Z)
            Add("desk_1", Vnb + "Office/desk_1", 1.48f, .95f, .75f);
            Add("desk_2", Vnb + "Office/desk_2", 1.48f, .95f, .75f);
            Add("desk_3", Vnb + "Office/desk_3", 2.37f, .95f, .75f);
            Add("chair_office", Vnb + "Office/office_chair", .70f, 1.15f, .68f, solid: false, cat: PropCat.Chair);
            Add("chair_std", Vnb + "Office/standard_office_chair_1", .70f, 1.15f, .65f, solid: false, cat: PropCat.Chair);
            Add("chair_modern", Vnb + "Furniture/modern_chair", .61f, 1.11f, .68f, solid: false, cat: PropCat.Chair);
            Add("chair_simple", Vnb + "Furniture/simple_chair_1", .72f, .91f, .62f, solid: false, cat: PropCat.Chair);
            Add("stool", Vnb + "Furniture/stool_1", .61f, .71f, .63f, solid: false, cat: PropCat.Chair);

            Add("monitor_new", Vnb + "Office/new_monitor", .40f, .54f, .16f, solid: false, cat: PropCat.DeskItem, value: 400, weight: 5f, label: "Monitor");
            Add("monitor_old", Vnb + "Office/old_monitor", .44f, .51f, .42f, solid: false, cat: PropCat.DeskItem, value: 250, weight: 6f, label: "Old Monitor");
            Add("keyboard", Vnb + "Office/new_keyboard", .44f, .04f, .17f, solid: false, cat: PropCat.DeskItem, value: 100, weight: 1f, label: "Keyboard");
            Add("mouse", Vnb + "Office/new_mouse", .09f, .04f, .15f, solid: false, cat: PropCat.DeskItem);
            Add("phone", Vnb + "Office/phone", .20f, .06f, .32f, solid: false, cat: PropCat.DeskItem, value: 200, weight: 1.5f, label: "Desk Phone");
            Add("stapler", Vnb + "Office/stapler", .14f, .04f, .04f, solid: false, cat: PropCat.DeskItem, value: 90, weight: .6f, label: "Stapler");
            Add("tape", Vnb + "Office/tape", .05f, .12f, .17f, solid: false, cat: PropCat.DeskItem, value: 70, weight: .4f, label: "Roll of Tape");
            Add("notepad", Vnb + "Office/notepad", .18f, .02f, .27f, solid: false, cat: PropCat.DeskItem, value: 50, weight: .4f, label: "Notepad");
            Add("papers", Vnb + "Office/pile_of_paper_small", .26f, .14f, .36f, solid: false, cat: PropCat.DeskItem);
            Add("pencil_holder", Vnb + "Office/pencil_holder", .10f, .15f, .10f, solid: false, cat: PropCat.DeskItem);
            Add("cup", Vnb + "Food/coffee_cup", .16f, .12f, .11f, solid: false, cat: PropCat.DeskItem);

            Add("bookshelf", Vnb + "Furniture/bookshelf", 1.16f, 2.05f, .64f);
            Add("bookshelf_locked", Vnb + "Furniture/bookshelf_locked", 1.16f, 2.05f, .64f);
            Add("shelf", Vnb + "Furniture/shelf", .99f, 2.0f, .55f);
            Add("shelf_low", Vnb + "Furniture/shelf_rect", 1.56f, .47f, .44f);
            Add("shelf_box", Vnb + "Furniture/shelf_box", .72f, .75f, .44f);
            Add("cabinet_med", Vnb + "Furniture/file_cabinet_medium", .57f, 1.05f, .53f);
            Add("cabinet_small", Vnb + "Furniture/file_cabinet_small", .27f, 1.05f, .60f);
            Add("counter_a1", Vnb + "Furniture/counter_a_1", 1.0f, .95f, .66f);
            Add("counter_a2", Vnb + "Furniture/counter_a_2", 1.0f, .95f, 1.0f);
            Add("counter_b1", Vnb + "Furniture/counter_b_1", 1.0f, .95f, .66f);
            Add("counter_b2", Vnb + "Furniture/counter_b_2", 1.0f, .95f, 1.0f);
            Add("table_box", Vnb + "Furniture/table", .72f, .95f, .75f);
            Add("table_round", Vnb + "Furniture/table_circular", .72f, .95f, .75f);
            Add("coffee_table", Vnb + "Furniture/coffee_table", 1.46f, .51f, .63f);

            Add("coffee_machine", Vnb + "Office/coffee_machine", .47f, .63f, .67f, cat: PropCat.Appliance, value: 650, weight: 6f, label: "Coffee Machine");
            Add("microwave", Vnb + "Office/microwave_new", .72f, .47f, .51f, cat: PropCat.Appliance, value: 350, weight: 9f, label: "Microwave");
            Add("printer", Vnb + "Office/printer", .72f, .52f, .92f, cat: PropCat.Appliance, value: 800, weight: 16f, lifters: 2, label: "Office Printer");
            Add("projector", Vnb + "Office/projector", .35f, .12f, .36f, cat: PropCat.Appliance, value: 900, weight: 5f, label: "Projector");

            Add("wall_tv", Vnb + "Office/wall_tv", 1.05f, .56f, .04f, solid: false, cat: PropCat.Mount);
            Add("whiteboard", Vnb + "Furniture/whiteboard_wall", 2.06f, 1.04f, .16f, solid: false, cat: PropCat.Mount);
            Add("clock", Vnb + "Office/clock", .40f, .40f, .05f, solid: false, cat: PropCat.Mount);
            Add("ac_unit", Vnb + "Office/air_conditioner", 1.4f, .26f, .36f, solid: false, cat: PropCat.Mount);
            Add("painting", Vnb + "Deco/big_painting", .87f, .44f, .03f, solid: false, cat: PropCat.Mount);
            Add("painting_small", Vnb + "Deco/small_painting", .25f, .25f, .04f, solid: false, cat: PropCat.Mount);

            Add("trash_bin", Vnb + "Deco/trash_bin", .36f, .63f, .31f, solid: false, cat: PropCat.Deco);
            Add("trash_small", Vnb + "Deco/small_trash_bin", .31f, .37f, .28f, solid: false, cat: PropCat.Deco);
            Add("box", Vnb + "Deco/box", .65f, .65f, .65f, cat: PropCat.Deco, value: 150, weight: 3f, label: "Sealed Crate");
            Add("briefcase", Vnb + "Deco/briefcaase", .73f, .61f, .17f, solid: false, cat: PropCat.Deco, value: 1100, weight: 4f, label: "Briefcase");
            Add("plant_small", Vnb + "Deco/nature_deco_plant", .31f, .92f, .31f, cat: PropCat.Deco);
            Add("plant_big", Vnb + "Deco/nature_deco_boxy", .79f, .83f, .77f, cat: PropCat.Deco);
            Add("lamp", Vnb + "Furniture/lamp", .35f, 1.51f, .35f, solid: false, cat: PropCat.Deco);

            // ---------- nappin office essentials (front along +X after import, hence frontQ -1; scaled down to the VNB world)
            Add("conf_table", Nap + "ConferenceTable", 1.22f, .94f, 2.44f, 1.2f);
            Add("sofa1", Nap + "Sofa1", 1.01f, .95f, 1.22f, 1f, -1);
            Add("sofa2", Nap + "Sofa2", 1.01f, .97f, 2.04f, 1f, -1);
            Add("sofa3", Nap + "Sofa3", 1.01f, .97f, 2.79f, 1f, -1);
            Add("lounge_chair", Nap + "LoungeChair", .77f, .81f, .83f, 1f, -1);
            Add("fatboy", Nap + "Fatboy", 1.33f, 1.26f, 1.37f, .7f);
            Add("water", Nap + "WaterDispenser", .40f, 1.58f, .45f, 1f, -1, cat: PropCat.Appliance, value: 300, weight: 10f, lifters: 2, label: "Water Cooler");
            Add("credenza", Nap + "BigDrawer", .89f, 1.18f, 3.69f, .8f, -1);
            Add("kitchen_unit", Nap + "KitchenModule", .64f, 2.54f, 5.33f, .75f, -1);
            Add("copier", Nap + "Printer", 1.02f, 1.64f, 1.67f, .6f, -1, cat: PropCat.Appliance, value: 1400, weight: 30f, lifters: 3, label: "Copy Machine");

            // ---------- Pandazole kitchen
            Add("fridge", Pan + "Prop_Fridge_01", 1.61f, 2.2f, .90f, cat: PropCat.Appliance);
            Add("kcab", Pan + "Prop_KitchenCabinet_01", 1.2f, 1.36f, .77f);
            Add("ksink", Pan + "Prop_Sink_01", 1.8f, 1.14f, .77f);
            Add("kstorage", Pan + "Prop_StorageCabinet_02", 1.62f, .73f, .77f);
            Add("kshelve", Pan + "Prop_StorageShelve", 2.28f, 2.1f, .58f);

            // ---------- coffee shop pack (scaled to match)
            Add("cs_counter", Cof + "Counter_1", 4.5f, 1.23f, 1.4f, .85f);
            Add("cs_display", Cof + "Display_Counter", 3.0f, 1.51f, 1.42f, .85f);
            Add("cs_register", Cof + "Cash_Register", 1.02f, .79f, .66f, .7f, solid: false, cat: PropCat.DeskItem);
            Add("cs_table", Cof + "Table_Square", 1.32f, 1.08f, 1.32f, .8f);
            Add("cs_chair", Cof + "Chair", .75f, 1.36f, .75f, .8f, solid: false, cat: PropCat.Chair);
            Add("cs_pot", Cof + "Flower_Pot", .81f, .87f, .87f, .8f, cat: PropCat.Deco);
            Add("cs_menu", Cof + "Menu_Board", 4.0f, 1.08f, .24f, .7f, solid: false, cat: PropCat.Mount);
            Add("pastry", Cof + "Croissant", .3f, .1f, .3f, solid: false, cat: PropCat.DeskItem, value: 60, weight: .5f, label: "Croissant");
            Add("takeaway", Cof + "Takeaway_Coffee_1", .2f, .23f, .2f, solid: false, cat: PropCat.DeskItem, value: 50, weight: .4f, label: "Takeaway Coffee");

            // ---------- built from boxes in code (no prefab, so the editor's catalog builder skips them): the guard outpost's lockers
            // and the floor safe. The safe is under 0.9 m tall on purpose: you cannot duck behind it.
            Add("locker2", null, 1.2f, 1.95f, 0.5f, label: "Locker");
            Add("vault_safe", null, 0.9f, 0.85f, 0.65f, label: "Floor Safe");
        }
    }
}
