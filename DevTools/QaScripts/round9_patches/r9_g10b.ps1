# Round 9, batch G10b (the owner's ladder of unlocks, 2026-10-19): the safes hold the gun of their floor, a clip is for one kind of gun, the dealer sells ammunition for the guns that were found and brought back.
#  - FloorSafe.FillWithGuns: any kind; a long gun lies slanted across the hollow with its barrel out through the open door (it never moves); FillWithExtras: the clip of the safe's gun
#  - FloorGenerator.Outpost: the clip's kind
#  - Vendor: ammunition products for the SMG, the rifle, the shotgun and the sniper rifle, what is on offer now (the pew once floor 3 is done, ammunition once the gun is unlocked), the three-clip limit
#  - Backpack / RunSync: a clip fits its gun only and not beyond three clips; the ammunition announcement carries the gun
#  - TestMode: a gun handed over also unlocks its ammunition
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'

# ---- FloorSafe
Open-Src "$Src\Outpost\FloorSafe.cs"
Rep 'LockoutSeconds = 1.5f, GunSpacing = 0.30f;' 'LockoutSeconds = 1.5f, GunSpacing = 0.30f, SlantDegrees = 50f;'
Rep @'
                if (items[i] != SafeRules.PewItem) continue;
                // (the pew with its silencer on is about 0.46 m long and the hollow 0.51 m deep, from the back wall at z -0.265 to the door at 0.245: the middle of the gun lies in the middle of the hollow)
                var local = new Vector3((i - (n - 1) * 0.5f) * GunSpacing, WallThickness + 0.024f, -0.01f);
                var gun = RangedWeapon.Create(RangedWeapon.Kind.Pew, transform.TransformPoint(local), parent, transform.eulerAngles.y - 90f);      // (the barrel is the gun's +X: turned by -90 degrees it points out of the door)
                gun.SetActive(false);
'@ @'
                if (!SafeRules.IsGunItem(items[i])) continue;
                var kind = SafeRules.GunOfItem(items[i]);
                if (!RangedWeapon.Has(kind)) continue;
                // (the pew with its silencer on is about 0.46 m long and the hollow 0.51 m deep, from the back wall at z -0.265 to the door at 0.245: the middle of the gun lies in the middle of the hollow. A long gun is longer than the hollow
                // is deep: it lies across it at an angle with its barrel out through the open door, and it never moves - it is kinematic, so it does not fall out - until somebody takes it)
                var lay = RangedWeapon.LayoutOf(kind);
                float length = lay.fullBounds.size.x * lay.scale, thick = lay.fullBounds.size.z * lay.scale;
                bool slanted = length > 0.46f;
                var local = new Vector3((i - (n - 1) * 0.5f) * GunSpacing, WallThickness + Mathf.Max(0.024f, thick * 0.5f) + 0.004f, -0.01f);
                var gun = RangedWeapon.Create(kind, transform.TransformPoint(local), parent, transform.eulerAngles.y - 90f + (slanted ? SlantDegrees : 0f), kind == RangedWeapon.Kind.Pew ? -1 : 0);      // (the barrel is the gun's +X: turned by -90 degrees it points out of the door; a gun found here holds its one clip and has no spare - the pew keeps its two for now)
                if (slanted && gun.TryGetComponent(out Rigidbody slantBody)) slantBody.isKinematic = true;
                gun.SetActive(false);
