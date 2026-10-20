# Round 8, batch F16 (the owner, 2026-10-18: "Please also add the chance of a watch being in the safe"):
# any safe - the cash safes of floors 1 and 2 as well as the weapon safes - may also hold ONE gold watch (SafeRules.SideWatchPercent = 30 %, decided by the seed alone), a valuable of its own
# (the same gold watch the desks and the outposts have, made by FloorGenerator.BuildValuable) that is picked up on its own like the cash and the clip. It lies at the front right of the hollow,
# switched off with its network id already taken until the safe opens (FloorSafe.AddWatch), and is worth 1.4 typical small valuables of the floor (the outposts' gold watch), in $25 steps.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor'

# ================================================================ the rules
Open-Src "$Src\Outpost\SafeRules.cs"
Rep 'SaltSideCash = 34, SaltSideClip = 35;' 'SaltSideCash = 34, SaltSideClip = 35, SaltSideWatch = 36;'
Rep @'
        public static int SideCash(int floor) => Math.Max(50, (int)(Math.Round(TowerRules.QuotaFor(floor) * (double)SideCashShareOfQuota / 25.0) * 25.0));
'@ @'
        public static int SideCash(int floor) => Math.Max(50, (int)(Math.Round(TowerRules.QuotaFor(floor) * (double)SideCashShareOfQuota / 25.0) * 25.0));

        // ------------------------------------------------------------------ and sometimes a gold watch (the owner, 2026-10-18)

        /// <summary>
        /// The chance (percent) that a safe also holds a gold watch (the owner, 2026-10-18: "Please also add the chance of a watch being in the safe"): ANY safe - the cash safes of floors 1 and 2 as well as the weapon safes -
        /// a valuable of its own, picked up on its own like the cash and the clip. Decided by the seed alone, independent of the other two chances.
        /// </summary>
        public const int SideWatchPercent = 30;

        /// <summary>Does this safe also hold a gold watch? (Never a safe that does not exist.)</summary>
        public static bool HasSideWatch(int runSeed, int floor, int index) =>
            index >= 0 && index < SafeCount(runSeed, floor) && Hash(runSeed, floor, SaltSideWatch + index * 127) % 100U < (uint)SideWatchPercent;

        /// <summary>What the watch is worth: 1.4 times a typical small valuable of the floor (the same as a gold watch lying in a guard outpost), in $25 steps, $100 at least.</summary>
        public static int SideWatchValue(int floor) => Math.Max(100, (int)(Math.Round(OutpostLoot.ValuableUnit(floor) * 1.4 / 25.0) * 25.0));
'@
Save-Src

# ================================================================ the safe: where the watch lies, switched off until the door opens
Open-Src "$Src\Outpost\FloorSafe.cs"
Rep @'
        readonly List<GameObject> clips = new List<GameObject>();
'@ @'
        readonly List<GameObject> clips = new List<GameObject>();
        readonly List<GameObject> watches = new List<GameObject>();
'@
Rep @'
        public int ClipCount => clips.Count;
'@ @'
        public int ClipCount => clips.Count;
        /// <summary>How many gold watches it was filled with (0 or 1; one that was taken is gone from the world but still counts here).</summary>
        public int WatchCount => watches.Count;
        /// <summary>The gold watches that were put in it (switched off until the safe is open), for the tests.</summary>
        public IReadOnlyList<GameObject> Watches => watches;
'@
Rep @'
        // ------------------------------------------------------------------ opening
'@ @'
        /// <summary>
        /// Where a gold watch lies in the hollow (the owner, 2026-10-18: "Please also add the chance of a watch being in the safe"): at the front right, clear of the pistol in the middle, the clip beside it and the cash at the
        /// back, its strap along the safe's width. Returns the world position (on the floor of the hollow) and the yaw the watch is turned by.
        /// </summary>
        public Vector3 WatchSpot(out float yaw)
        {
            yaw = transform.eulerAngles.y + 90f;
            return transform.TransformPoint(new Vector3(0.27f, WallThickness + 0.004f, 0.10f));
        }

        /// <summary>
        /// Takes a gold watch the generator made at <see cref="WatchSpot"/> into the safe: it is out of the world, switched off with its network id already taken, until the safe opens (like the pistol and the clip).
        /// Call after <see cref="FillWithExtras"/>, so that every machine makes the things in the same order.
        /// </summary>
        public void AddWatch(GameObject watch)
        {
            if (!watch) return;
            watch.SetActive(false);
            watches.Add(watch);
        }

        // ------------------------------------------------------------------ opening
'@
Rep 'foreach (var c in clips) if (c) c.SetActive(true);' @'
foreach (var w in watches) if (w) w.SetActive(true);                // (the gold watch, if there is one)
            foreach (var c in clips) if (c) c.SetActive(true);
'@
Save-Src

