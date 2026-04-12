using UnityEngine;

public class WorldSeedManager : MonoBehaviour
{
    public static WorldSeedManager Instance;

    public int selectedSeed = 12345;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetSeedFromString(string value)
    {
        if (int.TryParse(value, out int parsed))
            selectedSeed = parsed;
    }

    public void SetSeed(int value)
    {
        selectedSeed = value;
    }
}