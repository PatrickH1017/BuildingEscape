using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Basement shopkeeper. Stand in front of the counter: F sells every piece of loot inside the
    /// zone for cash (sellRate of its current value) and the number keys buy items, which appear
    /// on the counter as real objects you can grab (E) and carry up in the elevator.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Vendor : MonoBehaviour
    {
        public struct Product
        {
            public string name, blurb;
            public int price;
            public PrimitiveType shape;
            public Vector3 size;
            public float mass, stunSeconds, noise;
            public Color color;
            public bool isWeapon;
            public MeleeWeapon.Kind kind;
            /// <summary>A gun (the pew): made by <c>RangedWeapon.Create</c>.</summary>
            public bool isGun;
            public RangedWeapon.Kind gun;
            /// <summary>A clip of ammunition for the pew (the owner, 2026-10-18: "More ammo should need to be purchased from the Vendor"): not an item but <see cref="rounds"/> more rounds in the reserve of the pew in the buyer's pack (<see cref="GiveAmmo"/>).</summary>
            public bool isAmmo;
            public int rounds;
        }

        /// <summary>What the vendor sells. Key 1 buys the first entry, key 2 the second, and so on.</summary>
        public static readonly Product[] Catalog =
        {
            new Product
            {
                name = "Coffee Mug", blurb = "throw it to stun a guard for 2.5s", price = 60,
                shape = PrimitiveType.Cylinder, size = new Vector3(0.18f, 0.12f, 0.18f), mass = 0.4f,
                stunSeconds = 2.5f, noise = 14f, color = Color.white
            },
            new Product
            {
                name = "Heavy Stapler", blurb = "stuns a guard for 6s", price = 250,
                shape = PrimitiveType.Cube, size = new Vector3(0.45f, 0.2f, 0.2f), mass = 1.2f,
                stunSeconds = 6f, noise = 10f, color = new Color(0.45f, 0.5f, 0.6f)
            },
            new Product
            {
                name = "Squeaky Toy", blurb = "throw it far away to lure guards over", price = 150,
                shape = PrimitiveType.Sphere, size = new Vector3(0.3f, 0.3f, 0.3f), mass = 0.3f,
                stunSeconds = 0f, noise = 35f, color = new Color(1f, 0.9f, 0.1f)
            },
            new Product
            {
                name = "Fire Extinguisher", blurb = "stuns a guard for 10s", price = 600,
                shape = PrimitiveType.Cylinder, size = new Vector3(0.25f, 0.4f, 0.25f), mass = 2f,
                stunSeconds = 10f, noise = 20f, color = new Color(0.85f, 0.1f, 0.1f)
            },
            new Product
            {
                name = "Crowbar", blurb = "melee: LMB swing, stuns a guard 4s", price = 400,
                isWeapon = true, kind = MeleeWeapon.Kind.Crowbar
            },
            new Product
            {
                name = "Baseball Bat", blurb = "melee: LMB swing, big knockback, stun 3s", price = 300,
                isWeapon = true, kind = MeleeWeapon.Kind.Bat
            },
            new Product
            {
                name = "Hatchet", blurb = "melee: LMB swing, stuns a guard 6s", price = 550,
                isWeapon = true, kind = MeleeWeapon.Kind.Hatchet
            },
            new Product
            {
                name = "Pew Pistol", blurb = "gun: LMB fires, R reloads. 8 rounds a clip, 3 clips, 3 hits drop a guard. Try it on the dummy", price = PewPrice,
                isGun = true, gun = RangedWeapon.Kind.Pew
            },
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

        /// <summary>The pew's place in <see cref="Catalog"/> and the clips of ammunition right after it, so the places of the others are what they were.</summary>
        public const int PewIndex = 7, AmmoIndex = 8;
        /// <summary>What the pew costs (the owner, 2026-10-18: "The pistol should not be free from the Vendor anymore") and what a clip of eight rounds costs.</summary>
        public const int PewPrice = 900, AmmoClipPrice = 120;
        /// <summary>The pew is for sale once this floor has been finished (the owner, 2026-10-16: "after completion of the 4th floor the vendor should now sell the pew pistol"; 2026-10-17: the third floor, "so that players can buy the pistol after completing the third floor"; it was free until 2026-10-18).</summary>
        public const int PewAfterFloor = 3;

        /// <summary>Is the pew (and ammunition for it) for sale? <paramref name="resume"/> is the floor the elevator would go up to from the docks: past <see cref="PewAfterFloor"/> once that floor is finished.</summary>
        public static bool PewForSale(int resume) => resume > PewAfterFloor;

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

        public KeyCode sellKey = KeyCode.F;
        public KeyCode talkKey = KeyCode.E;
        [Tooltip("The dealer himself. You must be this close (and in the shop) to talk to him.")]
        public Transform npc;
        public float talkRange = 5.5f;

        /// <summary>True while the shop window is open (the cursor is free, other actions are paused).</summary>
        public static bool ShopOpen { get; private set; }

        /// <summary>Closes the shop window (an accepted invite closes every menu, see <see cref="MenuRegistry"/>).</summary>
        public static void CloseShop() { ShopOpen = false; }
        /// <summary>True when the player is close enough to the dealer to press E.</summary>
        public bool CanTalk { get; private set; }
        string msg; float msgUntil;
        [Range(0.1f, 1f)] [Tooltip("Share of a loot item's current value the vendor pays.")]
        public float sellRate = 0.7f;
        [Tooltip("Where bought items appear (above the counter).")]
        public Transform shelf;
        [Tooltip("Sideways gap between bought items on the shelf (five fit across).")]
        public float shelfSpacing = 0.9f;
        public Material itemMat;

        /// <summary>The vendor whose zone a player is standing in, if any (the HUD shows its shop).</summary>
        public static Vendor Nearby { get; private set; }

        public int LootCount { get { Tidy(); int n = loot.Count; foreach (var b in packs) n += b.LootCount; return n; } }

        /// <summary>Cash the loot currently in the zone would fetch.</summary>
        public int SellValue
        {
            get
            {
                Tidy();
                int total = 0;
                foreach (var l in loot) if (!l.Broken) total += Mathf.RoundToInt(l.CurrentValue * sellRate);
                foreach (var b in packs) total += b.LootValue(sellRate);
                return total;
            }
        }

        readonly HashSet<HeavyLoot> loot = new();
        readonly HashSet<Backpack> packs = new();
        int playersInside, spawned;
        Backpack buyer;                       // the pack whose owner is shopping on this machine (found every frame in Update)

        static readonly Color CantAfford = new Color(1f, 0.5f, 0.5f);

        /// <summary>The shop on this floor (there is one, in the basement).</summary>
        public static Vendor Current { get; private set; }

        void OnEnable() { Current = this; }
        void Reset() { GetComponent<Collider>().isTrigger = true; }

        void OnTriggerEnter(Collider o)
        {
            var l = o.GetComponentInParent<HeavyLoot>(); if (l) loot.Add(l);
            var bp = o.GetComponentInParent<Backpack>(); if (bp) packs.Add(bp);
            if (o.GetComponentInParent<WobblyPlayer>()) { playersInside++; Nearby = this; }
        }

        void OnTriggerExit(Collider o)
        {
            var l = o.GetComponentInParent<HeavyLoot>(); if (l) loot.Remove(l);
            var bp = o.GetComponentInParent<Backpack>(); if (bp) packs.Remove(bp);
            if (o.GetComponentInParent<WobblyPlayer>())
            {
                playersInside = Mathf.Max(0, playersInside - 1);
                if (playersInside == 0 && Nearby == this) Nearby = null;
            }
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (Nearby == this) { Nearby = null; ShopOpen = false; }
        }

        void Tidy()
        {
            loot.RemoveWhere(l => !l || !l.gameObject.activeInHierarchy);
            packs.RemoveWhere(b => !b);
        }

        Backpack Buyer()
        {
            packs.RemoveWhere(b => !b);
            Backpack first = null;
            foreach (var b in packs)
            {
                if (b.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) continue;   // pinned or busted: no shopping
                if (!first) first = b;
                if (b.Viewed) return b;
            }
            return Net.NetSession.Online ? null : first; // online, a teammate's pack never opens your shop
        }

        void Update()
        {
            buyer = playersInside > 0 ? Buyer() : null;
            CanTalk = buyer && npc && Vector3.Distance(buyer.transform.position, npc.position) <= talkRange;
            if (!CanTalk) { if (ShopOpen) ShopOpen = false; return; }
            var hm = HeistManager.Instance;
            if (!hm) return;
            // E talks to the dealer when the crosshair is on him (and shuts the shop again from inside it)
            if (Input.GetKeyDown(talkKey) && (ShopOpen || (Nearby == this && Crosshair.Aim.Kind == AimKind.Dealer))) ShopOpen = !ShopOpen;
            if (ShopOpen && Input.GetKeyDown(KeyCode.Escape)) ShopOpen = false;
            if (ShopOpen && Input.GetKeyDown(sellKey)) Sell(hm);
        }

        // ------------------------------------------------------------------ shop window

        void Say(string m) { msg = m; msgUntil = Time.time + 3f; }

        void OnGUI()
        {
            if (Nearby != this || !CanTalk) return;
            var hm = HeistManager.Instance;
            if (!hm) return;
            var label = HudStyles.Label(18);

            if (!ShopOpen)
            {
                var seen = Crosshair.Aim;
                if (seen.Kind == AimKind.Dealer)
                    HudWidgets.Prompt("[E] Talk to the dealer", Screen.height * 0.62f);
                else if (seen.Kind == AimKind.None)    // (nothing else is under the crosshair to prompt for)
                    HudWidgets.Prompt("Aim the crosshair at the dealer to talk", Screen.height * 0.62f, false, 18f);
                return;
            }

            // the shop window, in the same style as the rest of the menus (MenuKit: dimmed screen, cream-edged card, ribbon title, chunky buttons); reference pixels, scaled by HudTheme.S
            MenuKit.Begin();
            float S = HudTheme.S;
            const int cols = 4;
            const float cw = 188f, gap = 12f, pad = 26f;
            offered.Clear();
            int resume = ResumeNow;
            for (int oi = 0; oi < Catalog.Length; oi++) if (OnOffer(oi, resume)) offered.Add(oi);
            int shown = offered.Count;                                    // (what is for sale now: the pew once floor 3 is done, a gun's ammunition once the gun was found and brought back)
            int rows = Mathf.CeilToInt(shown / (float)cols);
            float ch = rows > 4 ? 112f : 128f;                            // (more cards than fit: they get a little shorter)
            var busted = BustedPlayers();
            float pw = (pad * 2 + cols * cw + (cols - 1) * gap) * S;
            float ph = (96f + rows * (ch + gap) + 62f + (busted.Count > 0 ? 44f + busted.Count * 52f : 0f) + 40f) * S;
            var r = new Rect((Screen.width - pw) / 2f, (Screen.height - ph) / 2f + 20f * S, pw, ph);
            MenuKit.Dim(0.45f);
            MenuKit.Sheet(r);
            MenuKit.Ribbon(r, "BLACK MARKET  -  no questions asked", HudTheme.Danger, 30f);

            // cash and pack chips
            float cy = r.y + 40f * S;
            var cashR = new Rect(r.x + pad * S, cy, 190f * S, 34f * S);
            HudTheme.Chip(cashR, "$" + hm.Cash, HudTheme.Cash, 20f, HudTheme.Ink);
            var packR = new Rect(cashR.xMax + 10f * S, cy, 330f * S, 34f * S);
            HudTheme.Chip(packR, "Pack " + WeightUnits.Num(buyer ? buyer.Weight : 0) + " / " + WeightUnits.Lbs(buyer ? buyer.capacity : 0), HudTheme.Info, 18f, HudTheme.Ink);
            MenuKit.Label(new Rect(packR.xMax + 14f * S, cy, r.xMax - packR.xMax - 40f * S, 34f * S), "Hover an item and left click to buy it.", 16f, HudTheme.Muted);

            var ev = Event.current;
            float y0 = r.y + 90f * S;
            for (int i = 0; i < shown; i++)
            {
                var p = Catalog[offered[i]];
                var cr = new Rect(r.x + (pad + (i % cols) * (cw + gap)) * S, y0 + (i / cols) * (ch + gap) * S, cw * S, ch * S);
                bool hover = cr.Contains(ev.mousePosition);
                bool afford = hm.Cash >= p.price;
                if (ev.type == EventType.Repaint)
                {
                    var fill = hover ? (afford ? HudTheme.Light(HudTheme.Card, 0.30f) : HudTheme.Shade(HudTheme.Danger, 0.45f)) : HudTheme.Light(HudTheme.Card, 0.12f);
                    HudTheme.Body(cr, fill, 14f, 3f, hover ? 4f : 2.5f, true, 0f);
                    HudTheme.Text(new Rect(cr.x + 12f * S, cr.y + 6f * S, cr.width - 24f * S, 28f * S), p.name, 20f, HudTheme.Bright, TextAnchor.MiddleLeft, false);
                    string price = p.price == 0 ? "FREE" : "$" + p.price;
                    string detail = p.isAmmo ? "+" + p.rounds + " rounds" : WeightUnits.Lbs(ItemWeight(p));
                    HudTheme.Text(new Rect(cr.x + 12f * S, cr.y + 34f * S, cr.width - 24f * S, 24f * S), price + "    " + detail, 17f, afford ? HudTheme.Cash : HudTheme.Lost, TextAnchor.MiddleLeft, false);
                    MenuKit.Wrapped(new Rect(cr.x + 12f * S, cr.y + 62f * S, cr.width - 24f * S, cr.height - 66f * S), p.blurb, 14f * S, HudTheme.Muted);
                }
                if (hover && ev.type == EventType.MouseDown && ev.button == 0)
                {
                    ev.Use();
                    Buy(hm, offered[i]);
                }
            }

            float by = y0 + rows * (ch + gap) * S + 6f * S;
            int lootCount = LootCount;
            string sell = lootCount > 0 ? $"Sell hot goods: {lootCount} item(s) for ${SellValue}  [F]" : "Nothing to sell (loot in your pack or on the counter)";
            if (MenuKit.Button(new Rect(r.x + pad * S, by, 560f * S, 46f * S), sell, HudTheme.Cash, 20f, lootCount > 0)) Sell(hm);
            if (MenuKit.Button(new Rect(r.xMax - (pad + 160f) * S, by, 160f * S, 46f * S), "Leave [E]", HudTheme.Danger, 20f)) ShopOpen = false;
            float my = by + 56f * S;
            if (busted.Count > 0)
            {
                // getting friends out of custody: he knows someone on the inside
                MenuKit.Label(new Rect(r.x + pad * S, my, r.width - pad * 2f * S, 32f * S), "\"" + CustodyRules.BuyBackLine + "\"", 18f, HudTheme.Cream);
                my += 40f * S;
                foreach (var bp in busted)
                {
                    int price = CustodyRules.BuyBackPrice(bp.BustedOnLevel);
                    if (MenuKit.Button(new Rect(r.x + pad * S, my, 560f * S, 44f * S), $"Buy back {LabelOf(bp)}  -  ${price}", HudTheme.Warning, 19f, CustodyRules.CanAffordBuyBack(hm.Cash, bp.BustedOnLevel))) BuyBackPlayer(hm, bp);
                    my += 52f * S;
                }
            }
            if (Time.time < msgUntil) MenuKit.Notice(new Rect(r.x + pad * S, r.yMax - 52f * S, r.width - pad * 2f * S, 34f * S), msg, HudTheme.Info, 17f);
        }
        // ------------------------------------------------------------------ buy-back (busted teammates)

        static readonly List<WobblyPlayer> bustedList = new();

        /// <summary>The players currently under arrest.</summary>
        public static List<WobblyPlayer> BustedPlayers()
        {
            bustedList.Clear();
            foreach (var p in WobblyPlayerRegistry.All) if (p && p.Custody == CustodyState.Busted) bustedList.Add(p);
            return bustedList;
        }

        static string LabelOf(WobblyPlayer p)
        {
            return p.TryGetComponent(out Net.NetPlayer np) && np.IsSpawned ? $"Player {np.Slot + 1}" : "your friend";
        }

        /// <summary>Pay the vendor to get a busted player out. A client only asks; the host / offline does it.</summary>
        void BuyBackPlayer(HeistManager hm, WobblyPlayer p)
        {
            if (!p) return;
            if (Net.NetRole.IsRemoteClient)
            {
                if (p.TryGetComponent(out Net.NetPlayer np) && Net.RunSync.Instance) Net.RunSync.Instance.RequestBuyBack(np.Slot);
                return;
            }
            Say(BuyBackFor(hm, p));
        }

        /// <summary>Host: a client asked to buy a teammate back. The asking player must be at the counter.</summary>
        public string HostBuyBack(Net.NetPlayer who, int slot)
        {
            var hm = HeistManager.Instance;
            Backpack asker = null;
            if (who) who.TryGetComponent(out asker);
            packs.RemoveWhere(b => !b);
            if (!hm || !asker) return null;
            bool down = who.TryGetComponent(out WobblyPlayer wp) && wp.InCustody;
            if (!packs.Contains(asker) || down) return "Stand at the counter to talk to the dealer";
            var target = Net.NetPlayer.BySlot(slot);
            WobblyPlayer friend = null;
            if (target) target.TryGetComponent(out friend);
            return BuyBackFor(hm, friend);
        }

        /// <summary>The buy-back rules (offline and host). Returns the message for the buyer.</summary>
        string BuyBackFor(HeistManager hm, WobblyPlayer p)
        {
            if (!p || p.Custody != CustodyState.Busted) return "Nobody to buy back";
            int price = CustodyRules.BuyBackPrice(p.BustedOnLevel);
            if (!hm.TrySpend(price)) return $"Not enough cash (it costs ${price})";
            hm.ReleaseBusted(p);
            return "Done. Your friend walks out the back.";
        }

        static float ItemWeight(Product p) => p.isGun ? RangedWeapon.Specs[(int)p.gun].weight : p.isWeapon ? MeleeWeapon.Specs[(int)p.kind].weight : Mathf.Max(0.2f, p.mass);

        /// <summary>Sell everything in the zone. A client only asks; the host / offline sells and tells everyone.</summary>
        void Sell(HeistManager hm)
        {
            if (Net.NetRole.IsRemoteClient) { Net.RunSync.Instance?.RequestSell(); return; }
            Tidy();
            int total = 0;
            foreach (var b in packs)
                foreach (var l in b.TakeLoot())
                {
                    total += Mathf.RoundToInt(l.CurrentValue * sellRate);
                    Consumed(l);
                    Destroy(l.gameObject);
                }
            foreach (var l in new List<HeavyLoot>(loot))
            {
                if (!l || l.Broken) continue;
                total += Mathf.RoundToInt(l.CurrentValue * sellRate);
                Consumed(l);
                Destroy(l.gameObject);
            }
            loot.Clear();
            hm.AddCash(total);
        }

        static void Consumed(Component c)
        {
            if (c.TryGetComponent(out Pickup p)) Net.RunSync.Instance?.BroadcastConsumed(p.NetId);
        }

        /// <summary>Host: a client asked to sell. The player must be at the counter.</summary>
        public string HostSell(Net.NetPlayer who)
        {
            var hm = HeistManager.Instance;
            var pack = who ? who.GetComponent<Backpack>() : null;
            packs.RemoveWhere(b => !b);
            if (hm == null || pack == null || !packs.Contains(pack)) return "Stand at the counter to sell";
            int before = hm.Cash;
            Sell(hm);
            return hm.Cash > before ? $"Sold for ${hm.Cash - before}" : "Nothing to sell";
        }

        void Buy(HeistManager hm, int index)
        {
            if (Net.NetRole.IsRemoteClient) { Net.RunSync.Instance?.RequestBuy(index); return; }
            var buyer = Buyer();
            if (!buyer) return;
            Say(BuyFor(hm, buyer, index));
        }

        /// <summary>Host: a client asked to buy. The player must be at the counter.</summary>
        public string HostBuy(Net.NetPlayer who, int index)
        {
            var hm = HeistManager.Instance;
            var buyer = who ? who.GetComponent<Backpack>() : null;
            packs.RemoveWhere(b => !b);
            if (hm == null || buyer == null || index < 0 || index >= Catalog.Length) return null;
            if (!packs.Contains(buyer)) return "Stand at the counter to buy";
            return BuyFor(hm, buyer, index);
        }

        /// <summary>The purchase rules (offline and host). Returns the message for the buyer.</summary>
        string BuyFor(HeistManager hm, Backpack buyer, int index)
        {
            var p = Catalog[index];
            if (p.isWeapon && !MeleeWeapon.Available) return "Weapons aren't set up: run Employees Take Revenge > Setup Weapons";
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
            float wgt = ItemWeight(p);
            if (buyer.Weight + wgt > buyer.capacity + 0.001f) return $"No room in your pack for the {p.name} ({WeightUnits.Lbs(wgt)})";
            if (!hm.TrySpend(p.price)) return p.price > 0 ? $"Not enough cash (it costs ${p.price})" : "Not enough cash";
            var item = Spawn(p, index);
            if (!item) return $"Bought {p.name}";
            var pk = item.GetComponent<Pickup>();
            if (Net.NetRole.IsHost && buyer.TryGetComponent(out Net.NetPlayer np))
            {
                Net.RunSync.Instance.AnnounceStored(pk.NetId, np.Slot); // straight into the buyer's pack, on every machine
                return $"Bought {p.name} - it's in your pack";
            }
            return buyer.TryStore(pk) ? $"Bought {p.name} - it's in your pack" : $"Bought {p.name} - it's on the counter";
        }

        /// <summary>Makes a bought item on the shelf and, in a networked run, tells the clients to make it too.</summary>
        GameObject Spawn(Product p, int index)
        {
            var basePos = shelf ? shelf.position : transform.position + Vector3.up;
            float x = ((spawned++ % 5) - 2) * shelfSpacing;
            float yaw = Random.Range(0f, 360f);
            var go = SpawnAt(p, basePos + new Vector3(x, p.isWeapon || p.isGun ? 0.3f : 0.2f, 0f), yaw);
            if (go && go.TryGetComponent(out Pickup pk)) Net.RunSync.Instance?.BroadcastSpawnItem(pk.NetId, index, go.transform.position, yaw);
            return go;
        }

        /// <summary>Client: build the item the host announced, with the host's id.</summary>
        public void SpawnFromNet(int id, int product, Vector3 pos, float yaw)
        {
            if (product < 0 || product >= Catalog.Length) return;
            Net.NetIds.PendingId = id;
            SpawnAt(Catalog[product], pos, yaw);
            Net.NetIds.PendingId = Net.NetIds.None;
        }

        GameObject SpawnAt(Product p, Vector3 pos, float yaw)
        {
            if (p.isGun) return RangedWeapon.Create(p.gun, pos, transform.parent, yaw);
            if (p.isWeapon) return MeleeWeapon.Create(p.kind, pos, transform.parent, yaw);

            var go = GameObject.CreatePrimitive(p.shape);
            go.name = p.name;
            go.transform.SetParent(transform.parent); // the Vendor root, so it is cleaned up with the floor
            go.transform.position = pos;
            go.transform.localScale = p.size;

            var r = go.GetComponent<Renderer>();
            if (itemMat) r.sharedMaterial = itemMat;
            var pb = new MaterialPropertyBlock();
            pb.SetColor("_Color", p.color);
            pb.SetColor("_BaseColor", p.color);
            r.SetPropertyBlock(pb);

            var rb = go.AddComponent<Rigidbody>(); rb.mass = p.mass;
            var pk = go.AddComponent<Pickup>();
            pk.weight = Mathf.Max(0.2f, p.mass);
            pk.bankValue = Mathf.RoundToInt(p.price * 0.25f / 5f) * 5; // worth less than it cost, so no free money
            var t = go.AddComponent<Throwable>();
            t.stunSeconds = p.stunSeconds;
            t.impactNoise = p.noise;
            return go;
        }
    }
}