'@
Rep @'
        public void FillWithExtras(Transform parent, int cashValue, bool clip, Material bill, Material band, Material clipBody, Material clipBrass)
        {
            if (cashValue > 0) FillWithCash(parent, new[] { cashValue }, bill, band);
            if (!clip) return;
            var go = new GameObject("Pistol clip");
            go.transform.SetParent(parent, true);
            // the pistol's own magazine (the Mag part of its model, as big as it is on the gun) lying on its side; a plain box when the model has no such part
            var model = RangedWeapon.CreateClipModel(go.transform, out var size);
'@ @'
        public void FillWithExtras(Transform parent, int cashValue, bool clip, Material bill, Material band, Material clipBody, Material clipBrass, RangedWeapon.Kind clipKind = RangedWeapon.Kind.Pew)
        {
            if (cashValue > 0) FillWithCash(parent, new[] { cashValue }, bill, band);
            if (!clip) return;
            var clipSpec = RangedWeapon.Specs[(int)clipKind];
            var go = new GameObject(clipKind == RangedWeapon.Kind.Pew ? "Pistol clip" : clipSpec.ammoName);
            go.transform.SetParent(parent, true);
            // the gun's own magazine (the Mag part of its model, as big as it is on the gun) lying on its side; a plain box when the model has no such part (the shotgun's)
            var model = RangedWeapon.CreateClipModel(go.transform, out var size, clipKind);
'@
Rep @'
            pk.weight = 0.2f; pk.label = "Pistol clip";
            go.AddComponent<AmmoClip>().rounds = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].magazine;
'@ @'
            pk.weight = 0.2f; pk.label = clipKind == RangedWeapon.Kind.Pew ? "Pistol clip" : clipSpec.ammoName;
            var ammo = go.AddComponent<AmmoClip>();
            ammo.rounds = clipSpec.magazine;
            ammo.kind = clipKind;
'@
Save-Src

Open-Src "$Src\FloorGenerator.Outpost.cs"
Rep @'
                                        OMat("bill"), OMat("band"), OMat("dark"), OMat("brass"));            // (...and sometimes a bit of cash and a spare clip, each an item of its own)
'@ @'
                                        OMat("bill"), OMat("band"), OMat("dark"), OMat("brass"), SafeRules.ClipKindOf(intelNow, floor, safe.Index));            // (...and sometimes a bit of cash and a spare clip - for the gun in that safe -, each an item of its own)
'@
Save-Src

# ---- Vendor
Open-Src "$Src\Vendor.cs"
Rep @'
            new Product
            {
                name = "Pistol Clip", blurb = "8 more rounds for your pew: it reloads from the clips you carry", price = AmmoClipPrice,
                isAmmo = true, rounds = 8
            },
        };
'@ @'
            new Product
            {
                name = "Pistol Clip", blurb = "8 more rounds for your pew: it reloads from the clips you carry", price = AmmoClipPrice,
                isAmmo = true, rounds = 8
            },
            // the ammunition of the guns that are found, not sold (the owner, 2026-10-19: "The Vendor should sell ammo for the specific guns after finding them, ammo from the Vendor should be expensive")
            new Product { name = "SMG Magazine", blurb = "24 more rounds for your SMG", price = RangedWeapon.Specs[(int)RangedWeapon.Kind.Smg].clipPrice, isAmmo = true, gun = RangedWeapon.Kind.Smg, rounds = RangedWeapon.Specs[(int)RangedWeapon.Kind.Smg].magazine },
            new Product { name = "Rifle Magazine", blurb = "30 more rounds for your rifle", price = RangedWeapon.Specs[(int)RangedWeapon.Kind.Rifle].clipPrice, isAmmo = true, gun = RangedWeapon.Kind.Rifle, rounds = RangedWeapon.Specs[(int)RangedWeapon.Kind.Rifle].magazine },
            new Product { name = "Shotgun Shells", blurb = "6 more shells for your shotgun", price = RangedWeapon.Specs[(int)RangedWeapon.Kind.Shotgun].clipPrice, isAmmo = true, gun = RangedWeapon.Kind.Shotgun, rounds = RangedWeapon.Specs[(int)RangedWeapon.Kind.Shotgun].magazine },
            new Product { name = "Sniper Rounds", blurb = "5 more rounds for your sniper rifle", price = RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper].clipPrice, isAmmo = true, gun = RangedWeapon.Kind.Sniper, rounds = RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper].magazine },
        };