# ================================================================ the generator makes the watch (the same code as the desks' watches)
Open-Src "$Src\FloorGenerator.Outpost.cs"
Rep @'
                    safe.FillWithCash(parent, values, OMat("bill"), OMat("band"));
                }
'@ @'
                    safe.FillWithCash(parent, values, OMat("bill"), OMat("band"));
                }
                if (SafeRules.HasSideWatch(intelNow, floor, safe.Index))
                {
                    // (any safe, cash or weapons: sometimes a gold watch - the same thing as the ones on the desks, an item of its own and the last thing made for this safe)
                    var watchAt = safe.WatchSpot(out float watchYaw);
                    safe.AddWatch(BuildValuable(ValuableKind.Watch, SafeRules.SideWatchValue(floor), watchAt, watchYaw, parent));
                }
'@
Rep "or the pistols (floor 3 and up) in the floor's safes." "or the pistols (floor 3 and up) in the floor's safes (and sometimes a gold watch in any of them)."
Save-Src

# ================================================================ the autopilot (the built game plays itself): the watches are there as the rules say, and the client and the host see the client take one
Open-Src "$Src\QaAutopilot.cs"
Rep 'qsShut = 0, qsLive = 0, qsWantCash = 0, qsWantClips = 0;' 'qsShut = 0, qsLive = 0, qsWantCash = 0, qsWantClips = 0, qsWatches = 0, qsWantWatches = 0;'
Rep 'qsGuns += qs.GunCount; qsCash += qs.BundleCount; qsClips += qs.ClipCount;' 'qsGuns += qs.GunCount; qsCash += qs.BundleCount; qsClips += qs.ClipCount; qsWatches += qs.WatchCount;'
Rep 'if (SafeRules.HasSideClip(tm.IntelSeed, floor, qi)) qsWantClips++; }' 'if (SafeRules.HasSideClip(tm.IntelSeed, floor, qi)) qsWantClips++; if (SafeRules.HasSideWatch(tm.IntelSeed, floor, qi)) qsWantWatches++; }'
Rep @'
                    if (qsWeapons && FloorSafe.OnShow.Count > 0)
                    {
                        // the open safe with the most things in it, close up: the pistol in the middle, the bit of cash and the spare clip at its sides
'@ @'
                    Check($"floor {floor}: the safes hold a gold watch exactly when the rules say ({qsWantWatches} of {qsWant})", qsWatches == qsWantWatches, $"({qsWatches} watches)");
                    if (qsWeapons && FloorSafe.OnShow.Count > 0)
                    {
                        // the open safe with the most things in it, close up: the pistol in the middle, the bit of cash and the spare clip at its sides
'@
Rep 'int qn = qb.GunCount + qb.BundleCount + qb.ClipCount;' 'int qn = qb.GunCount + qb.BundleCount + qb.ClipCount + qb.WatchCount;'
Rep '&& safes[i].ClipCount == (SafeRules.HasSideClip(seed, floor, i) ? 1 : 0);' '&& safes[i].ClipCount == (SafeRules.HasSideClip(seed, floor, i) ? 1 : 0) && safes[i].WatchCount == (SafeRules.HasSideWatch(seed, floor, i) ? 1 : 0);'
Rep 'Pickup cPew = null, cClip = null, cCash = null;' 'Pickup cPew = null, cClip = null, cCash = null, cWatch = null;'
Rep 'else if (cp.Label == "Cash bundle") cCash = cp;' 'else if (cp.Label == "Cash bundle") cCash = cp; else if (cp.Label == "Gold Watch") cWatch = cp;'
Rep @'
                        Check("client: the bit of cash is in my pack, on its own (the host agreed)", cPack.Contains(cCash) && cCash.Value == SafeRules.SideCash(floor), $"(${cCash.Value})");
                    }
'@ @'
                        Check("client: the bit of cash is in my pack, on its own (the host agreed)", cPack.Contains(cCash) && cCash.Value == SafeRules.SideCash(floor), $"(${cCash.Value})");
                    }
                    if (cWatch != null)
                    {
                        cPack.TryStore(cWatch);
                        yield return Until(() => cPack.Contains(cWatch), 30f, "the gold watch to go into my pack");
                        Check("client: the gold watch is in my pack, on its own, worth what the rules say (the host agreed)", cPack.Contains(cWatch) && cWatch.Value == SafeRules.SideWatchValue(floor), $"(${cWatch.Value})");
                    }
                    else Say("client: no gold watch in this safe: the watch was not tried");
'@
Rep @'
                    Check("host: the client's bit of cash is in his pack here too, on its own", rPack.Items.Any(it => it && it.Label == "Cash bundle"));
                }
