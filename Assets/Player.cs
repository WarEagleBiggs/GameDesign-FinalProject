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

    [Header("Camera Occlusion Flatten (Fallout 1 style)")]
    public bool flattenWhenBlockingCamera = true;

    [Tooltip("Only tiles hit by the camera->player ray are flattened.")]
    public bool raycastOnly = true;

    [Tooltip("Use SphereCast instead of Raycast (helps if player is wider / camera angle).")]
    public bool useSphereCast = true;

    [Range(0.0f, 2.0f)]
    public float sphereRadius = 0.25f;

    [Tooltip("Only flatten these materials (non-walkable tall tiles).")]
    public bool flattenBrown = true;
    public bool flattenGrey = true;
    public bool flattenWhite = true;

    [Tooltip("Height to flatten to, as a multiple of base tile size (1 = same height as green tiles).")]
    [Range(0.25f, 3f)]
    public float flatHeightMultiplier = 1f;

    [Tooltip("Optional safety limit per move.")]
    [Range(1, 256)]
    public int maxTilesToFlattenPerMove = 64;

    [Tooltip("Physics mask for occluders. Leave as Everything unless you have special layers.")]
    public LayerMask occluderMask = ~0;

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

        RaycastHit[] hits = Physics.RaycastAll(ray, 2000f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;

            Transform ht = hits[i].collider.transform;
            if (ht == null) continue;

            for (int e = 0; e < exits.Count; e++)
            {
                if (exits[e].obj != null && ht.gameObject == exits[e].obj)
                {
                    mapGen.TravelToNeighborChunk(exits[e].side, exits[e].exitX, exits[e].exitZ);
                    DestroyExits();
                    ClearHighlights();
                    return;
                }
            }

            if (!ht.name.StartsWith("Tile_")) continue;

            if (!highlightedTiles.Contains(ht)) return;

            MovePlayerToTile(ht);

            if (flattenWhenBlockingCamera)
                FlattenTilesBlockingCamera();

            ClearHighlights();
            HighlightMovable();
            UpdateExitTiles();
            return;
        }
    }

    void FlattenTilesBlockingCamera()
    {
        if (mapGen == null || cam == null) return;

        float tileSize = GetTileSizeWorld();
        float flatHeight = tileSize * flatHeightMultiplier;

        Vector3 camPos = cam.transform.position;
        Vector3 playerPos = transform.position;

        Vector3 dir = playerPos - camPos;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return;
        dir /= dist;

        RaycastHit[] hits;

        if (raycastOnly)
        {
            if (useSphereCast)
                hits = Physics.SphereCastAll(camPos, sphereRadius, dir, dist, occluderMask, QueryTriggerInteraction.Ignore);
            else
                hits = Physics.RaycastAll(camPos, dir, dist, occluderMask, QueryTriggerInteraction.Ignore);
        }
        else
        {
            hits = Physics.OverlapSphere(playerPos, tileSize * 6f, occluderMask, QueryTriggerInteraction.Ignore) != null
                ? Physics.SphereCastAll(camPos, sphereRadius, dir, dist, occluderMask, QueryTriggerInteraction.Ignore)
                : null;
        }

        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        int flattened = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            if (flattened >= maxTilesToFlattenPerMove) return;

            Collider c = hits[i].collider;
            if (c == null) continue;

            Transform t = c.transform;
            if (t == null) continue;

            if (t == transform || t.IsChildOf(transform)) continue;

            if (!t.name.StartsWith("Tile_")) continue;

            Renderer r = t.GetComponent<Renderer>();
            if (r == null) continue;

            Material m = r.sharedMaterial;
            bool isBlockingMat =
                (flattenBrown && m == mapGen.brownMat) ||
                (flattenGrey && m == mapGen.greyMat) ||
                (flattenWhite && m == mapGen.whiteMat);

            if (!isBlockingMat) continue;

            Vector3 s = t.localScale;
            if (s.y <= flatHeight + 0.0001f) continue;

            s.y = flatHeight;
            t.localScale = s;

            Vector3 lp = t.localPosition;
            lp.y = flatHeight * 0.5f;
            t.localPosition = lp;

            flattened++;
        }
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

        e.transform.localScale = Vector3.one * tileSizeWorld * 4f;

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

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
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