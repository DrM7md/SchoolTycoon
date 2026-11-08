using SchoolDirectorTycoon.Bridge;
using UnityEngine;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Greybox geometry built from primitives until final art arrives. No colliders:
    /// picking uses the ground plane, not physics.</summary>
    public static class Placeholders
    {
        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>A building: body plus a darker roof slab, pivot at ground level.</summary>
        public static GameObject Building(FacilityKind kind, FacilityStyle style, Transform parent)
        {
            var root = new GameObject(kind.ToString());
            root.transform.SetParent(parent, false);
            float w = style.size.x * GridMetrics.TileSize, d = style.size.z * GridMetrics.TileSize, h = style.size.y;
            Box("Body", root.transform, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), style.body);
            Box("Roof", root.transform, new Vector3(0f, h + 0.1f, 0f), new Vector3(w * 1.08f, 0.2f, d * 1.08f), style.roof);
            return root;
        }
    }
}
