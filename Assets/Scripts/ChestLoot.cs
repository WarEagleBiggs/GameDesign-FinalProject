using UnityEngine;

public class ChestLoot : MonoBehaviour
{
    public void OpenChest(MapGenerator mGen)
    {
        Debug.Log("ITEM GIVEN");

        if (mGen != null)
            mGen.coins += 10;

        gameObject.SetActive(false);
    }
}