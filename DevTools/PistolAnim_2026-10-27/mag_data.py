"""Measure the reload's magazine for the game: where the new magazine is in the LEFT hand, and the way the empty one leaves the grip.

blender -b Silenced_Pistol_Reload.blend --python mag_data.py

Prints, for the frames the new magazine rides in the left hand, its origin along the left hand's f, u and palm (the frame of
PistolAnimation.HandFrame, in units of the character's size k) and its +Y and +Z axes in the same frame (HeldGun.MagInHand,
MagInHandY, MagInHandZ), and the empty magazine's way out of the grip in the gun's own frame (HeldGun.MagEject).
"""
import bpy
import numpy
from mathutils import Matrix, Vector

scene = bpy.context.scene
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
A = arm.matrix_world
pb = arm.pose.bones
new = next(o for o in bpy.data.objects if o.name.startswith("SM_Wep_Pistol_Heavy_Mag_01_New"))
old = next(o for o in bpy.data.objects if o.name == "SM_Wep_Pistol_Heavy_Mag_01")


def head(name):
    return A @ pb[name].head


def hand_frame(s):
    kids = pb["Hand_" + s].children
    index = next(c for c in kids if "Index" in c.name)
    other = next(c for c in kids if "Thumb" not in c.name and "Index" not in c.name)
    w = head("Hand_" + s)
    i, o = A @ index.head, A @ other.head
    f = ((i + o) / 2 - w).normalized()
    u = i - o
    u = (u - f * u.dot(f)).normalized()
    palm = u.cross(f) if s == "R" else f.cross(u)
    return w, f, u, palm


# the character's size: its head over the stock 1.57 m, at the rest pose
action = arm.animation_data.action
arm.animation_data.action = None
for b in pb:
    b.rotation_quaternion = (1, 0, 0, 0)
bpy.context.view_layer.update()
k = (head("Head") - head("Root")).length / 1.57
arm.animation_data.action = action

for fr in (23, 30, 36):
    scene.frame_set(fr)
    bpy.context.view_layer.update()
    w, f, u, palm = hand_frame("L")
    M = new.matrix_world
    Hi = Matrix((f, u, palm)).transposed().inverted()
    o = Hi @ (M.translation - w) / k
    y = Hi @ (M.to_3x3() @ Vector((0, 1, 0))).normalized()
    z = Hi @ (M.to_3x3() @ Vector((0, 0, 1))).normalized()
    print("MAG frame %d origin (%.5f %.5f %.5f) y (%.4f %.4f %.4f) z (%.4f %.4f %.4f)" % (fr, *o, *y, *z))

# the way out of the grip: the empty magazine's long axis (a principal axis of its points), pointing away from the top of the gun
scene.frame_set(1)
P = [old.matrix_local @ v.co for v in old.data.vertices]
c = sum(P, Vector()) / len(P)
X = numpy.array([tuple(p - c) for p in P])
_, V = numpy.linalg.eigh(X.T @ X)
e = Vector(V[:, 2].tolist()).normalized()
if e.y > 0:
    e = -e
print("EJECT in the gun's frame (+Z the barrel, +Y the top): (%.4f %.4f %.4f), k = %.4f" % (*e, k))
