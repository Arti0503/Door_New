using System;
using System.Collections;
using System.Collections.Generic;
using CCEPL_CAD.Models;
using UnityEngine;
using UnityEngine.UI;

namespace CCEPL_CAD
{
    public class DirectModelRotatorAndScaler : MonoBehaviour
    {
        public enum ZoomMode
        {
            ModelScale,
            CameraFOV
        }

        [Header("Zoom Mode")]
        [SerializeField] private ZoomMode currentZoomMode = ZoomMode.CameraFOV;
        [SerializeField] private Camera targetCamera;

        [Header("Rotation Settings")]
        [SerializeField] private float rotateSpeedMouse = 0.5f;
        [SerializeField] private float rotateSpeedTouch = 0.15f;

        [Header("Scale Settings (Model Scale Mode)")]
        [SerializeField] private float mouseScrollSensitivity = 0.1f;
        [SerializeField] private float touchPinchSensitivity = 0.005f;
        [SerializeField] private float minScale = 0.5f;
        [SerializeField] private float maxScale = 3.0f;

        [Header("FOV Settings (Camera FOV Mode)")]
        [SerializeField] private float mouseScrollFovSensitivity = 15f;
        [SerializeField] private float touchPinchFovSensitivity = 0.08f;
        [SerializeField] private float minFOV = 20f;
        [SerializeField] private float maxFOV = 60f;

        private Vector3 lastMousePosition;
        private Vector3 initialLocalScale;
        private float currentScaleFactor;
        public static bool IsLocked { get; set; } = false;

        public static ZoomMode CurrentZoomMode { get; private set;}

        /// <summary>
        /// Initializes the static zoom mode property from the serialized configuration on component initialization.
        /// </summary>
        private void Awake()
        {
            CurrentZoomMode = currentZoomMode;
        }

        /// <summary>
        /// Synchronizes the static zoom mode property when inspector values change in the Unity Editor.
        /// </summary>
        private void OnValidate()
        {
            CurrentZoomMode = currentZoomMode;
        }

        /// <summary>
        /// Subscribes to the camera lock state event and updates the static zoom mode property.
        /// </summary>
        private void OnEnable()
        {
            EventManager.OnCameraLockStateChanged += SetLockState;
            CurrentZoomMode = currentZoomMode;
        }

        /// <summary>
        /// Unsubscribes from the camera lock state event when the component is disabled.
        /// </summary>
        private void OnDisable()
        {
            EventManager.OnCameraLockStateChanged -= SetLockState;
        }

        /// <summary>
        /// Updates the static locked state flag to enable or disable rotation and zoom interactions.
        /// </summary>
        private void SetLockState(bool locked)
        {
            IsLocked = locked;
        }

        /// <summary>
        /// Caches initial transform local scale and ensures a reference to the target camera exists.
        /// </summary>
        private void Start()
        {
            initialLocalScale = transform.localScale;
            currentScaleFactor = 1.0f;

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        /// <summary>
        /// Checks input locks and UI obstruction each frame, routing touch or mouse input for model rotation and zooming.
        /// </summary>
        private void Update()
        {
            CurrentZoomMode = currentZoomMode;
            //if (IsLocked) return;
            if (Utils.IsPointerOverUI()) return;

            if (Input.touchCount > 0) 
                HandleTouchInput();
            else 
                HandleMouseInput();
        }

        /// <summary>
        /// Processes mouse scroll wheel input to adjust camera FOV or model scale based on the active zoom mode.
        /// </summary>
        private void HandleMouseInput()
        {
            // Rotation can be re-enabled here if needed:
            // if (Input.GetMouseButtonDown(0)) lastMousePosition = Input.mousePosition;
            // else if (Input.GetMouseButton(0))
            // {
            //     Vector3 delta = Input.mousePosition - lastMousePosition;
            //     lastMousePosition = Input.mousePosition;
            //     transform.Rotate(Vector3.up, -delta.x * rotateSpeedMouse, Space.World);
            //     transform.Rotate(Vector3.right, delta.y * rotateSpeedMouse, Space.World);
            // }

            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollInput) <= 0.01f) return;

            if (currentZoomMode == ZoomMode.CameraFOV)
            {
                ApplyCameraFovDelta(-scrollInput * mouseScrollFovSensitivity);
            }
            else
            {
                currentScaleFactor += scrollInput * mouseScrollSensitivity;
                ApplyLocalScaleChanges();
            }
        }

        /// <summary>
        /// Handles two-finger touch pinch gestures to zoom the model via camera FOV or local scale adjustments.
        /// </summary>
        private void HandleTouchInput()
        {
            if (Input.touchCount == 2)
            {
                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                Vector2 touchZeroPrevPos = touch0.position - touch0.deltaPosition;
                Vector2 touchOnePrevPos = touch1.position - touch1.deltaPosition;

                float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float touchDeltaMag = (touch0.position - touch1.position).magnitude;

                float deltaMagnitudeDiff = touchDeltaMag - prevTouchDeltaMag;

                if (currentZoomMode == ZoomMode.CameraFOV)
                {
                    // Pinch out (fingers moving apart, delta > 0) zooms in by decreasing FOV
                    ApplyCameraFovDelta(-deltaMagnitudeDiff * touchPinchFovSensitivity);
                }
                else
                {
                    currentScaleFactor += deltaMagnitudeDiff * touchPinchSensitivity;
                    ApplyLocalScaleChanges();
                }

                /*Vector2 averageDelta = (touch0.deltaPosition + touch1.deltaPosition) * 0.5f;
                RotateObject(averageDelta);*/
            }
        }

        /// <summary>
        /// Rotates the object in world space around the up and right axes according to the provided screen delta vector.
        /// </summary>
        internal void RotateObject(Vector2 delta)
        {
            transform.Rotate(Vector3.up, -delta.x * rotateSpeedTouch, Space.World);
            transform.Rotate(Vector3.right, delta.y * rotateSpeedTouch, Space.World);
        }

        /// <summary>
        /// Clamps the scale factor within allowed minimum/maximum limits and updates the transform's local scale.
        /// </summary>
        private void ApplyLocalScaleChanges()
        {
            currentScaleFactor = Mathf.Clamp(currentScaleFactor, minScale, maxScale);
            transform.localScale = initialLocalScale * currentScaleFactor;
        }

        /// <summary>
        /// Adjusts and clamps the target camera's field of view or orthographic size based on the given delta value.
        /// </summary>
        private void ApplyCameraFovDelta(float fovDelta)
        {
            if (targetCamera == null) return;

            if (targetCamera.orthographic)
            {
                targetCamera.orthographicSize = Mathf.Clamp(
                    targetCamera.orthographicSize + fovDelta * 0.1f,
                    1f,
                    20f
                );
            }
            else
            {
                targetCamera.fieldOfView = Mathf.Clamp(
                    targetCamera.fieldOfView + fovDelta,
                    minFOV,
                    maxFOV
                );
            }
        }
    }
}