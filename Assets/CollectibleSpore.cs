using UnityEngine;

public class CollectibleSpore : MonoBehaviour, ICampInteractable, IWorldInteractionMetadata
{
    public int amount = 1;
    public float pickupRange = 1.1f;
    public string prompt = "Pick Up Spore";

    public int InteractionPriority => 0;
    public float InteractionRange => pickupRange;
    public bool CanInteract(GobboController player) => player != null;
    public Vector2 GetInteractionPoint() => transform.position;
    public string GetInteractPrompt() => prompt;

    public void Interact(GobboController player)
    {
        SporeInventory inventory = player != null ? player.GetComponent<SporeInventory>() : null;

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
