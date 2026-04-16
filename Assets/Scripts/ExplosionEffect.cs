using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionEffect : MonoBehaviour
{
    public static void Spawn(Vector3 position, Color color, float size = 1f)
    {
        GameObject root = new GameObject("ExplosionEffect");
        root.transform.position = position;

        ExplosionEffect effect = root.AddComponent<ExplosionEffect>();
        effect.StartCoroutine(effect.Play(color, size));
    }

    IEnumerator Play(Color color, float size)
    {
        List<GameObject> pieces = new List<GameObject>();
        Vector3[] dirs =
        {
            Vector3.up,
            Vector3.down,
            Vector3.left,
            Vector3.right,
            Vector3.forward,
            Vector3.back
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = $"ExplosionPiece_{i}";
            piece.transform.SetParent(transform, false);
            piece.transform.localPosition = Vector3.zero;
            piece.transform.localScale = Vector3.one * (0.28f * size);

            Collider col = piece.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = piece.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.color = color;
                rend.sharedMaterial = mat;
            }

            pieces.Add(piece);
        }

        float duration = 0.35f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;

                Vector3 dir = dirs[i].normalized;
                pieces[i].transform.localPosition = dir * (t * 1.35f * size);
                pieces[i].transform.localScale = Vector3.one * Mathf.Lerp(0.28f * size, 0.02f, t);
                pieces[i].transform.Rotate(new Vector3(180f, 200f, 150f) * Time.deltaTime, Space.Self);
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
