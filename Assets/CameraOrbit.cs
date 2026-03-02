using UnityEngine;
using UnityEngine.UI;

public class CameraOrbit : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Orbit Settings")]
    public float distance = 15f;
    public float height = 10f;

    [Header("UI")]
    public Slider rotationSlider;

    void Start()
    {
        if (rotationSlider != null)
            rotationSlider.onValueChanged.AddListener(UpdateCamera);
    }

    void UpdateCamera(float value)
    {
        if (target == null) return;

        float angle = value * 360f;

        float rad = angle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Sin(rad) * distance,
            height,
            Mathf.Cos(rad) * distance
        );

        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }
}