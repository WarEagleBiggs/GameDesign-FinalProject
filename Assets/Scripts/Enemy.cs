using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Stats")]
    public int maxHearts = 3;
    public int currentHearts = 3;

    [Header("State")]
    public MapGenerator mapGen;
    public Vector2Int tileCoords;

    private HitPulse hitPulse;

    void Awake()
    {
        hitPulse = GetComponent<HitPulse>();
        if (hitPulse == null)
            hitPulse = gameObject.AddComponent<HitPulse>();
    }

    public void Setup(MapGenerator generator, Vector2Int coords)
    {
        mapGen = generator;
        tileCoords = coords;
        currentHearts = maxHearts;
    }

    public void MoveToTile(Transform tile, Vector2Int coords)
    {
        if (mapGen == null || tile == null) return;

        tileCoords = coords;

        float tileTopY = tile.position.y + (tile.lossyScale.y * 0.5f);
        float enemyHalfY = transform.localScale.y * 0.5f;

        transform.position = new Vector3(
            tile.position.x,
            tileTopY + enemyHalfY + mapGen.enemyYOffset,
            tile.position.z
        );
    }

    public void TakeDamage(int amount)
    {
        currentHearts = Mathf.Clamp(currentHearts - amount, 0, maxHearts);
        if (hitPulse != null)
            hitPulse.PlayPulse();

        Debug.Log($"Enemy hit. Hearts left: {currentHearts}");

        if (currentHearts > 0) return;

        if (mapGen != null)
            mapGen.RemoveEnemy(this);

        Destroy(gameObject);
    }
}
