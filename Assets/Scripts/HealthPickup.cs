using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    public MapGenerator mapGen;
    public Vector2Int tileCoords;
    public int heartsContained = 1;
    public float spinSpeed = 35f;

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    public void Setup(MapGenerator generator, Vector2Int coords, int hearts)
    {
        mapGen = generator;
        tileCoords = coords;
        heartsContained = Mathf.Clamp(hearts, 1, 3);
        RebuildVisual();
    }

    public bool TryCollect(Player player)
    {
        if (player == null) return false;
        if (player.currentHearts >= player.maxHearts) return false;

        int restoreAmount = Mathf.Min(heartsContained, player.maxHearts - player.currentHearts);
        if (restoreAmount <= 0) return false;

        player.Heal(restoreAmount);

        if (mapGen != null)
            mapGen.RemoveHealthPickup(this);

        Destroy(gameObject);
        return true;
    }

    void RebuildVisual()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        GameObject heartRoot = new GameObject("HeartVisual");
        heartRoot.transform.SetParent(transform, false);
        heartRoot.transform.localPosition = Vector3.zero;
        BuildVoxelHeart(heartRoot.transform);
    }

    void BuildVoxelHeart(Transform root)
    {
        Vector2Int[] outline =
        {
            new Vector2Int(-3, 4), new Vector2Int(-2, 5), new Vector2Int(-1, 5), new Vector2Int(0, 4),
            new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(3, 4),
            new Vector2Int(-4, 3), new Vector2Int(-4, 2), new Vector2Int(-3, 1), new Vector2Int(-2, 0),
            new Vector2Int(-1, -1), new Vector2Int(0, -2), new Vector2Int(1, -1), new Vector2Int(2, 0),
            new Vector2Int(3, 1), new Vector2Int(4, 2), new Vector2Int(4, 3)
        };

        Vector2Int[] fill =
        {
            new Vector2Int(-2, 4), new Vector2Int(-1, 4), new Vector2Int(1, 4), new Vector2Int(2, 4),
            new Vector2Int(-3, 3), new Vector2Int(-2, 3), new Vector2Int(-1, 3), new Vector2Int(0, 3),
            new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(3, 3),
            new Vector2Int(-3, 2), new Vector2Int(-2, 2), new Vector2Int(-1, 2), new Vector2Int(0, 2),
            new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(3, 2),
            new Vector2Int(-2, 1), new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0),
            new Vector2Int(0, -1)
        };

        Vector2Int[] shine =
        {
            new Vector2Int(-2, 3), new Vector2Int(-1, 3),
            new Vector2Int(-2, 2), new Vector2Int(-1, 2),
            new Vector2Int(-2, 1)
        };

        const float cubeSize = 0.08f;
        const float depth = 0.08f;

        for (int i = 0; i < outline.Length; i++)
            CreateHeartCube(root, outline[i], cubeSize, depth, new Color(0.14f, 0.14f, 0.14f, 1f), "Outline");

        for (int i = 0; i < fill.Length; i++)
            CreateHeartCube(root, fill[i], cubeSize * 0.98f, depth * 0.9f, new Color(0.88f, 0.12f, 0.12f, 1f), "Fill");

        for (int i = 0; i < shine.Length; i++)
            CreateHeartCube(root, shine[i], cubeSize * 0.92f, depth * 0.92f, new Color(0.98f, 0.95f, 0.9f, 1f), "Shine");
    }

    void CreateHeartCube(Transform root, Vector2Int cell, float cubeSize, float depth, Color color, string prefix)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"{prefix}_{cell.x}_{cell.y}";
        cube.transform.SetParent(root, false);
        cube.transform.localScale = new Vector3(cubeSize, cubeSize, depth);
        cube.transform.localPosition = new Vector3(cell.x * cubeSize, cell.y * cubeSize, 0f);
        cube.layer = LayerMask.NameToLayer("Ignore Raycast");

        Collider col = cube.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = cube.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.color = color;
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
    }
}
