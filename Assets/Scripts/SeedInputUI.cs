using TMPro;
using UnityEngine;

public class SeedInputUI : MonoBehaviour
{
    public TMP_InputField seedInput;

    void Start()
    {
        if (WorldSeedManager.Instance == null)
        {
            GameObject go = new GameObject("WorldSeedManager");
            go.AddComponent<WorldSeedManager>();
        }

        if (seedInput != null)
            seedInput.text = WorldSeedManager.Instance.selectedSeed.ToString();
    }

    public void OnSeedChanged(string value)
    {
        if (WorldSeedManager.Instance != null)
            WorldSeedManager.Instance.SetSeedFromString(value);
    }
}