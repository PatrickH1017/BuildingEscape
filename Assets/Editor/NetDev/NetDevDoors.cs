using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode check (offline, Greybox scene) of the officers' symbol bubbles and sounds, the music and the sliding doors.
    /// Run: Unity -batchmode -executeMethod BuildingEscape.EditorTools.NetDevHarness.Doors   (look for "[Doors] DONE fails=0").
    /// </summary>
    public partial class NetDevRunner
    {
        int doorFails;

        void DoorCheck(string name, bool ok, string detail = "")
        {
            if (!ok) doorFails++;
            Log($"[Doors] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator LoadLevel(HeistManager hm, int floor)
        {
            TowerManager.Instance.GoToLevel(floor);
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == floor);
            yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == floor, 90, $"floor {floor}");
            yield return new WaitForSeconds(1f);
        }

        IEnumerator DoorsScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();

            // ================= music: the basement has none, floor 1 gets its own track
            var md = MusicDirector.Instance;
            DoorCheck("music: the director exists", md != null);
            if (md == null) { Quit(1); yield break; }
            DoorCheck("music: silence in the basement", md.Current == MusicDirector.Mood.None || md.Current == MusicDirector.Mood.Elevator, $"({md.Current})");

            yield return LoadLevel(hm, 1);
            int seed = TowerManager.Instance.baseSeed;
            yield return WaitFor(() => md.IntenseReady, 60, "the intense track of floor 1");
            string track1 = md.TrackName;
            DoorCheck("music: floor 1 has the track its seed says", track1 == MusicPlan.TrackFor(seed, 1).Name, $"({track1})");

            // ================= officers: sounds and symbol bubbles on every guard
            int guards = 0, withSounds = 0, withBubbles = 0;
            foreach (var g in GuardAI.All)
            {
                if (!g) continue;
                guards++;
                if (g.GetComponent<GuardSounds>()) withSounds++;
                if (g.GetComponent<GuardBubble>()) withBubbles++;
            }
            DoorCheck("officers: every guard has keys + radio and a bubble", guards > 0 && withSounds == guards && withBubbles == guards, $"({guards} guards)");
            float waited = 0f;
            while (waited < 30f && !AnyBubbleText()) { waited += Time.unscaledDeltaTime; yield return null; }
            string said = FirstBubbleText();
            bool symbolsOnly = !string.IsNullOrEmpty(said);
            if (symbolsOnly) foreach (char c in said) symbolsOnly &= SymbolSpeech.Symbols.IndexOf(c) >= 0;
            DoorCheck("officers: a guard cursed within 30 s, in symbols only", symbolsOnly, $"(\"{said}\" after {waited:0.0} s)");

            // ================= music: backup starts the intense track and the next floor changes it
            player.transform.position = gen.CellCentre(Vector2Int.zero) + new Vector3(0f, 1.1f, 7f); rb.linearVelocity = Vector3.zero;   // out of the elevator
            yield return new WaitForSeconds(6.5f);                                                                                       // (the ride grace time)
            DoorCheck("music: no intense music before backup is called", md.Current == MusicDirector.Mood.None && !md.IntensePlaying, $"({md.Current})");
            hm.CallBackup(GuardAI.All.Count > 0 ? GuardAI.All[0] : null);
            yield return new WaitForSeconds(1.5f);
            DoorCheck("music: calling backup starts the intense track", md.Current == MusicDirector.Mood.Intense && md.IntensePlaying, $"({md.Current}, playing {md.IntensePlaying})");
            yield return LoadLevel(hm, 2);
            yield return WaitFor(() => md.IntenseReady, 60, "the intense track of floor 2");
            DoorCheck("music: the new floor starts without it", md.Current != MusicDirector.Mood.Intense && !md.IntensePlaying, $"({md.Current})");
            DoorCheck("music: floor 2 has a different track", md.TrackName != track1 && md.TrackName == MusicPlan.TrackFor(seed, 2).Name, $"({track1} -> {md.TrackName})");

            // ================= doors (the guards stay out of the way)
            foreach (var g in new List<GuardAI>(GuardAI.All)) if (g) g.gameObject.SetActive(false);
            var floorRoot = gen.Current;
            var dm = floorRoot ? floorRoot.GetComponentInChildren<DoorManager>() : null;
            DoorCheck("doors: the floor has its doors", dm != null && dm.Count > 0, dm ? $"({dm.Count} doors)" : "");
            if (dm == null || dm.Count == 0) { Log($"[Doors] DONE fails={doorFails}"); Quit(1); yield break; }

            int beams = 0;
            var partitions = floorRoot.Find("Interior/Partitions");
            if (partitions) foreach (Transform t in partitions) if (t.name == "DoorHeader") beams++;
            DoorCheck("doors: one door under every doorway beam", dm.Count == beams, $"({dm.Count} doors, {beams} beams)");
            int shut = 0;
            foreach (var d in dm.Doors) if (d.IsShut && d.BlockerEnabled) shut++;
            DoorCheck("doors: they all start shut, with their colliders on", shut == dm.Count, $"({shut} of {dm.Count})");

            var door = dm.Doors[0];
            var across = door.AlongX ? Vector3.forward : Vector3.right;
            bool Blocks() => Physics.Raycast(door.Centre + across * 1.5f + Vector3.up * 1.2f, -across, 3f, ~0, QueryTriggerInteraction.Ignore);
            DoorCheck("doors: a shut door stops a ray through the doorway", Blocks());

            player.transform.position = door.Centre + across * 1.8f + Vector3.up * 1.1f; rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.9f);
            DoorCheck("doors: somebody walking up opens it", !door.IsShut && door.Logic.Open > 0.99f && !door.BlockerEnabled, $"(open {door.Logic.Open:0.00})");
            DoorCheck("doors: an open door lets a ray through", !Blocks());

            player.transform.position = gen.CellCentre(Vector2Int.zero) + new Vector3(0f, 1.1f, 7f); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(SlidingDoor.Linger - 0.3f);
            DoorCheck("doors: it stays open for a moment after they leave", !door.IsShut);
            yield return new WaitForSeconds(0.3f + SlidingDoor.WideCloseSeconds + 0.6f);
            DoorCheck("doors: then it shuts again", door.IsShut && door.BlockerEnabled && Blocks(), $"(open {door.Logic.Open:0.00})");

            // something in the doorway keeps it open
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "DoorTestCrate";
            crate.transform.position = door.Centre + Vector3.up * 0.7f;
            crate.transform.localScale = new Vector3(0.5f, 1.2f, 0.5f);
            player.transform.position = door.Centre + across * 1.8f + Vector3.up * 1.1f; rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.9f);
            player.transform.position = gen.CellCentre(Vector2Int.zero) + new Vector3(0f, 1.1f, 7f); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(SlidingDoor.Linger + SlidingDoor.WideCloseSeconds + 1.5f);
            DoorCheck("doors: a crate in the doorway keeps the door open", !door.IsShut && !door.BlockerEnabled);
            Destroy(crate);
            yield return new WaitForSeconds(0.6f + SlidingDoor.WideCloseSeconds + 0.6f);
            DoorCheck("doors: it closes once the crate is gone", door.IsShut && door.BlockerEnabled);

            Log($"[Doors] DONE fails={doorFails}");
            Quit(doorFails == 0 ? 0 : 1);
        }

        static bool AnyBubbleText() => !string.IsNullOrEmpty(FirstBubbleText());

        static string FirstBubbleText()
        {
            foreach (var b in GuardBubble.All) if (b && !string.IsNullOrEmpty(b.Text)) return b.Text;
            return null;
        }
    }
}
