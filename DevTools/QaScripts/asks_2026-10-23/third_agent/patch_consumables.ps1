# The consumables of the vending machines (the owner, 2026-10-25: "Please implement these consumables that will be sold from vending machines": speed burst, endurance laster, silent steps, invisible, accuracy cola - limits, durations and cooldowns as he gave them).
# New files (sources in consumables\): Scripts\Consumables\ConsumableRules.cs, ConsumableItem.cs, PlayerBuffs.cs, ConsumableHud.cs; Scripts\Net\RunSync.Buffs.cs; Editor\NetDev\NetDevConsumables.cs. The rest are small edits.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $Dry) { New-Item -ItemType Directory -Force ($A + 'Scripts\Consumables') | Out-Null }
foreach ($pair in @(@('ConsumableRules.cs', 'Scripts\Consumables\ConsumableRules.cs'), @('ConsumableItem.cs', 'Scripts\Consumables\ConsumableItem.cs'), @('PlayerBuffs.cs', 'Scripts\Consumables\PlayerBuffs.cs'),
                    @('ConsumableHud.cs', 'Scripts\Consumables\ConsumableHud.cs'), @('RunSync.Buffs.cs', 'Scripts\Net\RunSync.Buffs.cs'), @('NetDevConsumables.cs', 'Editor\NetDev\NetDevConsumables.cs'))) {
  $src = Join-Path $here ('consumables\' + $pair[0]); $dst = $A + $pair[1]
  if ($Dry) { if (-not (Test-Path $src)) { throw "missing $src" }; Write-Output "dry ok would copy $($pair[0])" } else { Copy-Item $src $dst -Force; Write-Output "copied $($pair[0])" }
}

# ---- the dealer's list: five more entries after the walkie talkie (the places of the others are unchanged), sold at the machines only
Open-Src ($A + 'Scripts\Vendor.cs')
Rep '            public bool isWalkie;' @'
            public bool isWalkie;
            /// <summary>A consumable of the vending machines (the owner, 2026-10-25; <see cref="ConsumableRules"/>): <see cref="consumable"/> says which. Sold at the machines only (<see cref="machine"/>).</summary>
            public bool isConsumable;
            public ConsumableKind consumable;
'@
Rep 'price = WalkieRules.Price, isWalkie = true },' @'
price = WalkieRules.Price, isWalkie = true },
            // the consumables of the vending machines (the owner, 2026-10-25), after the walkie talkie so that every other place in this list stays where it was: the words and the numbers are ConsumableRules'
            new Product { name = ConsumableRules.Of(ConsumableKind.SpeedBurst).name, blurb = ConsumableRules.Blurb(ConsumableKind.SpeedBurst), price = ConsumableRules.Of(ConsumableKind.SpeedBurst).price, machine = true, isConsumable = true, consumable = ConsumableKind.SpeedBurst },
            new Product { name = ConsumableRules.Of(ConsumableKind.EnduranceLaster).name, blurb = ConsumableRules.Blurb(ConsumableKind.EnduranceLaster), price = ConsumableRules.Of(ConsumableKind.EnduranceLaster).price, machine = true, isConsumable = true, consumable = ConsumableKind.EnduranceLaster },
            new Product { name = ConsumableRules.Of(ConsumableKind.SilentSteps).name, blurb = ConsumableRules.Blurb(ConsumableKind.SilentSteps), price = ConsumableRules.Of(ConsumableKind.SilentSteps).price, machine = true, isConsumable = true, consumable = ConsumableKind.SilentSteps },
            new Product { name = ConsumableRules.Of(ConsumableKind.Invisible).name, blurb = ConsumableRules.Blurb(ConsumableKind.Invisible), price = ConsumableRules.Of(ConsumableKind.Invisible).price, machine = true, isConsumable = true, consumable = ConsumableKind.Invisible },
            new Product { name = ConsumableRules.Of(ConsumableKind.AccuracyCola).name, blurb = ConsumableRules.Blurb(ConsumableKind.AccuracyCola), price = ConsumableRules.Of(ConsumableKind.AccuracyCola).price, machine = true, isConsumable = true, consumable = ConsumableKind.AccuracyCola },
