import bpy, sys, bmesh
from mathutils import Vector
fbx = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
o = next(x for x in bpy.context.scene.objects if x.type == 'MESH')
M = o.matrix_world
me = o.data
pts = [M @ v.co for v in me.vertices]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
print("BTN bounds", tuple(round(c,4) for c in mn), tuple(round(c,4) for c in mx))
uv = me.uv_layers.active.data
N = M.to_3x3()
for f in me.polygons:
    n = (N @ f.normal).normalized()
    c = M @ f.center
    if abs(c.x) < 0.12 and c.y < -0.1:
        vs = [M @ me.vertices[i].co for i in f.vertices]
        u = sum(uv[l].uv.x for l in f.loop_indices) / len(f.loop_indices); vv = sum(uv[l].uv.y for l in f.loop_indices) / len(f.loop_indices)
        print("BTN n(%.2f %.2f %.2f) " % tuple(n) + "face c(%.4f %.4f %.4f) size x %.4f z %.4f area %.1fcm2 uv(%.3f %.3f) frac x %.3f up %.3f" % (c.x, c.y, c.z, max(v.x for v in vs)-min(v.x for v in vs), max(v.z for v in vs)-min(v.z for v in vs), f.area*1e4, u, vv, (c.x-mn.x)/(mx.x-mn.x), (c.z-mn.z)/(mx.z-mn.z)))
