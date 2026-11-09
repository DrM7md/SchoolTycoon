using System.IO;
using UnityEditor;
using UnityEngine;

namespace SchoolDirectorTycoon.EditorTools
{
    /// <summary>Generates tiling ground textures from code with a fixed seed, so the look is reproducible
    /// and needs no third-party art.</summary>
    internal static class CampusTextures
    {
        private const int Size = 512;

        public static Texture2D Pavers(string path, Color light, Color dark, Color joint, int rows, float seed) =>
            Save(path, (u, v) =>
            {
                float row = v * rows;
                int r = Mathf.FloorToInt(row);
                int columns = rows / 2;
                float cell = Frac(u + (r % 2 == 0 ? 0f : 0.5f / columns)) * columns;
                int c = Mathf.FloorToInt(cell);
                float fx = cell - c, fy = row - r;
                const float gap = 0.05f;
                if (fx < gap * 0.5f || fx > 1f - gap * 0.5f || fy < gap || fy > 1f - gap) return joint;
                float tone = Hash(c, r, columns, rows, seed);
                float grain = Mathf.PerlinNoise(u * 48f + seed, v * 48f) * 0.12f - 0.06f;
                Color color = Color.Lerp(dark, light, tone);
                return new Color(color.r + grain, color.g + grain, color.b + grain, 1f);
            });

        public static Texture2D Sand(string path, Color light, Color dark, float seed) =>
            Save(path, (u, v) =>
            {
                float n = 0.6f * Periodic(u, v, 6, seed) + 0.28f * Periodic(u, v, 18, seed + 9f) + 0.12f * Periodic(u, v, 70, seed + 21f);
                return Color.Lerp(dark, light, Mathf.Clamp01(n * 1.4f - 0.2f));
            });

        private static float Periodic(float u, float v, int cells, float seed)
        {
            // Blend across the seam so the texture tiles.
            float a = Mathf.PerlinNoise(u * cells + seed, v * cells + seed);
            float b = Mathf.PerlinNoise((u - 1f) * cells + seed, v * cells + seed);
            float c = Mathf.PerlinNoise(u * cells + seed, (v - 1f) * cells + seed);
            float d = Mathf.PerlinNoise((u - 1f) * cells + seed, (v - 1f) * cells + seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static float Frac(float x) => x - Mathf.Floor(x);

        private static float Hash(int c, int r, int columns, int rows, float seed)
        {
            int wrappedC = ((c % columns) + columns) % columns, wrappedR = ((r % rows) + rows) % rows;
            return Frac(Mathf.Sin(wrappedC * 12.9898f + wrappedR * 78.233f + seed) * 43758.547f);
        }

        private static Texture2D Save(string path, System.Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGB24, true);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = pixel((x + 0.5f) / Size, (y + 0.5f) / Size);
            texture.SetPixels(pixels);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
