using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("Fixed Size")]
    private const int TileSize = 20;
    [SerializeField] private float mapSize = 10f;

    [Header("Seed")]
    public int seed = 12345;

    [Header("Noise")]
    public float noiseScale = 6f;
    public float heightMultiplier = 4f;
    [Range(0f, 1f)]
    public float wallThreshold = 0.5f;

    public Material cubeMat;

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
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        float tileSizeWorld = mapSize / TileSize;
        float half = mapSize * 0.5f;

        // Deterministic offsets from seed
        float offsetX = seed * 0.137f;
        float offsetZ = seed * 0.173f;

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

                if (noise >= wallThreshold)
                {
                    float normalized = (noise - wallThreshold) / (1f - wallThreshold);
                    extraHeight = normalized * heightMultiplier;
                }

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

                Renderer rend = cube.GetComponent<Renderer>();
                if (rend != null && cubeMat != null)
                    rend.sharedMaterial = cubeMat;

                cube.transform.SetParent(transform, false);

                float totalHeight = tileSizeWorld + extraHeight;

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
}