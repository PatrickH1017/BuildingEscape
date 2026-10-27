import bpy
s = bpy.context.scene
print("INS file", bpy.data.filepath)
print("INS scene", s.name, "frames", s.frame_start, s.frame_end, "fps", s.render.fps, "unit", s.unit_settings.scale_length)
for m in s.timeline_markers:
    print("INS marker", m.frame, m.name)
for t in bpy.data.texts:
    print("INS text", t.name, repr(t.as_string()[:1500]))
for o in bpy.data.objects:
    act = o.animation_data.action.name if o.animation_data and o.animation_data.action else "-"
    print("INS obj %-44s %-9s parent=%s/%s act=%s loc=(%.3f %.3f %.3f) dims=(%.3f %.3f %.3f) hide=%s" % (o.name, o.type, o.parent.name if o.parent else "-", o.parent_bone if o.parent_type == "BONE" else o.parent_type, act, *o.matrix_world.translation, *o.dimensions, o.hide_render))
    for c in o.constraints:
        print("INS   constraint", c.type, getattr(c, "target", None) and c.target.name, getattr(c, "subtarget", ""), "inf", c.influence)
arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
for arm in arms:
    print("INS armature", arm.name, "bones", len(arm.data.bones), "scale", tuple(arm.scale))
for a in bpy.data.actions:
    n = len(a.fcurves) if hasattr(a, "fcurves") else -1
    try:
        n = sum(len(cb.fcurves) for l in a.layers for st in l.strips for cb in st.channelbags) if n <= 0 and hasattr(a, "layers") else n
    except Exception as e:
        pass
    print("INS action", a.name, "range", tuple(a.frame_range), "users", a.users, "channels", n)
