using System;
using NUnit.Framework;
using SchoolDirectorTycoon.Bridge;
using SchoolDirectorTycoon.Core;
using SchoolDirectorTycoon.World;
using UnityEngine;

namespace SchoolDirectorTycoon.Tests
{
    public sealed class CampusWorldTests
    {
        private sealed class SilentLogger : IBridgeLogger
        {
            public void LogException(Exception error) { }
        }

        [Test]
        public void FacilityKindMirrorsCoreFacility()
        {
            Assert.AreEqual(Enum.GetNames(typeof(Facility)), Enum.GetNames(typeof(FacilityKind)));
            foreach (Facility facility in Enum.GetValues(typeof(Facility)))
                Assert.AreEqual(facility.ToString(), ((FacilityKind)(int)facility).ToString());
            Assert.AreEqual(Enum.GetValues(typeof(Facility)).Length, CampusModel.FacilityCount);
        }

        [Test]
        public void EveryCellRoundTripsThroughWorldSpace()
        {
            for (int row = 0; row < CampusModel.Rows; row++)
            for (int column = 0; column < CampusModel.Columns; column++)
            {
                Assert.IsTrue(GridMetrics.TryWorldToCell(GridMetrics.CellCenter(column, row), out int c, out int r));
                Assert.AreEqual((column, row), (c, r));
            }
            Assert.IsFalse(GridMetrics.TryWorldToCell(new Vector3(-0.1f, 0f, 1f), out _, out _));
            Assert.IsFalse(GridMetrics.TryWorldToCell(new Vector3(GridMetrics.Width + 0.1f, 0f, 1f), out _, out _));
        }

        [Test]
        public void DownwardRayHitsTheCellBelow()
        {
            Vector3 above = GridMetrics.CellCenter(3, 8) + Vector3.up * 30f;
            Assert.IsTrue(GridMetrics.TryRayToCell(new Ray(above, Vector3.down), out int c, out int r));
            Assert.AreEqual((3, 8), (c, r));
            Assert.IsFalse(GridMetrics.TryRayToCell(new Ray(above, Vector3.up), out _, out _));
        }

        [Test]
        public void BuildOnPathIsRejectedWithACodeAndChangesNothing()
        {
            var session = GameSession.New(7, new SilentLogger());
            int before = session.State.Buildings.Length;
            string code = null;
            using (CampusModel.OnRejected(session, c => code = c))
                Assert.IsFalse(CampusModel.Build(session, FacilityKind.Classroom, 2, CampusModel.MainPathRow));
            Assert.IsFalse(string.IsNullOrEmpty(code));
            Assert.AreEqual(before, session.State.Buildings.Length);
        }

        [Test]
        public void BuildingsViewFollowsSessionBuilds()
        {
            var session = GameSession.New(7, new SilentLogger());
            var palette = TestPalette();
            var go = new GameObject("Buildings");
            try
            {
                var view = go.AddComponent<BuildingsView>();
                view.Bind(session, palette);
                Assert.AreEqual(session.State.Buildings.Length, view.Count);

                int column = FindFreeLot(session, out int row);
                int changes = 0;
                using (CampusModel.OnChanged(session, () => changes++))
                    Assert.IsTrue(CampusModel.Build(session, FacilityKind.Library, column, row));
                Assert.AreEqual(1, changes);
                Assert.AreEqual(session.State.Buildings.Length, view.Count);
                Assert.IsTrue(CampusModel.IsOccupied(session, column, row));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(palette);
            }
        }

        [Test]
        public void EveryFacilityIsOneMergedRendererInsideItsLot()
        {
            var palette = TestPalette();
            var parent = new GameObject("Parent");
            try
            {
                GameObject greybox = FacilityModels.Create(FacilityKind.Bus, palette, parent.transform);
                Assert.IsNull(greybox.GetComponent<MeshFilter>(), "without detail materials the greybox is used");

                palette.glass = new Material(palette.ground);
                palette.trim = new Material(palette.ground);
                palette.accent = new Material(palette.ground);
                var shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<CampusPalette>("Assets/_Project/Settings/CampusPalette.asset");
                if (shipped == null) Assert.Ignore("Run SDT/Rebuild Campus Scene first.");
                for (int i = 0; i < palette.facilities.Length; i++) palette.facilities[i].size = shipped.facilities[i].size;
                foreach (FacilityKind kind in Enum.GetValues(typeof(FacilityKind)))
                {
                    GameObject model = FacilityModels.Create(kind, palette, parent.transform);
                    MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>();
                    Assert.AreEqual(1, renderers.Length, kind.ToString());
                    Mesh mesh = model.GetComponent<MeshFilter>().sharedMesh;
                    Assert.AreEqual(renderers[0].sharedMaterials.Length, mesh.subMeshCount, kind.ToString());
                    Assert.LessOrEqual(mesh.subMeshCount, 5, kind.ToString());
                    Bounds b = mesh.bounds;
                    Assert.AreEqual(0f, b.min.y, 0.001f, kind + " pivot at ground");
                    Assert.Less(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), GridMetrics.TileSize * 0.5f, kind + " width");
                    Assert.Less(Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)), GridMetrics.TileSize * 0.5f, kind + " depth");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(palette);
            }
        }

        [Test]
        public void TerrainStaysBelowCampusAndRisesBeyondIt()
        {
            for (int row = 0; row < CampusModel.Rows; row++)
            for (int column = 0; column < CampusModel.Columns; column++)
            {
                Vector3 p = GridMetrics.CellCenter(column, row);
                Assert.Less(CampusGridView.TerrainHeight(p.x, p.z), -0.05f);
            }
            Vector3 c = GridMetrics.Center;
            Assert.Greater(CampusGridView.TerrainHeight(c.x + GridMetrics.Width + 90f, c.z), 0.5f);
        }

        [Test]
        public void GeneratedPaletteAssetIsValid()
        {
#if UNITY_EDITOR
            var palette = UnityEditor.AssetDatabase.LoadAssetAtPath<CampusPalette>("Assets/_Project/Settings/CampusPalette.asset");
            if (palette == null) Assert.Ignore("Run SDT/Rebuild Campus Scene first.");
            Assert.AreEqual(string.Empty, palette.Validate());
#endif
        }

        private static int FindFreeLot(GameSession session, out int row)
        {
            for (row = 0; row < CampusModel.Rows; row++)
            for (int column = 0; column < CampusModel.Columns; column++)
                if (CampusModel.IsBuildable(column, row) && !CampusModel.IsOccupied(session, column, row))
                    return column;
            throw new InvalidOperationException("No free lot.");
        }

        private static CampusPalette TestPalette()
        {
            var palette = ScriptableObject.CreateInstance<CampusPalette>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            palette.ground = palette.tileLight = palette.tileDark = palette.path = material;
            palette.ghostValid = palette.ghostInvalid = material;
            for (int i = 0; i < palette.facilities.Length; i++)
                palette.facilities[i] = new FacilityStyle { body = material, roof = material, size = new Vector3(0.8f, 3f, 0.8f) };
            Assert.AreEqual(string.Empty, palette.Validate());
            return palette;
        }
    }
}
