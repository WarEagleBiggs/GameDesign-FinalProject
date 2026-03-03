using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class Minimap : MonoBehaviour
{
    [Header("Target (World Center)")]
    public Transform target;

    [Header("Orbit Settings")]
    public float distance = 80f;
    public float tiltAngle = 90f; // 90 = straight down
    public float baseYaw = 45f;   // starting angle (45, 135, 225, 315)

    [Header("Match Main Camera")]
    public Slider rotationSlider; // SAME slider as main camera

    void LateUpdate()
    {
        if (target == null) return;

        UpdateRotation();
    }

    void UpdateRotation()
    {
        float t = rotationSlider != null ? rotationSlider.value : 0f;

        // Convert slider (0–1) into 4 fixed indices
        int index = Mathf.RoundToInt(t * 3f);
        index = Mathf.Clamp(index, 0, 3);

        // EXACT angles: 45, 135, 225, 315
        float yaw = baseYaw + (index * 90f);

        Quaternion rotation = Quaternion.Euler(tiltAngle, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }
}