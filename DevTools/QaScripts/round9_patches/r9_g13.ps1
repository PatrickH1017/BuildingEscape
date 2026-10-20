# Round 9, batch G13 (the four guns): the Play Mode check of the guns (Guns), the screen pictures (GunUi), the demo scenario's clip purchase under the three-clip limit, and the protocol number ("21": the shot, the ammunition announcement and the safes changed).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor'

foreach ($pair in @(@('NetDevGuns.cs', 'C:\bqs\gun_draft\NetDevGuns.cs'), @('NetDevGunUi.cs', 'C:\bqs\gun_draft\NetDevGunUi.cs'))) {
  $t = [IO.File]::ReadAllText($pair[1]).Replace("`r`n", "`n").TrimEnd("`n").Replace("`n", "`r`n") + "`r`n"
  $target = "$Ed\NetDev\$($pair[0])"
  if ($env:R2_DRY) { Write-Output "dry run: would write $target" } else { [IO.File]::WriteAllText($target, $t, (New-Object Text.UTF8Encoding($false))); Write-Output "written $target" }
}

Open-Src "$Ed\NetDev\NetDevHarness.cs"
Rep @'
        public static void GunSafes()
        {
'@ @'
        public static void Guns()
        {
            SessionState.SetString("netdev.mode", "guns");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void GunUi()
        {
            SessionState.SetString("netdev.mode", "gunui");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void GunSafes()
        {
'@
Rep @'
                case "gunsafes": yield return GunSafesScenario(); break;
'@ @'
                case "gunsafes": yield return GunSafesScenario(); break;
                case "guns": yield return GunsScenario(); break;
                case "gunui": yield return GunUiScenario(); break;
'@
Rep 'mode != "gunsafes" && mode != "ui"' 'mode != "gunsafes" && mode != "guns" && mode != "gunui" && mode != "ui"'
Save-Src

Open-Src "$Ed\NetDev\NetDevDemo.cs"
Rep @'
            int cashClip = hm.Cash;
            string clip = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("a clip costs its price and puts eight rounds in the reserve of the pew", clip.Contains("Bought a clip") && cashClip - hm.Cash == Vendor.AmmoClipPrice && gc.Gun.Magazine.Reserve == 24, $"(\"{clip}\", cash {cashClip} -> {hm.Cash}, reserve {gc.Gun.Magazine.Reserve})");
            hm.TrySpend(hm.Cash);
            string brokeClip = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("with no cash left no clip is sold, and the reserve stays", brokeClip.Contains("Not enough cash") && gc.Gun.Magazine.Reserve == 24, $"(\"{brokeClip}\")");
'@ @'
            int cashClip = hm.Cash;
            string fullPack = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("a pew with its clip and two spare ones is carrying the three clips a player may (the owner, 2026-10-19): the dealer refuses a fourth and takes no money", fullPack.Contains("three clips") && hm.Cash == cashClip && gc.Gun.Magazine.Reserve == 16, $"(\"{fullPack}\")");
            gc.Gun.Magazine.SetReserve(8);                                               // (a spare clip used up: there is room for one)
            string clip = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("a clip costs its price and puts eight rounds in the reserve of the pew", clip.Contains("Bought a clip") && cashClip - hm.Cash == Vendor.AmmoClipPrice && gc.Gun.Magazine.Reserve == 16, $"(\"{clip}\", cash {cashClip} -> {hm.Cash}, reserve {gc.Gun.Magazine.Reserve})");
            hm.TrySpend(hm.Cash);
            gc.Gun.Magazine.SetReserve(8);
            string brokeClip = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("with no cash left no clip is sold, and the reserve stays", brokeClip.Contains("Not enough cash") && gc.Gun.Magazine.Reserve == 8, $"(\"{brokeClip}\")");
'@
Save-Src

Open-Src "$Src\Net\NetConst.cs"
Rep 'public const string ProtocolVersion = "20";   // 20:' 'public const string ProtocolVersion = "21";   // 21: the guns of the pack (a shot carries its seed and the host traces every pellet; ammunition is announced with its gun; a safe holds the gun of its floor and its spare clip is for that gun, so old and new cannot share a run); 20:'
Save-Src
