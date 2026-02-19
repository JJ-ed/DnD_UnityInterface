using UnityEngine;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class CameraZoomController : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Optional. If null and Auto Find Targets is enabled, uses Camera.main.")]
    [SerializeField] private Camera? targetCamera;

    [Tooltip("Optional. If assigned, zoom is applied to this CinemachineCamera's Lens (recommended when Cinemachine is driving the view).")]
    [SerializeField] private CinemachineCamera? targetCinemachineCamera;

    [Tooltip("If enabled, tries to auto-populate missing targets on enable.")]
    [SerializeField] private bool autoFindTargets = true;

    [Header("Input")]
    [Tooltip("Mouse wheel sensitivity. Higher = faster zoom.")]
    [SerializeField] private float mouseWheelSensitivity = 12f;

    [Tooltip("Invert scroll direction.")]
    [SerializeField] private bool invertMouseWheel;

    [Tooltip("Use unscaled delta time for smoothing (ignores Time.timeScale).")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Limits")]
    [Tooltip("Minimum orthographic size (most zoomed-in).")]
    [SerializeField] private float minOrthographicSize = 6f;

    [Tooltip("Maximum orthographic size (most zoomed-out).")]
    [SerializeField] private float maxOrthographicSize = 30f;

    [Header("Smoothing")]
    [SerializeField] private bool smooth = true;

    [Tooltip("SmoothDamp time in seconds.")]
    [SerializeField] private float smoothTime = 0.08f;

    private float _desiredOrthoSize;
    private float _orthoVelocity;
    private bool _initialized;

    private void OnEnable()
    {
        ResolveTargets();
        SyncDesiredFromCurrent();
    }

    private void Reset()
    {
        autoFindTargets = true;
        mouseWheelSensitivity = 12f;
        minOrthographicSize = 6f;
        maxOrthographicSize = 30f;
        smooth = true;
        smoothTime = 0.08f;
        useUnscaledTime = true;
        invertMouseWheel = false;
    }

    private void ResolveTargets()
    {
        if (!autoFindTargets) return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCinemachineCamera == null)
            targetCinemachineCamera = FindObjectOfType<CinemachineCamera>();
    }

    private void SyncDesiredFromCurrent()
    {
        var current = GetCurrentOrthographicSize();
        if (current <= 0f) return;

        _desiredOrthoSize = current;
        _initialized = true;
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private float GetCurrentOrthographicSize()
    {
        // Prefer Cinemachine lens if present (Cinemachine will overwrite the Camera component each frame).
        if (targetCinemachineCamera != null)
            return Mathf.Max(0.0001f, targetCinemachineCamera.Lens.OrthographicSize);

        if (targetCamera != null)
            return Mathf.Max(0.0001f, targetCamera.orthographicSize);

        return 0f;
    }

    private void ApplyOrthographicSize(float value)
    {
        value = Mathf.Clamp(value, minOrthographicSize, maxOrthographicSize);

        if (targetCinemachineCamera != null)
        {
            // Lens is a struct-like setting; assign back to ensure the change sticks across versions.
            var lens = targetCinemachineCamera.Lens;
            lens.OrthographicSize = value;
            targetCinemachineCamera.Lens = lens;
        }

        if (targetCamera != null)
        {
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = value;
        }
    }

    private void Update()
    {
        ResolveTargets();

        if (targetCamera == null && targetCinemachineCamera == null)
            return;

        if (!_initialized)
            SyncDesiredFromCurrent();

        // Mouse wheel zoom.
        float scroll;
        
#if ENABLE_INPUT_SYSTEM

        // Input System: scroll is in "wheel ticks" (typically 120 per notch on many mice).
        // Normalize so one notch ~= 1.0 for stable tuning.
        var scrollY = Mouse.current?.scroll.ReadValue().y ?? 0f;
        scroll = scrollY / 120f;
#else
        scroll = Input.GetAxis("Mouse ScrollWheel");
#endif
        if (invertMouseWheel) scroll = -scroll;

        // Scroll is typically small; scale to orthographic units.
        if (Mathf.Abs(scroll) > 0.00001f)
        {
            // Positive scroll should zoom IN (smaller ortho size).
            _desiredOrthoSize -= scroll * mouseWheelSensitivity;
            _desiredOrthoSize = Mathf.Clamp(_desiredOrthoSize, minOrthographicSize, maxOrthographicSize);
        }

        var current = GetCurrentOrthographicSize();
        var next = _desiredOrthoSize;

        if (smooth)
        {
            next = Mathf.SmoothDamp(current, _desiredOrthoSize, ref _orthoVelocity, smoothTime, Mathf.Infinity, GetDeltaTime());
        }

        ApplyOrthographicSize(next);
    }
}