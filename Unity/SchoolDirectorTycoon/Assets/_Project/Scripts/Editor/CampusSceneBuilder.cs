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
        private const string ActionsPath = Root + "/Resources/SchoolDirectorTycoon.inputactions";

        [MenuItem("SDT/Rebuild Campus Scene")]
        public static void Build()
        {
            // Seeding creates assets and can trigger reimports that invalidate objects loaded earlier,
            // so it runs before anything is loaded for the scene.
            LocalizationSeed.Seed();
            EnsurePipeline();
            CampusPalette palette = EnsurePalette();
            AssetDatabase.ImportAsset(ActionsPath, ImportAssetOptions.ForceSynchronousImport);
            var actions = AssetDatabase.LoadMainAssetAtPath(ActionsPath) as InputActionAsset
                ?? throw new System.InvalidOperationException("Input actions asset not found: " + ActionsPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.62f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.47f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.31f, 0.25f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.80f);
            sun.intensity = 1.0f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, 120f, 0f);

            var rigGo = new GameObject("CameraRig");
            var rig = rigGo.AddComponent<CameraRig>();
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(rigGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.66f, 0.79f, 0.90f);
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            EnsurePostProcessing();
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

            // UI Toolkit receives pointer events from the Input System through this module.
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            var hudGo = new GameObject("HUD");
            var document = hudGo.AddComponent<UnityEngine.UIElements.UIDocument>();
            document.panelSettings = EnsurePanelSettings();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(UiDir + "/Hud.uxml")
                ?? throw new System.InvalidOperationException("Hud.uxml missing");
            var hud = hudGo.AddComponent<SchoolDirectorTycoon.UI.HudController>();
            SetRef(hud, "bootstrap", boot);
            SetRef(hud, "palette", palette);
            SetRef(hudGo.AddComponent<SchoolDirectorTycoon.UI.DevCapture>(), "bootstrap", boot);

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
            if (value != null && property.objectReferenceValue != value)
                throw new System.InvalidOperationException($"{target.GetType().Name}.{field} did not keep {value.name} (persistent={EditorUtility.IsPersistent(value)}, id={value.GetInstanceID()})");
        }

        private const string UiDir = Root + "/UI";

        private static UnityEngine.UIElements.PanelSettings EnsurePanelSettings()
        {
            string path = UiDir + "/HudPanelSettings.asset";
            var panel = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(path);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
                AssetDatabase.CreateAsset(panel, path);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>(UiDir + "/HudTheme.tss");
            panel.scaleMode = UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            return panel;
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
                asset.msaaSampleCount = 4;
                AssetDatabase.CreateAsset(asset, path);
            }
            // Just past the farthest camera zoom, so the shadow map is not spread over empty distance.
            asset.shadowDistance = 120f;
            asset.shadowCascadeCount = 3;
            asset.mainLightShadowmapResolution = 4096;
            EditorUtility.SetDirty(asset);
            EnsureAmbientOcclusion();
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

            const string tex = Root + "/Art/Textures/";
            palette.ground = Mat("M_Ground", new Color(0.78f, 0.70f, 0.57f));
            palette.tileLight = Mat("M_LotLight", new Color(0.30f, 0.80f, 0.62f, 0.30f), transparent: true);
            palette.tileDark = Mat("M_LotDark", new Color(0.30f, 0.80f, 0.62f, 0.20f), transparent: true);
            palette.path = Mat("M_Path", new Color(0.62f, 0.57f, 0.50f));
            palette.ghostValid = Mat("M_GhostValid", new Color(0.35f, 0.85f, 0.55f, 0.5f), transparent: true);
            palette.ghostInvalid = Mat("M_GhostInvalid", new Color(0.95f, 0.30f, 0.28f, 0.55f), transparent: true);

            palette.sand = Textured(Mat("M_Sand", Color.white), CampusTextures.Sand(tex + "T_Sand.png", new Color(0.72f, 0.62f, 0.47f), new Color(0.60f, 0.50f, 0.37f), 13f));
            palette.paving = Textured(Mat("M_Paving", Color.white), CampusTextures.Pavers(tex + "T_Paving.png", new Color(0.66f, 0.63f, 0.57f), new Color(0.58f, 0.55f, 0.49f), new Color(0.44f, 0.41f, 0.37f), 16, 3f));
            palette.walkway = Textured(Mat("M_Walkway", Color.white), CampusTextures.Pavers(tex + "T_Walkway.png", new Color(0.58f, 0.46f, 0.42f), new Color(0.46f, 0.36f, 0.33f), new Color(0.34f, 0.28f, 0.26f), 12, 7f));
            palette.curb = Mat("M_Curb", new Color(0.86f, 0.84f, 0.80f));
            palette.wall = Mat("M_Wall", new Color(0.80f, 0.72f, 0.60f));
            palette.grass = Mat("M_Grass", new Color(0.36f, 0.45f, 0.24f));
            palette.palmTrunk = Mat("M_PalmTrunk", new Color(0.42f, 0.33f, 0.24f));
            palette.palmLeaf = Mat("M_PalmLeaf", new Color(0.27f, 0.42f, 0.20f));
            palette.glass = Mat("M_Glass", new Color(0.15f, 0.25f, 0.31f), smoothness: 0.9f);
            palette.trim = Mat("M_Trim", new Color(0.93f, 0.91f, 0.86f));
            palette.accent = Mat("M_Accent", new Color(0.45f, 0.09f, 0.19f)); // Qatari maroon

            // One campus identity: related stone tones, with form rather than paint telling the buildings apart.
            Material roof = Mat("M_Roof", new Color(0.52f, 0.49f, 0.45f));
            palette.facilities = new FacilityStyle[CampusModel.FacilityCount];
            palette.facilities[(int)FacilityKind.Classroom] = Style(Mat("M_Classroom", new Color(0.72f, 0.62f, 0.48f)), roof, 0.86f, 3.4f, 0.72f);
            palette.facilities[(int)FacilityKind.Lab] = Style(Mat("M_Lab", new Color(0.78f, 0.74f, 0.66f)), roof, 0.80f, 4.0f, 0.80f);
            palette.facilities[(int)FacilityKind.Library] = Style(Mat("M_Library", new Color(0.62f, 0.50f, 0.38f)), roof, 0.85f, 5.0f, 0.85f);
            palette.facilities[(int)FacilityKind.Bus] = Style(Mat("M_Bus", new Color(0.93f, 0.70f, 0.16f)), Mat("M_BusDark", new Color(0.16f, 0.16f, 0.17f)), 0.45f, 1.8f, 0.80f);
            palette.facilities[(int)FacilityKind.Canteen] = Style(Mat("M_Canteen", new Color(0.76f, 0.67f, 0.55f)), roof, 0.85f, 2.6f, 0.70f);

            EditorUtility.SetDirty(palette);
            string problem = palette.Validate();
            if (problem.Length > 0) throw new System.InvalidOperationException("Palette invalid: " + problem);
            return palette;
        }

        private static Material Textured(Material material, Texture2D texture)
        {
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsurePostProcessing()
        {
            string path = SettingsDir + "/CampusVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            if (!profile.TryGet(out Tonemapping tone)) tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            if (!profile.TryGet(out ColorAdjustments color)) color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(-0.1f);
            color.contrast.Override(10f);
            color.saturation.Override(0f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.92f));
            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.15f);
            bloom.intensity.Override(0.25f);
            if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.16f);
            foreach (VolumeComponent component in profile.components) EditorUtility.SetDirty(component);
            foreach (VolumeComponent component in profile.components)
                if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);

            var go = new GameObject("PostProcessing");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        /// <summary>Contact shadows ground the buildings. The URP feature type is not public, so it is added by name.</summary>
        private static void EnsureAmbientOcclusion()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SettingsDir + "/URP_Renderer.asset");
            var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            if (data == null || type == null) { Debug.LogWarning("SDT: SSAO not added."); return; }
            foreach (ScriptableRendererFeature existing in data.rendererFeatures)
                if (existing != null && existing.GetType() == type) return;
            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            feature.name = "SSAO";
            feature.SetActive(true);
            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long id);
            var so = new SerializedObject(data);
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            map.InsertArrayElementAtIndex(map.arraySize);
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        private static FacilityStyle Style(Material body, Material roof, float x, float height, float z) =>
            new FacilityStyle { body = body, roof = roof, size = new Vector3(x, height, z) };

        private static Material Mat(string name, Color color, bool transparent = false, float smoothness = 0.15f)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
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
