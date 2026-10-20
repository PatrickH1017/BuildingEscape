# Round 11, batch G05: the built-player network test also checks the armour over the loopback - in the docks the host unlocks Light Armour (what a finder brings back) and gives cash, the client sees the unlock (the dealer offers it), buys the piece at the counter,
# and both machines show it in his pack with the vest on his model.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Scripts\QaAutopilot.cs'
Rep @'
            yield return Shot("n3_host_docks");
'@ @'
            yield return Shot("n3_host_docks");
            yield return ArmourNet(true);
'@
Rep @'
            yield return Shot("n3_client_docks");
'@ @'
            yield return Shot("n3_client_docks");
            yield return ArmourNet(false);
'@
Rep @'
        IEnumerator RunHost()
'@ @'
        /// <summary>
        /// The armour over the network (the owner's queue of 2026-10-19), in the docks: the host unlocks Light Armour (what a finder who is back in the docks does for the party) and gives cash; the client sees the unlock - the dealer offers the piece -
        /// buys it at the counter, and both machines show it in his pack, worn, with the vest of its tier on his model.
        /// </summary>
        IEnumerator ArmourNet(bool host)
        {
            string who = host ? "host" : "client";
            var light = ShieldTier.Light;
            int il = System.Array.FindIndex(Vendor.Catalog, p => p.isArmour && p.armour == light);
            int price = ArmourRules.Price(light);
            if (host)
            {
                var hm = HeistManager.Instance;
                int cash0 = hm.Cash + 5000;
                hm.AddCash(5000);
                RunUnlocks.UnlockArmour(light);                                          // (the host's mask is mirrored to the client)
                NetPlayer remote = null;
                foreach (var np in NetPlayer.All) if (np && !np.IsOwner) remote = np;
                var rPack = remote != null ? remote.GetComponent<Backpack>() : null;
                yield return Until(() => rPack != null && PlayerArmour.TierOf(rPack) == light, 120f, "the client to buy Light Armour");
                yield return Wait(1.2f);
                var rVis = remote.GetComponent<PlayerArmourVisual>();
                Check($"{who}: the client bought Light Armour at the counter - it is in his pack here too, he wears it, his model shows the vest, and the dealer took its price", PlayerArmour.TierOf(rPack) == light && rVis != null && rVis.Shown == light && hm.Cash == cash0 - price,
                    $"(worn {PlayerArmour.TierOf(rPack)}, vest {(rVis != null ? rVis.Shown.ToString() : "none")}, cash {cash0} -> {hm.Cash})");
            }
            else
            {
                yield return Until(() => RunUnlocks.HasArmour(light) && HeistManager.Instance != null && HeistManager.Instance.Cash >= price, 90f, "the host's unlock and cash");
                Check($"{who}: the host's unlock reached me - the dealer offers Light Armour and not the others", Vendor.OnOffer(il, 1) && !Vendor.OnOffer(il + 1, 1) && !Vendor.OnOffer(il + 2, 1));
                var wob = NetPlayer.Local != null ? NetPlayer.Local.GetComponent<WobblyPlayer>() : null;
                var rb = wob != null ? wob.GetComponent<Rigidbody>() : null;
                var myPack = wob != null ? wob.GetComponent<Backpack>() : null;
                var counter = Vendor.Current != null ? Vendor.Current.GetComponent<Collider>() : null;
                if (wob == null || rb == null || myPack == null || counter == null) { Check($"{who}: my body and the dealer's counter are here", false); yield break; }
                wob.TeleportTo(counter.bounds.center + Vector3.up * 0.2f, wob.transform.rotation);           // (the host sells only to somebody at the counter)
                rb.linearVelocity = Vector3.zero;
                yield return Wait(2.5f);                                                 // (the host's copy of me has to arrive)
                RunSync.Instance.RequestBuy(il);
                yield return Until(() => PlayerArmour.TierOf(myPack) == light, 60f, "Light Armour in my pack");
                yield return Wait(1.2f);
                var myVis = wob.GetComponent<PlayerArmourVisual>();
                Check($"{who}: I bought Light Armour - it is in my pack, I wear it, and the vest is on my model", PlayerArmour.TierOf(myPack) == light && myVis != null && myVis.Shown == light, $"(worn {PlayerArmour.TierOf(myPack)}, vest {(myVis != null ? myVis.Shown.ToString() : "none")})");
            }
        }

        IEnumerator RunHost()
'@
Save-Src
