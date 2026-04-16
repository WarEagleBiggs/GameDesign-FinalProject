using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MapGenerator : MonoBehaviour
{
    [Header("Player STATS")] 
    public int coins = 0;
    public int level = 0;
    
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
    
    [Header("River")]
    public Material waterMatA;
    public Material waterMatB;
    public Material waterMatC;
    public float riverNoiseScale = 7f;
    [Range(0.01f, 0.3f)]
    public float riverWidth = 0.08f;
    public float riverWiggleScale = 3.5f;
    public float riverDepth = 0.35f;

        [Header("Structures")]
        public GameObject cabinPrefab;
        public GameObject towerPrefab;
        public GameObject chestPrefab;
        public GameObject moss1Prefab;
        public GameObject moss2Prefab;
        [Range(0f, 1f)] public float chunkHasStructureChance = 0.35f;
        [Range(1, 8)] public int maxStructuresPerChunk = 8;
        [Range(1, 50)] public int maxStructureAttemptsPerChunk = 20;

    [Header("Neighbor Preview")]
    public bool showNeighborPreviews = true;
    [Range(0.05f, 1f)]
    public float previewAlpha = 0.35f;
    public float previewVerticalOffset = 0f;

    [Header("Debug Materials")]
    public Material blockedRedMat;

    [Header("Player Settings")]
    public GameObject playerPrefab;
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

    [Header("Enemy Settings")]
    [Range(0f, 1f)]
    public float enemySpawnChance = 0.7f;
    [Range(1, 3)]
    public int minEnemiesPerChunk = 1;
    [Range(1, 3)]
    public int maxEnemiesPerChunk = 3;
    [Range(1, 10)]
    public int enemyAttackRange = 2;
    public int enemyDamage = 1;
    public float enemyYOffset = 0f;

    [Header("Move Trail")]
    public float moveTrailDuration = 1.2f;
    public Color moveTrailColor = new Color(0.7f, 0.55f, 0.35f, 1f);
    public Color enemyTrailColor = new Color(0.78f, 0.35f, 0.35f, 1f);

    [Header("Tile Layers (create these in Project Settings > Tags and Layers)")]
    public string walkableLayerName = "Walkable";
    public string blockedLayerName = "Blocked";

    [Header("UI")]
    public TextMeshProUGUI chunkText;
    public TextMeshProUGUI coinsText;
    public Image[] playerHeartImages;
    public Color fullHeartColor = Color.white;
    public Color emptyHeartColor = new Color(1f, 1f, 1f, 0.2f);

    [Header("Death Screen")]
    public Color deathOverlayColor = new Color(0f, 0f, 0f, 0.75f);
    public Color deathTitleColor = new Color(0.9f, 0.12f, 0.12f, 1f);
    public Color deathButtonColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
    public Color deathButtonTextColor = Color.white;

    [Header("Debug")]
    public bool doGen;

    public enum EntryDirection { North, South, East, West }
    public enum BiomeType { Mountains, Plains, Desert, Forest, River }

    private GameObject playerObj;
    private Transform previewRoot;
    private readonly List<Enemy> enemies = new List<Enemy>();

    private Transform[,] tiles;
    private bool[,] isGreen;

    private Vector2Int currentChunk = Vector2Int.zero;
    public BiomeType currentBiome = BiomeType.Plains;

    private int walkableLayer = -1;
    private int blockedLayer = -1;
    private int ignoreRaycastLayer = -1;

    private readonly Dictionary<Material, Material> previewMaterialCache = new Dictionary<Material, Material>();
    private readonly List<TrailTileState> trailTiles = new List<TrailTileState>();
    private GameObject deathScreenRoot;
    private TextMeshProUGUI deathTitleText;
    private Button deathMainMenuButton;
    private Material moveTrailMaterial;
    private Material enemyTrailMaterial;

    class TrailTileState
    {
        public Renderer renderer;
        public Material originalMaterial;
        public Material trailMaterial;
        public float expiresAt;
    }

    void Awake()
    {
        walkableLayer = LayerMask.NameToLayer(walkableLayerName);
        blockedLayer = LayerMask.NameToLayer(blockedLayerName);
        ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        EnsurePreviewRoot();
    }

    void Start()
    {
        EnsurePlayerHeartRefs();
        EnsureDeathScreen();
        SetDeathScreenVisible(false);

        if (WorldSeedManager.Instance != null)
            worldSeed = WorldSeedManager.Instance.selectedSeed;

        LoadChunk(currentChunk, null, null);
        UpdateChunkUI();
        ApplyCameraTargetIfEnabled();
    }

    void Update()
    {
        if (doGen)
        {
            doGen = false;

            if (WorldSeedManager.Instance != null)
                worldSeed = WorldSeedManager.Instance.selectedSeed;

            LoadChunk(currentChunk, null, null);
            UpdateChunkUI();
            ApplyCameraTargetIfEnabled();
        }
        
        coinsText.SetText(coins.ToString());
        UpdateMoveTrail();
        UpdateEnemyAttackIndicators();
    }

    void EnsurePlayerHeartRefs()
    {
        if (playerHeartImages != null && playerHeartImages.Length > 0)
            return;

        GameObject heartsRoot = GameObject.Find("Hearts");
        if (heartsRoot == null) return;

        List<Image> foundHearts = new List<Image>();
        for (int i = 0; i < heartsRoot.transform.childCount; i++)
        {
            Image heart = heartsRoot.transform.GetChild(i).GetComponent<Image>();
            if (heart != null)
                foundHearts.Add(heart);
        }

        playerHeartImages = foundHearts.ToArray();
    }

    public void UpdatePlayerHealthUI(int currentHearts, int maxHearts)
    {
        EnsurePlayerHeartRefs();
        if (playerHeartImages == null || playerHeartImages.Length == 0) return;

        int visibleHearts = Mathf.Clamp(currentHearts, 0, maxHearts);

        for (int i = 0; i < playerHeartImages.Length; i++)
        {
            if (playerHeartImages[i] == null) continue;
            playerHeartImages[i].color = i < visibleHearts ? fullHeartColor : emptyHeartColor;
        }
    }

    void EnsureDeathScreen()
    {
        if (deathScreenRoot != null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        TMP_FontAsset uiFont = GetUIFont();

        deathScreenRoot = new GameObject("DeathScreen");
        deathScreenRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = deathScreenRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = deathScreenRoot.AddComponent<Image>();
        overlay.color = deathOverlayColor;
        overlay.raycastTarget = true;

        GameObject titleObj = new GameObject("DeathTitle");
        titleObj.transform.SetParent(deathScreenRoot.transform, false);

        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 70f);
        titleRect.sizeDelta = new Vector2(900f, 160f);

        deathTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        deathTitleText.text = "YOU DIED";
        deathTitleText.alignment = TextAlignmentOptions.Center;
        deathTitleText.fontSize = 90f;
        deathTitleText.color = deathTitleColor;
        if (uiFont != null) deathTitleText.font = uiFont;

        GameObject buttonObj = new GameObject("MainMenuButton");
        buttonObj.transform.SetParent(deathScreenRoot.transform, false);

        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -35f);
        buttonRect.sizeDelta = new Vector2(340f, 90f);

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = deathButtonColor;

        deathMainMenuButton = buttonObj.AddComponent<Button>();
        ColorBlock colors = deathMainMenuButton.colors;
        colors.normalColor = deathButtonColor;
        colors.highlightedColor = deathButtonColor * 1.1f;
        colors.pressedColor = deathButtonColor * 0.9f;
        colors.selectedColor = deathButtonColor;
        colors.disabledColor = deathButtonColor * 0.7f;
        deathMainMenuButton.colors = colors;
        deathMainMenuButton.targetGraphic = buttonImage;
        deathMainMenuButton.onClick.AddListener(LoadMainMenuFromDeathScreen);

        GameObject buttonTextObj = new GameObject("Label");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);

        RectTransform buttonTextRect = buttonTextObj.AddComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = "MAIN MENU";
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.fontSize = 40f;
        buttonText.color = deathButtonTextColor;
        if (uiFont != null) buttonText.font = uiFont;

        deathScreenRoot.transform.SetAsLastSibling();
    }

    TMP_FontAsset GetUIFont()
    {
        if (chunkText != null && chunkText.font != null) return chunkText.font;
        if (coinsText != null && coinsText.font != null) return coinsText.font;

        TextMeshProUGUI anyText = FindObjectOfType<TextMeshProUGUI>();
        return anyText != null ? anyText.font : null;
    }

    void LoadMainMenuFromDeathScreen()
    {
        SceneManager.LoadScene("MainMenu");
    }

    void SetDeathScreenVisible(bool visible)
    {
        EnsureDeathScreen();
        if (deathScreenRoot == null) return;

        deathScreenRoot.SetActive(visible);
        if (visible)
            deathScreenRoot.transform.SetAsLastSibling();
    }

    public void ShowDeathScreen()
    {
        SetDeathScreenVisible(true);
    }

    Material GetMoveTrailMaterial()
    {
        if (moveTrailMaterial != null) return moveTrailMaterial;

        moveTrailMaterial = CreateTrailMaterial(moveTrailColor);

        return moveTrailMaterial;
    }

    Material GetEnemyTrailMaterial()
    {
        if (enemyTrailMaterial != null) return enemyTrailMaterial;

        enemyTrailMaterial = CreateTrailMaterial(enemyTrailColor);
        return enemyTrailMaterial;
    }

    Material CreateTrailMaterial(Color color)
    {
        Material source = brownMat != null ? brownMat : baseMat;
        if (source == null) return null;

        Material mat = new Material(source);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.color = color;
        return mat;
    }

    public void AddPlayerMoveTrail(Transform tile)
    {
        AddTrail(tile, GetMoveTrailMaterial());
    }

    public void AddEnemyMoveTrail(Transform tile)
    {
        AddTrail(tile, GetEnemyTrailMaterial());
    }

    void AddTrail(Transform tile, Material trailMaterial)
    {
        if (tile == null) return;

        Renderer renderer = tile.GetComponent<Renderer>();
        if (renderer == null) return;

        if (trailMaterial == null) return;

        TrailTileState existing = null;
        for (int i = 0; i < trailTiles.Count; i++)
        {
            if (trailTiles[i].renderer == renderer)
            {
                existing = trailTiles[i];
                break;
            }
        }

        if (existing == null)
        {
            existing = new TrailTileState
            {
                renderer = renderer,
                originalMaterial = renderer.sharedMaterial
            };
            trailTiles.Add(existing);
        }

        existing.trailMaterial = trailMaterial;
        existing.expiresAt = Time.time + moveTrailDuration;
        renderer.sharedMaterial = trailMaterial;
    }

    void UpdateMoveTrail()
    {
        for (int i = trailTiles.Count - 1; i >= 0; i--)
        {
            TrailTileState state = trailTiles[i];
            if (state.renderer == null)
            {
                trailTiles.RemoveAt(i);
                continue;
            }

            if (Time.time < state.expiresAt)
            {
                if (state.trailMaterial != null && state.renderer.sharedMaterial != state.trailMaterial)
                    state.renderer.sharedMaterial = state.trailMaterial;
                continue;
            }

            state.renderer.sharedMaterial = state.originalMaterial;
            trailTiles.RemoveAt(i);
        }
    }

    public void RefreshTrailVisuals()
    {
        UpdateMoveTrail();
    }

    void EnsurePreviewRoot()
    {
        if (previewRoot != null) return;

        Transform found = transform.Find("PreviewRoot");
        if (found != null)
        {
            previewRoot = found;
            return;
        }

        GameObject go = new GameObject("PreviewRoot");
        go.transform.SetParent(transform, false);
        previewRoot = go.transform;
    }

    void ClearPreviewRoot()
    {
        EnsurePreviewRoot();

        for (int i = previewRoot.childCount - 1; i >= 0; i--)
            Destroy(previewRoot.GetChild(i).gameObject);

        foreach (var kvp in previewMaterialCache)
            if (kvp.Value != null) Destroy(kvp.Value);

        previewMaterialCache.Clear();
    }

    void UpdateChunkUI()
    {
        if (chunkText != null)
            chunkText.SetText($"CHUNK: {currentChunk.x}, {currentChunk.y}\nBIOME: {currentBiome}\nLEVEL: 0");
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

        TileCutout cut = FindObjectOfType<TileCutout>();
        if (cut != null)
            cut.enabled = (currentBiome == BiomeType.Mountains);

        GenerateWithSeed(chunkCoord, chunkSeed, currentBiome);

        if (desiredEntry.HasValue && entrySide.HasValue)
            SpawnPlayerAtDesiredOrEdgeFallback(desiredEntry.Value, entrySide.Value);
        else
            SpawnPlayerOnSafeGreenCell();

        SpawnEnemiesForChunk(chunkSeed);
        UpdateEnemyAttackIndicators();
        GenerateNeighborPreviews();
    }

    BiomeType GetBiomeForChunk(Vector2Int chunkCoord, int chunkSeed)
    {
        if (chunkCoord == Vector2Int.zero)
        {
            int startPick = Mathf.Abs(worldSeed) % 2;
            return startPick == 0 ? BiomeType.Plains : BiomeType.Forest;
        }

        int h = Mathf.Abs(chunkSeed);
        int v = h % 5;

        if (v == 0) return BiomeType.Forest;
        if (v == 1) return BiomeType.Plains;
        if (v == 2) return BiomeType.Mountains;
        if (v == 3) return BiomeType.Desert;
        return BiomeType.River;
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
        EnsurePreviewRoot();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (playerObj != null && c.gameObject == playerObj) continue;
            if (previewRoot != null && c == previewRoot) continue;
            Destroy(c.gameObject);
        }

        enemies.Clear();

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
                else if (biome == BiomeType.Forest)
                    GenerateForestTile(x, z, xPos, zPos, tileSizeWorld, offsetX, offsetZ, seed);
                else
                    GenerateRiverTile(x, z, xPos, zPos, tileSizeWorld, offsetX, offsetZ, seed);
            }
        }

        GenerateStructuresForChunk(seed);
        EnsurePlayerExistsAndHooked();
    }

    void GenerateNeighborPreviews()
    {
        ClearPreviewRoot();

        if (!showNeighborPreviews) return;

        GeneratePreviewChunk(currentChunk + new Vector2Int(-1, 0), new Vector3(-mapSize, previewVerticalOffset, 0f));
        GeneratePreviewChunk(currentChunk + new Vector2Int(1, 0), new Vector3(mapSize, previewVerticalOffset, 0f));
        GeneratePreviewChunk(currentChunk + new Vector2Int(0, 1), new Vector3(0f, previewVerticalOffset, mapSize));
        GeneratePreviewChunk(currentChunk + new Vector2Int(0, -1), new Vector3(0f, previewVerticalOffset, -mapSize));

        GeneratePreviewChunk(currentChunk + new Vector2Int(-1, 1), new Vector3(-mapSize, previewVerticalOffset, mapSize));
        GeneratePreviewChunk(currentChunk + new Vector2Int(1, 1), new Vector3(mapSize, previewVerticalOffset, mapSize));
        GeneratePreviewChunk(currentChunk + new Vector2Int(-1, -1), new Vector3(-mapSize, previewVerticalOffset, -mapSize));
        GeneratePreviewChunk(currentChunk + new Vector2Int(1, -1), new Vector3(mapSize, previewVerticalOffset, -mapSize));
    }

    void GeneratePreviewChunk(Vector2Int chunkCoord, Vector3 chunkOffset)
    {
        int seed = GetChunkSeed(chunkCoord.x, chunkCoord.y);
        BiomeType biome = GetBiomeForChunk(chunkCoord, seed);

        GameObject chunkObj = new GameObject($"PreviewChunk_{chunkCoord.x}_{chunkCoord.y}_{biome}");
        chunkObj.transform.SetParent(previewRoot, false);
        chunkObj.transform.localPosition = chunkOffset;

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
                    GeneratePreviewMountainTile(chunkObj.transform, x, z, xPos, zPos, tileSizeWorld, offsetX, offsetZ);
                else if (biome == BiomeType.Plains)
                    GeneratePreviewPlainsTile(chunkObj.transform, x, z, xPos, zPos, tileSizeWorld, offsetX, offsetZ, seed);
                else if (biome == BiomeType.Desert)
                    GeneratePreviewDesertTile(chunkObj.transform, x, z, xPos, zPos, tileSizeWorld, offsetX, offsetZ, seed);
                else if (biome == BiomeType.Forest)
                    GeneratePreviewForestTile(chunkObj.transform, x, z, xPos, zPos, tileSizeWorld, offsetX, offsetZ, seed);
                else
                    GeneratePreviewRiverTile(chunkObj.transform, x, z, xPos, zPos, tileSizeWorld, offsetX, offsetZ, seed);
            }
        }
    }

    void GenerateMountainTile(int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ)
    {
        float noise = Mathf.PerlinNoise((x / noiseScale) + offsetX, (z / noiseScale) + offsetZ);

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
        float groundNoise = Mathf.PerlinNoise((x / noiseScale) + offsetX, (z / noiseScale) + offsetZ);
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

        float treeNoise = Mathf.PerlinNoise((x * 0.31f) + seed * 0.00031f, (z * 0.31f) + seed * 0.00047f);

        bool spawnTree = plainsTreePrefab != null && treeNoise > 1f - plainsTreeChance;
        if (spawnTree)
        {
            CreatePropOnTile(plainsTreePrefab, cube.transform);
            MarkTileBlocked(x, z);
        }
    }

    void GenerateDesertTile(int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ, int seed)
    {
        float groundNoise = Mathf.PerlinNoise((x / noiseScale) + offsetX, (z / noiseScale) + offsetZ);
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

        float cactusNoise = Mathf.PerlinNoise((x * 0.29f) + seed * 0.00061f, (z * 0.29f) + seed * 0.00079f);

        if (cactusPrefab != null && cactusNoise > 1f - desertCactusChance)
            CreatePropOnTile(cactusPrefab, cube.transform);
    }

    void GenerateForestTile(int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ, int seed)
    {
        float groundNoise = Mathf.PerlinNoise((x / noiseScale) + offsetX, (z / noiseScale) + offsetZ);
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

    void GeneratePreviewMountainTile(Transform parent, int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ)
    {
        float noise = Mathf.PerlinNoise((x / noiseScale) + offsetX, (z / noiseScale) + offsetZ);

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
        Collider col = cube.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Material picked = PickMountainMaterial(normalizedHeight, noise);
        ApplyPreviewMaterial(cube, picked != null ? picked : baseMat);

        cube.transform.SetParent(parent, false);
        cube.transform.localScale = new Vector3(tileSizeWorld, totalHeight, tileSizeWorld);
        cube.transform.localPosition = new Vector3(xPos, totalHeight * 0.5f, zPos);
        cube.name = $"PreviewTile_{x}_{z}";
        if (ignoreRaycastLayer != -1) cube.layer = ignoreRaycastLayer;
    }

    void GeneratePreviewPlainsTile(Transform parent, int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ, int seed)
    {
        float groundNoise = Mathf.PerlinNoise((x / noiseScale) + offsetX, (z / noiseScale) + offsetZ);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider col = cube.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Material picked = PickPlainsGroundMaterial(groundNoise);
        ApplyPreviewMaterial(cube, picked != null ? picked : baseMat);

        cube.transform.SetParent(parent, false);
        cube.transform.localScale = new Vector3(tileSizeWorld, tileSizeWorld, tileSizeWorld);
        cube.transform.localPosition = new Vector3(xPos, tileSizeWorld * 0.5f, zPos);
        cube.name = $"PreviewTile_{x}_{z}";
        if (ignoreRaycastLayer != -1) cube.layer = ignoreRaycastLayer;

        float treeNoise = Mathf.PerlinNoise((x * 0.31f) + seed * 0.00031f, (z * 0.31f) + seed * 0.00047f);
        bool spawnTree = plainsTreePrefab != null && treeNoise > 1f - plainsTreeChance;
        if (spawnTree)
            CreatePreviewPropOnTile(parent, plainsTreePrefab, cube.transform);
    }

    void GeneratePreviewDesertTile(Transform parent, int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ, int seed)
    {
        float groundNoise = Mathf.PerlinNoise((x / noiseScale) + offsetX, (z / noiseScale) + offsetZ);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider col = cube.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Material picked = PickDesertGroundMaterial(groundNoise);
        ApplyPreviewMaterial(cube, picked != null ? picked : baseMat);

        cube.transform.SetParent(parent, false);
        cube.transform.localScale = new Vector3(tileSizeWorld, tileSizeWorld, tileSizeWorld);
        cube.transform.localPosition = new Vector3(xPos, tileSizeWorld * 0.5f, zPos);
        cube.name = $"PreviewTile_{x}_{z}";
        if (ignoreRaycastLayer != -1) cube.layer = ignoreRaycastLayer;

        float cactusNoise = Mathf.PerlinNoise((x * 0.29f) + seed * 0.00061f, (z * 0.29f) + seed * 0.00079f);
        bool spawnCactus = cactusPrefab != null && cactusNoise > 1f - desertCactusChance;
        if (spawnCactus)
            CreatePreviewPropOnTile(parent, cactusPrefab, cube.transform);
    }

    void GeneratePreviewForestTile(Transform parent, int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ, int seed)
    {
        float groundNoise = Mathf.PerlinNoise((x / noiseScale) + offsetX, (z / noiseScale) + offsetZ);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider col = cube.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Material picked = PickForestGroundMaterial(groundNoise);
        ApplyPreviewMaterial(cube, picked != null ? picked : baseMat);

        cube.transform.SetParent(parent, false);
        cube.transform.localScale = new Vector3(tileSizeWorld, tileSizeWorld, tileSizeWorld);
        cube.transform.localPosition = new Vector3(xPos, tileSizeWorld * 0.5f, zPos);
        cube.name = $"PreviewTile_{x}_{z}";
        if (ignoreRaycastLayer != -1) cube.layer = ignoreRaycastLayer;

        float forestNoise = Mathf.PerlinNoise(
            (x / forestTreeNoiseScale) + seed * 0.00021f,
            (z / forestTreeNoiseScale) + seed * 0.00037f
        );

        bool isOpenPath = forestNoise < forestPathThreshold;
        bool spawnTree = forestNoise > forestTreeThreshold;

        if (!isOpenPath && spawnTree && forestTreePrefab != null)
            CreatePreviewPropOnTile(parent, forestTreePrefab, cube.transform);
    }

        void GenerateStructuresForChunk(int seed)
        {
            float roll = Hash01(seed, 9001);
            if (roll > chunkHasStructureChance) return;
    
            int structuresToPlace = 1 + Mathf.FloorToInt(Hash01(seed, 9002) * maxStructuresPerChunk);
    
            for (int i = 0; i < structuresToPlace; i++)
            {
                bool placedThisOne = false;
    
                for (int attempt = 0; attempt < maxStructureAttemptsPerChunk; attempt++)
                {
                    int structureSeed = seed ^ (i * 92821) ^ (attempt * 68917);
                    int type = PickStructureType(structureSeed);
    
                    if (type == 0)
                    {
                        if (TryPlaceStructure(chestPrefab, 1, 1, structureSeed, "Chest"))
                        {
                            placedThisOne = true;
                            break;
                        }
                    }
                    else if (type == 1)
                    {
                        bool rotate = Hash01(structureSeed, 44) > 0.5f;
                        int w = rotate ? 5 : 3;
                        int h = rotate ? 3 : 5;
    
                        if (TryPlaceStructure(cabinPrefab, w, h, structureSeed, "Cabin"))
                        {
                            placedThisOne = true;
                            break;
                        }
                    }
                    else if (type == 2)
                    {
                        if (TryPlaceStructure(towerPrefab, 3, 3, structureSeed, "Tower"))
                        {
                            placedThisOne = true;
                            break;
                        }
                    }
                    else if (type == 3)
                    {
                        if (TryPlaceStructure(moss1Prefab, 3, 5, structureSeed, "Moss1"))
                        {
                            placedThisOne = true;
                            break;
                        }
                    }
                    else
                    {
                        if (TryPlaceStructure(moss2Prefab, 3, 3, structureSeed, "Moss2"))
                        {
                            placedThisOne = true;
                            break;
                        }
                    }
                }
    
                if (!placedThisOne)
                    continue;
            }
        }

        int PickStructureType(int seed)
        {
            return Mathf.FloorToInt(Hash01(seed, 777) * 5f);
        }

    bool TryPlaceStructure(GameObject prefab, int sizeX, int sizeZ, int seed, string structureName)
    {
        if (prefab == null) return false;

        int maxX = tileCount - sizeX;
        int maxZ = tileCount - sizeZ;
        if (maxX < 0 || maxZ < 0) return false;

        int startX = Mathf.FloorToInt(Hash01(seed, 101) * (maxX + 1));
        int startZ = Mathf.FloorToInt(Hash01(seed, 202) * (maxZ + 1));

        for (int ring = 0; ring < tileCount; ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    if (Mathf.Abs(dx) != ring && Mathf.Abs(dz) != ring) continue;

                    int x = startX + dx;
                    int z = startZ + dz;

                    if (!CanPlaceStructureFootprint(x, z, sizeX, sizeZ))
                        continue;

                    PlaceStructure(prefab, x, z, sizeX, sizeZ, structureName);
                    return true;
                }
            }
        }

        return false;
    }

    bool CanPlaceStructureFootprint(int startX, int startZ, int sizeX, int sizeZ)
    {
        if (startX < 0 || startZ < 0) return false;
        if (startX + sizeX > tileCount || startZ + sizeZ > tileCount) return false;

        float firstTop = -999f;

        for (int x = startX; x < startX + sizeX; x++)
        {
            for (int z = startZ; z < startZ + sizeZ; z++)
            {
                if (!isGreen[x, z]) return false;
                if (tiles[x, z] == null) return false;

                float topY = tiles[x, z].position.y + (tiles[x, z].lossyScale.y * 0.5f);

                if (firstTop < -100f) firstTop = topY;
                if (Mathf.Abs(topY - firstTop) > 0.05f) return false;
            }
        }

        return true;
    }

    void PlaceStructure(GameObject prefab, int startX, int startZ, int sizeX, int sizeZ, string structureName)
    {
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        float topY = 0f;

        for (int x = startX; x < startX + sizeX; x++)
        {
            for (int z = startZ; z < startZ + sizeZ; z++)
            {
                Transform t = tiles[x, z];
                Vector3 p = t.position;

                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;

                topY = t.position.y + (t.lossyScale.y * 0.5f);
                MarkTileBlocked(x, z);
            }
        }

        Vector3 center = new Vector3(
            (minX + maxX) * 0.5f,
            topY,
            (minZ + maxZ) * 0.5f
        );

        GameObject obj = Instantiate(prefab, transform);
        obj.name = structureName;
        obj.transform.position = center;
        obj.transform.rotation = Quaternion.identity;
    }

    float Hash01(int seed, int salt)
    {
        unchecked
        {
            int h = seed;
            h = (h * 397) ^ salt;
            h ^= (h << 13);
            h ^= (h >> 17);
            h ^= (h << 5);

            uint u = (uint)h;
            return (u % 100000) / 100000f;
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

    void CreatePreviewPropOnTile(Transform parent, GameObject prefab, Transform tile)
    {
        GameObject obj = Instantiate(prefab, parent);
        obj.name = $"Preview_{prefab.name}";

        float tileTopY = tile.localPosition.y + (tile.localScale.y * 0.5f);

        obj.transform.localPosition = new Vector3(
            tile.localPosition.x,
            tileTopY,
            tile.localPosition.z
        );

        obj.transform.localRotation = Quaternion.identity;

        Collider[] cols = obj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            Destroy(cols[i]);

        ApplyPreviewMaterialsRecursively(obj);

        if (ignoreRaycastLayer != -1)
            SetLayerRecursively(obj, ignoreRaycastLayer);
    }

    void ApplyPreviewMaterial(GameObject go, Material source)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;

        r.sharedMaterial = GetPreviewMaterial(source);
    }

    void ApplyPreviewMaterialsRecursively(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] src = renderers[i].sharedMaterials;
            Material[] dst = new Material[src.Length];

            for (int m = 0; m < src.Length; m++)
                dst[m] = GetPreviewMaterial(src[m]);

            renderers[i].sharedMaterials = dst;
        }
    }

    Material GetPreviewMaterial(Material source)
    {
        if (source == null) return null;
        if (previewMaterialCache.TryGetValue(source, out Material cached)) return cached;

        Material m = new Material(source);
        MakeMaterialTransparent(m, previewAlpha);
        previewMaterialCache[source] = m;
        return m;
    }

    void MakeMaterialTransparent(Material m, float alpha)
    {
        if (m == null) return;

        if (m.HasProperty("_BaseColor"))
        {
            Color c = m.GetColor("_BaseColor");
            c.a = alpha;
            m.SetColor("_BaseColor", c);
        }

        if (m.HasProperty("_Color"))
        {
            Color c = m.color;
            c.a = alpha;
            m.color = c;
        }

        if (m.HasProperty("_Surface"))
            m.SetFloat("_Surface", 1f);

        if (m.HasProperty("_Blend"))
            m.SetFloat("_Blend", 0f);

        if (m.HasProperty("_AlphaClip"))
            m.SetFloat("_AlphaClip", 0f);

        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
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
            if (playerPrefab != null)
            {
                playerObj = Instantiate(playerPrefab);
                playerObj.name = "Player";
            }
            else
            {
                playerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                playerObj.name = "Player";
            }
        }

        playerObj.transform.localScale = new Vector3(tileSizeWorld, tileSizeWorld, tileSizeWorld);

        HitPulse pulse = playerObj.GetComponent<HitPulse>();
        if (pulse == null) pulse = playerObj.AddComponent<HitPulse>();
        pulse.RefreshBaseScale();

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
        UpdatePlayerHealthUI(p.currentHearts, p.maxHearts);

        TileCutout cut = FindObjectOfType<TileCutout>();
        if (cut != null)
        {
            cut.target = playerObj.transform;
            cut.enabled = (currentBiome == BiomeType.Mountains);
        }
    }

    void SpawnEnemiesForChunk(int chunkSeed)
    {
        if (playerObj == null) return;
        if (Hash01(chunkSeed, 9001) > enemySpawnChance) return;

        int minCount = Mathf.Clamp(minEnemiesPerChunk, 1, 3);
        int maxCount = Mathf.Clamp(maxEnemiesPerChunk, minCount, 3);
        int enemyCount = Mathf.RoundToInt(Mathf.Lerp(minCount, maxCount, Hash01(chunkSeed, 9002)));

        List<Vector2Int> candidates = new List<Vector2Int>();
        Vector2Int playerCoords = GetPlayerTileCoords();

        for (int x = 0; x < tileCount; x++)
        {
            for (int z = 0; z < tileCount; z++)
            {
                if (!IsTileWalkable(x, z)) continue;
                if (playerCoords == new Vector2Int(x, z)) continue;
                if (GetTileDistance(playerCoords, new Vector2Int(x, z)) <= 2) continue;
                candidates.Add(new Vector2Int(x, z));
            }
        }

        for (int i = 0; i < enemyCount && candidates.Count > 0; i++)
        {
            int pickSeed = chunkSeed + (i * 31);
            int index = Mathf.FloorToInt(Hash01(pickSeed, 9003 + i) * candidates.Count);
            index = Mathf.Clamp(index, 0, candidates.Count - 1);

            Vector2Int coords = candidates[index];
            candidates.RemoveAt(index);

            SpawnEnemyAt(coords);

            for (int c = candidates.Count - 1; c >= 0; c--)
                if (GetTileDistance(coords, candidates[c]) <= 1)
                    candidates.RemoveAt(c);
        }
    }

    void SpawnEnemyAt(Vector2Int coords)
    {
        Transform tile = GetTileAt(coords.x, coords.y);
        if (tile == null) return;

        GameObject enemyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemyObj.name = $"Enemy_{coords.x}_{coords.y}";
        enemyObj.transform.SetParent(transform, true);
        enemyObj.transform.localScale = Vector3.one * (mapSize / tileCount);

        Renderer rend = enemyObj.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = Color.red;

        Enemy enemy = enemyObj.AddComponent<Enemy>();
        enemy.Setup(this, coords);
        enemy.MoveToTile(tile, coords);
        enemies.Add(enemy);
    }

    public void ResolveEnemyTurn()
    {
        Player player = playerObj != null ? playerObj.GetComponent<Player>() : null;
        if (player == null) return;

        Vector2Int playerCoords = GetPlayerTileCoords();
        List<Enemy> activeEnemies = new List<Enemy>(enemies);

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            Enemy enemy = activeEnemies[i];
            if (enemy == null) continue;

            Vector2Int previousCoords = enemy.tileCoords;
            Vector2Int nextStep = GetNextEnemyStep(enemy.tileCoords, playerCoords);
            if (nextStep != enemy.tileCoords)
            {
                Transform nextTile = GetTileAt(nextStep.x, nextStep.y);
                if (nextTile != null)
                {
                    Transform previousTile = GetTileAt(previousCoords.x, previousCoords.y);
                    if (previousTile != null)
                        AddEnemyMoveTrail(previousTile);
                    enemy.MoveToTile(nextTile, nextStep);
                }
            }

            int distanceToPlayer = GetCombatDistance(enemy.tileCoords, playerCoords);
            if (distanceToPlayer <= enemyAttackRange)
                player.TakeDamage(enemyDamage);
        }

        UpdateEnemyAttackIndicators();
    }

    Vector2Int GetNextEnemyStep(Vector2Int from, Vector2Int playerCoords)
    {
        int bestDistance = GetTileDistance(from, playerCoords);
        List<Vector2Int> bestCandidates = new List<Vector2Int>();
        List<Vector2Int> pressureCandidates = new List<Vector2Int>();

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;

                Vector2Int candidate = new Vector2Int(from.x + dx, from.y + dz);
                if (!IsTileWalkable(candidate.x, candidate.y)) continue;
                if (IsEnemyAt(candidate.x, candidate.y)) continue;
                if (candidate == playerCoords) continue;

                int dist = GetTileDistance(candidate, playerCoords);
                if (dist > bestDistance) continue;

                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestCandidates.Clear();
                }

                if (!bestCandidates.Contains(candidate))
                    bestCandidates.Add(candidate);

                int combatDist = GetCombatDistance(candidate, playerCoords);
                if (combatDist <= enemyAttackRange + 1 && !pressureCandidates.Contains(candidate))
                    pressureCandidates.Add(candidate);
            }
        }

        if (bestCandidates.Count == 0)
            return from;

        int seed = unchecked(
            from.x * 92821 ^
            from.y * 68917 ^
            playerCoords.x * 1237 ^
            playerCoords.y * 3253 ^
            worldSeed
        );

        if (pressureCandidates.Count > 0 && Hash01(seed, 501) > 0.45f)
        {
            int pressurePick = Mathf.Abs(seed + 13) % pressureCandidates.Count;
            return pressureCandidates[pressurePick];
        }

        int pick = Mathf.Abs(seed) % bestCandidates.Count;
        return bestCandidates[pick];
    }

    public bool TryGetEnemyFromHit(Transform hitTransform, out Enemy enemy)
    {
        enemy = null;

        Transform current = hitTransform;
        while (current != null)
        {
            enemy = current.GetComponent<Enemy>();
            if (enemy != null) return true;
            current = current.parent;
        }

        return false;
    }

    public bool IsEnemyInPlayerAttackRange(Enemy enemy, int attackRange)
    {
        if (enemy == null) return false;
        return GetTileDistance(GetPlayerTileCoords(), enemy.tileCoords) <= attackRange;
    }

    public void UpdateEnemyAttackIndicators()
    {
        Vector2Int playerCoords = GetPlayerTileCoords();
        int playerAttackRange = enemyAttackRange;

        if (playerObj != null)
        {
            Player player = playerObj.GetComponent<Player>();
            if (player != null)
                playerAttackRange = player.attackRange;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null) continue;

            bool inRange = GetTileDistance(playerCoords, enemy.tileCoords) <= playerAttackRange;
            enemy.SetAttackIndicatorVisible(inRange);
        }
    }

    public bool IsEnemyAt(int x, int z)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null) continue;
            if (enemy.tileCoords.x == x && enemy.tileCoords.y == z) return true;
        }

        return false;
    }

    public void RemoveEnemy(Enemy enemy)
    {
        enemies.Remove(enemy);
    }

    public bool IsTileWalkable(int x, int z)
    {
        if (x < 0 || z < 0 || x >= tileCount || z >= tileCount) return false;
        if (tiles == null || tiles[x, z] == null) return false;
        return tiles[x, z].gameObject.layer == walkableLayer;
    }

    public Transform GetTileAt(int x, int z)
    {
        if (x < 0 || z < 0 || x >= tileCount || z >= tileCount) return null;
        return tiles != null ? tiles[x, z] : null;
    }

    public Vector2Int GetPlayerTileCoords()
    {
        Transform tile = GetTileUnderWorldPosition(playerObj != null ? playerObj.transform.position : Vector3.zero);
        if (tile != null && TryParseTileCoords(tile.name, out int x, out int z))
            return new Vector2Int(x, z);

        return Vector2Int.zero;
    }

    Transform GetTileUnderWorldPosition(Vector3 worldPos)
    {
        Vector3 origin = worldPos + Vector3.up * 5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return null;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;

            Transform t = hits[i].collider.transform;
            if (t != null && t.name.StartsWith("Tile_"))
                return t;
        }

        return null;
    }

    public bool TryParseTileCoords(string tileName, out int x, out int z)
    {
        x = 0;
        z = 0;

        if (!tileName.StartsWith("Tile_")) return false;

        string[] parts = tileName.Split('_');
        if (parts.Length < 3) return false;

        return int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z);
    }

    public int GetTileDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    public int GetCombatDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
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

    public float playerYOffset;

    public void PlacePlayerOnTile(Transform tile)
    {
        float tileTopY = tile.position.y + (tile.lossyScale.y * 0.5f);
        float playerHalfY = playerObj.transform.localScale.y * 0.5f;

        playerObj.transform.position = new Vector3(
            tile.position.x,
            tileTopY + playerHalfY + playerYOffset,
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
    
        void GenerateRiverTile(int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ, int seed)
        {
            float groundNoise = Mathf.PerlinNoise((x / noiseScale) + offsetX, (z / noiseScale) + offsetZ);
    
            float riverLine = Mathf.PerlinNoise(
                (x / riverNoiseScale) + offsetX + 100f,
                (z / riverNoiseScale) + offsetZ + 200f
            );
    
            float riverValue = Mathf.Abs(riverLine - 0.5f);
            bool isWater = riverValue < riverWidth;
    
            float totalHeight = isWater ? Mathf.Max(0.1f, tileSizeWorld - riverDepth) : tileSizeWorld;
    
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Renderer rend = cube.GetComponent<Renderer>();
    
            Material picked = isWater ? PickRiverWaterMaterial(groundNoise) : PickPlainsGroundMaterial(groundNoise);
    
            if (rend != null)
                rend.sharedMaterial = picked != null ? picked : baseMat;
    
            cube.transform.SetParent(transform, false);
            cube.transform.localScale = new Vector3(tileSizeWorld, totalHeight, tileSizeWorld);
            cube.transform.localPosition = new Vector3(xPos, totalHeight * 0.5f, zPos);
            cube.name = $"Tile_{x}_{z}";
    
            if (isWater)
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
    
        void GeneratePreviewRiverTile(Transform parent, int x, int z, float xPos, float zPos, float tileSizeWorld, float offsetX, float offsetZ, int seed)
        {
            float groundNoise = Mathf.PerlinNoise((x / noiseScale) + offsetX, (z / noiseScale) + offsetZ);
    
            float riverLine = Mathf.PerlinNoise(
                (x / riverNoiseScale) + offsetX + 100f,
                (z / riverNoiseScale) + offsetZ + 200f
            );
    
            float riverValue = Mathf.Abs(riverLine - 0.5f);
            bool isWater = riverValue < riverWidth;
    
            float totalHeight = isWater ? Mathf.Max(0.1f, tileSizeWorld - riverDepth) : tileSizeWorld;
    
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    
            Collider col = cube.GetComponent<Collider>();
            if (col != null) Destroy(col);
    
            Material picked = isWater ? PickRiverWaterMaterial(groundNoise) : PickPlainsGroundMaterial(groundNoise);
            ApplyPreviewMaterial(cube, picked != null ? picked : baseMat);
    
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = new Vector3(tileSizeWorld, totalHeight, tileSizeWorld);
            cube.transform.localPosition = new Vector3(xPos, totalHeight * 0.5f, zPos);
            cube.name = $"PreviewTile_{x}_{z}";
    
            if (ignoreRaycastLayer != -1)
                cube.layer = ignoreRaycastLayer;
        }
    Material PickRiverWaterMaterial(float noise01)
    {
        int idx = Mathf.FloorToInt(noise01 * 3f);
        if (idx > 2) idx = 2;

        if (idx == 0) return waterMatA;
        if (idx == 1) return waterMatB;
        return waterMatC;
    }
}