'@
Rep '        static float ItemWeight(Product p) => p.isWalkie ?' '        static float ItemWeight(Product p) => p.isConsumable ? ConsumableRules.Of(p.consumable).weight : p.isWalkie ?'
Rep '            if (hm == null || buyer == null || !OnMachine(index)) return "The machine does not sell that";
            var p = Catalog[index];' @'
            if (hm == null || buyer == null || !OnMachine(index)) return "The machine does not sell that";
            var p = Catalog[index];
            if (p.isConsumable && buyer.CountConsumable(p.consumable) >= ConsumableRules.Of(p.consumable).limit) return ConsumableRules.AtLimit(p.consumable);          // (the owner's "limit of n in inventory")
'@
Rep '            if (p.isWalkie) return WalkieTalkie.Create(pos, transform.parent, yaw);' @'
            if (p.isConsumable) return ConsumableItem.Create(p.consumable, pos, transform.parent, yaw);
            if (p.isWalkie) return WalkieTalkie.Create(pos, transform.parent, yaw);
'@
Save-Src

# ---- the pack: counting, using (the number keys, the USE button), the limit on picking up
Open-Src ($A + 'Scripts\Backpack.cs')
Rep '        /// <summary>Is the crosshair on an item within reach that E would pick up right now?</summary>' @'
        // ---- the consumables of the vending machines (the owner, 2026-10-25; ConsumableRules)

        /// <summary>How many cans of this kind the pack holds.</summary>
        public int CountConsumable(ConsumableKind kind)
        {
            int n = 0;
            foreach (var p in items) if (p && p.TryGetComponent(out ConsumableItem c) && c.kind == kind) n++;
            return n;
        }

        /// <summary>The number key (7 8 9 0 -) of a kind: the first can of it in the pack is drunk, or the player is told he has none.</summary>
        public void UseKind(ConsumableKind kind)
        {
            Pickup can = null;
            foreach (var p in items) if (p && p.TryGetComponent(out ConsumableItem c) && c.kind == kind) { can = p; break; }
            if (can == null) { if (Viewed) Say(ConsumableRules.NoneInPack(kind)); return; }
            UseConsumable(can);
        }

        /// <summary>Drinks a can of the pack (the USE button, a number key): in a networked run the host decides (<c>RunSync.HostUseConsumable</c>); offline it is checked here - not while pinned or busted, not while that kind cools down (or, invisible, was used this round) - and the can is used up.</summary>
        public void UseConsumable(Pickup can)
        {
            if (!can || !can.TryGetComponent(out ConsumableItem item) || !items.Contains(can)) return;
            if (wobbly && wobbly.InCustody) return;
            if (Net.NetRole.Synced) { Net.RunSync.Instance.RequestUseConsumable(can); return; }
            if (!TryGetComponent(out PlayerBuffs buffs)) return;
            var check = buffs.Check(item.kind);
            if (check != UseCheck.Ok) { Say(ConsumableRules.Refusal(item.kind, check, buffs.Set.CooldownLeft(item.kind, Time.time))); return; }
            RemoveUsed(can);
            Destroy(can.gameObject);
            buffs.StartLocal(item.kind);
        }

        /// <summary>Is the crosshair on an item within reach that E would pick up right now?</summary>
'@
Rep '            float add = AddedWeight(p);
            if (Weight + add <= capacity + 0.001f) return true;' @'
            if (p.TryGetComponent(out ConsumableItem can) && CountConsumable(can.kind) >= ConsumableRules.Of(can.kind).limit) { why = ConsumableRules.AtLimit(can.kind); return false; }          // (the owner's "limit of n in inventory")
            float add = AddedWeight(p);
            if (Weight + add <= capacity + 0.001f) return true;
'@
Rep '            if (!UiOpen && !talk && !EscapeMenu.Open && !TestMode.PanelOpen && Input.GetKeyDown(dropKey)) DropHeld();' @'
            if (!UiOpen && !talk && !EscapeMenu.Open && !TestMode.PanelOpen)
                for (int k = 0; k < ConsumableRules.Count; k++)
                    if (Input.GetKeyDown(ConsumableItem.Keys[k])) UseKind((ConsumableKind)k);
            if (!UiOpen && !talk && !EscapeMenu.Open && !TestMode.PanelOpen && Input.GetKeyDown(dropKey)) DropHeld();
'@
Rep '                bool fits = !tooMuchArmour && w + add <= capacity + 0.001f;' @'
                bool atLimit = target.TryGetComponent(out ConsumableItem canAimed) && CountConsumable(canAimed.kind) >= ConsumableRules.Of(canAimed.kind).limit;
                bool fits = !tooMuchArmour && !atLimit && w + add <= capacity + 0.001f;
