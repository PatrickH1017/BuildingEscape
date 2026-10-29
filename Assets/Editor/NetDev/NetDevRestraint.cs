using System.Collections;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The owner's restraint, drag and rescue clips (2026-10-28), scenario <c>Restraint</c> (stage 1: the tie-up, the hogtied lie, the weapon put away, standing up again, an eliminated officer dragged by the foot; the later stages are <c>RestraintGuard</c>).
    /// The real player in a plain arena far from the building, as Dock worker 1; then on floor 1 with a real officer. Needs graphics: <c>qa_regress.ps1 -Methods Restraint:gfx</c>. Pictures: %TEMP%\be_shot_restraint_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int restraintFails;

        void RestraintCheck(string name, bool ok, string detail = "")
        {
            if (!ok) restraintFails++;
            Log($"[Restraint] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator RestraintScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            hm.godMode = true;
            GunController.TestCursorLocked = true;
            Time.captureDeltaTime = 1f / 30f;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var vis = player.GetComponent<PlayerVisual>();
            var rig = FindFirstObjectByType<CameraFollow>();
            var rv = player.GetComponent<RestraintVisual>();
            if (!rig || !pack || !vis || rv == null) { Log("[Restraint] no camera rig, pack, look or restraint visual"); Quit(1); yield break; }
            pack.capacity = 100f;
            RestraintCheck("the thirteen clips are in the build (Resources/Animations: tie-up, hogtied, the drags, the drop, the free-up; humanoid)", RestraintClips.AllThere, $"({string.Join(", ", RestraintClips.All.Where(n => RestraintClips.Get(n) == null))} missing)");

            var root = new GameObject("Restraint");
            root.transform.position = new Vector3(0f, 0f, 220f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.1f, 8f);
            ground.transform.localScale = new Vector3(40f, 0.2f, 120f);
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.57f, 0.60f) };
            var side = new GameObject("RestraintCam").AddComponent<Camera>();
            side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = new Color(0.55f, 0.66f, 0.82f); side.enabled = false; side.nearClipPlane = 0.02f;
            void Shot(Transform t, string name, float away = 2.6f, float high = 1.2f, float fov = 40f)
            {
                side.fieldOfView = fov;
                var at0 = rv.ModelRoot ? rv.ModelRoot.position : t.position - Vector3.up; side.transform.position = at0 + t.right * away + Vector3.up * high;
                var aim = rv.ModelRoot ? rv.ModelRoot.position : t.position - Vector3.up; side.transform.LookAt(aim + Vector3.up * 0.25f);
                ShotCam(side, "restraint_" + name);
            }

            var firstId = PlayerAppearance.DefaultToons.First();
            vis.Apply(new CharacterLook(firstId, Color.white, 1, null, 1f, 1f, firstId));
            player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
            rig.SetFirstPerson(false);
            yield return WaitFor(() => rig.Zoom <= 0.001f, 30f, "third person");
            rig.TurnAim(-rig.AimYaw);
            rig.TiltAim(-rig.AimPitch + 8f);
            // a crowbar in the hand: it must go while he is tied
            var crowbar = MeleeWeapon.Create(MeleeWeapon.Kind.Crowbar, player.transform.position + Vector3.up * 0.5f, null, 0f).GetComponent<Pickup>();
            pack.TryStore(crowbar);
            yield return null;
            pack.SelectSlot(0);
            yield return new WaitForSeconds(1.4f);
            player.TeleportTo(root.transform.position + Vector3.up * 1.1f, Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
            var anim = vis.Anim;
            var an = anim ? anim.GetComponent<Animator>() : null;
            var mc = player.GetComponent<MeleeController>();
            RestraintCheck("the character can play a full-body clip (humanoid, clip mode)", anim != null && anim.CanPlayBody && an != null);
            if (anim == null || !anim.CanPlayBody) { Log($"[Restraint] DONE fails={restraintFails}"); Quit(1); yield break; }
            RestraintCheck("(test) a crowbar is in his hand and the model is shown", mc.HeldModel != null && mc.HeldModel.activeSelf && rv.Current == RestraintVisual.Look.None && !anim.BodyActive);
            var rest = anim.transform.localPosition;
            var restRot = anim.transform.localRotation;
            var capsule = player.GetComponent<CapsuleCollider>();
            int standDir = capsule.direction;
            float floorY = root.transform.position.y;
            var head = an.GetBoneTransform(HumanBodyBones.Head);
            var hips = an.GetBoneTransform(HumanBodyBones.Hips);

            // ---------------- an officer pins him: the tie-up, once
            float t0 = Time.time;
            player.ApplyCustody(CustodyState.Pinned, 12f, 0);
            yield return null; yield return null;
            RestraintCheck("pinned: the tie-up plays on him (the full-body layer is on), his capsule is laid down on the floor and he is not tipped over", rv.Current == RestraintVisual.Look.TieUp && anim.BodyActive && player.IsLying && capsule.direction == 2 && Vector3.Dot(player.transform.up, Vector3.up) > 0.95f, $"(look {rv.Current}, body {anim.BodyActive}, lying {player.IsLying}, capsule direction {capsule.direction})");
            RestraintCheck("pinned: the crowbar is put away (the hands are tied)", mc.HeldModel != null && !mc.HeldModel.activeSelf);
            RestraintCheck("pinned: no zip tie yet at the start of the tie-up", rv.Ties == null || (!rv.Ties.WristsOn && !rv.Ties.AnklesOn && !rv.Ties.LinkOn));
            yield return new WaitForSeconds(0.5f);
            Shot(player.transform, "tieup_05");
            while (Time.time - t0 < 1.25f) yield return null;
            RestraintCheck("tie-up, 1.25 s in: the wrists are tied (frame 31), not yet the ankles", rv.Ties != null && rv.Ties.WristsOn && !rv.Ties.AnklesOn && !rv.Ties.LinkOn, $"(wrists {rv.Ties?.WristsOn}, ankles {rv.Ties?.AnklesOn}, link {rv.Ties?.LinkOn})");
            Shot(player.transform, "tieup_125");
            while (Time.time - t0 < 1.9f) yield return null;
            RestraintCheck("tie-up, 1.9 s in: the ankles are tied (frame 51), not yet the link", rv.Ties != null && rv.Ties.WristsOn && rv.Ties.AnklesOn && !rv.Ties.LinkOn, $"(wrists {rv.Ties?.WristsOn}, ankles {rv.Ties?.AnklesOn}, link {rv.Ties?.LinkOn})");
            Shot(player.transform, "tieup_19");
            while (Time.time - t0 < 2.35f) yield return null;
            RestraintCheck("tie-up, 2.35 s in: the link is through both (frame 63): all three ties", rv.Ties != null && rv.Ties.WristsOn && rv.Ties.AnklesOn && rv.Ties.LinkOn);
            Shot(player.transform, "tieup_235");
            while (Time.time - t0 < 3.2f) yield return null;
            RestraintCheck("after the tie-up he lies hogtied (the loop), the ties on", rv.Current == RestraintVisual.Look.Hogtied && rv.Ties != null && rv.Ties.LinkOn, $"(look {rv.Current})");
            RestraintCheck("tie-up: a ratchet is heard as each of the three ties goes on", rv.Ratchets == 3, $"({rv.Ratchets})");
            float lowHead = head.position.y - floorY, lowHips = hips.position.y - floorY;
            RestraintCheck("hogtied: he lies on the floor - his head and his hips are within 0.45 m of it", lowHead < 0.45f && lowHips < 0.45f && lowHead > -0.05f, $"(head {lowHead:0.00} m, hips {lowHips:0.00} m over the floor)");
            Shot(player.transform, "hogtied_side"); Shot(player.transform, "hogtied_high", 0.8f, 2.6f, 50f);

            // ---------------- arrested: he stays hogtied
            player.ApplyCustody(CustodyState.Busted, 0f, 1);
            yield return new WaitForSeconds(1.0f);
            RestraintCheck("busted: he lies hogtied still", rv.Current == RestraintVisual.Look.Hogtied && anim.BodyWeightNow > 0.99f && player.IsLying, $"(look {rv.Current}, weight {anim.BodyWeightNow:0.00})");

            // ---------------- got free: he stands up, the crowbar is back
            player.ApplyCustody(CustodyState.Free, 0f, 0);
            yield return new WaitForSeconds(0.6f);
            RestraintCheck("free: the full-body layer is off, the capsule stands again and the body is upright", rv.Current == RestraintVisual.Look.None && !anim.BodyActive && !player.IsLying && capsule.direction == standDir && Vector3.Dot(player.transform.up, Vector3.up) > 0.9f, $"(look {rv.Current}, body {anim.BodyActive}, lying {player.IsLying}, direction {capsule.direction})");
            RestraintCheck("free: the character is back where his model stands (no lasting offset) and the crowbar is in his hand again", (anim.transform.localPosition - rest).magnitude < 0.01f && Quaternion.Angle(anim.transform.localRotation, restRot) < 1f && mc.HeldModel != null && mc.HeldModel.activeSelf && (rv.Ties == null || !rv.Ties.WristsOn), $"(offset {(anim.transform.localPosition - rest).magnitude:0.000} m)");
            Shot(player.transform, "free_after");

            // ---------------- an eliminated officer dragged by the foot (floor 1)
            pack.Drop(crowbar); Destroy(crowbar.gameObject);
            hm.freezeTimer = true;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(0.8f);
            player = WobblyPlayerRegistry.All[0]; pack = player.GetComponent<Backpack>(); vis = player.GetComponent<PlayerVisual>(); rv = player.GetComponent<RestraintVisual>();
            rig = FindFirstObjectByType<CameraFollow>();
            if (rig) { rig.SetFirstPerson(false); }
            var g = GuardAI.All.FirstOrDefault(x => x && !x.Dead && !x.IsBoss && !x.IsBackup && x.name != "Guard_Outpost");
            RestraintCheck("(test) an officer to drag", g != null);
            if (g != null)
            {
                for (int i = 0; i < 40 && !g.Dead; i++) g.BulletHitHost(5f, true, null);
                yield return new WaitForSeconds(0.8f);
                var body = g.GetComponent<DraggableBody>();
                var grv = g.GetComponent<RestraintVisual>();
                var gv = g.GetComponent<GuardVisual>();
                RestraintCheck("an eliminated officer has a body that can be dragged and a restraint visual", g.Dead && body != null && body.CanBeDragged && grv != null && gv != null && gv.Anim != null && gv.Anim.CanPlayBody, $"(dead {g.Dead}, body {body != null}, visual {grv != null})");
                if (body != null && grv != null)
                {
                    var pg = player.GetComponent<PlayerGrab>();
                    var arena = root.transform.position + new Vector3(0f, 0f, 6f);
                    var agent = g.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agent) agent.enabled = false;          // (an agent off the floor plan would put him back where it thinks he is)
                    g.transform.position = arena + Vector3.up * 0.1f; g.transform.rotation = Quaternion.identity;
                    player.TeleportTo(arena + new Vector3(0f, 1.1f, 1.5f), Quaternion.identity);
                    yield return new WaitForSeconds(0.5f);
                    pg.ApplyDragBody(body);
                    player.testMove = Vector3.forward;
                    yield return new WaitForSeconds(0.8f);
                    var pan = player.GetComponent<PlayerVisual>().Anim.GetComponent<Animator>();
                    var gan = gv.Anim.GetComponent<Animator>();
                    float worstRoot = 0f, worstHand = 0f, handMin = 9f;
                    var probe = new GameObject("RestraintProbe").AddComponent<RestraintProbe>(); probe.a = rv; probe.b = grv; probe.hand = pan.GetBoneTransform(HumanBodyBones.RightHand); probe.foot = gan.GetBoneTransform(HumanBodyBones.RightFoot);
                    bool draggerLook = false, draggedLook = false;
                    for (float tm = 0f; tm < 2.2f; tm += Time.deltaTime)
                    {
                        yield return null;
                        draggerLook |= rv.Current == RestraintVisual.Look.DragFootDragger;
                        draggedLook |= grv.Current == RestraintVisual.Look.DragFootDragged;
                        worstRoot = probe.worstRoot;
                        float d = probe.handNow;
                        handMin = Mathf.Min(handMin, d); worstHand = Mathf.Max(worstHand, d);
                    }
                    var pt = player.transform;
                    side.fieldOfView = 40f;
                    var at1 = rv.ModelRoot.position; side.transform.position = at1 + Vector3.right * 3.2f + Vector3.up * 1.1f;
                    side.transform.LookAt(at1 + Vector3.back * 0.5f + Vector3.up * 0.4f);
                    ShotCam(side, "restraint_dragfoot_side");
                    side.transform.position = at1 + new Vector3(-1.6f, 1.5f, -3.2f);
                    side.transform.LookAt(at1 + Vector3.back * 0.6f + Vector3.up * 0.3f);
                    ShotCam(side, "restraint_dragfoot_behind");
                    player.testMove = null;
                    RestraintCheck("dragging an officer by the foot: the dragger plays his half and the officer's body the other half of one clip", draggerLook && draggedLook && rv.PairTime >= 0f, $"(dragger {draggerLook}, dragged {draggedLook}; dragging {pg.IsDragging}, body dragged {body.IsDragged}, guard active {g.gameObject.activeInHierarchy}, dragger look {rv.Current}, dragged look {grv.Current}, body at {g.transform.position}, player at {player.transform.position})");
                    RestraintCheck("the two halves are at one place (the dragged model is put on the dragger's) and the right hand is on the right ankle (within 15 cm)", worstRoot < 0.02f && worstHand < 0.15f, $"(models {worstRoot * 100f:0.0} cm apart at worst, hand off the ankle {handMin * 100f:0.0} - {worstHand * 100f:0.0} cm)");
                    pg.ApplyDropBody();
                    yield return new WaitForSeconds(0.6f);
                    RestraintCheck("let go: the dragger walks as he did again and the officer lies as he fell", rv.Current == RestraintVisual.Look.None && !gv.Anim.BodyActive && grv.Current == RestraintVisual.Look.None, $"(dragger {rv.Current}, dragged {grv.Current})");
                }
            }
            GunController.TestCursorLocked = null;
            Time.captureDeltaTime = 0f;
            Log($"[Restraint] DONE fails={restraintFails}");
            Quit(restraintFails == 0 ? 0 : 1);
        }
    }
}

namespace BuildingEscape.EditorTools
{
    /// <summary>The Restraint scenario's eye on a dragger and the dragged body: measured after every other late update has run (the placement of the dragged model is one), so it is the picture of this frame.</summary>
    [DefaultExecutionOrder(5000)]
    public class RestraintProbe : UnityEngine.MonoBehaviour
    {
        public RestraintVisual a, b;
        public UnityEngine.Transform hand, foot;
        public float worstRoot, handNow;

        void LateUpdate()
        {
            if (a && b && a.ModelRoot && b.ModelRoot && a.Current == RestraintVisual.Look.DragFootDragger && b.Current == RestraintVisual.Look.DragFootDragged)
            {
                worstRoot = UnityEngine.Mathf.Max(worstRoot, (a.ModelRoot.position - b.ModelRoot.position).magnitude);
                handNow = hand && foot ? (hand.position - foot.position).magnitude : 9f;
            }
        }
    }
}
