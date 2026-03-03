using System.Collections.Generic;
using UnityEngine;

public class TileCutout : MonoBehaviour
{
    public Transform target;
    public Camera cam;

    [Header("Cutout")]
    public float radiusWorld = 3f;
    public bool onlyIfOccluding = true;

    [Header("Only affect this layer")]
    public string blockedLayerName = "Blocked";

    private int blockedLayer = -1;
    private readonly HashSet<Renderer> hidden = new HashSet<Renderer>();

    void Awake()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        blockedLayer = LayerMask.NameToLayer(blockedLayerName);
    }

    void LateUpdate()
    {
        if (target == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) target = p.transform;
        }

        if (target == null || cam == null) return;

        Restore();

        MapGenerator gen = FindObjectOfType<MapGenerator>();
        if (gen == null) return;

        Vector3 center = target.position;
        float r2 = radiusWorld * radiusWorld;

        Vector3 camPos = cam.transform.position;
        float playerDist = Vector3.Distance(camPos, center);

        Transform root = gen.transform;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform tile = root.GetChild(i);
            if (tile == null) continue;
            if (!tile.name.StartsWith("Tile_")) continue;

            if (blockedLayer != -1 && tile.gameObject.layer != blockedLayer) continue;

            Vector3 p = tile.position; p.y = 0f;
            Vector3 c = center; c.y = 0f;

            if ((p - c).sqrMagnitude > r2) continue;

            if (onlyIfOccluding)
            {
                float tileDist = Vector3.Distance(camPos, tile.position);
                if (tileDist >= playerDist) continue;
            }

            Renderer r = tile.GetComponent<Renderer>();
            if (r == null) continue;

            r.enabled = false;
            hidden.Add(r);
        }
    }

    void Restore()
    {
        foreach (var r in hidden)
            if (r != null) r.enabled = true;
        hidden.Clear();
    }

    void OnDisable()
    {
        Restore();
    }
}