using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    private const int TileSize = 20;
    [SerializeField] private float mapSize = 10f;

    [Header("Noise")]
    public float noiseScale = 6f;
    public float heightMultiplier = 6f;
    [Range(0f, 1f)]
    public float wallThreshold = 0.5f;

    [Header("Materials")]
    public Material baseMat;

    [Header("Ground Variants")]
    public Material greenMatA;
    public Material greenMatB;
    public Material greenMatC;

    [Header("Elevation Materials")]
    public Material brownMat;
    public Material greyMat;
    public Material whiteMat;

    public bool doGen;

    void Update()
    {
        if (doGen)
        {
            doGen = false;
            Generate();
        }
    }

    public void Generate()
    {
        int seed = Random.Range(int.MinValue, int.MaxValue);

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        float tileSizeWorld = mapSize / TileSize;
        float half = mapSize * 0.5f;

        float offsetX = seed * 0.0001f;
        float offsetZ = seed * 0.00013f;

        for (int x = 0; x < TileSize; x++)
        {
            for (int z = 0; z < TileSize; z++)
            {
                float xPos = -half + tileSizeWorld * 0.5f + x * tileSizeWorld;
                float zPos = -half + tileSizeWorld * 0.5f + z * tileSizeWorld;

                float noise = Mathf.PerlinNoise(
                    (x / noiseScale) + offsetX,
                    (z / noiseScale) + offsetZ
                );

                float extraHeight = 0f;
                float normalizedHeight = 0f;

                if (noise >= wallThreshold)
                {
                    normalizedHeight = (noise - wallThreshold) / (1f - wallThreshold);
                    extraHeight = normalizedHeight * heightMultiplier;
                }

                float totalHeight = tileSizeWorld + extraHeight;

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

                Renderer rend = cube.GetComponent<Renderer>();
                if (rend != null)
                {
                    Material picked = PickMaterial(normalizedHeight, noise);
                    rend.sharedMaterial = picked != null ? picked : baseMat;
                }

                cube.transform.SetParent(transform, false);

                cube.transform.localScale = new Vector3(
                    tileSizeWorld,
                    totalHeight,
                    tileSizeWorld
                );

                cube.transform.localPosition = new Vector3(
                    xPos,
                    totalHeight * 0.5f,
                    zPos
                );

                cube.name = $"Tile_{x}_{z}";
            }
        }
    }

    Material PickMaterial(float normalizedHeight, float noise01)
    {
        // Ground
        if (normalizedHeight <= 0f)
        {
            float t = Mathf.Clamp01(noise01 / wallThreshold);
            int idx = Mathf.FloorToInt(t * 3f);
            if (idx > 2) idx = 2;

            if (idx == 0) return greenMatA;
            if (idx == 1) return greenMatB;
            return greenMatC;
        }

        // Elevation bands
        if (normalizedHeight < 0.33f) return brownMat;
        if (normalizedHeight < 0.66f) return greyMat;
        return whiteMat;
    }
}