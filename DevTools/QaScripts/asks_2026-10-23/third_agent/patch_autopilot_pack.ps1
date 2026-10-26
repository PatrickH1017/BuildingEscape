# QA gap found by the owner's playtest (2026-10-23): "players couldn't unequip and re-equip their melee weapons and they couldn't drop them". The built-player autopilot (the game as the friends play it: IL2CPP, a host and a joined client) never put a weapon in a hand,
# away, in a hand again, on the floor and back in the pack. It does now, solo and on two built players: the player buys a crowbar at the dealer's counter, holds it out, puts it away, holds it out again, drops it, takes it up again; the OTHER machine
# watches every step on its copy of his pack (a client's pack is only a copy on the host, and the other way round).
# Also Vendor.TestBuy (a solo run has no RunSync to ask: the dealer's own buy for the autopilot).
# Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }

Open-Src ($Root + 'Scripts\Vendor.cs')
Rep '        void Buy(HeistManager hm, int index)' @'
        /// <summary>The built-player autopilot buys without a click (a solo run has no RunSync to ask).</summary>
        public void TestBuy(int index) => Buy(HeistManager.Instance, index);

        void Buy(HeistManager hm, int index)
'@
Save-Src

Open-Src ($Root + 'Scripts\QaAutopilot.cs')
Rep '            Check("before the demo is over the vendor has no pew for sale", !Vendor.PewForSale(tm.ResumeLevel));' @'
            Check("before the demo is over the vendor has no pew for sale", !Vendor.PewForSale(tm.ResumeLevel));
            yield return PackActs("solo");
'@
Rep '            yield return ArmourNet(true);' @'
            yield return ArmourNet(true);
            yield return PackNet(true);
'@
Rep '            yield return ArmourNet(false);' @'
            yield return ArmourNet(false);
            yield return PackNet(false);