'@ @'
                    Check("host: the client's bit of cash is in his pack here too, on its own", rPack.Items.Any(it => it && it.Label == "Cash bundle"));
                }
                if (hs.WatchCount > 0 && rPack != null)
                {
                    yield return Until(() => rPack.Items.Any(it => it && it.Label == "Gold Watch"), 60f, "the client's gold watch");
                    Check("host: the client's gold watch is in his pack here too, on its own", rPack.Items.Any(it => it && it.Label == "Gold Watch"));
                }
'@
Save-Src

# ================================================================ tests: the rule (pure), the outpost's safes (live), the picture
Open-Src "$Ed\NetDev\NetDevR7Test.cs"
Rep @'
                Check("safes: a journal line names the safe, its room and its code (or that a crowbar is wanted) - and not what is inside",
'@ @'
                bool wtOk = true; int wtHits = 0, wtSafes = 0, wtCashSafes = 0, wtCashHits = 0;
                for (int sd = 0; sd < 80; sd++)
                    for (int f = 1; f <= 12; f++)
                    {
                        int rs = sd * 43 + 3, n = SafeRules.SafeCount(rs, f);
                        for (int i = 0; i < n; i++)
                        {
                            bool watch = SafeRules.HasSideWatch(rs, f, i);
                            wtSafes++; if (watch) wtHits++;
                            if (f < 3) { wtCashSafes++; if (watch) wtCashHits++; }
                            wtOk &= watch == SafeRules.HasSideWatch(rs, f, i);
                        }
                        wtOk &= !SafeRules.HasSideWatch(rs, f, n) && !SafeRules.HasSideWatch(rs, f, -1);
                    }
                bool wtValues = SafeRules.SideWatchValue(1) >= 100 && SafeRules.SideWatchValue(1) % 25 == 0 && SafeRules.SideWatchValue(7) > SafeRules.SideWatchValue(1) && SafeRules.SideWatchValue(12) > SafeRules.SideWatchValue(7) && SafeRules.SideWatchValue(3) * 8 < SafeRules.Cash(3);
                Check("safes: any safe - the cash safes of floors 1 and 2 too - also holds a gold watch about 30 % of the time (decided by the seed alone, none in a safe that is not there), worth about one and a half typical small valuables of the floor (the owner, 2026-10-18)",
                    wtOk && wtValues && wtHits * 100 >= wtSafes * 24 && wtHits * 100 <= wtSafes * 36 && wtCashSafes > 50 && wtCashHits > 0, $"({wtHits} of {wtSafes} safes, {wtCashHits} of {wtCashSafes} on floors 1 and 2)");

                Check("safes: a journal line names the safe, its room and its code (or that a crowbar is wanted) - and not what is inside",
'@
Save-Src

Open-Src "$Ed\NetDev\NetDevOutpost.cs"
Rep @'
            while (!SafeRules.HasSafe(seed, 1) || !SideItemsOn(seed, 3) || !SideItemsOn(seed, 7)) seed++;
'@ @'
            while (!SafeRules.HasSafe(seed, 1) || !SideItemsOn(seed, 3) || !SideItemsOn(seed, 7) || !SafeRules.HasSideWatch(seed, 1, 0) || !WatchOn(seed, 3) || !WatchOn(seed, 7)) seed++;          // (and a gold watch in floor 1's safe and in a safe of floors 3 and 7)
'@
Rep @'
                OutpostCheck("safe: the bundles are dormant while it is shut", dormant == cash.Count);
'@ @'
                OutpostCheck("safe: the bundles are dormant while it is shut", dormant == cash.Count);
                OutpostCheck("safe: it also holds a gold watch (the seed says so), switched off while the door is shut", safe.WatchCount == 1 && safe.Watches[0] != null && !safe.Watches[0].activeSelf, $"({safe.WatchCount} watches)");
'@
Rep @'
                OutpostCheck("safe: and can be picked up", sawCash);
'@ @'
                OutpostCheck("safe: and can be picked up", sawCash);
                var wtW = safe.WatchCount > 0 ? safe.Watches[0] : null;
                var wtPk = wtW ? wtW.GetComponent<Pickup>() : null;
                OutpostCheck("safe: the gold watch comes to life with the door, lies in the hollow, and is worth what the rules say (the owner, 2026-10-18)",
                    wtPk != null && wtW.activeSelf && wtPk.isActiveAndEnabled && wtPk.Label == "Gold Watch" && wtPk.bankValue == SafeRules.SideWatchValue(1) && WatchInHollow(safe, wtPk.transform.position),
                    wtPk ? $"({wtPk.Label} ${wtPk.bankValue}, at {safe.transform.InverseTransformPoint(wtPk.transform.position)})" : "(no watch)");
'@
Rep @'
                        wsSide &= wsS.BundleCount == (SafeRules.HasSideCash(seed, wsFloor, wsI) ? 1 : 0) && wsS.ClipCount == (SafeRules.HasSideClip(seed, wsFloor, wsI) ? 1 : 0);
'@ @'
                        wsSide &= wsS.BundleCount == (SafeRules.HasSideCash(seed, wsFloor, wsI) ? 1 : 0) && wsS.ClipCount == (SafeRules.HasSideClip(seed, wsFloor, wsI) ? 1 : 0) && wsS.WatchCount == (SafeRules.HasSideWatch(seed, wsFloor, wsI) ? 1 : 0);
'@
Rep 'each also holds a bit of cash and a spare clip exactly when the rules say (each its own item)' 'each also holds a bit of cash, a spare clip and a gold watch exactly when the rules say (each its own item)'
Rep 'foreach (var xCount in wsSafes) wsItemsWanted += xCount.BundleCount + xCount.ClipCount;' 'foreach (var xCount in wsSafes) wsItemsWanted += xCount.BundleCount + xCount.ClipCount + xCount.WatchCount;'
Rep 'every item of every open safe (pistol, clip, cash) is aimed at on its own' 'every item of every open safe (pistol, clip, cash, watch) is aimed at on its own'
Rep @'
                            OutpostCheck($"{wsTag} the bit of cash goes into the pack on its own, worth what the rules say", tGotCash && pack.Contains(tCash2) && tCash2.Value == SafeRules.SideCash(wsFloor), $"(${(tCash2 ? tCash2.Value : 0)})");
                        }
'@ @'
                            OutpostCheck($"{wsTag} the bit of cash goes into the pack on its own, worth what the rules say", tGotCash && pack.Contains(tCash2) && tCash2.Value == SafeRules.SideCash(wsFloor), $"(${(tCash2 ? tCash2.Value : 0)})");
                        }
                        // the gold watch, from whichever safe has one
                        Pickup tWatch = null;
                        foreach (var tS3 in wsSafes)
                        {
                            if (tS3.WatchCount == 0) continue;
                            var tNear3 = new List<Pickup>();
                            Pickup.ScanAround(tS3.transform.position + tS3.transform.forward * 0.1f + Vector3.up * 0.15f, 0.6f, tNear3);
                            foreach (var tp3 in tNear3) if (tp3.Label == "Gold Watch" && WatchInHollow(tS3, tp3.transform.position)) tWatch = tp3;
                            if (tWatch != null) break;
                        }
                        OutpostCheck($"{wsTag} a safe with a gold watch has it lying in the hollow", tWatch != null);
                        if (tWatch != null)
                        {
                            bool tGotWatch = pack.TryStore(tWatch);
                            yield return new WaitForSeconds(0.2f);
                            OutpostCheck($"{wsTag} the gold watch goes into the pack on its own, worth what the rules say", tGotWatch && pack.Contains(tWatch) && tWatch.Value == SafeRules.SideWatchValue(wsFloor), $"(${(tWatch ? tWatch.Value : 0)})");
                        }
