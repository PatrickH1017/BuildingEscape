# Round 6, batch A1 (the owner's task list of 2026-10-18): (3) officers see less far down a hallway, (4) an officer cannot pin a player through a wall, (7) armed officers are faster to attack the higher the floor.
# Also the R6 Play scenario (NetDevHarness.R6, NetDevR6.cs) that the later items add their checks to, and the pure Round6Test. BE_qa (= C:\bq) only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

# ---------------------------------------------------------------- (3) the long sight: 44 m -> about 34 m
Open-Src (Join-Path $Dir 'Scripts\GuardRules.cs')
Rep '(44 m for the usual 14 m)' '(34 m for the usual 14 m; it was 44 m until the owner asked for a bit less, 2026-10-18: "Officers see too far down the hallway currently, please reduce their vision distance a bit")'
Rep 'public const float LongSightFactor = 3.15f, LongSightHalfAngle = 9f;' 'public const float LongSightFactor = 2.4f, LongSightHalfAngle = 9f;'
Save-Src
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR3Test.cs')
Rep 'Check("the long sight is much further than the ordinary one", far >= range * 2.5f);' 'Check("the long sight is much further than the ordinary one, but not as far as it was (44 m)", far >= range * 2f && far < range * 3f);'
Rep 'Check("a player 40 m down the hall, dead ahead, is seen", Sees(40f, 0f));' 'Check("a player 30 m down the hall, dead ahead, is seen", Sees(30f, 0f));'
Rep 'Check("...but 46 m away is not", !Sees(46f, 0f));' 'Check("...but 36 m away is not (the long range is about 34 m now, it was 44 m)", !Sees(36f, 0f));'
Rep 'Check("...and 40 m away but 5 degrees off is not (the beam narrows with the distance)", !Sees(40f, 5f));' 'Check("...and 30 m away but 5 degrees off is not (the beam narrows with the distance)", !Sees(30f, 5f));'
Save-Src

# ---------------------------------------------------------------- (4) no pinning through walls
Open-Src (Join-Path $Dir 'Scripts\GuardAI.cs')
Rep '            if (FlatDistance(target.transform) <= catchRadius && HeistManager.Instance != null && HeistManager.Instance.Pin(target, this))
                BeginHold(target);
        }' '            TryCatch(target);
        }

        /// <summary>
        /// Can he lay hands on this player? A wall, a shut door or any other solid thing that is fixed in the building between the two of them says no (the owner, 2026-10-18: "Players can be pinned through walls at times":
        /// catching used to be a flat distance only, so an officer on the other side of a thin partition could pin somebody through it). The line runs from his chest to theirs; things that move (a crate, another body) do not count.
        /// </summary>
        public bool HasReach(WobblyPlayer p)
        {
            if (!p) return false;
            var from = transform.position + Vector3.up * 0.9f;
            var to = p.transform.position + Vector3.up * 0.9f;
            var d = to - from;
            float dist = d.magnitude;
            if (dist < 0.3f) return true;
            var hits = Physics.RaycastAll(from, d / dist, dist, sightBlockers, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var c = h.collider;
                if (!c || c.transform.IsChildOf(transform) || c.transform.IsChildOf(p.transform)) continue;
                if (c.attachedRigidbody != null) continue;                                                    // (a crate, a body: it moves, it is not a wall)
                return false;
            }
            return true;
        }

        /// <summary>Is the player within his arm''s reach, with nothing solid between them? Then he pins them (host / offline) and starts to hold them down. True when he got hold of them.</summary>
        public bool TryCatch(WobblyPlayer p)
        {
            if (!p || FlatDistance(p.transform) > catchRadius || !HasReach(p)) return false;
            if (HeistManager.Instance == null || !HeistManager.Instance.Pin(p, this)) return false;
            BeginHold(p);
            return true;
        }'
Save-Src

# ---------------------------------------------------------------- (7) armed officers: faster by floor
Open-Src (Join-Path $Dir 'Scripts\Guns\GunRules.cs')
Rep '        /// <summary>How many bullets knock a guard out:' '        /// <summary>
        /// How fast an armed officer goes for a player on this floor (the owner, 2026-10-18: "Officers with weapons should be more aggressive (faster to attack as the floors go up)"): the multipliers for the time he takes
        /// to aim, the pause between two shots and the reload. On the first armed floor they are a little under 1 (he was a bit slower before), on <see cref="ArmedRules.TopFloor"/> and above they are at their smallest.
        /// </summary>
        public static void GuardTempo(int floor, int firstArmedFloor, out float aim, out float shot, out float reload)
        {
            float t = (floor <= firstArmedFloor || ArmedRules.TopFloor <= firstArmedFloor) ? 0f : Math.Min(1f, (floor - firstArmedFloor) / (float)(ArmedRules.TopFloor - firstArmedFloor));
            aim = 0.8f + (0.3f - 0.8f) * t;
            shot = 0.9f + (0.55f - 0.9f) * t;
            reload = 0.95f + (0.7f - 0.95f) * t;
        }

        /// <summary>How many bullets knock a guard out:'
Rep '        public const int Never = int.MaxValue;' '        public const int Never = int.MaxValue;

        /// <summary>The highest office floor (the boss is above it): armed officers are at their fastest from here (<see cref="GunRules.GuardTempo"/>).</summary>
        public const int TopFloor = 12;'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Guns\GuardGun.cs')
Rep '            var spec = RangedWeapon.Specs[(int)kind];
            mag = new GunMagazine(spec.magazine, reloadSeconds, fireInterval);
            ai.AttachGun(this);' '            var spec = RangedWeapon.Specs[(int)kind];
            // (the owner, 2026-10-18: armed officers "faster to attack as the floors go up": the higher the floor, the shorter the aim, the pause between shots and the reload)
            var hm = HeistManager.Instance;
            GunRules.GuardTempo(hm != null ? hm.Floor : 0, ArmedGuards.FirstArmedFloor, out float aimMul, out float shotMul, out float reloadMul);
            aimSeconds *= aimMul; fireInterval *= shotMul; reloadSeconds *= reloadMul;
            mag = new GunMagazine(spec.magazine, reloadSeconds, fireInterval);
            ai.AttachGun(this);'
Save-Src

# ---------------------------------------------------------------- the R6 scenario and the pure tests
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void R5()
        {' '        public static void R6()
        {
            SessionState.SetString("netdev.mode", "r6");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void R5()
        {'
Rep 'mode != "r5" && mode != "probe"' 'mode != "r5" && mode != "r6" && mode != "probe"'
Rep '                case "r5": yield return R5Scenario(); break;' '                case "r5": yield return R5Scenario(); break;
                case "r6": yield return R6Scenario(); break;'
Save-Src
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevTest.cs')
Rep '            TerminalParseTest();
' '            TerminalParseTest();
            Round6Test();
'
Save-Src
Write-Output 'round 6 batch A1 patched'