'@
Rep '        // ------------------------------------------------------------------ the run-start cinematic (the Intro) in a built player (DEVELOPER_NOTES 2.19)' @'
        // ------------------------------------------------------------------ the pack in a built player (the owner's playtest of 2026-10-23: "players couldn't unequip and re-equip their melee weapons and they couldn't drop them")

        static Pickup MeleeOf(Backpack b) { if (b == null) return null; foreach (var i in b.Items) if (i && i.GetComponent<MeleeWeapon>()) return i; return null; }
        static bool HoldsMelee(Backpack b) => b != null && b.Equipped && b.Equipped.GetComponent<MeleeWeapon>();

        /// <summary>The local player's round with a crowbar: buys it at the counter, holds it out, puts it away, holds it out again, drops it and takes it up again - each step through the pack's own calls (what the EQUIP / PUT AWAY / DROP buttons do), waiting for the answer a client gets from the host.</summary>
        IEnumerator PackActs(string who)
        {
            var wob = NetPlayer.Local != null ? NetPlayer.Local.GetComponent<WobblyPlayer>() : (WobblyPlayerRegistry.All.Count > 0 ? WobblyPlayerRegistry.All[0] : null);
            var rb = wob != null ? wob.GetComponent<Rigidbody>() : null;
            var pack = wob != null ? wob.GetComponent<Backpack>() : null;
            var counter = Vendor.Current != null ? Vendor.Current.GetComponent<Collider>() : null;
            if (wob == null || rb == null || pack == null || counter == null) { Check($"{who}: my body, my pack and the dealer's counter are here", false); yield break; }
            bool online = NetSession.Online;
            if (!online && HeistManager.Instance != null) HeistManager.Instance.AddCash(500);
            wob.TeleportTo(counter.bounds.center + Vector3.up * 0.2f, wob.transform.rotation);           // (the host sells only to somebody at the counter)
            rb.linearVelocity = Vector3.zero;
            yield return Wait(2.5f);                                                                 // (the host's copy of me has to arrive)
            if (online) RunSync.Instance.RequestBuy(4); else Vendor.Current.TestBuy(4);              // (the crowbar)
            yield return Until(() => MeleeOf(pack) != null, 60f, "the crowbar in my pack");
            var crowbar = MeleeOf(pack);
            Check($"{who}: the crowbar I bought is in my pack", crowbar != null);
            if (crowbar == null) yield break;
            yield return Wait(1.0f);
            pack.SetEquipped(crowbar);
            yield return Until(() => pack.Equipped == crowbar, 30f, "the crowbar in my hand");
            Check($"{who}: EQUIP holds the crowbar out", pack.Equipped == crowbar);
            yield return Wait(1.0f);
            pack.SetEquipped(null);
            yield return Until(() => pack.Equipped == null, 30f, "the crowbar put away");
            Check($"{who}: PUT AWAY empties the hand and the crowbar stays in the pack", pack.Equipped == null && pack.Items.Contains(crowbar));
            yield return Wait(1.0f);
            pack.SetEquipped(crowbar);
            yield return Until(() => pack.Equipped == crowbar, 30f, "the crowbar in my hand again");
            Check($"{who}: EQUIP again holds the crowbar out again", pack.Equipped == crowbar);
            yield return Wait(1.0f);
            pack.Drop(crowbar);
            yield return Until(() => !pack.Items.Contains(crowbar), 30f, "the crowbar dropped");
            Check($"{who}: DROP puts the crowbar on the floor next to me, out of the pack and out of my hand", !pack.Items.Contains(crowbar) && crowbar && crowbar.gameObject.activeInHierarchy && pack.Equipped != crowbar && Vector3.Distance(crowbar.transform.position, wob.transform.position) < 4f,
                $"(in pack {pack.Items.Contains(crowbar)}, in hand {pack.Equipped == crowbar}, {(crowbar ? Vector3.Distance(crowbar.transform.position, wob.transform.position) : -1f):0.0} m away)");
            yield return Wait(1.5f);
            if (crowbar) pack.TryStore(crowbar);
            yield return Until(() => crowbar && pack.Items.Contains(crowbar), 30f, "the crowbar taken up again");
            Check($"{who}: E on the dropped crowbar takes it up again", crowbar && pack.Items.Contains(crowbar));
            yield return Wait(1.0f);
        }

        /// <summary>The other player's copy of the same round, watched from this machine: his pack here shows every step he takes.</summary>
        IEnumerator PackWatch(string who, Backpack rPack, string whose)
        {
            yield return Until(() => MeleeOf(rPack) != null, 120f, $"{whose} crowbar in his pack");
            var cb = MeleeOf(rPack);
            Check($"{who}: {whose} crowbar is in his pack here", cb != null);
            if (cb == null) yield break;
            yield return Until(() => HoldsMelee(rPack), 60f, $"{whose} crowbar in his hand");
            Check($"{who}: I see {whose} crowbar in his hand", HoldsMelee(rPack));
            yield return Until(() => rPack.Equipped == null, 60f, $"{whose} crowbar put away");
            Check($"{who}: I see him put it away (it stays in his pack)", rPack.Equipped == null && rPack.Items.Contains(cb));
            yield return Until(() => HoldsMelee(rPack), 60f, $"{whose} crowbar in his hand again");
            Check($"{who}: I see it in his hand again", HoldsMelee(rPack));
            yield return Until(() => !rPack.Items.Contains(cb), 60f, $"{whose} crowbar dropped");
            Check($"{who}: I see it drop out of his pack onto the floor", !rPack.Items.Contains(cb) && cb && cb.gameObject.activeInHierarchy && rPack.Equipped != cb);
            yield return Until(() => rPack.Items.Contains(cb), 60f, $"{whose} crowbar taken up again");
            Check($"{who}: I see him take it up again", rPack.Items.Contains(cb));
        }

        /// <summary>Two built players: the client does the round while the host watches, then the host does it while the client watches.</summary>
        IEnumerator PackNet(bool host)
        {
            string who = host ? "host" : "client";
            NetPlayer remote = null;
            foreach (var np in NetPlayer.All) if (np && !np.IsOwner) remote = np;
            var rPack = remote != null ? remote.GetComponent<Backpack>() : null;
            if (rPack == null) { Check($"{who}: the other player's pack is here", false); yield break; }
            if (host) { yield return PackWatch(who, rPack, "the client's"); yield return PackActs(who); }
            else { yield return PackActs(who); yield return PackWatch(who, rPack, "the host's"); }
        }

        // ------------------------------------------------------------------ the run-start cinematic (the Intro) in a built player (DEVELOPER_NOTES 2.19)
'@
Save-Src
"autopilot pack round patched"
