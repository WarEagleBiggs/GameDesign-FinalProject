using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MapGenerator : MonoBehaviour
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

    [Header("Mountains Ground Variants")]
    public Material greenMatA;
    public Material greenMatB;
    public Material greenMatC;

    [Header("Mountain Elevation Materials")]
    public Material brownMat;
    public Material greyMat;
    public Material whiteMat;

    [Header("Plains")]
    public GameObject plainsTreePrefab;
    [Range(0f, 1f)]
    public float plainsTreeChance = 0.035f;

    [Header("Forest")]
    public GameObject forestTreePrefab;
    public float forestTreeNoiseScale = 5f;
    [Range(0f, 1f)]
    public float forestTreeThreshold = 0.58f;
    [Range(0f, 1f)]
    public float forestPathThreshold = 0.47f;

    [Header("Desert Ground Variants")]
    public Material desertMatA;
    public Material desertMatB;
    public Material desertMatC;

    [Header("Desert")]
    public GameObject cactusPrefab;
    [Range(0f, 1f)]
    public float desertCactusChance = 0.03f;

    [Header("Debug Materials")]
    public Material blockedRedMat;

    [Header("Player Settings")]
    public Material playerMat;
    public Material highlightBlueMat;
    public Material hoverSelectedMat;
    [Range(1, 20)]
    public int playerMoveRange = 3;
    public bool allowDiagonalMovement = true;

    [Header("Camera Focus")]
    public bool focusCameraOnPlayer;
    public CameraOrbit cameraOrbit;

    [Header("Spawn Rules")]
    public bool require3x3GreenForStart = true;

    [Header("Tile Layers (create these in Project Settings > Tags and Layers)")]
    public string walkableLayerName = "Walkable";
    public string blockedLayerName = "Blocked";

    [Header("UI")]
    public TextMeshProUGUI chunkText;

    [Header("Debug")]
    public bool doGen;

    public enum EntryDirection { North, South, East, West }
    public enum BiomeType { Mountains, Plains, Desert, Forest }

    private GameObject playerObj;

    private Transform[,] tiles;
    private bool[,] isGreen;

    private Vector2Int currentChunk = Vector2Int.zero;
    private BiomeType currentBiome = BiomeType.Plains;

    private int walkableLayer = -1;
    private int blockedLayer = -1;
    private int ignoreRaycastLayer = -1;

    void Awake()
    {
        walkableLayer = LayerMask.NameToLayer(walkableLayerName);
        blockedLayer = LayerMask.NameToLayer(blockedLayerName);
        ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
    }

    void Start()
    {
        LoadChunk(currentChunk, null, null);
        UpdateChunkUI();
        ApplyCameraTargetIfEnabled();
    }

    void Update()
    {
        if (doGen)
        {
            doGen = false;
            LoadChunk(currentChunk, null, null);
            UpdateChunkUI();
            ApplyCameraTargetIfEnabled();
        }
    }

    void UpdateChunkUI()
    {
        if (chunkText != null)
            chunkText.SetText($"CHUNK: {currentChunk.x}, {currentChunk.y}\nBIOME: {currentBiome}");
    }

    public void TravelToNeighborChunk(EntryDirection exitSide, int exitX, int exitZ)
    {
        if (exitSide == EntryDirection.West) currentChunk += new Vector2Int(-1, 0);
        if (exitSide == EntryDirection.East) currentChunk += new Vector2Int(1, 0);
        if (exitSide == EntryDirection.South) currentChunk += new Vector2Int(0, -1);
        if (exitSide == EntryDirection.North) currentChunk += new Vector2Int(0, 1);

        EntryDirection entrySide =
            exitSide == EntryDirection.West ? EntryDirection.East :
            exitSide == EntryDirection.East ? EntryDirection.West :
            exitSide == EntryDirection.South ? EntryDirection.North :
                                              EntryDirection.South;

        int entryX = exitX;
        int entryZ = exitZ;

        if (exitSide == EntryDirection.East) entryX = 0;
        if (exitSide == EntryDirection.West) entryX = tileCount - 1;
        if (exitSide == EntryDirection.North) entryZ = 0;
        if (exitSide == EntryDirection.South) entryZ = tileCount - 1;

        LoadChunk(currentChunk, entrySide, new Vector2Int(entryX, entryZ));
        UpdateChunkUI();
        ApplyCameraTargetIfEnabled();
    }

    void LoadChunk(Vector2Int chunkCoord, EntryDirection? entrySide, Vector2Int? desiredEntry)
    {
        int chunkSeed = GetChunkSeed(chunkCoord.x, chunkCoord.y);
        currentBiome = GetBiomeForChunk(chunkCoord, chunkSeed);

        GenerateWithSeed(chunkCoord, chunkSeed, currentBiome);

        if (desiredEntry.HasValue && entrySide.HasValue)
            SpawnPlayerAtDesiredOrEdgeFallback(desiredEntry.Value, entrySide.Value);
        else
            SpawnPlayerOnSafeGreenCell();
    }

    BiomeType GetBiomeForChunk(Vector2Int chunkCoord, int chunkSeed)
    {
        if (chunkCoord == Vector2Int.zero)
        {
            int startPick = Mathf.Abs(worldSeed) % 2;
            return startPick == 0 ? BiomeType.Plains : BiomeType.Forest;
        }

        int h = Mathf.Abs(chunkSeed);
        int v = h % 4;

        if (v == 0) return BiomeType.Forest;
        if (v == 1) return BiomeType.Plains;
        if (v == 2) return BiomeType.Mountains;
        return BiomeType.Desert;
    }

    int GetChunkSeed(int chunkX, int chunkZ)
    {
        unchecked
        {
            int h = worldSeed;
            h = (h * 397) ^ chunkX;
            h = (h * 397) ^ chunkZ;
            h ^= (h << 13);
            h ^= (h >> 17);
            h ^= (h << 5);
            return h;
        }
    }

    void GenerateWithSeed(Vector2Int chunkCoord, int seed, BiomeType biome)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (playerObj != null && c.gameObject == playerObj) continue;
            Destroy(c.gameObject);
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

                if (biome == BiomeType.Mountains)
                    GenerateMountainTile(x, z, xPos, zPos, tileSizeWorld, offsetX, offsetZ);
                else if (biome == BiomeType.Plains)
                    GeneratePlainsTile(x, z, xPos, zPos, tileSizeWorld, offsetX, offsetZ, seed);
                else if (biome == BiomeType.Desert)
                    GenerateDesertTile(x, z, xPos, zPos, tileSizeWorld, offsetX, offsetZ, seed);
                else
                    GenerateForestTile(x, z, xPos, zPos, tileSizeWorld, offsetX, offsetZ, seed);
            }
        }

        EnsurePlayerExistsAndHooked();
    }

    void GenerateMountainTile(int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ)
    {
        float noise = Mathf.PerlinNoise(
            (x / noiseScale) + offsetX,
            (z / noiseScale) + offsetZ
        );

        float extraHeight = 0f;
        float normalizedHeight = 0f;

        bool blocked = noise >= wallThreshold;

        if (blocked)
        {
            normalizedHeight = (noise - wallThreshold) / (1f - wallThreshold);
            extraHeight = normalizedHeight * heightMultiplier;
        }

        float totalHeight = tileSizeWorld + extraHeight;

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Material picked = PickMountainMaterial(normalizedHeight, noise);
        Renderer rend = cube.GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial = picked != null ? picked : baseMat;

        cube.transform.SetParent(transform, false);
        cube.transform.localScale = new Vector3(tileSizeWorld, totalHeight, tileSizeWorld);
        cube.transform.localPosition = new Vector3(xPos, totalHeight * 0.5f, zPos);
        cube.name = $"Tile_{x}_{z}";

        if (blocked)
        {
            if (blockedLayer != -1) cube.layer = blockedLayer;
            isGreen[x, z] = false;
        }
        else
        {
            if (walkableLayer != -1) cube.layer = walkableLayer;
            isGreen[x, z] = true;
        }

        tiles[x, z] = cube.transform;
    }

    void GeneratePlainsTile(int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ, int seed)
    {
        float groundNoise = Mathf.PerlinNoise(
            (x / noiseScale) + offsetX,
            (z / noiseScale) + offsetZ
        );

        float totalHeight = tileSizeWorld;

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Material picked = PickPlainsGroundMaterial(groundNoise);
        Renderer rend = cube.GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial = picked != null ? picked : baseMat;

        cube.transform.SetParent(transform, false);
        cube.transform.localScale = new Vector3(tileSizeWorld, totalHeight, tileSizeWorld);
        cube.transform.localPosition = new Vector3(xPos, totalHeight * 0.5f, zPos);
        cube.name = $"Tile_{x}_{z}";

        if (walkableLayer != -1) cube.layer = walkableLayer;

        tiles[x, z] = cube.transform;
        isGreen[x, z] = true;

        float treeNoise = Mathf.PerlinNoise(
            (x * 0.31f) + seed * 0.00031f,
            (z * 0.31f) + seed * 0.00047f
        );

        bool spawnTree = plainsTreePrefab != null && treeNoise > 1f - plainsTreeChance;
        if (spawnTree)
        {
            CreatePropOnTile(plainsTreePrefab, cube.transform);
            MarkTileBlocked(x, z);
        }
    }

    void GenerateDesertTile(int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ, int seed)
    {
        float groundNoise = Mathf.PerlinNoise(
            (x / noiseScale) + offsetX,
            (z / noiseScale) + offsetZ
        );

        float totalHeight = tileSizeWorld;

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Material picked = PickDesertGroundMaterial(groundNoise);
        Renderer rend = cube.GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial = picked != null ? picked : baseMat;

        cube.transform.SetParent(transform, false);
        cube.transform.localScale = new Vector3(tileSizeWorld, totalHeight, tileSizeWorld);
        cube.transform.localPosition = new Vector3(xPos, totalHeight * 0.5f, zPos);
        cube.name = $"Tile_{x}_{z}";

        if (walkableLayer != -1) cube.layer = walkableLayer;

        tiles[x, z] = cube.transform;
        isGreen[x, z] = true;

        float cactusNoise = Mathf.PerlinNoise(
            (x * 0.29f) + seed * 0.00061f,
            (z * 0.29f) + seed * 0.00079f
        );

        if (cactusPrefab != null && cactusNoise > 1f - desertCactusChance)
            CreatePropOnTile(cactusPrefab, cube.transform);
    }

    void GenerateForestTile(int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ, int seed)
    {
        float groundNoise = Mathf.PerlinNoise(
            (x / noiseScale) + offsetX,
            (z / noiseScale) + offsetZ
        );

        float totalHeight = tileSizeWorld;

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Material picked = PickForestGroundMaterial(groundNoise);
        Renderer rend = cube.GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial = picked != null ? picked : baseMat;

        cube.transform.SetParent(transform, false);
        cube.transform.localScale = new Vector3(tileSizeWorld, totalHeight, tileSizeWorld);
        cube.transform.localPosition = new Vector3(xPos, totalHeight * 0.5f, zPos);
        cube.name = $"Tile_{x}_{z}";

        if (walkableLayer != -1) cube.layer = walkableLayer;

        tiles[x, z] = cube.transform;
        isGreen[x, z] = true;

        float forestNoise = Mathf.PerlinNoise(
            (x / forestTreeNoiseScale) + seed * 0.00021f,
            (z / forestTreeNoiseScale) + seed * 0.00037f
        );

        bool isOpenPath = forestNoise < forestPathThreshold;
        bool spawnTree = forestNoise > forestTreeThreshold;

        if (!isOpenPath && spawnTree && forestTreePrefab != null)
        {
            CreatePropOnTile(forestTreePrefab, cube.transform);
            MarkTileBlocked(x, z);
        }
    }

    void MarkTileBlocked(int x, int z)
    {
        if (x < 0 || z < 0 || x >= tileCount || z >= tileCount) return;
        if (tiles == null || tiles[x, z] == null) return;

        if (blockedLayer != -1)
            tiles[x, z].gameObject.layer = blockedLayer;

        isGreen[x, z] = false;
    }

    void CreatePropOnTile(GameObject prefab, Transform tile)
    {
        GameObject obj = Instantiate(prefab, transform);
        obj.name = prefab.name;

        float tileTopY = tile.localPosition.y + (tile.localScale.y * 0.5f);

        obj.transform.localPosition = new Vector3(
            tile.localPosition.x,
            tileTopY,
            tile.localPosition.z
        );

        obj.transform.localRotation = Quaternion.identity;

        Collider[] cols = obj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;

        if (ignoreRaycastLayer != -1)
            SetLayerRecursively(obj, ignoreRaycastLayer);
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        for (int i = 0; i < obj.transform.childCount; i++)
            SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
    }

    void EnsurePlayerExistsAndHooked()
    {
        float tileSizeWorld = mapSize / tileCount;

        if (playerObj == null)
        {
            playerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerObj.name = "Player";
            playerObj.layer = LayerMask.NameToLayer("XRay");
        }

        playerObj.transform.localScale = Vector3.one * tileSizeWorld;

        Renderer pr = playerObj.GetComponent<Renderer>();
        if (pr != null && playerMat != null)
            pr.sharedMaterial = playerMat;

        Player p = playerObj.GetComponent<Player>();
        if (p == null) p = playerObj.AddComponent<Player>();

        p.mapGen = this;
        p.cam = Camera.main;
        p.highlightBlueMat = highlightBlueMat;
        p.hoverSelectedMat = hoverSelectedMat;
        p.moveRange = playerMoveRange;
        p.allowDiagonal = allowDiagonalMovement;

        TileCutout cut = FindObjectOfType<TileCutout>();
        if (cut != null) cut.target = playerObj.transform;
    }

    void SpawnPlayerOnSafeGreenCell()
    {
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

        int pick = Mathf.Abs(GetChunkSeed(currentChunk.x, currentChunk.y) + 17) % candidates.Count;
        Vector2Int chosen = candidates[pick];
        PlacePlayerOnTile(tiles[chosen.x, chosen.y]);
    }

    void SpawnPlayerAtDesiredOrEdgeFallback(Vector2Int desired, EntryDirection entrySide)
    {
        int x = Mathf.Clamp(desired.x, 0, tileCount - 1);
        int z = Mathf.Clamp(desired.y, 0, tileCount - 1);

        if (isGreen[x, z])
        {
            PlacePlayerOnTile(tiles[x, z]);
            return;
        }

        Renderer rr = tiles[x, z].GetComponent<Renderer>();
        if (rr != null && blockedRedMat != null)
            rr.sharedMaterial = blockedRedMat;

        List<Vector2Int> edge = GetEdgeCoords(entrySide);

        edge.Sort((a, b) =>
        {
            int da = Mathf.Abs(a.x - x) + Mathf.Abs(a.y - z);
            int db = Mathf.Abs(b.x - x) + Mathf.Abs(b.y - z);
            return da.CompareTo(db);
        });

        for (int i = 0; i < edge.Count; i++)
        {
            int ex = edge[i].x;
            int ez = edge[i].y;
            if (isGreen[ex, ez])
            {
                PlacePlayerOnTile(tiles[ex, ez]);
                return;
            }
        }

        for (int r = 1; r < tileCount; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;

                    int nx = x + dx;
                    int nz = z + dz;

                    if (nx < 0 || nz < 0 || nx >= tileCount || nz >= tileCount) continue;
                    if (!isGreen[nx, nz]) continue;

                    PlacePlayerOnTile(tiles[nx, nz]);
                    return;
                }
            }
        }
    }

    List<Vector2Int> GetEdgeCoords(EntryDirection edgeSide)
    {
        List<Vector2Int> coords = new List<Vector2Int>();

        if (edgeSide == EntryDirection.North)
        {
            int z = tileCount - 1;
            for (int x = 0; x < tileCount; x++) coords.Add(new Vector2Int(x, z));
        }
        else if (edgeSide == EntryDirection.South)
        {
            int z = 0;
            for (int x = 0; x < tileCount; x++) coords.Add(new Vector2Int(x, z));
        }
        else if (edgeSide == EntryDirection.East)
        {
            int x = tileCount - 1;
            for (int z = 0; z < tileCount; z++) coords.Add(new Vector2Int(x, z));
        }
        else
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
        float tileTopY = tile.position.y + (tile.lossyScale.y * 0.5f);
        float playerHalf = playerObj.transform.lossyScale.y * 0.5f;

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

    Material PickMountainMaterial(float normalizedHeight, float noise01)
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

    Material PickPlainsGroundMaterial(float noise01)
    {
        int idx = Mathf.FloorToInt(noise01 * 3f);
        if (idx > 2) idx = 2;

        if (idx == 0) return greenMatA;
        if (idx == 1) return greenMatB;
        return greenMatC;
    }

    Material PickForestGroundMaterial(float noise01)
    {
        int idx = Mathf.FloorToInt(noise01 * 3f);
        if (idx > 2) idx = 2;

        if (idx == 0) return greenMatA;
        if (idx == 1) return greenMatB;
        return greenMatC;
    }

    Material PickDesertGroundMaterial(float noise01)
    {
        int idx = Mathf.FloorToInt(noise01 * 3f);
        if (idx > 2) idx = 2;

        if (idx == 0) return desertMatA;
        if (idx == 1) return desertMatB;
        return desertMatC;
    }
}