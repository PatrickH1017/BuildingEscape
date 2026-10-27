using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The dock radio (the owner, 2026-10-27: "add a radio that plays random soundtracks in the dock ... If the player goes up the radio and uses it then they should get a screen where they can click the little buttons to change the
    /// station. Right now there is only 3 stations so only 3 buttons should be clickable"; "I want the radio put in that corner on top of two boxes that are stacked on top of each other"). In the real docks: the radio stands on two
    /// boxes in the corner east of the vending machines and plays a station from the start; E opens its screen with the radio's eight buttons where they are on the model; the first three change the station (the speaker plays a track of
    /// that station from where the shared clock says), the other five do nothing; Esc and walking away shut it. Pictures (a real window: <c>hd.ps1 ... NetDevHarness.Radio</c>): %TEMP%\be_shot_radio_*.png and %TEMP%\be_ui_radio_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int radioFails;

        void RadioCheck(string name, bool ok, string detail = "")
        {
            if (!ok) radioFails++;
            Log($"[Radio] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator RadioScenario()
        {
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null && !Application.isBatchMode) EditorWindow.GetWindow(gameView).maximized = true;
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            GunController.TestCursorLocked = true;
            yield return new WaitForSeconds(1.5f);
            var gen = FindFirstObjectByType<FloorGenerator>();
            var player = WobblyPlayerRegistry.All[0];
            var rig = FindFirstObjectByType<CameraFollow>();
            var radio = DockRadio.Current;
            RadioCheck("the catalog is set up (the radio and three stations with tracks)", RadioCatalog.Instance != null && RadioCatalog.Instance.radio != null && Enumerable.Range(0, 3).All(s => RadioCatalog.Instance.Tracks(s).Length > 0),
                RadioCatalog.Instance != null ? $"({string.Join(", ", Enumerable.Range(0, 3).Select(s => RadioCatalog.Instance.Tracks(s).Length + " tracks"))})" : "(no catalog)");
            RadioCheck("the docks have a radio", radio != null);
            if (radio == null) { Log($"[Radio] DONE fails={radioFails}"); Quit(1); yield break; }

            // ---- where it stands: the corner east of the vending machines, on two boxes
            float east = gen.DockRadioArea.xMax;                                  // (the inner face of the east wall)
            var vend = gen.DockVendingSpots.Take(2).ToArray();
            float machinesEast = vend.Max(v => v.x + v.width * 0.5f);
            var at = radio.transform.position;
            var boxes = radio.transform.parent.Cast<Transform>().Where(t => t.name == "box_long" && Vector3.Distance(new Vector3(t.position.x, 0f, t.position.z), new Vector3(at.x, 0f, at.z)) < 0.5f).ToList();
            RadioCheck("it stands in the corner east of the vending machines, against the south and the east wall", at.x > machinesEast + 0.5f && east - at.x < 0.8f && at.z < 1.0f,
                $"(radio at {at:F2}, the machines end at x {machinesEast:F2}, the east wall at x {east:F2})");
            RadioCheck("on two boxes stacked one on the other, at a height to use", boxes.Count == 2 && Mathf.Abs(boxes.Max(b => b.position.y) - boxes.Min(b => b.position.y)) > 0.25f && at.y > 0.5f && at.y < 1.0f,
                $"({boxes.Count} boxes under it, the radio's foot at {at.y:F2} m)");
            var cam = new GameObject("RadioCam").AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 55f;
            var cornerLook = new Vector3(at.x - 0.6f, 0.6f, at.z + 0.3f);
            foreach (var (name, pos) in new[] { ("corner", new Vector3(at.x - 4.5f, 2.3f, at.z + 5.5f)), ("close", at + new Vector3(-1.2f, 0.9f, 1.7f)), ("wide", new Vector3(at.x - 6f, 2.8f, at.z + 8f)) })
            {
                cam.transform.position = pos;
                cam.transform.LookAt(name == "close" ? at + Vector3.up * 0.3f : cornerLook);
                ShotCam(cam, "radio_" + name);
            }

            // ---- it plays from the start: a station at random, a track of that station, at the music volume
            yield return new WaitForSeconds(1.0f);
            int st0 = RadioState.Station;
            var cat = RadioCatalog.Instance;
            RadioCheck("a station is on from the start and the radio plays a track of it, quieter far away", RadioRules.ValidStation(st0) && radio.Playing && radio.ClipNow != null && cat.Tracks(st0).Contains(radio.ClipNow) && radio.Speaker.spatialBlend > 0.9f && radio.Speaker.volume > 0.1f,
                $"(station {st0}, playing {radio.Playing}, '{radio.TitleNow}', volume {radio.Speaker.volume:0.00})");
            // loud, and heard as far as the doorway to the elevator room (the owner: "You should be able to hear it up until the door that leads to the room with the elevator")
            {
                var door = gen.DockRadioHeardTo;
                float toDoor = Vector3.Distance(new Vector3(at.x, 0f, at.z), new Vector3(door.x, 0f, door.z));
                float near = radio.LoudnessAt(3f), atDoor = radio.LoudnessAt(toDoor), past = radio.LoudnessAt(toDoor + DockRadio.FadeBeyond + 0.5f), halfway = radio.LoudnessAt(toDoor * 0.5f);
                RadioCheck("it plays at the full music volume close by, still clearly at the doorway to the elevator room, and is gone a few metres past it",
                    Mathf.Approximately(radio.Speaker.volume, MusicDirector.Volume) && Mathf.Abs(radio.HeardTo - toDoor) < 0.5f && near > 0.99f && halfway > 0.55f && atDoor > 0.35f && past < 0.01f,
                    $"(volume {radio.Speaker.volume:0.00} = the music volume {MusicDirector.Volume:0.00}; the doorway {toDoor:0.0} m away at {(door.x):0.0}, {(door.z):0.0}; loudness 3 m {near:0.00}, half way {halfway:0.00}, at the doorway {atDoor:0.00}, {DockRadio.FadeBeyond + 0.5f:0.0} m past it {past:0.00})");
                var cam2 = new GameObject("RadioDoorCam").AddComponent<Camera>();
                cam2.enabled = false; cam2.fieldOfView = 60f;
                cam2.transform.position = new Vector3(door.x - 0.6f, 1.7f, door.z);
                cam2.transform.LookAt(new Vector3(at.x, 1.0f, at.z));
                ShotCam(cam2, "radio_from_doorway");
                Object.Destroy(cam2.gameObject);
            }
            radio.Where(st0, out _, out float want0);
            RadioCheck("it is where the shared clock says in the song (within 1.5 s)", Mathf.Abs(radio.Speaker.time - want0) < 1.5f, $"(at {radio.Speaker.time:0.0} s, the clock says {want0:0.0} s)");

            // ---- the player walks up to it and uses it
            var stand = at + radio.Forward * 1.3f;
            player.TeleportTo(new Vector3(stand.x, 1.1f, stand.z), Quaternion.LookRotation(-radio.Forward));
            yield return new WaitForSeconds(0.6f);
            if (rig)
            {
                var to = at + Vector3.up * 0.3f - (player.transform.position + Vector3.up * 0.6f);
                float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                rig.TurnAim(Mathf.DeltaAngle(rig.AimYaw, yaw));
                rig.TiltAim(-rig.AimPitch + 20f);
            }
            yield return new WaitForSeconds(0.6f);
            yield return Snap("radio_prompt");
            RadioCheck("the player is close enough to use it", radio.DistanceFrom(player.transform.position) <= radio.reach, $"({radio.DistanceFrom(player.transform.position):0.00} m, reach {radio.reach} m)");
            radio.Use(player);
            yield return new WaitForSeconds(0.5f);
            RadioCheck("using it opens its screen, a screen over the game (keys blocked, cursor free)", RadioScreen.Open && TestMode.PanelOpen && Crosshair.MenuOpen);
            yield return Snap("radio_screen");
            var rects = Enumerable.Range(0, RadioRules.ButtonCount).Select(RadioScreen.ButtonRect).ToArray();
            bool laidOut = rects.All(r => r.width > 4f && r.height > 2f) && rects.Select(r => r.center).Distinct().Count() == RadioRules.ButtonCount;
            bool reading = laidOut && rects[0].center.x < rects[1].center.x && rects[0].center.y < rects[2].center.y && rects[2].center.y < rects[4].center.y && rects[4].center.y < rects[6].center.y;
            RadioCheck("the eight buttons of the pad are on the screen, two columns of four, in reading order", laidOut && reading,
                $"({string.Join(" ", rects.Select(r => $"[{r.center.x:0},{r.center.y:0} {r.width:0}x{r.height:0}]"))})");

            // ---- the buttons: the first three change the station, the other five do nothing
            int changes = RadioState.Changes;
            bool dead = true;
            for (int b = 6; b < RadioRules.ButtonCount; b++) dead &= !RadioScreen.TestPress(b);
            RadioCheck("buttons 7 and 8 do nothing", dead && RadioState.Changes == changes && RadioState.Station == st0 && RadioState.On && RadioState.Volume == RadioRules.DefaultVolume, $"(station {RadioState.Station})");
            // (what station st0 played when it was left: the place it must be further on from when it is switched back to)
            var leftClip = radio.ClipNow; float leftAt = radio.Speaker.time, leftWhen = Time.unscaledTime;
            foreach (int b in new[] { (st0 + 1) % 3, (st0 + 2) % 3, st0 })
            {
                bool pressed = RadioScreen.TestPress(b);
                yield return new WaitForSeconds(0.4f);
                bool clipOk = radio.ClipNow != null && cat.Tracks(b).Contains(radio.ClipNow) && radio.Playing;
                radio.Where(b, out var wantClip, out float wantAt);
                RadioCheck($"button {b + 1} tunes to {RadioRules.Names[b]}: a track of it plays where that station's own clock is (it ran on while it was not heard)", pressed && RadioState.Station == b && clipOk && wantClip == radio.ClipNow && Mathf.Abs(radio.Speaker.time - wantAt) < 1.5f,
                    $"(station {RadioState.Station}, '{radio.TitleNow}' at {radio.Speaker.time:0.0} s, its clock says {wantAt:0.0} s, playing {radio.Playing})");
                yield return Snap("radio_screen_" + (b + 1));
                if (b != st0) yield return new WaitForSeconds(2f);
            }
            {
                // back on the first station: the same song further on (or the next song), not its start again
                float away = Time.unscaledTime - leftWhen;
                bool sameSong = radio.ClipNow == leftClip;
                bool further = sameSong ? Mathf.Abs(radio.Speaker.time - (leftAt + away)) < 1.5f : radio.ClipNow != null;
                RadioCheck($"switched away for {away:0.0} s and back, {RadioRules.Names[st0]} picks up where it would be by now - not at its start", further && (!sameSong || radio.Speaker.time > leftAt + 2f),
                    $"(left '{(leftClip ? leftClip.name : "-")}' at {leftAt:0.0} s, now '{(radio.ClipNow ? radio.ClipNow.name : "-")}' at {radio.Speaker.time:0.0} s)");
                // and the stations' clock runs the whole run: an hour on, each station is an hour further in its loop
                var tracks = cat.Tracks(st0);
                var order = RadioRules.Order(tracks.Length, RadioState.Seed + st0 * 7919);
                var lens = tracks.Select(c => c.length).ToArray();
                double total = lens.Sum();
                double now = RadioState.Elapsed(st0);
                RadioRules.Locate(lens, order, now, out int s0, out float o0);
                RadioRules.Locate(lens, order, now + 3600.0, out int s1, out float o1);
                double expect = (now + 3600.0) % total;
                double got = 0; for (int i = 0; i < s1; i++) got += lens[order[i]]; got += o1;
                RadioCheck("the playlists loop on the radio's clock for the whole run (an hour on is an hour further round the loop)", Mathf.Abs((float)(got - expect)) < 0.01f && RadioState.Running > 0.0,
                    $"(now slot {s0} at {o0:0.0} s, an hour on slot {s1} at {o1:0.0} s; the loop is {total / 60.0:0.0} min)");
                RadioCheck("the three stations start their loops at their own places", RadioState.StationStart(0) != RadioState.StationStart(1) && RadioState.StationStart(1) != RadioState.StationStart(2));
            }
            // a few seconds on: still the same place as the clock
            yield return new WaitForSeconds(3f);
            radio.Where(RadioState.Station, out var clipNow, out float wantNow);
            RadioCheck("seconds later the song is where the shared clock says (within 1.5 s)", clipNow == radio.ClipNow && Mathf.Abs(radio.Speaker.time - wantNow) < 1.5f, $"(at {radio.Speaker.time:0.0} s, the clock says {wantNow:0.0} s)");
            // the playlist moves on to the next track at the end of one (the clock pushed to a second before the end of the first track)
            {
                var tracks = cat.Tracks(RadioState.Station);
                var order = RadioRules.Order(tracks.Length, RadioState.Seed + RadioState.Station * 7919);
                var lens = tracks.Select(c => c.length).ToArray();
                RadioRules.Locate(lens, order, lens[order[0]] + 0.5, out int slot, out float off);
                RadioCheck("after the first track of the playlist comes the second", slot == 1 && off < 1f, $"(slot {slot}, {off:0.0} s)");
            }

            // ---- the volume (buttons 5 and 6) and the power (button 4): the owner, "add a volume control to the radio as well as an off button"
            {
                float musicWas = MusicDirector.Volume;
                MusicDirector.Volume = 0.7f;                                                    // (room above it: at the full music volume the radio cannot go louder than the speaker's 1)
                yield return null;
                float v8 = radio.Speaker.volume;
                RadioScreen.TestPress(RadioRules.VolumeUpButton);
                yield return null; yield return null;
                float v9 = radio.Speaker.volume;
                yield return Snap("radio_screen_volume9");
                for (int i = 0; i < 4; i++) RadioScreen.TestPress(RadioRules.VolumeDownButton);
                yield return null; yield return null;
                float v5 = radio.Speaker.volume;
                for (int i = 0; i < 12; i++) RadioScreen.TestPress(RadioRules.VolumeUpButton);
                int top = RadioState.Volume;
                for (int i = 0; i < 12; i++) RadioScreen.TestPress(RadioRules.VolumeDownButton);
                yield return null; yield return null;
                int bottom = RadioState.Volume; float v0 = radio.Speaker.volume;
                RadioCheck("VOL + and VOL - turn it up and down a step at a time, from silent (0) to 10", v9 > v8 && v5 < v8 && top == RadioRules.VolumeSteps && bottom == 0 && v0 == 0f && Mathf.Abs(v8 - MusicDirector.Volume) < 0.01f,
                    $"(8: {v8:0.00}, 9: {v9:0.00}, 5: {v5:0.00}, up to {top}, down to {bottom}: {v0:0.00})");
                for (int i = 0; i < RadioRules.DefaultVolume; i++) RadioScreen.TestPress(RadioRules.VolumeUpButton);
                MusicDirector.Volume = musicWas;
                int stOff = RadioState.Station;
                RadioScreen.TestPress(RadioRules.PowerButton);
                yield return new WaitForSeconds(0.4f);
                bool offOk = !RadioState.On && !radio.Playing && RadioState.Station == stOff;
                yield return Snap("radio_screen_off");
                yield return new WaitForSeconds(2f);
                RadioScreen.TestPress(RadioRules.PowerButton);
                yield return new WaitForSeconds(0.4f);
                radio.Where(RadioState.Station, out var clipBack, out float atBack);
                RadioCheck("OFF silences it; ON brings the same station back where its clock is (it ran on while the radio was off)", offOk && RadioState.On && radio.Playing && radio.ClipNow == clipBack && Mathf.Abs(radio.Speaker.time - atBack) < 1.5f,
                    $"(off: on {!offOk}, back on: '{radio.TitleNow}' at {radio.Speaker.time:0.0} s, its clock {atBack:0.0} s, volume {RadioState.Volume})");
                RadioScreen.TestPress(RadioRules.PowerButton);
                yield return null;
                RadioScreen.TestPress((RadioState.Station + 1) % 3);
                yield return new WaitForSeconds(0.4f);
                RadioCheck("off, a station's button switches it on with that station", RadioState.On && radio.Playing, $"(on {RadioState.On}, station {RadioState.Station})");
            }

            // ---- Esc and walking away shut it
            RadioScreen.Close();
            yield return null;
            radio.Use(player);
            yield return null;
            player.TeleportTo(new Vector3(stand.x - 6f, 1.1f, stand.z + 4f), player.transform.rotation);
            yield return new WaitForSeconds(0.5f);
            RadioCheck("walking away shuts the screen", !RadioScreen.Open && !TestMode.PanelOpen);
            GunController.TestCursorLocked = null;
            Log($"[Radio] DONE fails={radioFails}");
            Quit(radioFails == 0 ? 0 : 1);
        }
    }
}
