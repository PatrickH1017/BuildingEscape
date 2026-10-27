import bpy, math
from mathutils import Vector
s = bpy.context.scene
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
wep = [o for o in bpy.data.objects if o.type == "MESH" and o.parent == arm and o.parent_type == "BONE" and o.parent_bone == "Hand_R"]
wep = wep[0] if wep else None
flashes = [o for o in bpy.data.objects if o.name.startswith("MuzzleFlash")]
parts = [o for o in bpy.data.objects if o.animation_data and o.animation_data.action and o not in flashes and o != arm and not o.name.startswith("Casing")]
# keyframes of the small actions
def fcurves(act):
    if hasattr(act, "fcurves") and len(act.fcurves):
        return list(act.fcurves)
    out = []
    for l in getattr(act, "layers", []):
        for st in l.strips:
            for cb in st.channelbags:
                out += list(cb.fcurves)
    return out
for o in flashes + parts:
    for fc in fcurves(o.animation_data.action):
        ks = [(round(k.co[0], 1), round(k.co[1], 3)) for k in fc.keyframe_points]
        print("TIM keys %-36s %-16s[%d] %s" % (o.name, fc.data_path, fc.array_index, ks[:40]))
# bones animated
bones = sorted({fc.data_path.split('"')[1] for fc in fcurves(arm.animation_data.action) if fc.data_path.startswith("pose.bones")})
print("TIM bones animated", len(bones), bones)
fwd = Vector((0, -1, 0))
prev_tip = None
long_axis = None
if wep:
    d = wep.dimensions
    i = max(range(3), key=lambda k: d[k])
    long_axis = i
for f in range(s.frame_start, s.frame_end + 1):
    s.frame_set(f)
    line = "TIM f%3d" % f
    hr = arm.matrix_world @ arm.pose.bones["Hand_R"].head
    hl = arm.matrix_world @ arm.pose.bones["Hand_L"].head
    line += " handR(%.3f %.3f %.3f) handL(%.3f %.3f %.3f)" % (*hr, *hl)
    if wep:
        M = wep.matrix_world
        ax = (M.to_3x3() @ Vector([1 if k == long_axis else 0 for k in range(3)])).normalized()
        # tip: the far end of the weapon box along the long axis from the hand
        cs = [M @ Vector(c) for c in wep.bound_box]
        tip = max(cs, key=lambda p: (p - hr).length)
        sp = (tip - prev_tip).length * s.render.fps if prev_tip is not None else 0.0
        prev_tip = tip
        el = math.degrees(math.asin(max(-1, min(1, ax.z))))
        yaw = math.degrees(math.atan2(ax.x, -ax.y))
        line += " ax(%.2f %.2f %.2f) el%6.1f yaw%7.1f tip(%.2f %.2f %.2f) tipspeed %5.2f" % (*ax, el, yaw, *tip, sp)
    for fl in flashes:
        line += " %s=%.2f" % (fl.name[-5:], fl.matrix_world.to_scale()[0])
    print(line)
