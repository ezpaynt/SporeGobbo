using UnityEngine;

public class CollectibleSpore : MonoBehaviour
{
    public int amount = 1;
    public float pickupRange = 1.1f;
    public KeyCode pickupKey = KeyCode.E;

    void Update()
    {
        if (!Input.GetKeyDown(pickupKey))
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        if (Vector2.Distance(transform.position, player.transform.position) > pickupRange)
            return;

        SporeInventory inventory = player.GetComponent<SporeInventory>();

        if (inventory == null)
        {
            Debug.LogWarning("Player has no SporeInventory.");
            return;
        }

        inventory.AddSpore(amount);
        Debug.Log("Picked up spore!");

        Destroy(gameObject);
    }
}