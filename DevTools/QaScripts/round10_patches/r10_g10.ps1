# Round 10, batch G10: the built-player network test also checks the guard's dropped clip (the clip appears on the client with the host's network id and the client can take it) and the blast (a shotgun's hit on the client's player busts him on both machines and the blast plays on both).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Scripts\QaAutopilot.cs'
Rep @'
                if (floor == 3) yield return SafesNet(true, floor);
'@ @'
                if (floor == 3) { yield return SafesNet(true, floor); yield return GuardsNet(true, floor); }
'@
Rep @'
                if (floor == 3) yield return SafesNet(false, floor);
'@ @'
                if (floor == 3) { yield return SafesNet(false, floor); yield return GuardsNet(false, floor); }
'@
Rep @'
        IEnumerator RunHost()
'@ @'
        /// <summary>
        /// The guards over the network (the owner's queue of 2026-10-19), floor 3, after the safes. The host puts an armed officer down for good: he drops a clip of pistol ammunition, which the client has too (the host's network id) and takes with
        /// E - the host finds it by that id and the rounds go into the client's pistol on both machines. Then the host plays a shotgun's hit on the CLIENT's player (the host stays free, so the team is not down): he is busted at once on both machines
        /// and the blast plays on both.
        /// </summary>
        IEnumerator GuardsNet(bool host, int floor)
        {
            string who = host ? "host" : "client";
            var pewSpec = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew];
            if (host)
            {
                yield return Wait(5f);                                                   // (the client has done with the safes' clip)
                var hm = HeistManager.Instance;
                GuardAI victim = null;
                foreach (var g in GuardAI.All) if (g && !g.IsPuppet && !g.IsBoss && g.Armed && !g.Dead && g.name != "Guard_Outpost") { victim = g; break; }
                Check($"{who}: floor {floor} has an armed patrolling guard to put down", victim != null);
                NetPlayer remote = null;
                foreach (var np in NetPlayer.All) if (np && !np.IsOwner) remote = np;
                var rPack = remote != null ? remote.GetComponent<Backpack>() : null;
                var rGun = rPack != null ? Vendor.GunOf(rPack) : null;
                if (victim != null)
                {
                    var at = victim.transform.position;
                    int clips0 = FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length;
                    victim.BulletHitHost(50f, false, null);
                    yield return Wait(1.0f);
                    var made = new List<AmmoClip>();
                    foreach (var c in FindObjectsByType<AmmoClip>(FindObjectsSortMode.None)) if (c && c.kind == RangedWeapon.Kind.Pew && Vector3.Distance(c.transform.position, at) < 3f) made.Add(c);
                    Check($"{who}: the armed guard went down for good and dropped one clip of pistol ammunition where he fell", victim.Dead && made.Count == 1 && made[0].rounds == pewSpec.magazine && FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length == clips0 + 1,
                        $"(dead {victim.Dead}, {made.Count} clip(s) near, {FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length} lie about of {clips0 + 1})");
                    if (rGun != null)
                    {
                        rGun.Magazine.SetReserve(0);                                     // (room for a clip: the client does the same to his copy)
                        yield return Until(() => rGun.Magazine.Reserve >= pewSpec.magazine, 90f, "the client to take the dropped clip");
                        yield return Wait(0.5f);
                        Check($"{who}: the client took the dropped clip (his E, my answer by its id): its rounds are in his pistol's reserve here too and the clip is gone", rGun.Magazine.Reserve == pewSpec.magazine && FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length == clips0,
                            $"(reserve {rGun.Magazine.Reserve}, {FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length} clips lie about of {clips0})");
                    }
                }
                var rw = remote != null ? remote.GetComponent<WobblyPlayer>() : null;
                if (rw != null && hm != null)
                {
                    hm.godMode = false;
                    int blasts0 = BlastFx.Played;
                    bool blasted = hm.Blast(rw, victim);
                    hm.godMode = true;
                    yield return Wait(2.0f);
                    Check($"{who}: a shotgun's hit on the client's player blasts him - busted at once, the blast plays here, and the team is not down (I am free)", blasted && rw.Custody == CustodyState.Busted && BlastFx.Played == blasts0 + 1 && hm.Phase == HeistPhase.Heist,
                        $"(blasted {blasted}, {rw.Custody}, blasts {BlastFx.Played - blasts0}, {hm.Phase})");
                }
                else Check($"{who}: the client's player is here to be blasted", false);
            }
            else
            {
                Pickup dropped = null;
                yield return Until(() =>
                {
                    foreach (var c in FindObjectsByType<AmmoClip>(FindObjectsSortMode.None))
                    {
                        if (!c || c.kind != RangedWeapon.Kind.Pew) continue;
                        foreach (var g in GuardAI.All) if (g && g.Dead && Vector3.Distance(g.transform.position, c.transform.position) < 3f) { dropped = c.GetComponent<Pickup>(); return true; }
                    }
                    return false;
                }, 90f, "the clip the host's armed guard dropped");
                Check($"{who}: the host's armed guard went down and his clip lies here too - eight rounds for the pistol, with a network id", dropped != null && dropped.GetComponent<AmmoClip>().rounds == pewSpec.magazine && dropped.NetId >= 0, $"(id {(dropped != null ? dropped.NetId : -1)})");
                var wob = NetPlayer.Local != null ? NetPlayer.Local.GetComponent<WobblyPlayer>() : null;
                var rb = wob != null ? wob.GetComponent<Rigidbody>() : null;
                var cPack = wob != null ? wob.GetComponent<Backpack>() : null;
                var cGun = cPack != null ? Vendor.GunOf(cPack) : null;
                if (dropped != null && cGun != null && rb != null)
                {
                    cGun.Magazine.SetReserve(0);                                         // (room for a clip: the host does the same to his copy of my pistol)
                    wob.TeleportTo(dropped.transform.position + Vector3.up * 1.0f, wob.transform.rotation);
                    rb.linearVelocity = Vector3.zero;
                    yield return Wait(2.5f);                                             // (the host's copy of me has to arrive at the clip)
                    cPack.TryStore(dropped);
                    yield return Until(() => cGun.Magazine.Reserve >= pewSpec.magazine, 30f, "the dropped clip's rounds to reach my pistol");
                    yield return Wait(0.5f);
                    Check($"{who}: the dropped clip's rounds went into my pistol's reserve (the host found the clip by its id) and the clip is gone here", cGun.Magazine.Reserve == pewSpec.magazine && !dropped, $"(reserve {cGun.Magazine.Reserve})");
                }
                else Check($"{who}: a pistol and a body to take the dropped clip with", false);
                yield return Until(() => BlastFx.Played >= 1, 120f, "the blast the host played on me");
                if (wob != null) yield return Until(() => wob.Custody == CustodyState.Busted, 30f, "the host to bust me");
                Check($"{who}: a shotgun's hit blew me up - the blast played here and the host busted me", BlastFx.Played >= 1 && wob != null && wob.Custody == CustodyState.Busted, $"(blasts {BlastFx.Played}, {(wob != null ? wob.Custody.ToString() : "no body")})");
            }
        }

        IEnumerator RunHost()
'@
Save-Src
