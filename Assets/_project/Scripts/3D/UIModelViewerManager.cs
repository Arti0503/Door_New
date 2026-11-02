using System;
using CCEPL_CAD;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIModelViewerManager : MonoBehaviour
{
    [Header("UI & Render Target")]
    [SerializeField] private RectTransform toolViewPanel;
    [SerializeField] private Button closePanel;
    [SerializeField] private RawImage targetRawImage;

    [Header("Spawn Settings")]
    [Tooltip("World position and rotation where models will be instantiated (e.g., dedicated off-screen point at 0, 1000, 0).")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Layer assigned to the 3D preview camera (e.g., UI3DModel).")]
    [SerializeField] private int modelLayerIndex = 0;
    [Tooltip("Optional: Assign the preview camera to ensure screen-aligned rotation.")]
    [SerializeField] private Camera previewCamera;

    [Header("Scale / Zoom Limits")]
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 2.0f;
    [SerializeField] private float currentScale = 1.0f;
    [SerializeField] private float mouseScrollSensitivity = 0.1f;
    [SerializeField] private float touchPinchSensitivity = 0.005f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSensitivity = 0.4f;
    [SerializeField] private bool rotateHorizontal = true;
    [SerializeField] private bool rotateVertical = true;
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertY = false;

    [Header("Momentum / Inertia")]
    [SerializeField] private bool enableMomentum = true;
    [Tooltip("Higher values stop momentum faster.")]
    [SerializeField] private float momentumDecay = 4f;

    [Header("Auto-Rotation")]
    [SerializeField] private bool enableAutoRotation = true;
    [Tooltip("Degrees per second around world Y-axis.")]
    [SerializeField] private float autoRotationSpeed = 20f;
    [Tooltip("Seconds to wait after user interaction stops before auto-rotating.")]
    [SerializeField] private float idleDelay = 2f;

    private GameObject currentSpawnedModel;
    private bool isDragging;
    private float idleTimer;
    private Vector2 angularVelocity;

    /// <summary>
    /// Binds UI event listeners to the target RawImage and initially hides the 3D tool view panel.
    /// </summary>
    private void Awake()
    {
        SetupUIEvents();
        if (toolViewPanel != null)
        {
            toolViewPanel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Subscribes to the model spawn event and binds the close button click listener.
    /// </summary>
    private void OnEnable()
    {
        EventManager.Spawn3DToolObject += SpawnModel;
        if (closePanel != null)
        {
            closePanel.onClick.AddListener(ClearModel);
        }
    }

    /// <summary>
    /// Unsubscribes from the model spawn event and removes the close button listener.
    /// </summary>
    private void OnDisable()
    {
        EventManager.Spawn3DToolObject -= SpawnModel;
        if (closePanel != null)
        {
            closePanel.onClick.RemoveListener(ClearModel);
        }
    }

    /// <summary>
    /// Evaluates touch pinch zoom gestures, drag inertia decay, and automatic model rotation each frame.
    /// </summary>
    private void Update()
    {
        HandleTouchPinch();
        HandleInertiaAndAutoRotate();
    }

    // ==========================================
    // RUNTIME SPAWNING API
    // ==========================================

    /// <summary>
    /// Instantiates a 3D preview model from the given prefab, assigns preview layers, resets transforms, and opens the preview panel.
    /// </summary>
    public void SpawnModel(ToolObject toolPrefab)
    {
        if (toolPrefab == null)
        {
            Debug.LogWarning("[UIModelViewer] Attempted to spawn a null prefab.");
            return;
        }

        ClearModel();

        if (toolViewPanel != null)
        {
            toolViewPanel.gameObject.SetActive(true);
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : new Vector3(0, 1000, 0);
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        Transform parent = spawnPoint != null ? spawnPoint : null;

        // Instantiate without destroying the source asset/component
        currentSpawnedModel = Instantiate(toolPrefab.gameObject, pos, rot, parent);
        SetLayerRecursively(currentSpawnedModel, modelLayerIndex);

        // Reset scale and states
        currentScale = Mathf.Clamp(currentScale, minScale, maxScale);
        currentSpawnedModel.transform.localScale = Vector3.one * currentScale;

        angularVelocity = Vector2.zero;
        idleTimer = 0f;
    }

    /// <summary>
    /// Destroys the currently spawned preview model and hides the tool view panel.
    /// </summary>
    public void ClearModel()
    {
        if (currentSpawnedModel != null)
        {
            Destroy(currentSpawnedModel);
            currentSpawnedModel = null;
        }

        if (toolViewPanel != null)
        {
            toolViewPanel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Configures the minimum and maximum scaling boundaries and reapplies the current scale clamped within the new limits.
    /// </summary>
    public void SetScaleLimits(float min, float max)
    {
        minScale = min;
        maxScale = max;
        ApplyScale(0f);
    }

    // ==========================================
    // EVENT BINDINGS FOR RAW IMAGE
    // ==========================================

    /// <summary>
    /// Attaches and configures pointer and drag event entries on the preview RawImage's EventTrigger component.
    /// </summary>
    private void SetupUIEvents()
    {
        if (targetRawImage == null)
        {
            Debug.LogError("[UIModelViewer] Target RawImage is not assigned in the Inspector!");
            return;
        }

        targetRawImage.raycastTarget = true;

        EventTrigger trigger = targetRawImage.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = targetRawImage.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.Clear();
        AddEventEntry(trigger, EventTriggerType.PointerDown, (data) => OnPointerDown((PointerEventData)data));
        AddEventEntry(trigger, EventTriggerType.PointerUp, (data) => OnPointerUp((PointerEventData)data));
        AddEventEntry(trigger, EventTriggerType.Drag, (data) => OnDrag((PointerEventData)data));
        AddEventEntry(trigger, EventTriggerType.Scroll, (data) => OnScroll((PointerEventData)data));
    }

    /// <summary>
    /// Helper method to construct and register an event entry callback with an EventTrigger.
    /// </summary>
    private void AddEventEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    // ==========================================
    // INTERACTION HANDLERS
    // ==========================================

    /// <summary>
    /// Handles pointer down events on the preview viewport, initiating drag tracking and resetting velocity and idle timers.
    /// </summary>
    private void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        idleTimer = 0f;
        angularVelocity = Vector2.zero;
    }

    /// <summary>
    /// Handles pointer release events on the preview viewport, concluding the active drag interaction.
    /// </summary>
    private void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    /// <summary>
    /// Computes rotational deltas from pointer drag movements, applies rotation to the model, and calculates momentum velocity.
    /// </summary>
    private void OnDrag(PointerEventData eventData)
    {
        if (Input.touchCount > 1 || currentSpawnedModel == null) return;

        idleTimer = 0f;

        float deltaX = rotateHorizontal ? eventData.delta.x * rotationSensitivity * (invertX ? 1f : -1f) : 0f;
        float deltaY = rotateVertical ? eventData.delta.y * rotationSensitivity * (invertY ? -1f : 1f) : 0f;

        ApplyRotation(-deltaX, deltaY);

        if (Time.deltaTime > 0f)
        {
            angularVelocity = new Vector2(-deltaX, deltaY) / Time.deltaTime;
        }
    }

    /// <summary>
    /// Handles mouse scroll wheel deltas to adjust the preview model's scale.
    /// </summary>
    private void OnScroll(PointerEventData eventData)
    {
        idleTimer = 0f;
        ApplyScale(eventData.scrollDelta.y * mouseScrollSensitivity);
    }

    /// <summary>
    /// Detects two-finger pinch gestures to scale the spawned 3D preview model.
    /// </summary>
    private void HandleTouchPinch()
    {
        if (Input.touchCount != 2 || currentSpawnedModel == null) return;

        idleTimer = 0f;
        angularVelocity = Vector2.zero;

        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);

        Vector2 t0Prev = t0.position - t0.deltaPosition;
        Vector2 t1Prev = t1.position - t1.deltaPosition;

        float prevMag = (t0Prev - t1Prev).magnitude;
        float currMag = (t0.position - t1.position).magnitude;
        float diff = currMag - prevMag;

        ApplyScale(diff * touchPinchSensitivity);
    }

    /// <summary>
    /// Applies momentum decay when dragging stops, and initiates automatic turntable rotation after an idle threshold.
    /// </summary>
    private void HandleInertiaAndAutoRotate()
    {
        if (currentSpawnedModel == null || isDragging) return;

        if (enableMomentum && angularVelocity.sqrMagnitude > 0.01f)
        {
            ApplyRotation(angularVelocity.x * Time.deltaTime, angularVelocity.y * Time.deltaTime);
            angularVelocity = Vector2.Lerp(angularVelocity, Vector2.zero, momentumDecay * Time.deltaTime);
            return;
        }

        idleTimer += Time.deltaTime;
        if (enableAutoRotation && idleTimer >= idleDelay)
        {
            currentSpawnedModel.transform.Rotate(Vector3.up, autoRotationSpeed * Time.deltaTime, Space.World);
        }
    }

    /// <summary>
    /// Rotates the spawned model around the preview camera's relative horizontal and vertical axes.
    /// </summary>
    private void ApplyRotation(float horizontalDelta, float verticalDelta)
    {
        if (currentSpawnedModel == null) return;

        // Determine view axes based on preview camera (or fallback to world axes)
        Vector3 rightAxis = previewCamera != null ? previewCamera.transform.right : Vector3.right;
        Vector3 upAxis = previewCamera != null ? previewCamera.transform.up : Vector3.up;

        if (rotateHorizontal && Mathf.Abs(horizontalDelta) > 0.0001f)
        {
            currentSpawnedModel.transform.Rotate(upAxis, horizontalDelta, Space.World);
        }

        if (rotateVertical && Mathf.Abs(verticalDelta) > 0.0001f)
        {
            currentSpawnedModel.transform.Rotate(rightAxis, verticalDelta, Space.World);
        }
    }

    /// <summary>
    /// Adjusts and clamps the model's uniform scale by the given delta value.
    /// </summary>
    private void ApplyScale(float delta)
    {
        if (currentSpawnedModel == null) return;

        currentScale = Mathf.Clamp(currentScale + delta, minScale, maxScale);
        currentSpawnedModel.transform.localScale = Vector3.one * currentScale;
    }

    /// <summary>
    /// Recursively sets the Unity GameObject layer for an object and all its child transforms.
    /// </summary>
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}