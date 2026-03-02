using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Refs")]
    public MapGenerator mapGen;
    public Camera cam;

    [Header("Turn (Debug)")]
    public bool turn = true;
    public bool debugAlwaysTrue = true;

    [Header("Movement")]
    [Range(1, 10)]
    public int moveRange = 3;
    public bool allowDiagonal = true;

    [Header("Highlight")]
    public Material highlightBlueMat;

    private readonly Dictionary<Renderer, Material> originalMats = new Dictionary<Renderer, Material>();
    private readonly HashSet<Transform> highlightedTiles = new HashSet<Transform>();

    private class ExitInfo
    {
        public GameObject obj;
        public MapGenerator.EntryDirection side;
        public int exitX;
        public int exitZ;
    }

    private readonly List<ExitInfo> exits = new List<ExitInfo>();

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (debugAlwaysTrue) turn = true;

        if (!turn)
        {
            ClearHighlights();
            DestroyExits();
            return;
        }

        if (highlightedTiles.Count == 0)
        {
            HighlightMovable();
            UpdateExitTiles();
        }

        if (Input.GetMouseButtonDown(0))
            HandleClick();
    }

    void HandleClick()
    {
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

        Transform t = hit.collider != null ? hit.collider.transform : null;
        if (t == null) return;

        for (int i = 0; i < exits.Count; i++)
        {
            if (exits[i].obj != null && t.gameObject == exits[i].obj)
            {
                mapGen.TravelToNeighborChunk(exits[i].side, exits[i].exitX, exits[i].exitZ);
                DestroyExits();
                ClearHighlights();
                return;
            }
        }

        if (!t.name.StartsWith("Tile_")) return;
        if (!highlightedTiles.Contains(t)) return;

        MovePlayerToTile(t);

        ClearHighlights();
        HighlightMovable();
        UpdateExitTiles();
    }

    void HighlightMovable()
    {
        ClearHighlights();

        Transform currentTile = GetTileUnderPlayer();
        if (currentTile == null) return;

        if (!TryParseTileCoords(currentTile.name, out int cx, out int cz))
            return;

        for (int i = 0; i < mapGen.transform.childCount; i++)
        {
            Transform tile = mapGen.transform.GetChild(i);
            if (!tile.name.StartsWith("Tile_")) continue;

            if (!TryParseTileCoords(tile.name, out int tx, out int tz))
                continue;

            if (!IsGreenTile(tile))
                continue;

            int dx = Mathf.Abs(tx - cx);
            int dz = Mathf.Abs(tz - cz);

            bool inRange = allowDiagonal ? (Mathf.Max(dx, dz) <= moveRange) : (dx + dz <= moveRange);
            if (!inRange) continue;

            Renderer r = tile.GetComponent<Renderer>();
            if (r == null) continue;

            if (!originalMats.ContainsKey(r))
                originalMats[r] = r.sharedMaterial;

            if (highlightBlueMat != null)
                r.sharedMaterial = highlightBlueMat;

            highlightedTiles.Add(tile);
        }
    }

    void UpdateExitTiles()
    {
        DestroyExits();

        Transform currentTile = GetTileUnderPlayer();
        if (currentTile == null) return;

        if (!TryParseTileCoords(currentTile.name, out int cx, out int cz)) return;

        int n = mapGen.tileCount;
        float tileSizeWorld = GetTileSizeWorld();

        if (cx == 0) CreateExit(MapGenerator.EntryDirection.West, cx, cz, Vector3.left, tileSizeWorld, currentTile.position);
        if (cx == n - 1) CreateExit(MapGenerator.EntryDirection.East, cx, cz, Vector3.right, tileSizeWorld, currentTile.position);
        if (cz == 0) CreateExit(MapGenerator.EntryDirection.South, cx, cz, Vector3.back, tileSizeWorld, currentTile.position);
        if (cz == n - 1) CreateExit(MapGenerator.EntryDirection.North, cx, cz, Vector3.forward, tileSizeWorld, currentTile.position);
    }

    void CreateExit(MapGenerator.EntryDirection side, int exitX, int exitZ, Vector3 dir, float tileSizeWorld, Vector3 fromPos)
    {
        GameObject e = GameObject.CreatePrimitive(PrimitiveType.Cube);
        e.name = $"Exit_{side}";
        e.transform.SetParent(transform, true);

        Renderer r = e.GetComponent<Renderer>();
        if (r != null && highlightBlueMat != null)
            r.sharedMaterial = highlightBlueMat;

        e.transform.localScale = Vector3.one * tileSizeWorld * 4;

        Vector3 pos = fromPos + dir * tileSizeWorld;
        pos.y = tileSizeWorld * 0.5f;
        e.transform.position = pos;

        exits.Add(new ExitInfo { obj = e, side = side, exitX = exitX, exitZ = exitZ });
    }

    void DestroyExits()
    {
        for (int i = 0; i < exits.Count; i++)
            if (exits[i].obj != null) Destroy(exits[i].obj);
        exits.Clear();
    }

    void MovePlayerToTile(Transform tile)
    {
        float tileTopY = tile.position.y + (tile.lossyScale.y * 0.5f);
        float playerHalf = transform.lossyScale.y * 0.5f;

        transform.position = new Vector3(
            tile.position.x,
            tileTopY + playerHalf,
            tile.position.z
        );
    }

    Transform GetTileUnderPlayer()
    {
        Vector3 origin = transform.position + Vector3.up * 5f;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 100f);
        if (hits == null || hits.Length == 0) return null;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;
            if (hits[i].collider.transform == transform) continue;

            Transform t = hits[i].collider.transform;
            if (t != null && t.name.StartsWith("Tile_"))
                return t;
        }

        return null;
    }

    bool IsGreenTile(Transform tile)
    {
        Renderer r = tile.GetComponent<Renderer>();
        if (r == null) return false;

        Material m = r.sharedMaterial;
        return m == mapGen.greenMatA || m == mapGen.greenMatB || m == mapGen.greenMatC;
    }

    bool TryParseTileCoords(string name, out int x, out int z)
    {
        x = 0; z = 0;

        if (!name.StartsWith("Tile_")) return false;

        string[] parts = name.Split('_');
        if (parts.Length < 3) return false;

        return int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z);
    }

    float GetTileSizeWorld()
    {
        Transform t00 = mapGen.transform.Find("Tile_0_0");
        Transform t10 = mapGen.transform.Find("Tile_1_0");
        if (t00 != null && t10 != null)
            return Mathf.Abs(t10.position.x - t00.position.x);

        return 1f;
    }

    void ClearHighlights()
    {
        foreach (var kvp in originalMats)
        {
            if (kvp.Key != null)
                kvp.Key.sharedMaterial = kvp.Value;
        }
        originalMats.Clear();
        highlightedTiles.Clear();
    }
}