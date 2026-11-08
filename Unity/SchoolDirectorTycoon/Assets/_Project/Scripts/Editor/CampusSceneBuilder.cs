using System.IO;
using SchoolDirectorTycoon.Bridge;
using SchoolDirectorTycoon.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SchoolDirectorTycoon.EditorTools
{
    /// <summary>Generates the greybox campus scene, its materials and palette, and the URP asset,
    /// so the scene is reproducible from code. Run from the menu or with
    /// <c>-executeMethod SchoolDirectorTycoon.EditorTools.CampusSceneBuilder.BuildFromCommandLine</c>.</summary>
    public static class CampusSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string MaterialsDir = Root + "/Art/Materials";
        private const string SettingsDir = Root + "/Settings";
        private const string ScenePath = Root + "/Scenes/Campus.unity";
        private const string PalettePath = SettingsDir + "/CampusPalette.asset";
        private const string ActionsPath = SettingsDir + "/SchoolDirectorTycoon.inputactions";

        [MenuItem("SDT/Rebuild Campus Scene")]
        public static void Build()
        {
            EnsurePipeline();
            CampusPalette palette = EnsurePalette();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.60f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.30f, 0.26f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, 135f, 0f);

            var rigGo = new GameObject("CameraRig");
            var rig = rigGo.AddComponent<CameraRig>();
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(rigGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.68f, 0.80f, 0.92f);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            SetRef(rig, "viewCamera", cam);
            SetRef(rig, "actions", actions);

            var world = new GameObject("Campus");
            var grid = new GameObject("Grid").AddComponent<CampusGridView>();
            grid.transform.SetParent(world.transform, false);
            var buildings = new GameObject("Buildings").AddComponent<BuildingsView>();
            buildings.transform.SetParent(world.transform, false);
            var placement = new GameObject("Placement").AddComponent<PlacementController>();
            placement.transform.SetParent(world.transform, false);

            var session = new GameObject("Session");
            var clock = session.AddComponent<DayClock>();
            var boot = session.AddComponent<CampusBootstrap>();
            SetRef(boot, "palette", palette);
            SetRef(boot, "actions", actions);
            SetRef(boot, "cameraRig", rig);
            SetRef(boot, "grid", grid);
            SetRef(boot, "buildings", buildings);
            SetRef(boot, "placement", placement);
            SetRef(boot, "dayClock", clock);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"SDT: campus scene written to {ScenePath}");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field)
                ?? throw new System.InvalidOperationException($"{target.GetType().Name}.{field} not found");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsurePipeline()
        {
            string path = SettingsDir + "/URP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, SettingsDir + "/URP_Renderer.asset");
                asset = UniversalRenderPipelineAsset.Create(renderer);
                asset.shadowDistance = 150f;
                asset.msaaSampleCount = 4;
                AssetDatabase.CreateAsset(asset, path);
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
        }

        private static CampusPalette EnsurePalette()
        {
            Directory.CreateDirectory(MaterialsDir);
            var palette = AssetDatabase.LoadAssetAtPath<CampusPalette>(PalettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<CampusPalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }

            palette.ground = Mat("M_Ground", new Color(0.47f, 0.62f, 0.36f));
            palette.tileLight = Mat("M_LotLight", new Color(0.58f, 0.72f, 0.45f));
            palette.tileDark = Mat("M_LotDark", new Color(0.53f, 0.68f, 0.41f));
            palette.path = Mat("M_Path", new Color(0.86f, 0.80f, 0.66f));
            palette.ghostValid = Mat("M_GhostValid", new Color(0.35f, 0.85f, 0.55f, 0.5f), transparent: true);
            palette.ghostInvalid = Mat("M_GhostInvalid", new Color(0.95f, 0.30f, 0.28f, 0.55f), transparent: true);

            Material roof = Mat("M_Roof", new Color(0.36f, 0.33f, 0.31f));
            palette.facilities = new FacilityStyle[CampusModel.FacilityCount];
            palette.facilities[(int)FacilityKind.Classroom] = Style(Mat("M_Classroom", new Color(0.93f, 0.89f, 0.80f)), roof, 0.80f, 3.2f, 0.80f);
            palette.facilities[(int)FacilityKind.Lab] = Style(Mat("M_Lab", new Color(0.70f, 0.84f, 0.93f)), roof, 0.80f, 4.0f, 0.80f);
            palette.facilities[(int)FacilityKind.Library] = Style(Mat("M_Library", new Color(0.78f, 0.55f, 0.40f)), roof, 0.85f, 5.0f, 0.85f);
            palette.facilities[(int)FacilityKind.Bus] = Style(Mat("M_Bus", new Color(0.98f, 0.78f, 0.20f)), roof, 0.45f, 1.8f, 0.80f);
            palette.facilities[(int)FacilityKind.Canteen] = Style(Mat("M_Canteen", new Color(0.90f, 0.62f, 0.55f)), roof, 0.85f, 2.6f, 0.70f);

            EditorUtility.SetDirty(palette);
            string problem = palette.Validate();
            if (problem.Length > 0) throw new System.InvalidOperationException("Palette invalid: " + problem);
            return palette;
        }

        private static FacilityStyle Style(Material body, Material roof, float x, float height, float z) =>
            new FacilityStyle { body = body, roof = roof, size = new Vector3(x, height, z) };

        private static Material Mat(string name, Color color, bool transparent = false)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
