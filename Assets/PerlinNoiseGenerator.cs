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

    public float GetWallHeight(float x, float z)
    {
        float xCoord = (x / scale) + offsetX;
        float zCoord = (z / scale) + offsetY;

        float noise = Mathf.PerlinNoise(xCoord, zCoord);

        if (noise < wallThreshold)
            return 0f;   

        float normalized = (noise - wallThreshold) / (1f - wallThreshold);
        return normalized * heightMultiplier;
    }
}