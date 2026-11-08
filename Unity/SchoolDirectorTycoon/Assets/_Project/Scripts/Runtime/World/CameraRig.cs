#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;

namespace SchoolDirectorTycoon.World
{
    /// <summary>Tycoon camera: the rig is the look-at pivot on the ground; the camera orbits it.
    /// WASD/arrows and screen edges pan, right-drag pans, wheel zooms, Q/E rotates. Pivot stays over the campus.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Camera? viewCamera;
        [SerializeField] private InputActionAsset? actions;
        [SerializeField] private float panSpeed = 1.1f;          // per unit of distance, per second
        [SerializeField] private float rotateSpeed = 90f;        // degrees per second
        [SerializeField] private float zoomStep = 0.12f;         // fraction of distance per wheel notch
        [SerializeField] private float minDistance = 12f, maxDistance = 90f;
        [SerializeField] private float minPitch = 30f, maxPitch = 70f;
        [SerializeField] private float edgePixels = 8f;
        [SerializeField] private bool edgePan = true;
        [SerializeField] private float smoothing = 12f;

        private InputAction? pan, drag, point, zoom, rotate;
        private float yaw = 45f, pitch = 50f, distance = 55f, targetDistance = 55f;
        private Vector3 targetPivot;
        private Vector2 lastPoint;

        public Camera? ViewCamera => viewCamera;

        public void Configure(Camera cam, InputActionAsset asset)
        {
            viewCamera = cam;
            actions = asset;
            Bind();
        }

        private void Awake()
        {
            targetPivot = GridMetrics.Center;
            transform.position = targetPivot;
            Bind();
        }

        private void Bind()
        {
            InputActionMap? map = actions != null ? actions.FindActionMap("Camera") : null;
            if (map == null) return;
            pan = map.FindAction("Pan"); drag = map.FindAction("Drag"); point = map.FindAction("Point");
            zoom = map.FindAction("Zoom"); rotate = map.FindAction("Rotate");
            map.Enable();
        }

        public void ResetView()
        {
            targetPivot = GridMetrics.Center;
            yaw = 45f; pitch = 50f; targetDistance = 55f;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

            Vector2 move = pan?.ReadValue<Vector2>() ?? Vector2.zero;
            Vector2 pointer = point?.ReadValue<Vector2>() ?? Vector2.zero;
            if (edgePan && Application.isFocused && pointer != Vector2.zero)
            {
                if (pointer.x <= edgePixels) move.x -= 1f; else if (pointer.x >= Screen.width - edgePixels) move.x += 1f;
                if (pointer.y <= edgePixels) move.y -= 1f; else if (pointer.y >= Screen.height - edgePixels) move.y += 1f;
            }
            move = Vector2.ClampMagnitude(move, 1f);
            targetPivot += (right * move.x + forward * move.y) * (panSpeed * distance * dt);

            if (drag != null && drag.IsPressed())
            {
                Vector2 delta = pointer - lastPoint;
                float worldPerPixel = 2f * distance * Mathf.Tan(0.5f * Mathf.Deg2Rad * (viewCamera != null ? viewCamera.fieldOfView : 40f)) / Mathf.Max(1, Screen.height);
                targetPivot -= (right * delta.x + forward * delta.y) * worldPerPixel;
            }
            lastPoint = pointer;

            yaw += (rotate?.ReadValue<float>() ?? 0f) * rotateSpeed * dt;
            float wheel = zoom?.ReadValue<float>() ?? 0f;
            if (Mathf.Abs(wheel) > 0.01f)
                targetDistance *= 1f - Mathf.Sign(wheel) * zoomStep;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            // Look more top-down when zoomed out, more side-on when close.
            pitch = Mathf.Lerp(minPitch, maxPitch, Mathf.InverseLerp(minDistance, maxDistance, targetDistance));

            targetPivot.x = Mathf.Clamp(targetPivot.x, 0f, GridMetrics.Width);
            targetPivot.z = Mathf.Clamp(targetPivot.z, 0f, GridMetrics.Depth);
            targetPivot.y = 0f;

            float t = 1f - Mathf.Exp(-smoothing * dt);
            transform.position = Vector3.Lerp(transform.position, targetPivot, t);
            distance = Mathf.Lerp(distance, targetDistance, t);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (viewCamera != null)
            {
                viewCamera.transform.localPosition = new Vector3(0f, 0f, -distance);
                viewCamera.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
