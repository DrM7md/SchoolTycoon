using System.Reflection;
using SchoolDirectorTycoon.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace SchoolDirectorTycoon.EditorTools
{
    /// <summary>Generates the portrait scene for the week-by-week mode and builds it for Windows or Android.</summary>
    public static class ChronicleSceneBuilder
    {
        private const string Root = "Assets/_Project";
        public const string ScenePath = Root + "/Scenes/Chronicle.unity";

        [MenuItem("SDT/Rebuild Chronicle Scene")]
        public static void Build()
        {
            LocalizationSeed.Seed();
            var panelPath = Root + "/UI/ChroniclePanelSettings.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, panelPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/HudTheme.tss");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(540, 960);
            panel.match = 0f; // scale by width so phones of any height keep the same column
            EditorUtility.SetDirty(panel);
            EnableAdvancedText(panel);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath); // a new scene unloads saved assets
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.33f, 0.07f, 0.15f);
            cameraGo.AddComponent<UniversalAdditionalCameraData>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            var ui = new GameObject("Chronicle");
            var document = ui.AddComponent<UIDocument>();
            document.panelSettings = panel;
            if (document.panelSettings == null) throw new System.InvalidOperationException("Chronicle panel settings were not assigned");
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/Chronicle.uxml")
                ?? throw new System.InvalidOperationException("Chronicle.uxml missing");
            ui.AddComponent<LifeController>();
            ui.AddComponent<LifeDevTest>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("SDT: chronicle scene written.");
        }

        /// <summary>Arabic needs the Advanced Text Generator and its ICU data on the panel. Unity 6.0 exposes the switch
        /// only in Project Settings, so it is set through the same internal property that checkbox uses.</summary>
        private static void EnableAdvancedText(PanelSettings panel)
        {
            const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            System.Type settings = EditorType("UIToolkitProjectSettings");
            PropertyInfo enabled = settings.GetProperty("enableAdvancedText", Any)
                ?? throw new System.InvalidOperationException("enableAdvancedText not found");
            enabled.SetValue(null, true);
            AssetDatabase.SaveAssets();
            var serialized = new SerializedObject(panel);
            Object data = serialized.FindProperty("m_ICUDataAsset").objectReferenceValue;
            Debug.Log($"SDT: advanced text={enabled.GetValue(null)}, ICU={AssetDatabase.GetAssetPath(data)}");
            if (data == null) throw new System.InvalidOperationException("ICU data was not assigned to the Chronicle panel");
        }

        private static System.Type EditorType(string name)
        {
            foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "UnityEditor.UIElementsModule") continue;
                foreach (System.Type type in assembly.GetTypes())
                    if (type.Name == name) return type;
            }
            throw new System.InvalidOperationException(name + " not found");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        /// <summary><c>-executeMethod ...ChronicleSceneBuilder.Windows -buildPath &lt;dir&gt;</c></summary>
        public static void Windows() => Player(BuildTarget.StandaloneWindows64, "/SchoolChronicle.exe");

        private static void Player(BuildTarget target, string file)
        {
            string dir = "Builds/Chronicle";
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-buildPath") dir = args[i + 1];
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = dir + file,
                target = target,
                options = BuildOptions.Development,
            });
            Debug.Log($"SDT: build {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
