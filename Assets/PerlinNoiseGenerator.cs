using UnityEngine;

public class PerlinNoiseGenerator : MonoBehaviour
{
    [Header("Noise")]
    public float scale = 8f;
    public float heightMultiplier = 5f;
    [Range(0f, 1f)]
    public float wallThreshold = 0.5f;

    public float offsetX;
    public float offsetY;

    void Awake()
    {
        offsetX = Random.Range(-10000f, 10000f);
        offsetY = Random.Range(-10000f, 10000f);
    }

}