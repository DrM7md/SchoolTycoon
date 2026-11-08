#nullable enable
using SchoolDirectorTycoon.Bridge;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Scene entry point: creates the session and wires the views. Holds no rules.</summary>
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

        private void Start()
        {
            string problem = palette == null ? "No CampusPalette assigned." : palette.Validate();
            if (problem.Length > 0 || actions == null || cameraRig == null || cameraRig.ViewCamera == null
                || grid == null || buildings == null || placement == null)
            {
                Debug.LogError($"CampusBootstrap is not configured: {(problem.Length > 0 ? problem : "missing reference")}", this);
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
