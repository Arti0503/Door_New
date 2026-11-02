using System;
using CCEPL_CAD.Models;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CCEPL_CAD
{
    public class HandleToolOperation : MonoBehaviour
    {
        public static HandleToolOperation Instance;
        internal ToolObject _tool;
        Camera _camera;
        [SerializeField] private float distanceFromCamera = 5f;
        [SerializeField] private float minDistanceFromCamera = 4f;
        [SerializeField] private float maxDistanceFromCamera = 7.5f;
        [SerializeField] private float fovDistanceMultiplier = 0.0625f;
        [SerializeField] private float defaultFOV = 60f;
        [SerializeField] private float defaultOrthographicSize = 5f;
        [SerializeField] private LayerMask targetLayerMask = ~0; // All layers by default
        private Vector3 _initialToolScale = Vector3.one;

        /// <summary>
        /// Initializes the singleton instance and caches the default camera field-of-view or orthographic size for dynamic scaling calculations.
        /// </summary>
        private void Awake()
        {
            Instance = this;
            if (_camera == null) _camera = Camera.main;
            if (_camera != null)
            {
                if (!_camera.orthographic && _camera.fieldOfView > 0f)
                {
                    defaultFOV = _camera.fieldOfView;
                }
                else if (_camera.orthographic && _camera.orthographicSize > 0f)
                {
                    defaultOrthographicSize = _camera.orthographicSize;
                }
            }
        }

        /// <summary>
        /// Ensures the main camera reference is cached and subscribes to the tool selection event.
        /// </summary>
        private void OnEnable()
        {
            if (_camera == null) _camera = Camera.main;
            EventManager.OnToolSelected += ToolSelect;
        }

        /// <summary>
        /// Unsubscribes from the tool selection event to prevent memory leaks and dangling handlers.
        /// </summary>
        private void OnDisable()
        {
            EventManager.OnToolSelected -= ToolSelect;
        }

        /// <summary>
        /// Tracks pointer movement each frame to update tool positioning and processes pointer click/tap raycasts when not obstructed by UI.
        /// </summary>
        private void Update()
        {
            if (_camera == null) _camera = Camera.main;

            // Cross-platform UI obstruction check (Works for Touch and Mouse)
            if (RepairInputHandler.IsPointerOverUI())
            {
                return;
            }

            // Smooth tool position following pointer (Mobile Touch or Desktop Mouse)
            if (_tool && !_tool.IsEngage && _camera != null)
            {
                MoveToolWithPointer();

                // Direct tap/click 3D raycast selection (for touch screens and mobile devices)
                if (RepairInputHandler.IsPointerDown())
                {
                    TryRaycastTarget();
                }
            }
        }

        /// <summary>
        /// Updates the tool position along the camera pointer ray and adjusts its scale to match the current viewport settings.
        /// </summary>
        private void MoveToolWithPointer()
        {
            UpdateToolPositionAndScale();
        }

        /// <summary>
        /// Calculates the pointer ray from the active camera and positions/scales the active tool to maintain consistent screen size across zoom modes.
        /// </summary>
        private void UpdateToolPositionAndScale()
        {
            if (_tool == null || _camera == null) return;

            Vector3 pointerPos = RepairInputHandler.GetPointerPosition();
            Ray ray = _camera.ScreenPointToRay(pointerPos);
            float currentDist = distanceFromCamera;
            var toolOffset = _tool.Offset;

            // In CameraFOV zoom mode:
            // 1. Calculate dynamic distance along the ray (Option B) clamped within safe bounds.
            // 2. Adjust localScale by (distRatio * fovRatio) so the tool maintains the EXACT same visual screen size and cursor alignment.
            if (DirectModelRotatorAndScaler.CurrentZoomMode == DirectModelRotatorAndScaler.ZoomMode.CameraFOV)
            {
                float finalScaleFactor = 1f;

                if (_camera.orthographic)
                {
                    float sizeDiff = defaultOrthographicSize - _camera.orthographicSize;
                    currentDist = Mathf.Clamp(distanceFromCamera + (sizeDiff * fovDistanceMultiplier), minDistanceFromCamera, maxDistanceFromCamera);

                    if (defaultOrthographicSize > 0.001f)
                    {
                        finalScaleFactor = _camera.orthographicSize / defaultOrthographicSize;
                    }
                }
                else
                {
                    float fovDiff = defaultFOV - _camera.fieldOfView;
                    currentDist = Mathf.Clamp(distanceFromCamera + (fovDiff * fovDistanceMultiplier), minDistanceFromCamera, maxDistanceFromCamera);

                    if (defaultFOV > 0.001f && distanceFromCamera > 0.001f)
                    {
                        float currentHalfFovRad = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                        float defaultHalfFovRad = defaultFOV * 0.5f * Mathf.Deg2Rad;
                        float fovRatio = Mathf.Tan(currentHalfFovRad) / Mathf.Tan(defaultHalfFovRad);
                        float distRatio = currentDist / distanceFromCamera;

                        // Combining distance ratio with FOV ratio keeps tool screen size exactly constant
                        finalScaleFactor = distRatio * fovRatio;
                    }
                }

                _tool.transform.localScale = _initialToolScale * finalScaleFactor;
                toolOffset = _tool.Offset * finalScaleFactor;
            }
            else
            {
                currentDist = distanceFromCamera;
                _tool.transform.localScale = _initialToolScale;
            }

            Vector3 targetPosition = ray.GetPoint(currentDist);
            _tool.transform.position = targetPosition - toolOffset;
        }

        /// <summary>
        /// Casts a ray from the pointer into the scene to detect interactable component parts and triggers the active tool on the closest valid target.
        /// </summary>
        private void TryRaycastTarget()
        {
            Ray ray = RepairInputHandler.GetPointerRay(_camera);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, targetLayerMask);
            if (hits == null || hits.Length == 0) return;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // Ignore the tool itself so it never blocks interaction with the model underneath
                if (_tool != null && (hit.collider.gameObject == _tool.gameObject || hit.collider.transform.IsChildOf(_tool.transform)))
                {
                    continue;
                }

                var componentPart = hit.collider.GetComponentInParent<IntrectableComponentPart>();
                if (componentPart != null && _tool != null)
                {
                    _tool.OnTrigger(componentPart);
                    break;
                }
            }
        }

        /// <summary>
        /// Handles the tool selection event by destroying any active tool instance, spawning and initializing the newly selected tool, and updating UI references.
        /// </summary>
        private void ToolSelect(ToolType tool, ToolObject toolObject)
        {
            if (_tool) Destroy(_tool.gameObject);

            _tool = Instantiate(toolObject ?? RefManager.Instance.ToolObject, transform);
            _tool.Init(tool);
            _initialToolScale = _tool.transform.localScale;
            RefManager.Instance.UIManager._selectedTool = _tool;

            UpdateToolPositionAndScale();
        }
    }
}