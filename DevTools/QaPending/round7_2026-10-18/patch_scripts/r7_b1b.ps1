# Round 7, batch B1b: the vendor sells the pew for money and sells clips of ammunition; the host announces a sale of ammunition to every machine. BE_qa (= C:\bq) only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\Vendor.cs')
Rep @'
            public bool isGun;
            public RangedWeapon.Kind gun;
        }
'@ @'
            public bool isGun;
            public RangedWeapon.Kind gun;
            /// <summary>A clip of ammunition for the pew (the owner, 2026-10-18: "More ammo should need to be purchased from the Vendor"): not an item but <see cref="rounds"/> more rounds in the reserve of the pew in the buyer's pack (<see cref="GiveAmmo"/>).</summary>
            public bool isAmmo;
            public int rounds;
        }
'@
Rep @'
                name = "Pew Pistol", blurb = "gun: LMB fires, R reloads. Free after floor 3 - try it on the dummy", price = 0,
                isGun = true, gun = RangedWeapon.Kind.Pew
            },
        };
'@ @'
                name = "Pew Pistol", blurb = "gun: LMB fires, R reloads. 8 rounds a clip, 3 clips, 3 hits drop a guard. Try it on the dummy", price = PewPrice,
                isGun = true, gun = RangedWeapon.Kind.Pew
            },
            new Product
            {
                name = "Pistol Clip", blurb = "8 more rounds for your pew: it reloads from the clips you carry", price = AmmoClipPrice,
                isAmmo = true, rounds = 8
            },
        };
'@
Rep @'
        /// <summary>The pew's place in <see cref="Catalog"/>: the last entry, so the places of the others are what they were.</summary>
        public const int PewIndex = 7;
        /// <summary>The pew is for sale once this floor has been finished (the owner, 2026-10-16: "after completion of the 4th floor the vendor should now sell the pew pistol for free"; 2026-10-17: the third floor, "so that players can buy the pistol after completing the third floor").</summary>
        public const int FreePewAfterFloor = 3;

        /// <summary>Is the pew for sale? <paramref name="resume"/> is the floor the elevator would go up to from the docks: past <see cref="FreePewAfterFloor"/> once that floor is finished.</summary>
        public static bool PewForSale(int resume) => resume > FreePewAfterFloor;
'@ @'
        /// <summary>The pew's place in <see cref="Catalog"/> and the clips of ammunition right after it, so the places of the others are what they were.</summary>
        public const int PewIndex = 7, AmmoIndex = 8;
        /// <summary>What the pew costs (the owner, 2026-10-18: "The pistol should not be free from the Vendor anymore") and what a clip of eight rounds costs.</summary>
        public const int PewPrice = 900, AmmoClipPrice = 120;
        /// <summary>The pew is for sale once this floor has been finished (the owner, 2026-10-16: "after completion of the 4th floor the vendor should now sell the pew pistol"; 2026-10-17: the third floor, "so that players can buy the pistol after completing the third floor"; it was free until 2026-10-18).</summary>
        public const int PewAfterFloor = 3;

        /// <summary>Is the pew (and ammunition for it) for sale? <paramref name="resume"/> is the floor the elevator would go up to from the docks: past <see cref="PewAfterFloor"/> once that floor is finished.</summary>
        public static bool PewForSale(int resume) => resume > PewAfterFloor;

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
'@
Rep @'
                    string price = p.price == 0 ? "FREE" : "$" + p.price;
                    HudTheme.Text(new Rect(cr.x + 12f * S, cr.y + 34f * S, cr.width - 24f * S, 24f * S), price + "    " + WeightUnits.Lbs(ItemWeight(p)), 17f, afford ? HudTheme.Cash : HudTheme.Lost, TextAnchor.MiddleLeft, false);
'@ @'
                    string price = p.price == 0 ? "FREE" : "$" + p.price;
                    string detail = p.isAmmo ? "+" + p.rounds + " rounds" : WeightUnits.Lbs(ItemWeight(p));
                    HudTheme.Text(new Rect(cr.x + 12f * S, cr.y + 34f * S, cr.width - 24f * S, 24f * S), price + "    " + detail, 17f, afford ? HudTheme.Cash : HudTheme.Lost, TextAnchor.MiddleLeft, false);
'@
Rep @'
            if (p.isWeapon && !MeleeWeapon.Available) return "Weapons aren't set up: run Employees Take Revenge > Setup Weapons";
            if (p.isGun)
'@ @'
            if (p.isWeapon && !MeleeWeapon.Available) return "Weapons aren't set up: run Employees Take Revenge > Setup Weapons";
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
'@
Rep @'
            if (!hm.TrySpend(p.price)) return "Not enough cash";
            var item = Spawn(p, index);
'@ @'
            if (!hm.TrySpend(p.price)) return p.price > 0 ? $"Not enough cash (it costs ${p.price})" : "Not enough cash";
            var item = Spawn(p, index);
'@
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Net\RunSync.Events.cs')
Rep @'
        // ------------------------------------------------------------------ carrying

        [Rpc(SendTo.ClientsAndHost)]
        void GrabbedRpc(ushort id, byte slot)
'@ @'
        // ------------------------------------------------------------------ ammunition

        /// <summary>Host: the dealer sold a clip to this player. Every machine (the host's too, through the call below) adds the rounds to the reserve of the pew in that player's pack.</summary>
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

        // ------------------------------------------------------------------ carrying

        [Rpc(SendTo.ClientsAndHost)]
        void GrabbedRpc(ushort id, byte slot)
'@
Save-Src

Open-Src (Join-Path $Dir 'Scripts\QaAutopilot.cs')
Rep 'bool pewNow = floor >= Vendor.FreePewAfterFloor;' 'bool pewNow = floor >= Vendor.PewAfterFloor;'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Outpost\OutpostLoot.cs')
Rep '<see cref="Vendor.FreePewAfterFloor"/>' '<see cref="Vendor.PewAfterFloor"/>'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\HeistHud.cs')
Rep @'
                // the demo's last floor is done: the vendor has a free pew now (Vendor.PewForSale)
                HudWidgets.Banner("DEMO COMPLETE", "That was the last floor of the demo. The vendor has a pew pistol for free: try it on the training dummy", HudTheme.Cash,
'@ @'
                // the demo's last floor is done: the vendor sells the pew now (Vendor.PewForSale)
                HudWidgets.Banner("DEMO COMPLETE", "That was the last floor of the demo. The vendor sells a pew pistol and clips for it now: try it on the training dummy", HudTheme.Cash,
'@
Save-Src
Write-Output 'round 7 batch B1b patched'
