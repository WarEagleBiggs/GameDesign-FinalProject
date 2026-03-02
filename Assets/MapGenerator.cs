using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("World Size")]
    [Range(4, 64)]
    public int tileCount = 20;          // default 20x20
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

    [Header("Player")]
    public Material playerMat;

    [Header("Camera Focus")]
    public bool focusCameraOnPlayer;
    public CameraOrbit cameraOrbit;

    [Header("Spawn Rules")]
    public bool require3x3GreenForStart = true;
    public bool require3x3GreenForEdgeRespawn = false;

    [Header("Debug")]
    public bool doGen;

    public enum EntryDirection { North, South, East, West }

    private GameObject playerObj;

    private Transform[,] tiles;
    private bool[,] isGreen;

    void Start()
    {
        Generate();
        SpawnPlayerOnSafeGreenCell();
        ApplyCameraTargetIfEnabled();
    }

    void Update()
    {
        if (doGen)
        {
            doGen = false;
            Generate();
            SpawnPlayerOnSafeGreenCell();
            ApplyCameraTargetIfEnabled();
        }
    }

    // Call this from your movement/transition code when entering a new chunk.
    public void RegenerateAndPlacePlayerOnEdge(EntryDirection entryDir)
    {
        Generate();
        SpawnPlayerOnEdge(entryDir);
        ApplyCameraTargetIfEnabled();
    }

    public void Generate()
    {
        int seed = Random.Range(int.MinValue, int.MaxValue);

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

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

                if (noise >= wallThreshold)
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

                bool green = (picked == greenMatA) || (picked == greenMatB) || (picked == greenMatC);
                isGreen[x, z] = green;
            }
        }
    }

    void SpawnPlayerOnSafeGreenCell()
    {
        if (tiles == null || isGreen == null) return;

        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 0; x < tileCount; x++)
        {
            for (int z = 0; z < tileCount; z++)
            {
                if (!isGreen[x, z]) continue;
                if (!require3x3GreenForStart || AllNeighborsGreen3x3(x, z))
                    candidates.Add(new Vector2Int(x, z));
            }
        }

        if (candidates.Count == 0) return;

        Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
        PlacePlayerOnTile(tiles[chosen.x, chosen.y]);
    }

    void SpawnPlayerOnEdge(EntryDirection entryDir)
    {
        if (tiles == null || isGreen == null) return;

        List<Vector2Int> edge = GetEdgeCoords(entryDir);

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int i = 0; i < edge.Count; i++)
        {
            int x = edge[i].x;
            int z = edge[i].y;

            if (!isGreen[x, z]) continue;
            if (!require3x3GreenForEdgeRespawn || AllNeighborsGreen3x3(x, z))
                candidates.Add(edge[i]);
        }

        if (candidates.Count == 0)
        {
            // fallback: any green tile (still avoids walls)
            List<Vector2Int> greens = new List<Vector2Int>();
            for (int x = 0; x < tileCount; x++)
                for (int z = 0; z < tileCount; z++)
                    if (isGreen[x, z]) greens.Add(new Vector2Int(x, z));

            if (greens.Count == 0) return;
            Vector2Int chosenAny = greens[Random.Range(0, greens.Count)];
            PlacePlayerOnTile(tiles[chosenAny.x, chosenAny.y]);
            return;
        }

        Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
        PlacePlayerOnTile(tiles[chosen.x, chosen.y]);
    }

    List<Vector2Int> GetEdgeCoords(EntryDirection entryDir)
    {
        // Interpret entryDir as the side the player comes FROM, so spawn on that edge.
        List<Vector2Int> coords = new List<Vector2Int>();

        if (entryDir == EntryDirection.North)
        {
            int z = tileCount - 1;
            for (int x = 0; x < tileCount; x++) coords.Add(new Vector2Int(x, z));
        }
        else if (entryDir == EntryDirection.South)
        {
            int z = 0;
            for (int x = 0; x < tileCount; x++) coords.Add(new Vector2Int(x, z));
        }
        else if (entryDir == EntryDirection.East)
        {
            int x = tileCount - 1;
            for (int z = 0; z < tileCount; z++) coords.Add(new Vector2Int(x, z));
        }
        else // West
        {
            int x = 0;
            for (int z = 0; z < tileCount; z++) coords.Add(new Vector2Int(x, z));
        }

        return coords;
    }

    bool AllNeighborsGreen3x3(int cx, int cz)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                int nx = cx + dx;
                int nz = cz + dz;

                if (nx < 0 || nz < 0 || nx >= tileCount || nz >= tileCount)
                    return false;

                if (!isGreen[nx, nz])
                    return false;
            }
        }
        return true;
    }

    void PlacePlayerOnTile(Transform tile)
    {
        float tileSizeWorld = mapSize / tileCount;

        if (playerObj == null)
        {
            playerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerObj.name = "Player";

            Renderer pr = playerObj.GetComponent<Renderer>();
            if (pr != null && playerMat != null)
                pr.sharedMaterial = playerMat;

            // Add Player script automatically
            Player playerScript = playerObj.AddComponent<Player>();
            playerScript.mapGen = this;
            playerScript.cam = Camera.main;
        }

        playerObj.transform.localScale = Vector3.one * tileSizeWorld;

        float tileTopY = tile.position.y + (tile.lossyScale.y * 0.5f);
        float playerHalf = tileSizeWorld * 0.5f;

        playerObj.transform.position = new Vector3(
            tile.position.x,
            tileTopY + playerHalf,
            tile.position.z
        );
    }

    void ApplyCameraTargetIfEnabled()
    {
        if (!focusCameraOnPlayer) return;
        if (cameraOrbit == null) return;
        if (playerObj == null) return;

        cameraOrbit.target = playerObj.transform;
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