'@
Rep @'
        /// <summary>The pew in a player's pack (in hand or not), or null.</summary>
        public static RangedWeapon GunOf(Backpack pack)
        {
            if (!pack) return null;
            foreach (var held in pack.Items) if (held && held.TryGetComponent(out RangedWeapon gun)) return gun;
            return null;
        }

        /// <summary>Puts these rounds in the reserve of the pew in this pack. The host announces a sale to every machine (<c>RunSync.AnnounceAmmo</c>), each of which calls this for the buyer. True when a pew took them.</summary>
        public static bool GiveAmmo(Backpack pack, int rounds)
        {
            var gun = GunOf(pack);
            return gun != null && gun.Magazine.AddReserve(rounds) > 0;
        }

        static bool PewOnOffer { get { var tm = TowerManager.Instance; return PewForSale(tm ? tm.ResumeLevel : 1); } }
'@ @'
        /// <summary>A gun in a player's pack (in hand or not), or null: the one in his hand when it is of the kind asked for (any kind when none is), else the first that is.</summary>
        public static RangedWeapon GunOf(Backpack pack, RangedWeapon.Kind? kind = null)
        {
            if (!pack) return null;
            RangedWeapon first = null;
            foreach (var held in pack.Items)
            {
                if (!held || !held.TryGetComponent(out RangedWeapon gun)) continue;
                if (kind.HasValue && gun.kind != kind.Value) continue;
                if (pack.Equipped == held) return gun;
                if (first == null) first = gun;
            }
            return first;
        }

        /// <summary>Puts these rounds in the reserve of the gun of this kind in this pack (no more than three clips: <see cref="GunMagazine.Room"/>). The host announces a sale to every machine (<c>RunSync.AnnounceAmmo</c>), each of which calls this for the buyer. True when a gun took them.</summary>
        public static bool GiveAmmo(Backpack pack, int rounds, RangedWeapon.Kind kind = RangedWeapon.Kind.Pew)
        {
            var gun = GunOf(pack, kind);
            return gun != null && gun.Magazine.AddReserve(rounds) > 0;
        }

        static int ResumeNow { get { var tm = TowerManager.Instance; return tm ? tm.ResumeLevel : 1; } }

        /// <summary>
        /// Is entry <paramref name="index"/> of <see cref="Catalog"/> for sale now? The first seven always are; the pew once floor 3 is done (<see cref="PewForSale"/>); ammunition for a gun once the gun has been found and brought back to
        /// the docks (<see cref="RunUnlocks"/>; the pew's also once the pew is for sale). The other guns are found, not sold (the owner, 2026-10-19).
        /// </summary>
        public static bool OnOffer(int index, int resume)
        {
            if (index < 0 || index >= Catalog.Length) return false;
            var p = Catalog[index];
            if (p.isGun) return p.gun == RangedWeapon.Kind.Pew && PewForSale(resume);
            if (p.isAmmo) return RunUnlocks.Has(p.gun) || (p.gun == RangedWeapon.Kind.Pew && PewForSale(resume));
            return true;
        }

        readonly List<int> offered = new List<int>();
'@
Rep @'
            const int cols = 4;
            const float cw = 188f, ch = 128f, gap = 12f, pad = 26f;
            int shown = PewOnOffer ? Catalog.Length : PewIndex;          // (the pew is the last entry, and only there once floor 3 is done)
            int rows = Mathf.CeilToInt(shown / (float)cols);
'@ @'
            const int cols = 4;
            const float cw = 188f, gap = 12f, pad = 26f;
            offered.Clear();
            int resume = ResumeNow;
            for (int oi = 0; oi < Catalog.Length; oi++) if (OnOffer(oi, resume)) offered.Add(oi);
            int shown = offered.Count;                                    // (what is for sale now: the pew once floor 3 is done, a gun's ammunition once the gun was found and brought back)
            int rows = Mathf.CeilToInt(shown / (float)cols);
            float ch = rows > 4 ? 112f : 128f;                            // (more cards than fit: they get a little shorter)
