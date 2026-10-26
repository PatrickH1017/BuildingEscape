# Batch C, part 2: T6 (vending machines sell the consumables) and T23 (the black vending machine instead of the red one; its screen is the green terminal look).
# The owner's words (2026-10-23): "Vending machines should be used to buy consumables" and "Use the black vending machine instead of the red and the UI should be a screen like the reference image attached(the green one)".
# "Consumables" = the four single-use throwables the dealer sold (Coffee Mug, Heavy Stapler, Squeaky Toy, Fire Extinguisher: the game has no others, and none is invented). They stay in the dealer's catalog at their old places (so every index, the network
# requests and the saved numbers are what they were) with a new flag, machine: the dealer no longer offers them, the machines do.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($Dry) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
function RepRegionN([string]$startAnchor, [string]$endAnchor, [string]$new) {
  $sa = $startAnchor.Replace("`r`n", "`n").Replace("`n", $script:eol)
  $ea = $endAnchor.Replace("`r`n", "`n").Replace("`n", $script:eol)
  RepRegion $sa $ea $new
}
$bd = $Root.TrimEnd('\')
function Put-New([string]$from, [string]$to) {
  if ($Dry) { Write-Output "dry run: would copy $from -> $to"; return }
  New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "copied $to"
}
Put-New 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaScripts\asks_2026-10-23\third_agent\t6_new\Assets\Scripts\VendingMachine.cs' "$bd\Scripts\VendingMachine.cs"
Put-New 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaScripts\asks_2026-10-23\third_agent\t6_new\Assets\Scripts\Hud\VendingScreen.cs' "$bd\Scripts\Hud\VendingScreen.cs"

# ---------------------------------------------------------------- the dealer: the four consumables are the machines' now
Open-Src "$bd\Scripts\Vendor.cs"
Rep '            public ShieldTier armour;' @'
            public ShieldTier armour;
            /// <summary>Sold at the vending machines of the Loading Docks, not by the dealer (the owner, 2026-10-23: "Vending machines should be used to buy consumables"): <see cref="OnOffer"/> is false for it, <see cref="OnMachine"/> true.</summary>
            public bool machine;
'@
Rep 'name = "Coffee Mug", blurb = "throw it to stun a guard for 2.5s", price = 60,' 'name = "Coffee Mug", blurb = "throw it to stun a guard for 2.5s", price = 60, machine = true,'
Rep 'name = "Heavy Stapler", blurb = "stuns a guard for 6s", price = 250,' 'name = "Heavy Stapler", blurb = "stuns a guard for 6s", price = 250, machine = true,'
Rep 'name = "Squeaky Toy", blurb = "throw it far away to lure guards over", price = 150,' 'name = "Squeaky Toy", blurb = "throw it far away to lure guards over", price = 150, machine = true,'
Rep 'name = "Fire Extinguisher", blurb = "stuns a guard for 10s", price = 600,' 'name = "Fire Extinguisher", blurb = "stuns a guard for 10s", price = 600, machine = true,'
Rep 'for sale now? The first seven always are;' 'for sale now? The three melee weapons always are (the four consumables are sold at the vending machines, not here: <see cref="OnMachine"/>);'
Rep @'
            var p = Catalog[index];
            if (p.isGun) return p.gun == RangedWeapon.Kind.Pew && PewForSale(resume);
'@ @'
            var p = Catalog[index];
            if (p.machine) return false;                                          // (sold at the vending machines now)
            if (p.isGun) return p.gun == RangedWeapon.Kind.Pew && PewForSale(resume);
'@
Rep '        readonly List<int> offered = new List<int>();' @'
        /// <summary>Is entry <paramref name="index"/> of <see cref="Catalog"/> sold at the vending machines of the docks (the owner, 2026-10-23: "Vending machines should be used to buy consumables")?</summary>
        public static bool OnMachine(int index) => index >= 0 && index < Catalog.Length && Catalog[index].machine;

        static int[] machineProducts;
        /// <summary>The entries of <see cref="Catalog"/> the vending machines sell, in the order of the list.</summary>
        public static int[] MachineProducts
        {
            get
            {
                if (machineProducts == null)
                {
                    var l = new List<int>();
                    for (int i = 0; i < Catalog.Length; i++) if (Catalog[i].machine) l.Add(i);
                    machineProducts = l.ToArray();
                }
                return machineProducts;
            }
        }

        readonly List<int> offered = new List<int>();
'@
Rep '        /// <summary>The purchase rules (offline and host). Returns the message for the buyer.</summary>' @'
        /// <summary>
        /// A purchase at a vending machine (offline and host): the cash is taken and the item goes into the buyer's pack, or lies in the machine's tray when the pack has no room (the same rules as the dealer's <see cref="BuyFor"/>,
        /// but the item comes out of the machine at <paramref name="tray"/>, not onto the dealer's shelf). Returns the message for the buyer.
        /// </summary>
        public string BuyAtMachine(HeistManager hm, Backpack buyer, int index, Vector3 tray)
        {
            if (hm == null || buyer == null || !OnMachine(index)) return "The machine does not sell that";
            var p = Catalog[index];
            float wgt = ItemWeight(p);
            if (buyer.Weight + wgt > buyer.capacity + 0.001f) return $"No room in your pack for the {p.name} ({WeightUnits.Lbs(wgt)})";
            if (!hm.TrySpend(p.price)) return $"Not enough cash (it costs ${p.price})";
            float yaw = Random.Range(0f, 360f);
            var go = SpawnAt(p, tray, yaw);
            if (!go) return $"Bought {p.name}";
            var pk = go.GetComponent<Pickup>();
            Net.RunSync.Instance?.BroadcastSpawnItem(pk.NetId, index, go.transform.position, yaw);
            if (Net.NetRole.IsHost && buyer.TryGetComponent(out Net.NetPlayer np))
            {
                Net.RunSync.Instance.AnnounceStored(pk.NetId, np.Slot);              // straight into the buyer's pack, on every machine
                return $"Bought {p.name} - it's in your pack";
            }
            return buyer.TryStore(pk) ? $"Bought {p.name} - it's in your pack" : $"Bought {p.name} - it's in the tray";
        }

        /// <summary>Host: a player pressed a button of a vending machine. He must be standing at it and be free (not pinned or busted).</summary>
        public string HostBuyAtMachine(Net.NetPlayer who, VendingMachine machine, int index)
        {
            var hm = HeistManager.Instance;
            var buyer = who ? who.GetComponent<Backpack>() : null;
            if (hm == null || buyer == null || machine == null || !OnMachine(index)) return null;
            if (who.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return null;
            if (machine.DistanceFrom(who.transform.position) > machine.reach + 1.5f) return "Stand at the machine to buy";          // (the position the host has is a moment old: a little slack)
            return BuyAtMachine(hm, buyer, index, machine.TrayPoint);
        }

        /// <summary>The purchase rules (offline and host). Returns the message for the buyer.</summary>
'@
Save-Src

# ---------------------------------------------------------------- the host: a client's button
Open-Src "$bd\Scripts\Net\RunSync.Requests.cs"
Rep '        public void RequestSell()' @'
        /// <summary>A button of a vending machine (the owner, 2026-10-23): the machine's number (<see cref="VendingMachine.Number"/>, the same on every machine) and the product (an entry of the dealer's catalog the machines sell).</summary>
        public void RequestMachineBuy(int machine, int product)
        {
            if (machine < 0 || machine > byte.MaxValue || product < 0 || product > byte.MaxValue) return;
            if (IsServer) HostMachineBuy(Me, machine, product); else MachineBuyRequestRpc((byte)machine, (byte)product);
        }

        [Rpc(SendTo.Server)]
        void MachineBuyRequestRpc(byte machine, byte product, RpcParams rp = default) => HostMachineBuy(rp.Receive.SenderClientId, machine, product);

        void HostMachineBuy(ulong who, int machine, int product)
        {
            var np = PlayerOf(who);
            var v = Vendor.Current;
            var m = VendingMachine.ByNumber(machine);
            if (np == null || v == null || m == null) return;
            string msg = v.HostBuyAtMachine(np, m, product);
            if (!string.IsNullOrEmpty(msg)) Notice(who, msg);
        }

        public void RequestSell()
'@
Save-Src

# ---------------------------------------------------------------- the docks: both machines of the main room are the black one; every machine can be worked
Open-Src "$bd\Scripts\FloorGenerator.Dock.cs"
RepRegionN @'
        void BuildDockVending(Transform parent, float vendorX)
        {
'@ @'
                    if (r.bounds.size.magnitude < 0.45f) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
'@ @'
        void BuildDockVending(Transform parent, float vendorX)
        {
            DockVendingSpots = new DockVendingRules.Spot[0];
            if (!PropTable.TryGet("vending", out var black)) return;
            // both machines of the main room are the black one (the owner, 2026-10-23: "Use the black vending machine instead of the red"); each of the three is a vending machine that sells the consumables (VendingMachine)
            DockVendingRules.MainRoom(vendorX, DockWallInner, black.W, black.D, black.W, black.D, out var firstSpot, out var secondSpot);
            var breakSpot = DockVendingRules.BreakRoom(BreakRoom.xMax, BreakRoom.yMin, black.W, black.D);
            DockVendingSpots = new[] { firstSpot, secondSpot, breakSpot };
            var walls = new[] { DockWallInner, DockWallInner, BreakRoom.xMax };
            for (int i = 0; i < DockVendingSpots.Length; i++)
            {
                var go = PlaceKey("vending", DockVendingSpots[i].x, DockVendingSpots[i].z, DockVendingSpots[i].yaw, parent, 0f, true);
                SnapBackToWall(go, DockVendingSpots[i], walls[i]);
                HideVendingGlass(go);
                if (go != null) go.AddComponent<VendingMachine>().Setup(DockVendingSpots[i], go.transform.position.y);
            }
        }
'@
Rep '[0] the black one and [1] the red one on the main room''s south wall, [2] the black one in the break room.' '[0] and [1] the two black ones on the main room''s south wall, [2] the black one in the break room.'
Rep '        /// The vending machines (the owner, 2026-10-22:' '        /// The vending machines (the owner, 2026-10-23: both machines of the main room are the black one - "Use the black vending machine instead of the red" - and every machine sells the consumables, see <see cref="VendingMachine"/>; 2026-10-22:'
Save-Src

# ---------------------------------------------------------------- the machine's screen is a screen over the game; the pause menu does not open on the Esc that closes it
Open-Src "$bd\Scripts\TestMode.cs"
Rep 'public static bool PanelOpen => panelOpen || DevTerminal.Open || DevSpectator.MenuOpen;' 'public static bool PanelOpen => panelOpen || DevTerminal.Open || DevSpectator.MenuOpen || VendingScreen.Open;'
Rep '(<see cref="DevTerminal"/>) or the spectator''s dev menu is (<see cref="DevSpectator"/>): all are screens over the game,' '(<see cref="DevTerminal"/>), the spectator''s dev menu is (<see cref="DevSpectator"/>) or a vending machine''s screen is (<see cref="VendingScreen"/>): all are screens over the game,'
Save-Src
Open-Src "$bd\Scripts\EscapeMenu.cs"
Rep 'terminalWasOpen = DevTerminal.Open; }' 'terminalWasOpen = DevTerminal.Open || VendingScreen.Open; }'
Save-Src

# ---------------------------------------------------------------- the old checks that expected the dealer to sell the mug
Open-Src "$bd\Editor\NetDev\NetDevGunsTest.cs"
Rep 'bool basics = true; for (int i = 0; i < Vendor.PewIndex; i++) basics &= Vendor.OnOffer(i, 1) && Vendor.OnOffer(i, 13);' 'bool basics = true; for (int i = 0; i < Vendor.PewIndex; i++) basics &= Vendor.OnOffer(i, 1) == !Vendor.Catalog[i].machine && Vendor.OnOffer(i, 13) == !Vendor.Catalog[i].machine;'
Rep 'Check("vendor: the first seven are always for sale; before floor 3 is done nothing else is (no pew, no ammunition)"' 'Check("vendor: the three melee weapons are always for sale (the four consumables are the vending machines'' now); before floor 3 is done nothing else is (no pew, no ammunition)"'
Save-Src
Open-Src "$bd\Editor\NetDev\NetDevGuns.cs"
Rep 'GunCheck("dealer: the shop shows the first seven, the walkie talkie, and the ammunition of the rifle and the shotgun that were found - nothing else", offeredNow.Count == 10 &&' 'GunCheck("dealer: the shop shows the three melee weapons, the walkie talkie, and the ammunition of the rifle and the shotgun that were found - nothing else", offeredNow.Count == 6 &&'
Save-Src
Open-Src "$bd\Editor\NetDev\NetDevHarness.cs"
Rep 'RunSync.Instance.RequestBuy(0);' @'
Teleport(MachineSpot(0));                             // (the coffee mug is a vending machine's now: the owner, 2026-10-23)
            yield return new WaitForSeconds(1.0f);
            RunSync.Instance.RequestMachineBuy(0, 0);
'@
Rep @'
            yield return WaitFor(() => pack.Items.Count >= 1, 20, "mug in pack");
            if (MeleeWeapon.Available)
            {
'@ @'
            yield return WaitFor(() => pack.Items.Count >= 1, 20, "mug in pack");
            Teleport(vendor.transform.position + Vector3.up * 1.1f);
            yield return new WaitForSeconds(1.0f);
            if (MeleeWeapon.Available)
            {
'@
Save-Src
Open-Src "$bd\Editor\NetDev\NetDevQa.cs"
Rep '                if (role == "c1") { RunSync.Instance.RequestBuy(0); yield return WaitFor(() => pack.Items.Count >= 1, 30, "mug bought"); }' @'
                if (role == "c1")
                {
                    Teleport(MachineSpot(0)); yield return Settle(1.0f);                  // (the coffee mug is a vending machine's now: the owner, 2026-10-23)
                    RunSync.Instance.RequestMachineBuy(0, 0);
                    yield return WaitFor(() => pack.Items.Count >= 1, 30, "mug bought");
                    Teleport(CounterSpot(-1.0f)); yield return Settle(1.0f);
                }
'@
Rep @'
                Teleport(CounterSpot(1.0f));
                yield return Settle(1.5f);
                int packBefore = MyPack.Items.Count;
                RunSync.Instance.RequestBuy(0);
'@ @'
                Teleport(MachineSpot(0));
                yield return Settle(1.5f);
                int packBefore = MyPack.Items.Count;
                RunSync.Instance.RequestMachineBuy(0, 0);
'@
Save-Src

# ---------------------------------------------------------------- the tests
Put-New 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaScripts\asks_2026-10-23\third_agent\t6_new\Assets\Editor\NetDev\NetDevAsks1023VTest.cs' "$bd\Editor\NetDev\NetDevAsks1023VTest.cs"
Put-New 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaScripts\asks_2026-10-23\third_agent\t6_new\Assets\Editor\NetDev\NetDevAsks1023V.cs' "$bd\Editor\NetDev\NetDevAsks1023V.cs"
Open-Src "$bd\Editor\NetDev\NetDevTest.cs"
Rep '            Asks1023JTest();' @'
            Asks1023JTest();
            Asks1023VTest();
'@
Save-Src
Open-Src "$bd\Editor\NetDev\NetDevHarness.cs"
Rep '        public static void Asks1023D()' @'
        public static void Asks1023V()
        {
            SessionState.SetString("netdev.mode", "asks1023v");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Asks1023VUi()
        {
            SessionState.SetString("netdev.mode", "asks1023vui");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Asks1023D()
'@
Rep '&& mode != "walkielook")' '&& mode != "walkielook" && mode != "asks1023v" && mode != "asks1023vui")'
Rep '                case "asks1023d": yield return Asks1023DScenario(); break;' @'
                case "asks1023d": yield return Asks1023DScenario(); break;
                case "asks1023v": yield return Asks1023VScenario(); break;
                case "asks1023vui": yield return Asks1023VUiScenario(); break;
'@
Save-Src
Write-Output 'batch C part 2 (T6, T23) patched'
