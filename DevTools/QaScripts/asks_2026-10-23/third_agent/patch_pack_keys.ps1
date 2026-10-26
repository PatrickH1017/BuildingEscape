# The owner's playtest of 2026-10-23: "players couldn't unequip and re-equip their melee weapons and they couldn't drop them, there were just problems all over". The weapon in hand could only be put away or dropped with the buttons of the Tab window (the number key of a
# slot only took the weapon IN hand, never out again, and there was no key to drop it): now the number key of the weapon that is in hand puts it away (and again takes it out), and G drops the weapon in hand (not while a screen is open: the elevator's G is a button of its own panel).
# Both go through the pack's own calls (SetEquipped / Drop: on a client the host decides), so they work in a networked run like the buttons. Also the scenario PackKeys (one process) that tests them.
# Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

Open-Src ($Root + 'Scripts\Backpack.cs')
Rep '        public KeyCode inventoryKey = KeyCode.Tab;' @'
        public KeyCode inventoryKey = KeyCode.Tab;
        /// <summary>Drops the weapon in hand (the owner's playtest, 2026-10-23: "players ... couldn't drop them"); the Tab window's DROP did it only.</summary>
        public KeyCode dropKey = KeyCode.G;
'@
Rep @'
        /// <summary>Takes the weapon of a bar slot in hand (the keys 1 to 6); false for an empty slot, which changes nothing.</summary>
'@ @'
        /// <summary>
        /// The number key of a bar slot (the owner's playtest, 2026-10-23: "players couldn't unequip and re-equip their melee weapons"): the weapon of the slot comes into the hand, and when it IS in the hand already the key puts it away again.
        /// False for an empty slot, which changes nothing.
        /// </summary>
        public bool PressSlot(int slot)
        {
            var p = BarWeapon(slot);
            if (!p) return false;
            SetEquipped(p == Equipped ? null : p);
            return true;
        }

        /// <summary>Drops the weapon in hand (the drop key); false when the hands are empty.</summary>
        public bool DropHeld()
        {
            if (!Equipped) return false;
            Drop(Equipped);
            return true;
        }

        /// <summary>Takes the weapon of a bar slot in hand (the keys 1 to 6); false for an empty slot, which changes nothing.</summary>
'@
Rep '                    if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d)) SelectSlot(WeaponBarRules.SlotOfKey(d));' @'
                    if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d)) PressSlot(WeaponBarRules.SlotOfKey(d));
            if (!UiOpen && !talk && !EscapeMenu.Open && !TestMode.PanelOpen && Input.GetKeyDown(dropKey)) DropHeld();
'@
Save-Src

# ---- the scenario
Open-Src ($Root + 'Editor\NetDev\NetDevPlaytestFixes.cs')
Rep '        string TagFor(int slot) =>' @'
        /// <summary>One process: the pack's keys. A crowbar is bought; its slot key puts it away and takes it out again; the drop key puts it on the floor; with empty hands the drop key does nothing.</summary>
        IEnumerator PackKeysScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true; hm.AddCash(1000);
            yield return new WaitForSeconds(1.0f);
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            Vendor.Current.TestBuy(4);                                                                         // (the crowbar)
            yield return WaitFor(() => pack.Items.Any(i => i && i.GetComponent<MeleeWeapon>()), 20, "the crowbar in the pack");
            var crowbar = pack.Items.First(i => i && i.GetComponent<MeleeWeapon>());
            if (pack.Equipped != crowbar) pack.SetEquipped(crowbar);
            yield return null;
            int slot = pack.BarSlotOf(crowbar);
            PtCheck("keys: the crowbar is in the pack, in hand, and on a slot of the bar", pack.Equipped == crowbar && slot >= 0, $"(slot {slot})");
            PtCheck("keys: the slot key of the weapon in hand puts it away (it stays in the pack)", pack.PressSlot(slot) && pack.Equipped == null && pack.Items.Contains(crowbar));
            PtCheck("keys: the slot key again takes it out", pack.PressSlot(slot) && pack.Equipped == crowbar);
            PtCheck("keys: the key of an empty slot changes nothing", !pack.PressSlot(5) && pack.Equipped == crowbar);
            PtCheck("keys: the drop key drops the weapon in hand: out of the pack, out of the hand, on the floor beside the player",
                pack.DropHeld() && !pack.Items.Contains(crowbar) && pack.Equipped == null && crowbar.gameObject.activeInHierarchy && Vector3.Distance(crowbar.transform.position, player.transform.position) < 3f);
            PtCheck("keys: with empty hands the drop key does nothing", !pack.DropHeld() && pack.Equipped == null);
            Log($"[Playtest] PackKeys DONE fails={ptFails}");
            Quit(ptFails == 0 ? 0 : 1);
        }

        string TagFor(int slot) =>
'@
Save-Src
Open-Src ($Root + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void SafetyNet()' @'
        public static void PackKeys()
        {
            SessionState.SetString("netdev.mode", "packkeys");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void SafetyNet()
'@
Rep '&& mode != "propscan")' '&& mode != "propscan" && mode != "packkeys")'
Rep '                case "propscan": yield return PropScanScenario(); break;' @'
                case "propscan": yield return PropScanScenario(); break;
                case "packkeys": yield return PackKeysScenario(); break;
'@
Save-Src
"pack keys patched"
