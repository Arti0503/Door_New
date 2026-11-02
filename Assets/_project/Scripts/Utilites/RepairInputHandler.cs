using UnityEngine;
using UnityEngine.EventSystems;

namespace CCEPL_CAD
{
    public static class RepairInputHandler
    {
        /// <summary>
        /// Gets the current pointer screen position (Touch or Mouse).
        /// Works on Mobile Touch, Touchscreen Kiosks, and PC Mouse.
        /// </summary>
        public static Vector3 GetPointerPosition()
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                return touch.position;
            }
            return Input.mousePosition;
        }

        /// <summary>
        /// Returns true if pointer interaction just started (Touch phase began or Mouse button down).
        /// </summary>
        public static bool IsPointerDown()
        {
            if (Input.touchCount > 0)
            {
                return Input.GetTouch(0).phase == TouchPhase.Began;
            }
            return Input.GetMouseButtonDown(0);
        }

        /// <summary>
        /// Returns true if pointer is actively pressing/dragging.
        /// </summary>
        public static bool IsPointerHeld()
        {
            if (Input.touchCount > 0)
            {
                TouchPhase phase = Input.GetTouch(0).phase;
                return phase == TouchPhase.Moved || phase == TouchPhase.Stationary;
            }
            return Input.GetMouseButton(0);
        }

        /// <summary>
        /// Returns true if pointer interaction released (Touch phase ended/canceled or Mouse button up).
        /// </summary>
        public static bool IsPointerUp()
        {
            if (Input.touchCount > 0)
            {
                TouchPhase phase = Input.GetTouch(0).phase;
                return phase == TouchPhase.Ended || phase == TouchPhase.Canceled;
            }
            return Input.GetMouseButtonUp(0);
        }

        /// <summary>
        /// Checks whether current touch or mouse pointer is over a UI element.
        /// Prevents 3D object interaction when tapping UI buttons.
        /// </summary>
        public static bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;

            if (Input.touchCount > 0)
            {
                int fingerId = Input.GetTouch(0).fingerId;
                return EventSystem.current.IsPointerOverGameObject(fingerId);
            }
            return EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>
        /// Converts the current pointer position (Touch or Mouse) to a 3D Ray.
        /// </summary>
        public static Ray GetPointerRay(Camera cam)
        {
            Camera targetCam = cam != null ? cam : Camera.main;
            if (targetCam == null) return new Ray();

            return targetCam.ScreenPointToRay(GetPointerPosition());
        }
    }
}
