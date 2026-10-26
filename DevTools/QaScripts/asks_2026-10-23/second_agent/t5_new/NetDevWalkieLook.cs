using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pictures of the walkie talkie (needs graphics: <c>ui.ps1 -Methods WalkieLook -Project C:\bd</c>; the pictures go to %TEMP%\be_shot_walkie_*.png): lying on a table-high block, and in the right hand of a Tiny Toon person and a Party character, from the side and from the front,
    /// for tuning how it sits (T5, the owner's list of 2026-10-23). The log says how big the model measured.
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator WalkieLookScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            bool ok = WalkieTalkie.Measure(out var lay, out float scale, out var centre, out var size);
            Log($"[WalkieLook] model measured {ok}: size {size.x:0.000} x {size.y:0.000} x {size.z:0.000}, centre {centre}, lying turn {lay.eulerAngles}, scale {scale:0.000}");
            var root = new GameObject("WalkieLookLineup");
            root.transform.position = new Vector3(0f, 0f, 90f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -1.05f, 0f);
            ground.transform.localScale = new Vector3(40f, 0.1f, 12f);
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.38f, 0.42f) };
            var camGo = new GameObject("WalkieLookCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f); cam.enabled = false;

            // lying about
            var lying = WalkieTalkie.Create(root.transform.position + new Vector3(-6f, -0.7f, 0f), null, 20f);
            foreach (var rb in lying.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
            cam.fieldOfView = 30f;
            cam.transform.position = lying.transform.position + new Vector3(0f, 0.55f, -0.8f);
            cam.transform.LookAt(lying.transform.position);
            yield return null;
            ShotCam(cam, "walkie_lying");

            // in the hand
            var cat = CharacterCatalog.Instance;
            var anims = new List<CharacterAnimator>(); var names = new List<string>();
            var chars = cat.characters.ToList();
            for (int i = 0; i <= Mathf.Min(chars.Count, 2); i++)
            {
                var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                host.name = i < chars.Count ? "Host_" + chars[i].id : "Host_party";
                host.transform.SetParent(root.transform, false);
                host.transform.localPosition = new Vector3(i * 3.2f, 0f, 0f);
                host.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);        // facing the camera, which is at -z
                Object.Destroy(host.GetComponent<Collider>());
                var look = i < chars.Count ? new CharacterLook(chars[i].id, Color.white, 1, null, 1f, 1f, chars[i].id) : PartyCharacterFactory.PlayerLooks[0];
                var anim = PartyCharacterFactory.Attach(host, look);
                anims.Add(anim); names.Add(host.name);
            }
            foreach (var an in anims) if (an) { var ar = an.GetComponent<Animator>(); if (ar) ar.cullingMode = AnimatorCullingMode.AlwaysAnimate; foreach (var smr in an.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true; }
            yield return new WaitForSeconds(1.0f);
            for (int i = 0; i < anims.Count; i++)
            {
                if (!anims[i]) continue;
                var model = HeldWalkie.BuildHeld(anims[i]);
                Log($"[WalkieLook] {names[i]}: model in hand {(model ? model.name : "none")}");
            }
            yield return new WaitForSeconds(0.6f);
            foreach (var (label, offset) in new[] { ("front", new Vector3(-0.35f, 0.15f, -2.2f)), ("side", new Vector3(-2.2f, 0.15f, -0.2f)), ("close", new Vector3(-0.7f, 0.1f, -1.3f)) })
            {
                for (int i = 0; i < anims.Count; i++)
                {
                    if (!anims[i]) continue;
                    var at = root.transform.position + new Vector3(i * 3.2f, 0f, 0f);
                    cam.fieldOfView = label == "close" ? 22f : 30f;
                    cam.transform.position = at + offset;
                    cam.transform.LookAt(at + new Vector3(-0.2f, 0.1f, 0f));
                    yield return null;
                    ShotCam(cam, $"walkie_{names[i].Replace("Host_", "")}_{label}");
                }
            }
            Log("[WalkieLook] DONE fails=0");
            Quit(0);
        }
    }
}
