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

        const string Syn = "Assets/Synty/PolygonOffice/Prefabs/";
        const string Br = "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/";
        const string Nap = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)";

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
            // ---------- the Synty POLYGON Office pack (the owner, 2026-10-17 night: "use the office assets for each floor"). Every prop faces +Z (frontQ 0); the sizes are the prefabs' own, MEASURED
            // by Editor\SyntySheet.cs Measure (Employees Take Revenge > the tools; synty_measure.tsv) - do not guess them: the planner places by these numbers.
            Add("desk_1", Syn + "Props/Furniture/SM_Prop_Desk_01", 1.96f, .75f, .88f);
            Add("desk_2", Syn + "Props/Furniture/SM_Prop_Desk_04", 2.06f, .75f, .89f);
            Add("desk_3", Syn + "Props/Furniture/SM_Prop_Desk_03", 2.18f, .75f, .95f);
            Add("desk_l", Syn + "Props/Furniture/SM_Prop_Desk_02", 1.96f, .75f, 1.96f);
            Add("desk_l_big", Syn + "Props/Furniture/SM_Prop_Desk_05", 2.74f, .75f, 2.08f);
            Add("desk_reception", Syn + "Props/Furniture/SM_Prop_Desk_06", 3.14f, .93f, 1.78f);
            Add("desk_stand", Syn + "Props/Furniture/SM_Prop_Desk_Standing_01", 1.6f, 1.02f, .83f);
            Add("chair_office", Syn + "Props/Furniture/SM_Prop_Chair_05", .73f, .94f, .73f, solid: false, cat: PropCat.Chair);
            Add("chair_std", Syn + "Props/Furniture/SM_Prop_Chair_07", .68f, 1.08f, .7f, solid: false, cat: PropCat.Chair);
            Add("chair_modern", Syn + "Props/Furniture/SM_Prop_Chair_02", .68f, 1.01f, .69f, solid: false, cat: PropCat.Chair);
            Add("chair_simple", Syn + "Props/Furniture/SM_Prop_Chair_01", .53f, .97f, .64f, solid: false, cat: PropCat.Chair);
            Add("chair_exec", Syn + "Props/Furniture/SM_Prop_Chair_08", .72f, 1.16f, .73f, solid: false, cat: PropCat.Chair);
            Add("chair_cafe", Syn + "Props/Furniture/SM_Prop_Chair_09", .53f, .83f, .58f, solid: false, cat: PropCat.Chair);
            Add("chair_blue", Syn + "Props/Furniture/SM_Prop_Chair_03", .68f, 1.06f, .72f, solid: false, cat: PropCat.Chair);
            Add("stool", Syn + "Props/Furniture/SM_Prop_Chair_11", .48f, .73f, .46f, solid: false, cat: PropCat.Chair);
            Add("sofa1", Syn + "Props/Furniture/SM_Prop_Couch_01", 1.16f, 1.0f, .9f);
            Add("sofa2", Syn + "Props/Furniture/SM_Prop_Couch_02", 1.94f, .94f, .92f);
            Add("sofa3", Syn + "Props/Furniture/SM_Prop_Couch_03", 2.3f, .95f, .81f);
            Add("sofa_wood", Syn + "Props/Furniture/SM_Prop_Couch_04", 1.83f, .91f, .93f);
            Add("armchair", Syn + "Props/Furniture/SM_Prop_Couch_05", .95f, 1.21f, .93f);
            Add("lounge_chair", Syn + "Props/Furniture/SM_Prop_Couch_06", 1.1f, .98f, .83f);
            Add("fatboy", Syn + "Props/Furniture/SM_Prop_Chair_BeanBag_02", 1.13f, .78f, 1.14f);
            Add("beanbag_blue", Syn + "Props/Furniture/SM_Prop_Chair_BeanBag_01", .81f, .66f, .83f);
            Add("table_box", Syn + "Props/Furniture/SM_Prop_Table_02", 1.33f, .75f, 1.27f);
            Add("table_round", Syn + "Props/Furniture/SM_Prop_Table_Round_01", 1.17f, .75f, 1.18f);
            Add("table_round2", Syn + "Props/Furniture/SM_Prop_Table_Round_02", 1.3f, .75f, 1.3f);
            Add("table_long", Syn + "Props/Furniture/SM_Prop_Table_03", 1.87f, .75f, .88f);
            Add("table_big", Syn + "Props/Furniture/SM_Prop_Table_01", 2.1f, .75f, 1.3f);
            Add("conf_table", Syn + "Props/Furniture/SM_Prop_Table_Conference_01", 4.06f, .75f, 2.22f);
            Add("conf_table2", Syn + "Props/Furniture/SM_Prop_Table_Conference_02", 4.04f, .75f, 1.38f);
            Add("coffee_table", Syn + "Props/Furniture/SM_Prop_CoffeeTable_01", 1.05f, .42f, .71f);
            Add("coffee_table2", Syn + "Props/Furniture/SM_Prop_CoffeeTable_02", .96f, .51f, .7f);
            Add("coffee_table3", Syn + "Props/Furniture/SM_Prop_CoffeeTable_03", .7f, .53f, .7f);
            Add("bookshelf", Syn + "Props/Furniture/SM_Prop_Shelf_01", 1.18f, 1.81f, .48f);
            Add("bookshelf_locked", Syn + "Props/Furniture/SM_Prop_Shelf_05", 1.36f, 1.52f, .45f);
            Add("shelf", Syn + "Props/Furniture/SM_Prop_Shelf_03", 1.21f, 1.93f, .51f);
            Add("shelf_low", Syn + "Props/Furniture/SM_Prop_Shelf_06", 1.4f, .89f, .38f);
            Add("shelf_box", Syn + "Props/Furniture/SM_Prop_Cabinets_02", .65f, .76f, .85f);
            Add("shelf_wide", Syn + "Props/Furniture/SM_Prop_Shelf_07", 3.07f, 1.67f, .48f);
            Add("shelf_wood", Syn + "Props/Furniture/SM_Prop_Shelf_02", 2.21f, 1.6f, .51f);
            Add("cabinet_med", Syn + "Props/Furniture/SM_Prop_Cabinets_03", .48f, 1.19f, .56f);
            Add("cabinet_small", Syn + "Props/Furniture/SM_Prop_Cabinets_01", .48f, 1.51f, .56f);
            Add("credenza", Syn + "Props/Furniture/SM_Prop_Shelf_06", 1.4f, .89f, .38f);
            Add("cube_wall", Syn + "Props/Furniture/SM_Prop_CubicalWall_01", 2.0f, 1.33f, .07f);
            Add("cube_wall_tall", Syn + "Props/Furniture/SM_Prop_CubicalWall_Tall_01", 2.0f, 1.92f, .07f);
            Add("cube_wall_half", Syn + "Props/Furniture/SM_Prop_CubicalWall_01_Half", 1.0f, 1.33f, .07f);
            Add("divider", Syn + "Props/Furniture/SM_Prop_Divider_01", 2.5f, 1.5f, .05f);
            Add("monitor_new", Syn + "Props/Desk Props/SM_Prop_Computer_Monitor_01", .66f, .5f, .26f, solid: false, cat: PropCat.DeskItem, value: 400, weight: 5f, label: "Monitor");
            Add("monitor_old", Syn + "Props/Desk Props/SM_Prop_Monitor_Crt_01", .51f, .53f, .55f, solid: false, cat: PropCat.DeskItem, value: 250, weight: 6f, label: "Old Monitor");
            Add("keyboard", Syn + "Props/Desk Props/SM_Prop_Computer_Keyboard_01", .66f, .04f, .21f, solid: false, cat: PropCat.DeskItem, value: 100, weight: 1f, label: "Keyboard");
            Add("mouse", Syn + "Props/Desk Props/SM_Prop_Computer_Mouse_01", .11f, .04f, .16f, solid: false, cat: PropCat.DeskItem);
            Add("phone", Syn + "Props/Desk Props/SM_Prop_Phone_Desk_01", .45f, .28f, .4f, solid: false, cat: PropCat.DeskItem, value: 200, weight: 1.5f, label: "Desk Phone");
            Add("stapler", Syn + "Props/Desk Props/SM_Prop_Stapler_01", .08f, .1f, .28f, solid: false, cat: PropCat.DeskItem, value: 90, weight: .6f, label: "Stapler");
            Add("tape", Syn + "Props/Desk Props/SM_Prop_Cellotape_01", .2f, .12f, .07f, solid: false, cat: PropCat.DeskItem, value: 70, weight: .4f, label: "Roll of Tape");
            Add("notepad", Syn + "Props/Desk Props/SM_Prop_NotePad_01", .17f, .04f, .27f, solid: false, cat: PropCat.DeskItem, value: 50, weight: .4f, label: "Notepad");
            Add("papers", Syn + "Props/Desk Props/SM_Prop_Paper_Pile_01", .27f, .12f, .36f, solid: false, cat: PropCat.DeskItem);
            Add("pencil_holder", Syn + "Props/Desk Props/SM_Prop_PencilCup_01", .13f, .24f, .15f, solid: false, cat: PropCat.DeskItem);
            Add("cup", Syn + "Props/Desk Props/SM_Prop_Cup_Paper_01", .14f, .13f, .14f, solid: false, cat: PropCat.DeskItem);
            Add("laptop", Syn + "Props/Desk Props/SM_Prop_Laptop_01", .52f, .37f, .44f, solid: false, cat: PropCat.DeskItem);
            Add("desk_lamp", Syn + "Props/Desk Props/SM_Prop_Desklamp_02", .17f, .42f, .3f, solid: false, cat: PropCat.DeskItem);
            Add("folder_tray", Syn + "Props/Desk Props/SM_Prop_FolderTray_02", .39f, .13f, .5f, solid: false, cat: PropCat.DeskItem);
            Add("photo", Syn + "Props/Desk Props/SM_Prop_Photo_01", .43f, .39f, .21f, solid: false, cat: PropCat.DeskItem);
            Add("trophy_cup", Syn + "Props/Desk Props/SM_Prop_Trophy_01", .32f, .47f, .24f, solid: false, cat: PropCat.DeskItem);
            Add("name_plate", Syn + "Props/Desk Props/SM_Prop_NamePlate_01", .21f, .07f, .06f, solid: false, cat: PropCat.DeskItem);
            Add("coffee_machine", Syn + "Props/Kitchen Props/SM_Prop_CoffeeMachine_Dripper_01", .36f, .61f, .47f, cat: PropCat.Appliance, value: 650, weight: 6f, label: "Coffee Machine");
            Add("espresso", Syn + "Props/Kitchen Props/SM_Prop_CoffeeMachine_01", 1.04f, .82f, .88f, cat: PropCat.Appliance, value: 900, weight: 12f, lifters: 2, label: "Espresso Machine");
            Add("microwave", Syn + "Props/Kitchen Props/SM_Prop_Microwave_01", .57f, .29f, .47f, cat: PropCat.Appliance, value: 350, weight: 9f, label: "Microwave");
            Add("toaster", Syn + "Props/Kitchen Props/SM_Prop_Toaster_01", .21f, .23f, .4f, solid: false, cat: PropCat.DeskItem);
            Add("kettle", Syn + "Props/Kitchen Props/SM_Prop_Kettle_01", .39f, .4f, .28f, solid: false, cat: PropCat.DeskItem);
            Add("printer", Syn + "Props/Misc/SM_Prop_Photocopier_02", .87f, .68f, .6f, cat: PropCat.Appliance, value: 800, weight: 16f, lifters: 2, label: "Office Printer");
            Add("copier", Syn + "Props/Misc/SM_Prop_Photocopier_01", 1.41f, 1.07f, .6f, cat: PropCat.Appliance, value: 1400, weight: 30f, lifters: 3, label: "Copy Machine");
            Add("shredder", Syn + "Props/Desk Props/SM_Prop_PaperShredder_01", .34f, .48f, .24f, cat: PropCat.Appliance);
            Add("projector", Syn + "Props/Desk Props/SM_Prop_Projector_01", .41f, .13f, .35f, cat: PropCat.Appliance, value: 900, weight: 5f, label: "Projector");
            Add("water", Syn + "Props/Misc/SM_Prop_WaterCooler_01", .57f, 1.68f, .47f, cat: PropCat.Appliance, value: 300, weight: 10f, lifters: 2, label: "Water Cooler");
            Add("fridge", Syn + "Props/Kitchen Props/SM_Prop_Fridge_01", 1.25f, 1.96f, .73f, cat: PropCat.Appliance);
            Add("fridge2", Syn + "Props/Kitchen Props/SM_Prop_Fridge_02", .93f, 2.15f, .85f, cat: PropCat.Appliance);
            Add("minifridge", Syn + "Props/Kitchen Props/SM_Prop_MiniFridge_01", .77f, 1.31f, .77f, cat: PropCat.Appliance);
            Add("vending", Syn + "Props/Bathroom/SM_Prop_VendingMachine_01", 1.2f, 1.99f, .8f, cat: PropCat.Appliance);
            Add("counter_a1", Syn + "Props/Kitchen Props/SM_Prop_Kitchen_Cabinet_01", 1.0f, 1.04f, .71f);
            Add("counter_a2", Syn + "Props/Kitchen Props/SM_Prop_Kitchen_Counter_01", 1.0f, .95f, 1.03f);
            Add("counter_b1", Syn + "Props/Kitchen Props/SM_Prop_Kitchen_Cabinet_01", 1.0f, 1.04f, .71f);
            Add("counter_b2", Syn + "Props/Kitchen Props/SM_Prop_Kitchen_Counter_01", 1.0f, .95f, 1.03f);
            Add("kcab", Syn + "Props/Kitchen Props/SM_Prop_Kitchen_Cabinet_01", 1.0f, 1.04f, .71f);
            Add("ksink", Syn + "Props/Kitchen Props/SM_Prop_Kitchen_CounterSink_01", 1.0f, 1.47f, 1.03f);
            Add("kstorage", Syn + "Props/Furniture/SM_Prop_Cabinets_02", .65f, .76f, .85f);
            Add("kshelve", Syn + "Props/Furniture/SM_Prop_Shelf_02", 2.21f, 1.6f, .51f);
            Add("kitchen_unit", Syn + "Props/Furniture/SM_Prop_Shelf_07", 3.07f, 1.67f, .48f);
            Add("server_rack", Syn + "Props/Misc/SM_Prop_Server_Cabinet_01_Full", .93f, 2.03f, 1.0f);
            Add("server_rack2", Syn + "Props/Misc/SM_Prop_Server_Cabinet_02_Full", .93f, 2.03f, 1.0f);
            Add("server_half", Syn + "Props/Misc/SM_Prop_Server_Cabinet_03_Full", .57f, .82f, .77f);
            Add("pallet", Syn + "Props/Misc/SM_Prop_Pallet_01", 1.44f, .24f, 1.42f);
            Add("cart", Syn + "Props/Misc/SM_Prop_Cart_01", .53f, .93f, 1.16f);
            Add("janitor_cart", Syn + "Props/Misc/SM_Prop_Janitor_Cart_01", 1.6f, 1.3f, .79f);
            Add("mop_bucket", Syn + "Props/Misc/SM_Prop_Janitor_Bucket_01", .49f, .69f, .49f, solid: false, cat: PropCat.Deco);
            Add("coat_rack", Syn + "Props/Misc/SM_Prop_CoatRack_01", .74f, 2.02f, .74f, solid: false, cat: PropCat.Deco);
            Add("arcade", Syn + "Props/Misc/SM_Prop_ArcadeMachine_01", .9f, 1.93f, 1.0f);
            Add("jukebox", Syn + "Props/Misc/SM_Prop_Jukebox_01", 1.18f, 1.63f, .79f);
            Add("pool_table", Syn + "Props/Misc/SM_Prop_PoolTable_01", 3.33f, .89f, 1.88f);
            Add("table_football", Syn + "Props/Furniture/SM_Prop_TableFootball_01", 1.43f, .89f, 1.5f);
            Add("fishtank", Syn + "Props/Misc/SM_Prop_Fishtank_01", 1.28f, 1.34f, .67f);
            Add("presentation_chart", Syn + "Props/Misc/SM_Prop_Presentation_Chart_01", 1.22f, 1.82f, 1.06f, solid: false, cat: PropCat.Deco);
            Add("rolling_tv", Syn + "Props/Misc/SM_Prop_Rolling_TV_01", 1.27f, 1.85f, .84f);
            Add("wall_tv", Syn + "Props/Wall Props/SM_Prop_TV_Wall_01", 1.55f, 1.02f, .24f, solid: false, cat: PropCat.Mount);
            Add("whiteboard", Syn + "Props/Misc/SM_Prop_Whiteboard_01", 1.5f, 1.02f, .06f, solid: false, cat: PropCat.Mount);
            Add("blackboard", Syn + "Props/Wall Props/SM_Prop_Blackboard_01", 1.52f, 1.08f, .04f, solid: false, cat: PropCat.Mount);
            Add("projector_screen", Syn + "Props/Wall Props/SM_Prop_ProjectorScreen_01", 2.61f, 1.56f, .07f, solid: false, cat: PropCat.Mount);
            Add("dartboard", Syn + "Props/Wall Props/SM_Prop_Darkboard_01", .714f, .679f, .334f, solid: false, cat: PropCat.Mount);          // (the dart board: its name has a typo in the pack; the model stands on its back, its front +z, the board 0.7 m across with a dart stuck in it)
            Add("clock", Syn + "Props/Wall Props/SM_Prop_Clock_01", .39f, .37f, .05f, solid: false, cat: PropCat.Mount);
            Add("ac_unit", Syn + "Props/Wall Props/SM_Prop_Aircon_01", 1.84f, .45f, .45f, solid: false, cat: PropCat.Mount);
            Add("painting", Syn + "Props/Wall Props/SM_Prop_Poster_02", .96f, .56f, .08f, solid: false, cat: PropCat.Mount);
            Add("painting_small", Syn + "Props/Wall Props/SM_Prop_Poster_04", .65f, .46f, .06f, solid: false, cat: PropCat.Mount);
            Add("poster_1", Syn + "Props/Wall Props/SM_Prop_Poster_01", .83f, 1.1f, .08f, solid: false, cat: PropCat.Mount);
            Add("poster_3", Syn + "Props/Wall Props/SM_Prop_Poster_03", .79f, .56f, .06f, solid: false, cat: PropCat.Mount);
            Add("poster_stocks", Syn + "Props/Wall Props/SM_Prop_Poster_Stocks_01", .93f, .59f, .05f, solid: false, cat: PropCat.Mount);
            Add("certificate", Syn + "Props/Wall Props/SM_Prop_Certificate_01", .35f, .44f, .04f, solid: false, cat: PropCat.Mount);
            Add("calendar", Syn + "Props/Wall Props/SM_Prop_Calender_01", .48f, .34f, .02f, solid: false, cat: PropCat.Mount);
            Add("child_art", Syn + "Props/Wall Props/SM_Prop_Child_Art_01", .34f, .47f, .05f, solid: false, cat: PropCat.Mount);
            Add("corkboard", Syn + "Props/Wall Props/SM_Prop_CorkBoard_01", 2.04f, 1.82f, .17f, solid: false, cat: PropCat.Mount);
            Add("corkboard_blank", Syn + "Props/Wall Props/SM_Prop_CorkBoard_03", 2.04f, 1.63f, .1f, solid: false, cat: PropCat.Mount);
            Add("wall_dressing", Syn + "Props/Wall Props/SM_Prop_WallDressing_01", 2.27f, .81f, .08f, solid: false, cat: PropCat.Mount);
            Add("wall_dressing2", Syn + "Props/Wall Props/SM_Prop_WallDressing_02", .9f, .76f, .06f, solid: false, cat: PropCat.Mount);
            Add("wall_shelf", Syn + "Props/Wall Props/SM_Prop_Shelf_Wall_01", 1.46f, .53f, .34f, solid: false, cat: PropCat.Mount);
            Add("dartboard", Syn + "Props/Wall Props/SM_Prop_Darkboard_01", .71f, .68f, .33f, solid: false, cat: PropCat.Mount);
            // ---------- the Battle Royale pack's warehouse things for the PVP room (the owner, 2026-10-19 night: "storage containers for players to use as coverage ... somewhat like a warehouse"); measured by Editor\PropMeasure.cs
            Add("container", Br + "SM_Prop_Container_01", 2.45f, 2.61f, 6.19f, cat: PropCat.Deco);
            Add("container_small", Br + "SM_Prop_Container_Small_01", 2.45f, 2.61f, 3.14f, cat: PropCat.Deco);
            Add("crate_large", Br + "SM_Prop_Crate_Large_01", .81f, .79f, .81f, cat: PropCat.Deco);
            Add("pallet_loaded", Br + "SM_Prop_Pallet_Loaded_02", 1.44f, 1.14f, 1.42f, cat: PropCat.Deco);
            Add("barrel", Br + "SM_Prop_Barrel_01", .66f, .91f, .57f, cat: PropCat.Deco);
            Add("extinguisher", Syn + "Props/Wall Props/SM_Prop_Fire_Extinguisher_01", .3f, .53f, .16f, solid: false, cat: PropCat.Mount);
            Add("fire_alarm", Syn + "Props/Wall Props/SM_Prop_FireAlarm_Switch_01", .2f, .2f, .06f, solid: false, cat: PropCat.Mount);
            Add("emergency_axe", Syn + "Props/Wall Props/SM_Prop_Emergency_Axe_01", .98f, .7f, .13f, solid: false, cat: PropCat.Mount);
            Add("fire_detector", Syn + "Props/Wall Props/SM_Prop_Fire_Detector_01", .33f, .33f, .09f, solid: false, cat: PropCat.Mount);
            Add("sign_exit", Syn + "Props/Wall Props/SM_Prop_Sign_Exit_01", .43f, .21f, .05f, solid: false, cat: PropCat.Mount);
            Add("sign_stairs", Syn + "Props/Wall Props/SM_Prop_Sign_Stairs_01", .4f, .16f, .04f, solid: false, cat: PropCat.Mount);
            Add("sign_stairs_big", Syn + "Props/Wall Props/SM_Prop_Sign_Stairs_01", .4f, .16f, .04f, 2f, solid: false, cat: PropCat.Mount);        // (the stairwells' sign: the pack's own, twice its size)
            Add("sign_kitchen", Syn + "Props/Wall Props/SM_Prop_Sign_Kitchen_01", .43f, .16f, .04f, solid: false, cat: PropCat.Mount);
            Add("sign_wc_men", Syn + "Props/Wall Props/SM_Prop_Sign_Toilets_Male_01", .24f, .26f, .03f, solid: false, cat: PropCat.Mount);
            Add("sign_wc_women", Syn + "Props/Wall Props/SM_Prop_Sign_Toilets_Female_01", .23f, .26f, .03f, solid: false, cat: PropCat.Mount);
            Add("sign_wc", Syn + "Props/Wall Props/SM_Prop_Sign_Toilets_01", .46f, .38f, .03f, solid: false, cat: PropCat.Mount);
            Add("sign_out_of_order", Syn + "Props/Wall Props/SM_Prop_Sign_OutofOrder_01", .52f, .33f, .05f, solid: false, cat: PropCat.Mount);
            Add("sign_manager", Syn + "Props/Wall Props/SM_Prop_Sign_Manager_01", .57f, .16f, .04f, solid: false, cat: PropCat.Mount);
            Add("sign_director", Syn + "Props/Wall Props/SM_Prop_Sign_Director_01", .57f, .16f, .04f, solid: false, cat: PropCat.Mount);
            Add("sign_hr", Syn + "Props/Wall Props/SM_Prop_Sign_HR_Dep_01", .35f, .16f, .04f, solid: false, cat: PropCat.Mount);
            Add("sign_sales", Syn + "Props/Wall Props/SM_Prop_Sign_Sales_01", .4f, .16f, .04f, solid: false, cat: PropCat.Mount);
            Add("sign_reception", Syn + "Props/Wall Props/SM_Prop_Sign_Receptionist_01", .58f, .15f, .04f, solid: false, cat: PropCat.Mount);
            Add("trash_bin", Syn + "Props/Misc/SM_Prop_Bin_02", .44f, .7f, .44f, solid: false, cat: PropCat.Deco);
            Add("trash_small", Syn + "Props/Misc/SM_Prop_Bin_01", .45f, .44f, .46f, solid: false, cat: PropCat.Deco);
            Add("box", Syn + "Props/Misc/SM_Prop_CardboardBox_04", .52f, .42f, .5f, cat: PropCat.Deco, value: 150, weight: 3f, label: "Sealed Crate");
            Add("box_small", Syn + "Props/Misc/SM_Prop_CardboardBox_01", .33f, .28f, .47f, cat: PropCat.Deco);
            Add("box_long", Syn + "Props/Misc/SM_Prop_CardboardBox_03", .61f, .35f, .83f, cat: PropCat.Deco);
            Add("box_stack", Syn + "Props/Misc/SM_Prop_CardboardBox_Stack_01", 1.01f, .92f, 1.04f, cat: PropCat.Deco);
            Add("briefcase", Syn + "Props/Misc/SM_Prop_Briefcase_01", .53f, .47f, .14f, solid: false, cat: PropCat.Deco, value: 1100, weight: 4f, label: "Briefcase");
            Add("plant_small", Syn + "Props/Misc/SM_Prop_Plant_09", .31f, .37f, .32f, cat: PropCat.Deco);
            Add("plant_big", Syn + "Props/Misc/SM_Prop_Plant_04", .76f, 1.16f, .58f, cat: PropCat.Deco);
            Add("plant_tall", Syn + "Props/Misc/SM_Prop_Plant_02", .5f, 1.05f, .43f, cat: PropCat.Deco);
            Add("plant_floor", Syn + "Props/Misc/SM_Prop_Plant_01", .68f, 1.01f, .74f, cat: PropCat.Deco);
            Add("plant_box", Syn + "Props/Misc/SM_Prop_Plant_05", 1.13f, 1.08f, .59f, cat: PropCat.Deco);
            Add("plant_med", Syn + "Props/Misc/SM_Prop_Plant_10", .46f, .53f, .51f, cat: PropCat.Deco);
            Add("plant_pot", Syn + "Props/Misc/SM_Prop_Plant_08", .33f, .39f, .35f, cat: PropCat.Deco);
            Add("plant_palm", Syn + "Props/Misc/SM_Prop_Plant_14", 1.58f, 2.5f, 1.64f, cat: PropCat.Deco);
            Add("plant_tiny", Syn + "Props/Misc/SM_Prop_Plant_11", .22f, .31f, .21f, solid: false, cat: PropCat.Deco);
            Add("rug_1", Syn + "Props/Misc/SM_Prop_Rug_01", 2.95f, .04f, 3.41f, solid: false, cat: PropCat.Deco);
            Add("rug_2", Syn + "Props/Misc/SM_Prop_Rug_02", 1.6f, .04f, 3.41f, solid: false, cat: PropCat.Deco);
            Add("rug_3", Syn + "Props/Misc/SM_Prop_Rug_03", 1.03f, .04f, 2.19f, solid: false, cat: PropCat.Deco);
            Add("lamp", Syn + "Props/Misc/SM_Prop_Plant_02", .5f, 1.05f, .43f, solid: false, cat: PropCat.Deco);
            Add("cs_counter", Syn + "Props/Furniture/SM_Prop_Desk_06", 3.14f, .93f, 1.78f);
            Add("cs_display", Syn + "Props/Kitchen Props/SM_Prop_MiniFridge_01", .77f, 1.31f, .77f);
            Add("cs_register", Syn + "Props/Desk Props/SM_Prop_Computer_Monitor_01", .66f, .5f, .26f, solid: false, cat: PropCat.DeskItem);
            Add("cs_table", Syn + "Props/Furniture/SM_Prop_Table_Round_02", 1.3f, .75f, 1.3f);
            Add("cs_chair", Syn + "Props/Furniture/SM_Prop_Chair_09", .53f, .83f, .58f, solid: false, cat: PropCat.Chair);
            Add("cs_pot", Syn + "Props/Misc/SM_Prop_Plant_05", 1.13f, 1.08f, .59f, cat: PropCat.Deco);
            Add("cs_menu", Syn + "Props/Wall Props/SM_Prop_Blackboard_01", 1.52f, 1.08f, .04f, solid: false, cat: PropCat.Mount);
            Add("pastry", Syn + "Props/Kitchen Props/SM_Prop_Donut_Box_01", .54f, .36f, .43f, solid: false, cat: PropCat.DeskItem, value: 60, weight: .5f, label: "Donut Box");
            Add("takeaway", Syn + "Props/Desk Props/SM_Prop_Cup_Coffee_Disposable_Closed_01", .18f, .22f, .18f, solid: false, cat: PropCat.DeskItem, value: 50, weight: .4f, label: "Takeaway Coffee");
            // the big RED vending machine of the nappin Office Essentials pack, for the loading dock's main room (the owner, 2026-10-22: "add them both to the main room of the loading dock"; the black one is "vending" above). Its front faces +X once
            // imported (frontQ -1, see PropDef); 1.28 m deep, 1.51 m wide, 2.43 m high from the FBX (FloorGenerator.GetProto measures the real thing at run time and warns if this is off by more than 15 %).
            Add("vending_red", Nap + "VendingMachine", 1.28f, 2.43f, 1.51f, frontQ: -1, cat: PropCat.Appliance, label: "Vending Machine");

            // ---------- the Floor 7 dressing pass (the owner, 2026-10-23: "Environmental Art Pass - Floor 7 Only"): more of the same Synty POLYGON Office pack, the pieces the rooms did not use yet - books, paper, mugs, lamps, kitchen
            // things, cleaning gear, rugs, wall shelves. Sizes MEASURED like the ones above (synty_measure.tsv). Small things are not solid (no collider); the few solid ones are only ever placed by OfficePlanner.Dressing.cs, which checks
            // their clearance. None is loot (value 0).
            Add("book_row_1", Syn + "Props/Desk Props/SM_Prop_Book_Group_01", 0.906f, 0.287f, 0.258f, solid: false, cat: PropCat.DeskItem);
            Add("book_row_2", Syn + "Props/Desk Props/SM_Prop_Book_Group_02", 0.982f, 0.306f, 0.272f, solid: false, cat: PropCat.DeskItem);
            Add("book_row_3", Syn + "Props/Desk Props/SM_Prop_Book_Group_03", 0.968f, 0.303f, 0.272f, solid: false, cat: PropCat.DeskItem);
            Add("book_pile_1", Syn + "Props/Desk Props/SM_Prop_Book_Group_04", 0.391f, 0.187f, 0.433f, solid: false, cat: PropCat.DeskItem);
            Add("book_pile_2", Syn + "Props/Desk Props/SM_Prop_Book_Group_05", 0.387f, 0.368f, 0.281f, solid: false, cat: PropCat.DeskItem);
            Add("book_pile_3", Syn + "Props/Desk Props/SM_Prop_Book_Group_06", 0.243f, 0.38f, 0.272f, solid: false, cat: PropCat.DeskItem);
            Add("book_pile_4", Syn + "Props/Desk Props/SM_Prop_Book_Group_07", 0.366f, 0.38f, 0.272f, solid: false, cat: PropCat.DeskItem);
            Add("book_pile_5", Syn + "Props/Desk Props/SM_Prop_Book_Group_08", 0.404f, 0.174f, 0.394f, solid: false, cat: PropCat.DeskItem);
            Add("magazine_1", Syn + "Props/Desk Props/SM_Prop_Book_Magazine_01", 0.308f, 0.028f, 0.387f, solid: false, cat: PropCat.DeskItem);
            Add("magazine_2", Syn + "Props/Desk Props/SM_Prop_Book_Magazine_02", 0.308f, 0.028f, 0.387f, solid: false, cat: PropCat.DeskItem);
            Add("folder_1", Syn + "Props/Desk Props/SM_Prop_Folder_Manila_01", 0.382f, 0.026f, 0.307f, solid: false, cat: PropCat.DeskItem);
            Add("folder_2", Syn + "Props/Desk Props/SM_Prop_Folder_Manila_02", 0.409f, 0.026f, 0.307f, solid: false, cat: PropCat.DeskItem);
            Add("folder_3", Syn + "Props/Desk Props/SM_Prop_Folder_Manila_03", 0.425f, 0.073f, 0.307f, solid: false, cat: PropCat.DeskItem);
            Add("folder_4", Syn + "Props/Desk Props/SM_Prop_Folder_Manila_04", 0.427f, 0.039f, 0.409f, solid: false, cat: PropCat.DeskItem);
            Add("file_holder", Syn + "Props/Desk Props/SM_Prop_Folder_Holder_02", 0.092f, 0.409f, 0.329f, solid: false, cat: PropCat.DeskItem);
            Add("folder_tray_2", Syn + "Props/Desk Props/SM_Prop_FolderTray_04", 0.466f, 0.145f, 0.504f, solid: false, cat: PropCat.DeskItem);
            Add("paper_1", Syn + "Props/Desk Props/SM_Prop_Paper_01", 0.221f, 0.012f, 0.333f, solid: false, cat: PropCat.DeskItem);
            Add("paper_2", Syn + "Props/Desk Props/SM_Prop_Paper_02", 0.221f, 0.012f, 0.333f, solid: false, cat: PropCat.DeskItem);
            Add("paper_3", Syn + "Props/Desk Props/SM_Prop_Paper_03", 0.221f, 0.012f, 0.333f, solid: false, cat: PropCat.DeskItem);
            Add("paper_pile_2", Syn + "Props/Desk Props/SM_Prop_Paper_Pile_02", 0.267f, 0.194f, 0.362f, solid: false, cat: PropCat.DeskItem);
            Add("paper_pile_3", Syn + "Props/Desk Props/SM_Prop_Paper_Pile_03", 0.283f, 0.368f, 0.368f, solid: false, cat: PropCat.DeskItem);
            Add("paper_tray_empty", Syn + "Props/Desk Props/SM_Prop_PaperTray_01_Empty_01", 0.349f, 0.307f, 0.456f, solid: false, cat: PropCat.DeskItem);
            Add("paper_tray_full", Syn + "Props/Desk Props/SM_Prop_PaperTray_01_Full_01", 0.349f, 0.307f, 0.456f, solid: false, cat: PropCat.DeskItem);
            Add("sticky_1", Syn + "Props/Desk Props/SM_Prop_Note_01", 0.148f, 0.156f, 0.049f, solid: false, cat: PropCat.DeskItem);
            Add("sticky_2", Syn + "Props/Desk Props/SM_Prop_Note_02", 0.15f, 0.158f, 0.049f, solid: false, cat: PropCat.DeskItem);
            Add("sticky_3", Syn + "Props/Desk Props/SM_Prop_Note_03", 0.144f, 0.15f, 0.049f, solid: false, cat: PropCat.DeskItem);
            Add("notepad_2", Syn + "Props/Desk Props/SM_Prop_NotePad_02", 0.289f, 0.024f, 0.243f, solid: false, cat: PropCat.DeskItem);
            Add("clipboard", Syn + "Props/Desk Props/SM_Prop_Clipboard_01", 0.276f, 0.05f, 0.432f, solid: false, cat: PropCat.DeskItem);
            Add("calculator", Syn + "Props/Desk Props/SM_Prop_Calculator_01", 0.123f, 0.027f, 0.185f, solid: false, cat: PropCat.DeskItem);
            Add("cup_coffee_open", Syn + "Props/Desk Props/SM_Prop_Cup_Coffee_Disposable_Open_Full_01", 0.164f, 0.182f, 0.164f, solid: false, cat: PropCat.DeskItem);
            Add("cup_red", Syn + "Props/Desk Props/SM_Prop_Cup_Red_01", 0.164f, 0.218f, 0.164f, solid: false, cat: PropCat.DeskItem);
            Add("cup_boss", Syn + "Props/Desk Props/SM_Prop_Cup_Boss_01", 0.21f, 0.158f, 0.154f, solid: false, cat: PropCat.DeskItem);
            Add("cup_pens", Syn + "Props/Desk Props/SM_Prop_Cup_Pens_01", 0.21f, 0.29f, 0.154f, solid: false, cat: PropCat.DeskItem);
            Add("cup_foam", Syn + "Props/Desk Props/SM_Prop_Cup_Styrofoam_01", 0.124f, 0.168f, 0.124f, solid: false, cat: PropCat.DeskItem);
            Add("coffee_tray", Syn + "Props/Desk Props/SM_Prop_Coffee_Tray_01_Full", 0.388f, 0.263f, 0.388f, solid: false, cat: PropCat.DeskItem);
            Add("lamp_a", Syn + "Props/Desk Props/SM_Prop_Desklamp_01", 0.382f, 0.401f, 0.28f, solid: false, cat: PropCat.DeskItem);
            Add("lamp_b", Syn + "Props/Desk Props/SM_Prop_Desklamp_03", 0.182f, 0.378f, 0.271f, solid: false, cat: PropCat.DeskItem);
            Add("lamp_c", Syn + "Props/Desk Props/SM_Prop_Desklamp_04", 0.205f, 0.405f, 0.265f, solid: false, cat: PropCat.DeskItem);
            Add("lamp_d", Syn + "Props/Desk Props/SM_Prop_DeskLamp_06", 0.214f, 0.485f, 0.247f, solid: false, cat: PropCat.DeskItem);
            Add("cradle", Syn + "Props/Desk Props/SM_Prop_DeskToy_NewtownsCradle_01", 0.428f, 0.383f, 0.265f, solid: false, cat: PropCat.DeskItem);
            Add("orbiter", Syn + "Props/Desk Props/SM_Prop_DeskToy_Orbiter_01", 0.441f, 0.46f, 0.27f, solid: false, cat: PropCat.DeskItem);
            Add("globe", Syn + "Props/Desk Props/SM_Prop_Globe_01", 0.423f, 0.584f, 0.354f, solid: false, cat: PropCat.DeskItem);
            Add("photo_2", Syn + "Props/Desk Props/SM_Prop_Photo_02", 0.361f, 0.285f, 0.253f, solid: false, cat: PropCat.DeskItem);
            Add("photo_3", Syn + "Props/Desk Props/SM_Prop_Photo_03", 0.348f, 0.388f, 0.21f, solid: false, cat: PropCat.DeskItem);
            Add("photo_4", Syn + "Props/Desk Props/SM_Prop_Photo_04", 0.251f, 0.256f, 0.211f, solid: false, cat: PropCat.DeskItem);
            Add("photo_5", Syn + "Props/Desk Props/SM_Prop_Photo_05", 0.251f, 0.29f, 0.213f, solid: false, cat: PropCat.DeskItem);
            Add("figure_knight", Syn + "Props/Desk Props/SM_Prop_Figure_Knight_01", 0.12f, 0.221f, 0.215f, solid: false, cat: PropCat.DeskItem);
            Add("figure_viking", Syn + "Props/Desk Props/SM_Prop_Figure_Viking_01", 0.119f, 0.21f, 0.161f, solid: false, cat: PropCat.DeskItem);
            Add("headphones", Syn + "Props/Desk Props/SM_Prop_Headphones_01", 0.286f, 0.269f, 0.154f, solid: false, cat: PropCat.DeskItem);
            Add("headset", Syn + "Props/Desk Props/SM_Prop_Headset_01", 0.286f, 0.269f, 0.244f, solid: false, cat: PropCat.DeskItem);
            Add("typewriter", Syn + "Props/Desk Props/SM_Prop_Typewriter_01", 0.552f, 0.209f, 0.424f, solid: false, cat: PropCat.DeskItem);
            Add("laptop_2", Syn + "Props/Desk Props/SM_Prop_Laptop_02", 0.519f, 0.37f, 0.442f, solid: false, cat: PropCat.DeskItem);
            Add("laptop_folded", Syn + "Props/Desk Props/SM_Prop_Laptop_Folding_01", 0.519f, 0.372f, 0.483f, solid: false, cat: PropCat.DeskItem);
            Add("monitor_2", Syn + "Props/Desk Props/SM_Prop_Computer_Monitor_02", 0.711f, 0.572f, 0.215f, solid: false, cat: PropCat.DeskItem);
            Add("monitor_wide", Syn + "Props/Desk Props/SM_Prop_Computer_Monitor_05", 1.027f, 0.595f, 0.22f, solid: false, cat: PropCat.DeskItem);
            Add("monitor_dual", Syn + "Props/Desk Props/SM_Prop_Computer_Monitor_Double_01", 1.388f, 0.835f, 0.45f, solid: false, cat: PropCat.DeskItem);
            Add("pc_setup", Syn + "Props/Desk Props/SM_Prop_Computer_Setup_01", 0.864f, 0.572f, 0.574f, solid: false, cat: PropCat.DeskItem);
            Add("mousepad", Syn + "Props/Desk Props/SM_Prop_Computer_Mousepad_01", 0.42f, 0.008f, 0.334f, solid: false, cat: PropCat.DeskItem);
            Add("keyboard_2", Syn + "Props/Desk Props/SM_Prop_Computer_Keyboard_02", 0.655f, 0.037f, 0.207f, solid: false, cat: PropCat.DeskItem);
            Add("phone_rotary", Syn + "Props/Desk Props/SM_Prop_Phone_Rotary_01", 0.445f, 0.167f, 0.349f, solid: false, cat: PropCat.DeskItem);
            Add("phone_conf", Syn + "Props/Desk Props/SM_Prop_Phone_Conference_01", 0.508f, 0.092f, 0.54f, solid: false, cat: PropCat.DeskItem);
            Add("fax_machine", Syn + "Props/Desk Props/SM_Prop_FaxMachine_01", 0.665f, 0.408f, 0.621f, solid: false, cat: PropCat.DeskItem);
            Add("printer_mini", Syn + "Props/Desk Props/SM_Prop_Printer_Mini_01", 0.433f, 0.363f, 0.621f, solid: false, cat: PropCat.DeskItem);
            Add("radio_1", Syn + "Props/Desk Props/SM_Prop_Radio_01", 0.386f, 0.408f, 0.14f, solid: false, cat: PropCat.DeskItem);
            Add("holepunch", Syn + "Props/Desk Props/SM_Prop_HolePunch_01", 0.212f, 0.14f, 0.154f, solid: false, cat: PropCat.DeskItem);
            Add("guillotine", Syn + "Props/Desk Props/SM_Prop_Guillotine_02", 0.356f, 0.137f, 0.547f, solid: false, cat: PropCat.DeskItem);
            Add("scissors", Syn + "Props/Desk Props/SM_Prop_Scissors_01", 0.17f, 0.041f, 0.403f, solid: false, cat: PropCat.DeskItem);
            Add("can_soda", Syn + "Props/Desk Props/SM_Prop_Can_Soda_01", 0.11f, 0.195f, 0.108f, solid: false, cat: PropCat.DeskItem);
            Add("water_bottle", Syn + "Props/Desk Props/SM_Prop_Bottle_01", 0.104f, 0.317f, 0.103f, solid: false, cat: PropCat.DeskItem);
            Add("neon_cactus", Syn + "Props/Desk Props/SM_Prop_Neon_Cactus_01", 0.289f, 0.555f, 0.173f, solid: false, cat: PropCat.DeskItem);
            Add("server_node_1", Syn + "Props/Misc/SM_Prop_Server_Node_01", 0.798f, 0.2f, 0.7f, solid: false, cat: PropCat.DeskItem);
            Add("server_node_2", Syn + "Props/Misc/SM_Prop_Server_Node_02", 0.735f, 0.1f, 0.7f, solid: false, cat: PropCat.DeskItem);
            Add("server_node_3", Syn + "Props/Misc/SM_Prop_Server_Node_03", 0.409f, 0.081f, 0.7f, solid: false, cat: PropCat.DeskItem);
            Add("modem", Syn + "Props/Desk Props/SM_Prop_Modem_02", 0.3f, 0.248f, 0.267f, solid: false, cat: PropCat.DeskItem);
            Add("net_cable", Syn + "Props/Desk Props/SM_Prop_NetCable_02", 0.431f, 0.116f, 0.468f, solid: false, cat: PropCat.DeskItem);
            Add("mints", Syn + "Props/Desk Props/SM_Prop_Mints_01", 0.243f, 0.08f, 0.243f, solid: false, cat: PropCat.DeskItem);
            Add("jellybeans", Syn + "Props/Desk Props/SM_Prop_Jellybeans_01", 0.215f, 0.312f, 0.215f, solid: false, cat: PropCat.DeskItem);
            Add("plant_desk", Syn + "Props/Misc/SM_Prop_Plant_03", 0.462f, 0.301f, 0.194f, solid: false, cat: PropCat.DeskItem);
            Add("fruit_bowl", Syn + "Props/Kitchen Props/SM_Prop_FruitBowl_01", 0.43f, 0.23f, 0.401f, solid: false, cat: PropCat.DeskItem);
            Add("cake", Syn + "Props/Kitchen Props/SM_Prop_Cake_01", 0.563f, 0.374f, 0.535f, solid: false, cat: PropCat.DeskItem);
            Add("donut_choc", Syn + "Props/Kitchen Props/SM_Prop_Donut_Choc_01", 0.218f, 0.068f, 0.218f, solid: false, cat: PropCat.DeskItem);
            Add("donut_pink", Syn + "Props/Kitchen Props/SM_Prop_Donut_Pink_01", 0.218f, 0.068f, 0.218f, solid: false, cat: PropCat.DeskItem);
            Add("dishes_1", Syn + "Props/Kitchen Props/SM_Prop_Dishes_01", 0.435f, 0.033f, 0.435f, solid: false, cat: PropCat.DeskItem);
            Add("dishes_3", Syn + "Props/Kitchen Props/SM_Prop_Dishes_03", 0.343f, 0.088f, 0.343f, solid: false, cat: PropCat.DeskItem);
            Add("lunchbox", Syn + "Props/Kitchen Props/SM_Prop_Lunchbox_01", 0.36f, 0.14f, 0.314f, solid: false, cat: PropCat.DeskItem);
            Add("lunchbox_2", Syn + "Props/Kitchen Props/SM_Prop_Lunchbox_Plastic_01", 0.36f, 0.174f, 0.353f, solid: false, cat: PropCat.DeskItem);
            Add("milk", Syn + "Props/Kitchen Props/SM_Prop_Milk_Claimed_01", 0.19f, 0.414f, 0.213f, solid: false, cat: PropCat.DeskItem);
            Add("sandwich_1", Syn + "Props/Kitchen Props/SM_Prop_Sandwich_01", 0.177f, 0.096f, 0.273f, solid: false, cat: PropCat.DeskItem);
            Add("sandwich_2", Syn + "Props/Kitchen Props/SM_Prop_Sandwich_02", 0.201f, 0.167f, 0.213f, solid: false, cat: PropCat.DeskItem);
            Add("toaster_2", Syn + "Props/Kitchen Props/SM_Prop_Toaster_02", 0.401f, 0.24f, 0.327f, solid: false, cat: PropCat.DeskItem);
            Add("blender", Syn + "Props/Kitchen Props/SM_Prop_Blender_01", 0.279f, 0.459f, 0.294f, solid: false, cat: PropCat.DeskItem);
            Add("cup_kit_1", Syn + "Props/Kitchen Props/SM_Prop_Cup_01", 0.21f, 0.158f, 0.154f, solid: false, cat: PropCat.DeskItem);
            Add("cup_kit_2", Syn + "Props/Kitchen Props/SM_Prop_Cup_02", 0.192f, 0.158f, 0.154f, solid: false, cat: PropCat.DeskItem);
            Add("food_container", Syn + "Props/Kitchen Props/SM_Prop_Container_01", 0.202f, 0.289f, 0.202f, solid: false, cat: PropCat.DeskItem);
            Add("chopping_board", Syn + "Props/Kitchen Props/SM_Prop_ChoppingBoard_02", 0.666f, 0.048f, 0.378f, solid: false, cat: PropCat.DeskItem);
            Add("knife_holder", Syn + "Props/Kitchen Props/SM_Prop_Knife_Holder_01", 0.188f, 0.478f, 0.402f, solid: false, cat: PropCat.DeskItem);
            Add("pot", Syn + "Props/Kitchen Props/SM_Prop_Pot_01", 0.611f, 0.349f, 0.452f, solid: false, cat: PropCat.DeskItem);
            Add("napkins", Syn + "Props/Desk Props/SM_Prop_Napkins_01", 0.259f, 0.04f, 0.296f, solid: false, cat: PropCat.DeskItem);
            Add("cup_saucer", Syn + "Props/Kitchen Props/SM_Prop_Cup_Saucer_03", 0.231f, 0.021f, 0.2f, solid: false, cat: PropCat.DeskItem);
            Add("snack_1", Syn + "Props/Misc/SM_Prop_Snack_01", 0.306f, 0.136f, 0.343f, solid: false, cat: PropCat.DeskItem);
            Add("snack_2", Syn + "Props/Misc/SM_Prop_Snack_02", 0.306f, 0.136f, 0.343f, solid: false, cat: PropCat.DeskItem);
            Add("snack_3", Syn + "Props/Misc/SM_Prop_Snack_03", 0.306f, 0.136f, 0.343f, solid: false, cat: PropCat.DeskItem);
            Add("bin_tall", Syn + "Props/Misc/SM_Prop_Bin_03", 0.298f, 0.565f, 0.383f, solid: false, cat: PropCat.Deco);
            Add("bin_rubbish", Syn + "Props/Misc/SM_Prop_Bin_Rubbish_01", 0.327f, 0.31f, 0.348f, solid: false, cat: PropCat.Deco);
            Add("bucket_1", Syn + "Props/Misc/SM_Prop_Bucket_01", 0.476f, 0.513f, 0.469f, solid: false, cat: PropCat.Deco);
            Add("mop_1", Syn + "Props/Misc/SM_Prop_Mop_01", 0.499f, 1.6f, 0.698f, solid: false, cat: PropCat.Deco);
            Add("mop_2", Syn + "Props/Misc/SM_Prop_Mop_02", 0.269f, 1.711f, 0.257f, solid: false, cat: PropCat.Deco);
            Add("broom", Syn + "Props/Misc/SM_Prop_Broom_01", 0.222f, 1.032f, 0.07f, solid: false, cat: PropCat.Deco);
            Add("wet_floor", Syn + "Props/Misc/SM_Prop_Sign_Wetfloor_01", 0.43f, 0.786f, 0.459f, solid: false, cat: PropCat.Deco);
            Add("cord_pile", Syn + "Props/Desk Props/SM_Prop_Cord_Pile_01", 1f, 0.184f, 0.582f, solid: false, cat: PropCat.Deco);
            Add("papers_floor_1", Syn + "Props/Misc/SM_Prop_Papers_03", 0.526f, 0.017f, 0.55f, solid: false, cat: PropCat.Deco);
            Add("papers_floor_2", Syn + "Props/Misc/SM_Prop_Papers_02", 1.053f, 0.022f, 0.558f, solid: false, cat: PropCat.Deco);
            Add("rug_4", Syn + "Props/Misc/SM_Prop_Rug_04", 1.068f, 0.078f, 2.24f, solid: false, cat: PropCat.Deco);
            Add("plant_flowers_1", Syn + "Props/Misc/SM_Prop_Plant_Flowers_01", 0.243f, 0.714f, 0.232f, solid: false, cat: PropCat.Deco);
            Add("plant_flowers_2", Syn + "Props/Misc/SM_Prop_Plant_Flowers_02", 0.281f, 0.919f, 0.242f, solid: false, cat: PropCat.Deco);
            Add("vacuum", Syn + "Props/Misc/SM_Prop_Vacuum_01", 0.926f, 1.283f, 0.674f, cat: PropCat.Deco);
            Add("cart_2", Syn + "Props/Misc/SM_Prop_Cart_02", 0.642f, 0.933f, 1.056f, cat: PropCat.Deco);
            Add("fan_stand", Syn + "Props/Misc/SM_Prop_Fan_Pedistal_01", 0.69f, 1.512f, 0.69f, cat: PropCat.Deco);
            Add("swiss_ball", Syn + "Props/Misc/SM_Prop_SwissBall_01", 0.69f, 0.681f, 0.692f, cat: PropCat.Deco);
            Add("ohp", Syn + "Props/Misc/SM_Prop_OHP_01", 0.608f, 0.976f, 0.559f, cat: PropCat.Deco);
            Add("whiteboard_easel", Syn + "Props/Misc/SM_Prop_Whiteboard_02", 1.627f, 1.906f, 0.709f, cat: PropCat.Deco);
            Add("beanbag_2", Syn + "Props/Furniture/SM_Prop_Chair_BeanBag_03", 0.835f, 0.618f, 0.822f);
            Add("beanbag_3", Syn + "Props/Furniture/SM_Prop_Chair_BeanBag_04", 1.134f, 0.895f, 1.058f);
            Add("divider_fold", Syn + "Props/Furniture/SM_Prop_Divider_Folding_01", 1.734f, 1.95f, 0.065f);
            Add("divider_half", Syn + "Props/Furniture/SM_Prop_Divider_Half_01", 1.25f, 1.5f, 0.05f);
            Add("shelf_4", Syn + "Props/Furniture/SM_Prop_Shelf_04", 1.102f, 1.092f, 0.664f);
            Add("drafting_table", Syn + "Props/Furniture/SM_Prop_Table_Drafting_01", 1.633f, 1.324f, 0.842f);
            Add("chair_4", Syn + "Props/Furniture/SM_Prop_Chair_04", 0.679f, 1.078f, 0.708f, solid: false, cat: PropCat.Chair);
            Add("chair_6", Syn + "Props/Furniture/SM_Prop_Chair_06", 0.732f, 1.086f, 0.732f, solid: false, cat: PropCat.Chair);
            Add("chair_10", Syn + "Props/Furniture/SM_Prop_Chair_10", 0.704f, 1.261f, 0.704f, solid: false, cat: PropCat.Chair);
            Add("shelf_wall_2", Syn + "Props/Wall Props/SM_Prop_Shelf_Wall_02", 2.119f, 0.705f, 0.437f, solid: false, cat: PropCat.Mount);
            Add("shelf_wall_3", Syn + "Props/Wall Props/SM_Prop_Shelf_Wall_03", 1.603f, 0.351f, 0.437f, solid: false, cat: PropCat.Mount);
            Add("employee_board", Syn + "Props/Wall Props/SM_Prop_Employee_Wall_01", 2.45f, 1.42f, 0.144f, solid: false, cat: PropCat.Mount);
            Add("corkboard_2", Syn + "Props/Wall Props/SM_Prop_CorkBoard_02", 2.045f, 1.737f, 0.15f, solid: false, cat: PropCat.Mount);
            Add("wall_dressing_3", Syn + "Props/Wall Props/SM_Prop_WallDressing_03", 1.862f, 0.986f, 0.134f, solid: false, cat: PropCat.Mount);
            Add("wall_dressing_4", Syn + "Props/Wall Props/SM_Prop_WallDressing_04", 1.729f, 0.952f, 0.123f, solid: false, cat: PropCat.Mount);
            Add("air_vent", Syn + "Props/Wall Props/SM_Prop_AirVent_Wall_01", 0.849f, 0.425f, 0.047f, solid: false, cat: PropCat.Mount);
            Add("card_scanner", Syn + "Props/Wall Props/SM_Prop_Card_Scanner_01", 0.071f, 0.226f, 0.05f, solid: false, cat: PropCat.Mount);
            Add("socket_1", Syn + "Props/Wall Props/SM_Prop_Socket_Wall_01", 0.122f, 0.203f, 0.052f, solid: false, cat: PropCat.Mount);
            Add("switch_1", Syn + "Props/Wall Props/SM_Prop_Socket_Switch_01", 0.122f, 0.203f, 0.057f, solid: false, cat: PropCat.Mount);
            Add("bunting_1", Syn + "Props/Misc/SM_Prop_Bunting_01", 2.794f, 0.626f, 0.026f, solid: false, cat: PropCat.Mount);
            Add("bunting_2", Syn + "Props/Misc/SM_Prop_Bunting_02", 2.794f, 0.626f, 0.026f, solid: false, cat: PropCat.Mount);

            // ---------- the Floor 7 walls and ceiling (the owner, 2026-10-23): the pack's wall pieces and ceiling tile. WallSkinRules / FloorGenerator.WallSkin place them by their own measures (2.5 m long, the decorated face +z).
            Add("skin_wall_baseboard", Syn + "Buildings/SM_Bld_Wall_Baseboard_01", 2.5f, 2.997f, 0.164f, solid: false, cat: PropCat.Deco);
            Add("skin_wall_trim", Syn + "Buildings/SM_Bld_Wall_Trim_01", 2.5f, 2.997f, 0.164f, solid: false, cat: PropCat.Deco);
            Add("skin_wall_blank", Syn + "Buildings/SM_Bld_Wall_Blank_01", 2.5f, 2.997f, 0.15f, solid: false, cat: PropCat.Deco);
            Add("skin_wall_brick", Syn + "Buildings/SM_Bld_Wall_Brick_01", 2.5f, 2.997f, 0.15f, solid: false, cat: PropCat.Deco);
            Add("skin_ceiling_panel", Syn + "Buildings/SM_Bld_Ceiling_Panel_02", 2.5f, 0.039f, 2.5f, solid: false, cat: PropCat.Deco);
            // ---------- the Floor 7 doors (the owner, 2026-10-23): the pack's doors, fitted to the sliding leaves by DoorSkinRules / FloorGenerator.DoorSkin (a leaf is a unit cube, so only the measures matter, not the way they face).
            Add("skin_door_white", Syn + "Buildings/SM_Bld_Door_02", 1.029f, 1.967f, 0.247f, solid: false, cat: PropCat.Deco);
            Add("skin_door_wood", Syn + "Buildings/SM_Bld_Door_05", 1.029f, 1.967f, 0.247f, solid: false, cat: PropCat.Deco);
            Add("skin_door_glass_l", Syn + "Buildings/SM_Bld_Door_Large_01_L", 0.974f, 2.452f, 0.172f, solid: false, cat: PropCat.Deco);
            Add("skin_door_glass_r", Syn + "Buildings/SM_Bld_Door_Large_01_R", 0.974f, 2.452f, 0.172f, solid: false, cat: PropCat.Deco);

            // ---------- built from boxes in code (no prefab, so the editor's catalog builder skips them): the guard outpost's lockers
            // and the floor safe. The safe is under 0.9 m tall on purpose: you cannot duck behind it.
            Add("locker2", null, 1.2f, 1.95f, 0.5f, label: "Locker");
            Add("vault_safe", null, 0.9f, 0.85f, 0.65f, label: "Floor Safe");
        }
    }
}
