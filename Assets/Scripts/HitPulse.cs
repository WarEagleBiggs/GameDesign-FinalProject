using System.Collections;
using UnityEngine;

public class HitPulse : MonoBehaviour
{
    [Header("Pulse")]
    public float scaleMultiplier = 1.2f;
    public float expandDuration = 0.08f;
    public float shrinkDuration = 0.12f;

    private Vector3 baseScale;
    private Coroutine pulseRoutine;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    public void RefreshBaseScale()
    {
        baseScale = transform.localScale;
    }

    public void PlayPulse()
    {
        RefreshBaseScale();

        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);

        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    IEnumerator PulseRoutine()
    {
        Vector3 enlargedScale = baseScale * scaleMultiplier;
        float timer = 0f;

        while (timer < expandDuration)
        {
            timer += Time.deltaTime;
            float t = expandDuration <= 0f ? 1f : timer / expandDuration;
            transform.localScale = Vector3.Lerp(baseScale, enlargedScale, t);
            yield return null;
        }

        timer = 0f;

        while (timer < shrinkDuration)
        {
            timer += Time.deltaTime;
            float t = shrinkDuration <= 0f ? 1f : timer / shrinkDuration;
            transform.localScale = Vector3.Lerp(enlargedScale, baseScale, t);
            yield return null;
        }

        transform.localScale = baseScale;
        pulseRoutine = null;
    }
}
