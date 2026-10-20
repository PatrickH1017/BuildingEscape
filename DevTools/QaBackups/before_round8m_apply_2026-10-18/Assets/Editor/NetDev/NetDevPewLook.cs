using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pictures of the pew in the hands of every kind of character (needs graphics): each Tiny Toon person and a Party character,
    /// with the arm raised along +x (a side view) and across towards the camera, resting, and with the gun lowered. For tuning how it sits.
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator PewLookScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var cat = CharacterCatalog.Instance;
            var root = new GameObject("PewLookLineup");
            root.transform.position = new Vector3(0f, 0f, 90f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -1.05f, 0f);
            ground.transform.localScale = new Vector3(40f, 0.1f, 12f);
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.38f, 0.42f) };

            var names = new List<string>();
            var anims = new List<CharacterAnimator>();
            var chars = cat.characters.ToList();
            for (int i = 0; i <= chars.Count; i++)
            {
                var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                host.name = i < chars.Count ? "Host_" + chars[i].id : "Host_party";
                host.transform.SetParent(root.transform, false);
                host.transform.localPosition = new Vector3((i - chars.Count / 2f) * 3.2f, 0f, 0f);
                host.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);        // facing the camera, which is at -z
                Object.Destroy(host.GetComponent<Collider>());
                var look = i < chars.Count
                    ? new CharacterLook(chars[i].id, Color.white, 1, null, 1f, 1f, chars[i].id)
                    : PartyCharacterFactory.PlayerLooks[0];
                var anim = PartyCharacterFactory.Attach(host, look);
                names.Add(host.name); anims.Add(anim);
            }
            foreach (var an in anims) if (an) { var ar = an.GetComponent<Animator>(); if (ar) ar.cullingMode = AnimatorCullingMode.AlwaysAnimate; foreach (var smr in an.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true; }
            yield return new WaitForSeconds(1.0f);

            var camGo = new GameObject("PewLookCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f); cam.enabled = false;

            var helds = new List<HeldGun>();
            for (int i = 0; i < anims.Count; i++)
            {
                var held = anims[i] ? HeldGun.Attach(anims[i], RangedWeapon.Kind.Pew) : null;
                PewCheck($"look: the pew goes into the hand of {names[i]}", held != null, held != null ? "(" + held.ArmInfo() + ")" : "");
                helds.Add(held);
            }

            // pose 1: the arm raised, aimed along +x (a side view) / pose 2: raised, aimed 45 degrees towards the camera / pose 3: lowered
            var aims = new[] { ("side", Vector3.left), ("front45", (Vector3.left + Vector3.back).normalized), ("away", Vector3.forward), ("down", Vector3.down) };   // (they face the camera: their right hand is on the -x side)
            foreach (var (label, dir) in aims)
            {
                foreach (var h in helds) if (h) { h.Raise = 1f; h.AimDirection = dir; }
                yield return new WaitForSeconds(0.9f);
                for (int i = 0; i < anims.Count; i++)
                {
                    if (!anims[i]) continue;
                    var at = root.transform.position + root.transform.TransformVector(new Vector3((i - chars.Count / 2f) * 3.2f, 0f, 0f));
                    // (a body's pivot is its middle: the chest is a little above it, the head 0.85 m above, the feet one metre below)
                    cam.fieldOfView = 30f;
                    cam.transform.position = at + new Vector3(-0.45f, 0.2f, -3.2f);
                    cam.transform.LookAt(at + new Vector3(-0.3f, 0.1f, 0f));
                    yield return null;
                    ShotCam(cam, $"pewlook_{names[i].Replace("Host_", "")}_{label}");
                }
            }
            // low ready (a guard on patrol): the gun out in front, pointing at the floor ahead (he faces the camera)
            foreach (var h in helds) if (h) { h.Raise = 0.55f; h.AimDirection = (Vector3.back * 0.75f + Vector3.down * 0.65f).normalized; }
            yield return new WaitForSeconds(0.9f);
            for (int i = 0; i < anims.Count; i++)
            {
                if (!anims[i]) continue;
                var at = root.transform.position + root.transform.TransformVector(new Vector3((i - chars.Count / 2f) * 3.2f, 0f, 0f));
                cam.fieldOfView = 30f;
                cam.transform.position = at + new Vector3(-1.3f, 0.2f, -2.8f);
                cam.transform.LookAt(at + new Vector3(-0.15f, -0.05f, 0f));
                yield return null;
                ShotCam(cam, $"pewlook_{names[i].Replace("Host_", "")}_ready");
            }
            // lowered (not aiming): how it hangs in the hand
            foreach (var h in helds) if (h) { h.Raise = 0f; }
            yield return new WaitForSeconds(0.9f);
            for (int i = 0; i < anims.Count; i++)
            {
                if (!anims[i]) continue;
                var at = root.transform.position + root.transform.TransformVector(new Vector3((i - chars.Count / 2f) * 3.2f, 0f, 0f));
                cam.fieldOfView = 30f;
                cam.transform.position = at + new Vector3(-0.7f, 0.0f, -3.0f);
                cam.transform.LookAt(at + new Vector3(-0.15f, -0.2f, 0f));
                yield return null;
                ShotCam(cam, $"pewlook_{names[i].Replace("Host_", "")}_lowered");
            }
            Log($"[Pew] look DONE fails={pewFails}");
            Quit(pewFails == 0 ? 0 : 1);
        }
    }
}
