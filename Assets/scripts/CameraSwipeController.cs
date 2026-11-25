using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Camera Swipe Controller for forward pan navigation.
/// Supports both mobile touch swipes and desktop mouse drag.
/// Provides:
/// - Default camera position matches the current scene position (where it is placed)
/// - Forward movement along the camera viewing direction across the battlefield
/// - 1:1 direct finger-tracking pan (world-space ground raycast or screen delta fallback)
/// - Configurable forward boundaries (clamped: cannot move backward behind current position)
/// - Smooth damping and optional inertia momentum
/// - UI drag protection (won't swipe if dragging on buttons/UI elements)
/// </summary>
[DisallowMultipleComponent]
public class CameraSwipeController : MonoBehaviour
{
    public enum SwipeTrackingMode
    {
        WorldRaycast,   // Converts screen touch directly to ground plane raycasts (true 1:1 finger anchor)
        ScreenDelta     // Uses delta pixels scaled by sensitivity (classic smooth RTS feel)
    }

    [Header("Tracking Mode")]
    [Tooltip("WorldRaycast anchors the camera under the finger across the battlefield ground plane. ScreenDelta uses pixel delta.")]
    [SerializeField] private SwipeTrackingMode trackingMode = SwipeTrackingMode.WorldRaycast;

    [Header("Default Position & Forward Movement")]
    [Tooltip("If true, retains the camera's current scene transform position as the default base position.")]
    [SerializeField] private bool useCurrentPositionAsDefault = true;
    [Tooltip("Maximum distance the camera can move forward from the default position.")]
    [SerializeField, Min(0.1f)] private float maxForwardDistance = 80f;

    [Header("Sensitivity & Smoothing")]
    [Tooltip("Movement speed multiplier when using ScreenDelta mode or mouse panning.")]
    [SerializeField, Min(0.01f)] private float swipeSensitivity = 1.0f;
    [Tooltip("Smooth transition speed for camera dampening. Higher = snappier, Lower = smoother.")]
    [SerializeField, Min(1f)] private float smoothSpeed = 14f;
    [Tooltip("If true, the camera glides with momentum after releasing a flick swipe.")]
    [SerializeField] private bool enableInertia = true;
    [Tooltip("How quickly inertia momentum fades out (decay rate).")]
    [SerializeField, Range(0.80f, 0.99f)] private float inertiaDecay = 0.92f;

    [Header("Boundaries & Resistance")]
    [Tooltip("Minimum X limit (backward boundary). Automatically updated based on default position.")]
    [SerializeField] private float minX = -8f;
    [Tooltip("Maximum X limit (forward boundary). Automatically updated based on default position.")]
    [SerializeField] private float maxX = 40f;
    [Tooltip("If true, applies elastic rubber-band resistance when dragging beyond limits.")]
    [SerializeField] private bool bounceBackAtBounds = true;
    [Tooltip("Maximum distance the camera can be stretched past bounds before snapping back.")]
    [SerializeField, Range(0f, 3f)] private float maxOverdrag = 1.2f;

    [Header("Input Filters")]
    [Tooltip("Prevent camera panning if the swipe begins over a UI element or button.")]
    [SerializeField] private bool ignoreUIInteractions = true;
    [Tooltip("Ground plane height (Y) used for WorldRaycast tracking.")]
    [SerializeField] private float groundPlaneY = 0f;

    /// <summary>Global flag to suppress camera swiping (e.g. while dragging power-ups).</summary>
    public static bool SuppressSwipe = false;

    private static CameraSwipeController activeInstance;
    public static CameraSwipeController Instance => activeInstance;

    private Camera targetCamera;
    private Vector3 defaultPosition;
    private Quaternion defaultRotation;
    private Vector3 forwardDir = Vector3.right;
    private Vector3 targetPosition;
    private float currentForwardDistance = 0f;
    private float velocityForward = 0f;

    private Plane groundPlane;
    private bool isDragging;
    private Vector3 dragStartWorldPos;
    private Vector3 dragStartCamPos;
    private Vector2 lastScreenPos;
    private bool dragStartedOverUI;

