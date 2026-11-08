using SchoolDirectorTycoon.Bridge;
using UnityEngine;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Draws the ground, the 14×12 lot grid and the reserved main path from <see cref="CampusModel"/>.</summary>
    public sealed class CampusGridView : MonoBehaviour
    {
        private const float Margin = 24f;
        private const float TileInset = 0.92f;

        public void Build(CampusPalette palette)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            Vector3 center = GridMetrics.Center;
            Placeholders.Box("Ground", transform, new Vector3(center.x, -0.55f, center.z),
                new Vector3(GridMetrics.Width + Margin * 2f, 1f, GridMetrics.Depth + Margin * 2f), palette.ground);

            var tiles = new GameObject("Tiles").transform;
            tiles.SetParent(transform, false);
            for (int row = 0; row < CampusModel.Rows; row++)
            {
                bool isPath = !CampusModel.IsBuildable(0, row);
                if (isPath)
                {
                    // One continuous strip reads as a path better than separate tiles.
                    Vector3 c = GridMetrics.CellCenter(0, row);
                    Placeholders.Box("MainPath", tiles, new Vector3(center.x, -0.04f, c.z),
                        new Vector3(GridMetrics.Width + Margin * 2f, 0.1f, GridMetrics.TileSize), palette.path);
                    continue;
                }
                for (int column = 0; column < CampusModel.Columns; column++)
                {
                    Material material = (column + row) % 2 == 0 ? palette.tileLight : palette.tileDark;
                    Vector3 p = GridMetrics.CellCenter(column, row);
                    Placeholders.Box($"Lot {column},{row}", tiles, new Vector3(p.x, -0.05f, p.z),
                        new Vector3(GridMetrics.TileSize * TileInset, 0.1f, GridMetrics.TileSize * TileInset), material);
                }
            }
        }
    }
}
