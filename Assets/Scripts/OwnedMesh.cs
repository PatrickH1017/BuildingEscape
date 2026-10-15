using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Frees a mesh that was made at run time when the object it hangs on goes (a mesh is not destroyed with its object).</summary>
    public sealed class OwnedMesh : MonoBehaviour
    {
        public Mesh mesh;

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
        }
    }
}
