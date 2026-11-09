#nullable enable
using UnityEngine;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Destroys a runtime-generated mesh together with the object that shows it.</summary>
    public sealed class OwnedMesh : MonoBehaviour
    {
        public Mesh? Mesh;

        private void OnDestroy()
        {
            if (Mesh == null) return;
            if (Application.isPlaying) Destroy(Mesh);
            else DestroyImmediate(Mesh);
        }
    }
}
