# The fixes after the owner's playtest with friends (2026-10-23):
#  1. "a player was pushed to the roof of the building, out of the map, because too many guards pushed him into a corner": the safety net under the body (PlayerSafetyRules, WobblyPlayer.KeepInTheBuilding): the push out of an overlap is slow,
#     the up speed is cut, a body outside the building is put back at the last spot it stood on (or at the elevator).
#  2. "Players need nametags above their heads": NetPlayer.DisplayName (written by the owner of the body, his Steam name, cleaned by every reader: NameRules) and NameTags (drawn over the heads of the others).
# Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept; new files are written with CRLF.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 'playtest_new\NameRules.cs') ($A + 'Scripts\Net\NameRules.cs')
Put-Crlf (Join-Path $here 'playtest_new\NameTags.cs') ($A + 'Scripts\Hud\NameTags.cs')
Put-Crlf (Join-Path $here 'playtest_new\PlayerSafetyRules.cs') ($A + 'Scripts\PlayerSafetyRules.cs')
Put-Crlf (Join-Path $here 'playtest_new\NetDevPlaytestFixes.cs') ($A + 'Editor\NetDev\NetDevPlaytestFixes.cs')

# ---- the name a player writes for himself
Open-Src ($A + 'Scripts\Net\NetPlayer.cs')
Rep '        // Hiding at a desk, written by the owner:' @'
        // The name over his head (the owner, 2026-10-23: "Players need nametags above their heads"), written by the owner of the body (his Steam name; empty without Steam), read cleaned (NameRules) by everybody.
        readonly NetworkVariable<FixedString64Bytes> displayName = new(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Hiding at a desk, written by the owner:
'@
Rep '        public int Slot => slot.Value;' @'
        public int Slot => slot.Value;

        /// <summary>The name this player's own machine wrote for him, cleaned (<see cref="NameRules.Clean"/>); "" when he has none (<see cref="NameRules.Tag"/> then says "Player n").</summary>
        public string DisplayName => NameRules.Clean(displayName.Value.ToString());

        /// <summary>The owner of this body writes his name (cleaned first).</summary>
        public void SetDisplayName(string raw) { if (IsOwner) displayName.Value = NameRules.Clean(raw); }
'@
Rep 'else ApplyLook(look.Value.ToString());' @'
else ApplyLook(look.Value.ToString());
            if (IsOwner) { var steam = SteamService.Instance; SetDisplayName(steam != null && steam.Ready ? steam.LocalName : ""); }
'@
Save-Src

# ---- the safety net under the body
Open-Src ($A + 'Scripts\WobblyPlayer.cs')
Rep 'rb = GetComponent<Rigidbody>(); rb.maxAngularVelocity = 12f; cam = Camera.main ? Camera.main.transform : null;' @'
rb = GetComponent<Rigidbody>(); rb.maxAngularVelocity = 12f; cam = Camera.main ? Camera.main.transform : null;
            rb.maxDepenetrationVelocity = PlayerSafetyRules.MaxDepenetrationSpeed;          // (the friends' playtest, 2026-10-23: officers squeezing a body into a corner threw it up through the ceiling: the push out of an overlap is slow now)
'@
Rep '            rb.position = position;' @'
            rb.position = position;
            haveSafeSpot = false;                                                      // (a good spot of the place he was before is no spot to come back to)
'@
Rep @'
        void FixedUpdate()
        {
            grounded = CheckGrounded();
            if (rb.isKinematic) return;
'@ @'
        // ---- the safety net (the friends' playtest, 2026-10-23: "a player was pushed to the roof of the building, out of the map, because too many guards pushed him into a corner"): the body is never left outside the building
        Vector3 safeSpot; bool haveSafeSpot; float nextSafeSpot;
        /// <summary>How many times the net put this body back (tests, the log).</summary>
        public int Rescues { get; private set; }

        void KeepInTheBuilding()
        {
            if (rb.isKinematic) return;
            var p = rb.position;
            if (PlayerSafetyRules.Outside(p.y))
            {
                Vector3 to;
                if (haveSafeSpot) to = safeSpot + Vector3.up * 0.1f;
                else
                {
                    var tower = TowerManager.Instance;
                    to = (tower != null && tower.HasFloor ? tower.CurrentInfo.spawn : new Vector3(p.x, 0f, p.z)) + Vector3.up * 1.1f;
                }
                Rescues++;
                Debug.LogWarning($"[Safety] a body was found outside the building at {p}: put back at {to}");
                TeleportTo(to, transform.rotation);
                safeSpot = to; haveSafeSpot = true; nextSafeSpot = Time.time + PlayerSafetyRules.RememberEvery;
                return;
            }
            var v = rb.linearVelocity;
            if (v.y > PlayerSafetyRules.MaxUpSpeed) { v.y = PlayerSafetyRules.MaxUpSpeed; rb.linearVelocity = v; }          // (a jump is 5.5: nothing but a squeeze goes faster)
            if (Time.time >= nextSafeSpot && PlayerSafetyRules.WorthRemembering(grounded, p.y, v.magnitude)) { safeSpot = p; haveSafeSpot = true; nextSafeSpot = Time.time + PlayerSafetyRules.RememberEvery; }
        }

        void FixedUpdate()
        {
            grounded = CheckGrounded();
            KeepInTheBuilding();
            if (rb.isKinematic) return;
'@
Save-Src

# ---- the tests
Open-Src ($A + 'Editor\NetDev\NetDevTest.cs')
Rep '            PvpBattlefieldTest();' @'
            PvpBattlefieldTest();
            PlaytestFixesTest();
'@
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void PvpMap()' @'
        public static void SafetyNet()
        {
            SessionState.SetString("netdev.mode", "safetynet");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void NameTagNet() => Start("nametagnet");
        public static void PvpMap()
'@
Rep '&& mode != "pvpbattlefieldui")' '&& mode != "pvpbattlefieldui" && mode != "safetynet")'
Rep '                case "pvpbattlefieldui": yield return PvpBattlefieldUiScenario(); break;' @'
                case "pvpbattlefieldui": yield return PvpBattlefieldUiScenario(); break;
                case "safetynet": yield return SafetyNetScenario(); break;
                case "nametagnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? NameTagHostScenario() : NameTagClientScenario(); break;
'@
Save-Src
"playtest fixes patched"