'@
Rep '                    : tooMuchArmour ? $"{target.Label}: {ArmourRules.TooMuchArmour}"' @'
                    : tooMuchArmour ? $"{target.Label}: {ArmourRules.TooMuchArmour}"
                    : atLimit ? $"{target.Label}: {ConsumableRules.AtLimit(canAimed.kind)}"
'@
Rep 'const int ActNone = 0, ActDrop = 1, ActEquip = 2, ActTake = 3;' 'const int ActNone = 0, ActDrop = 1, ActEquip = 2, ActTake = 3, ActUse = 4;'
Rep '                if (MenuKit.Button(dropRect, "DROP", HudTheme.Warning, 19f)) act = ActDrop;' @'
                if (MenuKit.Button(dropRect, "DROP", HudTheme.Warning, 19f)) act = ActDrop;
                if (ConsumableItem.IsConsumable(p))
                {
                    var useRect = new Rect(x - 110f * s, by, 110f * s, bh);                              // (the owner, 2026-10-25: the consumables are drunk from here or with their number key)
                    x = useRect.x - gap;
                    if (MenuKit.Button(useRect, "USE", HudTheme.Good, 19f)) act = ActUse;
                }
'@
Rep '            else if (action == ActTake && picked) { TryStore(picked); nearby.Remove(picked); }' @'
            else if (action == ActUse && picked) UseConsumable(picked);
            else if (action == ActTake && picked) { TryStore(picked); nearby.Remove(picked); }
'@
Save-Src

# ---- the body: the speed burst's factor, silent steps
Open-Src ($A + 'Scripts\WobblyPlayer.cs')
Rep '        public bool IsSprinting { get; private set; }' @'
        public bool IsSprinting { get; private set; }
        /// <summary>The walking speed is multiplied by this (1 = none): the speed burst (<see cref="PlayerBuffs"/>, the owner, 2026-10-25).</summary>
        public float SpeedBuff { get; set; } = 1f;
        /// <summary>The steps make no sound and no noise for the guards: silent steps (<see cref="PlayerBuffs"/>, the owner, 2026-10-25). Set on every machine, puppets included.</summary>
        public bool StepsSilent { get; set; }
'@
Rep 'public float CurrentFootstepNoise => IsSprinting ? sprintNoise : IsCrouching ? crouchNoise : footstepNoise;' 'public float CurrentFootstepNoise => StepsSilent ? 0f : IsSprinting ? sprintNoise : IsCrouching ? crouchNoise : footstepNoise;'
Rep '                float gait = (IsSprinting ? sprintMultiplier : IsCrouching ? crouchSpeedMultiplier : 1f) * DragFactor;' '                float gait = (IsSprinting ? sprintMultiplier : IsCrouching ? crouchSpeedMultiplier : 1f) * DragFactor * SpeedBuff;'
Rep '            if (!GetComponent<Stamina>()) gameObject.AddComponent<Stamina>();' @'
            if (!GetComponent<Stamina>()) gameObject.AddComponent<Stamina>();
            if (!GetComponent<PlayerBuffs>()) gameObject.AddComponent<PlayerBuffs>();                              // (the consumables in use: the owner, 2026-10-25; after the stamina meter, which it hands the drain factor)
'@
Save-Src

Open-Src ($A + 'Scripts\FootstepPlayer.cs')
Rep '            var cat = GameAudio.Catalog;' @'
            if (player && player.StepsSilent) return;                                  // (silent steps: the owner, 2026-10-25)
            var cat = GameAudio.Catalog;
'@
Save-Src

# ---- the guards' eyes: an invisible player is not seen
Open-Src ($A + 'Scripts\GuardAI.cs')
Rep '            if (p.Custody != CustodyState.Free) return false; // pinned or arrested players are not what he is looking for' @'
            if (p.Custody != CustodyState.Free) return false; // pinned or arrested players are not what he is looking for
            if (PlayerBuffs.IsInvisible(p)) return false;     // the "invisible" consumable (the owner, 2026-10-25): nothing of him is seen, so his notice meter never fills and no chase starts
'@
Save-Src

# ---- the guns' spread: the accuracy cola
Open-Src ($A + 'Scripts\Guns\GunController.cs')
RepRx 'GunRules\.Spread\(GunRules\.PelletSeed\(seed, i\), spec\.spread, out' 'GunRules.Spread(GunRules.PelletSeed(seed, i), spec.spread * PlayerBuffs.SpreadFactor(gameObject), out' 2
Save-Src
Open-Src ($A + 'Scripts\Net\RunSync.Guns.cs')
RepRx 'GunRules\.Spread\(GunRules\.PelletSeed\(seed, i\), spec\.spread, out' 'GunRules.Spread(GunRules.PelletSeed(seed, i), spec.spread * PlayerBuffs.SpreadFactor(np.gameObject), out' 1
Save-Src

