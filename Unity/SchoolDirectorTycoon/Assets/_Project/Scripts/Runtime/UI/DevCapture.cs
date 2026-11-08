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
            string? path = Argument("-sdtShot");
            if (path == null || !Debug.isDebugBuild || bootstrap == null) yield break;

            var hud = GetComponent<HudController>();
            for (int i = 0; i < 300 && (bootstrap.Session == null || (hud != null && !hud.IsBound)); i++) yield return null;
            GameSession? session = bootstrap.Session;
            if (session == null) { Application.Quit(2); yield break; }

            CampusModel.Build(session, FacilityKind.Library, 6, 7);
            CampusModel.Build(session, FacilityKind.Lab, 8, 7);
            CampusModel.Build(session, FacilityKind.Canteen, 6, 3);
            if (HudSnapshot.From(session).Incident == null) CampusModel.EndDay(session);
            bootstrap.Placement?.Select(FacilityKind.Classroom);
            CampusModel.Build(session, FacilityKind.Bus, 3, CampusModel.MainPathRow); // rejected → toast

            for (int i = 0; i < 40; i++) yield return null; // let the toast transition and layout settle
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 5; i++) yield return null;
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