    // Camera shake
    private float shakeTimer = 0f;
    private float shakeTotalDuration = 0f;
    private float shakeIntensity = 0f;
    private Vector3 shakeOffset = Vector3.zero;
    private bool isFlippedView = false;
    private bool isCinematicOverride = false;

    public float MinX
    {
        get => minX;
        set
        {
            minX = value;
            maxForwardDistance = Mathf.Max(0.1f, Mathf.Abs(maxX - minX));
        }
    }

    public float MaxX
    {
        get => maxX;
        set
        {
            maxX = value;
            maxForwardDistance = Mathf.Max(0.1f, Mathf.Abs(maxX - minX));
        }
    }

    public Vector3 DefaultPosition => defaultPosition;
    public float CurrentForwardDistance => currentForwardDistance;
    public float MaxForwardDistance { get => maxForwardDistance; set => maxForwardDistance = Mathf.Max(0.1f, value); }
    public bool IsFlippedView { get => isFlippedView; set => isFlippedView = value; }
    public bool IsCinematicOverride { get => isCinematicOverride; set => isCinematicOverride = value; }
    public Vector3 CurrentShakeOffset => shakeOffset;
    public Vector3 ForwardDirection => forwardDir;

    private void Awake()
    {
        activeInstance = this;
        targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        // Capture current scene position as default
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;
        currentForwardDistance = 0f;

        UpdateForwardDirection();

        // Calculate initial bounds from default position
        if (forwardDir.x >= 0f)
        {
            minX = defaultPosition.x;
            maxX = defaultPosition.x + maxForwardDistance;
        }
        else
        {
            maxX = defaultPosition.x;
            minX = defaultPosition.x - maxForwardDistance;
        }

        targetPosition = defaultPosition;
        groundPlane = new Plane(Vector3.up, new Vector3(0f, groundPlaneY, 0f));
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }

    private void Update()
    {
        UpdateShake();

        if (isCinematicOverride)
        {
            isDragging = false;
            dragStartedOverUI = false;
            velocityForward = 0f;
            return;
        }

        HandleInput();
        ApplyMovement();
    }

    private void UpdateForwardDirection()
    {
        Vector3 camForward = transform.forward;
        if (targetCamera != null)
        {
            camForward = targetCamera.transform.forward;
        }

        forwardDir = Vector3.ProjectOnPlane(camForward, Vector3.up).normalized;
        if (forwardDir.sqrMagnitude < 0.001f)
        {
            forwardDir = isFlippedView ? Vector3.left : Vector3.right;
        }
    }

