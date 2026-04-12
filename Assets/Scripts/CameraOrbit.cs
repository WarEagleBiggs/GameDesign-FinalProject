using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class CameraOrbit : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Orbit Settings")]
    public float distance = 15f;
    public float tiltAngle = 45f;

    [Header("Rotation Slider (4 fixed isometric views)")]
    public Slider rotationSlider;

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
            cam.orthographicSize = defaultOrthoSize;

        if (rotationSlider != null)
        {
            rotationSlider.minValue = 0f;
            rotationSlider.maxValue = 1f;
            rotationSlider.wholeNumbers = false;
            rotationSlider.value = 0f;
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
        float t = rotationSlider != null ? rotationSlider.value : 0f;

        int index = Mathf.RoundToInt(t * 3f);
        index = Mathf.Clamp(index, 0, 3);

        float yaw = 45f + (index * 90f);

        Quaternion rotation = Quaternion.Euler(tiltAngle, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }

    void UpdateZoom()
    {
        if (cam == null || !cam.orthographic || zoomSlider == null) return;

        float size = Mathf.Lerp(minOrthoSize, maxOrthoSize, zoomSlider.value);
        cam.orthographicSize = size;
    }
}