using SchoolDirectorTycoon.Bridge;
using SchoolDirectorTycoon.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SchoolDirectorTycoon.EditorTools
{
    /// <summary>Renders the campus scene with a sample layout to a PNG, for docs and visual review
    /// without entering Play mode. <c>-executeMethod SchoolDirectorTycoon.EditorTools.CampusScreenshot.Capture -shot &lt;path&gt;</c>.</summary>
    public static class CampusScreenshot
    {
        public static void Capture()
        {
            string path = "campus.png";
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-shot") path = args[i + 1];
            try
            {
                Render(path, 1600, 900);
                EditorApplication.Exit(0);
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        public static void Render(string path, int width, int height)
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Campus.unity");
            var palette = AssetDatabase.LoadAssetAtPath<CampusPalette>("Assets/_Project/Settings/CampusPalette.asset");
            Object.FindFirstObjectByType<CampusGridView>().Build(palette);

            var session = GameSession.New(1);
            (FacilityKind kind, int c, int r)[] sample =
            {
                (FacilityKind.Library, 6, 7), (FacilityKind.Lab, 8, 7), (FacilityKind.Canteen, 6, 3),
                (FacilityKind.Bus, 1, 4), (FacilityKind.Bus, 2, 4), (FacilityKind.Classroom, 10, 6),
            };
            foreach (var (kind, c, r) in sample)
                if (!CampusModel.IsOccupied(session, c, r)) CampusModel.Build(session, kind, c, r);
            var view = Object.FindFirstObjectByType<BuildingsView>();
            view.Bind(session, palette);

            Camera cam = Camera.main;
            Transform rig = cam.transform.parent;
            rig.position = GridMetrics.Center;
            rig.rotation = Quaternion.Euler(42f, 35f, 0f);
            cam.transform.localPosition = new Vector3(0f, 0f, -62f);

            // Batch mode: compile shader variants synchronously so the first frame is not placeholder-shaded.
            EditorSettings.asyncShaderCompilation = false;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Debug.Log($"SDT: screenshot written to {path} ({view.Count} buildings)");
        }
    }
}
