using UnityEngine;

public class MainMenuGen : MonoBehaviour
{
    [Header("World Size")]
    [Range(4, 128)]
    public int tileCount = 40;
    [SerializeField] private float mapSize = 40f;

    [Header("World Seed")]
    public int worldSeed = 12345;

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

    [Header("Spin")]
    public bool spinChunk = true;
    public float spinSpeed = 10f;

    [Header("Debug")]
    public bool doGen;

    private Transform[,] tiles;
    private bool[,] isGreen;

    void Start()
    {
        GenerateWithSeed(worldSeed);
    }

    void Update()
    {
        if (doGen)
        {
            doGen = false;
            GenerateWithSeed(worldSeed);
        }

        if (spinChunk)
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }
    }

    void GenerateWithSeed(int seed)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        tiles = new Transform[tileCount, tileCount];
        isGreen = new bool[tileCount, tileCount];

        float tileSizeWorld = mapSize / tileCount;
        float half = mapSize * 0.5f;

        float offsetX = seed * 0.0001f;
        float offsetZ = seed * 0.00013f;

        for (int x = 0; x < tileCount; x++)
        {
            for (int z = 0; z < tileCount; z++)
            {
                float xPos = -half + tileSizeWorld * 0.5f + x * tileSizeWorld;
                float zPos = -half + tileSizeWorld * 0.5f + z * tileSizeWorld;

                float noise = Mathf.PerlinNoise(
                    (x / noiseScale) + offsetX,
                    (z / noiseScale) + offsetZ
                );

                float extraHeight = 0f;
                float normalizedHeight = 0f;

                bool isBlocked = noise >= wallThreshold;

                if (isBlocked)
                {
                    normalizedHeight = (noise - wallThreshold) / (1f - wallThreshold);
                    extraHeight = normalizedHeight * heightMultiplier;
                }

                float totalHeight = tileSizeWorld + extraHeight;

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

                Material picked = PickMaterial(normalizedHeight, noise);
                Renderer rend = cube.GetComponent<Renderer>();
                if (rend != null)
                    rend.sharedMaterial = picked != null ? picked : baseMat;

                cube.transform.SetParent(transform, false);
                cube.transform.localScale = new Vector3(tileSizeWorld, totalHeight, tileSizeWorld);
                cube.transform.localPosition = new Vector3(xPos, totalHeight * 0.5f, zPos);
                cube.name = $"Tile_{x}_{z}";

                tiles[x, z] = cube.transform;
                isGreen[x, z] = picked == greenMatA || picked == greenMatB || picked == greenMatC;
            }
        }
    }

    Material PickMaterial(float normalizedHeight, float noise01)
    {
        if (normalizedHeight <= 0f)
        {
            float t = Mathf.Clamp01(noise01 / wallThreshold);
            int idx = Mathf.FloorToInt(t * 3f);
            if (idx > 2) idx = 2;

            if (idx == 0) return greenMatA;
            if (idx == 1) return greenMatB;
            return greenMatC;
        }

        if (normalizedHeight < 0.33f) return brownMat;
        if (normalizedHeight < 0.66f) return greyMat;
        return whiteMat;
    }
}