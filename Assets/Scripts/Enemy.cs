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
    private GameObject attackIndicator;
    private Material attackIndicatorMaterial;

    void Awake()
    {
        hitPulse = GetComponent<HitPulse>();
        if (hitPulse == null)
            hitPulse = gameObject.AddComponent<HitPulse>();

        EnsureAttackIndicator();
    }

    public void Setup(MapGenerator generator, Vector2Int coords)
    {
        mapGen = generator;
        tileCoords = coords;
        currentHearts = maxHearts;
        EnsureAttackIndicator();
        SetAttackIndicatorVisible(false);
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

        UpdateAttackIndicatorPosition();
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

    public void SetAttackIndicatorVisible(bool visible)
    {
        EnsureAttackIndicator();
        if (attackIndicator != null)
            attackIndicator.SetActive(visible);
    }

    void EnsureAttackIndicator()
    {
        if (attackIndicator != null) return;

        Transform found = transform.Find("AttackIndicator");
        if (found != null)
        {
            attackIndicator = found.gameObject;
            return;
        }

        attackIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        attackIndicator.name = "AttackIndicator";
        attackIndicator.transform.SetParent(transform, false);
        attackIndicator.layer = LayerMask.NameToLayer("Ignore Raycast");

        Collider col = attackIndicator.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = attackIndicator.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null)
                unlitShader = Shader.Find("Unlit/Color");

            if (unlitShader != null)
            {
                attackIndicatorMaterial = new Material(unlitShader);
                if (attackIndicatorMaterial.HasProperty("_BaseColor"))
                    attackIndicatorMaterial.SetColor("_BaseColor", Color.yellow);
                if (attackIndicatorMaterial.HasProperty("_Color"))
                    attackIndicatorMaterial.color = Color.yellow;
                rend.sharedMaterial = attackIndicatorMaterial;
            }
            else
            {
                rend.material.color = Color.yellow;
            }

            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        UpdateAttackIndicatorPosition();
        attackIndicator.SetActive(false);
    }

    void UpdateAttackIndicatorPosition()
    {
        if (attackIndicator == null) return;

        float scale = Mathf.Max(transform.localScale.x, 0.01f);
        attackIndicator.transform.localScale = new Vector3(scale * 1.1f, scale * 0.05f, scale * 1.1f);
        attackIndicator.transform.localPosition = new Vector3(0f, scale * 1.9f, 0f);
    }
}
