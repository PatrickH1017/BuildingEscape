# Round 7, batch B1a (the owner's list of 2026-10-18: "The pistol should have 8 bullets per clip and 3 clips total and 3 shots to knock down a guard",
# "More ammo should need to be purchased from the Vendor", "The pistol should not be free from the Vendor anymore"): the game code. BE_qa (= C:\bq) only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

# ---------------------------------------------------------------- the magazine and its reserve
Open-Src (Join-Path $Dir 'Scripts\Guns\GunRules.cs')
Rep @'
        public bool Unlimited;

        public int Rounds { get; private set; }
'@ @'
        public bool Unlimited;

        /// <summary>
        /// The rounds in the spare clips (the owner, 2026-10-18: "8 bullets per clip and 3 clips total"): a reload takes what it needs from them, and with none left an empty gun only clicks until more are
        /// bought. A gun that never runs short (an armed officer's) keeps the default, an endless reserve.
        /// </summary>
        public bool InfiniteReserve = true;
        public int Reserve { get; private set; }

        public int Rounds { get; private set; }
'@
Rep @'
            if (Reloading || Rounds >= Capacity || Unlimited) return false;
            Reloading = true;
'@ @'
            if (!CanReload) return false;
            Reloading = true;
'@
Rep @'
                Reloading = false;
                Rounds = Capacity;
                Reloads++;
                return Event.ReloadFinished;
'@ @'
                Reloading = false;
                if (InfiniteReserve) Rounds = Capacity;
                else
                {
                    int take = Math.Min(Capacity - Rounds, Reserve);                 // (only what is missing: nothing is wasted)
                    Rounds += take;
                    Reserve -= take;
                }
                Reloads++;
                return Event.ReloadFinished;
'@
Rep @'
        /// <summary>Fill the magazine at once (a test).</summary>
        public void Fill() { Rounds = Capacity; Reloading = false; }
'@ @'
        /// <summary>Fill the magazine at once (a test). The spare clips are not touched.</summary>
        public void Fill() { Rounds = Capacity; Reloading = false; }

        /// <summary>Can a reload start now: the gun is not full, not reloading already, and there is a clip to load (always, for an endless reserve)? Never in the basement, where the magazine is never short.</summary>
        public bool CanReload => !Reloading && Rounds < Capacity && !Unlimited && (InfiniteReserve || Reserve > 0);

        /// <summary>The magazine is empty and so is the reserve: the trigger only clicks until the dealer sells more.</summary>
        public bool OutOfAmmo => !Unlimited && !InfiniteReserve && Rounds <= 0 && Reserve <= 0 && !Reloading;

        /// <summary>Give the gun a real reserve of this many rounds (the player's pistol; a new one comes with two spare clips).</summary>
        public void SetReserve(int rounds) { InfiniteReserve = false; Reserve = Math.Max(0, rounds); }

        /// <summary>More rounds in the reserve (a clip from the dealer). Returns how many were added: none for a gun with an endless reserve.</summary>
        public int AddReserve(int rounds)
        {
            if (InfiniteReserve || rounds <= 0) return 0;
            Reserve += rounds;
            return rounds;
        }
'@
Rep @'
        /// <summary>A guard is a worse shot the further away the player is: the cone he fires into at this distance.</summary>
'@ @'
        /// <summary>How many spare clips these spare rounds make (a part of a clip counts as one): the "2 clips" on the screen.</summary>
        public static int SpareClips(int reserveRounds, int capacity) => capacity <= 0 ? 0 : (Math.Max(0, reserveRounds) + capacity - 1) / capacity;

        /// <summary>A guard is a worse shot the further away the player is: the cone he fires into at this distance.</summary>
'@
Save-Src

# ---------------------------------------------------------------- the pistol's numbers
Open-Src (Join-Path $Dir 'Scripts\Guns\RangedWeapon.cs')
Rep @'
    /// Today it is only handed out by the test menu (F1) and carried by armed guards; the numbers below make it the strongest weapon in
    /// the game: one bullet counts as five of the ten hits it takes to knock a guard out, a swing of the hatchet as one.
'@ @'
    /// It lies in the guard outpost of floor 3 and the dealer sells it once that floor is finished (for money); the numbers below make it the strongest weapon in
    /// the game: one bullet counts as four of the ten hits it takes to knock a guard out (three bullets), a swing of the hatchet as one. It holds a clip of eight
    /// rounds and comes with two spare clips (three in all, the owner, 2026-10-18); more rounds are bought from the dealer.
'@
Rep @'
            public int magazine;
            public float reloadSeconds, fireInterval;
