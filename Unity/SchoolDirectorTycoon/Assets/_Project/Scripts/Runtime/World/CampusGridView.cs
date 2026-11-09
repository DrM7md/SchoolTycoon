#nullable enable
using SchoolDirectorTycoon.Bridge;
using UnityEngine;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Draws the site from <see cref="CampusModel"/>: desert terrain, a walled and paved campus, the main
    /// walkway, planting, and a lot grid that is only visible while the player is placing a building.</summary>
    public sealed class CampusGridView : MonoBehaviour
    {
        private const float Margin = 24f;
        private const float TileInset = 0.92f;
        private const float WallOffset = 7f;
        private const float WallHeight = 1.5f;
        private const float BedWidth = 3.2f;

        private GameObject? lotGrid;

        public void SetGridVisible(bool visible)
        {
            if (lotGrid != null) lotGrid.SetActive(visible);
        }

        public static int PathRow()
        {
            for (int row = 0; row < CampusModel.Rows; row++)
                if (!CampusModel.IsBuildable(0, row)) return row;
            return CampusModel.MainPathRow;
        }

        public void Build(CampusPalette palette)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            Vector3 center = GridMetrics.Center;
            if (palette.sand != null) BuildTerrain(palette.sand);

            float pathZ = GridMetrics.CellCenter(0, PathRow()).z;
            float yardW = GridMetrics.Width + WallOffset * 2f, yardD = GridMetrics.Depth + WallOffset * 2f;

            if (palette.HasLandscape)
            {
                Quad("Yard", palette.paving!, center.x, center.z, yardW, yardD, -0.03f, 6f);
                Quad("Walkway", palette.walkway!, center.x, pathZ, GridMetrics.Width + Margin * 2f, GridMetrics.TileSize, -0.01f, 6f);
                BuildLandscape(palette, pathZ);
            }
            else
            {
                Placeholders.Box("Ground", transform, new Vector3(center.x, -0.55f, center.z), new Vector3(yardW, 1f, yardD), palette.ground);
                Placeholders.Box("MainPath", transform, new Vector3(center.x, -0.04f, pathZ), new Vector3(GridMetrics.Width + Margin * 2f, 0.1f, GridMetrics.TileSize), palette.path);
            }

            lotGrid = new GameObject("LotGrid");
            lotGrid.transform.SetParent(transform, false);
            for (int row = 0; row < CampusModel.Rows; row++)
            for (int column = 0; column < CampusModel.Columns; column++)
            {
                if (!CampusModel.IsBuildable(column, row)) continue;
                Material material = (column + row) % 2 == 0 ? palette.tileLight : palette.tileDark;
                Vector3 p = GridMetrics.CellCenter(column, row);
                GameObject tile = Placeholders.Box($"Lot {column},{row}", lotGrid.transform, new Vector3(p.x, 0.01f, p.z),
                    new Vector3(GridMetrics.TileSize * TileInset, 0.02f, GridMetrics.TileSize * TileInset), material);
                tile.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            lotGrid.SetActive(false);
        }

        private void BuildLandscape(CampusPalette p, float pathZ)
        {
            Vector3 c = GridMetrics.Center;
            float x0 = c.x - GridMetrics.Width * 0.5f - WallOffset, x1 = c.x + GridMetrics.Width * 0.5f + WallOffset;
            float z0 = c.z - GridMetrics.Depth * 0.5f - WallOffset, z1 = c.z + GridMetrics.Depth * 0.5f + WallOffset;
            float gate = GridMetrics.TileSize * 0.5f + 0.5f;
            var kit = new FacilityModels.Kit();

            float kerbLength = GridMetrics.Width + Margin * 2f;
            foreach (float side in new[] { -1f, 1f })
                kit.Box(p.curb!, new Vector3(c.x, 0.05f, pathZ + side * (GridMetrics.TileSize * 0.5f + 0.1f)), new Vector3(kerbLength, 0.14f, 0.2f));

            Wall(kit, p, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0));
            Wall(kit, p, new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1));
            foreach (float x in new[] { x0, x1 })
            {
                Wall(kit, p, new Vector3(x, 0f, z0), new Vector3(x, 0f, pathZ - gate));
                Wall(kit, p, new Vector3(x, 0f, pathZ + gate), new Vector3(x, 0f, z1));
                foreach (float side in new[] { -1f, 1f })
                {
                    kit.Box(p.trim!, new Vector3(x, 1.3f, pathZ + side * gate), new Vector3(0.7f, 2.6f, 0.7f));
                    kit.Box(p.accent!, new Vector3(x, 2.68f, pathZ + side * gate), new Vector3(0.84f, 0.16f, 0.84f));
                }
            }

            float bedInner = WallOffset * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                float z = c.z + side * (GridMetrics.Depth * 0.5f + bedInner);
                kit.Box(p.curb!, new Vector3(c.x, 0.04f, z), new Vector3(GridMetrics.Width + 0.3f, 0.1f, BedWidth + 0.3f));
                kit.Box(p.grass!, new Vector3(c.x, 0.07f, z), new Vector3(GridMetrics.Width, 0.1f, BedWidth));
                for (float x = c.x - GridMetrics.Width * 0.5f + 3f; x < c.x + GridMetrics.Width * 0.5f; x += 8f)
                    Palm(kit, p, new Vector3(x, 0.1f, z), x * 37f);
            }
            foreach (float side in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                float x = c.x + side * (GridMetrics.Width * 0.5f + bedInner);
                float z = pathZ + sz * (GridMetrics.TileSize * 0.5f + 2.4f);
                kit.Box(p.curb!, new Vector3(x, 0.04f, z), new Vector3(BedWidth + 0.3f, 0.1f, 3.3f));
                kit.Box(p.grass!, new Vector3(x, 0.07f, z), new Vector3(BedWidth, 0.1f, 3f));
                Palm(kit, p, new Vector3(x, 0.1f, z), x + z * 13f);
            }

            kit.Build("Landscape", transform);
        }

        private static void Wall(FacilityModels.Kit kit, CampusPalette p, Vector3 from, Vector3 to)
        {
            Vector3 mid = (from + to) * 0.5f;
            float length = Vector3.Distance(from, to);
            bool alongX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.z - from.z);
            Vector3 Size(float l, float h, float t) => alongX ? new Vector3(l, h, t) : new Vector3(t, h, l);
            kit.Box(p.wall!, mid + Vector3.up * WallHeight * 0.5f, Size(length, WallHeight, 0.3f));
            kit.Box(p.trim!, mid + Vector3.up * (WallHeight + 0.05f), Size(length + 0.1f, 0.1f, 0.42f));
            int piers = Mathf.Max(1, Mathf.RoundToInt(length / 6f));
            for (int i = 0; i <= piers; i++)
            {
                Vector3 at = Vector3.Lerp(from, to, i / (float)piers);
                kit.Box(p.trim!, at + Vector3.up * (WallHeight + 0.2f) * 0.5f, new Vector3(0.5f, WallHeight + 0.2f, 0.5f));
            }
        }

        private static void Palm(FacilityModels.Kit kit, CampusPalette p, Vector3 foot, float seed)
        {
            float height = 4.6f + Mathf.PerlinNoise(seed * 0.13f, 3.3f) * 1.6f;
            kit.Cylinder(p.palmTrunk!, foot + Vector3.up * height * 0.25f, 0.2f, height * 0.5f);
            kit.Cylinder(p.palmTrunk!, foot + Vector3.up * height * 0.75f, 0.15f, height * 0.5f);
            Vector3 crown = foot + Vector3.up * height;
            const int fronds = 9;
            for (int i = 0; i < fronds; i++)
            {
                float yaw = seed + i * 360f / fronds;
                Quaternion inner = Quaternion.Euler(-12f, yaw, 0f), outer = Quaternion.Euler(32f, yaw, 0f);
                Vector3 joint = crown + inner * Vector3.forward * 1.3f;
                kit.Box(p.palmLeaf!, crown + inner * Vector3.forward * 0.65f, new Vector3(0.5f, 0.04f, 1.35f), inner);
                kit.Box(p.palmLeaf!, joint + outer * Vector3.forward * 0.6f, new Vector3(0.34f, 0.04f, 1.25f), outer);
            }
        }

        /// <summary>A flat quad whose UVs follow world position, so textures keep their scale at any size.</summary>
        private void Quad(string name, Material material, float cx, float cz, float width, float depth, float y, float metresPerTile)
        {
            float x0 = cx - width * 0.5f, x1 = cx + width * 0.5f, z0 = cz - depth * 0.5f, z1 = cz + depth * 0.5f;
            var mesh = new Mesh
            {
                name = name,
                vertices = new[] { new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0) },
                uv = new[] { new Vector2(x0, z0) / metresPerTile, new Vector2(x0, z1) / metresPerTile, new Vector2(x1, z1) / metresPerTile, new Vector2(x1, z0) / metresPerTile },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            AddMesh(name, mesh, material, castShadows: false);
        }

        private void AddMesh(string name, Mesh mesh, Material material, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<OwnedMesh>().Mesh = mesh;
        }

        private const float TerrainReach = 130f, TerrainStep = 3f, DuneHeight = 7f, DuneRamp = 40f;

        /// <summary>Terrain height: below the campus ground inside the site, rising into dunes beyond it.
        /// Fixed noise offsets keep it identical every run. Presentation only; picking uses the y=0 plane.</summary>
        public static float TerrainHeight(float x, float z)
        {
            Vector3 c = GridMetrics.Center;
            float dx = Mathf.Max(0f, Mathf.Abs(x - c.x) - (GridMetrics.Width * 0.5f + Margin - 2f));
            float dz = Mathf.Max(0f, Mathf.Abs(z - c.z) - (GridMetrics.Depth * 0.5f + Margin - 2f));
            float outside = Mathf.Sqrt(dx * dx + dz * dz);
            float noise = 0.7f * Mathf.PerlinNoise(x * 0.018f + 31.7f, z * 0.03f + 11.3f) + 0.3f * Mathf.PerlinNoise(x * 0.07f + 5.1f, z * 0.07f + 77.9f);
            return -0.12f + Mathf.SmoothStep(0f, 1f, outside / DuneRamp) * noise * DuneHeight;
        }

        private void BuildTerrain(Material sand)
        {
            Vector3 c = GridMetrics.Center;
            float hx = GridMetrics.Width * 0.5f + Margin + TerrainReach, hz = GridMetrics.Depth * 0.5f + Margin + TerrainReach;
            int nx = Mathf.CeilToInt(2f * hx / TerrainStep) + 1, nz = Mathf.CeilToInt(2f * hz / TerrainStep) + 1;
            var vertices = new Vector3[nx * nz];
            var uv = new Vector2[nx * nz];
            var triangles = new int[(nx - 1) * (nz - 1) * 6];
            for (int j = 0, t = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = c.x - hx + i * TerrainStep, z = c.z - hz + j * TerrainStep;
                vertices[j * nx + i] = new Vector3(x, TerrainHeight(x, z), z);
                uv[j * nx + i] = new Vector2(x, z) / 14f;
                if (i == nx - 1 || j == nz - 1) continue;
                int a = j * nx + i, b = a + nx;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
            }
            var mesh = new Mesh { name = "Terrain", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AddMesh("Terrain", mesh, sand, castShadows: false);
        }
    }
}
