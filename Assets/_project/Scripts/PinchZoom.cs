using System;
using CCEPL_CAD;
using UnityEngine;

public class PinchZoom : MonoBehaviour
{
    [Header("Scale/Zoom Settings")] [SerializeField]
    private float mouseScrollSensitivity = 0.1f;

    [SerializeField] private float touchPinchSensitivity = 0.005f;
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 3.0f;

    private Vector3 lastMousePosition;
    private float currentScaleFactor;
    private Vector3 initialLocalScale;

    /// <summary>
    /// Initializes and caches the GameObject's initial local scale as a reference base for zoom calculations.
    /// </summary>
    private void Start()
    {
        initialLocalScale = transform.localScale;
    }

    /// <summary>
    /// Processes zoom input each frame via touch or mouse scroll wheel, ignoring input when the pointer is over UI.
    /// </summary>
    private void Update()
    {
        if(Utils.IsPointerOverUI()) return;
        
        if (Input.touchCount > 0) HandleTouchInput();
        else HandleMouseInput();
    }

    /// <summary>
    /// Reads mouse scroll wheel delta to adjust the current scale factor and update object scale.
    /// </summary>
    private void HandleMouseInput()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (!(Mathf.Abs(scrollInput) > 0.01f)) return;
        currentScaleFactor += scrollInput * mouseScrollSensitivity;
        ApplyLocalScaleChanges();
    }

    /// <summary>
    /// Detects two-finger pinch gestures to calculate pinch delta and update object scale.
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

            currentScaleFactor += deltaMagnitudeDiff * touchPinchSensitivity;
            ApplyLocalScaleChanges();

            /*Vector2 averageDelta = (touch0.deltaPosition + touch1.deltaPosition) * 0.5f;
            RotateObject(averageDelta);*/
        }
    }

    /// <summary>
    /// Clamps the scale factor within configured limits and applies the resulting scale to the transform.
    /// </summary>
    private void ApplyLocalScaleChanges()
    {
        currentScaleFactor = Mathf.Clamp(currentScaleFactor, minScale, maxScale);
        transform.localScale = initialLocalScale * currentScaleFactor;
    }
}
