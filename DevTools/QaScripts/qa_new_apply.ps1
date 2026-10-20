# Puts the Q/A and build tooling into a project tree: new files (QaAutopilot, DemoBuild, NetDevLootQa) and small patches (BootLoader keeps the game
# running in the background, NetDevHarness gets the LootQa entry). Safe to run twice. Keeps CRLF.
# Usage: .\qa_new_apply.ps1 -Tree <project root> [-Backup <folder>]     (variable names $s / $path / $bom / $eol belong to patchlib)
param([Parameter(Mandatory = $true)][string]$Tree, [string]$Backup = '')
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$here\patchlib.ps1"
$new = "$here\qa_new"
function Backup-File([string]$rel) {
  if ($Backup -eq '') { return }
  $f = Join-Path $Tree $rel
  if (Test-Path $f) { $b = Join-Path $Backup $rel; New-Item -ItemType Directory -Force (Split-Path $b) | Out-Null; Copy-Item $f $b -Force }
}
function Put-File([string]$from, [string]$rel) {
  $dst = Join-Path $Tree $rel
  Backup-File $rel
  $text = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
  [IO.File]::WriteAllText($dst, $text, (New-Object System.Text.UTF8Encoding($false)))
  "wrote   $rel"
}
Put-File "$new\QaAutopilot.cs" 'Assets\Scripts\QaAutopilot.cs'
Put-File "$new\DemoBuild.cs" 'Assets\Editor\DemoBuild.cs'
Put-File "$new\NetDevLootQa.cs" 'Assets\Editor\NetDev\NetDevLootQa.cs'
Put-File "$new\NetDevMeleeNet.cs" 'Assets\Editor\NetDev\NetDevMeleeNet.cs'

