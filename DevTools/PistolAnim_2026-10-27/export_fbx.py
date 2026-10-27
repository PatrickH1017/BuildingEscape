"""Export a dock-worker pistol animation (.blend) as an FBX Unity can read as a humanoid clip, plus reference numbers.

blender -b <file.blend> --python export_fbx.py -- <out.fbx> <out.json> <frames,comma,separated>

- Only the armature and its action go into the FBX (the gun and the effects are the game's own).
- The right-hand finger bones are renamed from Blender's "Thumb_01.001" to Unity's "Thumb_01 1", the name Unity gives the
  second of two same-named nodes in the Synty FBX, so the clip's bones carry the same names as the game's avatars.
- The JSON holds, for each listed frame, where the key bones are in the character's frame (right, forward, up), measured
  from the Root bone and divided by k (the character's size against the stock 1.57 m head height), and the gun's place
  in the right hand's frame, for checking the clip in Unity and placing the game's pistol the same way.
"""
import bpy, json, sys
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
out_fbx, out_json = args[0], args[1]
frames = [int(f) for f in args[2].split(",")] if len(args) > 2 else []

scene = bpy.context.scene
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
act = arm.animation_data.action
print("EXP action", act.name, tuple(act.frame_range), "scene", scene.frame_start, scene.frame_end, scene.render.fps)

# ---- reference numbers (before any renaming)
A = arm.matrix_world
pb = arm.pose.bones


def head(name):
    return A @ pb[name].head


def finger_roots(s):
    kids = pb["Hand_" + s].children
    thumb = next(c for c in kids if "Thumb" in c.name)
    index = next(c for c in kids if "Index" in c.name)
    other = next(c for c in kids if c not in (thumb, index))
    return thumb, index, other


def hand_frame(s):
    _, index, other = finger_roots(s)
    w = head("Hand_" + s)
    i, o = A @ index.head, A @ other.head
    f = ((i + o) / 2 - w).normalized()
    u = i - o
    u = (u - f * u.dot(f)).normalized()
    palm = u.cross(f) if s == "R" else f.cross(u)
    return w, f, u, palm


scene.frame_set(scene.frame_start)
bpy.context.view_layer.update()
# the character's frame from the rest of the body at the first frame (as the build scripts did, at the rest pose)
root = head("Root")
up = Vector((0, 0, 1))
fwd = Vector((0, -1, 0))
right = fwd.cross(up)
saved = {b.name: (b.rotation_quaternion.copy(), b.location.copy()) for b in pb}
for b in pb:
    b.rotation_quaternion = (1, 0, 0, 0)
    b.location = (0, 0, 0)
bpy.context.view_layer.update()
k = (head("Head") - head("Root")).length / 1.57
eyes = head("Eyes") - root
for b in pb:
    b.rotation_quaternion, b.location = saved[b.name]
bpy.context.view_layer.update()


def cs(v):
    """A world vector in the character's frame (right, forward, up), in units of k."""
    return [round(v.dot(right) / k, 5), round(v.dot(fwd) / k, 5), round(v.dot(up) / k, 5)]


def csd(v):
    v = v.normalized()
    return [round(v.dot(right), 5), round(v.dot(fwd), 5), round(v.dot(up), 5)]


KEY = ["Hips", "Spine_03", "Neck", "Head", "Shoulder_R", "Elbow_R", "Hand_R", "Shoulder_L", "Elbow_L", "Hand_L"]
rf = finger_roots("R")
lf = finger_roots("L")


def tip(name):
    """The tip of a finger chain (the end bone's head)."""
    b = pb[name]
    while b.children:
        b = b.children[0]
    return A @ b.head


gun = next((o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("SM_Wep_Pistol_Heavy_01")), None)
data = {"k": k, "eyes": cs(eyes), "frames": {}}
for fr in frames:
    scene.frame_set(fr)
    bpy.context.view_layer.update()
    rec = {b: cs(head(b) - root) for b in KEY}
    rec["tip_index_R"] = cs(tip(rf[1].name) - root)
    rec["tip_other_R"] = cs(tip(rf[2].name) - root)
    rec["tip_thumb_R"] = cs((A @ pb[rf[0].name].children[0].children[0].tail) - root) if pb[rf[0].name].children and pb[rf[0].name].children[0].children else None
    rec["tip_index_L"] = cs(tip(lf[1].name) - root)
    w, f, u, palm = hand_frame("R")
    rec["hand_R_f"], rec["hand_R_u"] = csd(f), csd(u)
    w, f, u, palm = hand_frame("L")
    rec["hand_L_f"], rec["hand_L_u"] = csd(f), csd(u)
    if gun:
        w, f, u, palm = hand_frame("R")
        G = gun.matrix_world
        # the gun in the right hand's frame: origin at the wrist, axes f (to the knuckles), u (to the index side), palm; lengths in units of k
        H = Matrix((f, u, palm)).transposed()                 # columns f, u, palm
        Hi = H.inverted()
        o = Hi @ (G.translation - w) / k
        gx, gy, gz = (G.to_3x3() @ Vector((1, 0, 0))), (G.to_3x3() @ Vector((0, 1, 0))), (G.to_3x3() @ Vector((0, 0, 1)))
        rec["gun_in_hand"] = {"origin": [round(c, 5) for c in o], "x": [round(c, 5) for c in Hi @ gx.normalized()],
                              "y": [round(c, 5) for c in Hi @ gy.normalized()], "z": [round(c, 5) for c in Hi @ gz.normalized()],
                              "scale": round(G.to_scale()[0] / k, 7)}
    data["frames"][str(fr)] = rec

# ---- the export
renamed = []
for b in arm.data.bones:
    if b.name.endswith(".001"):
        old = b.name
        b.name = old[:-4] + " 1"
        renamed.append((old, b.name))
paths = {fc.data_path.split('"')[1] for fc in act.fcurves if fc.data_path.startswith("pose.bones")} if hasattr(act, "fcurves") else set()
print("EXP renamed", len(renamed), "bones; action channels for renamed:", sum(1 for _, n in renamed if n in paths), "stale:", sum(1 for o, _ in renamed if o in paths))
data["renamed"] = renamed

bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=out_fbx, use_selection=True, object_types={"ARMATURE"},
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", global_scale=1.0, axis_forward="-Z", axis_up="Y",
    add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False, armature_nodetype="NULL",
    bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0, use_metadata=True)
with open(out_json, "w") as fh:
    json.dump(data, fh, indent=1)
print("EXP wrote", out_fbx, "and", out_json, "k=%.4f" % k)
