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

    private Material blueMat;

    private readonly Dictionary<Renderer, Material> originalMats = new Dictionary<Renderer, Material>();
    private readonly HashSet<Transform> highlightedTiles = new HashSet<Transform>();

    private GameObject exitObj;

    void Awake()
    {
        if (cam == null) cam = Camera.main;

        Shader s = Shader.Find("Standard");
        blueMat = new Material(s);
        blueMat.color = Color.blue;
    }

    void Update()
    {
        if (debugAlwaysTrue) turn = true;

        if (!turn)
        {
            ClearHighlights();
            DestroyExit();
            return;
        }

        if (highlightedTiles.Count == 0)
        {
            HighlightMovable();
            UpdateExitTile();
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

        if (exitObj != null && t.gameObject == exitObj)
        {
            TryLoadNextChunkFromExit();
            return;
        }

        if (!t.name.StartsWith("Tile_")) return;
        if (!highlightedTiles.Contains(t)) return;

        MovePlayerToTile(t);

        ClearHighlights();
        HighlightMovable();
        UpdateExitTile();
    }

    void TryLoadNextChunkFromExit()
    {
        Transform currentTile = GetTileUnderPlayer();
        if (currentTile == null) return;

        if (!TryParseTileCoords(currentTile.name, out int cx, out int cz)) return;

        int n = mapGen.tileCount;

        if (cx == 0)
            mapGen.RegenerateAndPlacePlayerOnEdge(MapGenerator.EntryDirection.East);  // moved West, came from East
        else if (cx == n - 1)
            mapGen.RegenerateAndPlacePlayerOnEdge(MapGenerator.EntryDirection.West);  // moved East, came from West
        else if (cz == 0)
            mapGen.RegenerateAndPlacePlayerOnEdge(MapGenerator.EntryDirection.North); // moved South, came from North
        else if (cz == n - 1)
            mapGen.RegenerateAndPlacePlayerOnEdge(MapGenerator.EntryDirection.South); // moved North, came from South
        else
            return;

        DestroyExit();
        ClearHighlights();
        HighlightMovable();
        UpdateExitTile();
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

            r.sharedMaterial = blueMat;
            highlightedTiles.Add(tile);
        }
    }

    void UpdateExitTile()
    {
        DestroyExit();

        Transform currentTile = GetTileUnderPlayer();
        if (currentTile == null) return;

        if (!TryParseTileCoords(currentTile.name, out int cx, out int cz)) return;

        int n = mapGen.tileCount;
        float tileSizeWorld = GetTileSizeWorld();

        Vector3 dir = Vector3.zero;

        if (cx == 0) dir = Vector3.left;
        else if (cx == n - 1) dir = Vector3.right;
        else if (cz == 0) dir = Vector3.back;
        else if (cz == n - 1) dir = Vector3.forward;
        else return;

        exitObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        exitObj.name = "Exit";
        exitObj.transform.SetParent(mapGen.transform, false);

        Renderer r = exitObj.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = blueMat;

        exitObj.transform.localScale = Vector3.one * tileSizeWorld;

        Vector3 pos = currentTile.position + dir * tileSizeWorld;
        pos.y = tileSizeWorld * 0.5f;
        exitObj.transform.position = pos;
    }

    void DestroyExit()
    {
        if (exitObj != null)
            Destroy(exitObj);
        exitObj = null;
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
        return mapGen != null ? (GetMapSize() / mapGen.tileCount) : 1f;
    }

    float GetMapSize()
    {
        Transform t00 = mapGen.transform.Find("Tile_0_0");
        Transform t10 = mapGen.transform.Find("Tile_1_0");
        if (t00 != null && t10 != null)
            return Mathf.Abs(t10.localPosition.x - t00.localPosition.x) * mapGen.tileCount;

        return 10f;
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