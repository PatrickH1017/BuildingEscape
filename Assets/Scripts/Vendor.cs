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
        };

        public KeyCode sellKey = KeyCode.F;
        public KeyCode talkKey = KeyCode.E;
        [Tooltip("The dealer himself. You must be this close (and in the shop) to talk to him.")]
        public Transform npc;
        public float talkRange = 5.5f;

        /// <summary>True while the shop window is open (the cursor is free, other actions are paused).</summary>
        public static bool ShopOpen { get; private set; }
        /// <summary>True when the player is close enough to the dealer to press E.</summary>
        public bool CanTalk { get; private set; }
        string msg; float msgUntil;
        [Range(0.1f, 1f)] [Tooltip("Share of a loot item's current value the vendor pays.")]
        public float sellRate = 0.7f;
        [Tooltip("Where bought items appear (above the counter).")]
        public Transform shelf;
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

        void OnDestroy() { if (Nearby == this) { Nearby = null; ShopOpen = false; } }

        void Tidy()
        {
            loot.RemoveWhere(l => !l || !l.gameObject.activeInHierarchy);
            packs.RemoveWhere(b => !b);
        }

        Backpack Buyer()
        {
            packs.RemoveWhere(b => !b);
            Backpack first = null;
            foreach (var b in packs) { if (!first) first = b; if (b.Viewed) return b; }
            return first;
        }

        void Update()
        {
            var buyer = playersInside > 0 ? Buyer() : null;
            CanTalk = buyer && npc && Vector3.Distance(buyer.transform.position, npc.position) <= talkRange;
            if (!CanTalk) { if (ShopOpen) ShopOpen = false; return; }
            var hm = HeistManager.Instance;
            if (!hm) return;
            if (Input.GetKeyDown(talkKey)) ShopOpen = !ShopOpen;
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
            var label = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };

            if (!ShopOpen)
            {
                GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 36), "[E] Talk to the dealer",
                    new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 24 });
                return;
            }

            const int cols = 4;
            const float cw = 176f, ch = 130f, gap = 8f, pad = 14f;
            int rows = Mathf.CeilToInt(Catalog.Length / (float)cols);
            float pw = pad * 2 + cols * cw + (cols - 1) * gap;
            float ph = 120f + rows * (ch + gap) + 70f;
            var r = new Rect((Screen.width - pw) / 2f, (Screen.height - ph) / 2f, pw, ph);
            GUI.Box(r, "BLACK MARKET  -  no questions asked");

            var buyer = Buyer();
            GUI.Label(new Rect(r.x + pad, r.y + 28, pw - pad * 2, 28),
                $"Cash ${hm.Cash}      Pack {(buyer ? buyer.Weight : 0):0.#} / {(buyer ? buyer.capacity : 0):0.#} kg      Hover an item and left click to buy it.", label);

            var ev = Event.current;
            float y0 = r.y + 64f;
            for (int i = 0; i < Catalog.Length; i++)
            {
                var p = Catalog[i];
                var cr = new Rect(r.x + pad + (i % cols) * (cw + gap), y0 + (i / cols) * (ch + gap), cw, ch);
                bool hover = cr.Contains(ev.mousePosition);
                bool afford = hm.Cash >= p.price;
                var old = GUI.color;
                GUI.color = hover ? (afford ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.5f, 0.5f)) : Color.white;
                GUI.Box(cr, GUIContent.none);
                GUI.color = old;
                var small = new GUIStyle(label) { fontSize = 14 };
                GUI.Label(new Rect(cr.x + 8, cr.y + 4, cw - 16, 26), p.name, new GUIStyle(label) { fontStyle = FontStyle.Bold });
                GUI.Label(new Rect(cr.x + 8, cr.y + 30, cw - 16, 22), $"${p.price}   {ItemWeight(p):0.#} kg", afford ? label : new GUIStyle(label) { normal = { textColor = new Color(1f, 0.5f, 0.5f) } });
                GUI.Label(new Rect(cr.x + 8, cr.y + 56, cw - 16, ch - 60), p.blurb, small);
                if (hover && ev.type == EventType.MouseDown && ev.button == 0)
                {
                    ev.Use();
                    Buy(hm, i);
                }
            }

            float by = y0 + rows * (ch + gap) + 4f;
            string sell = LootCount > 0 ? $"Sell hot goods: {LootCount} item(s) for ${SellValue}  [F]" : "Nothing to sell (loot in your pack or on the counter)";
            var was = GUI.enabled; GUI.enabled = LootCount > 0;
            if (GUI.Button(new Rect(r.x + pad, by, 470, 32), sell)) Sell(hm);
            GUI.enabled = was;
            if (GUI.Button(new Rect(r.xMax - pad - 120, by, 120, 32), "Leave [E]")) ShopOpen = false;
            if (Time.time < msgUntil) GUI.Label(new Rect(r.x + pad, by + 36, pw - pad * 2, 28), msg, label);
        }

        static float ItemWeight(Product p) => p.isWeapon ? MeleeWeapon.Specs[(int)p.kind].weight : Mathf.Max(0.2f, p.mass);

        void Sell(HeistManager hm)
        {
            Tidy();
            int total = 0;
            foreach (var b in packs)
                foreach (var l in b.TakeLoot())
                {
                    total += Mathf.RoundToInt(l.CurrentValue * sellRate);
                    Destroy(l.gameObject);
                }
            foreach (var l in new List<HeavyLoot>(loot))
            {
                if (!l || l.Broken) continue;
                total += Mathf.RoundToInt(l.CurrentValue * sellRate);
                Destroy(l.gameObject);
            }
            loot.Clear();
            hm.AddCash(total);
        }

        void Buy(HeistManager hm, int index)
        {
            var p = Catalog[index];
            if (p.isWeapon && !MeleeWeapon.Available) { Say("Weapons aren't set up: run Overtime Escape > Setup Weapons"); return; }
            var buyer = Buyer();
            if (!buyer) return;
            float wgt = ItemWeight(p);
            if (buyer.Weight + wgt > buyer.capacity + 0.001f) { Say($"No room in your pack for the {p.name} ({wgt:0.#} kg)"); return; }
            if (!hm.TrySpend(p.price)) { Say("Not enough cash"); return; }
            var item = Spawn(p);
            if (item && buyer.TryStore(item.GetComponent<Pickup>())) Say($"Bought {p.name} - it's in your pack");
            else Say($"Bought {p.name} - it's on the counter");
        }

        GameObject Spawn(Product p)
        {
            if (p.isWeapon)
            {
                var basePos0 = shelf ? shelf.position : transform.position + Vector3.up;
                return MeleeWeapon.Create(p.kind, basePos0 + new Vector3(((spawned++ % 5) - 2) * 0.9f, 0.3f, 0f), transform.parent);
            }
            var go = GameObject.CreatePrimitive(p.shape);
            go.name = p.name;
            go.transform.SetParent(transform.parent); // the Vendor root, so it is cleaned up with the floor
            var basePos = shelf ? shelf.position : transform.position + Vector3.up;
            float x = ((spawned++ % 5) - 2) * 0.9f;
            go.transform.position = basePos + new Vector3(x, 0.2f, 0f);
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
