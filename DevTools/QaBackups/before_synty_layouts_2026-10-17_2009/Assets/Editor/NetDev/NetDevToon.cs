using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>Play Mode check of the Tiny Toon characters: lobby stage, line-up, animation states, materials.</summary>
    public partial class NetDevRunner
    {
        int toonFails;

        void ToonCheck(string name, bool ok, string detail = "")
        {
            if (!ok) toonFails++;
            Log($"[Toon] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator ToonScenario()
        {
            var cat = CharacterCatalog.Instance;
            ToonCheck("catalog loads at runtime", cat != null && cat.characters.Length >= 4);
            if (!cat) { Quit(1); yield break; }

            // ---- 1. the lobby stage dresses friends as Tiny Toon people
            var stage = LobbyStage.Instance;
            ToonCheck("lobby stage exists", stage != null);
            var looks = new List<string>();
            foreach (var c in cat.Players())
            {
                var a = PlayerAppearance.ForSlot(0);
                a.model = c.id;
                looks.Add(a.Serialize());
            }
            var party = PlayerAppearance.ForSlot(3); party.model = null;
            looks.Add(party.Serialize()); // and a Party character beside them
            stage.DebugShowLooks(looks);
            yield return new WaitForSeconds(3f);
            var anims = Object.FindObjectsByType<CharacterAnimator>(FindObjectsSortMode.None);
            int clipModes = anims.Count(x => x.ClipMode);
            ToonCheck("stage shows 3 Tiny Toon people + 1 Party character", anims.Length == 4 && clipModes == 3, $"(animators={anims.Length}, clip mode={clipModes})");
            Shot("toon_stage");

            // ---- 2. free-standing line-up: every character in the catalog + a Party one
            var root = new GameObject("ToonLineup");
            root.transform.position = new Vector3(0f, 0f, 60f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
            sun.transform.SetParent(root.transform, false);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -1.05f, 0f);
            ground.transform.localScale = new Vector3(20f, 0.1f, 6f);
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.38f, 0.42f) };

            var hosts = new List<GameObject>();
            var byId = new Dictionary<string, CharacterAnimator>();
            var chars = cat.characters.ToList();
            for (int i = 0; i <= chars.Count; i++)
            {
                var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                host.name = i < chars.Count ? "Host_" + chars[i].id : "Host_party";
                host.transform.SetParent(root.transform, false);
                host.transform.localPosition = new Vector3((i - chars.Count / 2f) * 1.7f, 0f, 0f);
                host.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face the camera
                Object.Destroy(host.GetComponent<Collider>());
                var look = i < chars.Count
                    ? new CharacterLook(chars[i].id, Color.white, 1, null, 1f, 1f, chars[i].id)
                    : PartyCharacterFactory.PlayerLooks[0];
                var anim = PartyCharacterFactory.Attach(host, look);
                hosts.Add(host);
                if (i < chars.Count && anim) byId[chars[i].id] = anim;
                ToonCheck($"attach {host.name}", anim != null, anim ? $"(clip mode {anim.ClipMode})" : "");
            }

            var camGo = new GameObject("LineupCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f);
            cam.fieldOfView = 32f;
            cam.transform.localPosition = new Vector3(0f, 0.9f, -13f);
            cam.transform.LookAt(root.transform.position + new Vector3(0f, 0.1f, 0f));
            cam.enabled = false;

            yield return new WaitForSeconds(1.2f);
            ShotCam(cam, "toon_lineup_idle");

            // ---- 3. looks: size, materials
            foreach (var c in chars)
            {
                var host = hosts.First(h => h.name == "Host_" + c.id);
                var rs = host.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && r.gameObject.activeInHierarchy && r.name != host.name).ToList();
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                bool allStandard = rs.All(r => r.sharedMaterials.All(m => m && m.shader && m.shader.name == "Standard" && m.mainTexture == cat.atlas));
                ToonCheck($"{c.id}: height ~1.85 m, Standard shader + atlas on every renderer", Mathf.Abs(b.size.y - ToonCharacterFactory.Height) < 0.2f && allStandard,
                    $"(height {b.size.y:0.00} m, renderers {rs.Count}, first y {b.min.y - host.transform.position.y:0.00})");
            }

            // ---- 4. animation states on the police officer
            var cop = byId.ContainsKey("police") ? byId["police"] : byId.Values.First();
            var w = cop.DebugWeights();
            ToonCheck("idle at rest", w[0] > 0.9f, $"(weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");
            ToonCheck("right hand bone found", cop.RightHand != null, cop.RightHand ? $"({cop.RightHand.name})" : "");

            for (float t = 0; t < 0.7f; t += Time.deltaTime) { cop.Locomote(true, 0.7f); yield return null; }
            w = cop.DebugWeights();
            ToonCheck("walks at low speed", w[1] > 0.8f, $"(weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");
            ShotCam(cam, "toon_lineup_walk");
            for (float t = 0; t < 0.7f; t += Time.deltaTime) { cop.Locomote(true, 1.4f); yield return null; }
            w = cop.DebugWeights();
            ToonCheck("runs at high speed", w[2] > 0.8f, $"(weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");
            ShotCam(cam, "toon_lineup_run");

            cop.Fall();
            for (float t = 0; t < 1.0f; t += Time.deltaTime) { cop.Fall(); yield return null; }
            w = cop.DebugWeights();
            ToonCheck("knocked down", w[3] > 0.8f, $"(weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");
            ShotCam(cam, "toon_lineup_down");
            cop.Recover();
            for (float t = 0; t < 3.0f; t += Time.deltaTime) { cop.Recover(); cop.Locomote(false); yield return null; }
            w = cop.DebugWeights();
            ToonCheck("gets back up to idle", w[0] > 0.8f, $"(weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");

            cop.PlaySlash(null, 1f);
            for (float t = 0; t < 0.4f; t += Time.deltaTime) { cop.Locomote(false); yield return null; }
            w = cop.DebugWeights();
            ToonCheck("swing plays", cop.Slashing && w[4] > 0.6f, $"(slashing {cop.Slashing}, weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");
            ShotCam(cam, "toon_lineup_swing");
            for (float t = 0; t < 1.6f; t += Time.deltaTime) { cop.Locomote(false); yield return null; }
            ToonCheck("swing ends", !cop.Slashing && cop.DebugWeights()[0] > 0.8f);

            // ---- 5. armed: the officer's pistol is hidden by default and can be shown
            var pistol = cop.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("w_"));
            ToonCheck("officer carries a pistol object", pistol != null, pistol ? $"({pistol.name}, active {pistol.gameObject.activeSelf})" : "");
            if (pistol)
            {
                ToonCheck("pistol hidden by default", !pistol.gameObject.activeSelf);
                ToonCharacterFactory.SetArmed(cop, true);
                yield return null;
                ToonCheck("pistol shows when armed", pistol.gameObject.activeSelf);
                ShotCam(cam, "toon_lineup_armed");
                ToonCharacterFactory.SetArmed(cop, false);
            }

            // ---- 6. a held melee weapon sits in the officer's / a player's hand
            if (MeleeWeapon.Available)
            {
                var holders = new[] { byId.ContainsKey("male_a") ? byId["male_a"] : null, byId.ContainsKey("police") ? byId["police"] : null, byId.ContainsKey("female") ? byId["female"] : null };
                var kinds = new[] { MeleeWeapon.Kind.Crowbar, MeleeWeapon.Kind.Bat, MeleeWeapon.Kind.Hatchet };
                for (int i = 0; i < holders.Length; i++)
                {
                    if (!holders[i] || !holders[i].RightHand) continue;
                    var held = MeleeWeapon.BuildHeld(kinds[i], holders[i].RightHand);
                    ToonCheck($"held {kinds[i]} on {holders[i].transform.parent.name}", held != null && held.transform.parent == holders[i].RightHand);
                }
                yield return new WaitForSeconds(0.6f);
                // a close look at each grip, at rest and mid swing
                cam.fieldOfView = 11f;
                foreach (var h in holders)
                {
                    if (!h) continue;
                    var at = h.transform.parent.position;
                    cam.transform.position = at + new Vector3(0.9f, 0.7f, -9f);
                    cam.transform.LookAt(at + new Vector3(0f, 0.2f, 0f));
                    yield return null;
                    ShotCam(cam, "toon_held_" + h.transform.parent.name.Replace("Host_", ""));
                    h.PlaySlash(null, 1f);
                    yield return new WaitForSeconds(0.3f);
                    ShotCam(cam, "toon_held_swing_" + h.transform.parent.name.Replace("Host_", ""));
                    yield return new WaitForSeconds(1f);
                }
            }
            else Log("[Toon] weapon catalog not available; skipped held weapon check");

            Log($"[Toon] DONE fails={toonFails}");
            Quit(toonFails == 0 ? 0 : 1);
        }
    }
}
