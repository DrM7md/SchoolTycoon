#nullable enable
using System;
using SchoolDirectorTycoon.Bridge;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Build mode: keys 1–5 pick a facility, a ghost follows the pointer over the grid,
    /// click asks Core to build. All validity comes from Core; the ghost colour is only a preview.
    /// A rejection shakes the ghost red (the game has no audio, so feedback is visual).</summary>
    public sealed class PlacementController : MonoBehaviour
    {
        private const float ShakeSeconds = 0.35f;

        [SerializeField] private InputActionAsset? actions;

        private GameSession? session;
        private CampusPalette? palette;
        private Camera? viewCamera;
        private InputAction? point, select, cancel, endDay;
        private readonly InputAction?[] facilityKeys = new InputAction?[CampusModel.FacilityCount];
        private IDisposable? rejected;
        private GameObject? ghost;
        private Renderer[] ghostRenderers = Array.Empty<Renderer>();
        private float shake;
        private int column = -1, row = -1;

        public FacilityKind? Selected { get; private set; }

        /// <summary>Last rejection as a RuleCode name, for the HUD to localize.</summary>
        public string? LastRejection { get; private set; }

        public void Bind(GameSession newSession, CampusPalette newPalette, Camera cam, InputActionAsset asset)
        {
            rejected?.Dispose();
            session = newSession;
            palette = newPalette;
            viewCamera = cam;
            actions = asset;
            rejected = CampusModel.OnRejected(session, code => { LastRejection = code; shake = ShakeSeconds; });

            InputActionMap map = asset.FindActionMap("Build", throwIfNotFound: true);
            point = map.FindAction("Point"); select = map.FindAction("Select"); cancel = map.FindAction("Cancel");
            endDay = map.FindAction("EndDay");
            for (int i = 0; i < facilityKeys.Length; i++)
                facilityKeys[i] = map.FindAction("Facility" + (i + 1));
            map.Enable();
        }

        private void OnDestroy() => rejected?.Dispose();

        public void Select(FacilityKind? kind)
        {
            Selected = kind;
            if (ghost != null) Destroy(ghost);
            ghost = null;
            ghostRenderers = Array.Empty<Renderer>();
            if (kind == null || palette == null) return;
            ghost = Placeholders.Building(kind.Value, palette.StyleFor(kind.Value), transform);
            ghost.name = "Ghost";
            ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
        }

        private void Update()
        {
            if (session == null) return;

            for (int i = 0; i < facilityKeys.Length; i++)
                if (facilityKeys[i]?.WasPressedThisFrame() == true)
                    Select(Selected == (FacilityKind)i ? (FacilityKind?)null : (FacilityKind)i);
            if (cancel?.WasPressedThisFrame() == true) Select(null);
            if (endDay?.WasPressedThisFrame() == true) CampusModel.EndDay(session);

            UpdateHover();
            if (Selected != null && column >= 0 && select?.WasPressedThisFrame() == true)
            {
                if (CampusModel.Build(session, Selected.Value, column, row)) LastRejection = null;
            }
            UpdateGhost();
        }

        private void UpdateHover()
        {
            column = row = -1;
            if (viewCamera == null || point == null) return;
            Ray ray = viewCamera.ScreenPointToRay(point.ReadValue<Vector2>());
            if (!GridMetrics.TryRayToCell(ray, out int c, out int r)) return;
            column = c; row = r;
        }

        private void UpdateGhost()
        {
            if (ghost == null || palette == null || session == null) return;
            bool visible = column >= 0;
            ghost.SetActive(visible);
            if (!visible) return;

            bool valid = CampusModel.IsBuildable(column, row) && !CampusModel.IsOccupied(session, column, row);
            shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime);
            Material material = valid && shake <= 0f ? palette.ghostValid : palette.ghostInvalid;
            foreach (Renderer r in ghostRenderers) r.sharedMaterial = material;

            Vector3 position = GridMetrics.CellCenter(column, row);
            if (shake > 0f) position.x += Mathf.Sin(shake * 60f) * 0.25f * (shake / ShakeSeconds);
            ghost.transform.localPosition = position + Vector3.up * 0.02f;
        }
    }
}
