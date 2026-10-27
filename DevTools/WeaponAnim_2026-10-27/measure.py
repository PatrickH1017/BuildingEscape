"""Where the weapon is in the right hand (and the left hand on it) at one frame: blender -b X.blend --python measure.py -- <frame>"""
import bpy, sys, json
from mathutils import Vector, Matrix
fr = int(sys.argv[sys.argv.index("--") + 1])
s = bpy.context.scene
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
A = arm.matrix_world
pb = arm.pose.bones
def head(n): return A @ pb[n].head
def finger_roots(sd):
    kids = pb["Hand_" + sd].children
    thumb = next(c for c in kids if "Thumb" in c.name)
    index = next(c for c in kids if "Index" in c.name)
    other = next(c for c in kids if c not in (thumb, index))
    return thumb, index, other
def hand_frame(sd):
    _, index, other = finger_roots(sd)
    w = head("Hand_" + sd)
    i, o = A @ index.head, A @ other.head
    f = ((i + o) / 2 - w).normalized()
    u = i - o
    u = (u - f * u.dot(f)).normalized()
    palm = u.cross(f) if sd == "R" else f.cross(u)
    return w, f, u, palm
# k at rest
saved = {b.name: (b.rotation_quaternion.copy(), b.location.copy(), b.rotation_euler.copy()) for b in pb}
for b in pb:
    b.rotation_quaternion = (1, 0, 0, 0); b.location = (0, 0, 0); b.rotation_euler = (0, 0, 0)
bpy.context.view_layer.update()
k = (head("Head") - head("Root")).length / 1.57
armlen = ((head("Elbow_R") - head("Shoulder_R")).length + (head("Hand_R") - head("Elbow_R")).length) / k
for b in pb:
    b.rotation_quaternion, b.location, b.rotation_euler = saved[b.name]
s.frame_set(fr)
bpy.context.view_layer.update()
W = next(o for o in bpy.data.objects if o.type == "MESH" and o.parent == arm and o.parent_type == "BONE" and o.parent_bone == "Hand_R")
w, f, u, palm = hand_frame("R")
H = Matrix((f, u, palm))                 # rows: world -> hand coords
M = W.matrix_world
def hc(v): return [round(x, 5) for x in (H @ v)]
ax = [M.to_3x3() @ Vector(e) for e in ((1, 0, 0), (0, 1, 0), (0, 0, 1))]
scale = [a.length for a in ax]
out = {"file": bpy.data.filepath, "frame": fr, "k": round(k, 5), "arm": round(armlen, 5), "weapon": W.name,
       "origin_in_hand": hc((M.translation - w) / k), "x_in_hand": hc(ax[0].normalized()), "y_in_hand": hc(ax[1].normalized()), "z_in_hand": hc(ax[2].normalized()),
       "world_scale": [round(x, 6) for x in scale], "bbox_local_min": [round(min(c[i] for c in W.bound_box), 4) for i in range(3)], "bbox_local_max": [round(max(c[i] for c in W.bound_box), 4) for i in range(3)]}
Mi = M.inverted()
lw, lf, lu, lpalm = hand_frame("L")
out["left_wrist_in_weapon"] = [round(x, 4) for x in (Mi @ lw)]
out["right_wrist_in_weapon"] = [round(x, 4) for x in (Mi @ w)]
out["left_wrist_in_hand"] = hc((lw - w) / k)
R3 = Mi.to_3x3()
out["left_f_in_weapon"] = [round(x, 4) for x in (R3 @ lf).normalized()]
out["left_u_in_weapon"] = [round(x, 4) for x in (R3 @ lu).normalized()]
kids = {}
for o in bpy.data.objects:
    p = o.parent
    while p and p != W: p = p.parent
    if p == W:
        cs = [Mi @ (o.matrix_world @ Vector(c)) for c in o.bound_box]
        kids[o.name] = {"at": [round(x, 4) for x in (Mi @ o.matrix_world.translation)], "min": [round(min(c[i] for c in cs), 4) for i in range(3)], "max": [round(max(c[i] for c in cs), 4) for i in range(3)]}
out["parts"] = kids
print("MEAS " + json.dumps(out))
