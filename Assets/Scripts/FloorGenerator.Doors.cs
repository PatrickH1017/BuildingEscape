using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The doors of an office floor: a sliding door in every doorway of the room partitions (the doorways have a beam over them, see
    /// OfficePlanner.WallRun). Players open and close them with E; officers open the ones they are about to walk through (patrolling, investigating or chasing). They are built after the
    /// NavMesh bake, so the bake sees every doorway open and the guards' routes never depend on a door. The doors themselves live in
    /// <see cref="DoorManager"/>.
    /// </summary>
    public partial class FloorGenerator
    {
        [Header("Doors")]
        [Tooltip("Sliding doors in the doorways between rooms: a player opens one with E and closes it with E again; officers open the ones they are about to walk through (not the ones they merely walk past). They make door noises.")]
        public bool slidingDoors = true;

        Material doorMat;

        /// <summary>Metal-grey, copied from the wall material so it follows whatever shader the project uses.</summary>
        Material DoorMat()
        {
            if (doorMat) return doorMat;
            if (!wallMat) return null;
            doorMat = new Material(wallMat) { hideFlags = HideFlags.HideAndDontSave };
            if (doorMat.HasProperty("_Color")) doorMat.color = new Color(0.50f, 0.58f, 0.66f);
            return doorMat;
        }

        void DestroyDoorMaterial()
        {
            if (doorMat) Kill(doorMat);
        }

        void BuildDoors()
        {
            if (!slidingDoors || layout == null || !Application.isPlaying) return;   // (the editor tools that build floors do not need doors)

            var holder = new GameObject("Doors");
            holder.transform.SetParent(current, false);
            var manager = holder.AddComponent<DoorManager>();
            var mat = DoorMat();
            foreach (var w in layout.walls)
            {
                if (w.kind == "header") manager.Add(MakeDoor(holder.transform, w, mat));
                else if (w.kind == "lockheader")
                {
                    // the guard outpost's front door: dark steel with wired-glass panes; the controller (BuildOutpostExtras) keeps it locked
                    outpostDoor = MakeOutpostDoor(holder.transform, w);
                    outpostDoorIndex = manager.Count;
                    manager.Add(outpostDoor);
                }
            }
            if (manager.Count == 0) { Kill(holder); return; }
            manager.OpenDoorsWithPeople();
        }

        /// <summary>One door in the doorway under a beam: the beam says where the doorway is, how wide and how high.</summary>
        SlidingDoor MakeDoor(Transform parent, WallBox beam, Material mat)
        {
            var rc = beam.rc;
            bool alongX = rc.W >= rc.D;
            float gap = alongX ? rc.W : rc.D, depth = alongX ? rc.D : rc.W, height = beam.y0;

            var go = new GameObject("Door");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(rc.Cx, 0f, rc.Cz), alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f));

            // what closes the doorway: only switched on while the door is shut (SlidingDoor)
            var blocker = go.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, height * 0.5f, 0f);
            blocker.size = new Vector3(gap, height, depth);

            return new SlidingDoor(go.transform, Leaf("LeafLow", go.transform, mat), Leaf("LeafHigh", go.transform, mat), blocker, alongX, gap, height, depth);
        }

        Transform Leaf(string name, Transform door, Material mat)
        {
            var leaf = Box(name, door.position, Vector3.one, mat, door, collider: false);
            leaf.transform.localRotation = Quaternion.identity;
            leaf.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return leaf.transform;
        }
    }
}