'@ @'
            public int magazine;
            /// <summary>The spare clips a new gun comes with (the clip in the gun is the first of the three).</summary>
            public int spareClips;
            public float reloadSeconds, fireInterval;
'@
Rep @'
                damage = 60f, guardHits = 5, stun = 8f, knock = 16f, noise = 45f,
                magazine = 10, reloadSeconds = 2.2f, fireInterval = 0.3f, range = 45f, spread = 1.2f,
'@ @'
                damage = 60f, guardHits = 4, stun = 8f, knock = 16f, noise = 45f,
                magazine = 8, spareClips = 2, reloadSeconds = 2.2f, fireInterval = 0.3f, range = 45f, spread = 1.2f,
'@
Rep @'
        // every gun in the world (the ones players carry, the ones lying about, the guards' are not here): the elevator refills them all when a floor starts
        static readonly System.Collections.Generic.List<RangedWeapon> all = new System.Collections.Generic.List<RangedWeapon>();
        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() { all.Remove(this); }

        /// <summary>Every gun gets a full magazine again (a floor starts: "when using the elevator to go to a floor all ammo is reset"). Returns how many guns were refilled.</summary>
        public static int RefillAll()
        {
            int n = 0;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (!all[i]) { all.RemoveAt(i); continue; }
                all[i].Magazine.Fill();
                n++;
            }
            return n;
        }

        public static GunMagazine NewMagazine(Spec s) => new GunMagazine(s.magazine, s.reloadSeconds, s.fireInterval);
'@ @'
        // (The elevator used to give every gun a full magazine again when a floor started. Since 2026-10-18 it does not: "more ammo should need to be purchased from the Vendor".)

        /// <summary>A new gun: a full clip in it and two spare ones (a gun that has been bought or found is loaded, the rest costs money).</summary>
        public static GunMagazine NewMagazine(Spec s)
        {
            var m = new GunMagazine(s.magazine, s.reloadSeconds, s.fireInterval);
            m.SetReserve(s.spareClips * s.magazine);
            return m;
        }
'@
Rep 'static void ResetStatics() { layouts = new Layout[Specs.Length]; all.Clear(); }' 'static void ResetStatics() { layouts = new Layout[Specs.Length]; }'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\TowerManager.cs')
Rep @'
            if (floor > 0) RangedWeapon.RefillAll();          // (the elevator to a floor: every gun has a full magazine again; in the basement the ammunition is unlimited, see GunRules.UnlimitedAmmo)
'@ @'
            // (the elevator no longer refills the guns: ammunition is bought from the dealer. In the basement it is unlimited, see GunRules.UnlimitedAmmo)
'@
Save-Src

# ---------------------------------------------------------------- the armed officers keep their ten-round magazine
Open-Src (Join-Path $Dir 'Scripts\Guns\GuardGun.cs')
Rep @'
            var spec = RangedWeapon.Specs[(int)kind];
            // (the owner, 2026-10-18: armed officers "faster to attack as the floors go up": the higher the floor, the shorter the aim, the pause between shots and the reload)
'@ @'
            // (the owner, 2026-10-18: armed officers "faster to attack as the floors go up": the higher the floor, the shorter the aim, the pause between shots and the reload)
'@
Rep 'mag = new GunMagazine(spec.magazine, reloadSeconds, fireInterval);' 'mag = new GunMagazine(MagazineSize, reloadSeconds, fireInterval);          // (an endless reserve: an officer reloads for ever)'
Rep @'
        /// <summary>He has the gun in his hand (a guard whose pew was taken away is not armed any more).</summary>
'@ @'
        /// <summary>Rounds in an officer's magazine. (The player's pistol holds eight since 2026-10-18; the officers keep ten and never run out.)</summary>
        public const int MagazineSize = 10;

        /// <summary>He has the gun in his hand (a guard whose pew was taken away is not armed any more).</summary>
'@
Save-Src

# ---------------------------------------------------------------- the gun in the hand: the trigger, the reload key, the screen
Open-Src (Join-Path $Dir 'Scripts\Guns\GunController.cs')
Rep @'
    /// left mouse fires (hold it to keep firing), R reloads, and after ten shots the magazine is empty and the gun has to be reloaded
    /// (it starts by itself, the trigger of an empty gun only clicks). A bullet goes from the muzzle to wherever the crosshair points; it
    /// stops at the first wall, desk or door, knocks a guard down for good in two hits, and makes a loud noise that guards come to look at.
'@ @'
    /// left mouse fires (hold it to keep firing), R reloads, and after eight shots the magazine is empty and the gun has to be reloaded from a
    /// spare clip (it starts by itself, the trigger of an empty gun only clicks; with no spare clip left it stays empty until the dealer sells
    /// more). A bullet goes from the muzzle to wherever the crosshair points; it stops at the first wall, desk or door, knocks a guard down
    /// for good in three hits, and makes a loud noise that guards come to look at.