'@
Rep @'
        /// <summary>Do the safes of this floor, between them, hold at least one bit of cash and at least one spare clip (so that a test can try both)?</summary>
'@ @'
        /// <summary>Do the safes of this floor hold at least one gold watch (so that a test can try it)?</summary>
        static bool WatchOn(int runSeed, int floor)
        {
            for (int i = 0, n = SafeRules.SafeCount(runSeed, floor); i < n; i++) if (SafeRules.HasSideWatch(runSeed, floor, i)) return true;
            return false;
        }

        /// <summary>Does this world position lie in the hollow of the safe?</summary>
        static bool WatchInHollow(FloorSafe s, Vector3 world)
        {
            var l = s.transform.InverseTransformPoint(world);
            return Mathf.Abs(l.x) < 0.39f && l.y > 0.04f && l.y < 0.5f && l.z > -0.30f && l.z < 0.26f;
        }

        /// <summary>Do the safes of this floor, between them, hold at least one bit of cash and at least one spare clip (so that a test can try both)?</summary>
'@
Save-Src

Open-Src "$Ed\NetDev\NetDevWeaponSheet.cs"
Rep @'
            safe.ApplyOpen(true);
            yield return new WaitForSeconds(1.6f);
'@ @'
            // the gold watch the rules give some safes: the generator's own builder (the same watch as the desks'), put in at the safe's own spot
            var gen = FindFirstObjectByType<FloorGenerator>();
            var build = typeof(FloorGenerator).GetMethod("BuildValuable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (gen && build != null)
            {
                var watchAt = safe.WatchSpot(out float watchYaw);
                safe.AddWatch((GameObject)build.Invoke(gen, new object[] { ValuableKind.Watch, SafeRules.SideWatchValue(7), watchAt, watchYaw, root.transform }));
            }
            else Log("[WeaponSheet] no way to make a watch");
            safe.ApplyOpen(true);
            yield return new WaitForSeconds(1.6f);
'@
Save-Src
Write-Output 'round 8 batch F16 (a gold watch in some safes) patched'
