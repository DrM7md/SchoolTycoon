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