'@
Rep @'
        float remoteAimUntil;

        void Awake()
'@ @'
        float remoteAimUntil;
        float nextAmmoNotice;

        void Awake()
'@
Rep @'
            if (Input.GetKeyDown(KeyCode.R) && mag.StartReload(now)) reloadStep = 0;
'@ @'
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (mag.StartReload(now)) reloadStep = 0;
                else if (mag.OutOfAmmo) OutOfAmmoNotice();
            }
'@
Rep @'
                    GunSounds.Dry(muzzlePos, true);
                    if (mag.StartReload(now)) reloadStep = 0;
                    return false;
'@ @'
                    GunSounds.Dry(muzzlePos, true);
                    if (mag.StartReload(now)) reloadStep = 0;
                    else if (mag.OutOfAmmo) OutOfAmmoNotice();
                    return false;
'@
Rep @'
        // ------------------------------------------------------------------ the ammunition on screen

'@ @'
        /// <summary>No round in the gun and no spare clip: say where to get more (at most every few seconds).</summary>
        void OutOfAmmoNotice()
        {
            if (!pack || !pack.Viewed || Time.time < nextAmmoNotice) return;
            nextAmmoNotice = Time.time + 3f;
            Backpack.NoticeToLocal("Out of ammo - the dealer in the Loading Docks sells pistol clips");
        }

        // ------------------------------------------------------------------ the ammunition on screen

'@
Rep @'
                reloadProgress = mag.Reloading ? mag.ReloadProgress(Time.time) : 0f,
            });
        }
'@ @'
                reloadProgress = mag.Reloading ? mag.ReloadProgress(Time.time) : 0f,
            });
            if (!mag.Unlimited && !mag.InfiniteReserve) DrawSpare(mag);
        }

        /// <summary>The spare clips, on a small tag just above the ammunition panel: orange with one clip or less, red with none.</summary>
        static void DrawSpare(GunMagazine mag)
        {
            float s = HudTheme.S, w = 196f * s, gap = 6f * s, h = 24f * s;
            var r = new Rect(Screen.width - 14f * s - w, Screen.height - 14f * s - 68f * s - gap - h, w, h);          // (the panel is 68 high)
            int clips = GunRules.SpareClips(mag.Reserve, mag.Capacity);
            string text = mag.Reserve <= 0 ? "NO SPARE CLIPS" : "SPARE  " + mag.Reserve + "  (" + clips + (clips == 1 ? " clip)" : " clips)");
            var fill = mag.Reserve <= 0 ? HudTheme.Danger : mag.Reserve <= mag.Capacity ? HudTheme.Warning : HudTheme.Info;
            HudTheme.Chip(r, text, fill, 13f);
        }
'@
Save-Src

# ---------------------------------------------------------------- the pack window shows the spare rounds
Open-Src (Join-Path $Dir 'Scripts\Backpack.cs')
Rep @'
                string ammo = rounds + " / " + rw.Magazine.Capacity + " rounds";
'@ @'
                string ammo = rounds + " / " + rw.Magazine.Capacity + " rounds" + (rw.Magazine.InfiniteReserve ? "" : "  +" + rw.Magazine.Reserve + " spare");
'@
Save-Src

# ---------------------------------------------------------------- the test menu
Open-Src (Join-Path $Dir 'Scripts\TestMode.cs')
Rep @'
                GUILayout.Label(m.Reloading ? "Pew: reloading ..." : $"Pew: {m.Rounds} / {m.Capacity} rounds (LMB fires, R reloads)");
                if (GUILayout.Button("Refill the magazine")) m.Fill();
            }
            else GUILayout.Label("Equip the Pew (inventory) to shoot: 10 rounds, then a reload");
'@ @'
                GUILayout.Label(m.Reloading ? "Pew: reloading ..." : $"Pew: {m.Rounds} / {m.Capacity} rounds, {(m.InfiniteReserve ? "endless" : m.Reserve.ToString())} spare (LMB fires, R reloads)");
                if (GUILayout.Button("Refill the magazine and the spare clips"))
                {
                    m.Fill();
                    if (!m.InfiniteReserve) m.SetReserve(Mathf.Max(m.Reserve, RangedWeapon.Specs[(int)gun.kind].spareClips * m.Capacity));
                }
            }
            else GUILayout.Label("Equip the Pew (inventory) to shoot: 8 rounds a clip, two spare clips, then it only clicks");
'@
Save-Src
Write-Output 'round 7 batch B1a patched'
