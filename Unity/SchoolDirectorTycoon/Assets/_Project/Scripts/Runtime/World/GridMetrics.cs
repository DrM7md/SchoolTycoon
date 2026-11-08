using SchoolDirectorTycoon.Bridge;
using UnityEngine;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Maps campus cells to world space. Column → +X, row → +Z, origin at the grid corner.</summary>
    public static class GridMetrics
    {
        public const float TileSize = 4f;

        public static float Width => CampusModel.Columns * TileSize;
        public static float Depth => CampusModel.Rows * TileSize;
        public static Vector3 Center => new Vector3(Width * 0.5f, 0f, Depth * 0.5f);

        public static Vector3 CellCenter(int column, int row) =>
            new Vector3((column + 0.5f) * TileSize, 0f, (row + 0.5f) * TileSize);

        public static bool TryWorldToCell(Vector3 position, out int column, out int row)
        {
            column = Mathf.FloorToInt(position.x / TileSize);
            row = Mathf.FloorToInt(position.z / TileSize);
            return column >= 0 && column < CampusModel.Columns && row >= 0 && row < CampusModel.Rows;
        }

        /// <summary>Intersects a screen ray with the ground plane (y = 0).</summary>
        public static bool TryRayToCell(Ray ray, out int column, out int row)
        {
            column = row = -1;
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float distance)) return false;
            return TryWorldToCell(ray.GetPoint(distance), out column, out row);
        }
    }
}
