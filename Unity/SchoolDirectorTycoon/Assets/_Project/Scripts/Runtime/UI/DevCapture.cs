#nullable enable
using System.Collections;
using SchoolDirectorTycoon.Bridge;
using SchoolDirectorTycoon.World;
using UnityEngine;

namespace SchoolDirectorTycoon.UI
{
    /// <summary>Development builds only: <c>-sdtShot &lt;file.png&gt;</c> plays a short scripted sequence
    /// (builds, a rejection, a selection), saves a screenshot and quits. Used for visual review and CI smoke tests.</summary>
    public sealed class DevCapture : MonoBehaviour
    {
        [SerializeField] private CampusBootstrap? bootstrap;

        private IEnumerator Start()
        {
            string? play = Argument("-sdtPlayTest");
            if (play != null && Debug.isDebugBuild && bootstrap != null)
            {
                yield return DevPlayTest.Run(bootstrap, GetComponent<HudController>(), GetComponent<UnityEngine.UIElements.UIDocument>(), play);
                yield break;
            }

            string? perf = Argument("-sdtPerf");
            if (perf != null && Debug.isDebugBuild && bootstrap != null)
            {
                yield return Measure(perf);
                yield break;
            }

            string? path = Argument("-sdtShot");
            if (path == null || !Debug.isDebugBuild || bootstrap == null) yield break;

            var hud = GetComponent<HudController>();
            for (int i = 0; i < 300 && (bootstrap.Session == null || (hud != null && !hud.IsBound)); i++) yield return null;
            GameSession? session = bootstrap.Session;
            if (session == null) { Application.Quit(2); yield break; }

            CampusModel.Build(session, FacilityKind.Library, 2, 3);
            CampusModel.Build(session, FacilityKind.Lab, 3, 3);
            CampusModel.Build(session, FacilityKind.Canteen, 4, 3);
            CampusModel.Build(session, FacilityKind.Bus, 5, 3);
            CampusModel.Build(session, FacilityKind.Classroom, 6, 3);
            if (HudSnapshot.From(session).Incident == null) CampusModel.EndDay(session);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sdtClean") < 0)
            {
                bootstrap.Placement?.Select(FacilityKind.Classroom);
                CampusModel.Build(session, FacilityKind.Bus, 3, CampusModel.MainPathRow); // rejected → toast
            }

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sdtCloseUp") >= 0 && Camera.main != null)
            {
                // Look at the classroom entrance from the path side.
                Camera cam = Camera.main;
                var rig = cam.GetComponentInParent<CameraRig>();
                if (rig != null) rig.enabled = false;
                Vector3 target = GridMetrics.CellCenter(4, 3) + Vector3.up * 1.6f;
                cam.transform.position = target + new Vector3(2f, 8f, 17f);
                cam.transform.LookAt(target);
            }

            for (int i = 0; i < 40; i++) yield return null; // let the toast transition and layout settle
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 5; i++) yield return null;
            Application.Quit(0);
        }

        /// <summary>Fills every free lot with detailed models (views only, no session change) and logs frame times.</summary>
        private IEnumerator Measure(string file)
        {
            var hud = GetComponent<HudController>();
            for (int i = 0; i < 300 && (bootstrap!.Session == null || (hud != null && !hud.IsBound)); i++) yield return null;
            GameSession? session = bootstrap!.Session;
            if (session == null || bootstrap.Palette == null) { Application.Quit(2); yield break; }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var stress = new GameObject("PerfStress").transform;
            int count = 0;
            for (int row = 0; row < CampusModel.Rows; row++)
            for (int column = 0; column < CampusModel.Columns; column++)
            {
                if (!CampusModel.IsBuildable(column, row) || CampusModel.IsOccupied(session, column, row)) continue;
                GameObject model = FacilityModels.Create((FacilityKind)(count++ % CampusModel.FacilityCount), bootstrap.Palette, stress);
                model.transform.localPosition = GridMetrics.CellCenter(column, row);
            }

            for (int i = 0; i < 120; i++) yield return null;
            const int frames = 1200;
            var ms = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                ms[i] = Time.unscaledDeltaTime * 1000f;
            }
            System.Array.Sort(ms);
            float total = 0f;
            foreach (float m in ms) total += m;
            float average = total / frames;
            System.IO.File.WriteAllText(file, $"gpu={SystemInfo.graphicsDeviceName}\nresolution={Screen.width}x{Screen.height}\nmodels={count}\nframes={frames}\navg_ms={average:F2}\navg_fps={1000f / average:F0}\np50_ms={ms[frames / 2]:F2}\np99_ms={ms[frames * 99 / 100]:F2}\nworst_ms={ms[frames - 1]:F2}\n");
            Application.Quit(0);
        }

        private static string? Argument(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
