#nullable enable
using System.Collections.Generic;
using SchoolDirectorTycoon.Bridge;
using UnityEngine;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Detailed procedural facility models. Each model is assembled from boxes and then merged into
    /// one mesh with one submesh per material, so a building costs one renderer and a few draw calls.
    /// Without the detail materials in the palette, facilities fall back to <see cref="Placeholders.Building"/>.</summary>
    public static class FacilityModels
    {
        private static readonly Dictionary<PrimitiveType, Mesh> primitives = new Dictionary<PrimitiveType, Mesh>();

        public static GameObject Create(FacilityKind kind, CampusPalette palette, Transform parent)
        {
            FacilityStyle style = palette.StyleFor(kind);
            if (palette.HasDetailMaterials)
            {
                switch (kind)
                {
                    case FacilityKind.Classroom: return Classroom(style, palette, parent);
                    case FacilityKind.Lab: return Lab(style, palette, parent);
                    case FacilityKind.Library: return Library(style, palette, parent);
                    case FacilityKind.Bus: return Bus(style, palette, parent);
                    case FacilityKind.Canteen: return Canteen(style, palette, parent);
                }
            }
            return Placeholders.Building(kind, style, parent);
        }

        /// <summary>Two-storey classroom block in a Gulf school style: sand walls on a stone plinth, glazed
        /// window bands with vertical shading fins, a parapet roof with AC units, and a shaded maroon entrance
        /// facing -Z. Pivot at ground centre; stays inside its footprint (plus a small plinth lip).</summary>
        public static GameObject Classroom(FacilityStyle style, CampusPalette palette, Transform parent)
        {
            float w = style.size.x * GridMetrics.TileSize, d = style.size.z * GridMetrics.TileSize, h = style.size.y;
            var kit = new Kit();
            Material body = style.body, roof = style.roof, glass = palette.glass!, trim = palette.trim!, accent = palette.accent!;

            const float plinth = 0.18f, parapet = 0.22f, wall = 0.07f;
            float storey = (h - plinth) * 0.5f;
            float top = plinth + 2f * storey;

            kit.Box(trim, new Vector3(0f, plinth * 0.5f, 0f), new Vector3(w + 0.16f, plinth, d + 0.16f));
            kit.Box(body, new Vector3(0f, plinth + storey, 0f), new Vector3(w, 2f * storey, d));
            kit.Box(trim, new Vector3(0f, plinth + storey, 0f), new Vector3(w + 0.06f, 0.08f, d + 0.06f)); // floor band
            kit.Box(trim, new Vector3(0f, top + 0.03f, 0f), new Vector3(w + 0.08f, 0.06f, d + 0.08f));  // cornice

            // Parapet walls and recessed roof deck.
            float py = top + 0.06f + parapet * 0.5f;
            kit.Box(body, new Vector3(0f, py, -d * 0.5f + wall * 0.5f), new Vector3(w, parapet, wall));
            kit.Box(body, new Vector3(0f, py, d * 0.5f - wall * 0.5f), new Vector3(w, parapet, wall));
            kit.Box(body, new Vector3(-w * 0.5f + wall * 0.5f, py, 0f), new Vector3(wall, parapet, d - 2f * wall));
            kit.Box(body, new Vector3(w * 0.5f - wall * 0.5f, py, 0f), new Vector3(wall, parapet, d - 2f * wall));
            kit.Box(roof, new Vector3(0f, top + 0.08f, 0f), new Vector3(w - 2f * wall, 0.04f, d - 2f * wall));

            // Rooftop AC units.
            kit.Box(trim, new Vector3(w * 0.22f, top + 0.22f, d * 0.12f), new Vector3(0.42f, 0.24f, 0.34f));
            kit.Box(trim, new Vector3(-w * 0.18f, top + 0.2f, d * 0.18f), new Vector3(0.34f, 0.2f, 0.3f));
            kit.Box(roof, new Vector3(w * 0.22f, top + 0.345f, d * 0.12f), new Vector3(0.26f, 0.01f, 0.26f));

            // Window bands with shading fins on the long faces; narrow windows on the ends.
            const int bays = 5;
            float bay = w / bays;
            for (int floor = 0; floor < 2; floor++)
            {
                float y0 = plinth + floor * storey;
                float wy = y0 + storey * 0.55f, wh = storey * 0.46f;
                foreach (float side in new[] { -1f, 1f })
                {
                    float z = side * (d * 0.5f + 0.012f);
                    for (int b = 0; b < bays; b++)
                    {
                        float x = -w * 0.5f + bay * (b + 0.5f);
                        bool door = floor == 0 && side < 0f && b == bays / 2;
                        if (!door) kit.Box(glass, new Vector3(x, wy, z), new Vector3(bay * 0.72f, wh, 0.02f));
                    }
                    for (int f = 0; f <= bays; f++)
                    {
                        float x = -w * 0.5f + bay * f;
                        x = Mathf.Clamp(x, -w * 0.5f + 0.03f, w * 0.5f - 0.03f);
                        kit.Box(trim, new Vector3(x, y0 + storey * 0.5f, side * (d * 0.5f + 0.06f)), new Vector3(0.05f, storey * 0.92f, 0.12f));
                    }
                    kit.Box(trim, new Vector3(0f, wy - wh * 0.5f - 0.03f, side * (d * 0.5f + 0.04f)), new Vector3(w * 0.96f, 0.04f, 0.08f)); // sill
                }
                foreach (float side in new[] { -1f, 1f })
                    kit.Box(glass, new Vector3(side * (w * 0.5f + 0.012f), wy, 0f), new Vector3(0.02f, wh, d * 0.34f));
            }

            // Entrance: maroon door panel, canopy on two posts, a step and a name band above.
            float doorW = bay * 0.8f, doorH = storey * 0.78f, front = -d * 0.5f;
            kit.Box(accent, new Vector3(0f, plinth + doorH * 0.5f, front - 0.012f), new Vector3(doorW, doorH, 0.02f));
            kit.Box(glass, new Vector3(0f, plinth + doorH * 0.62f, front - 0.025f), new Vector3(doorW * 0.6f, doorH * 0.5f, 0.01f));
            kit.Box(trim, new Vector3(0f, plinth + doorH + 0.06f, front - 0.24f), new Vector3(doorW * 1.9f, 0.06f, 0.48f));
            kit.Box(accent, new Vector3(0f, plinth + doorH + 0.1f, front - 0.47f), new Vector3(doorW * 1.9f, 0.05f, 0.02f));
            foreach (float side in new[] { -1f, 1f })
                kit.Box(trim, new Vector3(side * doorW * 0.85f, (plinth + doorH) * 0.5f, front - 0.43f), new Vector3(0.05f, plinth + doorH, 0.05f));
            kit.Box(trim, new Vector3(0f, 0.05f, front - 0.2f), new Vector3(doorW * 1.4f, 0.1f, 0.3f));
            kit.Box(accent, new Vector3(0f, top - storey * 0.12f, front - 0.14f), new Vector3(w * 0.36f, storey * 0.14f, 0.03f));

            return kit.Build("Classroom", parent);
        }

        private static void Parapet(Kit kit, Material m, float w, float d, float y, float height, float thick)
        {
            kit.Box(m, new Vector3(0f, y, -d * 0.5f + thick * 0.5f), new Vector3(w, height, thick));
            kit.Box(m, new Vector3(0f, y, d * 0.5f - thick * 0.5f), new Vector3(w, height, thick));
            kit.Box(m, new Vector3(-w * 0.5f + thick * 0.5f, y, 0f), new Vector3(thick, height, d - 2f * thick));
            kit.Box(m, new Vector3(w * 0.5f - thick * 0.5f, y, 0f), new Vector3(thick, height, d - 2f * thick));
        }

        /// <summary>Science block: glazed curtain wall on the entrance face, solid side walls and fume stacks.</summary>
        public static GameObject Lab(FacilityStyle style, CampusPalette p, Transform parent)
        {
            float w = style.size.x * GridMetrics.TileSize, d = style.size.z * GridMetrics.TileSize, h = style.size.y;
            const float plinth = 0.18f;
            float front = -d * 0.5f;
            var kit = new Kit();
            kit.Box(p.trim!, new Vector3(0f, plinth * 0.5f, 0f), new Vector3(w + 0.14f, plinth, d + 0.14f));
            kit.Box(style.body, new Vector3(0f, plinth + (h - plinth) * 0.5f, 0f), new Vector3(w, h - plinth, d));
            kit.Box(p.glass!, new Vector3(0f, plinth + (h - plinth) * 0.5f, front - 0.012f), new Vector3(w * 0.86f, (h - plinth) * 0.86f, 0.02f));
            for (int i = 0; i <= 6; i++)
                kit.Box(p.trim!, new Vector3(-w * 0.43f + w * 0.86f * i / 6f, plinth + (h - plinth) * 0.5f, front - 0.04f), new Vector3(0.05f, (h - plinth) * 0.88f, 0.06f));
            for (int i = 0; i <= 3; i++)
                kit.Box(p.trim!, new Vector3(0f, plinth + (h - plinth) * (0.07f + 0.86f * i / 3f), front - 0.04f), new Vector3(w * 0.88f, 0.05f, 0.06f));
            kit.Box(p.accent!, new Vector3(0f, plinth + 0.5f, front - 0.08f), new Vector3(0.62f, 1f, 0.03f));
            foreach (float side in new[] { -1f, 1f })
                for (int i = -1; i <= 1; i++)
                    kit.Box(p.glass!, new Vector3(side * (w * 0.5f + 0.012f), plinth + (h - plinth) * 0.58f, i * d * 0.28f), new Vector3(0.02f, (h - plinth) * 0.5f, d * 0.16f));
            kit.Box(p.accent!, new Vector3(0f, h - 0.02f, 0f), new Vector3(w + 0.06f, 0.08f, d + 0.06f));
            Parapet(kit, style.body, w, d, h + 0.14f, 0.24f, 0.07f);
            kit.Box(style.roof, new Vector3(0f, h + 0.05f, 0f), new Vector3(w - 0.14f, 0.04f, d - 0.14f));
            for (int i = -1; i <= 1; i++)
            {
                kit.Cylinder(p.trim!, new Vector3(i * w * 0.26f, h + 0.5f, d * 0.2f), 0.2f, 0.9f);
                kit.Cylinder(style.roof, new Vector3(i * w * 0.26f, h + 0.97f, d * 0.2f), 0.26f, 0.05f);
            }
            return kit.Build("Lab", parent);
        }

        /// <summary>Library: stepped base, colonnade across a recessed entrance, tall windows and a domed lantern.</summary>
        public static GameObject Library(FacilityStyle style, CampusPalette p, Transform parent)
        {
            float w = style.size.x * GridMetrics.TileSize, d = style.size.z * GridMetrics.TileSize, h = style.size.y;
            const float porch = 0.7f, base0 = 0.14f, base1 = 0.28f;
            float wallFront = -d * 0.5f + porch, bodyDepth = d - porch, bodyZ = porch * 0.5f;
            float eave = h - 0.9f;
            var kit = new Kit();
            kit.Box(p.trim!, new Vector3(0f, base0 * 0.5f, 0f), new Vector3(w + 0.14f, base0, d + 0.14f));
            kit.Box(p.trim!, new Vector3(0f, (base0 + base1) * 0.5f, 0.05f), new Vector3(w + 0.02f, base1 - base0, d - 0.1f));
            kit.Box(style.body, new Vector3(0f, base1 + (eave - base1) * 0.5f, bodyZ), new Vector3(w, eave - base1, bodyDepth));
            kit.Box(p.accent!, new Vector3(0f, base1 + 0.75f, wallFront - 0.012f), new Vector3(0.8f, 1.5f, 0.02f));
            kit.Box(p.glass!, new Vector3(0f, base1 + 2.1f, wallFront - 0.012f), new Vector3(0.8f, 0.6f, 0.02f));
            for (int i = 0; i < 6; i++)
                kit.Cylinder(p.trim!, new Vector3(-w * 0.45f + w * 0.9f * i / 5f, base1 + (eave - base1) * 0.5f, -d * 0.5f + 0.16f), 0.1f, eave - base1);
            kit.Box(p.trim!, new Vector3(0f, eave + 0.12f, 0f), new Vector3(w + 0.1f, 0.24f, d + 0.04f));
            kit.Box(p.accent!, new Vector3(0f, eave + 0.12f, -d * 0.5f - 0.03f), new Vector3(w * 0.5f, 0.12f, 0.02f));
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = -1; i <= 1; i++)
                    kit.Box(p.glass!, new Vector3(side * (w * 0.5f + 0.012f), base1 + (eave - base1) * 0.52f, bodyZ + i * bodyDepth * 0.28f), new Vector3(0.02f, (eave - base1) * 0.68f, bodyDepth * 0.14f));
                kit.Box(p.glass!, new Vector3(side * w * 0.33f, base1 + (eave - base1) * 0.52f, wallFront - 0.012f), new Vector3(w * 0.14f, (eave - base1) * 0.68f, 0.02f));
            }
            kit.Box(style.roof, new Vector3(0f, eave + 0.27f, 0f), new Vector3(w - 0.1f, 0.06f, d - 0.1f));
            kit.Cylinder(style.body, new Vector3(0f, eave + 0.55f, 0.1f), 0.8f, 0.5f);
            kit.Cylinder(p.trim!, new Vector3(0f, eave + 0.82f, 0.1f), 0.88f, 0.06f);
            kit.Sphere(p.accent!, new Vector3(0f, eave + 0.85f, 0.1f), new Vector3(1.5f, 1.1f, 1.5f));
            return kit.Build("Library", parent);
        }

        /// <summary>School bus parked on its lot, bonnet toward the path.</summary>
        public static GameObject Bus(FacilityStyle style, CampusPalette p, Transform parent)
        {
            float w = style.size.x * GridMetrics.TileSize, d = style.size.z * GridMetrics.TileSize, h = style.size.y;
            const float floor = 0.42f, wheel = 0.3f;
            float cabinFront = -d * 0.5f + 0.55f, cabinDepth = d - 0.55f, cabinZ = 0.275f, cabinH = h - floor;
            var kit = new Kit();
            kit.Box(style.roof, new Vector3(0f, floor - 0.08f, 0f), new Vector3(w * 0.92f, 0.16f, d * 0.98f));
            kit.Box(style.body, new Vector3(0f, floor + cabinH * 0.5f, cabinZ), new Vector3(w, cabinH, cabinDepth));
            kit.Box(style.body, new Vector3(0f, floor + 0.24f, -d * 0.5f + 0.28f), new Vector3(w * 0.86f, 0.48f, 0.56f));
            kit.Box(style.body, new Vector3(0f, h + 0.03f, cabinZ), new Vector3(w * 0.94f, 0.06f, cabinDepth * 0.96f));
            kit.Box(p.glass!, new Vector3(0f, floor + cabinH * 0.68f, cabinFront - 0.012f), new Vector3(w * 0.84f, cabinH * 0.42f, 0.02f));
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 5; i++)
                    kit.Box(p.glass!, new Vector3(side * (w * 0.5f + 0.012f), floor + cabinH * 0.68f, cabinFront + cabinDepth * (0.12f + 0.19f * i)), new Vector3(0.02f, cabinH * 0.36f, cabinDepth * 0.15f));
                kit.Box(style.roof, new Vector3(side * (w * 0.5f + 0.01f), floor + cabinH * 0.36f, cabinZ), new Vector3(0.02f, 0.07f, cabinDepth * 0.98f));
                foreach (float z in new[] { -d * 0.3f, d * 0.3f })
                {
                    kit.Cylinder(style.roof, new Vector3(side * w * 0.42f, wheel, z), wheel, 0.24f, Quaternion.Euler(0f, 0f, 90f));
                    kit.Cylinder(p.trim!, new Vector3(side * (w * 0.42f + 0.125f), wheel, z), wheel * 0.5f, 0.02f, Quaternion.Euler(0f, 0f, 90f));
                }
                kit.Box(p.trim!, new Vector3(side * w * 0.3f, floor + 0.28f, -d * 0.5f - 0.005f), new Vector3(0.2f, 0.14f, 0.02f));
            }
            return kit.Build("Bus", parent);
        }

        /// <summary>Canteen: low pavilion with a serving counter under a striped awning and a kitchen vent.</summary>
        public static GameObject Canteen(FacilityStyle style, CampusPalette p, Transform parent)
        {
            float w = style.size.x * GridMetrics.TileSize, d = style.size.z * GridMetrics.TileSize, h = style.size.y;
            const float plinth = 0.14f;
            float front = -d * 0.5f;
            var kit = new Kit();
            kit.Box(p.trim!, new Vector3(0f, plinth * 0.5f, 0f), new Vector3(w + 0.14f, plinth, d + 0.14f));
            kit.Box(style.body, new Vector3(0f, plinth + (h - plinth) * 0.5f, 0f), new Vector3(w, h - plinth, d));
            kit.Box(p.glass!, new Vector3(0f, plinth + 1.2f, front - 0.012f), new Vector3(w * 0.8f, 0.9f, 0.02f));
            kit.Box(p.trim!, new Vector3(0f, plinth + 0.72f, front - 0.14f), new Vector3(w * 0.84f, 0.07f, 0.3f));
            kit.Box(p.accent!, new Vector3(0f, plinth + 0.34f, front - 0.04f), new Vector3(w * 0.84f, 0.68f, 0.05f));
            const int stripes = 8;
            float stripe = w * 0.9f / stripes;
            Quaternion tilt = Quaternion.Euler(-24f, 0f, 0f);
            for (int i = 0; i < stripes; i++)
                kit.Box(i % 2 == 0 ? p.accent! : p.trim!, new Vector3(-w * 0.45f + stripe * (i + 0.5f), plinth + 1.92f, front - 0.25f), new Vector3(stripe, 0.04f, 0.56f), tilt);
            foreach (float side in new[] { -1f, 1f })
                kit.Box(p.glass!, new Vector3(side * (w * 0.5f + 0.012f), plinth + 1.2f, 0f), new Vector3(0.02f, 0.8f, d * 0.5f));
            kit.Box(p.trim!, new Vector3(0f, h + 0.03f, 0f), new Vector3(w + 0.08f, 0.06f, d + 0.08f));
            Parapet(kit, style.body, w, d, h + 0.14f, 0.18f, 0.07f);
            kit.Box(style.roof, new Vector3(0f, h + 0.07f, 0f), new Vector3(w - 0.14f, 0.03f, d - 0.14f));
            kit.Box(p.trim!, new Vector3(w * 0.2f, h + 0.3f, d * 0.1f), new Vector3(0.6f, 0.4f, 0.6f));
            kit.Box(style.roof, new Vector3(w * 0.2f, h + 0.53f, d * 0.1f), new Vector3(0.74f, 0.06f, 0.74f));
            return kit.Build("Canteen", parent);
        }

        /// <summary>Collects boxes per material and merges them into a single multi-material mesh.</summary>
        internal sealed class Kit
        {
            private readonly List<Material> materials = new List<Material>();
            private readonly List<List<CombineInstance>> parts = new List<List<CombineInstance>>();

            public void Box(Material material, Vector3 center, Vector3 size, Quaternion? rotation = null) =>
                Add(material, Primitive(PrimitiveType.Cube), Matrix4x4.TRS(center, rotation ?? Quaternion.identity, size));

            public void Cylinder(Material material, Vector3 center, float radius, float height, Quaternion? rotation = null) =>
                // The built-in cylinder is 2 units tall with a 1 unit diameter.
                Add(material, Primitive(PrimitiveType.Cylinder), Matrix4x4.TRS(center, rotation ?? Quaternion.identity, new Vector3(radius * 2f, height * 0.5f, radius * 2f)));

            public void Sphere(Material material, Vector3 center, Vector3 size) =>
                Add(material, Primitive(PrimitiveType.Sphere), Matrix4x4.TRS(center, Quaternion.identity, size));

            private void Add(Material material, Mesh mesh, Matrix4x4 matrix)
            {
                int index = materials.IndexOf(material);
                if (index < 0)
                {
                    index = materials.Count;
                    materials.Add(material);
                    parts.Add(new List<CombineInstance>());
                }
                parts[index].Add(new CombineInstance { mesh = mesh, transform = matrix });
            }

            public GameObject Build(string name, Transform parent)
            {
                var submeshes = new CombineInstance[materials.Count];
                for (int i = 0; i < materials.Count; i++)
                {
                    var merged = new Mesh { name = $"{name}_{materials[i].name}" };
                    merged.CombineMeshes(parts[i].ToArray(), mergeSubMeshes: true, useMatrices: true);
                    submeshes[i] = new CombineInstance { mesh = merged, transform = Matrix4x4.identity };
                }
                var mesh = new Mesh { name = name };
                mesh.CombineMeshes(submeshes, mergeSubMeshes: false, useMatrices: false);
                mesh.RecalculateBounds();
                foreach (CombineInstance s in submeshes) Release(s.mesh);

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
                go.AddComponent<OwnedMesh>().Mesh = mesh;
                return go;
            }

            private static void Release(Object o)
            {
                if (Application.isPlaying) Object.Destroy(o);
                else Object.DestroyImmediate(o);
            }
        }

        private static Mesh Primitive(PrimitiveType type)
        {
            if (primitives.TryGetValue(type, out Mesh? mesh) && mesh != null) return mesh;
            var temp = GameObject.CreatePrimitive(type);
            mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            primitives[type] = mesh;
            return mesh;
        }
    }
}