    private void HandleInput()
    {
        if (SuppressSwipe)
        {
            isDragging = false;
            dragStartedOverUI = false;
            return;
        }

        bool hasTouch = Input.touchCount > 0;
        bool isPointerDown = false;
        bool isPointerHeld = false;
        bool isPointerUp = false;
        Vector2 screenPosition = Vector2.zero;

        if (hasTouch)
        {
            Touch touch = Input.GetTouch(0);
            screenPosition = touch.position;
            if (touch.phase == TouchPhase.Began) isPointerDown = true;
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) isPointerHeld = true;
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) isPointerUp = true;
        }
        else
        {
            screenPosition = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) isPointerDown = true;
            else if (Input.GetMouseButton(0)) isPointerHeld = true;
            else if (Input.GetMouseButtonUp(0)) isPointerUp = true;
        }

        // Pointer Down: Start Drag
        if (isPointerDown)
        {
            dragStartedOverUI = ignoreUIInteractions && IsPointerOverUI(screenPosition);
            if (!dragStartedOverUI)
            {
                isDragging = true;
                velocityForward = 0f;
                lastScreenPos = screenPosition;
                dragStartCamPos = targetPosition;

                if (trackingMode == SwipeTrackingMode.WorldRaycast && TryGetGroundPoint(screenPosition, out Vector3 groundHit))
                {
                    dragStartWorldPos = groundHit;
                }
            }
        }
        // Pointer Held: Pan forward/backward along viewing line
        else if (isPointerHeld && isDragging && !dragStartedOverUI)
        {
            float deltaForward = 0f;
            bool calculatedDelta = false;

            // 1. Precise Frame-to-Frame Ground Plane Tracking (1:1 finger anchor)
            Ray prevRay = targetCamera.ScreenPointToRay(lastScreenPos);
            Ray curRay = targetCamera.ScreenPointToRay(screenPosition);

            if (groundPlane.Raycast(prevRay, out float enterPrev) && groundPlane.Raycast(curRay, out float enterCur))
            {
                Vector3 prevHit = prevRay.GetPoint(enterPrev);
                Vector3 curHit = curRay.GetPoint(enterCur);
                Vector3 worldDelta = prevHit - curHit;
                deltaForward = Vector3.Dot(worldDelta, forwardDir);
                calculatedDelta = true;
            }

            // 2. Reliable Fallback if raycast misses or grazes ground
            if (!calculatedDelta || float.IsNaN(deltaForward) || float.IsInfinity(deltaForward))
            {
                float deltaY = (screenPosition.y - lastScreenPos.y) / (float)Screen.height;
                float deltaX = (screenPosition.x - lastScreenPos.x) / (float)Screen.width;
                deltaForward = -(deltaY + deltaX * 0.35f) * maxForwardDistance * swipeSensitivity * 1.5f;
            }

            float desiredDistance = currentForwardDistance + deltaForward;

            // Elastic overdrag calculation if out of bounds (cannot go below 0 = default position)
            if (bounceBackAtBounds)
            {
                if (desiredDistance < 0f)
                    desiredDistance = 0f - Mathf.Clamp((0f - desiredDistance) * 0.4f, 0f, maxOverdrag);
                else if (desiredDistance > maxForwardDistance)
                    desiredDistance = maxForwardDistance + Mathf.Clamp((desiredDistance - maxForwardDistance) * 0.4f, 0f, maxOverdrag);
            }
            else
            {
                desiredDistance = Mathf.Clamp(desiredDistance, 0f, maxForwardDistance);
            }

            velocityForward = (desiredDistance - currentForwardDistance) / Mathf.Max(0.001f, Time.deltaTime);
            currentForwardDistance = desiredDistance;

            targetPosition = defaultPosition + forwardDir * currentForwardDistance;
            lastScreenPos = screenPosition;
        }
        // Pointer Up: Release Drag
        else if (isPointerUp)
        {
            isDragging = false;
            dragStartedOverUI = false;

            if (!enableInertia)
            {
                velocityForward = 0f;
            }
        }
    }

    private void ApplyMovement()
    {
        // Inertia glide when finger is released
        if (!isDragging && enableInertia && Mathf.Abs(velocityForward) > 0.05f)
        {
            currentForwardDistance += velocityForward * Time.deltaTime;
            velocityForward *= Mathf.Pow(inertiaDecay, Time.deltaTime * 60f);

            // Friction damp if sliding outside bounds
            if (currentForwardDistance < 0f || currentForwardDistance > maxForwardDistance)
            {
                velocityForward *= 0.5f;
            }
        }

        // Rebound snap back into bounds when released (default position is minimum limit)
        if (!isDragging)
        {
            currentForwardDistance = Mathf.Clamp(currentForwardDistance, 0f, maxForwardDistance);
        }

        targetPosition = defaultPosition + forwardDir * currentForwardDistance;

        // Smoothly interpolate camera position
        Vector3 curPos = transform.position;
        curPos = Vector3.Lerp(curPos, targetPosition, Time.deltaTime * smoothSpeed);

        transform.position = curPos + shakeOffset;
    }

    private void UpdateShake()
    {
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float damper = shakeTotalDuration > 0.001f ? (shakeTimer / shakeTotalDuration) : 0f;
            shakeOffset = (Vector3)(Random.insideUnitCircle * (shakeIntensity * damper));
        }
        else
        {
            shakeOffset = Vector3.zero;
            shakeIntensity = 0f;
        }
    }

    /// <summary>
    /// Snaps camera immediately to a specified X coordinate.
    /// </summary>
    public void SnapToX(float newX)
    {
        if (Mathf.Abs(forwardDir.x) > 0.001f)
        {
            float dist = (newX - defaultPosition.x) / forwardDir.x;
            currentForwardDistance = Mathf.Clamp(dist, 0f, maxForwardDistance);
        }
        targetPosition = defaultPosition + forwardDir * currentForwardDistance;
        transform.position = targetPosition;
        velocityForward = 0f;
    }

    /// <summary>
    /// Resets the camera back to its default starting position.
    /// </summary>
    public void ResetToDefaultPosition()
    {
        currentForwardDistance = 0f;
        targetPosition = defaultPosition;
        velocityForward = 0f;
    }

    /// <summary>
    /// Configures the camera view and orientation for the assigned player team.
    /// Team 0: Normal view looking towards +X from Blue base (default scene position).
    /// Team 1: Flipped 180° view looking towards -X from Red base.
    /// </summary>
    public void ConfigureForTeam(int teamId, Vector3 defaultWorldPos, Quaternion defaultWorldRot)
    {
        isFlippedView = (teamId == 1);

        // Always use the pose passed in (both teams now pass exact Inspector values).
        if (true)
        {
            defaultPosition = defaultWorldPos;
            transform.position = defaultWorldPos;
            transform.rotation = defaultWorldRot;
        }
        else
        {
            // Team 0 retains the current scene position as default
            targetPosition = defaultPosition;
            transform.position = defaultPosition;
        }

        UpdateForwardDirection();

        if (forwardDir.x >= 0f)
        {
            minX = defaultPosition.x;
            maxX = defaultPosition.x + maxForwardDistance;
        }
        else
        {
            maxX = defaultPosition.x;
            minX = defaultPosition.x - maxForwardDistance;
        }

        currentForwardDistance = 0f;
        targetPosition = defaultPosition;
        velocityForward = 0f;
    }

    /// <summary>
    /// Triggers a screen shake with the specified intensity and duration.
    /// </summary>
    public void TriggerShake(float intensity = 0.35f, float duration = 0.25f)
    {
        shakeIntensity = Mathf.Max(shakeIntensity, intensity);
        shakeTotalDuration = duration;
        shakeTimer = duration;
    }

    private bool TryGetGroundPoint(Vector2 screenPos, out Vector3 hitPoint)
    {
        if (targetCamera == null)
        {
            hitPoint = Vector3.zero;
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(screenPos);
        if (groundPlane.Raycast(ray, out float enter))
        {
            hitPoint = ray.GetPoint(enter);
            return true;
        }
        hitPoint = Vector3.zero;
        return false;
    }

    private bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        // Check standalone input module / current UI raycast
        if (EventSystem.current.IsPointerOverGameObject())
            return true;

        if (Input.touchCount > 0)
        {
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }

        return false;
    }

    /// <summary>
    /// Utility method to programmatically set X limits (e.g. from BattleSceneSetup arenaWidth).
    /// </summary>
    public void SetBoundaries(float newMinX, float newMaxX)
    {
        minX = newMinX;
        maxX = newMaxX;
        maxForwardDistance = Mathf.Max(0.1f, Mathf.Abs(maxX - minX));

        if (forwardDir.x >= 0f)
        {
            defaultPosition.x = minX;
        }
        else
        {
            defaultPosition.x = maxX;
        }

        currentForwardDistance = Mathf.Clamp(currentForwardDistance, 0f, maxForwardDistance);
        targetPosition = defaultPosition + forwardDir * currentForwardDistance;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? defaultPosition : transform.position;
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.right;
        Vector3 end = start + fwd * maxForwardDistance;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
        Gizmos.DrawLine(start + Vector3.up * 2f, start - Vector3.up * 2f);
        Gizmos.DrawLine(end + Vector3.up * 2f, end - Vector3.up * 2f);
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(start, 0.3f);
        Gizmos.DrawSphere(end, 0.3f);
    }
}
