using System.Collections.Generic;
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

    [Header("Ground Variants")]
    public Material greenMatA;
    public Material greenMatB;
    public Material greenMatC;

    [Header("Elevation Materials")]
    public Material brownMat;
    public Material greyMat;
    public Material whiteMat;

    [Header("Debug Materials")]
    public Material blockedRedMat;

    [Header("Player Settings")]
    public Material playerMat;
    public Material highlightBlueMat;
    [Range(1, 20)]
    public int playerMoveRange = 3;
    public bool allowDiagonalMovement = true;

    [Header("Camera Focus")]
    public bool focusCameraOnPlayer;
    public CameraOrbit cameraOrbit;

    [Header("Spawn Rules")]
    public bool require3x3GreenForStart = true;

    [Header("Debug")]
    public bool doGen;

    public enum EntryDirection { North, South, East, West }

    private GameObject playerObj;

    private Transform[,] tiles;
    private bool[,] isGreen;

    private Vector2Int currentChunk = Vector2Int.zero;

    void Start()
    {
        LoadChunk(currentChunk, null, null);
        ApplyCameraTargetIfEnabled();
    }

    void Update()
    {
        if (doGen)
        {
            doGen = false;
            LoadChunk(currentChunk, null, null);
            ApplyCameraTargetIfEnabled();
        }
    }

    // exitSide = which edge you leave CURRENT chunk from
    // exitX/exitZ = tile coords on CURRENT chunk you are standing on when you leave
    public void TravelToNeighborChunk(EntryDirection exitSide, int exitX, int exitZ)
    {
        if (exitSide == EntryDirection.West)  currentChunk += new Vector2Int(-1, 0);
        if (exitSide == EntryDirection.East)  currentChunk += new Vector2Int( 1, 0);
        if (exitSide == EntryDirection.South) currentChunk += new Vector2Int( 0,-1);
        if (exitSide == EntryDirection.North) currentChunk += new Vector2Int( 0, 1);

        // You enter the next chunk FROM the opposite side
        EntryDirection entrySide =
            exitSide == EntryDirection.West  ? EntryDirection.East  :
            exitSide == EntryDirection.East  ? EntryDirection.West  :
            exitSide == EntryDirection.South ? EntryDirection.North :
                                               EntryDirection.South;

        // Desired entry tile is the "same row/column" but on the opposite edge
        int entryX = exitX;
        int entryZ = exitZ;

        if (exitSide == EntryDirection.East) entryX = 0;
        if (exitSide == EntryDirection.West) entryX = tileCount - 1;
        if (exitSide == EntryDirection.North) entryZ = 0;
        if (exitSide == EntryDirection.South) entryZ = tileCount - 1;

        LoadChunk(currentChunk, entrySide, new Vector2Int(entryX, entryZ));
        ApplyCameraTargetIfEnabled();
    }

    void LoadChunk(Vector2Int chunkCoord, EntryDirection? entrySide, Vector2Int? desiredEntry)
    {
        int chunkSeed = GetChunkSeed(chunkCoord.x, chunkCoord.y);
        GenerateWithSeed(chunkSeed);

        if (desiredEntry.HasValue && entrySide.HasValue)
            SpawnPlayerAtDesiredOrEdgeFallback(desiredEntry.Value, entrySide.Value);
        else
            SpawnPlayerOnSafeGreenCell();
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

    void GenerateWithSeed(int seed)
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
                isGreen[x, z] = picked == greenMatA || picked == greenMatB || picked == greenMatC;
            }
        }

        EnsurePlayerExistsAndHooked();
    }

    void EnsurePlayerExistsAndHooked()
    {
        float tileSizeWorld = mapSize / tileCount;

        if (playerObj == null)
        {
            playerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerObj.name = "Player";
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
        p.moveRange = playerMoveRange;
        p.allowDiagonal = allowDiagonalMovement;
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

        Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
        PlacePlayerOnTile(tiles[chosen.x, chosen.y]);
    }

    // KEY FIX: if desired is blocked, find green ON THE ENTRY EDGE FIRST (same edge line),
    // so backtracking always returns to the correct neighbor.
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

        // 1) Try same ENTRY EDGE, closest to the intended z/x
        // entrySide tells which edge we are coming in on:
        // West edge = x=0, East edge = x=n-1, South edge = z=0, North edge = z=n-1
        List<Vector2Int> edge = GetEdgeCoords(entrySide);

        // sort by distance along edge to intended spot
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

        // 2) If entire edge is blocked, fall back to nearest green anywhere
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