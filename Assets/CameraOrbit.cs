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

    [Header("UI")]
    public Slider rotationSlider;

    void Start()
    {
        if (rotationSlider != null)
            rotationSlider.onValueChanged.AddListener(_ => { });
    }

    void LateUpdate()
    {
        if (target == null) return;

        float value = rotationSlider != null ? rotationSlider.value : 0f;
        float yaw = (value * 360f) + yawOffset;

        Quaternion rotation = Quaternion.Euler(tiltAngle, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }
}