# ---- the round: one for every floor the party enters (invisible is used once for each)
Open-Src ($A + 'Scripts\HeistManager.cs')
Rep '        public void StartFloor(int floor, int totalFloors, int quota)' @'
        /// <summary>How many floors the party has entered (the docks do not count): "once a round" (the owner, 2026-10-25: the invisible consumable) is once for each.</summary>
        public int Round { get; private set; }

        public void StartFloor(int floor, int totalFloors, int quota)
'@
Rep '            Floor = floor; TotalFloors = totalFloors; Quota = quota;
            Banked = Lost = Caught = 0;' @'
            Floor = floor; TotalFloors = totalFloors; Quota = quota;
            if (floor > 0) Round++;
            Banked = Lost = Caught = 0;
'@
Save-Src

# ---- the machine's screen: nine rows now, so they shrink to fit the window; and how many of each the pack holds
Open-Src ($A + 'Scripts\Hud\VendingScreen.cs')
Rep '            float pw = Mathf.Min(560f * s, Screen.width - 40f * s), pad = 22f * s, rowH = 58f * s, gap = 9f * s, headH = 112f * s, footH = 112f * s;' @'
            float pw = Mathf.Min(560f * s, Screen.width - 40f * s), pad = 22f * s, gap = 9f * s, headH = 112f * s, footH = 112f * s;
            float rowH = Mathf.Clamp((Screen.height - 32f * s - headH - footH - pad) / Mathf.Max(1, products.Length) - gap, 30f * s, 58f * s);          // (nine products since 2026-10-25: the rows shrink to fit a short window)
            var minePack = LocalPlayer() ? LocalPlayer().GetComponent<Backpack>() : null;
'@
Rep '                    HudTheme.Text(new Rect(r.xMax - 170f * s, r.y, 150f * s, r.height), HudTheme.Money(p.price), 22f, can ? Ink : InkDim, TextAnchor.MiddleRight);' @'
                    HudTheme.Text(new Rect(r.xMax - 170f * s, r.y, 150f * s, r.height), HudTheme.Money(p.price), 22f, can ? Ink : InkDim, TextAnchor.MiddleRight);
                    if (p.isConsumable && minePack) HudTheme.Text(new Rect(r.xMax - 250f * s, r.y, 70f * s, r.height), minePack.CountConsumable(p.consumable) + "/" + ConsumableRules.Of(p.consumable).limit, 16f, InkDim, TextAnchor.MiddleRight);          // (what the pack holds / the limit)
'@
Save-Src

Open-Src ($A + 'Scripts\HowToPlayContent.cs')
Rep '            ("Esc", "Menu"),' @'
            ("7 8 9 0 -", "Use a consumable you bought at a vending machine: speed burst, endurance laster, silent steps, invisible, accuracy cola (or the USE button of the Tab window)"),
            ("Esc", "Menu"),
'@
Save-Src

# ---- the built-player self-play: buy two at a machine, drink them, see the effects
Open-Src ($A + 'Scripts\QaAutopilot.cs')
Rep '            yield return PackActs("solo");' @'
            yield return PackActs("solo");
            yield return ConsumableActs();
