#nullable enable
using SchoolDirectorTycoon.Bridge;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Scene entry point: creates the session and wires the views. Holds no rules.</summary>
    [DefaultExecutionOrder(-100)] // the HUD reads Session in its own Start
    public sealed class CampusBootstrap : MonoBehaviour
    {
        [SerializeField] private uint seed = 1;
        [SerializeField] private CampusPalette? palette;
        [SerializeField] private InputActionAsset? actions;
        [SerializeField] private CameraRig? cameraRig;
        [SerializeField] private CampusGridView? grid;
        [SerializeField] private BuildingsView? buildings;
        [SerializeField] private PlacementController? placement;
        [SerializeField] private DayClock? dayClock;

        public GameSession? Session { get; private set; }
        public PlacementController? Placement => placement;
        public DayClock? Clock => dayClock;

        /// <summary>Resources name of the default actions. The scene field is optional: a scene reference to the
        /// imported .inputactions asset was observed to serialize as empty when the scene is generated in batch mode.</summary>
        public const string DefaultActions = "SchoolDirectorTycoon";

        private void Start()
        {
            if (actions == null) actions = Resources.Load<InputActionAsset>(DefaultActions);
            if (actions != null && cameraRig != null && cameraRig.ViewCamera != null) cameraRig.Configure(cameraRig.ViewCamera, actions);
            string problem = palette == null ? "No CampusPalette assigned." : palette.Validate();
            if (problem.Length > 0 || actions == null || cameraRig == null || cameraRig.ViewCamera == null
                || grid == null || buildings == null || placement == null)
            {
                string missing = actions == null ? "actions" : cameraRig == null ? "cameraRig" : cameraRig.ViewCamera == null ? "cameraRig.viewCamera"
                    : grid == null ? "grid" : buildings == null ? "buildings" : placement == null ? "placement" : "";
                Debug.LogError($"CampusBootstrap is not configured: {(problem.Length > 0 ? problem : "missing " + missing)}", this);
                enabled = false;
                return;
            }

            Session = GameSession.New(seed);
            grid.Build(palette!);
            buildings.Bind(Session, palette!);
            placement.Bind(Session, palette!, cameraRig.ViewCamera, actions);
            if (dayClock != null)
            {
                dayClock.Session = Session;
                dayClock.SetSpeed(0); // paused until the HUD speed controls exist; N ends the day manually
            }
        }
    }
}
