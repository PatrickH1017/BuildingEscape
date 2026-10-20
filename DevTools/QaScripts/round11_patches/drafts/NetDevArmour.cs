using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode check of the armour (the owner's queue of 2026-10-19): a piece goes into the pack and is worn (one at a time) with its vest on the model, the docks' clock unlocks the tier a player carries back, the dealer sells the tiers that were
    /// brought back (not a second piece to a wearer), a rifle guard's bullet knocks a wearer down for the share of the time the tier says, and the whole way from a safe on floor 3 that holds Light Armour to the dealer.
    /// </summary>
    public partial class NetDevRunner
    {
        int armourFails;

        void ArmourCheck(string name, bool ok, string detail = "")
        {
            if (!ok) armourFails++;
            Log($"[Armour] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static int ArmourVestParts(WobblyPlayer p)
        {
            var v = p.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BodyArmour");
            return v ? v.childCount : 0;
        }

        /// <summary>1 when this weapon safe holds a gun (0 when it holds armour instead: a safe that is not the first of its floor may).</summary>
        static int GunsInSafe(int seed, int floor, int index)
        {
            var items = SafeRules.WeaponContents(seed, floor, index);
            return items.Length > 0 && SafeRules.IsGunItem(items[0]) ? 1 : 0;
        }

        static Pickup MakeArmour(ShieldTier tier, Vector3 at) => ArmourItem.Create(tier, at, null, 0f).GetComponent<Pickup>();

        /// <summary>Takes off whatever armour the pack holds (it is dropped and thrown away).</summary>
        static void ArmourOff(Backpack pack)
        {
            foreach (var held in pack.Snapshot())
                if (held && held.TryGetComponent(out ArmourItem _)) { pack.Drop(held); Object.Destroy(held.gameObject); }
        }

        IEnumerator ArmourScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            bool gfx = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var pack = player.GetComponent<Backpack>();
            var rig = FindFirstObjectByType<CameraFollow>();
            var buyFor = typeof(Vendor).GetMethod("BuyFor", AnyInst);
            int Index(ShieldTier t) => System.Array.FindIndex(Vendor.Catalog, p => p.isArmour && p.armour == t);
            int il = Index(ShieldTier.Light), im = Index(ShieldTier.Medium), ih = Index(ShieldTier.Heavy);
            string Buy(int index) => (string)buyFor.Invoke(Vendor.Current, new object[] { hm, pack, index });

            // ================= 1. the pieces and the pack
            RunUnlocks.Reset();
            ArmourCheck("a new run: no armour is worn and the dealer sells none", PlayerArmour.TierOf(pack) == ShieldTier.None && il >= 0 && !Vendor.OnOffer(il, 5) && !Vendor.OnOffer(im, 5) && !Vendor.OnOffer(ih, 5));
            var spot = player.transform.position + Vector3.up * 1.0f + player.transform.forward * 0.8f;
            var pLight = MakeArmour(ShieldTier.Light, spot);
            float w0 = pack.Weight;
            bool storedLight = pack.TryStore(pLight);
            ArmourCheck("Light Armour goes into the pack, weighs what the rules say, and the player wears it", storedLight && PlayerArmour.TierOf(pack) == ShieldTier.Light && Mathf.Abs(pack.Weight - w0 - ArmourRules.Weight(ShieldTier.Light)) < 0.01f, $"(weight {w0} -> {pack.Weight})");
            yield return new WaitForSeconds(0.8f);
            var vis = player.GetComponent<PlayerArmourVisual>();
            ArmourCheck("his model wears a light vest - one part on the chest", vis != null && vis.Shown == ShieldTier.Light && ArmourVestParts(player) == 1, $"(shown {(vis != null ? vis.Shown.ToString() : "no component")}, parts {ArmourVestParts(player)})");
            var pMedium = MakeArmour(ShieldTier.Medium, spot + Vector3.up * 0.4f);
            bool second = pack.TryStore(pMedium);
            ArmourCheck("a second piece is refused while one is worn (one at a time)", !second && PlayerArmour.TierOf(pack) == ShieldTier.Light && !pack.Contains(pMedium));
            pack.Drop(pLight); Destroy(pLight.gameObject);
            yield return new WaitForSeconds(0.8f);
            ArmourCheck("dropped, the armour is off: nothing worn and no vest on the model", PlayerArmour.TierOf(pack) == ShieldTier.None && vis.Shown == ShieldTier.None && ArmourVestParts(player) == 0, $"(parts {ArmourVestParts(player)})");
            bool storedMedium = pack.TryStore(pMedium);
            yield return new WaitForSeconds(0.8f);
            ArmourCheck("with nothing worn the medium piece is taken and its vest has three parts (the plates)", storedMedium && PlayerArmour.TierOf(pack) == ShieldTier.Medium && vis.Shown == ShieldTier.Medium && ArmourVestParts(player) == 3, $"(parts {ArmourVestParts(player)})");
            pack.Drop(pMedium); Destroy(pMedium.gameObject);
            var pHeavy = MakeArmour(ShieldTier.Heavy, spot);
            bool storedHeavy = pack.TryStore(pHeavy);
            yield return new WaitForSeconds(0.8f);
            ArmourCheck("Heavy Armour weighs 8 kg and its vest has five parts (plates and shoulder pads)", storedHeavy && PlayerArmour.TierOf(pack) == ShieldTier.Heavy && Mathf.Abs(ArmourRules.Weight(ShieldTier.Heavy) - 8f) < 0.01f && vis.Shown == ShieldTier.Heavy && ArmourVestParts(player) == 5, $"(parts {ArmourVestParts(player)})");

            // ================= 2. the docks: what the player carries back is unlocked for the party
            RunUnlocks.Reset();
            yield return new WaitForSeconds(1.4f);
            ArmourCheck("in the docks with Heavy Armour on, that tier is unlocked for the party within a second and no other", RunUnlocks.HasArmour(ShieldTier.Heavy) && !RunUnlocks.HasArmour(ShieldTier.Medium) && !RunUnlocks.HasArmour(ShieldTier.Light), $"(mask {RunUnlocks.Mask})");
            ArmourCheck("the dealer offers Heavy Armour now and not the other two", Vendor.OnOffer(ih, 1) && !Vendor.OnOffer(im, 1) && !Vendor.OnOffer(il, 1));

            // ================= 3. the dealer
            hm.AddCash(20000);
            int cash0 = hm.Cash;
            string said = Buy(ih);
            ArmourCheck("wearing armour already, the dealer sells no second piece and takes no money", said.Contains("already wear") && hm.Cash == cash0, $"(\"{said}\")");
            ArmourOff(pack);
            yield return new WaitForSeconds(0.3f);
            said = Buy(il);
            ArmourCheck("a tier that was not brought back is not for sale", said.Contains("nothing like that") && hm.Cash == cash0, $"(\"{said}\")");
            said = Buy(ih);
            ArmourCheck("Heavy Armour is bought for what the rules say, lands in the pack and is worn", PlayerArmour.TierOf(pack) == ShieldTier.Heavy && hm.Cash == cash0 - ArmourRules.Price(ShieldTier.Heavy), $"(\"{said}\", cash {cash0} -> {hm.Cash})");
            ArmourOff(pack);
            RunUnlocks.UnlockArmour(ShieldTier.Light);
            cash0 = hm.Cash;
            said = Buy(il);
            ArmourCheck("once Light Armour was brought back too, it is bought like the other", PlayerArmour.TierOf(pack) == ShieldTier.Light && hm.Cash == cash0 - ArmourRules.Price(ShieldTier.Light), $"(\"{said}\", cash {cash0} -> {hm.Cash})");
            ArmourOff(pack);

            // ---- the pictures (graphics only), here in the docks where nobody else is about: the player in each tier, and the three pieces lying
            if (gfx)
            {
                if (rig) rig.enabled = false;
                var chest = player.transform.position + Vector3.up * 0.3f;
                Vector3 camSpot = default;
                if (OpenSpot(chest, 3.4f, out camSpot, false) || OpenSpot(chest, 2.8f, out camSpot, false) || OpenSpot(chest, 2.2f, out camSpot, false))
                {
                    var eye = new GameObject("ArmourEye").AddComponent<Camera>();
                    eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = new Color(0.2f, 0.22f, 0.26f); eye.fieldOfView = 36f; eye.enabled = false;
                    eye.transform.position = camSpot + Vector3.up * 1.25f;
                    eye.transform.LookAt(chest);
                    var toEye = camSpot - player.transform.position; toEye.y = 0f;
                    var toCam = toEye.normalized;
                    player.TeleportTo(player.transform.position, Quaternion.LookRotation(toCam));                   // (he faces the camera)
                    rb.linearVelocity = Vector3.zero;
                    yield return new WaitForSeconds(0.6f);
                    foreach (var t in new[] { ShieldTier.Light, ShieldTier.Medium, ShieldTier.Heavy })
                    {
                        ArmourOff(pack);
                        pack.TryStore(MakeArmour(t, player.transform.position + Vector3.up * 1.0f));
                        yield return new WaitForSeconds(0.8f);
                        eye.transform.LookAt(player.transform.position + Vector3.up * 0.3f);
                        ShotCam(eye, "armour_worn_" + t.ToString().ToLower());
                    }
                    ArmourOff(pack);
                    // the three pieces lying side by side between the camera and the player
                    var right = Vector3.Cross(Vector3.up, -toCam);
                    var lying = new System.Collections.Generic.List<GameObject>();
                    var middle = player.transform.position + toCam * 1.3f;
                    int slot = 0;
                    foreach (var t in new[] { ShieldTier.Light, ShieldTier.Medium, ShieldTier.Heavy })
                        lying.Add(ArmourItem.Create(t, middle + right * ((slot++ - 1) * 0.9f) + Vector3.up * 0.2f, null, 0f));
                    yield return new WaitForSeconds(1.2f);
                    eye.transform.position = camSpot + Vector3.up * 1.6f;
                    eye.fieldOfView = 50f;
                    eye.transform.LookAt(new Vector3(middle.x, 0.05f, middle.z));
                    ShotCam(eye, "armour_items");
                    foreach (var g in lying) Destroy(g);
                    Destroy(eye.gameObject);
                }
                else Log("SKIP the armour pictures: no clear spot for the camera");
            }
            // ================= 4. what armour does: a guard's bullet knocks the wearer down for less time
            yield return R5LoadFloor(4);
            player = WobblyPlayerRegistry.All[0]; rb = player.GetComponent<Rigidbody>(); pack = player.GetComponent<Backpack>();
            ArmourOff(pack);
            float rifleDown = GuardLoadouts.Tuning(RangedWeapon.Kind.Rifle).stumbleSeconds;
            yield return CombatDuel(RangedWeapon.Kind.Rifle, 7f, "no armour", true);
            float noneSeconds = duelStumbleSeconds; bool noneHit = duelHits >= 1;
            ArmourCheck("with no armour a rifle guard's bullet knocks the player down for what the guard's gun says", noneHit && Mathf.Abs(noneSeconds - rifleDown) < 0.4f, $"(down {noneSeconds:0.00} s, the gun says {rifleDown:0.00} s)");
            var heavyPiece = MakeArmour(ShieldTier.Heavy, player.transform.position + Vector3.up * 1.0f + player.transform.forward * 0.8f);
            pack.TryStore(heavyPiece);
            yield return CombatDuel(RangedWeapon.Kind.Rifle, 7f, "heavy armour", true);
            float heavySeconds = duelStumbleSeconds; bool heavyHit = duelHits >= 1;
            ArmourCheck("in Heavy Armour the same bullet knocks him down for 30 % of that", heavyHit && Mathf.Abs(heavySeconds - rifleDown * 0.3f) < 0.3f && heavySeconds < noneSeconds * 0.6f, $"(down {heavySeconds:0.00} s, 30 % of {rifleDown:0.00} is {rifleDown * 0.3f:0.00})");
            ArmourCheck("a rifle guard's bullet does not blow a wearer up either: armour only softens the knock-down", !duelBusted && duelBlasts == 0);

            // ================= 5. from a safe on floor 3 to the dealer
            int seed = 0;
            for (int s = 1; s < 5000 && seed == 0; s++) if (SafeRules.HoldsArmour(s, 3, 1) && SafeRules.PickArmour(s, 3, 1, 0) == ShieldTier.Light) seed = s;
            ArmourCheck("(test) a run whose second safe on floor 3 holds Light Armour", seed != 0, $"(seed {seed})");
            ArmourOff(pack);
            RunUnlocks.Reset();
            tm.SetIntelSeed(seed);
            yield return R5LoadFloor(3);
            player = WobblyPlayerRegistry.All[0]; rb = player.GetComponent<Rigidbody>(); pack = player.GetComponent<Backpack>();
            var inSafes = FindObjectsByType<ArmourItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(a => a && FloorSafe.HoldsGun(a.gameObject)).ToList();
            var safe0 = FloorSafe.ByIndex(0); var safe1 = FloorSafe.ByIndex(1);
            ArmourCheck("floor 3: the first safe holds a gun, the second Light Armour instead of one; the piece is out of the world while the safe is shut",
                safe0 != null && safe1 != null && inSafes.Count == 1 && inSafes[0].tier == ShieldTier.Light && safe0.GunCount == 1 && safe1.GunCount == 1 && !inSafes[0].gameObject.activeSelf && !safe1.IsOpen && !safe0.IsOpen,
                $"({inSafes.Count} piece(s) in safes)");
            if (safe1 != null && inSafes.Count == 1)
            {
                safe1.ApplyOpen(true);
                yield return new WaitForSeconds(0.8f);
                var piece = inSafes[0].GetComponent<Pickup>();
                var local = safe1.transform.InverseTransformPoint(piece.transform.position);
                ArmourCheck("opened, the piece lies in the hollow of the safe, inside it", piece.gameObject.activeInHierarchy && Mathf.Abs(local.x) < 0.39f && local.y > 0.02f && local.y < 0.5f && local.z > -0.30f && local.z < 0.26f, $"(local {local})");
                if (gfx)
                {
                    var eye2 = new GameObject("ArmourSafeEye").AddComponent<Camera>();
                    eye2.clearFlags = CameraClearFlags.SolidColor; eye2.backgroundColor = new Color(0.2f, 0.22f, 0.26f); eye2.fieldOfView = 40f; eye2.enabled = false;
                    var sat = safe1.transform.position;
                    eye2.transform.position = sat + safe1.transform.forward * 1.15f + Vector3.up * 1.05f;
                    eye2.transform.LookAt(sat + safe1.transform.forward * 0.1f + Vector3.up * 0.12f);
                    yield return null;
                    ShotCam(eye2, "armour_safe_open");
                    Destroy(eye2.gameObject);
                }
                bool took = pack.TryStore(piece);
                ArmourCheck("taken from the safe, it is worn: Light Armour", took && PlayerArmour.TierOf(pack) == ShieldTier.Light);
                ArmourCheck("still up on the floors, it has unlocked nothing at the dealer yet", !RunUnlocks.HasArmour(ShieldTier.Light) && RunUnlocks.Mask == 0, $"(mask {RunUnlocks.Mask})");
                tm.GoToLevel(0);
                yield return new WaitUntil(() => tm.Loading || hm.Floor == 0);
                yield return WaitFor(() => !tm.Loading && hm.Floor == 0 && hm.Phase == HeistPhase.Hub, 90, "the docks");
                hm.godMode = true; hm.freezeTimer = true;
                yield return new WaitForSeconds(1.6f);
                pack = WobblyPlayerRegistry.All[0].GetComponent<Backpack>();
                ArmourCheck("back in the docks with it, the finder has unlocked Light Armour for the party, and the dealer offers it", RunUnlocks.HasArmour(ShieldTier.Light) && Vendor.OnOffer(il, 1) && !Vendor.OnOffer(im, 1) && PlayerArmour.TierOf(pack) == ShieldTier.Light, $"(mask {RunUnlocks.Mask})");
            }

            Log($"[Armour] DONE fails={armourFails}");
            Quit(armourFails == 0 ? 0 : 1);
        }
    }
}