'@
Rep '        IEnumerator PackActs(string who)' @'
        /// <summary>The consumables of the vending machines in the built game (the owner, 2026-10-25): two are bought at a machine, drunk, and their effects are there; the effects are cleared afterwards so that nothing later in the run is changed by them.</summary>
        IEnumerator ConsumableActs()
        {
            var wob = WobblyPlayerRegistry.All.Count > 0 ? WobblyPlayerRegistry.All[0] : null;
            var pack = wob != null ? wob.GetComponent<Backpack>() : null;
            var buffs = wob != null ? wob.GetComponent<PlayerBuffs>() : null;
            var machine = VendingMachine.Machines.Count > 0 ? VendingMachine.Machines[0] : null;
            var vendor = Vendor.Current;
            var hm = HeistManager.Instance;
            if (wob == null || pack == null || buffs == null || machine == null || vendor == null || hm == null) { Check("solo: a vending machine, my pack and the dealer are here for the consumables", false); yield break; }
            hm.AddCash(2000);
            wob.TeleportTo(machine.TrayPoint + Vector3.up * 0.6f, wob.transform.rotation);
            yield return Wait(1.0f);
            int speed = System.Array.FindIndex(Vendor.Catalog, p => p.isConsumable && p.consumable == ConsumableKind.SpeedBurst);
            int silent = System.Array.FindIndex(Vendor.Catalog, p => p.isConsumable && p.consumable == ConsumableKind.SilentSteps);
            string said1 = vendor.BuyAtMachine(hm, pack, speed, machine.TrayPoint), said2 = vendor.BuyAtMachine(hm, pack, silent, machine.TrayPoint);
            yield return Wait(0.5f);
            Check("solo: the vending machine sells the five consumables, and a speed burst and silent steps bought there are in my pack", Vendor.MachineProducts.Length >= 9 && pack.CountConsumable(ConsumableKind.SpeedBurst) == 1 && pack.CountConsumable(ConsumableKind.SilentSteps) == 1, $"('{said1}' / '{said2}')");
            pack.UseKind(ConsumableKind.SpeedBurst);
            pack.UseKind(ConsumableKind.SilentSteps);
            yield return Wait(0.6f);
            Check("solo: drunk, the cans are gone, my speed is up and my steps are silent", pack.CountConsumable(ConsumableKind.SpeedBurst) == 0 && pack.CountConsumable(ConsumableKind.SilentSteps) == 0 && wob.SpeedBuff > 1f && wob.StepsSilent && wob.CurrentFootstepNoise == 0f, $"(speed {wob.SpeedBuff:0.0}, silent {wob.StepsSilent})");
            Check("solo: the HUD row above the weapon bar shows the two that run", ConsumableHud.LastChips >= 2, $"({ConsumableHud.LastChips} chips)");
            yield return Shot("02c_consumables");
            buffs.Set.Clear();
            yield return Wait(0.3f);
        }

        IEnumerator PackActs(string who)
'@
Save-Src

# ---- the test harness: the two scenarios, and the pure test
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void PropScan()' @'
        public static void Consumables()
        {
            SessionState.SetString("netdev.mode", "consumables");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void ConsumablesNet() => Start("consumablesnet");
        public static void PropScan()
'@
Rep ' && mode != "alarmlooks" && ' ' && mode != "alarmlooks" && mode != "consumables" && '
Rep '                case "alarmlooks": yield return AlarmLooksScenario(); break;' @'
                case "alarmlooks": yield return AlarmLooksScenario(); break;
                case "consumables": yield return ConsumablesScenario(); break;
                case "consumablesnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? ConsumablesHostScenario() : ConsumablesClientScenario(); break;
'@
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevTest.cs')
Rep '            DockPosterRulesTest();' @'
            DockPosterRulesTest();
            ConsumablesRulesTest();
'@
Save-Src

# ---- tests that said the walkie talkie is the last entry of the dealer's list or that the machines sell four things
Open-Src ($A + 'Editor\NetDev\NetDevAsks1023VTest.cs')
Rep 'sold.Length == 4 && sold.SequenceEqual(new[] { 0, 1, 2, 3 })' 'sold.Length == 4 + ConsumableRules.Count && sold.Take(4).SequenceEqual(new[] { 0, 1, 2, 3 })'
Rep 'cat.Count(p => p.machine) == 4);' 'cat.Count(p => p.machine) == 4 + ConsumableRules.Count);'
Rep '17 entries in all: the walkie talkie is the last)", Vendor.PewIndex == 7 && Vendor.AmmoIndex == 8 && cat.Length == 17 && Vendor.WalkieIndex == 16' '17 entries up to the walkie talkie, the five consumables of 2026-10-25 come after it)", Vendor.PewIndex == 7 && Vendor.AmmoIndex == 8 && cat.Length == 17 + ConsumableRules.Count && Vendor.WalkieIndex == 16'
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevAsks1023GTest.cs')
Rep 'idx == Vendor.Catalog.Length - 1 && Vendor.Catalog[idx].isWalkie' 'idx == 16 && Vendor.Catalog[idx].isWalkie'
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevArmourTest.cs')
Rep 'ih == Vendor.Catalog.Length - 2 && Vendor.Catalog[Vendor.Catalog.Length - 1].isWalkie' 'ih == Vendor.WalkieIndex - 1 && Vendor.Catalog[Vendor.WalkieIndex].isWalkie'
Save-Src
"consumables patched"