'@
Rep @'
            for (int i = 0; i < shown; i++)
            {
                var p = Catalog[i];
'@ @'
            for (int i = 0; i < shown; i++)
            {
                var p = Catalog[offered[i]];
'@
Rep @'
                    ev.Use();
                    Buy(hm, i);
'@ @'
                    ev.Use();
                    Buy(hm, offered[i]);
'@
Rep @'
            if (p.isAmmo)
            {
                if (!PewOnOffer) return "The dealer has nothing like that for sale yet";
                if (GunOf(buyer) == null) return "You have no pew to load - buy the pistol first";
                if (!hm.TrySpend(p.price)) return $"Not enough cash (a clip costs ${p.price})";
                if (Net.NetRole.IsHost && buyer.TryGetComponent(out Net.NetPlayer ammoBuyer)) Net.RunSync.Instance.AnnounceAmmo(ammoBuyer.Slot, p.rounds);      // (every machine adds the rounds to that player's pew)
                else GiveAmmo(buyer, p.rounds);
                return $"Bought a clip: +{p.rounds} rounds for your pew";
            }
            if (p.isGun)
            {
                if (!PewOnOffer) return "The dealer has nothing like that for sale yet";
                if (!RangedWeapon.Available) return "Weapons aren't set up: run Employees Take Revenge > Setup Weapons";
                foreach (var held in buyer.Items) if (held && held.TryGetComponent(out RangedWeapon _)) return "You already have a pew";
            }
'@ @'
            if (!OnOffer(index, ResumeNow)) return "The dealer has nothing like that for sale yet";
            if (p.isAmmo)
            {
                var spec = RangedWeapon.Specs[(int)p.gun];
                var gun = GunOf(buyer, p.gun);
                if (gun == null) return p.gun == RangedWeapon.Kind.Pew ? "You have no pew to load - buy the pistol first" : $"You carry no {spec.name} to load";
                if (gun.Magazine.Room < p.rounds) return "You can't carry more than three clips";
                if (!hm.TrySpend(p.price)) return $"Not enough cash (a clip costs ${p.price})";
                if (Net.NetRole.IsHost && buyer.TryGetComponent(out Net.NetPlayer ammoBuyer)) Net.RunSync.Instance.AnnounceAmmo(ammoBuyer.Slot, p.rounds, p.gun);      // (every machine adds the rounds to that player's gun)
                else GiveAmmo(buyer, p.rounds, p.gun);
                return p.gun == RangedWeapon.Kind.Pew ? $"Bought a clip: +{p.rounds} rounds for your pew" : $"Bought {spec.ammoName}: +{p.rounds} rounds for your {spec.name}";
            }
            if (p.isGun)
            {
                if (!RangedWeapon.Available) return "Weapons aren't set up: run Employees Take Revenge > Setup Weapons";
                foreach (var held in buyer.Items) if (held && held.TryGetComponent(out RangedWeapon _)) return "You already have a gun";
            }
'@
Save-Src

# ---- Backpack: a clip fits its gun, not beyond three clips
Open-Src "$Src\Backpack.cs"
Rep @'
        public static string ClipTaken(int rounds) => "+" + rounds + " rounds for your pew";
'@ @'
        public static string ClipTaken(int rounds) => "+" + rounds + " rounds for your pew";
        /// <summary>The same for a clip of any gun: what a player is told who goes for a clip of a gun he does not carry, one who carries the most clips already, and who has taken one (placeholders: the owner writes the words).</summary>
        public static string NoGunForClip(RangedWeapon.Kind kind) => kind == RangedWeapon.Kind.Pew ? NoPewForClip : "You have no " + RangedWeapon.Specs[(int)kind].name + " for this clip";
        public const string TooManyClips = "You can't carry more than three clips";
        public static string ClipTaken(int rounds, RangedWeapon.Kind kind) => kind == RangedWeapon.Kind.Pew ? ClipTaken(rounds) : "+" + rounds + " rounds for your " + RangedWeapon.Specs[(int)kind].name;
'@
Rep @'
            if (!Vendor.GiveAmmo(this, clip.rounds)) { Say(NoPewForClip); return false; }
            int rounds = clip.rounds;
            Destroy(p.gameObject);
            if (Viewed) Say(ClipTaken(rounds));
            return true;
'@ @'
            var gun = Vendor.GunOf(this, clip.kind);
            if (gun == null) { Say(NoGunForClip(clip.kind)); return false; }
            if (gun.Magazine.Room < clip.rounds) { Say(TooManyClips); return false; }               // (three clips at most: the clip stays where it lies)
            Vendor.GiveAmmo(this, clip.rounds, clip.kind);
            int rounds = clip.rounds;
            Destroy(p.gameObject);
            if (Viewed) Say(ClipTaken(rounds, clip.kind));
            return true;
'@
Save-Src

# ---- RunSync: the clip of a gun, the announcement of ammunition
Open-Src "$Src\Net\RunSync.Requests.cs"
Rep @'
                if (Vendor.GunOf(bp) == null) { Notice(who, Backpack.NoPewForClip); return; }
                int rounds = clip.rounds;
                AnnounceAmmo(np.Slot, rounds);
                Notice(who, Backpack.ClipTaken(rounds));
'@ @'
                var clipGun = Vendor.GunOf(bp, clip.kind);
                if (clipGun == null) { Notice(who, Backpack.NoGunForClip(clip.kind)); return; }
                if (clipGun.Magazine.Room < clip.rounds) { Notice(who, Backpack.TooManyClips); return; }      // (three clips at most: the clip stays where it lies)
                int rounds = clip.rounds;
                AnnounceAmmo(np.Slot, rounds, clip.kind);
                Notice(who, Backpack.ClipTaken(rounds, clip.kind));
'@
Save-Src

Open-Src "$Src\Net\RunSync.Events.cs"
Rep @'
        public void AnnounceAmmo(int slot, int rounds)
        {
            if (IsServer && rounds > 0) AmmoBoughtRpc((byte)slot, (byte)Mathf.Min(255, rounds));
        }

        [Rpc(SendTo.ClientsAndHost)]
        void AmmoBoughtRpc(byte slot, byte rounds)
        {
            var np = NetPlayer.BySlot(slot);
            if (np && np.TryGetComponent(out Backpack bp)) Vendor.GiveAmmo(bp, rounds);
        }
'@ @'
        public void AnnounceAmmo(int slot, int rounds, RangedWeapon.Kind kind = RangedWeapon.Kind.Pew)
        {
            if (IsServer && rounds > 0) AmmoBoughtRpc((byte)slot, (byte)Mathf.Min(255, rounds), (byte)kind);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void AmmoBoughtRpc(byte slot, byte rounds, byte kind)
        {
            var np = NetPlayer.BySlot(slot);
            if (np && np.TryGetComponent(out Backpack bp) && kind < RangedWeapon.Specs.Length) Vendor.GiveAmmo(bp, rounds, (RangedWeapon.Kind)kind);
        }
'@
Save-Src

# ---- TestMode: a gun handed over also unlocks its ammunition at the dealer
Open-Src "$Src\TestMode.cs"
Rep @'
            pack.SetEquipped(p);
            Say(RangedWeapon.Specs[(int)kind].name + " in your hand: LMB fires, R reloads");
'@ @'
            pack.SetEquipped(p);
            RunUnlocks.Unlock(kind);                                       // (the dealer sells its ammunition now: it was "found")
            Say(RangedWeapon.Specs[(int)kind].name + " in your hand: LMB fires, R reloads");
'@
Save-Src
