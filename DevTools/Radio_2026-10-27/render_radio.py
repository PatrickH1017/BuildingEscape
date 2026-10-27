import bpy, sys, math
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:]
fbx, tex, out = args[0], args[1], args[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
mat = bpy.data.materials.new("atlas"); mat.use_nodes = True
nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
img = nt.nodes.new("ShaderNodeTexImage"); img.image = bpy.data.images.load(tex)
nt.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
for o in objs:
    o.data.materials.clear(); o.data.materials.append(mat)
    print("RAD obj", o.name, "dims", tuple(round(d, 4) for d in o.dimensions), "loc", tuple(round(c, 3) for c in o.matrix_world.translation), "rot", tuple(round(math.degrees(r), 1) for r in o.rotation_euler), "verts", len(o.data.vertices))
# bounds
pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
print("RAD bounds", tuple(round(c, 4) for c in mn), tuple(round(c, 4) for c in mx))
c = (mn + mx) / 2; size = (mx - mn).length
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
pass

scene.render.resolution_x = 900; scene.render.resolution_y = 700
w = bpy.data.worlds.new("w"); scene.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = 1.5
for i, (name, d) in enumerate([("front", Vector((0, -1, 0.15))), ("back", Vector((0, 1, 0.15))), ("top", Vector((0.0, -0.3, 1))), ("side", Vector((1, -0.4, 0.2)))]):
    cam = bpy.data.cameras.new(name); co = bpy.data.objects.new(name, cam); scene.collection.objects.link(co)
    co.location = c + d.normalized() * size * 1.6
    co.rotation_euler = (c - co.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = co
    scene.render.filepath = out + "_" + name + ".png"
    bpy.ops.render.render(write_still=True)
