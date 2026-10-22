# The ping, round 2: PingSystem.Send / ItemTarget / ShownFrom (for the tests and the two-machine scenario), and the PingNet + PingUi scenarios registered in the harness.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

Open-Src "$Tree\Assets\Scripts\Ping\PingSystem.cs"
if (-not $s.Contains('ItemTarget')) {
Rep @'
            if (!TryAim(origin, direction, player, out var t)) return false;
            Instance.nextLocalPing = Time.unscaledTime + PingRules.Cooldown;
            Made++;
            Instance.Place(LocalSlot, t, true);
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestPing((byte)t.kind, (ushort)Mathf.Max(0, t.id), t.pos);
            return true;
        }
'@ @'
            if (!TryAim(origin, direction, player, out var t)) return false;
            Instance.nextLocalPing = Time.unscaledTime + PingRules.Cooldown;
            Send(t);
            return true;
        }

        /// <summary>Puts this player's marker up on <paramref name="t"/> (here at once) and, in a networked run, asks the host to tell the team.</summary>
        public static void Send(PingTarget t)
        {
            if (!Instance) return;
            Made++;
            Instance.Place(LocalSlot, t, true);
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestPing((byte)t.kind, (ushort)Mathf.Max(0, t.id), t.pos);
        }

        /// <summary>The target a ping on this item would have (the same as the one the crosshair finds): for the tests.</summary>
        public static PingTarget ItemTarget(Pickup p)
        {
            var cat = CategoryOf(p);
            var box = p.TryGetBounds(out var b) ? b : new Bounds(p.transform.position, Vector3.zero);
            return new PingTarget { kind = PingTargetKind.Item, id = p.NetId, pos = box.center + Vector3.up * (box.extents.y + 0.1f), label = PingRules.NameOf(p.Label, cat, false, p.Value, p.Loot != null ? p.Loot.lifters : 1), category = cat };
        }

        static readonly int[] shownFrom = new int[8];

        /// <summary>How many markers have gone up for this player's slot since the game started (the player's own and the ones the host announced), for the tests.</summary>
        public static int ShownFrom(int slot) => shownFrom[Mathf.Clamp(slot, 0, shownFrom.Length - 1)];
'@
Rep 'static void ResetStatics() { Instance = null; rig = null; nextRigLookup = 0f; Made = 0; }' 'static void ResetStatics() { Instance = null; rig = null; nextRigLookup = 0f; Made = 0; System.Array.Clear(shownFrom, 0, shownFrom.Length); }'
Rep '            marks[slot] = m;' @'
            marks[slot] = m;
            shownFrom[Mathf.Clamp(slot, 0, shownFrom.Length - 1)]++;
'@
Save-Src
}

# ---- the scenarios: the master texts are installed and registered in the harness (entry points, the offline list for PingUi, the switch)
foreach ($pair in @(@('NetDevPingNet.cs.txt', 'NetDevPingNet.cs'), @('NetDevPingUi.cs.txt', 'NetDevPingUi.cs'))) {
  $t = [IO.File]::ReadAllText("$PSScriptRoot\$($pair[0])")
  [IO.File]::WriteAllText("$Tree\Assets\Editor\NetDev\$($pair[1])", $t.Replace("`r`n","`n").Replace("`n","`r`n"), (New-Object Text.UTF8Encoding($false)))
}
Open-Src "$Tree\Assets\Editor\NetDev\NetDevHarness.cs"
if (-not $s.Contains('"pingnet"')) {
Rep '        public static void MeleeNet() => Start("meleenet");' @'
        public static void MeleeNet() => Start("meleenet");
        public static void PingNet() => Start("pingnet");
        public static void PingUi()
        {
            SessionState.SetString("netdev.mode", "pingui");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
'@
Rep '&& mode != "asks1021" &&' '&& mode != "asks1021" && mode != "pingui" &&'
Rep '                case "asks1021": yield return Asks1021Scenario(); break;' @'
                case "asks1021": yield return Asks1021Scenario(); break;
                case "pingui": yield return PingUiScenario(); break;
                case "pingnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? PingNetHostScenario() : PingNetClientScenario(); break;
'@
Save-Src
}
"p10 done"
