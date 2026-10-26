using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The checks of the fixes after the owner's playtest with friends (2026-10-23): nametags over the players' heads (<see cref="NameRules"/>, <see cref="NameTags"/>) and the safety net under a player's body
    /// (<see cref="PlayerSafetyRules"/>, <c>WobblyPlayer.KeepInTheBuilding</c>). Pure checks here; Play: <c>SafetyNet</c> (one process: <c>qa_regress_bd.ps1 -Methods SafetyNet</c>) and <c>NameTagNet</c> (two machines:
    /// <c>qa_mp_bd.ps1 -Method NameTagNet</c>).
    /// </summary>
    public static partial class NetDevTest
    {
        public static void PlaytestFixesTest()
        {
            Check("nametag: a plain name stays as it is", NameRules.Clean("Bob") == "Bob" && NameRules.Clean("Anna Maria") == "Anna Maria");
            Check("nametag: rich-text tags, control characters and runs of white space go (the owner of a body writes his own name: every machine cleans what it reads)",
                NameRules.Clean("<color=red>Bob</color>  the\tBuilder") == "Bob the Builder" && NameRules.Clean("a" + (char)1 + "b") == "ab" && NameRules.Clean("  x  ") == "x");
            Check("nametag: nothing, spaces only or an unclosed tag give no name", NameRules.Clean(null) == "" && NameRules.Clean("   ") == "" && NameRules.Clean("<b") == "" && NameRules.Clean("<b>") == "");
            Check("nametag: a name is cut to 16 characters and never ends in a space", NameRules.Clean(new string('x', 50)).Length == NameRules.MaxLength && NameRules.Clean("aaaaaaaaaaaaaaa bbbb") == "aaaaaaaaaaaaaaa");
            Check("nametag: no name shows as 'Player n' (n = slot + 1, as the game calls him everywhere else), a name as itself", NameRules.Tag("", 0) == "Player 1" && NameRules.Tag(null, 3) == "Player 4" && NameRules.Tag("Ann", 3) == "Ann");
            Check("nametag: full at 18 m, half at 24 m, gone at 30 m and beyond (and for a number that is not one)",
                NameRules.Alpha(0f) == 1f && NameRules.Alpha(18f) == 1f && System.Math.Abs(NameRules.Alpha(24f) - 0.5f) < 1e-4f && NameRules.Alpha(30f) == 0f && NameRules.Alpha(99f) == 0f && NameRules.Alpha(float.NaN) == 0f);
            Check("safety net: the body is outside the building above 3.1 m, below -2.5 m, or at no height at all; a jump (about 2.5 m) and the floor are inside",
                PlayerSafetyRules.Outside(3.2f) && PlayerSafetyRules.Outside(-3f) && PlayerSafetyRules.Outside(float.NaN) && !PlayerSafetyRules.Outside(2.5f) && !PlayerSafetyRules.Outside(1f) && !PlayerSafetyRules.Outside(0f));
            Check("safety net: a spot is worth coming back to only when the body stands on the ground at a normal height and is not being thrown about",
                PlayerSafetyRules.WorthRemembering(true, 1f, 2f) && !PlayerSafetyRules.WorthRemembering(false, 1f, 0f) && !PlayerSafetyRules.WorthRemembering(true, 2.5f, 1f) && !PlayerSafetyRules.WorthRemembering(true, 1f, 9f));
            Check("safety net: the up speed limit lets a jump (5.5) through, the roof (a wall is 3.5 m high) is above the highest inside spot",
                PlayerSafetyRules.MaxUpSpeed > 5.5f && PlayerSafetyRules.MaxHeight < 3.5f && PlayerSafetyRules.MaxDepenetrationSpeed < 10f);
        }
    }

    public partial class NetDevRunner
    {
        int ptFails;
        void PtCheck(string name, bool ok, string detail = "") { if (!ok) ptFails++; Log($"[Playtest] {(ok ? "PASS" : "FAIL")} {name} {detail}"); }

        /// <summary>One process: the body is not thrown out of the building (a squeeze of colliders, an up speed, a position on the roof) and is put back where it stood.</summary>
        IEnumerator SafetyNetScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(2.0f);
            hm.godMode = true; hm.freezeTimer = true;
            foreach (var g in GuardAI.All) if (g) { g.sightRange = 0.1f; g.hearingMultiplier = 0f; }
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            PtCheck("safety: the body is pushed out of an overlap at 1.5 m/s at most", Mathf.Abs(rb.maxDepenetrationVelocity - PlayerSafetyRules.MaxDepenetrationSpeed) < 0.01f, $"({rb.maxDepenetrationVelocity})");
            yield return new WaitForSeconds(1.5f);
            var spot = rb.position;
            PtCheck("safety: standing on the floor nothing is rescued", player.Rescues == 0 && !PlayerSafetyRules.Outside(spot.y), $"(y {spot.y:0.00}, rescues {player.Rescues})");

            // ---- six officers' colliders closing in on the body from every side: it is not thrown up
            var ring = new List<GameObject>();
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                var go = new GameObject("SquishRing"); var cc = go.AddComponent<CapsuleCollider>(); cc.radius = 0.45f; cc.height = 1.8f;
                go.transform.position = new Vector3(spot.x + Mathf.Cos(a) * 0.4f, spot.y, spot.z + Mathf.Sin(a) * 0.4f);
                ring.Add(go);
            }
            Physics.SyncTransforms();
            float maxY = spot.y, maxVy = 0f;
            for (float t = 0f; t < 2.0f; t += Time.deltaTime) { maxY = Mathf.Max(maxY, rb.position.y); maxVy = Mathf.Max(maxVy, rb.linearVelocity.y); yield return null; }
            foreach (var go in ring) if (go) Destroy(go);
            PtCheck("safety: squeezed by six colliders the body is not thrown into the ceiling", maxY < PlayerSafetyRules.MaxHeight, $"(highest {maxY:0.00} m, fastest up {maxVy:0.0} m/s, rescues {player.Rescues})");
            yield return new WaitForSeconds(1.0f);
            spot = rb.position;

            // ---- an up speed far beyond a jump is cut
            rb.linearVelocity = new Vector3(0f, 40f, 0f);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            PtCheck("safety: shot upwards at 40 m/s the body goes up no faster than the limit", rb.linearVelocity.y <= PlayerSafetyRules.MaxUpSpeed + 0.05f, $"({rb.linearVelocity.y:0.0} m/s)");
            yield return new WaitForSeconds(2.0f);
            spot = rb.position;

            // ---- on the roof: put back where he last stood
            int rescues0 = player.Rescues;
            rb.position = spot + Vector3.up * 8f; rb.linearVelocity = Vector3.zero; Physics.SyncTransforms();
            yield return new WaitForSeconds(0.5f);
            PtCheck("safety: found on the roof the body is put back where it last stood", player.Rescues == rescues0 + 1 && rb.position.y < 2f && Vector2.Distance(new Vector2(rb.position.x, rb.position.z), new Vector2(spot.x, spot.z)) < 1.5f,
                $"(rescues {player.Rescues - rescues0}, now {rb.position.x:0.0}, {rb.position.y:0.0}, {rb.position.z:0.0}; stood at {spot.x:0.0}, {spot.z:0.0})");

            // ---- a body that has no good spot yet (just teleported) goes back to the elevator
            player.TeleportTo(spot + Vector3.up * 9f, player.transform.rotation);
            yield return new WaitForSeconds(0.5f);
            var from = tm.CurrentInfo.spawn;
            PtCheck("safety: with no good spot to go back to the body is put at the floor's elevator", player.Rescues == rescues0 + 2 && rb.position.y < 2f && Vector2.Distance(new Vector2(rb.position.x, rb.position.z), new Vector2(from.x, from.z)) < 3f,
                $"(rescues {player.Rescues - rescues0}, now {rb.position.x:0.0}, {rb.position.y:0.0}, {rb.position.z:0.0}; elevator {from.x:0.0}, {from.z:0.0})");

            Log($"[Playtest] SafetyNet DONE fails={ptFails}");
            Quit(ptFails == 0 ? 0 : 1);
        }

        /// <summary>What a tag over this slot says on a machine: the Steam name when Steam is running (the two test machines share one account), else "Player n".</summary>
        static string ExpectedTag(int slot) { var st = SteamService.Instance; return NameRules.Tag(st != null && st.Ready ? NameRules.Clean(st.LocalName) : "", slot); }

        /// <summary>One process: the pack's keys. A crowbar is bought; its slot key puts it away and takes it out again; the drop key puts it on the floor; with empty hands the drop key does nothing.</summary>
        IEnumerator PackKeysScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true; hm.AddCash(1000);
            yield return new WaitForSeconds(1.0f);
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var counter = Vendor.Current.GetComponent<Collider>();
            player.TeleportTo(counter.bounds.center + Vector3.up * 0.2f, player.transform.rotation);           // (the dealer sells only to somebody at his counter)
            yield return new WaitForSeconds(1.5f);
            Vendor.Current.TestBuy(4);                                                                         // (the crowbar)
            yield return WaitFor(() => pack.Items.Any(i => i && i.GetComponent<MeleeWeapon>()), 20, "the crowbar in the pack");
            var crowbar = pack.Items.First(i => i && i.GetComponent<MeleeWeapon>());
            if (pack.Equipped != crowbar) pack.SetEquipped(crowbar);
            yield return null;
            int slot = pack.BarSlotOf(crowbar);
            PtCheck("keys: the crowbar is in the pack, in hand, and on a slot of the bar", pack.Equipped == crowbar && slot >= 0, $"(slot {slot})");
            PtCheck("keys: the slot key of the weapon in hand puts it away (it stays in the pack)", pack.PressSlot(slot) && pack.Equipped == null && pack.Items.Contains(crowbar));
            PtCheck("keys: the slot key again takes it out", pack.PressSlot(slot) && pack.Equipped == crowbar);
            PtCheck("keys: the key of an empty slot changes nothing", !pack.PressSlot(5) && pack.Equipped == crowbar);
            PtCheck("keys: the drop key drops the weapon in hand: out of the pack, out of the hand, on the floor beside the player",
                pack.DropHeld() && !pack.Items.Contains(crowbar) && pack.Equipped == null && crowbar.gameObject.activeInHierarchy && Vector3.Distance(crowbar.transform.position, player.transform.position) < 3f);
            PtCheck("keys: with empty hands the drop key does nothing", !pack.DropHeld() && pack.Equipped == null);
            // the owner, 2026-10-24: the mic pill is up in a solo run, with the mute hint, and M mutes it
            var vc = VoiceChat.Instance;
            string micLine = vc != null ? vc.HudLine(out bool micOff) : "";
            PtCheck("solo: the mic pill is up under the player badge and says how to mute", vc != null && micLine.Length > 0 && micLine.Contains("M"), $"('{micLine}')");
            if (vc != null)
            {
                vc.Muted = true;
                string mutedLine = vc.HudLine(out bool mutedOff);
                PtCheck("solo: muted, the pill says MIC MUTED (M)", mutedOff && mutedLine.StartsWith("MIC MUTED"), $"('{mutedLine}')");
                vc.Muted = false;
            }
            // the journal opens on its checklist
            if (IntelScreens.Instance != null)
            {
                IntelScreens.Instance.JournalTab = 0;
                IntelScreens.Instance.OpenJournal();
                PtCheck("journal: J opens it on the CHECKLIST tab", IntelScreens.Instance.JournalTab == 1 && OutpostUi.Current == OutpostUi.Kind.Journal, $"(tab {IntelScreens.Instance.JournalTab}, screen {OutpostUi.Current})");
                OutpostUi.Hide(OutpostUi.Kind.Journal);
            }
            Log($"[Playtest] PackKeys DONE fails={ptFails}");
            Quit(ptFails == 0 ? 0 : 1);
        }

        string TagFor(int slot) => NameTags.Tags.Where(t => t.slot == slot).Select(t => t.text).FirstOrDefault();

        /// <summary>Two machines: each sees the other's tag over his head ("Player n" with no Steam name), none over his own, and a name written by its owner arrives cleaned.</summary>
        IEnumerator NameTagHostScenario()
        {
            var net = NetSession.Instance;
            ClearSignals();
            net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "host lobby");
            yield return WaitFor(() => net.Roster.Count >= 2, 200, "a client");
            net.StartRun();
            yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
            yield return FloorReady(0, 90);
            var hm = HeistManager.Instance; hm.godMode = true; hm.freezeTimer = true;
            Signal("h_ready");
            yield return WaitSig("c_ready", 90);
            yield return WaitFor(() => NetPlayer.All.Count >= 2, 30, "both players");
            NameTags.TestAllVisible = true;
            yield return new WaitForSeconds(1.0f);
            var other = NetPlayer.All.FirstOrDefault(p => !p.IsOwner);
            var mine = NetPlayer.All.FirstOrDefault(p => p.IsOwner);
            PtCheck("host: the client's tag is over his head: his Steam name, or 'Player n' without Steam", other != null && TagFor(other.Slot) == ExpectedTag(other.Slot), $"(tags: {string.Join(", ", NameTags.Tags.Select(t => t.text))})");
            PtCheck("host: there is no tag over his own head", mine != null && TagFor(mine.Slot) == null);
            Signal("h_saw");
            yield return WaitSig("c_named", 60);
            yield return WaitFor(() => other != null && other.DisplayName == "Kim Lee", 20, "the client's name");
            yield return new WaitForSeconds(0.5f);
            PtCheck("host: the name the client wrote (with a tag in it) arrives cleaned, and is the text of his tag", other != null && other.DisplayName == "Kim Lee" && TagFor(other.Slot) == "Kim Lee", $"(name '{other?.DisplayName}', tag '{(other != null ? TagFor(other.Slot) : "")}')");
            Signal("h_done");
            yield return WaitSig("c_done", 60);
            Log($"[Playtest] NameTag host DONE fails={ptFails}");
            Quit(ptFails == 0 ? 0 : 1);
        }

        IEnumerator NameTagClientScenario()
        {
            var net = NetSession.Instance;
            float deadline = Time.realtimeSinceStartup + 150f;
            while (net.State != NetSessionState.InLobby && Time.realtimeSinceStartup < deadline)
            {
                net.JoinLocal("127.0.0.1");
                float t = 0;
                while (net.State == NetSessionState.Connecting && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
                if (net.State != NetSessionState.InLobby) yield return new WaitForSeconds(2f);
            }
            yield return FloorReady(0, 120);
            yield return WaitSig("h_ready", 120);
            Signal("c_ready");
            yield return WaitFor(() => NetPlayer.All.Count >= 2, 30, "both players");
            NameTags.TestAllVisible = true;
            yield return new WaitForSeconds(1.0f);
            var other = NetPlayer.All.FirstOrDefault(p => !p.IsOwner);
            var mine = NetPlayer.All.FirstOrDefault(p => p.IsOwner);
            PtCheck("client: the host's tag is over his head: his Steam name, or 'Player n' without Steam", other != null && TagFor(other.Slot) == ExpectedTag(other.Slot), $"(tags: {string.Join(", ", NameTags.Tags.Select(t => t.text))})");
            PtCheck("client: there is no tag over his own head", mine != null && TagFor(mine.Slot) == null);
            yield return WaitSig("h_saw", 60);
            mine.SetDisplayName("<b>Kim</b>   Lee");
            Signal("c_named");
            yield return WaitSig("h_done", 60);
            Signal("c_done");
            Log($"[Playtest] NameTag client DONE fails={ptFails}");
            Quit(ptFails == 0 ? 0 : 1);
        }
    }
}
