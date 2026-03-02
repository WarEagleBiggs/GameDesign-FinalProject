using UnityEngine;
using UnityEngine.UI;

public class CameraOrbit : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Orbit Settings")]
    public float distance = 15f;
    public float tiltAngle = 45f;
    public float yawOffset = 0f;

    [Header("Rotation Slider")]
    public Slider rotationSlider;
    public float snapDegrees = 15f;

    [Header("Zoom Slider (Orthographic)")]
    public Slider zoomSlider;
    public float minOrthoSize = 3f;
    public float maxOrthoSize = 15f;
    public float defaultOrthoSize = 5f;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();

        if (cam != null && cam.orthographic)
        {
            cam.orthographicSize = defaultOrthoSize;
        }

        if (zoomSlider != null)
        {
            zoomSlider.minValue = 0f;
            zoomSlider.maxValue = 1f;
            zoomSlider.value = Mathf.InverseLerp(minOrthoSize, maxOrthoSize, defaultOrthoSize);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        UpdateRotation();
        UpdateZoom();
    }

    void UpdateRotation()
    {
        float sliderValue = rotationSlider != null ? rotationSlider.value : 0f;

        float yaw = (sliderValue * 360f * 3) + yawOffset;

        if (snapDegrees > 0f)
            yaw = Mathf.Round(yaw / snapDegrees) * snapDegrees;

        Quaternion rotation = Quaternion.Euler(tiltAngle, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }

    void UpdateZoom()
    {
        if (cam == null || !cam.orthographic || zoomSlider == null) return;

        float t = zoomSlider.value;
        float size = Mathf.Lerp(minOrthoSize, maxOrthoSize, t);

        cam.orthographicSize = size;
    }
}