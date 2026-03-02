using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MapGenerator : MonoBehaviour
{
    [Header("Generation")]
    [Range(4, 64)]
    public int TileSize = 4;
    public bool doGen;
    [SerializeField] private float mapSize = 10f;

    public Material cubeMat;

    [Header("UI")]
    public TMP_InputField tileSizeInput;

    void Update()
    {
        if (doGen)
        {
            doGen = false;
            Generate();
        }
    }

    public void Generate()
    {
        if (tileSizeInput != null)
        {
            int parsed;
            if (int.TryParse(tileSizeInput.text, out parsed))
                TileSize = Mathf.Clamp(parsed, 4, 64);
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        float tileSizeWorld = mapSize / TileSize;
        float half = mapSize * 0.5f;

        for (int x = 0; x < TileSize; x++)
        {
            for (int z = 0; z < TileSize; z++)
            {
                float xPos = -half + tileSizeWorld * 0.5f + x * tileSizeWorld;
                float zPos = -half + tileSizeWorld * 0.5f + z * tileSizeWorld;

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

                Renderer rend = cube.GetComponent<Renderer>();
                if (rend != null && cubeMat != null) rend.sharedMaterial = cubeMat;

                cube.transform.SetParent(transform, false);
                cube.transform.localPosition = new Vector3(xPos, 0f, zPos);
                cube.transform.localScale = Vector3.one * tileSizeWorld;

                cube.name = $"Tile_{x}_{z}";
            }
        }
    }
}