# ---- BootLoader: the game keeps running when its window is not in front
Backup-File 'Assets\Scripts\Net\BootLoader.cs'
Open-Src "$Tree\Assets\Scripts\Net\BootLoader.cs"
if ($script:s.Contains('runInBackground')) { "BootLoader already keeps the game running" }
else {
  Rep @'
    public class BootLoader : MonoBehaviour
    {
        IEnumerator Start()
'@ @'
    public class BootLoader : MonoBehaviour
    {
        /// <summary>
        /// A networked game must keep running when its window is not in front: the project's own setting is "off", so a host who alt-tabs
        /// (to chat, to Steam) would freeze the whole team, and voice chat would stop. Set before the first scene loads, so it also holds
        /// when a scene other than Boot is played in the Editor.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void KeepRunningInTheBackground() => Application.runInBackground = true;

        IEnumerator Start()
'@
  Save-Src
}

# ---- NetDevHarness: LootQa
Backup-File 'Assets\Editor\NetDev\NetDevHarness.cs'
Open-Src "$Tree\Assets\Editor\NetDev\NetDevHarness.cs"
if ($script:s.Contains('LootQaScenario')) { "NetDevHarness already has LootQa" }
else {
  Rep @'
        public static void DemoNet() => Start("demonet");
'@ @'
        public static void DemoNet() => Start("demonet");
        public static void LootQa()
        {
            SessionState.SetString("netdev.mode", "lootqa");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
'@
  Rep 'mode != "pewloot" && mode != "demo" && mode != "demoui")' 'mode != "pewloot" && mode != "demo" && mode != "demoui" && mode != "lootqa")'
  Rep @'
                case "demoui": yield return DemoUiScenario(); break;
'@ @'
                case "demoui": yield return DemoUiScenario(); break;
                case "lootqa": yield return LootQaScenario(); break;
'@
  Save-Src
}
Open-Src "$Tree\Assets\Editor\NetDev\NetDevHarness.cs"
if ($script:s.Contains('MeleeHostScenario')) { "NetDevHarness already has MeleeNet" }
else {
  Rep @'
        public static void DemoNet() => Start("demonet");
'@ @'
        public static void DemoNet() => Start("demonet");
        public static void MeleeNet() => Start("meleenet");
'@
  Rep @'
                case "demoui": yield return DemoUiScenario(); break;
'@ @'
                case "demoui": yield return DemoUiScenario(); break;
                case "meleenet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? MeleeHostScenario() : MeleeClientScenario(); break;
'@
  Save-Src
}
# ---- the 5-player Qa: three expectations the demo made out of date
Backup-File 'Assets\Editor\NetDev\NetDevQa.cs'
Open-Src "$Tree\Assets\Editor\NetDev\NetDevQa.cs"
if ($script:s.Contains('a backup door (the guards') -and $script:s.Contains('attempt < 4')) { "NetDevQa already knows the stairwell doors, the demo limit and the walking guard" }
elseif ($script:s.Contains('a backup door (the guards')) {
  # (a tree that already got the first two patches)
  Rep @'
                var g = FirstGuard();
                Teleport(g.transform.position + new Vector3(1.4f, 0.5f, 0f));
                yield return Settle(1.5f);
                g = NetIds.Get<GuardAI>(g.NetId);
                g.WeaponHit(4f, Mine.gameObject);
                QaSignal("hit_id", g.NetId.ToString());
'@ @'
                var g = FirstGuard();
                for (int attempt = 0; attempt < 4; attempt++)       // (the guard walks on while the player waits: stand next to where he is now and swing at once; the host judges the distance)
                {
                    Teleport(g.transform.position + new Vector3(1.4f, 0.5f, 0f));
                    yield return Settle(0.5f);
                    g = NetIds.Get<GuardAI>(g.NetId);
                    g.WeaponHit(4f, Mine.gameObject);
                    yield return Settle(0.8f);
                    if (g.State == GuardState.Stunned) break;
                }
                QaSignal("hit_id", g.NetId.ToString());
'@
  Save-Src
}
else {
  Rep 'QaSignal("guest_open", gen.GuestDoorOpen ? "1" : "0");' 'QaSignal("guest_open", gen.EntryDoorMask != 0 ? "1" : "0");     // (backup comes out of the guards'' elevator or a stairwell)'
  Rep 'if (!host) Check("the guest elevator door opened on my machine", FindFirstObjectByType<FloorGenerator>().GuestDoorOpen);' 'if (!host) Check("a backup door (the guards'' elevator or a stairwell) opened on my machine", FindFirstObjectByType<FloorGenerator>().EntryDoorMask != 0);'
  Rep @'
                var g = FirstGuard();
                Teleport(g.transform.position + new Vector3(1.4f, 0.5f, 0f));
                yield return Settle(1.5f);
                g = NetIds.Get<GuardAI>(g.NetId);
                g.WeaponHit(4f, Mine.gameObject);
                QaSignal("hit_id", g.NetId.ToString());
'@ @'
                var g = FirstGuard();
                for (int attempt = 0; attempt < 4; attempt++)       // (the guard walks on while the player waits: stand next to where he is now and swing at once; the host judges the distance)
                {
                    Teleport(g.transform.position + new Vector3(1.4f, 0.5f, 0f));
                    yield return Settle(0.5f);
                    g = NetIds.Get<GuardAI>(g.NetId);
                    g.WeaponHit(4f, Mine.gameObject);
                    yield return Settle(0.8f);
                    if (g.State == GuardState.Stunned) break;
                }
                QaSignal("hit_id", g.NetId.ToString());
'@
  Rep @'
            yield return Barrier("hidden_in", remaining);
            if (role == "c1") RunSync.Instance.RequestPanel(1);
'@ @'
            yield return Barrier("hidden_in", remaining);
            // the demo's hidden elevator does not go to the boss floor (TowerRules.DemoLastFloor): the host's test hook does, so the boss logic is still tested
            if (TowerRules.Beyond(TowerRules.BossFloor)) { if (host) tm.GoToLevel(TowerRules.BossFloor); }
            else if (role == "c1") RunSync.Instance.RequestPanel(1);
'@
  Save-Src
}
# ---- Steam voice: once Steam has been shut down there is nothing left to stop (SteamService.OnApplicationQuit runs before VoiceChat.OnDestroy:
# the built player logged "InvalidOperationException: Steamworks is not initialized" at every quit of a host that had been talking)
Backup-File 'Assets\Scripts\Net\Voice\VoiceCodec.cs'
Open-Src "$Tree\Assets\Scripts\Net\Voice\VoiceCodec.cs"
if ($script:s.Contains('SteamService.Instance')) { "VoiceCodec already skips Steam calls after Steam is shut down" }
else {
  Rep @'
            if (on == recording) return;
            recording = on;
            if (on) SteamUser.StartVoiceRecording(); else SteamUser.StopVoiceRecording();
'@ @'
            if (on == recording) return;
            recording = on;
            var steam = SteamService.Instance;
            if (steam == null || !steam.Ready) return;       // (SteamService shuts Steam down in OnApplicationQuit, before VoiceChat is destroyed: nothing left to stop)
            if (on) SteamUser.StartVoiceRecording(); else SteamUser.StopVoiceRecording();
'@
  Save-Src
}
"Q/A and build tooling applied to $Tree"
